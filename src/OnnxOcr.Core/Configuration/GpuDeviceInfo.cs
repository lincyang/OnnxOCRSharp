//-----------------------------------------------------------------------
// <copyright file="GpuDeviceInfo.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Configuration;

public sealed class GpuDeviceInfo
{
    public int DeviceId { get; init; }
    public string Name { get; init; } = "";
    public long TotalMemoryBytes { get; init; }
    public long FreeMemoryBytes { get; init; }
    public int ComputeCapabilityMajor { get; init; }
    public int ComputeCapabilityMinor { get; init; }
    public bool IsAvailable { get; init; }

    public double TotalMemoryMb => TotalMemoryBytes / (1024.0 * 1024.0);
    public double FreeMemoryMb => FreeMemoryBytes / (1024.0 * 1024.0);

    public string DisplayLabel => string.IsNullOrWhiteSpace(Name)
        ? $"GPU {DeviceId}"
        : $"[{DeviceId}] {Name} ({TotalMemoryMb:F0}MB)";
}
