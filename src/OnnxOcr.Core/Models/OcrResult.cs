//-----------------------------------------------------------------------
// <copyright file="OcrResult.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Models;

public sealed class OcrResult
{
    public IReadOnlyList<TextLine> Lines { get; init; } = Array.Empty<TextLine>();
    public TimeSpan Elapsed { get; init; }
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }

    public static OcrResult Empty(int width, int height) => new()
    {
        ImageWidth = width,
        ImageHeight = height,
    };
}
