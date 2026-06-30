//-----------------------------------------------------------------------
// <copyright file="OcrOptions.cs" company="����ԱLinc">
// Copyright (c) ����ԱLinc. All rights reserved.
// </copyright>
// <author>����ԱLinc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>���ںţ�����ԱLinc</wechat>
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
    public int CpuThreads { get; set; } = 4;

    public string DetAlgorithm { get; set; } = "DB";
    public float DetLimitSideLen { get; set; } = 960f;
    public string DetLimitType { get; set; } = "max";
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

    public bool UseAngleCls { get; set; }

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
