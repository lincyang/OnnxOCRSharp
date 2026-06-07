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

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var imagePath = Path.GetFullPath(args[0]);
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

var options = OcrOptions.CreateDefault();
Console.WriteLine("OnnxOCR C# Console");
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

static void PrintUsage()
{
    Console.WriteLine("Usage: OnnxOcr.Console <image-path>");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  dotnet run --project src/OnnxOcr.Console -- path/to/image.jpg");
}
