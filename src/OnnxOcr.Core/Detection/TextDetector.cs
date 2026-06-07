//-----------------------------------------------------------------------
// <copyright file="TextDetector.cs" company="程序员Linc">
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

namespace OnnxOcr.Core.Detection;

public sealed class TextDetector : IDisposable
{
    private readonly OcrOptions _options;
    private readonly DbPostProcess _postProcess;
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string[] _outputNames;

    public TextDetector(OcrOptions options, OnnxSessionFactory sessionFactory)
    {
        _options = options;
        _postProcess = new DbPostProcess(
            thresh: options.DetDbThresh,
            boxThresh: options.DetDbBoxThresh,
            maxCandidates: 1000,
            unclipRatio: options.DetDbUnclipRatio,
            useDilation: options.UseDilation,
            scoreMode: options.DetDbScoreMode,
            boxType: options.DetBoxType);

        _session = sessionFactory.Create(options.DetModelPath);
        _inputName = _session.InputMetadata.Keys.First();
        _outputNames = _session.OutputMetadata.Keys.ToArray();
        WarmUp();
    }

    public IReadOnlyList<Point2f[]> Detect(Mat image)
    {
        var (inputTensor, shape) = DetPreprocessor.Prepare(image, _options.DetLimitSideLen, _options.DetLimitType);
        var channels = inputTensor.GetLength(0);
        var height = inputTensor.GetLength(1);
        var width = inputTensor.GetLength(2);

        var tensor = new DenseTensor<float>(new[] { 1, channels, height, width });
        for (var c = 0; c < channels; c++)
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            tensor[0, c, y, x] = inputTensor[c, y, x];

        using var results = _session.Run(
            new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) },
            _outputNames);

        var output = results.First().AsTensor<float>();
        var maps = ToMaps(output);
        var rawBoxes = _postProcess.Process(maps, new[] { shape });
        return FilterBoxes(rawBoxes, image.Rows, image.Cols);
    }

    private IReadOnlyList<Point2f[]> FilterBoxes(IReadOnlyList<Point2f[]> boxes, int imageHeight, int imageWidth)
    {
        var filtered = new List<Point2f[]>();

        foreach (var box in boxes)
        {
            var ordered = OrderPointsClockwise(box);
            var clipped = ClipBox(ordered, imageHeight, imageWidth);

            var width = Distance(clipped[0], clipped[1]);
            var height = Distance(clipped[0], clipped[3]);
            if (width <= 3 || height <= 3)
                continue;

            filtered.Add(clipped);
        }

        return filtered;
    }

    private static Point2f[] OrderPointsClockwise(Point2f[] points)
    {
        var ordered = new Point2f[4];
        var sums = points.Select(point => point.X + point.Y).ToArray();
        var minIndex = Array.IndexOf(sums, sums.Min());
        var maxIndex = Array.IndexOf(sums, sums.Max());
        ordered[0] = points[minIndex];
        ordered[2] = points[maxIndex];

        var remaining = points.Where((_, index) => index != minIndex && index != maxIndex).ToArray();
        var diffs = remaining.Select(point => point.Y - point.X).ToArray();
        ordered[1] = remaining[Array.IndexOf(diffs, diffs.Min())];
        ordered[3] = remaining[Array.IndexOf(diffs, diffs.Max())];
        return ordered;
    }

    private static Point2f[] ClipBox(Point2f[] points, int imageHeight, int imageWidth)
    {
        return points.Select(point => new Point2f(
            Math.Clamp((int)point.X, 0, imageWidth - 1),
            Math.Clamp((int)point.Y, 0, imageHeight - 1))).ToArray();
    }

    private static float[,,] ToMaps(Tensor<float> output)
    {
        var dims = output.Dimensions.ToArray();
        if (dims.Length != 4)
            throw new InvalidOperationException($"Unexpected detection output shape: [{string.Join(", ", dims)}]");

        var maps = new float[dims[0], dims[2], dims[3]];
        for (var b = 0; b < dims[0]; b++)
        for (var y = 0; y < dims[2]; y++)
        for (var x = 0; x < dims[3]; x++)
            maps[b, y, x] = output[b, 0, y, x];

        return maps;
    }

    private void WarmUp()
    {
        var side = (int)_options.DetLimitSideLen;
        var tensor = new DenseTensor<float>(new[] { 1, 3, side, side });
        using var _ = _session.Run(
            new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, tensor) },
            _outputNames);
    }

    private static float Distance(Point2f a, Point2f b)
        => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    public void Dispose() => _session.Dispose();
}
