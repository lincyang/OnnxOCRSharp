//-----------------------------------------------------------------------
// <copyright file="DetPreprocessor.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OpenCvSharp;

namespace OnnxOcr.Core.Detection;

internal static class DetPreprocessor
{
    public static (float[,,] Image, DetShapeInfo Shape) Prepare(Mat source, float limitSideLen, string limitType)
    {
        var ownsWorkingImage = false;
        var working = source;
        if (source.Rows + source.Cols < 64)
        {
            working = PadImage(source);
            ownsWorkingImage = true;
        }

        var (resized, ratioH, ratioW) = ResizeImageType0(working, limitSideLen, limitType);
        if (ownsWorkingImage)
            working.Dispose();

        var normalized = NormalizeImage(resized);
        resized.Dispose();
        var tensor = ToChw(normalized);
        normalized.Dispose();

        return (tensor, new DetShapeInfo(source.Rows, source.Cols, ratioH, ratioW));
    }

    private static Mat PadImage(Mat image)
    {
        var padded = new Mat(Math.Max(32, image.Rows), Math.Max(32, image.Cols), image.Type(), Scalar.All(0));
        image.CopyTo(new Mat(padded, new Rect(0, 0, image.Cols, image.Rows)));
        return padded;
    }

    private static (Mat Image, float RatioH, float RatioW) ResizeImageType0(Mat image, float limitSideLen, string limitType)
    {
        var height = image.Rows;
        var width = image.Cols;

        float ratio = 1f;
        if (limitType == "max")
        {
            if (Math.Max(height, width) > limitSideLen)
                ratio = height > width ? limitSideLen / height : limitSideLen / width;
        }
        else if (limitType == "min")
        {
            if (Math.Min(height, width) < limitSideLen)
                ratio = height < width ? limitSideLen / height : limitSideLen / width;
        }
        else if (limitType == "resize_long")
        {
            ratio = limitSideLen / Math.Max(height, width);
        }
        else
        {
            throw new ArgumentException($"Unsupported limit type: {limitType}", nameof(limitType));
        }

        var resizeH = AlignTo32(Math.Max((int)Math.Round(height * ratio), 32));
        var resizeW = AlignTo32(Math.Max((int)Math.Round(width * ratio), 32));

        var resized = new Mat();
        Cv2.Resize(image, resized, new Size(resizeW, resizeH));
        return (resized, resizeH / (float)height, resizeW / (float)width);
    }

    private static int AlignTo32(int value) => Math.Max((int)Math.Round(value / 32.0) * 32, 32);

    private static Mat NormalizeImage(Mat image)
    {
        var mean = new[] { 0.485f, 0.456f, 0.406f };
        var std = new[] { 0.229f, 0.224f, 0.225f };

        using var floatImage = new Mat();
        image.ConvertTo(floatImage, MatType.CV_32FC3, 1.0 / 255.0);

        var sourceChannels = Cv2.Split(floatImage);
        var normalizedChannels = new Mat[3];
        for (var i = 0; i < 3; i++)
        {
            normalizedChannels[i] = new Mat();
            sourceChannels[i].ConvertTo(normalizedChannels[i], MatType.CV_32FC1, 1.0 / std[i], -mean[i] / std[i]);
            sourceChannels[i].Dispose();
        }

        var normalized = new Mat();
        Cv2.Merge(normalizedChannels, normalized);
        foreach (var channel in normalizedChannels)
            channel.Dispose();

        return normalized;
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
