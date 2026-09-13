//-----------------------------------------------------------------------
// <copyright file="TableExcelExporter.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using OnnxOcr.Core;

namespace OnnxOcr.App.Services;

/// <summary>
/// Writes table recognition results to an .xlsx workbook via ClosedXML.
/// Prefers HTML→flat grid; always keeps an OCR plain-text sheet as fallback
/// so exports are never blank when OCR succeeded.
/// </summary>
public static class TableExcelExporter
{
    public sealed class ExportStats
    {
        public int TableFilledCells { get; init; }
        public int OcrLineCount { get; init; }
        public string Mode { get; init; } = "";
        public long FileBytes { get; init; }
    }

    public static ExportStats Export(
        string outputPath,
        IReadOnlyList<int[]> logicPoints,
        IReadOnlyList<string> cellTexts,
        string? html = null,
        IReadOnlyList<string>? ocrLines = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(logicPoints);
        ArgumentNullException.ThrowIfNull(cellTexts);

        using var workbook = BuildWorkbook(logicPoints, cellTexts, html, ocrLines, out var mode, out var tableFilled);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // ClosedXML can leave a 0-byte file if SaveAs fails mid-write; write via temp then replace.
        var tempPath = outputPath + ".tmp.xlsx";
        try
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            workbook.SaveAs(tempPath);
            File.Copy(tempPath, outputPath, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { /* ignore */ }
        }

        var bytes = new FileInfo(outputPath).Length;
        var verified = VerifyFilledCells(outputPath);
        OcrLogger.Log(
            $"[TableExcel] saved={outputPath}, mode={mode}, tableFilled={tableFilled}, " +
            $"verifiedFilled={verified}, ocrLines={ocrLines?.Count ?? 0}, bytes={bytes}");

        if (verified == 0 && (ocrLines?.Count ?? 0) > 0)
        {
            // Last resort: rewrite as a simple OCR-only workbook.
            using var fallback = BuildOcrOnlyWorkbook(ocrLines!);
            fallback.SaveAs(outputPath);
            verified = VerifyFilledCells(outputPath);
            mode = "ocr-fallback";
            tableFilled = verified;
            bytes = new FileInfo(outputPath).Length;
            OcrLogger.Log($"[TableExcel] OCR fallback rewrite, verifiedFilled={verified}, bytes={bytes}");
        }

        return new ExportStats
        {
            TableFilledCells = tableFilled,
            OcrLineCount = ocrLines?.Count ?? 0,
            Mode = mode,
            FileBytes = bytes,
        };
    }

    public static byte[] ExportToBytes(
        IReadOnlyList<int[]> logicPoints,
        IReadOnlyList<string> cellTexts,
        string? html = null,
        IReadOnlyList<string>? ocrLines = null)
    {
        ArgumentNullException.ThrowIfNull(logicPoints);
        ArgumentNullException.ThrowIfNull(cellTexts);

        using var workbook = BuildWorkbook(logicPoints, cellTexts, html, ocrLines, out _, out _);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLWorkbook BuildWorkbook(
        IReadOnlyList<int[]> logicPoints,
        IReadOnlyList<string> cellTexts,
        string? html,
        IReadOnlyList<string>? ocrLines,
        out string mode,
        out int tableFilled)
    {
        var workbook = new XLWorkbook();
        // ClosedXML 0.105+: new XLWorkbook() has zero worksheets — must AddWorksheet.
        var ws = workbook.Worksheets.Count > 0
            ? workbook.Worksheet(1)
            : workbook.AddWorksheet("Sheet1");
        if (ws.Name != "Sheet1")
            ws.Name = "Sheet1";

        var grid = TryBuildGridFromHtml(html);
        if (grid is { Count: > 0 } && grid.Any(row => row.Any(c => !string.IsNullOrWhiteSpace(c))))
        {
            tableFilled = WriteGrid(ws, grid);
            mode = "html-grid";
        }
        else
        {
            tableFilled = WriteFromLogicPoints(ws, logicPoints, cellTexts);
            mode = tableFilled > 0 ? "logic-points" : "empty-table";
        }

        if (tableFilled == 0 && cellTexts.Any(t => !string.IsNullOrWhiteSpace(t)))
        {
            // Dense dump of CellTexts when geometry mapping failed.
            tableFilled = WriteCellTextsSequential(ws, cellTexts);
            mode = "celltexts-sequential";
        }

        WriteOcrSheet(workbook, ocrLines);

        OcrLogger.Log(
            $"[TableExcel] build mode={mode}, tableFilled={tableFilled}, " +
            $"logic={logicPoints.Count}, cellTexts={cellTexts.Count}, " +
            $"htmlLen={html?.Length ?? 0}, nonEmptyCellTexts={cellTexts.Count(t => !string.IsNullOrWhiteSpace(t))}");

        return workbook;
    }

    private static XLWorkbook BuildOcrOnlyWorkbook(IReadOnlyList<string> ocrLines)
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Count > 0
            ? workbook.Worksheet(1)
            : workbook.AddWorksheet("OCR原文");
        ws.Name = "OCR原文";
        ws.Cell(1, 1).SetValue("序号");
        ws.Cell(1, 2).SetValue("识别文本");
        for (var i = 0; i < ocrLines.Count; i++)
        {
            ws.Cell(i + 2, 1).SetValue(i + 1);
            SetCellText(ws.Cell(i + 2, 2), ocrLines[i] ?? "");
        }

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 40;
        return workbook;
    }

