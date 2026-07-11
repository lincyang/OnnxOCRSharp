//-----------------------------------------------------------------------
// <copyright file="TextOrientationMode.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Configuration;

public enum TextOrientationMode
{
    /// <summary>Never run the orientation classifier.</summary>
    None,

    /// <summary>Only correct crops that look like vertical text lines.</summary>
    Auto,

    /// <summary>Run the orientation classifier on every crop.</summary>
    Always,
}
