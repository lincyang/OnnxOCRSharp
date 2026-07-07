//-----------------------------------------------------------------------
// <copyright file="TextOrientationClassifier.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Inference;
using OpenCvSharp;

namespace OnnxOcr.Core.Orientation;

public sealed class TextOrientationClassifier : IDisposable
{
    private const int InputSize = 224;
    private static readonly Scalar PadColor = new(255, 255, 255);
    private static readonly float[] Mean = [0.485f, 0.456f, 0.406f];
    private static readonly float[] Std = [0.229f, 0.224f, 0.225f];

    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly int[] _angleLabels;

    public TextOrientationClassifier(OcrOptions options, OnnxSessionFactory sessionFactory)
    {
        _session = sessionFactory.Create(options.OrientationModelPath);
        _inputName = _session.InputMetadata.Keys.First();
        _angleLabels = ParseAngleLabels(_session);
        WarmUp();
    }

    public int Classify(Mat image)
    {
        var tensor = Preprocess(image);
        using var results = _session.Run(
            new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) });

        var output = results.First().AsTensor<float>();
        var dims = output.Dimensions.ToArray();
        var classCount = dims[^1];
        var batchIndex = 0;
        var bestIndex = 0;
        var bestScore = output[batchIndex, 0];

        for (var i = 1; i < classCount; i++)
        {
            var score = output[batchIndex, i];
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return _angleLabels[Math.Min(bestIndex, _angleLabels.Length - 1)];
    }

    public Mat CorrectOrientation(Mat image)
    {
        var angle = Classify(image);
        if (IsVerticalDominant(image) && angle is 0 or 180)
            angle = 90;

        return RotateToUpright(image, angle);
    }

    private static bool IsVerticalDominant(Mat image)
        => image.Rows / (double)Math.Max(image.Cols, 1) >= 1.5;

    public static Mat RotateToUpright(Mat image, int angle)
    {
        return angle switch
        {
            0 => image,
            90 => Rotate(image, RotateFlags.Rotate90Counterclockwise),
            180 => Rotate(image, RotateFlags.Rotate180),
            270 => Rotate(image, RotateFlags.Rotate90Clockwise),
            _ => image,
        };
    }

    private static Mat Rotate(Mat image, RotateFlags flag)
    {
        var rotated = new Mat();
        Cv2.Rotate(image, rotated, flag);
        return rotated;
    }

    private static DenseTensor<float> Preprocess(Mat image)
    {
        using var working = ResizeToMinSide(image, 256);
        using var padded = PadToMinSize(working, InputSize);
        using var cropped = CenterCrop(padded, InputSize);

        var tensor = new DenseTensor<float>(new[] { 1, 3, InputSize, InputSize });
        for (var y = 0; y < InputSize; y++)
        {
            for (var x = 0; x < InputSize; x++)
            {
                var pixel = cropped.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = (pixel.Item0 / 255f - Mean[0]) / Std[0];
                tensor[0, 1, y, x] = (pixel.Item1 / 255f - Mean[1]) / Std[1];
                tensor[0, 2, y, x] = (pixel.Item2 / 255f - Mean[2]) / Std[2];
            }
        }

        return tensor;
    }

    private static Mat ResizeToMinSide(Mat image, int minSide)
    {
        var scale = minSide / (float)Math.Min(image.Cols, image.Rows);
        var resized = new Mat();
        Cv2.Resize(
            image,
            resized,
            new Size((int)Math.Round(image.Cols * scale), (int)Math.Round(image.Rows * scale)),
            interpolation: InterpolationFlags.Lanczos4);
        return resized;
    }

    private static Mat PadToMinSize(Mat image, int minSize)
    {
        var bottom = Math.Max(0, minSize - image.Rows);
        var right = Math.Max(0, minSize - image.Cols);
        if (bottom == 0 && right == 0)
            return image.Clone();

        var padded = new Mat();
        Cv2.CopyMakeBorder(image, padded, 0, bottom, 0, right, BorderTypes.Constant, PadColor);
        return padded;
    }

    private static Mat CenterCrop(Mat image, int size)
    {
        var x = Math.Max(0, (image.Cols - size) / 2);
        var y = Math.Max(0, (image.Rows - size) / 2);
        var width = Math.Min(size, image.Cols - x);
        var height = Math.Min(size, image.Rows - y);
        return new Mat(image, new Rect(x, y, width, height)).Clone();
    }

    private static int[] ParseAngleLabels(InferenceSession session)
    {
        if (!session.ModelMetadata.CustomMetadataMap.TryGetValue("character", out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return [0, 90, 180, 270];
        }

        var labels = raw
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();

        return labels.Length > 0 ? labels : [0, 90, 180, 270];
    }

    private void WarmUp()
    {
        using var dummy = new Mat(InputSize, InputSize, MatType.CV_8UC3, PadColor);
        _ = Classify(dummy);
    }

    public void Dispose() => _session.Dispose();
}
