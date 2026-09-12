//-----------------------------------------------------------------------
// <copyright file="TablePreprocess.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OpenCvSharp;

namespace OnnxOcr.Core.Table;

/// <summary>
/// SLANet-plus table preprocess: resize long side to 488, ImageNet normalize, pad to 488x488, CHW.
/// </summary>
internal sealed class TablePreprocess
{
    public const int MaxLen = 488;

    private static readonly float[] Mean = { 0.485f, 0.456f, 0.406f };
    private static readonly float[] Std = { 0.229f, 0.224f, 0.225f };
    private const float Scale = 1f / 255f;

    public (float[,,] Tensor, float[] ShapeList) Run(Mat image)
    {
        if (image.Empty())
            throw new ArgumentException("Input image is empty.", nameof(image));

        var (resized, shapeList) = ResizeImage(image);
        using (resized)
        {
            using var normalized = Normalize(resized);
            using var padded = PadImg(normalized, shapeList);
            var tensor = ToChw(padded);
            return (tensor, shapeList.ToArray());
        }
    }

    private static (Mat Image, List<float> Shape) ResizeImage(Mat img)
    {
        var h = img.Rows;
        var w = img.Cols;
        var ratio = MaxLen / (float)Math.Max(h, w);
        var resizeH = (int)(h * ratio);
        var resizeW = (int)(w * ratio);

        var resized = new Mat();
        Cv2.Resize(img, resized, new Size(resizeW, resizeH));
        return (resized, new List<float> { h, w, ratio, ratio });
    }

    private static Mat Normalize(Mat img)
    {
        using var floatImage = new Mat();
        img.ConvertTo(floatImage, MatType.CV_32FC3, Scale);

        var channels = Cv2.Split(floatImage);
        try
        {
            var normalizedChannels = new Mat[3];
            for (var i = 0; i < 3; i++)
            {
                normalizedChannels[i] = new Mat();
                // (pixel - mean) / std
                channels[i].ConvertTo(normalizedChannels[i], MatType.CV_32FC1, 1.0 / Std[i], -Mean[i] / Std[i]);
            }

            var merged = new Mat();
            Cv2.Merge(normalizedChannels, merged);
            foreach (var ch in normalizedChannels)
                ch.Dispose();
            return merged;
        }
        finally
        {
            foreach (var ch in channels)
                ch.Dispose();
        }
    }

    private static Mat PadImg(Mat img, List<float> shape)
    {
        var padding = new Mat(MaxLen, MaxLen, MatType.CV_32FC3, Scalar.All(0));
        var h = img.Rows;
        var w = img.Cols;
        img.CopyTo(new Mat(padding, new Rect(0, 0, w, h)));
        shape.Add(MaxLen);
        shape.Add(MaxLen);
        return padding;
    }

    private static float[,,] ToChw(Mat image)
    {
        var height = image.Rows;
        var width = image.Cols;
        var tensor = new float[3, height, width];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var pixel = image.At<Vec3f>(y, x);
            tensor[0, y, x] = pixel.Item0;
            tensor[1, y, x] = pixel.Item1;
            tensor[2, y, x] = pixel.Item2;
        }

        return tensor;
    }
}
