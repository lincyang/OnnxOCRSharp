//-----------------------------------------------------------------------
// <copyright file="OcrBatchProgress.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.App.Models;

public sealed class OcrBatchProgress
{
    public required int CurrentIndex { get; init; }
    public required int Total { get; init; }
    public required string CurrentPath { get; init; }
    public required int Succeeded { get; init; }
    public required int Failed { get; init; }
}
