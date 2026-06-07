//-----------------------------------------------------------------------
// <copyright file="BitmapSourceHelper.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OnnxOcr.Desktop.Helpers;

internal static class BitmapSourceHelper
{
    /// <summary>
    /// �?BitmapSource �?DPI 规范�?96，使 WPF 逻辑尺寸�?OCR 像素坐标一致�?    /// </summary>
    public static BitmapSource NormalizeDpi(BitmapSource source)
    {
        if (Math.Abs(source.DpiX - 96) < 0.01 && Math.Abs(source.DpiY - 96) < 0.01)
            return source;

        var stride = (source.PixelWidth * source.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);

        var normalized = BitmapSource.Create(
            source.PixelWidth,
            source.PixelHeight,
            96,
            96,
            source.Format,
            source.Palette,
            pixels,
            stride);

        normalized.Freeze();
        return normalized;
    }

    public static (double Width, double Height) GetPixelSize(ImageSource? source)
    {
        if (source is BitmapSource bitmap)
            return (bitmap.PixelWidth, bitmap.PixelHeight);

        return (0, 0);
    }
}
