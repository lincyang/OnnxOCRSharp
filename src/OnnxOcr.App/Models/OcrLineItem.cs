//-----------------------------------------------------------------------
// <copyright file="OcrLineItem.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.App.Models;

public sealed class OcrLineItem
{
    public required int Index { get; init; }
    public required string Text { get; init; }
    public required float Score { get; init; }

    /// <summary>四边形顶�?[x, y]，共 4 个点�?/summary>
    public required IReadOnlyList<(double X, double Y)> Box { get; init; }
}
