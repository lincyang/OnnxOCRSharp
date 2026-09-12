//-----------------------------------------------------------------------
// <copyright file="TableResult.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Table;

public sealed class TableResult
{
    public required string Html { get; init; }
    /// <summary>Each entry is [r0, r1, c0, c1] inclusive logical spans.</summary>
    public required IReadOnlyList<int[]> LogicPoints { get; init; }
    public required IReadOnlyList<float[]> CellBboxes { get; init; }
    /// <summary>OCR text matched into each cell (same order as <see cref="LogicPoints"/>).</summary>
    public IReadOnlyList<string> CellTexts { get; init; } = Array.Empty<string>();
    public required TimeSpan Elapsed { get; init; }
    public float StructureScore { get; init; }
}
