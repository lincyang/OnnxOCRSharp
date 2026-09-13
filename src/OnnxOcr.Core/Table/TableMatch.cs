//-----------------------------------------------------------------------
// <copyright file="TableMatch.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Table;

/// <summary>
/// Matches OCR text boxes to predicted table cells and builds HTML / logic points.
/// </summary>
public sealed class TableMatch
{
    private static readonly string[] FilterElements = { "<thead>", "</thead>", "<tbody>", "</tbody>" };

    public (string Html, IReadOnlyList<string> CellTexts) ProcessOne(
        IReadOnlyList<string> predStructure,
        float[][] cellBboxes,
        float[][] dtBoxes,
        IReadOnlyList<(string Text, float Score)> recRes)
    {
        var (filteredBoxes, filteredRec) = FilterOcrResult(cellBboxes, dtBoxes, recRes);
        var matchedIndex = MatchResult(cellBboxes, filteredBoxes);
        var (html, _) = GetPredHtml(predStructure, matchedIndex, filteredRec);
        var cellTexts = ExtractCellTexts(predStructure, matchedIndex, filteredRec);
        return (html, cellTexts);
    }

    public IReadOnlyList<int[]> DecodeOneLogicPoints(IReadOnlyList<string> predStructures)
    {
        var logicPoints = new List<int[]>();
        var currentRow = 0;
        var currentCol = 0;
        var occupiedCells = new HashSet<(int Row, int Col)>();

        bool IsOccupied(int row, int col) => occupiedCells.Contains((row, col));

        void MarkOccupied(int row, int col, int rowspan, int colspan)
        {
            for (var r = row; r < row + rowspan; r++)
            for (var c = col; c < col + colspan; c++)
                occupiedCells.Add((r, c));
        }

        var i = 0;
        while (i < predStructures.Count)
        {
            var token = predStructures[i];

            if (token == "<tr>")
            {
                currentCol = 0;
            }
            else if (token == "</tr>")
            {
                currentRow += 1;
            }
            else if (token.StartsWith("<td", StringComparison.Ordinal))
            {
                var colspan = 1;
                var rowspan = 1;
                var j = i;
                if (token != "<td></td>")
                {
                    j += 1;
                    while (j < predStructures.Count && !predStructures[j].StartsWith('>'))
                    {
                        var part = predStructures[j];
                        if (part.Contains("colspan=", StringComparison.Ordinal))
                            colspan = ParseSpanAttr(part);
                        else if (part.Contains("rowspan=", StringComparison.Ordinal))
                            rowspan = ParseSpanAttr(part);
                        j += 1;
                    }
                }

                i = j;

                while (IsOccupied(currentRow, currentCol))
                    currentCol += 1;

                var rStart = currentRow;
                var rEnd = currentRow + rowspan - 1;
                var colStart = currentCol;
                var colEnd = currentCol + colspan - 1;

                logicPoints.Add(new[] { rStart, rEnd, colStart, colEnd });
                MarkOccupied(rStart, colStart, rowspan, colspan);
                currentCol += colspan;
            }

            i += 1;
        }

        return logicPoints;
    }

    private static int ParseSpanAttr(string token)
    {
        var eq = token.IndexOf('=');
        if (eq < 0)
            return 1;
        var value = token[(eq + 1)..].Trim().Trim('"', '\'');
        return int.TryParse(value, out var n) ? n : 1;
    }

    public (float[][] Boxes, List<(string Text, float Score)> Rec) FilterOcrResult(
        float[][] cellBboxes,
        float[][] dtBoxes,
        IReadOnlyList<(string Text, float Score)> recRes)
    {
        if (dtBoxes.Length == 0)
            return (Array.Empty<float[]>(), new List<(string, float)>());

        // No cell boxes → keep all OCR (caller may still fail to match, but do not wipe text).
        if (cellBboxes.Length == 0)
            return (dtBoxes, recRes.ToList());

        var y1 = float.MaxValue;
        var x1 = float.MaxValue;
        foreach (var box in cellBboxes)
        {
            for (var i = 0; i < box.Length; i += 2)
                x1 = Math.Min(x1, box[i]);
            for (var i = 1; i < box.Length; i += 2)
                y1 = Math.Min(y1, box[i]);
        }

        var newBoxes = new List<float[]>();
        var newRec = new List<(string Text, float Score)>();
        for (var i = 0; i < dtBoxes.Length; i++)
        {
            var box = dtBoxes[i];
            var maxY = float.MinValue;
            var maxX = float.MinValue;
            for (var k = 0; k < box.Length; k += 2)
                maxX = Math.Max(maxX, box[k]);
            for (var k = 1; k < box.Length; k += 2)
                maxY = Math.Max(maxY, box[k]);

            if (maxY < y1)
                continue;
            if (maxX < x1 - 2)
                continue;

            newBoxes.Add(box);
            if (i < recRes.Count)
                newRec.Add(recRes[i]);
        }

        // Python RapidTable: if filter removes everything, restore originals.
        if (newBoxes.Count == 0)
            return (dtBoxes, recRes.ToList());

        return (newBoxes.ToArray(), newRec);
    }

