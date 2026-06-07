//-----------------------------------------------------------------------
// <copyright file="DbPostProcess.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using Clipper2Lib;
using OpenCvSharp;

namespace OnnxOcr.Core.Detection;

public sealed class DbPostProcess
{
    private const double ClipperScale = 1000.0;

    private readonly float _thresh;
    private readonly float _boxThresh;
    private readonly int _maxCandidates;
    private readonly float _unclipRatio;
    private readonly string _scoreMode;
    private readonly string _boxType;
    private readonly Mat? _dilationKernel;

    public DbPostProcess(
        float thresh = 0.3f,
        float boxThresh = 0.6f,
        int maxCandidates = 1000,
        float unclipRatio = 1.5f,
        bool useDilation = false,
        string scoreMode = "fast",
        string boxType = "quad")
    {
        _thresh = thresh;
        _boxThresh = boxThresh;
        _maxCandidates = maxCandidates;
        _unclipRatio = unclipRatio;
        _scoreMode = scoreMode;
        _boxType = boxType;

        if (useDilation)
        {
            _dilationKernel = new Mat(2, 2, MatType.CV_8UC1, Scalar.All(1));
        }
    }

    public IReadOnlyList<Point2f[]> Process(float[,,] maps, IReadOnlyList<DetShapeInfo> shapes)
    {
        var batchSize = maps.GetLength(0);
        var results = new List<Point2f[]>();

        for (var batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            var height = maps.GetLength(1);
            var width = maps.GetLength(2);
            var shape = shapes[batchIndex];
            var predMap = new float[height, width];
            using var mask = new Mat(height, width, MatType.CV_8UC1);

            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var value = maps[batchIndex, y, x];
                predMap[y, x] = value;
                mask.Set(y, x, value > _thresh ? (byte)255 : (byte)0);
            }

            if (_dilationKernel != null)
            {
                using var dilated = new Mat();
                Cv2.Dilate(mask, dilated, _dilationKernel);
                dilated.CopyTo(mask);
            }

            var boxes = _boxType == "poly"
                ? PolygonsFromBitmap(predMap, mask, shape.SourceWidth, shape.SourceHeight)
                : BoxesFromBitmap(predMap, mask, shape.SourceWidth, shape.SourceHeight);

            results.AddRange(boxes);
        }

