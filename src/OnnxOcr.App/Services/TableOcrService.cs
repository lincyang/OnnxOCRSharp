//-----------------------------------------------------------------------
// <copyright file="TableOcrService.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.App.Models;
using OnnxOcr.Core;
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OnnxOcr.Core.Table;
using OpenCvSharp;

namespace OnnxOcr.App.Services;

/// <summary>
/// Runs TextSystem OCR then SLANet-plus table structure matching.
/// </summary>
public sealed class TableOcrService : IDisposable
{
    private readonly TextSystem _textSystem;
    private readonly TableRecognizer _tableRecognizer;
    private readonly OcrOptions _options;

    public TableOcrService(OcrOptions? options = null, string? tableModelPath = null)
    {
        _options = options ?? OcrOptions.CreateDefault();
        _textSystem = new TextSystem(_options);

        var modelPath = tableModelPath;
        if (string.IsNullOrWhiteSpace(modelPath))
            modelPath = ModelPathResolver.FindTableModelPath();

        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                "Table model not found. Place slanet-plus.onnx under models/table/ " +
                "or call ModelDownloadService.DownloadTableModelAsync. " +
                "Expected path: models/table/slanet-plus.onnx");
        }

        _tableRecognizer = new TableRecognizer(modelPath, _options);
    }

    public TableOcrService(OcrModelPreset preset, string? modelsRoot = null, string? tableModelPath = null)
        : this(OcrOptions.ForPreset(preset, modelsRoot), tableModelPath ?? ModelPathResolver.FindTableModelPath(modelsRoot))
    {
    }

    public Task<TableRunResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path is required.", nameof(imagePath));

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var image = Cv2.ImRead(imagePath);
            if (image.Empty())
                throw new InvalidOperationException($"无法读取图片: {imagePath}");

            var started = DateTime.UtcNow;
            cancellationToken.ThrowIfCancellationRequested();
            var ocr = _textSystem.Run(image);

            cancellationToken.ThrowIfCancellationRequested();
            OcrLogger.Log($"[TableOcr] structure start | ocrLines={ocr.Lines.Count}, image={image.Cols}x{image.Rows}");
            var table = _tableRecognizer.Recognize(image, ocr.Lines);
            var filled = table.CellTexts.Count(t => !string.IsNullOrWhiteSpace(t));
            OcrLogger.Log(
                $"[TableOcr] structure done | score={table.StructureScore:F4}, " +
                $"logic={table.LogicPoints.Count}, cells={table.CellTexts.Count}, filled={filled}, " +
                $"htmlLen={table.Html?.Length ?? 0}, elapsed={table.Elapsed.TotalMilliseconds:F0}ms");

            return TableRunResult.From(table, ocr, imagePath, DateTime.UtcNow - started);
        }, cancellationToken);
    }

    public void Dispose()
    {
        _tableRecognizer.Dispose();
        _textSystem.Dispose();
    }
}