    /// <summary>
    /// Match OCR boxes to cells: prefer IoU overlap, then center-in-cell
    /// (ignoring oversized SLANet rowspan boxes), then L1 distance.
    /// Always assigns when any cell exists — never drops OCR solely for IoU≈0
    /// (that produced empty Excel while the UI still showed OCR boxes).
    /// </summary>
    public Dictionary<int, List<int>> MatchResult(
        float[][] cellBboxes,
        float[][] dtBoxes,
        double minIou = 1e-8)
    {
        var matched = new Dictionary<int, List<int>>();
        if (cellBboxes.Length == 0 || dtBoxes.Length == 0)
            return matched;

        for (var i = 0; i < dtBoxes.Length; i++)
        {
            var gtXyxy = AsXyxy(dtBoxes[i]);
            var (gcx, gcy) = BoxCenter(gtXyxy);
            var ocrH = Math.Max(gtXyxy[3] - gtXyxy[1], 1f);
            var bestJ = -1;
            (int Tier, double OneMinusIou, double Dist, double Dy, double Dx)? bestKey = null;

            for (var j = 0; j < cellBboxes.Length; j++)
            {
                var predXyxy = AsXyxy(cellBboxes[j]);
                // Skip all-zero placeholder bboxes kept for index alignment.
                if (predXyxy[0] == 0 && predXyxy[1] == 0 && predXyxy[2] == 0 && predXyxy[3] == 0)
                    continue;

                var iou = ComputeIou(gtXyxy, predXyxy);
                var (pcx, pcy) = BoxCenter(predXyxy);
                var cellH = Math.Max(predXyxy[3] - predXyxy[1], 1f);
                // Giant rowspan cells (e.g. title spanning half the sheet) must not
                // vacuum up every OCR whose center falls inside them.
                var oversized = cellH > Math.Max(ocrH * 4f, 80f);
                var inside =
                    !oversized &&
                    predXyxy[0] - 2 <= gcx && gcx <= predXyxy[2] + 2 &&
                    predXyxy[1] - 2 <= gcy && gcy <= predXyxy[3] + 2;

                // Tier 0: real overlap; 1: center inside a normal cell; 2: distance only.
                int tier;
                if (iou >= minIou)
                    tier = 0;
                else if (inside)
                    tier = 1;
                else
                    tier = 2;

                var key = (
                    Tier: tier,
                    OneMinusIou: 1.0 - iou,
                    Dist: Distance(gtXyxy, predXyxy),
                    Dy: Math.Abs(gcy - pcy),
                    Dx: Math.Abs(gcx - pcx));

                if (bestKey is null || CompareMatchKey(key, bestKey.Value) < 0)
                {
                    bestKey = key;
                    bestJ = j;
                }
            }

            if (bestJ < 0)
                continue;

            if (!matched.TryGetValue(bestJ, out var list))
            {
                list = new List<int>();
                matched[bestJ] = list;
            }

            list.Add(i);
        }

        return matched;
    }

    private static int CompareMatchKey(
        (int Tier, double OneMinusIou, double Dist, double Dy, double Dx) a,
        (int Tier, double OneMinusIou, double Dist, double Dy, double Dx) b)
    {
        var c = a.Tier.CompareTo(b.Tier);
        if (c != 0) return c;
        c = a.OneMinusIou.CompareTo(b.OneMinusIou);
        if (c != 0) return c;
        c = a.Dy.CompareTo(b.Dy);
        if (c != 0) return c;
        c = a.Dx.CompareTo(b.Dx);
        if (c != 0) return c;
        return a.Dist.CompareTo(b.Dist);
    }

    private static float[] AsXyxy(float[] box)
    {
        if (box.Length == 8)
        {
            return new[]
            {
                Math.Min(Math.Min(box[0], box[2]), Math.Min(box[4], box[6])),
                Math.Min(Math.Min(box[1], box[3]), Math.Min(box[5], box[7])),
                Math.Max(Math.Max(box[0], box[2]), Math.Max(box[4], box[6])),
                Math.Max(Math.Max(box[1], box[3]), Math.Max(box[5], box[7])),
            };
        }

        return box.Length >= 4 ? new[] { box[0], box[1], box[2], box[3] } : box;
    }

