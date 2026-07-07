using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Detection;
using OnnxOcr.Core.Imaging;
using OnnxOcr.Core.Inference;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;

var repoRoot = FindRepoRoot();
var imagePath = args.Length > 0
    ? args[0]
    : Path.Combine(repoRoot, "test_assets", "crop_bug_rotate_2boxes.png");

if (!File.Exists(imagePath))
{
    Console.WriteLine($"图片不存在: {imagePath}");
    return 1;
}

Console.WriteLine($"图片: {imagePath}");
using var image = Cv2.ImRead(imagePath);
Console.WriteLine($"尺寸: {image.Cols}x{image.Rows}");
Console.WriteLine();

var options = OcrOptions.ForPpOcrV6Tiny(Path.Combine(repoRoot, "models"));
Console.WriteLine($"方向分类: {(options.UseAngleCls ? "已启用" : "未启用（需 models/orientation/rapid_orientation.onnx）")}");
Console.WriteLine();
using var detector = new TextDetector(options, new OnnxSessionFactory(options));
var boxes = detector.Detect(image);

Console.WriteLine($"检测框数量: {boxes.Count}");
if (boxes.Count == 0)
{
    Console.WriteLine("未检出文本框，请换图或调低 det_db_thresh。");
    return 2;
}

for (var i = 0; i < boxes.Count; i++)
{
    var box = boxes[i];
    var crop = ImageCropper.Crop(image, box, options.DetBoxType);
    var ratio = crop.Rows / (double)Math.Max(crop.Cols, 1);
    var rotates = ratio >= 1.5 ? "是(旧版会 Dispose 已释放 Mat)" : "否";
    Console.WriteLine($"  框 {i + 1}: crop {crop.Cols}x{crop.Rows}, Rows/Cols={ratio:F2}, 触发旋转={rotates}");

    try
    {
        using var resized = new Mat();
        Cv2.Resize(crop, resized, new Size(crop.Cols, crop.Rows));
        _ = resized.At<Vec3b>(0, 0);
        Console.WriteLine($"         像素读取: OK");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"         像素读取: 失败 -> {ex.GetType().Name}: {ex.Message}");
    }
    finally
    {
        crop.Dispose();
    }
}

Console.WriteLine();
if (boxes.Count >= 2 && boxes.Take(2).All((_, idx) => true))
{
    Console.WriteLine("若上面任一框「像素读取: 失败」，说明旧版 ImageCropper 已复现 bug。");
}

try
{
    using var system = new TextSystem(options);
    var result = system.Run(image);
    Console.WriteLine($"TextSystem 完整识别: {result.Lines.Count} 行, 耗时 {result.Elapsed.TotalMilliseconds:F0}ms");
    foreach (var line in result.Lines)
        Console.WriteLine($"  [{line.Score:F4}] {line.Text}");
}
catch (Exception ex)
{
    Console.WriteLine($"TextSystem 崩溃: {ex}");
    return 1;
}

return 0;

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        if (File.Exists(Path.Combine(dir.FullName, "OnnxOcr.sln")))
            return dir.FullName;
        dir = dir.Parent;
    }

    return Directory.GetCurrentDirectory();
}
