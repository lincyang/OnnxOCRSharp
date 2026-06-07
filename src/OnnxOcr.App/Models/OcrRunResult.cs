//-----------------------------------------------------------------------
// <copyright file="OcrRunResult.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.Core.Models;

namespace OnnxOcr.App.Models;

public sealed class OcrRunResult
{
    public required string ImagePath { get; init; }
    public required int ImageWidth { get; init; }
    public required int ImageHeight { get; init; }
    public required TimeSpan Elapsed { get; init; }
    public required IReadOnlyList<OcrLineItem> Lines { get; init; }

    public static OcrRunResult From(OcrResult result, string imagePath)
    {
        var lines = result.Lines
            .Select((line, index) => new OcrLineItem
            {
                Index = index + 1,
                Text = line.Text,
                Score = line.Score,
                Box = line.Box.Select(p => ((double)p.X, (double)p.Y)).ToArray(),
            })
            .ToArray();

        return new OcrRunResult
        {
            ImagePath = imagePath,
            ImageWidth = result.ImageWidth,
            ImageHeight = result.ImageHeight,
            Elapsed = result.Elapsed,
            Lines = lines,
        };
    }
}