    private static void WriteOcrSheet(XLWorkbook workbook, IReadOnlyList<string>? ocrLines)
    {
        if (ocrLines is not { Count: > 0 })
            return;

        var ws = workbook.Worksheets.Add("OCR原文");
        ws.Cell(1, 1).SetValue("序号");
        ws.Cell(1, 2).SetValue("识别文本");
        for (var i = 0; i < ocrLines.Count; i++)
        {
            ws.Cell(i + 2, 1).SetValue(i + 1);
            SetCellText(ws.Cell(i + 2, 2), ocrLines[i] ?? "");
        }

        ws.Column(1).Width = 8;
        ws.Column(2).Width = 40;
    }

    private static int WriteGrid(IXLWorksheet ws, IReadOnlyList<IReadOnlyList<string>> grid)
    {
        var maxCol = grid.Max(r => r.Count);
        var filled = 0;
        for (var r = 0; r < grid.Count; r++)
        {
            var row = grid[r];
            for (var c = 0; c < maxCol; c++)
            {
                var text = c < row.Count ? row[c] ?? "" : "";
                var cell = ws.Cell(r + 1, c + 1);
                if (string.IsNullOrEmpty(text))
                    continue;
                SetCellText(cell, text);
                filled++;
            }
        }

        if (maxCol > 0)
        {
            for (var c = 1; c <= maxCol; c++)
                ws.Column(c).AdjustToContents(1, Math.Max(grid.Count, 1), 8.0, 40.0);
        }

        return filled;
    }

    private static int WriteFromLogicPoints(
        IXLWorksheet ws,
        IReadOnlyList<int[]> logicPoints,
        IReadOnlyList<string> cellTexts)
    {
        if (logicPoints.Count == 0)
            return 0;

        var maxRow = 0;
        var maxCol = 0;
        for (var i = 0; i < logicPoints.Count; i++)
        {
            var lp = logicPoints[i];
            if (lp.Length < 4)
                continue;
            maxRow = Math.Max(maxRow, lp[1] + 1);
            maxCol = Math.Max(maxCol, lp[3] + 1);
        }

        var filled = 0;
        for (var i = 0; i < logicPoints.Count; i++)
        {
            var lp = logicPoints[i];
            if (lp.Length < 4)
                continue;

            var r0 = lp[0];
            var c0 = lp[2];
            if (r0 < 0 || c0 < 0 || r0 >= maxRow || c0 >= maxCol)
                continue;

            var text = i < cellTexts.Count ? cellTexts[i] ?? "" : "";
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var cell = ws.Cell(r0 + 1, c0 + 1);
            if (!cell.IsEmpty() && !string.IsNullOrWhiteSpace(cell.GetString()))
                continue;

            SetCellText(cell, text);
            filled++;
        }

        if (maxCol > 0)
        {
            for (var c = 1; c <= maxCol; c++)
                ws.Column(c).AdjustToContents(1, Math.Max(maxRow, 1), 8.0, 40.0);
        }

        return filled;
    }

    private static int WriteCellTextsSequential(IXLWorksheet ws, IReadOnlyList<string> cellTexts)
    {
        var filled = 0;
        var row = 1;
        foreach (var text in cellTexts)
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;
            SetCellText(ws.Cell(row, 1), text);
            filled++;
            row++;
        }

        if (filled > 0)
            ws.Column(1).AdjustToContents(1, filled, 8.0, 60.0);
        return filled;
    }

    private static void SetCellText(IXLCell cell, string text)
    {
        // Prefer SetValue(string) over Value= to avoid XLCellValue blanking quirks.
        cell.SetValue(text);
        cell.Style.Alignment.WrapText = true;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static int VerifyFilledCells(string path)
    {
        try
        {
            using var wb = new XLWorkbook(path);
            var filled = 0;
            foreach (var ws in wb.Worksheets)
            {
                foreach (var cell in ws.CellsUsed())
                {
                    if (!string.IsNullOrWhiteSpace(cell.GetString()))
                        filled++;
                }
            }

            return filled;
        }
        catch (Exception ex)
        {
            OcrLogger.Log($"[TableExcel] verify failed: {ex.Message}");
            return -1;
        }
    }

    /// <summary>
    /// Flatten RapidTable HTML into a 2D string grid.
    /// Ignores rowspan/colspan (each td is one cell), matching Python html_to_xlsx.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>>? TryBuildGridFromHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var tableMatch = Regex.Match(
            html,
            @"<table\b[^>]*>(.*?)</table>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!tableMatch.Success)
            return null;

        var tableInner = tableMatch.Groups[1].Value;
        var rows = new List<IReadOnlyList<string>>();
        foreach (Match tr in Regex.Matches(
                     tableInner,
                     @"<tr\b[^>]*>(.*?)</tr>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            var rowHtml = tr.Groups[1].Value;
            var cells = ExtractRowCells(rowHtml);
            if (cells.Count > 0)
                rows.Add(cells);
        }

        if (rows.Count == 0)
            return null;

        var maxCol = rows.Max(r => r.Count);
        var padded = new List<IReadOnlyList<string>>(rows.Count);
        foreach (var row in rows)
        {
            if (row.Count == maxCol)
            {
                padded.Add(row);
                continue;
            }

            var copy = new List<string>(row);
            while (copy.Count < maxCol)
                copy.Add("");
            padded.Add(copy);
        }

        return padded;
    }

    private static List<string> ExtractRowCells(string rowHtml)
    {
        var cells = new List<string>();
        foreach (Match m in Regex.Matches(
                     rowHtml,
                     @"<(td|th)\b([^>]*)>(.*?)</\1>",
                     RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            cells.Add(NormalizeCellHtml(m.Groups[3].Value));
        }

        return cells;
    }

    private static string NormalizeCellHtml(string inner)
    {
        if (string.IsNullOrEmpty(inner))
            return "";

        var noTags = Regex.Replace(inner, @"<[^>]+>", "");
        var decoded = WebUtility.HtmlDecode(noTags);
        return decoded.Replace('\u00a0', ' ').Trim();
    }
}
