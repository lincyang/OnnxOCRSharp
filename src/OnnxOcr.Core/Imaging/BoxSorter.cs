//-----------------------------------------------------------------------
// <copyright file="BoxSorter.cs" company="程序员Linc">
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

internal static class BoxSorter
{
    public static IReadOnlyList<Point2f[]> Sort(IReadOnlyList<Point2f[]> boxes)
    {
        var sorted = boxes
            .OrderBy(box => box[0].Y)
            .ThenBy(box => box[0].X)
            .Select(box => box.ToArray())
            .ToList();

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            for (var j = i; j >= 0; j--)
            {
                if (Math.Abs(sorted[j + 1][0].Y - sorted[j][0].Y) < 10 &&
                    sorted[j + 1][0].X < sorted[j][0].X)
                {
                    (sorted[j], sorted[j + 1]) = (sorted[j + 1], sorted[j]);
                }
                else
                {
                    break;
                }
            }
        }

        return sorted;
    }
}
