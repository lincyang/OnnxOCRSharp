//-----------------------------------------------------------------------
// <copyright file="InferenceYamlProfile.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Globalization;

namespace OnnxOcr.Core.Configuration;

internal static class InferenceYamlProfile
{
    private const float DefaultDetLimitSideLen = 736f;
    private const string DefaultDetLimitType = "min";
    private const float DefaultDetMaxSideLimit = 4000f;

    public static void Apply(OcrOptions options)
    {
        ApplyDetectionProfile(options);
        ApplyRecognitionProfile(options);
    }

    private static void ApplyDetectionProfile(OcrOptions options)
    {
        var ymlPath = Path.Combine(Path.GetDirectoryName(options.DetModelPath)!, "inference.yml");
        if (!File.Exists(ymlPath))
            return;

        ApplyDetResizeForTest(options, ymlPath);

        var values = ReadScalarMap(ymlPath, "PostProcess:");
        if (values.TryGetValue("thresh", out var thresh))
            options.DetDbThresh = thresh;
        if (values.TryGetValue("box_thresh", out var boxThresh))
            options.DetDbBoxThresh = boxThresh;
        if (values.TryGetValue("unclip_ratio", out var unclipRatio))
            options.DetDbUnclipRatio = unclipRatio;
        if (values.TryGetValue("max_candidates", out var maxCandidates))
            options.DetDbMaxCandidates = (int)maxCandidates;
    }

    private static void ApplyDetResizeForTest(OcrOptions options, string ymlPath)
    {
        var resize = ReadDetResizeForTest(ymlPath);
        if (resize == null)
            return;

        if (resize.LimitSideLen.HasValue)
            options.DetLimitSideLen = resize.LimitSideLen.Value;
        if (!string.IsNullOrWhiteSpace(resize.LimitType))
            options.DetLimitType = resize.LimitType;
        if (resize.MaxSideLimit.HasValue)
            options.DetMaxSideLimit = resize.MaxSideLimit.Value;
    }

    private static DetResizeForTestConfig? ReadDetResizeForTest(string ymlPath)
    {
        var lines = File.ReadAllLines(ymlPath);
        var inPreProcess = false;
        var inDetResize = false;
        float? limitSideLen = null;
        string? limitType = null;
        float? resizeLong = null;
        float? maxSideLimit = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed == "PreProcess:")
            {
                inPreProcess = true;
                inDetResize = false;
                continue;
            }

            if (!inPreProcess)
                continue;

            if (trimmed.StartsWith("- DetResizeForTest:", StringComparison.Ordinal))
            {
                inDetResize = true;
                var value = trimmed["- DetResizeForTest:".Length..].Trim();
                if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    return new DetResizeForTestConfig(
                        DefaultDetLimitSideLen,
                        DefaultDetLimitType,
                        null,
                        DefaultDetMaxSideLimit);
                }

                continue;
            }

            if (inDetResize)
            {
                if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                    break;

                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex <= 0)
                    continue;

                var key = trimmed[..colonIndex].Trim();
                var valueText = trimmed[(colonIndex + 1)..].Trim();
                switch (key)
                {
                    case "limit_side_len":
                        if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var sideLen))
                            limitSideLen = sideLen;
                        break;
                    case "limit_type":
                        limitType = valueText.Trim('"', '\'');
                        break;
                    case "resize_long":
                        if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var longSide))
                            resizeLong = longSide;
                        break;
                    case "max_side_limit":
                        if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var sideLimit))
                            maxSideLimit = sideLimit;
                        break;
                }

                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                inDetResize = false;
        }

        if (limitSideLen.HasValue || !string.IsNullOrWhiteSpace(limitType) || resizeLong.HasValue || maxSideLimit.HasValue)
        {
            if (resizeLong.HasValue)
            {
                return new DetResizeForTestConfig(
                    resizeLong.Value,
                    "resize_long",
                    resizeLong,
                    maxSideLimit ?? DefaultDetMaxSideLimit);
            }

            return new DetResizeForTestConfig(
                limitSideLen ?? DefaultDetLimitSideLen,
                limitType ?? DefaultDetLimitType,
                resizeLong,
                maxSideLimit ?? DefaultDetMaxSideLimit);
        }

        return null;
    }

    private static void ApplyRecognitionProfile(OcrOptions options)
    {
        var ymlPath = options.DictPath;
        if (!File.Exists(ymlPath))
            return;

        var shape = ReadRecImageShape(ymlPath);
        if (shape != null)
            options.RecImageShape = shape;
    }

    private static string? ReadRecImageShape(string ymlPath)
    {
        var lines = File.ReadAllLines(ymlPath);
        var inPreProcess = false;
        var inRecResize = false;
        var shapeValues = new List<int>();

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.Trim();

            if (trimmed == "PreProcess:")
            {
                inPreProcess = true;
                inRecResize = false;
                continue;
            }

            if (!inPreProcess)
                continue;

            if (trimmed.StartsWith("- RecResizeImg:", StringComparison.Ordinal))
            {
                inRecResize = true;
                shapeValues.Clear();
                continue;
            }

            if (inRecResize && trimmed == "image_shape:")
                continue;

            if (inRecResize && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (int.TryParse(trimmed[2..].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                    shapeValues.Add(value);
                continue;
            }

            if (inRecResize && shapeValues.Count > 0)
                break;

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) && !trimmed.StartsWith("- RecResizeImg", StringComparison.Ordinal))
                inRecResize = false;
        }

        return shapeValues.Count == 3
            ? string.Join(",", shapeValues)
            : null;
    }

    private static Dictionary<string, float> ReadScalarMap(string ymlPath, string sectionName)
    {
        var values = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var lines = File.ReadAllLines(ymlPath);
        var inSection = false;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();

            if (trimmed == sectionName)
            {
                inSection = true;
                continue;
            }

            if (!inSection)
                continue;

            if (trimmed.Length == 0 || trimmed.StartsWith('-'))
                continue;

            var colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
            {
                if (!char.IsWhiteSpace(rawLine.FirstOrDefault()))
                    break;
                continue;
            }

            var key = trimmed[..colonIndex].Trim();
            var valueText = trimmed[(colonIndex + 1)..].Trim();
            if (float.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                values[key] = value;
        }

        return values;
    }

    private sealed record DetResizeForTestConfig(
        float? LimitSideLen,
        string? LimitType,
        float? ResizeLong,
        float? MaxSideLimit);
}
