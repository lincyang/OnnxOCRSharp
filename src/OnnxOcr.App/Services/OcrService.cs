//-----------------------------------------------------------------------
// <copyright file="OcrService.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.App.Models;
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;

namespace OnnxOcr.App.Services;

public sealed class OcrService : IDisposable
{
    private readonly TextSystem _textSystem;

    public OcrService(OcrOptions? options = null)
    {
        _textSystem = new TextSystem(options ?? OcrOptions.CreateDefault());
    }

    public Task<OcrRunResult> RecognizeAsync(string imagePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            throw new ArgumentException("Image path is required.", nameof(imagePath));

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var image = Cv2.ImRead(imagePath);
            if (image.Empty())
                throw new InvalidOperationException($"无法读取图片: {imagePath}");

            cancellationToken.ThrowIfCancellationRequested();
            var result = _textSystem.Run(image);
            return OcrRunResult.From(result, imagePath);
        }, cancellationToken);
    }

    public void Dispose() => _textSystem.Dispose();
}
