//-----------------------------------------------------------------------
// <copyright file="TableOcrGrid.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using OnnxOcr.Core.Models;
using OpenCvSharp;

namespace OnnxOcr.Core.Table;

/// <summary>
/// Rebuild HTML tables from OCR boxes via row/column clustering
/// (port of onnx-ocr-python table_ocr_grid — used for spreadsheet-like images
/// where SLANet cell matching is unreliable).
/// </summary>
public static class TableOcrGrid
{
    private static readonly Regex Digit = new(@"^-?\d+(\.\d+)?$", RegexOptions.Compiled);
    private static readonly Regex Amount = new(@"^-?\d+\.\d{2}$", RegexOptions.Compiled);

    private sealed class Item
    {
        public float Cx;
        public float Cy;
        public float X1;
        public float X2;
        public float W;
        public float H;
        public string T = "";
    }

    public static (bool UseGrid, float Score) PreferOcrGrid(IReadOnlyList<TextLine> lines, int imgW)
    {
        var (score, _) = ScoreSpreadsheetLikeness(lines, imgW);
        return (score >= 0.55f, score);
    }

    public static (float Score, Dictionary<string, float> Feats) ScoreSpreadsheetLikeness(
        IReadOnlyList<TextLine> lines,
        int imgW)
    {
        var feats = new Dictionary<string, float>
        {
            ["n_items"] = 0,
            ["n_rows"] = 0,
            ["pitch_cv"] = 1,
            ["aligned_digit_cols"] = 0,
            ["numeric_ratio"] = 0,
            ["avg_boxes_per_row"] = 0,
        };

        var items = ParseItems(lines);
        feats["n_items"] = items.Count;
        if (items.Count < 8)
            return (0.15f, feats);

        var cys = items.Select(i => i.Cy).ToList();
        var heights = items.Select(i => i.H).ToList();
        var yGap = RowGap(cys, heights);
        var rowClusters = ClusterIndices(cys, yGap);
        var nRows = rowClusters.Count;
        feats["n_rows"] = nRows;
        if (nRows < 4)
            return (0.2f, feats);

        var rowYs = rowClusters
            .Select(cl => Median(cl.Select(i => items[i].Cy).ToList()))
            .ToList();
        var pitches = new List<float>();
        for (var i = 0; i < rowYs.Count - 1; i++)
        {
            var p = rowYs[i + 1] - rowYs[i];
            if (p > 1f)
                pitches.Add(p);
        }

        float pitchCv = 1f;
        if (pitches.Count > 0)
        {
            var meanP = pitches.Average();
            pitchCv = meanP > 1e-6f ? StdDev(pitches) / meanP : 1f;
        }

        feats["pitch_cv"] = pitchCv;
        var boxesPerRow = rowClusters.Select(cl => (float)cl.Count).ToList();
        var avgBpr = boxesPerRow.Count > 0 ? boxesPerRow.Average() : 0f;
        feats["avg_boxes_per_row"] = avgBpr;

        var numeric = items.Where(it => Digit.IsMatch(it.T) || Amount.IsMatch(it.T)).ToList();
        feats["numeric_ratio"] = numeric.Count / (float)Math.Max(items.Count, 1);

        var alignedCols = 0;
        if (numeric.Count >= 4)
        {
            var nx = numeric.Select(it => it.Cx).ToList();
            var medW = Median(numeric.Select(it => it.W).ToList());
            if (medW < 1f) medW = 20f;
            var colCl = ClusterIndices(nx, Math.Max(16f, medW * 0.7f));
            foreach (var cl in colCl)
            {
                if (cl.Count < 3)
                    continue;
                var xs = cl.Select(i => nx[i]).ToList();
                var med = Median(xs);
                var mad = xs.Select(x => Math.Abs(x - med)).Average();
                if (mad <= medW * 0.35f)
                    alignedCols++;
            }
        }

        feats["aligned_digit_cols"] = alignedCols;

        float score = 0;
        if (pitchCv <= 0.18f) score += 0.40f;
        else if (pitchCv <= 0.30f) score += 0.28f;
        else if (pitchCv <= 0.45f) score += 0.14f;

        if (nRows >= 10) score += 0.15f;
        else if (nRows >= 6) score += 0.10f;

        if (alignedCols >= 3) score += 0.25f;
        else if (alignedCols >= 2) score += 0.18f;
        else if (alignedCols >= 1) score += 0.08f;

        if (feats["numeric_ratio"] >= 0.35f) score += 0.12f;
        else if (feats["numeric_ratio"] >= 0.20f) score += 0.06f;

        if (avgBpr >= 3f) score += 0.10f;
        else if (avgBpr >= 2f) score += 0.05f;

        _ = imgW;
        return (Math.Min(1f, score), feats);
    }

