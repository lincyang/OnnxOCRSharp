//-----------------------------------------------------------------------
// <copyright file="TextRecognizer.cs" company="程序员Linc">
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

namespace OnnxOcr.Core.Recognition;

public sealed class TextRecognizer : IDisposable
{
    private readonly OcrOptions _options;
    private readonly CtcLabelDecode _decoder;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string[] _outputNames;
    private readonly int[] _recImageShape;

    public TextRecognizer(OcrOptions options, OnnxSessionFactory sessionFactory)
    {
        _options = options;
        _decoder = new CtcLabelDecode(options.DictPath, options.UseSpaceChar);
        _recImageShape = options.RecImageShape
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();

        _session = sessionFactory.Create(options.RecModelPath);
        _inputName = _session.InputMetadata.Keys.First();
        _outputNames = _session.OutputMetadata.Keys.ToArray();

        WarmUp();
    }

    public IReadOnlyList<(string Text, float Score)> Recognize(IReadOnlyList<Mat> images)
    {
        if (images.Count == 0)
            return Array.Empty<(string, float)>();

        var widthRatios = images.Select(img => img.Width / (float)img.Height).ToArray();
        var sortedIndices = Enumerable.Range(0, images.Count)
            .OrderBy(index => widthRatios[index])
            .ToArray();

        var results = new (string Text, float Score)[images.Count];

        for (var batchStart = 0; batchStart < images.Count; batchStart += _options.RecBatchNum)
        {
            var batchEnd = Math.Min(images.Count, batchStart + _options.RecBatchNum);
            var imgC = _recImageShape[0];
            var imgH = _recImageShape[1];
            var baseImgW = _recImageShape[2];
            var maxWhRatio = baseImgW / (float)imgH;

            for (var i = batchStart; i < batchEnd; i++)
            {
                var image = images[sortedIndices[i]];
                var ratio = image.Width / (float)image.Height;
                maxWhRatio = Math.Max(maxWhRatio, ratio);
            }

            var batchWidth = Math.Max(baseImgW, (int)Math.Ceiling(imgH * maxWhRatio));
            var batchSize = batchEnd - batchStart;
            var batchTensor = new DenseTensor<float>(new[] { batchSize, imgC, imgH, batchWidth });

            for (var i = 0; i < batchSize; i++)
            {
                var normalized = ResizeNormImage(images[sortedIndices[batchStart + i]], maxWhRatio, batchWidth);
                CopyToBatch(batchTensor, i, normalized);
            }

            using var resultsOnnx = _session.Run(
                new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, batchTensor) },
                _outputNames);

            var output = resultsOnnx.First().AsEnumerable<float>().ToArray();
            var outputDims = resultsOnnx.First().AsTensor<float>().Dimensions.ToArray();
            var preds = ToPredArray(output, outputDims);
            var decoded = _decoder.Decode(preds);

            for (var i = 0; i < decoded.Count; i++)
                results[sortedIndices[batchStart + i]] = decoded[i];
        }

        return results;
    }

    private static float[,,] ToPredArray(float[] flat, int[] dims)
    {
        if (dims.Length != 3)
            throw new InvalidOperationException($"Unexpected recognition output shape: [{string.Join(", ", dims)}]");

        var preds = new float[dims[0], dims[1], dims[2]];
        var index = 0;
        for (var b = 0; b < dims[0]; b++)
        for (var t = 0; t < dims[1]; t++)
        for (var c = 0; c < dims[2]; c++)
            preds[b, t, c] = flat[index++];

        return preds;
    }

    private static void CopyToBatch(DenseTensor<float> batchTensor, int batchIndex, float[,,] image)
    {
        var channels = image.GetLength(0);
        var height = image.GetLength(1);
        var width = image.GetLength(2);

        for (var c = 0; c < channels; c++)
        for (var h = 0; h < height; h++)
        for (var w = 0; w < width; w++)
            batchTensor[batchIndex, c, h, w] = image[c, h, w];
    }

    private float[,,] ResizeNormImage(Mat image, float maxWhRatio, int targetWidth)
    {
        var imgC = _recImageShape[0];
        var imgH = _recImageShape[1];

        var ratio = image.Width / (float)image.Height;
        var resizedW = (int)Math.Ceiling(imgH * ratio);
        if (resizedW > targetWidth)
            resizedW = targetWidth;

        using var resized = new Mat();
        Cv2.Resize(image, resized, new Size(resizedW, imgH));

        var normalized = new float[imgC, imgH, targetWidth];
        for (var y = 0; y < imgH; y++)
        {
            for (var x = 0; x < resizedW; x++)
            {
                var pixel = resized.At<Vec3b>(y, x);
                normalized[0, y, x] = (pixel.Item0 / 255f - 0.5f) / 0.5f;
                normalized[1, y, x] = (pixel.Item1 / 255f - 0.5f) / 0.5f;
                normalized[2, y, x] = (pixel.Item2 / 255f - 0.5f) / 0.5f;
            }
        }

        return normalized;
    }

    private void WarmUp()
    {
        var imgC = _recImageShape[0];
        var imgH = _recImageShape[1];
        var imgW = _recImageShape[2];
        var tensor = new DenseTensor<float>(new[] { 1, imgC, imgH, imgW });
        using var _ = _session.Run(
            new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) },
            _outputNames);
    }

    public void Dispose() => _session.Dispose();
}
