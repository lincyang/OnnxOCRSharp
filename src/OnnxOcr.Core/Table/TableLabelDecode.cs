//-----------------------------------------------------------------------
// <copyright file="TableLabelDecode.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OpenCvSharp;

namespace OnnxOcr.Core.Table;

/// <summary>
/// Decodes SLANet-plus structure tokens and cell boxes (with SLANETPLUS rescale).
/// </summary>
internal sealed class TableLabelDecode
{
    private readonly List<string> _character;
    private readonly Dictionary<string, int> _charToIndex;
    private readonly HashSet<int> _ignoredTokens;
    private readonly int _endIdx;
    private readonly bool _slanetPlus;
    private static readonly HashSet<string> TdTokens = new(StringComparer.Ordinal)
    {
        "<td>", "<td", "<td></td>",
    };

    private const string BegStr = "sos";
    private const string EndStr = "eos";

    public TableLabelDecode(IEnumerable<string> dictCharacter, bool mergeNoSpanStructure = true, bool slanetPlus = true)
    {
        var chars = dictCharacter.ToList();
        if (mergeNoSpanStructure)
        {
            if (!chars.Contains("<td></td>"))
                chars.Add("<td></td>");
            chars.Remove("<td>");
        }

        chars = AddSpecialChar(chars);
        _character = chars;
        _charToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < chars.Count; i++)
            _charToIndex[chars[i]] = i;

        _ignoredTokens = new HashSet<int>
        {
            _charToIndex[BegStr],
            _charToIndex[EndStr],
        };
        _endIdx = _charToIndex[EndStr];
        _slanetPlus = slanetPlus;
    }

    public (IReadOnlyList<string> Structure, float Score, float[][] CellBboxes) Decode(
        float[,] bboxPreds,
        float[,] structureProbs,
        float[] shapeList,
        Mat oriImg)
    {
        // structureProbs: [seq, vocab] for batch item; bboxPreds: [seq, 4] (or 8)
        var seqLen = structureProbs.GetLength(0);
        var vocab = structureProbs.GetLength(1);

        var structureList = new List<string>();
        var bboxList = new List<float[]>();
        var scoreList = new List<float>();

        for (var idx = 0; idx < seqLen; idx++)
        {
            var charIdx = ArgMaxRow(structureProbs, idx, vocab);
            if (idx > 0 && charIdx == _endIdx)
                break;

            if (_ignoredTokens.Contains(charIdx))
                continue;

            var text = _character[charIdx];
            if (TdTokens.Contains(text))
            {
                var bboxDim = bboxPreds.GetLength(1);
                var bbox = new float[bboxDim];
                for (var d = 0; d < bboxDim; d++)
                    bbox[d] = bboxPreds[idx, d];
                BboxDecode(bbox, shapeList);
                bboxList.Add(bbox);
            }

            structureList.Add(text);
            scoreList.Add(MaxRow(structureProbs, idx, vocab));
        }

        var wrapped = WrapWithHtmlStruct(structureList);
        var score = scoreList.Count > 0 ? scoreList.Average() : 0f;
        var cellBboxes = NormalizeBboxes(bboxList, oriImg);
        return (wrapped, score, cellBboxes);
    }

    private static int ArgMaxRow(float[,] probs, int row, int cols)
    {
        var best = 0;
        var bestVal = probs[row, 0];
        for (var c = 1; c < cols; c++)
        {
            if (probs[row, c] > bestVal)
            {
                bestVal = probs[row, c];
                best = c;
            }
        }

        return best;
    }

    private static float MaxRow(float[,] probs, int row, int cols)
    {
        var bestVal = probs[row, 0];
        for (var c = 1; c < cols; c++)
        {
            if (probs[row, c] > bestVal)
                bestVal = probs[row, c];
        }

        return bestVal;
    }

    private static void BboxDecode(float[] bbox, float[] shape)
    {
        var h = shape[0];
        var w = shape[1];
        for (var i = 0; i < bbox.Length; i += 2)
            bbox[i] *= w;
        for (var i = 1; i < bbox.Length; i += 2)
            bbox[i] *= h;
    }

    private float[][] NormalizeBboxes(List<float[]> bboxList, Mat oriImg)
    {
        if (bboxList.Count == 0)
            return Array.Empty<float[]>();

        var cellBboxes = bboxList.Select(b => (float[])b.Clone()).ToArray();
        if (_slanetPlus)
            RescaleCellBboxes(oriImg, cellBboxes);

        return FilterBlankBbox(cellBboxes);
    }

    private static void RescaleCellBboxes(Mat img, float[][] cellBboxes)
    {
        var h = img.Rows;
        var w = img.Cols;
        const float resized = TablePreprocess.MaxLen;
        var ratio = Math.Min(resized / h, resized / w);
        var wRatio = resized / (w * ratio);
        var hRatio = resized / (h * ratio);

        foreach (var bbox in cellBboxes)
        {
            for (var i = 0; i < bbox.Length; i += 2)
                bbox[i] *= wRatio;
            for (var i = 1; i < bbox.Length; i += 2)
                bbox[i] *= hRatio;
        }
    }

    private static float[][] FilterBlankBbox(float[][] cellBboxes)
    {
        return cellBboxes
            .Where(b => b.Any(v => v != 0f))
            .ToArray();
    }

    private static List<string> AddSpecialChar(List<string> dictCharacter)
    {
        var result = new List<string>(dictCharacter.Count + 2) { BegStr };
        result.AddRange(dictCharacter);
        result.Add(EndStr);
        return result;
    }

    internal static IReadOnlyList<string> WrapWithHtmlStruct(IReadOnlyList<string> structure)
    {
        var result = new List<string>(structure.Count + 6)
        {
            "<html>", "<body>", "<table>",
        };
        result.AddRange(structure);
        result.Add("</table>");
        result.Add("</body>");
        result.Add("</html>");
        return result;
    }
}