        return results;
    }

    private List<Point2f[]> BoxesFromBitmap(float[,] pred, Mat mask, int destWidth, int destHeight)
    {
        var bitmapHeight = mask.Rows;
        var bitmapWidth = mask.Cols;
        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var numContours = Math.Min(contours.Length, _maxCandidates);
        var boxes = new List<Point2f[]>();

        for (var index = 0; index < numContours; index++)
        {
            var contour = contours[index];
            var (points, shortSide) = GetMiniBoxes(contour);
            if (shortSide < 3)
                continue;

            var score = _scoreMode == "fast"
                ? BoxScoreFast(pred, points)
                : BoxScoreSlow(pred, contour);

            if (_boxThresh > score)
                continue;

            var expanded = Unclip(points, _unclipRatio);
            if (expanded.Length == 0)
                continue;

            var (boxPoints, expandedShortSide) = GetMiniBoxesFromPolygon(expanded);
            if (expandedShortSide < 5)
                continue;

            var mapped = MapBoxToSource(boxPoints, bitmapWidth, bitmapHeight, destWidth, destHeight);
            boxes.Add(mapped);
        }

        return boxes;
    }

    private List<Point2f[]> PolygonsFromBitmap(float[,] pred, Mat mask, int destWidth, int destHeight)
    {
        var bitmapHeight = mask.Rows;
        var bitmapWidth = mask.Cols;
        Cv2.FindContours(mask, out Point[][] contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
        var boxes = new List<Point2f[]>();

        foreach (var contour in contours.Take(_maxCandidates))
        {
            var epsilon = 0.002 * Cv2.ArcLength(contour, true);
            using var approxMat = new Mat();
            Cv2.ApproxPolyDP(InputArray.Create(contour), approxMat, epsilon, true);
            var points = ContourMatToPoints(approxMat);
            if (points.Length < 4)
                continue;

            var score = BoxScoreFast(pred, points);
            if (_boxThresh > score)
                continue;

            var expanded = Unclip(points, _unclipRatio);
            if (expanded.Length != 4)
                continue;

            var (_, shortSide) = GetMiniBoxesFromPoints(expanded);
            if (shortSide < 5)
                continue;

            boxes.Add(MapBoxToSource(expanded, bitmapWidth, bitmapHeight, destWidth, destHeight));
        }

        return boxes;
    }

    private static Point2f[] MapBoxToSource(
        Point2f[] box,
        int bitmapWidth,
        int bitmapHeight,
        int destWidth,
        int destHeight)
    {
        return box.Select(point => new Point2f(
            (float)Math.Clamp(Math.Round(point.X / bitmapWidth * destWidth), 0, destWidth),
            (float)Math.Clamp(Math.Round(point.Y / bitmapHeight * destHeight), 0, destHeight))).ToArray();
    }

    private static Point2f[] Unclip(Point2f[] box, float unclipRatio)
    {
        var area = PolygonArea(box);
        var length = PolygonLength(box);
        if (length <= 0)
            return Array.Empty<Point2f>();

        var distance = area * unclipRatio / length;
        var path = new Path64();
        foreach (var point in box)
            path.Add(new Point64((long)Math.Round(point.X * ClipperScale), (long)Math.Round(point.Y * ClipperScale)));

        var clipper = new ClipperOffset();
        clipper.AddPath(path, JoinType.Round, EndType.Polygon);

        var solution = new Paths64();
        clipper.Execute(distance * ClipperScale, solution);
        if (solution.Count == 0 || solution[0].Count == 0)
            return Array.Empty<Point2f>();

        return solution[0]
            .Select(point => new Point2f((float)(point.X / ClipperScale), (float)(point.Y / ClipperScale)))
            .ToArray();
    }

    private static (Point2f[] Points, float ShortSide) GetMiniBoxes(IReadOnlyList<Point> contour)
    {
        var rotatedRect = Cv2.MinAreaRect(contour);
        var points = Cv2.BoxPoints(rotatedRect)
            .OrderBy(point => point.X)
            .Select(point => new Point2f(point.X, point.Y))
            .ToArray();

        return GetMiniBoxesFromPoints(points, rotatedRect.Size.Width, rotatedRect.Size.Height);
    }

    private static (Point2f[] Points, float ShortSide) GetMiniBoxesFromPolygon(IReadOnlyList<Point2f> points)
    {
        var contour = points.Select(point => new Point((int)point.X, (int)point.Y)).ToArray();
        return GetMiniBoxes(contour);
    }

    private static (Point2f[] Points, float ShortSide) GetMiniBoxesFromPoints(
        Point2f[] points,
        float? width = null,
        float? height = null)
    {
        var sorted = points.OrderBy(point => point.X).ToArray();
        var index1 = 0;
        var index4 = 1;
        if (sorted[1].Y > sorted[0].Y)
        {
            index1 = 0;
            index4 = 1;
        }
        else
        {
            index1 = 1;
            index4 = 0;
        }

        var index2 = 2;
        var index3 = 3;
        if (sorted[3].Y > sorted[2].Y)
        {
            index2 = 2;
            index3 = 3;
        }
        else
        {
            index2 = 3;
            index3 = 2;
        }

        var box = new[]
        {
            sorted[index1],
            sorted[index2],
            sorted[index3],
            sorted[index4],
        };

        var shortSide = width.HasValue && height.HasValue
            ? Math.Min(width.Value, height.Value)
            : Math.Min(
                Distance(box[0], box[1]),
                Distance(box[0], box[3]));

        return (box, shortSide);
    }

    private static float BoxScoreFast(float[,] bitmap, Point2f[] box)
    {
        var height = bitmap.GetLength(0);
        var width = bitmap.GetLength(1);
        var xmin = (int)Math.Clamp(Math.Floor(box.Min(point => point.X)), 0, width - 1);
        var xmax = (int)Math.Clamp(Math.Ceiling(box.Max(point => point.X)), 0, width - 1);
        var ymin = (int)Math.Clamp(Math.Floor(box.Min(point => point.Y)), 0, height - 1);
        var ymax = (int)Math.Clamp(Math.Ceiling(box.Max(point => point.Y)), 0, height - 1);

        using var maskMat = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.All(0));
        var shifted = box
            .Select(point => new Point((int)(point.X - xmin), (int)(point.Y - ymin)))
            .ToArray();

        Cv2.FillPoly(maskMat, new[] { shifted }, Scalar.All(1));

        double sum = 0;
        var count = 0;
        for (var y = ymin; y <= ymax; y++)
        {
            for (var x = xmin; x <= xmax; x++)
            {
                if (maskMat.At<byte>(y - ymin, x - xmin) == 0)
                    continue;

                sum += bitmap[y, x];
                count++;
            }
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    private static float BoxScoreSlow(float[,] bitmap, IReadOnlyList<Point> contour)
    {
        var width = bitmap.GetLength(1);
        var height = bitmap.GetLength(0);
        var points = ContourToPoints(contour);

        var xmin = (int)Math.Clamp(Math.Floor(points.Min(point => point.X)), 0, width - 1);
        var xmax = (int)Math.Clamp(Math.Ceiling(points.Max(point => point.X)), 0, width - 1);
        var ymin = (int)Math.Clamp(Math.Floor(points.Min(point => point.Y)), 0, height - 1);
        var ymax = (int)Math.Clamp(Math.Ceiling(points.Max(point => point.Y)), 0, height - 1);

        using var maskMat = new Mat(ymax - ymin + 1, xmax - xmin + 1, MatType.CV_8UC1, Scalar.All(0));
        var shifted = points
            .Select(point => new Point((int)(point.X - xmin), (int)(point.Y - ymin)))
            .ToArray();

        Cv2.FillPoly(maskMat, new[] { shifted }, Scalar.All(1));

        double sum = 0;
        var count = 0;
        for (var y = ymin; y <= ymax; y++)
        {
            for (var x = xmin; x <= xmax; x++)
            {
                if (maskMat.At<byte>(y - ymin, x - xmin) == 0)
                    continue;

                sum += bitmap[y, x];
                count++;
            }
        }

        return count == 0 ? 0f : (float)(sum / count);
    }

    private static Point2f[] ContourToPoints(IReadOnlyList<Point> contour)
        => contour.Select(point => new Point2f(point.X, point.Y)).ToArray();

    private static Point2f[] ContourMatToPoints(Mat contour)
    {
        var rows = contour.Rows;
        var points = new Point2f[rows];
        for (var i = 0; i < rows; i++)
        {
            var point = contour.At<Point>(i, 0);
            points[i] = new Point2f(point.X, point.Y);
        }

        return points;
    }

    private static double PolygonArea(IReadOnlyList<Point2f> points)
    {
        double area = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }

        return Math.Abs(area) / 2.0;
    }

    private static double PolygonLength(IReadOnlyList<Point2f> points)
    {
        double length = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var j = (i + 1) % points.Count;
            length += Distance(points[i], points[j]);
        }

        return length;
    }

    private static float Distance(Point2f a, Point2f b)
        => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
}

public readonly record struct DetShapeInfo(int SourceHeight, int SourceWidth, float RatioHeight, float RatioWidth);
