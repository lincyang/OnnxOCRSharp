//-----------------------------------------------------------------------
// <copyright file="CtcLabelDecode.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Recognition;

public sealed class CtcLabelDecode
{
    private readonly List<string> _characters;

    public CtcLabelDecode(string dictionaryPath, bool useSpaceChar)
    {
        _characters = ["blank"];

        foreach (var line in File.ReadLines(dictionaryPath))
        {
            var text = line.Trim('\r', '\n');
            if (text.Length > 0)
                _characters.Add(text);
        }

        if (useSpaceChar)
            _characters.Add(" ");
    }

    public IReadOnlyList<(string Text, float Score)> Decode(float[,,] preds)
    {
        var batchSize = preds.GetLength(0);
        var seqLen = preds.GetLength(1);
        var numClasses = preds.GetLength(2);
        var results = new List<(string Text, float Score)>(batchSize);

        for (var batchIndex = 0; batchIndex < batchSize; batchIndex++)
        {
            var indices = new List<int>(seqLen);
            var probs = new List<float>(seqLen);

            for (var t = 0; t < seqLen; t++)
            {
                var bestIndex = 0;
                var bestProb = preds[batchIndex, t, 0];

                for (var c = 1; c < numClasses; c++)
                {
                    var value = preds[batchIndex, t, c];
                    if (value > bestProb)
                    {
                        bestProb = value;
                        bestIndex = c;
                    }
                }

                indices.Add(bestIndex);
                probs.Add(bestProb);
            }

            results.Add(DecodeSequence(indices, probs));
        }

        return results;
    }

    private (string Text, float Score) DecodeSequence(IReadOnlyList<int> textIndex, IReadOnlyList<float> textProb)
    {
        var chars = new List<string>();
        var confidences = new List<float>();

        for (var i = 0; i < textIndex.Count; i++)
        {
            var token = textIndex[i];
            if (token == 0)
                continue;

            if (i > 0 && token == textIndex[i - 1])
                continue;

            if (token < 0 || token >= _characters.Count)
                continue;

            chars.Add(_characters[token]);
            confidences.Add(textProb[i]);
        }

        var score = confidences.Count == 0 ? 0f : confidences.Average();
        return (string.Concat(chars), score);
    }
}
