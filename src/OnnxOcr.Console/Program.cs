//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;
using System.Text;

if (!TryParseArguments(args, out var preset, out var modelsRoot, out var imagePath))
{
    PrintUsage();
    return 1;
}

imagePath = Path.GetFullPath(imagePath);
if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"Image not found: {imagePath}");
    return 1;
}

using var image = Cv2.ImRead(imagePath);
if (image.Empty())
{
    Console.Error.WriteLine($"Failed to read image: {imagePath}");
    return 1;
}

var options = OcrOptions.ForPreset(preset, modelsRoot);
Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("OnnxOCR C# Console");
Console.WriteLine($"Preset    : {preset}");
Console.WriteLine($"Det model : {options.DetModelPath}");
Console.WriteLine($"Rec model : {options.RecModelPath}");
Console.WriteLine($"Dict      : {options.DictPath}");
Console.WriteLine($"Image     : {imagePath} ({image.Cols}x{image.Rows})");
Console.WriteLine();

using var textSystem = new TextSystem(options);
Console.WriteLine("Running OCR...");
var result = textSystem.Run(image);

Console.WriteLine($"Done in {result.Elapsed.TotalSeconds:F3}s, {result.Lines.Count} line(s)");
Console.WriteLine(new string('-', 60));

for (var i = 0; i < result.Lines.Count; i++)
{
    var line = result.Lines[i];
    Console.WriteLine($"{i + 1,3}. [{line.Score:F4}] {line.Text}");
}

return 0;

static bool TryParseArguments(
    string[] args,
    out OcrModelPreset preset,
    out string? modelsRoot,
    out string imagePath)
{
    preset = OcrModelPreset.PpOcrV5;
    modelsRoot = null;
    imagePath = "";

    var index = 0;
    while (index < args.Length)
    {
        var arg = args[index];
        if (arg is "--preset" or "-p")
        {
            if (++index >= args.Length || !TryParsePreset(args[index], out preset))
                return false;

            index++;
            continue;
        }

        if (arg is "--models" or "-m")
        {
            if (++index >= args.Length)
                return false;

            modelsRoot = Path.GetFullPath(args[index]);
            index++;
            continue;
        }

        if (arg.StartsWith('-'))
            return false;

        imagePath = arg;
        index++;
        break;
    }

    if (string.IsNullOrWhiteSpace(imagePath))
        return false;

    if (index < args.Length)
        return false;

    return true;
}

static bool TryParsePreset(string value, out OcrModelPreset preset)
{
    preset = value.Trim().ToLowerInvariant() switch
    {
        "v5" or "ppocrv5" => OcrModelPreset.PpOcrV5,
        "v6" or "v6-tiny" or "ppocrv6-tiny" or "ppocrv6tiny" => OcrModelPreset.PpOcrV6Tiny,
        "v6-small" or "ppocrv6-small" or "ppocrv6small" => OcrModelPreset.PpOcrV6Small,
        "v6-medium" or "ppocrv6-medium" or "ppocrv6medium" => OcrModelPreset.PpOcrV6Medium,
        _ => default,
    };

    return value.Trim().ToLowerInvariant() is
        "v5" or "ppocrv5" or
        "v6" or "v6-tiny" or "ppocrv6-tiny" or "ppocrv6tiny" or
        "v6-small" or "ppocrv6-small" or "ppocrv6small" or
        "v6-medium" or "ppocrv6-medium" or "ppocrv6medium";
}

static void PrintUsage()
{
    Console.WriteLine("Usage: OnnxOcr.Console [options] <image-path>");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --preset, -p <name>   v5 | v6 | v6-tiny | v6-small | v6-medium");
    Console.WriteLine("  --models, -m <dir>    models root directory (contains ppocrv5/ or ppocrv6/)");
    Console.WriteLine();
    Console.WriteLine("Examples:");
    Console.WriteLine("  dotnet run --project src/OnnxOcr.Console -- test_assets/sample.jpg");
    Console.WriteLine("  dotnet run --project src/OnnxOcr.Console -- --preset v6 test_assets/sample.jpg");
}
