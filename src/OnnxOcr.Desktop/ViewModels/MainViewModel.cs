//-----------------------------------------------------------------------
// <copyright file="MainViewModel.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnnxOcr.App.Models;
using OnnxOcr.App.Services;
using OnnxOcr.Core;
using OnnxOcr.Core.Configuration;
using OnnxOcr.Desktop.Helpers;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace OnnxOcr.Desktop.ViewModels;

public enum InferenceDevice
{
    Cpu,
    Gpu,
}

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp",
    };

    public const string WeChatOfficialAccount = "程序员Linc";

    private readonly OcrService? _ownedOcrService;
    private OcrService? _ocrService;
    private CancellationTokenSource? _recognizeCts;
    private CancellationTokenSource? _loadModelCts;
    private bool _suppressModelReload;
    private bool _suppressSelectionSync;

    public MainViewModel()
        : this(null)
    {
    }

    public MainViewModel(OcrService? ocrService)
    {
        _ownedOcrService = ocrService;
        _ocrService = ocrService;
        PresetOptions = new List<PresetOption>
        {
            new(OcrModelPreset.PpOcrV5, "PP-OCRv5"),
            new(OcrModelPreset.PpOcrV6Tiny, "PP-OCRv6 Tiny"),
            new(OcrModelPreset.PpOcrV6Small, "PP-OCRv6 Small"),
            new(OcrModelPreset.PpOcrV6Medium, "PP-OCRv6 Medium"),
        };

        GpuDevices = DetectGpuDevices();

        DeviceOptions = new List<DeviceOption> { new(InferenceDevice.Cpu, "CPU") };
        if (GpuDevices.Count > 0)
        {
            DeviceOptions.Add(new(InferenceDevice.Gpu, "GPU"));
        }
    }

    public List<PresetOption> PresetOptions { get; }
    public List<DeviceOption> DeviceOptions { get; }
    public IReadOnlyList<GpuDeviceInfo> GpuDevices { get; }

    public ObservableCollection<QueueItemViewModel> QueueItems { get; } = new();
    public ObservableCollection<OcrLineViewModel> Lines { get; } = new();

    [ObservableProperty]
    private InferenceDevice _selectedDevice = InferenceDevice.Cpu;

    [ObservableProperty]
    private GpuDeviceInfo? _selectedGpuDevice;

    [ObservableProperty]
    private OcrModelPreset _selectedPreset = OcrModelPreset.PpOcrV6Tiny;

    [ObservableProperty]
    private BitmapSource? _previewImage;

    [ObservableProperty]
    private QueueItemViewModel? _selectedQueueItem;

    [ObservableProperty]
    private string _statusMessage = "正在加载模型...";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isReady;

    [ObservableProperty]
    private OcrLineViewModel? _selectedLine;

    [ObservableProperty]
    private string _elapsedText = "耗时: -";

    [ObservableProperty]
    private string _queueSummary = "队列: 0";

    [ObservableProperty]
    private bool _hasQueueItems;

    [ObservableProperty]
    private double _batchProgress;

    [ObservableProperty]
    private bool _isBatchRunning;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadProgress;

    [ObservableProperty]
    private string _downloadStatus = "";

    [ObservableProperty]
    private bool _needsDownload;

    [ObservableProperty]
    private bool _isDownloadSupported;

    public async Task InitializeAsync()
    {
        if (_ocrService != null)
        {
            IsReady = true;
            StatusMessage = "就绪";
            return;
        }

        await LoadModelAsync(SelectedPreset);
    }

    private async Task LoadModelAsync(OcrModelPreset preset)
    {
        _loadModelCts?.Cancel();
        _loadModelCts = new CancellationTokenSource();
        var token = _loadModelCts.Token;

        try
        {
            _recognizeCts?.Cancel();
            NeedsDownload = false;
            IsDownloadSupported = false;

            IsBusy = true;
            IsReady = false;
            StatusMessage = "正在加载模型...";

            if (!OcrOptions.TryValidatePreset(preset))
            {
                ShowModelMissing(preset);
                return;
            }

            token.ThrowIfCancellationRequested();

            var service = await Task.Run(() => CreateOcrService(preset), token);

            token.ThrowIfCancellationRequested();

            if (_ownedOcrService == null)
                _ocrService?.Dispose();

            _ocrService = service;
            IsReady = true;
            StatusMessage = SelectedDevice == InferenceDevice.Gpu
                ? $"就绪 (GPU: {SelectedGpuDevice?.Name ?? "auto"})"
                : "就绪 (CPU)";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (IsModelNotFoundException(ex))
            {
                ShowModelMissing(preset);
            }
            else
            {
                StatusMessage = $"模型加载失败: {ex.Message}";
                IsReady = false;
                MessageBox.Show(ex.Message, "模型加载失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    private void ShowModelMissing(OcrModelPreset preset)
    {
        NeedsDownload = true;
        IsDownloadSupported = IsV6Preset(preset);
        StatusMessage = "模型文件未找到";
        IsReady = false;
    }

    private static bool IsModelNotFoundException(Exception ex)
    {
        return ex is FileNotFoundException or DirectoryNotFoundException;
    }

    private static bool IsV6Preset(OcrModelPreset preset)
    {
        return preset is OcrModelPreset.PpOcrV6Tiny
            or OcrModelPreset.PpOcrV6Small
            or OcrModelPreset.PpOcrV6Medium;
    }

    [RelayCommand(CanExecute = nameof(CanDownload))]
    private async Task DownloadModelAsync()
    {
        var preset = SelectedPreset;
        _loadModelCts?.Cancel();
        _loadModelCts = new CancellationTokenSource();
        var token = _loadModelCts.Token;

        try
        {
            IsDownloading = true;
            IsBusy = true;
            NeedsDownload = false;
            DownloadProgress = 0;
            DownloadStatus = "准备下载...";
            RefreshCommands();

            var targetDir = preset switch
            {
                OcrModelPreset.PpOcrV6Tiny or OcrModelPreset.PpOcrV6Small or OcrModelPreset.PpOcrV6Medium
                    => FindV6TargetDir(),
                _ => Path.Combine(FindModelsRoot(), preset.ToString().Replace("PpOcr", "").ToLowerInvariant())
            };

            using var downloader = new ModelDownloadService();
            downloader.StatusChanged += status => DownloadStatus = status;
            downloader.ProgressChanged += progress => DownloadProgress = progress * 100;

            await downloader.DownloadPresetModelsAsync(preset, targetDir, token);

            DownloadStatus = "下载完成，正在加载模型...";
            await LoadModelAsync(preset);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "已取消";
            NeedsDownload = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"下载失败: {ex.Message}";
            DownloadStatus = "";
            NeedsDownload = true;
            MessageBox.Show(ex.Message, "下载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsDownloading = false;
            IsBusy = false;
            RefreshCommands();
        }
    }

    private bool CanDownload() => IsDownloadSupported && !IsBusy && !IsDownloading;

    private string FindModelsRoot()
    {
        var searchRoot = AppContext.BaseDirectory;
        for (var dir = new DirectoryInfo(searchRoot); dir != null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "models");
            if (Directory.Exists(candidate))
                return candidate;
        }
        return Path.Combine(AppContext.BaseDirectory, "models");
    }

    private string FindV6TargetDir()
    {
        var modelsRoot = FindModelsRoot();
        var v6Dir = Path.Combine(modelsRoot, "ppocrv6");
        Directory.CreateDirectory(v6Dir);
        return v6Dir;
    }

    partial void OnSelectedPresetChanged(OcrModelPreset value)
    {
        if (_suppressModelReload) return;
        _ = LoadModelAsync(value);
    }

    partial void OnSelectedDeviceChanged(InferenceDevice value)
    {
        OnPropertyChanged(nameof(IsGpuSelected));
        if (_suppressModelReload) return;
        if (value == InferenceDevice.Gpu && SelectedGpuDevice == null && GpuDevices.Count > 0)
        {
            _suppressModelReload = true;
            SelectedGpuDevice = GpuDevices[0];
            _suppressModelReload = false;
        }
        _ = LoadModelAsync(SelectedPreset);
    }

    partial void OnSelectedGpuDeviceChanged(GpuDeviceInfo? value)
    {
        if (_suppressModelReload) return;
        if (SelectedDevice == InferenceDevice.Gpu)
            _ = LoadModelAsync(SelectedPreset);
    }

    public bool IsGpuSelected => SelectedDevice == InferenceDevice.Gpu;

    private OcrService CreateOcrService(OcrModelPreset preset)
    {
        if (SelectedDevice == InferenceDevice.Gpu)
        {
            if (SelectedGpuDevice != null)
                return OcrService.CreateWithGpu(preset, SelectedGpuDevice.DeviceId);
            return OcrService.CreateWithAutoDevice(preset);
        }

        return new OcrService(preset);
    }

    private static IReadOnlyList<GpuDeviceInfo> DetectGpuDevices()
    {
        try
        {
            return GpuDeviceDetector.DetectGpus();
        }
        catch
        {
            return new List<GpuDeviceInfo>
            {
                new() { DeviceId = 0, Name = "GPU 0", IsAvailable = true },
            };
        }
    }

    [RelayCommand(CanExecute = nameof(CanModifyQueue))]
    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图片（可多选）",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
            Multiselect = true,
        };

        if (dialog.ShowDialog() != true)
            return;

        AddPaths(dialog.FileNames);
    }

    [RelayCommand(CanExecute = nameof(CanModifyQueue))]
    private void AddFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择包含图片的文件夹",
        };

        if (dialog.ShowDialog() != true)
            return;

        var files = Directory.EnumerateFiles(dialog.FolderName, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsSupportedImage)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (files.Length == 0)
        {
            MessageBox.Show("该文件夹下没有支持的图片文件。", "添加文件夹", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AddPaths(files);
    }

    public void AddDroppedPaths(IEnumerable<string> paths)
    {
        if (!CanModifyQueue())
            return;

        var expanded = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                expanded.AddRange(
                    Directory.EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(IsSupportedImage));
            }
            else if (File.Exists(path) && IsSupportedImage(path))
            {
                expanded.Add(path);
            }
        }

        AddPaths(expanded);
    }

    private void AddPaths(IEnumerable<string> paths)
    {
        var existing = new HashSet<string>(
            QueueItems.Select(i => i.FilePath),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        QueueItemViewModel? firstAdded = null;

        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!IsSupportedImage(path) || !File.Exists(path))
                continue;
            if (!existing.Add(path))
                continue;

            var item = new QueueItemViewModel(path);
            QueueItems.Add(item);
            firstAdded ??= item;
            added++;
        }

        UpdateQueueSummary();
        RefreshCommands();

        if (added == 0)
        {
            StatusMessage = "没有新的图片加入队列";
            return;
        }

        StatusMessage = $"已添加 {added} 张图片";
        if (SelectedQueueItem == null && firstAdded != null)
            SelectedQueueItem = firstAdded;
    }

    private static bool IsSupportedImage(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path));
    }

    [RelayCommand(CanExecute = nameof(CanRecognize))]
    private async Task RecognizeAsync()
    {
        if (_ocrService == null || QueueItems.Count == 0)
            return;

        var targets = QueueItems
            .Where(i => i.Status is QueueItemStatus.Pending or QueueItemStatus.Failed)
            .ToList();

        if (targets.Count == 0)
        {
            foreach (var item in QueueItems)
                item.ResetForRerun();
            targets = QueueItems.ToList();
        }

        _recognizeCts?.Cancel();
        _recognizeCts = new CancellationTokenSource();
        var token = _recognizeCts.Token;

        try
        {
            IsBusy = true;
            IsBatchRunning = true;
            BatchProgress = 0;
            SelectedLine = null;
            RefreshCommands();

            var deviceTag = SelectedDevice == InferenceDevice.Gpu
                ? $"[GPU:{SelectedGpuDevice?.Name ?? "auto"}]"
                : "[CPU]";

            var sw = Stopwatch.StartNew();
            var succeeded = 0;
            var failed = 0;

            for (var i = 0; i < targets.Count; i++)
            {
                token.ThrowIfCancellationRequested();

                var item = targets[i];
                item.MarkRunning();
                StatusMessage = $"识别中 ({i + 1}/{targets.Count}): {item.FileName}";
                BatchProgress = (i * 100.0) / targets.Count;

                if (SelectedQueueItem != item)
                {
                    _suppressSelectionSync = true;
                    SelectedQueueItem = item;
                    _suppressSelectionSync = false;
                    ShowQueueItem(item);
                }

                try
                {
                    var result = await _ocrService.RecognizeAsync(item.FilePath, token);
                    item.MarkSucceeded(result);
                    succeeded++;

                    if (ReferenceEquals(SelectedQueueItem, item))
                        ShowQueueItem(item);
                }
                catch (OperationCanceledException)
                {
                    item.Status = QueueItemStatus.Pending;
                    item.StatusText = "等待";
                    throw;
                }
                catch (Exception ex)
                {
                    item.MarkFailed(ex.Message);
                    failed++;
                    if (ReferenceEquals(SelectedQueueItem, item))
                        ShowQueueItem(item);
                }

                BatchProgress = ((i + 1) * 100.0) / targets.Count;
                UpdateQueueSummary();
            }

            sw.Stop();
            ElapsedText = $"{deviceTag} 批量耗时: {sw.Elapsed.TotalSeconds:F2}s，成功 {succeeded}，失败 {failed}";
            StatusMessage = failed == 0
                ? $"批量识别完成 {deviceTag}"
                : $"批量完成（含失败）{deviceTag}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "已取消";
        }
        catch (Exception ex)
        {
            StatusMessage = $"识别失败: {ex.Message}";
            MessageBox.Show(ex.Message, "识别失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            IsBatchRunning = false;
            UpdateQueueSummary();
            RefreshCommands();
        }
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        QueueItems.Clear();
        PreviewImage = null;
        Lines.Clear();
        SelectedLine = null;
        SelectedQueueItem = null;
        ElapsedText = "耗时: -";
        BatchProgress = 0;
        UpdateQueueSummary();
        StatusMessage = "就绪";
        RefreshCommands();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelected))]
    private void RemoveSelected()
    {
        if (SelectedQueueItem == null)
            return;

        var index = QueueItems.IndexOf(SelectedQueueItem);
        QueueItems.Remove(SelectedQueueItem);

        SelectedQueueItem = QueueItems.Count == 0
            ? null
            : QueueItems[Math.Clamp(index, 0, QueueItems.Count - 1)];

        if (SelectedQueueItem == null)
        {
            PreviewImage = null;
            Lines.Clear();
            SelectedLine = null;
            ElapsedText = "耗时: -";
        }

        UpdateQueueSummary();
        RefreshCommands();
    }

    private bool CanClear() => !IsBusy && QueueItems.Count > 0;

    private bool CanRemoveSelected() => !IsBusy && SelectedQueueItem != null;

    private bool CanModifyQueue() => IsReady && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCopyAll))]
    private void CopyAll()
    {
        if (Lines.Count == 0)
            return;

        Clipboard.SetText(string.Join(Environment.NewLine, Lines.Select(line => line.Text)));
        StatusMessage = "已复制当前文件结果";
    }

    [RelayCommand(CanExecute = nameof(CanCopyBatch))]
    private void CopyBatch()
    {
        var sb = new StringBuilder();
        var any = false;

        foreach (var item in QueueItems.Where(i => i.Status == QueueItemStatus.Succeeded && i.Lines.Count > 0))
        {
            any = true;
            sb.AppendLine($"===== {item.FileName} =====");
            foreach (var line in item.Lines)
                sb.AppendLine(line.Text);
            sb.AppendLine();
        }

        if (!any)
            return;

        Clipboard.SetText(sb.ToString().TrimEnd());
        StatusMessage = "已复制全部文件结果";
    }

    [RelayCommand(CanExecute = nameof(CanExport))]
    private void ExportResults()
    {
        var dialog = new SaveFileDialog
        {
            Title = "导出识别结果",
            Filter = "文本文件|*.txt|所有文件|*.*",
            FileName = $"ocr_results_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };

        if (dialog.ShowDialog() != true)
            return;

        var sb = new StringBuilder();
        sb.AppendLine($"OnnxOCRSharp 批量识别结果");
        sb.AppendLine($"公众号：{WeChatOfficialAccount}");
        sb.AppendLine($"导出时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        foreach (var item in QueueItems)
        {
            sb.AppendLine($"===== {item.FileName} [{item.StatusText}] =====");
            if (item.Status == QueueItemStatus.Failed)
            {
                sb.AppendLine($"错误: {item.ErrorMessage}");
            }
            else if (item.Lines.Count == 0)
            {
                sb.AppendLine("(无文字)");
            }
            else
            {
                foreach (var line in item.Lines)
                    sb.AppendLine(line.Text);
            }

            sb.AppendLine();
        }

        File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
        StatusMessage = $"已导出: {Path.GetFileName(dialog.FileName)}";
    }

    [RelayCommand]
    private void CopyWeChatAccount()
    {
        Clipboard.SetText(WeChatOfficialAccount);
        StatusMessage = $"已复制公众号：{WeChatOfficialAccount}";
    }

    [RelayCommand]
    private void ShowAbout()
    {
        var owner = Application.Current?.MainWindow;
        var about = new AboutWindow();
        if (owner != null && owner.IsLoaded)
            about.Owner = owner;
        about.ShowDialog();
    }

    [RelayCommand]
    private void CancelOperation()
    {
        _loadModelCts?.Cancel();
        _recognizeCts?.Cancel();

        IsDownloading = false;
        NeedsDownload = false;
        DownloadProgress = 0;
        DownloadStatus = "";
        IsBusy = false;
        IsBatchRunning = false;

        foreach (var item in QueueItems.Where(i => i.Status == QueueItemStatus.Running))
        {
            item.Status = QueueItemStatus.Pending;
            item.StatusText = "等待";
        }

        if (_ocrService != null)
        {
            IsReady = true;
            StatusMessage = "就绪";
        }
        else
        {
            IsReady = false;
        }

        RefreshCommands();
    }

    [RelayCommand]
    private void DismissDownloadPrompt()
    {
        NeedsDownload = false;
        IsDownloadSupported = false;
        StatusMessage = "模型未就绪";
    }

    partial void OnSelectedQueueItemChanged(QueueItemViewModel? value)
    {
        if (_suppressSelectionSync)
            return;

        ShowQueueItem(value);
        RefreshCommands();
    }

    partial void OnSelectedLineChanged(OcrLineViewModel? value)
    {
        foreach (var line in Lines)
            line.IsSelected = line == value;
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommands();

    partial void OnIsReadyChanged(bool value) => RefreshCommands();

    private bool CanRecognize() => IsReady && !IsBusy && QueueItems.Count > 0;

    private bool CanCopyAll() => !IsBusy && Lines.Count > 0;

    private bool CanCopyBatch() =>
        !IsBusy && QueueItems.Any(i => i.Status == QueueItemStatus.Succeeded && i.Lines.Count > 0);

    private bool CanExport() =>
        !IsBusy && QueueItems.Any(i => i.Status is QueueItemStatus.Succeeded or QueueItemStatus.Failed);

    private void ShowQueueItem(QueueItemViewModel? item)
    {
        Lines.Clear();
        SelectedLine = null;

        if (item == null)
        {
            PreviewImage = null;
            ElapsedText = "耗时: -";
            return;
        }

        try
        {
            using var mat = Cv2.ImRead(item.FilePath);
            if (mat.Empty())
                throw new InvalidOperationException("无法读取图片");

            var bitmap = BitmapSourceHelper.NormalizeDpi(mat.ToBitmapSource());
            bitmap.Freeze();
            PreviewImage = bitmap;
        }
        catch (Exception ex)
        {
            PreviewImage = null;
            StatusMessage = $"预览失败: {ex.Message}";
        }

        foreach (var line in item.Lines)
            Lines.Add(line);

        ElapsedText = item.Status switch
        {
            QueueItemStatus.Succeeded => $"当前: {item.FileName} | {item.ElapsedText} | {item.LineCount} 行",
            QueueItemStatus.Failed => $"当前: {item.FileName} | 失败: {item.ErrorMessage}",
            QueueItemStatus.Running => $"当前: {item.FileName} | 识别中...",
            _ => $"当前: {item.FileName} | 等待识别",
        };
    }

    private void UpdateQueueSummary()
    {
        var total = QueueItems.Count;
        HasQueueItems = total > 0;
        var done = QueueItems.Count(i => i.Status is QueueItemStatus.Succeeded or QueueItemStatus.Failed);
        var ok = QueueItems.Count(i => i.Status == QueueItemStatus.Succeeded);
        var fail = QueueItems.Count(i => i.Status == QueueItemStatus.Failed);
        QueueSummary = total == 0
            ? "队列: 0"
            : $"队列: {done}/{total}（成功 {ok} / 失败 {fail}）";
    }

    private void RefreshCommands()
    {
        AddFilesCommand.NotifyCanExecuteChanged();
        AddFolderCommand.NotifyCanExecuteChanged();
        RecognizeCommand.NotifyCanExecuteChanged();
        CopyAllCommand.NotifyCanExecuteChanged();
        CopyBatchCommand.NotifyCanExecuteChanged();
        ExportResultsCommand.NotifyCanExecuteChanged();
        CancelOperationCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        DownloadModelCommand.NotifyCanExecuteChanged();
    }

    public async ValueTask DisposeAsync()
    {
        _loadModelCts?.Cancel();
        _loadModelCts?.Dispose();
        _recognizeCts?.Cancel();
        _recognizeCts?.Dispose();

        if (_ownedOcrService != null)
            _ownedOcrService.Dispose();
        else if (_ocrService != null)
            _ocrService.Dispose();

        await Task.CompletedTask;
    }
}

public class PresetOption
{
    public OcrModelPreset Preset { get; }
    public string DisplayName { get; }

    public PresetOption(OcrModelPreset preset, string displayName)
    {
        Preset = preset;
        DisplayName = displayName;
    }
}

public class DeviceOption
{
    public InferenceDevice Device { get; }
    public string DisplayName { get; }

    public DeviceOption(InferenceDevice device, string displayName)
    {
        Device = device;
        DisplayName = displayName;
    }
}