    public static IReadOnlyList<IReadOnlyList<string>> BuildGrid(
        IReadOnlyList<TextLine> lines,
        Mat? image = null)
    {
        var items = ParseItems(lines);
        if (items.Count == 0)
            return Array.Empty<IReadOnlyList<string>>();

        var vlines = image is not null && !image.Empty() ? DetectVerticalLineXs(image) : new List<float>();
        var yGap = RowGap(items.Select(i => i.Cy).ToList(), items.Select(i => i.H).ToList());
        var rowClusters = ClusterIndices(items.Select(i => i.Cy).ToList(), yGap);

        var rowsItems = new List<List<Item>>();
        foreach (var cl in rowClusters)
        {
            var row = cl.Select(i => items[i]).OrderBy(x => x.Cx).ToList();
            rowsItems.Add(MergeHorizontalFragments(row, vlines));
        }

        var flat = rowsItems.SelectMany(r => r).ToList();
        var lineCenters = CentersFromVlines(vlines);
        List<float> colCenters;
        float medShortW;
        if (lineCenters.Count >= 3)
        {
            colCenters = lineCenters;
            medShortW = flat.Count > 0 ? Median(flat.Select(i => i.W).ToList()) : 40f;
        }
        else
        {
            (colCenters, medShortW) = ColumnCenters(flat);
        }

        if (colCenters.Count == 0)
            return rowsItems.Select(r => (IReadOnlyList<string>)r.Select(i => i.T).ToList()).ToList();

        var nCols = colCenters.Count;
        var useLineBands = lineCenters.Count >= 3 && vlines.Count >= 2;
        var grid = rowsItems.Select(_ => Enumerable.Repeat("", nCols).ToList()).ToList();

        for (var r = 0; r < rowsItems.Count; r++)
        {
            foreach (var it in rowsItems[r])
            {
                var ax = AssignAnchorX(it, colCenters, medShortW);
                int c;
                if (useLineBands)
                {
                    var band = BandIndexForX(ax, vlines);
                    c = band ?? ArgMin(colCenters.Select(cx => Math.Abs(ax - cx)).ToList());
                    c = Math.Clamp(c, 0, nCols - 1);
                }
                else
                {
                    c = ArgMin(colCenters.Select(cx => Math.Abs(ax - cx)).ToList());
                }

                c = ResolveCollision(grid[r], c, ax, colCenters);
                grid[r][c] = string.IsNullOrEmpty(grid[r][c])
                    ? it.T
                    : $"{grid[r][c]} {it.T}".Trim();
            }
        }

        return DropEmptyRows(DropSparseColumns(grid));
    }

