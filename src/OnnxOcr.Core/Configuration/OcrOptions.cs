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

    public static OcrOptions CreateDefault() => ForPreset(OcrModelPreset.PpOcrV5);

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
            throw new FileNotFoundException(BuildMissingFileMessage("Detection model", DetModelPath));

        if (!File.Exists(RecModelPath))
            throw new FileNotFoundException(BuildMissingFileMessage("Recognition model", RecModelPath));

        if (!File.Exists(DictPath))
            throw new FileNotFoundException(BuildMissingFileMessage("Dictionary", DictPath));
    }

    private string BuildMissingFileMessage(string kind, string path)
    {
        var presetHint = ModelPreset switch
        {
            OcrModelPreset.PpOcrV6Tiny =>
                "Download PP-OCRv6 tiny det/rec from ModelScope and place ppocrv6_tiny_dict.txt under models/ppocrv6/.",
            OcrModelPreset.PpOcrV6Small or OcrModelPreset.PpOcrV6Medium =>
                "Download PP-OCRv6 small/medium det/rec from ModelScope and place ppocrv6_dict.txt under models/ppocrv6/.",
            OcrModelPreset.PpOcrV5 =>
                "Place PP-OCRv5 models under models/ppocrv5/ or call OcrOptions.ForPpOcrV5(modelsRoot).",
            _ => "Configure model paths manually or use OcrOptions.ForPreset(...).",
        };

        return $"{kind} not found: {path}{Environment.NewLine}{presetHint}";
    }
}
