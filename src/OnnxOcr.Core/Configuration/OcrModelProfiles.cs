//-----------------------------------------------------------------------
// <copyright file="OcrModelProfiles.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Configuration;

internal static class OcrModelProfiles
{
    public static void Apply(OcrOptions options, OcrModelPreset preset, string? modelsRoot = null)
    {
        options.ModelPreset = preset;
        options.DetModelPath = ModelPathResolver.ResolveDetModelPath(preset, modelsRoot);
        options.RecModelPath = ModelPathResolver.ResolveRecModelPath(preset, modelsRoot);
        options.DictPath = ModelPathResolver.ResolveRecDictionaryPath(options.RecModelPath);
        options.OrientationModelPath = ModelPathResolver.FindOrientationModelPath(modelsRoot);
        options.UseAngleCls = !string.IsNullOrWhiteSpace(options.OrientationModelPath)
            && File.Exists(options.OrientationModelPath);

        switch (preset)
        {
            case OcrModelPreset.PpOcrV5:
                ApplyPpOcrV5Defaults(options);
                break;
            case OcrModelPreset.PpOcrV6Tiny:
            case OcrModelPreset.PpOcrV6Small:
            case OcrModelPreset.PpOcrV6Medium:
                ApplyPpOcrV6Defaults(options);
                InferenceYamlProfile.Apply(options);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported model preset.");
        }
    }

    private static void ApplyPpOcrV5Defaults(OcrOptions options)
    {
        options.DetDbThresh = 0.3f;
        options.DetDbBoxThresh = 0.6f;
        options.DetDbUnclipRatio = 1.5f;
        options.DetDbMaxCandidates = 1000;
        options.RecImageShape = "3,48,320";
        options.UseSpaceChar = true;
    }

    private static void ApplyPpOcrV6Defaults(OcrOptions options)
    {
        options.DetDbThresh = 0.2f;
        options.DetDbBoxThresh = 0.4f;
        options.DetDbUnclipRatio = 1.4f;
        options.DetDbMaxCandidates = 3000;
        options.RecImageShape = "3,48,320";
        options.UseSpaceChar = true;
    }
}
