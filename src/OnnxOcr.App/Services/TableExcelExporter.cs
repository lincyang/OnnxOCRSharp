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
using ClosedXML.Excel;

namespace OnnxOcr.App.Services;

/// <summary>
/// Writes table recognition LogicPoints + cell texts to an .xlsx workbook via ClosedXML.
/// </summary>
public static class TableExcelExporter
{
    public static void Export(
        string outputPath,
        IReadOnlyList<int[]> logicPoints,
        IReadOnlyList<string> cellTexts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(logicPoints);
        ArgumentNullException.ThrowIfNull(cellTexts);

        using var workbook = BuildWorkbook(logicPoints, cellTexts);
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        workbook.SaveAs(outputPath);
    }

    public static byte[] ExportToBytes(
        IReadOnlyList<int[]> logicPoints,
        IReadOnlyList<string> cellTexts)
    {
        ArgumentNullException.ThrowIfNull(logicPoints);
        ArgumentNullException.ThrowIfNull(cellTexts);

        using var workbook = BuildWorkbook(logicPoints, cellTexts);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static XLWorkbook BuildWorkbook(
        IReadOnlyList<int[]> logicPoints,
        IReadOnlyList<string> cellTexts)
    {
        var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sheet1");

        if (logicPoints.Count == 0)
            return workbook;

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

        for (var i = 0; i < logicPoints.Count; i++)
        {
            var lp = logicPoints[i];
            if (lp.Length < 4)
                continue;

            var r0 = lp[0];
            var r1 = lp[1];
            var c0 = lp[2];
            var c1 = lp[3];
            if (r0 < 0 || c0 < 0 || r0 >= maxRow || c0 >= maxCol)
                continue;

            var text = i < cellTexts.Count ? cellTexts[i] ?? "" : "";
            var excelRow = r0 + 1;
            var excelCol = c0 + 1;
            var cell = ws.Cell(excelRow, excelCol);
            cell.Value = text;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var rowSpan = r1 - r0 + 1;
            var colSpan = c1 - c0 + 1;
            if (rowSpan > 1 || colSpan > 1)
            {
                var range = ws.Range(excelRow, excelCol, r1 + 1, c1 + 1);
                range.Merge();
                range.Style.Alignment.WrapText = true;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
        }

        if (maxCol > 0)
        {
            for (var c = 1; c <= maxCol; c++)
                ws.Column(c).AdjustToContents(1, Math.Max(maxRow, 1), 8.0, 40.0);
        }

        return workbook;
    }
}