    public static string GridToHtml(IReadOnlyList<IReadOnlyList<string>> grid)
    {
        if (grid.Count == 0)
            return "<html><body><table></table></body></html>";

        var sb = new StringBuilder();
        sb.Append("<html><body><table>");
        foreach (var row in grid)
        {
            sb.Append("<tr>");
            foreach (var cell in row)
                sb.Append("<td>").Append(WebUtility.HtmlEncode(cell ?? "")).Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    public static (IReadOnlyList<int[]> LogicPoints, IReadOnlyList<string> CellTexts) GridToLogicAndTexts(
        IReadOnlyList<IReadOnlyList<string>> grid)
    {
        var logic = new List<int[]>();
        var texts = new List<string>();
        for (var r = 0; r < grid.Count; r++)
        {
            var row = grid[r];
            for (var c = 0; c < row.Count; c++)
            {
                logic.Add(new[] { r, r, c, c });
                texts.Add(row[c] ?? "");
            }
        }

        return (logic, texts);
    }

    public static TableResult BuildTableResult(IReadOnlyList<TextLine> lines, Mat image, TimeSpan elapsed)
    {
        var grid = BuildGrid(lines, image);
        var html = GridToHtml(grid);
        var (logic, texts) = GridToLogicAndTexts(grid);
        return new TableResult
        {
            Html = html,
            LogicPoints = logic,
            CellBboxes = Array.Empty<float[]>(),
            CellTexts = texts,
            StructureScore = 1f,
            Elapsed = elapsed,
        };
    }

    private static List<Item> ParseItems(IReadOnlyList<TextLine> lines)
    {
        var items = new List<Item>();
        foreach (var line in lines)
        {
            var token = (line.Text ?? "").Trim();
            if (token.Length == 0)
                continue;
            var xs = line.Box.Select(p => p.X).ToList();
            var ys = line.Box.Select(p => p.Y).ToList();
            items.Add(new Item
            {
                Cx = xs.Average(),
                Cy = ys.Average(),
                X1 = xs.Min(),
                X2 = xs.Max(),
                W = xs.Max() - xs.Min(),
                H = Math.Max(ys.Max() - ys.Min(), 1f),
                T = token,
            });
        }

        return items;
    }

    private static float RowGap(IReadOnlyList<float> cys, IReadOnlyList<float> heights)
    {
        var ys = cys.OrderBy(v => v).ToList();
        var medH = heights.Count > 0 ? Median(heights.ToList()) : 16f;
        var diffs = new List<float>();
        for (var i = 0; i < ys.Count - 1; i++)
        {
            var d = ys[i + 1] - ys[i];
            if (d > 2 && d < medH * 3)
                diffs.Add(d);
        }

        if (diffs.Count == 0)
            return Math.Max(8f, medH * 0.7f);
        return Math.Max(8f, Median(diffs) * 0.55f);
    }

    private static List<List<int>> ClusterIndices(IReadOnlyList<float> values, float gap)
    {
        if (values.Count == 0)
            return new List<List<int>>();

        var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToList();
        var clusters = new List<List<int>> { new() { order[0] } };
        for (var k = 1; k < order.Count; k++)
        {
            var idx = order[k];
            var prev = clusters[^1][^1];
            if (values[idx] - values[prev] > gap)
                clusters.Add(new List<int> { idx });
            else
                clusters[^1].Add(idx);
        }

        return clusters;
    }

    private static List<Item> MergeHorizontalFragments(List<Item> row, IReadOnlyList<float> vlines)
    {
        if (row.Count == 0)
            return row;

        var merged = new List<Item>
        {
            new()
            {
                Cx = row[0].Cx, Cy = row[0].Cy, X1 = row[0].X1, X2 = row[0].X2,
                W = row[0].W, H = row[0].H, T = row[0].T,
            },
        };

        for (var i = 1; i < row.Count; i++)
        {
            var it = row[i];
            var prev = merged[^1];
            var gap = it.X1 - prev.X2;
            if (HasVlineBetween(prev.X2, it.X1, vlines))
            {
                merged.Add(Clone(it));
                continue;
            }

            var close = gap < Math.Max(10f, prev.H * 0.7f);
            var shortTok = prev.T.Length <= 2 || it.T.Length <= 2;
            var bothCjk = IsCjk(prev.T) && IsCjk(it.T);
            var spacedPair = prev.T.Length == 1 && it.T.Length == 1 && bothCjk && gap >= 0 && gap < 150f;
            var longCont = bothCjk && !IsAtomic(prev.T) && !IsAtomic(it.T)
                           && prev.T.Length >= 4 && it.T.Length >= 4
                           && gap >= -8f && gap < Math.Max(28f, prev.H * 1.2f);

            if ((close && shortTok && bothCjk || spacedPair || longCont)
                && !IsAtomic(prev.T) && !IsAtomic(it.T))
            {
                prev.T += it.T;
                prev.X2 = Math.Max(prev.X2, it.X2);
                prev.Cx = (prev.X1 + prev.X2) / 2f;
                prev.W = prev.X2 - prev.X1;
            }
            else
            {
                merged.Add(Clone(it));
            }
        }

        return merged;
    }

    private static Item Clone(Item it) => new()
    {
        Cx = it.Cx, Cy = it.Cy, X1 = it.X1, X2 = it.X2, W = it.W, H = it.H, T = it.T,
    };

    private static bool IsAtomic(string t) =>
        Amount.IsMatch(t) || Digit.IsMatch(t);

    private static bool IsCjk(string t) => t.Any(ch => ch is >= '\u4e00' and <= '\u9fff');

    private static bool HasVlineBetween(float xLeft, float xRight, IReadOnlyList<float> vlines)
    {
        if (vlines.Count == 0)
            return false;
        var lo = Math.Min(xLeft, xRight);
        var hi = Math.Max(xLeft, xRight);
        if (hi - lo < 4f)
            return false;
        return vlines.Any(vx => lo + 1f < vx && vx < hi - 1f);
    }

    private static (List<float> Centers, float MedShortW) ColumnCenters(List<Item> items)
    {
        if (items.Count == 0)
            return (new List<float>(), 40f);

        var seed = items.Where(IsStructuralSeed).ToList();
        if (seed.Count < 4)
            seed = items;

        var cxs = seed.Select(i => i.Cx).ToList();
        var widths = seed.Select(i => i.W).ToList();
        var medShortW = widths.Count > 0 ? Median(widths) : 40f;

        var xs = cxs.OrderBy(v => v).ToList();
        var xdiffs = new List<float>();
        for (var i = 0; i < xs.Count - 1; i++)
        {
            var d = xs[i + 1] - xs[i];
            if (d > 2 && d < Math.Max(medShortW * 4f, 160f))
                xdiffs.Add(d);
        }

        float xGap;
        if (xdiffs.Count > 0)
        {
            xdiffs.Sort();
            var p25 = xdiffs[Math.Max(0, xdiffs.Count / 4)];
            xGap = Math.Clamp(p25 * 0.7f, 12f, 38f);
        }
        else
        {
            xGap = 24f;
        }

        var clusters = ClusterIndices(cxs, xGap);
        var centers = clusters.Select(cl => Median(cl.Select(i => cxs[i]).ToList())).ToList();
        centers = ConsolidateNearbyCenters(items, centers);
        return (centers, medShortW);
    }

    private static bool IsStructuralSeed(Item it)
    {
        if (IsAtomic(it.T))
            return true;
        if (it.T.Length <= 8 && it.W < 90f)
            return true;
        return false;
    }

    private static List<float> ConsolidateNearbyCenters(List<Item> items, List<float> centers, float minSep = 38f)
    {
        if (centers.Count < 2)
            return centers;
        var ordered = centers.OrderBy(c => c).ToList();
        var strength = CenterStrength(items, ordered);
        var output = new List<float> { ordered[0] };
        var outS = new List<int> { strength[0] };
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i] - output[^1] < minSep)
            {
                if (strength[i] > outS[^1])
                {
                    output[^1] = ordered[i];
                    outS[^1] = strength[i];
                }

                continue;
            }

            output.Add(ordered[i]);
            outS.Add(strength[i]);
        }

