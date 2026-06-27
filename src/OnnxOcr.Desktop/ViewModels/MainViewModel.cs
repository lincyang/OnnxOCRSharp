//-----------------------------------------------------------------------
// <copyright file="MainViewModel.cs" company="����ԱLinc">
// Copyright (c) ����ԱLinc. All rights reserved.
// </copyright>
// <author>����ԱLinc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>���ںţ�����ԱLinc</wechat>
//-----------------------------------------------------------------------
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnnxOcr.App.Models;
using OnnxOcr.App.Services;
using OnnxOcr.Core.Configuration;
using OnnxOcr.Desktop.Helpers;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace OnnxOcr.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject, IAsyncDisposable
{
    private readonly OcrService? _ownedOcrService;
    private OcrService? _ocrService;
    private CancellationTokenSource? _recognizeCts;
    private CancellationTokenSource? _loadModelCts;

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
        _selectedPreset = OcrModelPreset.PpOcrV5;
    }

    public List<PresetOption> PresetOptions { get; }

    [ObservableProperty]
    private OcrModelPreset _selectedPreset;

    [ObservableProperty]
    private BitmapSource? _previewImage;

    [ObservableProperty]
    private string? _imagePath;

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

    public ObservableCollection<OcrLineViewModel> Lines { get; } = new();

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

            IsBusy = true;
            IsReady = false;
            StatusMessage = $"正在加载模型 ({PresetOptions.First(p => p.Preset == preset).DisplayName})...";

            token.ThrowIfCancellationRequested();

            var service = await Task.Run(() => new OcrService(preset), token);

            token.ThrowIfCancellationRequested();

            if (_ownedOcrService == null)
                _ocrService?.Dispose();

            _ocrService = service;
            IsReady = true;
            StatusMessage = "就绪";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"模型加载失败: {ex.Message}";
            IsReady = false;
            MessageBox.Show(ex.Message, "模型加载失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            RefreshCommands();
        }
    }

    partial void OnSelectedPresetChanged(OcrModelPreset value)
    {
        if (_ocrService != null)
            _ = LoadModelAsync(value);
    }

    [RelayCommand(CanExecute = nameof(CanOpenImage))]
    private void OpenImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图片",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
        };

        if (dialog.ShowDialog() != true)
            return;

        LoadImage(dialog.FileName);
    }

    [RelayCommand(CanExecute = nameof(CanRecognize))]
    private async Task RecognizeAsync()
    {
        if (_ocrService == null || string.IsNullOrWhiteSpace(ImagePath))
            return;

        _recognizeCts?.Cancel();
        _recognizeCts = new CancellationTokenSource();
        var token = _recognizeCts.Token;

        try
        {
            IsBusy = true;
            StatusMessage = "识别中...";
            Lines.Clear();
            SelectedLine = null;
            ElapsedText = "耗时: -";

            var result = await _ocrService.RecognizeAsync(ImagePath, token);

            Lines.Clear();
            foreach (var line in result.Lines)
                Lines.Add(OcrLineViewModel.From(line));

            ElapsedText = $"耗时: {result.Elapsed.TotalSeconds:F2}s，共 {result.Lines.Count} 行";
            StatusMessage = result.Lines.Count > 0 ? "识别完成" : "未检测到文字";
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
            RefreshCommands();
        }
    }

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        PreviewImage = null;
        ImagePath = null;
        Lines.Clear();
        SelectedLine = null;
        ElapsedText = "耗时: -";
        StatusMessage = "就绪";
        RefreshCommands();
    }

    private bool CanClear() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCopyAll))]
    private void CopyAll()
    {
        if (Lines.Count == 0)
            return;

        Clipboard.SetText(string.Join(Environment.NewLine, Lines.Select(line => line.Text)));
        StatusMessage = "已复制到剪贴板";
    }

    [RelayCommand(CanExecute = nameof(CanRecognize))]
    private void CancelRecognize()
    {
        _recognizeCts?.Cancel();
    }

    partial void OnSelectedLineChanged(OcrLineViewModel? value)
    {
        foreach (var line in Lines)
            line.IsSelected = line == value;
    }

    partial void OnIsBusyChanged(bool value) => RefreshCommands();

    partial void OnIsReadyChanged(bool value) => RefreshCommands();

    private bool CanOpenImage() => IsReady && !IsBusy;

    private bool CanRecognize() => IsReady && !IsBusy && !string.IsNullOrWhiteSpace(ImagePath);

    private bool CanCopyAll() => !IsBusy && Lines.Count > 0;

    private void LoadImage(string path)
    {
        try
        {
            using var mat = Cv2.ImRead(path);
            if (mat.Empty())
                throw new InvalidOperationException("无法读取图片");

            var bitmap = BitmapSourceHelper.NormalizeDpi(mat.ToBitmapSource());
            bitmap.Freeze();
            PreviewImage = bitmap;

            ImagePath = path;
            Lines.Clear();
            SelectedLine = null;
            ElapsedText = "耗时: -";
            StatusMessage = System.IO.Path.GetFileName(path);
            RefreshCommands();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "打开图片失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshCommands()
    {
        OpenImageCommand.NotifyCanExecuteChanged();
        RecognizeCommand.NotifyCanExecuteChanged();
        CopyAllCommand.NotifyCanExecuteChanged();
        CancelRecognizeCommand.NotifyCanExecuteChanged();
        ClearCommand.NotifyCanExecuteChanged();
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
