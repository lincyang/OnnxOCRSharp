//-----------------------------------------------------------------------
// <copyright file="OnnxSessionFactory.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using Microsoft.ML.OnnxRuntime;
using OnnxOcr.Core.Configuration;

namespace OnnxOcr.Core.Inference;

public sealed class OnnxSessionFactory
{
    private readonly OcrOptions _options;

    public OnnxSessionFactory(OcrOptions options)
    {
        _options = options;
    }

    public InferenceSession Create(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"Model file not found: {modelPath}. " +
                "Ensure OnnxOCR models are available or update OcrOptions.");
        }

        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        if (_options.CpuThreads > 0)
            sessionOptions.IntraOpNumThreads = _options.CpuThreads;

        if (_options.UseGpu)
        {
            try
            {
                sessionOptions.AppendExecutionProvider_CUDA(_options.GpuId);
            }
            catch
            {
                // Fall back to CPU when CUDA is unavailable.
            }
        }

        return new InferenceSession(modelPath, sessionOptions);
    }
}