        return output;
    }

    private static List<int> CenterStrength(List<Item> items, List<float> centers)
    {
        var strength = Enumerable.Repeat(0, centers.Count).ToList();
        if (centers.Count == 0)
            return strength;
        foreach (var it in items)
        {
            var c = ArgMin(centers.Select(cx => Math.Abs(it.Cx - cx)).ToList());
            strength[c] += IsAtomic(it.T) ? 3 : 1;
        }

        return strength;
    }

    private static float AssignAnchorX(Item it, List<float> centers, float medShortW)
    {
        var cx = it.Cx;
        if (centers.Count == 0 || IsAtomic(it.T))
            return cx;

        var iCx = ArgMin(centers.Select(c => Math.Abs(cx - c)).ToList());
        var left = it.X1 + Math.Min(Math.Max(it.W * 0.12f, 8f), 24f);
        var iLeft = ArgMin(centers.Select(c => Math.Abs(left - c)).ToList());
        if (iCx == iLeft)
            return cx;

        var thr = Math.Max(2f * Math.Max(medShortW, 1f), 90f);
        if (it.W >= thr && centers[iLeft] < centers[iCx] - 8f)
            return left;
        return cx;
    }

    private static int ResolveCollision(List<string> row, int c, float ax, List<float> centers)
    {
        if (string.IsNullOrEmpty(row[c]))
            return c;
        // Prefer nearest empty column.
        var best = c;
        var bestDist = float.MaxValue;
        for (var i = 0; i < centers.Count; i++)
        {
            if (!string.IsNullOrEmpty(row[i]))
                continue;
            var d = Math.Abs(ax - centers[i]);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
            }
        }

        return best;
    }

    private static List<List<string>> DropSparseColumns(List<List<string>> grid)
    {
        if (grid.Count == 0)
            return grid;
        var nCols = grid.Max(r => r.Count);
        var keep = new List<int>();
        for (var c = 0; c < nCols; c++)
        {
            var filled = grid.Count(r => c < r.Count && !string.IsNullOrWhiteSpace(r[c]));
            if (filled >= Math.Max(1, (int)Math.Ceiling(grid.Count * 0.08)))
                keep.Add(c);
        }

        if (keep.Count == 0 || keep.Count == nCols)
            return grid;

        return grid.Select(r => keep.Select(c => c < r.Count ? r[c] : "").ToList()).ToList();
    }

    private static List<IReadOnlyList<string>> DropEmptyRows(List<List<string>> grid) =>
        grid.Where(r => r.Any(c => !string.IsNullOrWhiteSpace(c)))
            .Select(r => (IReadOnlyList<string>)r)
            .ToList();

    private static List<float> DetectVerticalLineXs(Mat image)
    {
        try
        {
            using var gray = image.Channels() == 1 ? image.Clone() : image.CvtColor(ColorConversionCodes.BGR2GRAY);
            var h = gray.Rows;
            var w = gray.Cols;
            if (h < 40 || w < 80)
                return new List<float>();

            using var blur = new Mat();
            Cv2.GaussianBlur(gray, blur, new Size(3, 3), 0);
            using var binary = new Mat();
            Cv2.AdaptiveThreshold(blur, binary, 255, AdaptiveThresholdTypes.MeanC, ThresholdTypes.BinaryInv, 15, 8);
            var kH = Math.Max(12, h / 25);
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, kH));
            using var vertical = new Mat();
            Cv2.MorphologyEx(binary, vertical, MorphTypes.Open, kernel);
            using var dilateK = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 3));
            Cv2.Dilate(vertical, vertical, dilateK);

            var proj = new double[w];
            for (var x = 0; x < w; x++)
            {
                double sum = 0;
                for (var y = 0; y < h; y++)
                    sum += vertical.At<byte>(y, x);
                proj[x] = sum;
            }

            var max = proj.Max();
            if (max < 1)
                return new List<float>();

            var sorted = proj.OrderBy(v => v).ToList();
            var p90 = sorted[(int)(sorted.Count * 0.9)];
            var thr = Math.Max(p90 * 0.45, 255.0 * h * 0.12);
            var xs = new List<float>();
            var i = 0;
            while (i < w)
            {
                if (proj[i] < thr)
                {
                    i++;
                    continue;
                }

                var j = i;
                var peak = i;
                var peakVal = proj[i];
                while (j < w && proj[j] >= thr)
                {
                    if (proj[j] > peakVal)
                    {
                        peakVal = proj[j];
                        peak = j;
                    }

                    j++;
                }

                xs.Add(peak);
                i = j;
            }

            xs = DedupeSorted(xs, Math.Max(18f, w * 0.028f));
            if (xs.Count < 3)
                return new List<float>();
            var interior = xs.Where(x => 0.08 * w < x && x < 0.92 * w).ToList();
            if (interior.Count >= 3)
                xs = interior;
            return xs.Count >= 3 ? xs : new List<float>();
        }
        catch
        {
            return new List<float>();
        }
    }

    private static List<float> CentersFromVlines(IReadOnlyList<float> vlines)
    {
        if (vlines.Count < 2)
            return new List<float>();
        var xs = vlines.OrderBy(v => v).ToList();
        var centers = new List<float>();
        for (var i = 0; i < xs.Count - 1; i++)
        {
            if (xs[i + 1] - xs[i] >= 16f)
                centers.Add((xs[i] + xs[i + 1]) / 2f);
        }

        return centers;
    }

    private static int? BandIndexForX(float x, IReadOnlyList<float> vlines)
    {
        if (vlines.Count < 2)
            return null;
        var xs = vlines.OrderBy(v => v).ToList();
        if (x < xs[0] - 2)
            return 0;
        for (var i = 0; i < xs.Count - 1; i++)
        {
            if (xs[i] - 1 <= x && x <= xs[i + 1] + 1)
                return i;
        }

        return xs.Count - 2;
    }

    private static List<float> DedupeSorted(List<float> xs, float minSep)
    {
        if (xs.Count == 0)
            return xs;
        var output = new List<float> { xs[0] };
        for (var i = 1; i < xs.Count; i++)
        {
            if (xs[i] - output[^1] >= minSep)
                output.Add(xs[i]);
            else
                output[^1] = (output[^1] + xs[i]) / 2f;
        }

        return output;
    }

    private static float Median(List<float> values)
    {
        if (values.Count == 0)
            return 0;
        var s = values.OrderBy(v => v).ToList();
        var m = s.Count / 2;
        return s.Count % 2 == 0 ? (s[m - 1] + s[m]) / 2f : s[m];
    }

    private static float StdDev(List<float> values)
    {
        if (values.Count == 0)
            return 0;
        var mean = values.Average();
        return (float)Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));
    }

    private static int ArgMin(List<float> values)
    {
        var best = 0;
        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] < values[best])
                best = i;
        }

        return best;
    }
}