    private static (float X, float Y) BoxCenter(float[] xyxy) =>
        ((xyxy[0] + xyxy[2]) / 2f, (xyxy[1] + xyxy[3]) / 2f);

    public (string Html, List<string> Tokens) GetPredHtml(
        IReadOnlyList<string> predStructures,
        Dictionary<int, List<int>> matchedIndex,
        IReadOnlyList<(string Text, float Score)> ocrContents)
    {
        var endHtml = new List<string>();
        var tdIndex = 0;

        foreach (var tag in predStructures)
        {
            if (!tag.Contains("</td>", StringComparison.Ordinal))
            {
                endHtml.Add(tag);
                continue;
            }

            if (tag == "<td></td>")
                endHtml.Add("<td>");

            if (matchedIndex.TryGetValue(tdIndex, out var matched))
            {
                var bWith = false;
                if (matched.Count > 0 &&
                    ocrContents[matched[0]].Text.Contains("<b>", StringComparison.Ordinal) &&
                    matched.Count > 1)
                {
                    bWith = true;
                    endHtml.Add("<b>");
                }

                endHtml.Add(MergeCellTexts(matched, ocrContents));

                if (bWith)
                    endHtml.Add("</b>");
            }

            if (tag == "<td></td>")
                endHtml.Add("</td>");
            else
                endHtml.Add(tag);

            tdIndex += 1;
        }

        endHtml = endHtml.Where(v => !FilterElements.Contains(v)).ToList();
        return (string.Concat(endHtml), endHtml);
    }

    private static IReadOnlyList<string> ExtractCellTexts(
        IReadOnlyList<string> predStructures,
        Dictionary<int, List<int>> matchedIndex,
        IReadOnlyList<(string Text, float Score)> ocrContents)
    {
        var texts = new List<string>();
        var tdIndex = 0;

        foreach (var tag in predStructures)
        {
            if (!tag.Contains("</td>", StringComparison.Ordinal))
                continue;

            if (matchedIndex.TryGetValue(tdIndex, out var matched))
                texts.Add(MergeCellTexts(matched, ocrContents));
            else
                texts.Add("");

            tdIndex += 1;
        }

        return texts;
    }

    private static string MergeCellTexts(
        IReadOnlyList<int> ocrIds,
        IReadOnlyList<(string Text, float Score)> ocrContents)
    {
        var parts = new List<string>();
        foreach (var oid in ocrIds)
        {
            if (oid < 0 || oid >= ocrContents.Count)
                continue;
            var content = ocrContents[oid].Text;
            if (string.IsNullOrEmpty(content))
                continue;
            if (content[0] == ' ')
                content = content[1..];
            content = content.Replace("<b>", "", StringComparison.Ordinal)
                .Replace("</b>", "", StringComparison.Ordinal)
                .Trim();
            if (content.Length > 0)
                parts.Add(content);
        }

        if (parts.Count == 0)
            return "";
        if (parts.Count == 1)
            return parts[0];

        return string.Join(" ", parts);
    }

    public static double Distance(float[] box1, float[] box2)
    {
        var x1 = box1[0];
        var y1 = box1[1];
        var x2 = box1[2];
        var y2 = box1[3];
        var x3 = box2[0];
        var y3 = box2[1];
        var x4 = box2[2];
        var y4 = box2[3];
        var dis = Math.Abs(x3 - x1) + Math.Abs(y3 - y1) + Math.Abs(x4 - x2) + Math.Abs(y4 - y2);
        var dis2 = Math.Abs(x3 - x1) + Math.Abs(y3 - y1);
        var dis3 = Math.Abs(x4 - x2) + Math.Abs(y4 - y2);
        return dis + Math.Min(dis2, dis3);
    }

    /// <summary>
    /// Port of RapidTable compute_iou. Boxes are axis-aligned [x1,y1,x2,y2]
    /// (Python helper naming is confusing but IoU value matches).
    /// </summary>
    public static double ComputeIou(float[] rec1, float[] rec2)
    {
        var sRec1 = (rec1[2] - rec1[0]) * (rec1[3] - rec1[1]);
        var sRec2 = (rec2[2] - rec2[0]) * (rec2[3] - rec2[1]);
        var sumArea = sRec1 + sRec2;

        var leftLine = Math.Max(rec1[1], rec2[1]);
        var rightLine = Math.Min(rec1[3], rec2[3]);
        var topLine = Math.Max(rec1[0], rec2[0]);
        var bottomLine = Math.Min(rec1[2], rec2[2]);

        if (leftLine >= rightLine || topLine >= bottomLine)
            return 0.0;

        var intersect = (rightLine - leftLine) * (bottomLine - topLine);
        return intersect / (sumArea - intersect);
    }
}
