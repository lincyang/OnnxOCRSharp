//-----------------------------------------------------------------------
// <copyright file="OcrOptions.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Configuration;

public sealed class OcrOptions
{
    public OcrModelPreset? ModelPreset { get; set; }

    public string DetModelPath { get; set; } = "";
    public string RecModelPath { get; set; } = "";
    public string DictPath { get; set; } = "";
    public string OrientationModelPath { get; set; } = "";

    public bool UseGpu { get; set; }
    public int GpuId { get; set; }
    public bool AutoSelectGpu { get; set; } = true;
    public long GpuMemoryLimitBytes { get; set; } = 0;

    public int CpuThreads { get; set; } = 4;

    public string DetAlgorithm { get; set; } = "DB";
    public float DetLimitSideLen { get; set; } = 960f;
    public string DetLimitType { get; set; } = "max";
    public float DetMaxSideLimit { get; set; } = 4000f;
    public string DetBoxType { get; set; } = "quad";
    public float DetDbThresh { get; set; } = 0.3f;
    public float DetDbBoxThresh { get; set; } = 0.6f;
    public float DetDbUnclipRatio { get; set; } = 1.5f;
    public int DetDbMaxCandidates { get; set; } = 1000;
    public bool UseDilation { get; set; }
    public string DetDbScoreMode { get; set; } = "fast";

    public string RecAlgorithm { get; set; } = "SVTR_LCNet";
    public string RecImageShape { get; set; } = "3,48,320";
    public int RecBatchNum { get; set; } = 6;
    public bool UseSpaceChar { get; set; } = true;
    public float DropScore { get; set; } = 0.5f;

    public TextOrientationMode TextOrientationMode { get; set; } = TextOrientationMode.None;

    public bool UseAngleCls
    {
        get => TextOrientationMode != TextOrientationMode.None;
        set => TextOrientationMode = value ? TextOrientationMode.Always : TextOrientationMode.None;
    }

    public static OcrOptions CreateDefault() => ForPreset(OcrModelPreset.PpOcrV6Tiny);

    public static bool TryValidatePreset(OcrModelPreset preset, string? modelsRoot = null)
    {
        try
        {
            ForPreset(preset, modelsRoot).Validate();
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    public static OcrOptions ForPreset(OcrModelPreset preset, string? modelsRoot = null)
    {
        var options = new OcrOptions();
        OcrModelProfiles.Apply(options, preset, modelsRoot);
        return options;
    }

    public static OcrOptions ForPpOcrV5(string? modelsRoot = null)
        => ForPreset(OcrModelPreset.PpOcrV5, modelsRoot);

    public static OcrOptions ForPpOcrV6Tiny(string? modelsRoot = null)
        => ForPreset(OcrModelPreset.PpOcrV6Tiny, modelsRoot);

    public static OcrOptions ForPpOcrV6Small(string? modelsRoot = null)
        => ForPreset(OcrModelPreset.PpOcrV6Small, modelsRoot);

    public static OcrOptions ForPpOcrV6Medium(string? modelsRoot = null)
        => ForPreset(OcrModelPreset.PpOcrV6Medium, modelsRoot);

    public static OcrOptions ForPresetWithGpu(
        OcrModelPreset preset,
        int gpuId = 0,
        string? modelsRoot = null)
    {
        var options = ForPreset(preset, modelsRoot);
        options.UseGpu = true;
        options.GpuId = gpuId;
        options.AutoSelectGpu = false;
        return options;
    }

    public static OcrOptions ForPresetWithAutoDevice(
        OcrModelPreset preset,
        string? modelsRoot = null)
    {
        var options = ForPreset(preset, modelsRoot);

        if (GpuDeviceDetector.IsCudaAvailable())
        {
            var recommended = GpuDeviceDetector.GetRecommendedDevice();
            if (recommended >= 0)
            {
                options.UseGpu = true;
                options.GpuId = recommended;
                options.AutoSelectGpu = true;
            }
        }

        return options;
    }

    public void Validate()
    {
        if (!File.Exists(DetModelPath))
            throw new FileNotFoundException("Detection model not found.");

        if (!File.Exists(RecModelPath))
            throw new FileNotFoundException("Recognition model not found.");

        if (!File.Exists(DictPath))
            throw new FileNotFoundException("Dictionary not found.");
    }
}
