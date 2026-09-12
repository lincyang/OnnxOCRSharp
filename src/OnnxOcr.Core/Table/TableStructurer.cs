//-----------------------------------------------------------------------
// <copyright file="TableStructurer.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Inference;
using OpenCvSharp;

namespace OnnxOcr.Core.Table;

/// <summary>
/// Runs SLANet-plus ONNX: preprocess → infer → postprocess.
/// </summary>
public sealed class TableStructurer : IDisposable
{
    private readonly InferenceSession _session;
    private readonly TablePreprocess _preprocess = new();
    private readonly TableLabelDecode _postprocess;
    private readonly string _inputName;
    private readonly string[] _outputNames;

    public TableStructurer(string modelPath, OcrOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(modelPath))
            throw new ArgumentException("Model path is required.", nameof(modelPath));

        var sessionOptions = options ?? new OcrOptions();
        var factory = new OnnxSessionFactory(sessionOptions);
        _session = factory.Create(modelPath);

        _inputName = _session.InputMetadata.Keys.First();
        _outputNames = _session.OutputMetadata.Keys.ToArray();
        if (_outputNames.Length < 2)
            throw new InvalidOperationException("Table model must expose bbox and structure outputs.");

        var character = GetCharacterList(_session);
        _postprocess = new TableLabelDecode(character, mergeNoSpanStructure: true, slanetPlus: true);
    }

    public TableStructurer(InferenceSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _inputName = _session.InputMetadata.Keys.First();
        _outputNames = _session.OutputMetadata.Keys.ToArray();
        if (_outputNames.Length < 2)
            throw new InvalidOperationException("Table model must expose bbox and structure outputs.");

        var character = GetCharacterList(_session);
        _postprocess = new TableLabelDecode(character, mergeNoSpanStructure: true, slanetPlus: true);
    }

    public (IReadOnlyList<string> Structure, float Score, float[][] CellBboxes) Run(Mat image)
    {
        var (tensor, shapeList) = _preprocess.Run(image);
        var (bboxPreds, structProbs) = Infer(tensor);
        return _postprocess.Decode(bboxPreds, structProbs, shapeList, image);
    }

    private (float[,] BboxPreds, float[,] StructProbs) Infer(float[,,] chw)
    {
        var c = chw.GetLength(0);
        var h = chw.GetLength(1);
        var w = chw.GetLength(2);
        var dense = new DenseTensor<float>(new[] { 1, c, h, w });
        for (var ci = 0; ci < c; ci++)
        for (var yi = 0; yi < h; yi++)
        for (var xi = 0; xi < w; xi++)
            dense[0, ci, yi, xi] = chw[ci, yi, xi];

        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(_inputName, dense),
        };

        using var results = _session.Run(inputs, _outputNames);
        var outputs = results.ToArray();
        // Output order matches Python OrtInferSession: bbox_preds, structure_probs
        var bboxTensor = outputs[0].AsTensor<float>();
        var structTensor = outputs[1].AsTensor<float>();

        // If model emits structure first, swap by rank heuristic
        if (bboxTensor.Dimensions.Length == 3 && structTensor.Dimensions.Length == 3)
        {
            var bboxLast = bboxTensor.Dimensions[^1];
            var structLast = structTensor.Dimensions[^1];
            if (bboxLast > 16 && structLast <= 16)
            {
                (bboxTensor, structTensor) = (structTensor, bboxTensor);
            }
        }

        return (To2D(bboxTensor), To2D(structTensor));
    }

    private static float[,] To2D(Tensor<float> tensor)
    {
        // Expect [1, seq, dim] or [seq, dim]
        if (tensor.Dimensions.Length == 3)
        {
            var seq = tensor.Dimensions[1];
            var dim = tensor.Dimensions[2];
            var result = new float[seq, dim];
            for (var i = 0; i < seq; i++)
            for (var j = 0; j < dim; j++)
                result[i, j] = tensor[0, i, j];
            return result;
        }

        if (tensor.Dimensions.Length == 2)
        {
            var seq = tensor.Dimensions[0];
            var dim = tensor.Dimensions[1];
            var result = new float[seq, dim];
            for (var i = 0; i < seq; i++)
            for (var j = 0; j < dim; j++)
                result[i, j] = tensor[i, j];
            return result;
        }

        throw new InvalidOperationException($"Unexpected table output rank: {tensor.Dimensions.Length}");
    }

    internal static IReadOnlyList<string> GetCharacterList(InferenceSession session)
    {
        if (!session.ModelMetadata.CustomMetadataMap.TryGetValue("character", out var raw) ||
            string.IsNullOrEmpty(raw))
        {
            throw new InvalidOperationException("Table ONNX metadata key 'character' is missing.");
        }

        // Match Python str.splitlines(): keep empty mid-lines, drop trailing empty from final \n.
        var lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length > 0 && lines[^1].Length == 0)
            return lines.Take(lines.Length - 1).ToArray();
        return lines;
    }

    public void Dispose() => _session.Dispose();
}
