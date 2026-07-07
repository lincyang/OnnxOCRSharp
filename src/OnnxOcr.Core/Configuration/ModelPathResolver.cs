//-----------------------------------------------------------------------
// <copyright file="ModelPathResolver.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
namespace OnnxOcr.Core.Configuration;

internal static class ModelPathResolver
{
    public static string FindPpOcrV5ModelsRoot(string? modelsRoot = null)
    {
        foreach (var root in GetSearchRoots(modelsRoot))
        {
            var candidateInProject = Path.Combine(root, "models", "ppocrv5");
            if (Directory.Exists(candidateInProject))
                return candidateInProject;

            var candidateDirect = Path.Combine(root, "ppocrv5");
            if (Directory.Exists(candidateDirect))
                return candidateDirect;

            var candidateInSibling = Path.Combine(root, "OnnxOCR", "onnxocr", "models", "ppocrv5");
            if (Directory.Exists(candidateInSibling))
                return candidateInSibling;
        }

        throw new DirectoryNotFoundException("PP-OCRv5 models not found.");
    }

    public static string FindPpOcrV6ModelsRoot(string? modelsRoot = null)
    {
        foreach (var root in GetSearchRoots(modelsRoot))
        {
            var candidateInProject = Path.Combine(root, "models", "ppocrv6");
            if (Directory.Exists(candidateInProject))
                return candidateInProject;

            var candidateDirect = Path.Combine(root, "ppocrv6");
            if (Directory.Exists(candidateDirect))
                return candidateDirect;
        }

        throw new DirectoryNotFoundException("PP-OCRv6 models not found.");
    }

    public static string ResolveDetModelPath(OcrModelPreset preset, string? modelsRoot = null)
    {
        return preset switch
        {
            OcrModelPreset.PpOcrV5 => ResolveFirstExisting(
                Path.Combine(FindPpOcrV5ModelsRoot(modelsRoot), "det", "det.onnx")),
            OcrModelPreset.PpOcrV6Tiny => ResolveV6DetModel("tiny", modelsRoot),
            OcrModelPreset.PpOcrV6Small => ResolveV6DetModel("small", modelsRoot),
            OcrModelPreset.PpOcrV6Medium => ResolveV6DetModel("medium", modelsRoot),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported model preset."),
        };
    }

    public static string ResolveRecModelPath(OcrModelPreset preset, string? modelsRoot = null)
    {
        return preset switch
        {
            OcrModelPreset.PpOcrV5 => ResolveFirstExisting(
                Path.Combine(FindPpOcrV5ModelsRoot(modelsRoot), "rec", "rec.onnx")),
            OcrModelPreset.PpOcrV6Tiny => ResolveV6RecModel("tiny", modelsRoot),
            OcrModelPreset.PpOcrV6Small => ResolveV6RecModel("small", modelsRoot),
            OcrModelPreset.PpOcrV6Medium => ResolveV6RecModel("medium", modelsRoot),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported model preset."),
        };
    }

    public static string ResolveDictPath(OcrModelPreset preset, string? modelsRoot = null)
    {
        return preset switch
        {
            OcrModelPreset.PpOcrV5 => ResolveFirstExisting(
                Path.Combine(FindPpOcrV5ModelsRoot(modelsRoot), "ppocrv5_dict.txt")),
            OcrModelPreset.PpOcrV6Tiny => ResolveRecDictionaryPath(ResolveV6RecModel("tiny", modelsRoot)),
            OcrModelPreset.PpOcrV6Small => ResolveRecDictionaryPath(ResolveV6RecModel("small", modelsRoot)),
            OcrModelPreset.PpOcrV6Medium => ResolveRecDictionaryPath(ResolveV6RecModel("medium", modelsRoot)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported model preset."),
        };
    }

    public static string ResolveRecDictionaryPath(string recModelPath)
    {
        var dictPath = Path.Combine(Path.GetDirectoryName(recModelPath)!, "inference.yml");
        if (!File.Exists(dictPath))
            throw new FileNotFoundException("Dictionary not found.");

        return dictPath;
    }

    public static string FindOrientationModelPath(string? modelsRoot = null)
    {
        foreach (var root in GetSearchRoots(modelsRoot))
        {
            var candidateInProject = Path.Combine(root, "models", "orientation", "rapid_orientation.onnx");
            if (File.Exists(candidateInProject))
                return candidateInProject;

            var candidateDirect = Path.Combine(root, "orientation", "rapid_orientation.onnx");
            if (File.Exists(candidateDirect))
                return candidateDirect;

            var candidateInSibling = Path.Combine(root, "OnnxOCR", "onnxocr", "models", "orientation", "rapid_orientation.onnx");
            if (File.Exists(candidateInSibling))
                return candidateInSibling;
        }

        return "";
    }

    private static string ResolveV6DetModel(string tier, string? modelsRoot)
    {
        var v6Root = FindPpOcrV6ModelsRoot(modelsRoot);
        return ResolveFirstExisting(
            Path.Combine(v6Root, $"PP-OCRv6_{tier}_det_onnx", "inference.onnx"),
            Path.Combine(v6Root, tier, "det", "inference.onnx"),
            Path.Combine(v6Root, tier, "det", "det.onnx"));
    }

    private static string ResolveV6RecModel(string tier, string? modelsRoot)
    {
        var v6Root = FindPpOcrV6ModelsRoot(modelsRoot);
        return ResolveFirstExisting(
            Path.Combine(v6Root, $"PP-OCRv6_{tier}_rec_onnx", "inference.onnx"),
            Path.Combine(v6Root, tier, "rec", "inference.onnx"),
            Path.Combine(v6Root, tier, "rec", "rec.onnx"));
    }

    private static string ResolveFirstExisting(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("Model file not found.");
    }

    private static IEnumerable<string> GetSearchRoots(string? modelsRoot)
    {
        if (!string.IsNullOrWhiteSpace(modelsRoot))
        {
            yield return Path.GetFullPath(modelsRoot);
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in WalkUp(AppContext.BaseDirectory).Concat(WalkUp(Directory.GetCurrentDirectory())))
        {
            if (seen.Add(path))
                yield return path;
        }
    }

    private static IEnumerable<string> WalkUp(string startPath)
    {
        var current = new DirectoryInfo(startPath);
        while (current != null)
        {
            yield return current.FullName;
            current = current.Parent;
        }
    }

}
