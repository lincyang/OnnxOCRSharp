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

        throw new DirectoryNotFoundException(
            BuildMissingRootMessage(
                "PP-OCRv5",
                """
                models/ppocrv5/
                ├── det/det.onnx
                ├── rec/rec.onnx
                └── ppocrv5_dict.txt
                """));
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

        throw new DirectoryNotFoundException(
            BuildMissingRootMessage(
                "PP-OCRv6",
                """
                models/ppocrv6/
                ├── PP-OCRv6_tiny_det_onnx/inference.onnx
                ├── PP-OCRv6_tiny_rec_onnx/inference.onnx
                └── ppocrv6_tiny_dict.txt
                """));
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
            OcrModelPreset.PpOcrV6Tiny => ResolveV6DictPath(
                FindPpOcrV6ModelsRoot(modelsRoot),
                "ppocrv6_tiny_dict.txt"),
            OcrModelPreset.PpOcrV6Small or OcrModelPreset.PpOcrV6Medium => ResolveV6DictPath(
                FindPpOcrV6ModelsRoot(modelsRoot),
                "ppocrv6_dict.txt"),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported model preset."),
        };
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
            Path.Combine(v6Root, tier, "det", "det.onnx"),
            Path.Combine(v6Root, "det", "inference.onnx"),
            Path.Combine(v6Root, "det", "det.onnx"));
    }

    private static string ResolveV6RecModel(string tier, string? modelsRoot)
    {
        var v6Root = FindPpOcrV6ModelsRoot(modelsRoot);
        return ResolveFirstExisting(
            Path.Combine(v6Root, $"PP-OCRv6_{tier}_rec_onnx", "inference.onnx"),
            Path.Combine(v6Root, tier, "rec", "inference.onnx"),
            Path.Combine(v6Root, tier, "rec", "rec.onnx"),
            Path.Combine(v6Root, "rec", "inference.onnx"),
            Path.Combine(v6Root, "rec", "rec.onnx"));
    }

    private static string ResolveV6DictPath(string v6Root, string dictFileName)
    {
        return ResolveFirstExisting(Path.Combine(v6Root, dictFileName));
    }

    private static string ResolveFirstExisting(params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException(
            "Could not locate model file. Checked paths:" + Environment.NewLine +
            string.Join(Environment.NewLine, candidates.Select(path => $"  - {path}")));
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

    private static string BuildMissingRootMessage(string family, string layoutExample)
        => $"Could not locate {family} models directory.{Environment.NewLine}" +
           $"Expected layout:{Environment.NewLine}{layoutExample}";
}
