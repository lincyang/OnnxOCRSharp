//-----------------------------------------------------------------------
// <copyright file="ImageCropper.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OpenCvSharp;

namespace OnnxOcr.Core.Imaging;

internal static class ImageCropper
{
    public static Mat Crop(Mat image, Point2f[] points, string boxType)
    {
        return boxType == "quad"
            ? GetRotateCropImage(image, points)
            : GetMinAreaRectCrop(image, points);
    }

    private static Mat GetRotateCropImage(Mat image, Point2f[] points)
    {
        if (points.Length != 4)
            throw new ArgumentException("A text box must contain 4 points.", nameof(points));

        var cropWidth = (int)Math.Max(Distance(points[0], points[1]), Distance(points[2], points[3]));
        var cropHeight = (int)Math.Max(Distance(points[0], points[3]), Distance(points[1], points[2]));

        var src = InputArray.Create(points);
        var dst = InputArray.Create(new[]
        {
            new Point2f(0, 0),
            new Point2f(cropWidth, 0),
            new Point2f(cropWidth, cropHeight),
            new Point2f(0, cropHeight),
        });

        using var matrix = Cv2.GetPerspectiveTransform(src, dst);
        var cropped = new Mat();
        Cv2.WarpPerspective(
            image,
            cropped,
            matrix,
            new Size(cropWidth, cropHeight),
            InterpolationFlags.Cubic,
            BorderTypes.Replicate);

        if (cropped.Rows * 1.0 / cropped.Cols >= 1.5)
        {
            using var rotated = new Mat();
            Cv2.Rotate(cropped, rotated, RotateFlags.Rotate90Clockwise);
            cropped.Dispose();
            return rotated;
        }

        return cropped;
    }

    private static Mat GetMinAreaRectCrop(Mat image, Point2f[] points)
    {
        var rotatedRect = Cv2.MinAreaRect(points.Select(point => new Point((int)point.X, (int)point.Y)).ToArray());
        var boxPoints = Cv2.BoxPoints(rotatedRect)
            .OrderBy(point => point.X)
            .Select(point => new Point2f(point.X, point.Y))
            .ToArray();

        var indexA = 0;
        var indexD = 1;
        if (boxPoints[1].Y > boxPoints[0].Y)
        {
            indexA = 0;
            indexD = 1;
        }
        else
        {
            indexA = 1;
            indexD = 0;
        }

        var indexB = 2;
        var indexC = 3;
        if (boxPoints[3].Y > boxPoints[2].Y)
        {
            indexB = 2;
            indexC = 3;
        }
        else
        {
            indexB = 3;
            indexC = 2;
        }

        var ordered = new[]
        {
            boxPoints[indexA],
            boxPoints[indexB],
            boxPoints[indexC],
            boxPoints[indexD],
        };

        return GetRotateCropImage(image, ordered);
    }

    private static float Distance(Point2f a, Point2f b)
        => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}
