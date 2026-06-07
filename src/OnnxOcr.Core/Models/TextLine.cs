//-----------------------------------------------------------------------
// <copyright file="TextLine.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OpenCvSharp;

namespace OnnxOcr.Core.Models;

public sealed class TextLine
{
    public required Point2f[] Box { get; init; }
    public required string Text { get; init; }
    public required float Score { get; init; }
}
