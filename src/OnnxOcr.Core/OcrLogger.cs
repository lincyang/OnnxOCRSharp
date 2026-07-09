//-----------------------------------------------------------------------
// <copyright file="OcrLogger.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Diagnostics;

namespace OnnxOcr.Core;

public static class OcrLogger
{
    public static event Action<string>? OnLog;

    public static void Log(string message)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Debug.WriteLine(timestamped);
        OnLog?.Invoke(timestamped);
    }
}
