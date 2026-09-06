//-----------------------------------------------------------------------
// <copyright file="OcrService.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.App.Models;
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;

namespace OnnxOcr.App.Services;

public sealed class OcrService : IDisposable
{
    private readonly TextSystem _textSystem;

    public OcrService(OcrModelPreset preset, string? modelsRoot = null)
        : this(OcrOptions.ForPreset(preset, modelsRoot))
    {
    }

    public OcrService(OcrOptions? options = null)
    {
        _textSystem = new TextSystem(options ?? OcrOptions.CreateDefault());
    }

    public static OcrService CreateWithGpu(OcrModelPreset preset, int gpuId = 0, string? modelsRoot = null)
    {
        var options = OcrOptions.ForPresetWithGpu(preset, gpuId, modelsRoot);
        return new OcrService(options);
    }

    public static OcrService CreateWithAutoDevice(OcrModelPreset preset, string? modelsRoot = null)
    {
        var options = OcrOptions.ForPresetWithAutoDevice(preset, modelsRoot);
        return new OcrService(options);
    }

    public Task<OcrRunResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path is required.", nameof(imagePath));

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var image = Cv2.ImRead(imagePath);
            if (image.Empty())
                throw new InvalidOperationException($"无法读取图片: {imagePath}");

            cancellationToken.ThrowIfCancellationRequested();
            var result = _textSystem.Run(image);
            return OcrRunResult.From(result, imagePath);
        }, cancellationToken);
    }

    /// <summary>
    /// 按顺序串行识别多张图片。单张失败会记录错误并继续，不会中断整批。
    /// </summary>
    public async Task<IReadOnlyList<OcrBatchItemResult>> RecognizeManyAsync(
        IEnumerable<string> imagePaths,
        IProgress<OcrBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);

        var paths = imagePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<OcrBatchItemResult>(paths.Count);
        var succeeded = 0;
        var failed = 0;

        for (var i = 0; i < paths.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = paths[i];
            progress?.Report(new OcrBatchProgress
            {
                CurrentIndex = i + 1,
                Total = paths.Count,
                CurrentPath = path,
                Succeeded = succeeded,
                Failed = failed,
            });

            try
            {
                var run = await RecognizeAsync(path, cancellationToken).ConfigureAwait(false);
                succeeded++;
                results.Add(new OcrBatchItemResult
                {
                    ImagePath = path,
                    Success = true,
                    Result = run,
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                results.Add(new OcrBatchItemResult
                {
                    ImagePath = path,
                    Success = false,
                    ErrorMessage = ex.Message,
                });
            }
        }

        return results;
    }

    public void Dispose() => _textSystem.Dispose();
}
