//-----------------------------------------------------------------------
// <copyright file="ModelDownloadService.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Net.Http;
using OnnxOcr.Core.Configuration;

namespace OnnxOcr.Core;

public class ModelDownloadService : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string ModelScopeEndpoint = "https://modelscope.cn";

    public event Action<string>? StatusChanged;
    public event Action<double>? ProgressChanged;

    public ModelDownloadService()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = System.Net.DecompressionMethods.All };
        _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "OnnxOCRSharp/1.0");
    }

    public async Task DownloadPresetModelsAsync(OcrModelPreset preset, string targetDir, CancellationToken cancellationToken = default)
    {
        var detModelId = GetDetModelId(preset);
        var recModelId = GetRecModelId(preset);
        var detFolder = GetDetFolder(preset);
        var recFolder = GetRecFolder(preset);
        var detFileName = GetDetFileName(preset);
        var recFileName = GetRecFileName(preset);

        var files = new List<(string ModelId, string Folder, string FileName)>();
        if (!string.IsNullOrEmpty(detModelId))
            files.Add((detModelId, detFolder, detFileName));
        if (!string.IsNullOrEmpty(recModelId))
        {
            files.Add((recModelId, recFolder, recFileName));
            files.Add((recModelId, recFolder, "inference.yml"));
        }

        if (files.Count == 0)
            throw new InvalidOperationException($"No ModelScope model IDs defined for preset {preset}.");

        int fileIndex = 0;
        foreach (var (modelId, folder, fileName) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var localDir = Path.Combine(targetDir, folder);
            var localPath = Path.Combine(localDir, fileName);
            StatusChanged?.Invoke($"下载: {folder}/{fileName}");

            await DownloadFileAsync(modelId, fileName, localPath, cancellationToken);

            fileIndex++;
            ProgressChanged?.Invoke((double)fileIndex / files.Count);
        }
    }

    private async Task DownloadFileAsync(string modelId, string fileName, string localPath, CancellationToken cancellationToken)
    {
        var encodedName = Uri.EscapeDataString(fileName);
        var url = $"{ModelScopeEndpoint}/api/v1/models/{modelId}/repo?Revision=master&FilePath={encodedName}";

        if (File.Exists(localPath))
        {
            StatusChanged?.Invoke($"  已存在，跳过: {fileName}");
            return;
        }

        StatusChanged?.Invoke($"  下载中: {fileName}");
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[81920];
        int bytesRead;
        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        StatusChanged?.Invoke($"  完成: {fileName}");
    }

    private static string GetDetModelId(OcrModelPreset preset) => preset switch
    {
        OcrModelPreset.PpOcrV6Tiny => "PaddlePaddle/PP-OCRv6_tiny_det_onnx",
        OcrModelPreset.PpOcrV6Small => "PaddlePaddle/PP-OCRv6_small_det_onnx",
        OcrModelPreset.PpOcrV6Medium => "PaddlePaddle/PP-OCRv6_medium_det_onnx",
        _ => ""
    };

    private static string GetRecModelId(OcrModelPreset preset) => preset switch
    {
        OcrModelPreset.PpOcrV6Tiny => "PaddlePaddle/PP-OCRv6_tiny_rec_onnx",
        OcrModelPreset.PpOcrV6Small => "PaddlePaddle/PP-OCRv6_small_rec_onnx",
        OcrModelPreset.PpOcrV6Medium => "PaddlePaddle/PP-OCRv6_medium_rec_onnx",
        _ => ""
    };

    private static string GetDetFolder(OcrModelPreset preset) => preset switch
    {
        OcrModelPreset.PpOcrV6Tiny => "PP-OCRv6_tiny_det_onnx",
        OcrModelPreset.PpOcrV6Small => "PP-OCRv6_small_det_onnx",
        OcrModelPreset.PpOcrV6Medium => "PP-OCRv6_medium_det_onnx",
        _ => "det"
    };

    private static string GetRecFolder(OcrModelPreset preset) => preset switch
    {
        OcrModelPreset.PpOcrV6Tiny => "PP-OCRv6_tiny_rec_onnx",
        OcrModelPreset.PpOcrV6Small => "PP-OCRv6_small_rec_onnx",
        OcrModelPreset.PpOcrV6Medium => "PP-OCRv6_medium_rec_onnx",
        _ => "rec"
    };

    private static string GetDetFileName(OcrModelPreset preset) => "inference.onnx";

    private static string GetRecFileName(OcrModelPreset preset) => "inference.onnx";

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
