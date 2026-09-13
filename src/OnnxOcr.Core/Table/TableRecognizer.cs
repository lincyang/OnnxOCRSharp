//-----------------------------------------------------------------------
// <copyright file="TableRecognizer.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Models;
using OnnxOcr.Core;
using OpenCvSharp;

namespace OnnxOcr.Core.Table;

/// <summary>
/// Public API for table recognition: SLANet-plus structure + OCR-grid fallback
/// (same auto routing as onnx-ocr-python table_recognition).
/// </summary>
public sealed class TableRecognizer : IDisposable
{
    private readonly TableStructurer _structurer;
    private readonly TableMatch _matcher = new();

    public TableRecognizer(string modelPath, OcrOptions? sessionOptions = null)
    {
        _structurer = new TableStructurer(modelPath, sessionOptions);
    }

    public TableResult Recognize(Mat image, IReadOnlyList<TextLine> ocrLines)
    {
        if (image.Empty())
            throw new ArgumentException("Input image is empty.", nameof(image));

        ArgumentNullException.ThrowIfNull(ocrLines);

        var started = DateTime.UtcNow;

        // Primary path: SLANet structure + OCR→cell match (best for most tables).
        var (structure, score, cellBboxes) = _structurer.Run(image);
        var logicPoints = _matcher.DecodeOneLogicPoints(structure);
        var (dtBoxes, recRes) = FormatOcrResults(ocrLines, image.Rows, image.Cols);
        var (html, cellTexts) = _matcher.ProcessOne(structure, cellBboxes, dtBoxes, recRes);
        var filled = cellTexts.Count(t => !string.IsNullOrWhiteSpace(t));
        OcrLogger.Log(
            $"[TableRecognizer] SLANet score={score:F4} cells={cellTexts.Count} filled={filled} " +
            $"ocrLines={ocrLines.Count}");

        // Only when SLANet matched nothing: OCR-grid fallback (spreadsheet / dual tables).
        // Do NOT prefer grid by heuristic — that regresses tables SLANet already handles well.
        if (filled == 0 && ocrLines.Count > 0)
        {
            var (useGrid, gridScore) = TableOcrGrid.PreferOcrGrid(ocrLines, image.Cols);
            OcrLogger.Log(
                $"[TableRecognizer] SLANet empty match → try OCR-grid (gridScore={gridScore:F3} useGrid={useGrid})");
            if (useGrid || ocrLines.Count >= 8)
            {
                var gridResult = TableOcrGrid.BuildTableResult(ocrLines, image, DateTime.UtcNow - started);
                var gridFilled = gridResult.CellTexts.Count(t => !string.IsNullOrWhiteSpace(t));
                OcrLogger.Log($"[TableRecognizer] OCR-grid filled={gridFilled}");
                if (gridFilled > 0)
                    return gridResult;
            }
        }

        return new TableResult
        {
            Html = html,
            LogicPoints = logicPoints,
            CellBboxes = cellBboxes,
            CellTexts = cellTexts,
            StructureScore = score,
            Elapsed = DateTime.UtcNow - started,
        };
    }

    /// <summary>
    /// Converts TextLine quads to axis-aligned [x1,y1,x2,y2] (RapidTable format_ocr_results).
    /// </summary>
    public static (float[][] DtBoxes, List<(string Text, float Score)> RecRes) FormatOcrResults(
        IReadOnlyList<TextLine> ocrLines,
        int imgH,
        int imgW)
    {
        var dtBoxes = new float[ocrLines.Count][];
        var recRes = new List<(string Text, float Score)>(ocrLines.Count);

        for (var i = 0; i < ocrLines.Count; i++)
        {
            var line = ocrLines[i];
            var box = line.Box;
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            foreach (var p in box)
            {
                minX = Math.Min(minX, p.X);
                minY = Math.Min(minY, p.Y);
                maxX = Math.Max(maxX, p.X);
                maxY = Math.Max(maxY, p.Y);
            }

            minX = Math.Max(minX, 0);
            minY = Math.Max(minY, 0);
            maxX = Math.Min(maxX, imgW);
            maxY = Math.Min(maxY, imgH);

            dtBoxes[i] = new[] { minX, minY, maxX, maxY };
            recRes.Add((line.Text, line.Score));
        }

        return (dtBoxes, recRes);
    }

    public void Dispose() => _structurer.Dispose();
}
