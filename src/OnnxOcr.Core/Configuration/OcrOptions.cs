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
    public bool UseDilation { get; set; }
    public string DetDbScoreMode { get; set; } = "fast";

    public string RecAlgorithm { get; set; } = "SVTR_LCNet";
    public string RecImageShape { get; set; } = "3,48,320";
    public int RecBatchNum { get; set; } = 6;
    public bool UseSpaceChar { get; set; } = true;
    public float DropScore { get; set; } = 0.5f;

    public bool UseAngleCls { get; set; }

    public static OcrOptions CreateDefault()
    {
        var modelsRoot = PathResolver.FindPpOcrV5ModelsRoot();
        return new OcrOptions
        {
            DetModelPath = Path.Combine(modelsRoot, "det", "det.onnx"),
            RecModelPath = Path.Combine(modelsRoot, "rec", "rec.onnx"),
            DictPath = Path.Combine(modelsRoot, "ppocrv5_dict.txt"),
            OrientationModelPath = PathResolver.FindOrientationModelPath(),
        };
    }

    public void Validate()
    {
        if (!File.Exists(DetModelPath))
            throw new FileNotFoundException($"Detection model not found: {DetModelPath}");
        if (!File.Exists(RecModelPath))
            throw new FileNotFoundException($"Recognition model not found: {RecModelPath}");
        if (!File.Exists(DictPath))
            throw new FileNotFoundException($"Dictionary not found: {DictPath}");
    }
}

internal static class PathResolver
{
    public static string FindPpOcrV5ModelsRoot()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            // 优先�?OnnxOCRSharp 目录下查�?            var candidateInProject = Path.Combine(root, "models", "ppocrv5");
            if (Directory.Exists(candidateInProject))
                return candidateInProject;
            
            // 然后在同级目录的 OnnxOCR 中查找（保持兼容性）
            var candidateInSibling = Path.Combine(root, "OnnxOCR", "onnxocr", "models", "ppocrv5");
            if (Directory.Exists(candidateInSibling))
                return candidateInSibling;
        }

        throw new DirectoryNotFoundException(
            "Could not locate models/ppocrv5 or OnnxOCR/onnxocr/models/ppocrv5. " +
            "Place models in OnnxOCRSharp/models/ directory or set model paths manually.");
    }

    public static string FindOrientationModelPath()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            // 优先�?OnnxOCRSharp 目录下查�?            var candidateInProject = Path.Combine(root, "models", "orientation", "rapid_orientation.onnx");
            if (File.Exists(candidateInProject))
                return candidateInProject;
            
            // 然后在同级目录的 OnnxOCR 中查找（保持兼容性）
            var candidateInSibling = Path.Combine(root, "OnnxOCR", "onnxocr", "models", "orientation", "rapid_orientation.onnx");
            if (File.Exists(candidateInSibling))
                return candidateInSibling;
        }

        return "";
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
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
