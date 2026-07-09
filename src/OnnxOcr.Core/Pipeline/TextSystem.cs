//-----------------------------------------------------------------------
// <copyright file="TextSystem.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Detection;
using OnnxOcr.Core.Imaging;
using OnnxOcr.Core.Inference;
using OnnxOcr.Core.Models;
using OnnxOcr.Core.Orientation;
using OnnxOcr.Core.Recognition;
using OpenCvSharp;

namespace OnnxOcr.Core.Pipeline;

public sealed class TextSystem : IDisposable
{
    private readonly OcrOptions _options;
    private readonly Detection.TextDetector _detector;
    private readonly TextRecognizer _recognizer;
    private readonly TextOrientationClassifier? _orientationClassifier;

    public TextSystem(OcrOptions options)
    {
        options.Validate();
        _options = options;

        var sessionFactory = new OnnxSessionFactory(options);
        _detector = new Detection.TextDetector(options, sessionFactory);
        _recognizer = new TextRecognizer(options, sessionFactory);

        if (options.UseAngleCls
            && !string.IsNullOrWhiteSpace(options.OrientationModelPath)
            && File.Exists(options.OrientationModelPath))
        {
            _orientationClassifier = new TextOrientationClassifier(options, sessionFactory);
        }
    }

    public OcrResult Run(Mat image)
    {
        if (image.Empty())
            throw new ArgumentException("Input image is empty.", nameof(image));

        var started = DateTime.UtcNow;
        OcrLogger.Log($"[TextSystem] ===== OCR Start | Device={(_options.UseGpu ? "GPU" : "CPU")} =====");

        var detStarted = DateTime.UtcNow;
        var boxes = _detector.Detect(image);
        var detElapsed = DateTime.UtcNow - detStarted;
        OcrLogger.Log($"[TextSystem] Detection: {boxes.Count} boxes in {detElapsed.TotalMilliseconds:F1}ms");

        if (boxes.Count == 0)
        {
            var total = DateTime.UtcNow - started;
            OcrLogger.Log($"[TextSystem] No text found. Total: {total.TotalMilliseconds:F1}ms");
            return new OcrResult
            {
                Elapsed = total,
                ImageWidth = image.Cols,
                ImageHeight = image.Rows,
            };
        }

        var sortStarted = DateTime.UtcNow;
        var sortedBoxes = BoxSorter.Sort(boxes);
        OcrLogger.Log($"[TextSystem] BoxSort: {(DateTime.UtcNow - sortStarted).TotalMilliseconds:F1}ms");

        var cropStarted = DateTime.UtcNow;
        var crops = new List<Mat>(sortedBoxes.Count);

        foreach (var box in sortedBoxes)
        {
            var crop = ImageCropper.Crop(
                image,
                box,
                _options.DetBoxType,
                applyVerticalRotate: _orientationClassifier == null);

            if (_orientationClassifier != null)
            {
                var clsStarted = DateTime.UtcNow;
                var corrected = _orientationClassifier.CorrectOrientation(crop);
                OcrLogger.Log($"[TextSystem] Orientation classify: {(DateTime.UtcNow - clsStarted).TotalMilliseconds:F1}ms");
                if (!ReferenceEquals(corrected, crop))
                {
                    crop.Dispose();
                    crop = corrected;
                }
            }

            crops.Add(crop);
        }
        OcrLogger.Log($"[TextSystem] Crop total: {(DateTime.UtcNow - cropStarted).TotalMilliseconds:F1}ms");

        try
        {
            var recStarted = DateTime.UtcNow;
            var recResults = _recognizer.Recognize(crops);
            var recElapsed = DateTime.UtcNow - recStarted;
            OcrLogger.Log($"[TextSystem] Recognition: {recResults.Count} texts in {recElapsed.TotalMilliseconds:F1}ms");

            var lines = new List<TextLine>();

            for (var i = 0; i < sortedBoxes.Count; i++)
            {
                var (text, score) = recResults[i];
                if (score < _options.DropScore)
                    continue;

                lines.Add(new TextLine
                {
                    Box = sortedBoxes[i],
                    Text = text,
                    Score = score,
                });
            }

            var total = DateTime.UtcNow - started;
            OcrLogger.Log($"[TextSystem] ===== OCR Done | {lines.Count} lines | Total: {total.TotalMilliseconds:F1}ms =====");

            return new OcrResult
            {
                Lines = lines,
                Elapsed = total,
                ImageWidth = image.Cols,
                ImageHeight = image.Rows,
            };
        }
        finally
        {
            foreach (var crop in crops)
                crop.Dispose();
        }
    }

    public void Dispose()
    {
        _detector.Dispose();
        _recognizer.Dispose();
        _orientationClassifier?.Dispose();
    }
}
