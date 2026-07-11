//-----------------------------------------------------------------------
// <copyright file="BoxOverlapFilter.cs" company="程序员Linc">
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

internal static class BoxOverlapFilter
{
    private const float ContainmentThreshold = 0.85f;
    private const float MaxNestedAreaRatio = 0.6f;

    public static IReadOnlyList<Point2f[]> RemoveNestedBoxes(IReadOnlyList<Point2f[]> boxes)
    {
        if (boxes.Count <= 1)
            return boxes;

        var entries = boxes
            .Select((box, index) => new BoxEntry(index, box, GetAxisAlignedBounds(box)))
            .OrderByDescending(entry => entry.Bounds.Area)
            .ToList();

        var removed = new HashSet<int>();
        for (var i = 0; i < entries.Count; i++)
        {
            var larger = entries[i];
            if (removed.Contains(larger.Index))
                continue;

            for (var j = i + 1; j < entries.Count; j++)
            {
                var smaller = entries[j];
                if (removed.Contains(smaller.Index))
                    continue;

                if (!ShouldRemoveNestedBox(larger.Bounds, smaller.Bounds))
                    continue;

                removed.Add(smaller.Index);
            }
        }

        return boxes.Where((_, index) => !removed.Contains(index)).ToList();
    }

    private static bool ShouldRemoveNestedBox(AxisAlignedBounds larger, AxisAlignedBounds smaller)
    {
        if (smaller.Area <= 0 || larger.Area <= 0)
            return false;

        var intersection = larger.IntersectionArea(smaller);
        if (intersection <= 0)
            return false;

        var containment = intersection / smaller.Area;
        var areaRatio = smaller.Area / larger.Area;
        return containment >= ContainmentThreshold && areaRatio <= MaxNestedAreaRatio;
    }

    private static AxisAlignedBounds GetAxisAlignedBounds(Point2f[] box)
    {
        var minX = box.Min(point => point.X);
        var maxX = box.Max(point => point.X);
        var minY = box.Min(point => point.Y);
        var maxY = box.Max(point => point.Y);
        return new AxisAlignedBounds(minX, minY, maxX, maxY);
    }

    private readonly record struct BoxEntry(int Index, Point2f[] Box, AxisAlignedBounds Bounds);

    private readonly record struct AxisAlignedBounds(float MinX, float MinY, float MaxX, float MaxY)
    {
        public float Area
        {
            get
            {
                var width = Math.Max(0f, MaxX - MinX);
                var height = Math.Max(0f, MaxY - MinY);
                return width * height;
            }
        }

        public float IntersectionArea(AxisAlignedBounds other)
        {
            var left = Math.Max(MinX, other.MinX);
            var top = Math.Max(MinY, other.MinY);
            var right = Math.Min(MaxX, other.MaxX);
            var bottom = Math.Min(MaxY, other.MaxY);
            var width = Math.Max(0f, right - left);
            var height = Math.Max(0f, bottom - top);
            return width * height;
        }
    }
}
