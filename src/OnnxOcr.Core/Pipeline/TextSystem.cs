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
using OnnxOcr.Core.Recognition;
using OpenCvSharp;

namespace OnnxOcr.Core.Pipeline;

public sealed class TextSystem : IDisposable
{
    private readonly OcrOptions _options;
    private readonly Detection.TextDetector _detector;
    private readonly TextRecognizer _recognizer;

    public TextSystem(OcrOptions options)
    {
        options.Validate();
        _options = options;

        var sessionFactory = new OnnxSessionFactory(options);
        _detector = new Detection.TextDetector(options, sessionFactory);
        _recognizer = new TextRecognizer(options, sessionFactory);
    }

    public OcrResult Run(Mat image)
    {
        if (image.Empty())
            throw new ArgumentException("Input image is empty.", nameof(image));

        var started = DateTime.UtcNow;
        var boxes = _detector.Detect(image);
        if (boxes.Count == 0)
        {
            return new OcrResult
            {
                Elapsed = DateTime.UtcNow - started,
                ImageWidth = image.Cols,
                ImageHeight = image.Rows,
            };
        }

        var sortedBoxes = BoxSorter.Sort(boxes);
        var crops = new List<Mat>(sortedBoxes.Count);
        foreach (var box in sortedBoxes)
            crops.Add(ImageCropper.Crop(image, box, _options.DetBoxType));

        try
        {
            var recResults = _recognizer.Recognize(crops);
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

            return new OcrResult
            {
                Lines = lines,
                Elapsed = DateTime.UtcNow - started,
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
    }
}
