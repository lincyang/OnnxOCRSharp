//-----------------------------------------------------------------------
// <copyright file="TableRunResult.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.Core.Models;
using OnnxOcr.Core.Table;

namespace OnnxOcr.App.Models;

public sealed class TableRunResult
{
    public required string ImagePath { get; init; }
    public required int ImageWidth { get; init; }
    public required int ImageHeight { get; init; }
    public required string Html { get; init; }
    public required IReadOnlyList<int[]> LogicPoints { get; init; }
    public required IReadOnlyList<float[]> CellBboxes { get; init; }
    public required IReadOnlyList<string> CellTexts { get; init; }
    public required TimeSpan TableElapsed { get; init; }
    public required TimeSpan TotalElapsed { get; init; }
    public float StructureScore { get; init; }
    public required IReadOnlyList<OcrLineItem> OcrLines { get; init; }

    public static TableRunResult From(
        TableResult table,
        OcrResult ocr,
        string imagePath,
        TimeSpan totalElapsed)
    {
        var lines = ocr.Lines
            .Select((line, index) => new OcrLineItem
            {
                Index = index + 1,
                Text = line.Text,
                Score = line.Score,
                Box = line.Box.Select(p => ((double)p.X, (double)p.Y)).ToArray(),
            })
            .ToArray();

        return new TableRunResult
        {
            ImagePath = imagePath,
            ImageWidth = ocr.ImageWidth,
            ImageHeight = ocr.ImageHeight,
            Html = table.Html,
            LogicPoints = table.LogicPoints,
            CellBboxes = table.CellBboxes,
            CellTexts = table.CellTexts,
            TableElapsed = table.Elapsed,
            TotalElapsed = totalElapsed,
            StructureScore = table.StructureScore,
            OcrLines = lines,
        };
    }
}
