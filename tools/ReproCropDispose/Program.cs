using OnnxOcr.Core.Imaging;
using OpenCvSharp;

// 复现粉丝反馈：2 个检测框 + 竖条裁剪触发 90° 旋转时，旧代码 `using var rotated` + `return rotated`
// 会在 return 时先 Dispose rotated，TextSystem 识别或 finally 再次 Dispose 时报错。

var repoRoot = FindRepoRoot();
var assetDir = Path.Combine(repoRoot, "test_assets");
Directory.CreateDirectory(assetDir);

var imagePath = Path.Combine(assetDir, "crop_bug_2boxes.png");
using var image = CreateSampleImage();
Cv2.ImWrite(imagePath, image);
Console.WriteLine($"已生成测试图: {imagePath}");
Console.WriteLine();

var boxes = CreateTwoRotatedBoxes();
Console.WriteLine("模拟 2 个倾斜/竖长检测框，裁剪后高宽比 >= 1.5 会触发 Rotate90...");
Console.WriteLine();

var crops = new List<Mat>();
try
{
    for (var i = 0; i < boxes.Count; i++)
    {
        var box = boxes[i];
        LogBoxMetrics(i, box);

        var crop = ImageCropper.Crop(image, box, "quad");
        crops.Add(crop);
        Console.WriteLine($"  框 {i + 1}: crop {crop.Cols}x{crop.Rows}, ratio={crop.Rows / (double)Math.Max(crop.Cols, 1):F2}");
    }

    Console.WriteLine();
    Console.WriteLine("模拟 TextRecognizer 读取像素 + TextSystem finally Dispose ...");

    for (var i = 0; i < crops.Count; i++)
    {
        var crop = crops[i];
        SimulateRecognizerRead(crop, i + 1);
    }

    Console.WriteLine();
    Console.WriteLine("全部通过 — 若看到此消息说明 ImageCropper 旋转分支未提前释放 Mat。");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("失败（旧版 ImageCropper 在此抛异常）:");
    Console.WriteLine(ex);
    return 1;
}
finally
{
    foreach (var crop in crops)
        crop.Dispose();
}

static void SimulateRecognizerRead(Mat crop, int index)
{
    var imgH = crop.Rows;
    var imgW = crop.Cols;
    using var resized = new Mat();
    Cv2.Resize(crop, resized, new Size(imgW, imgH));

    var sum = 0.0;
    for (var y = 0; y < imgH; y++)
    {
        for (var x = 0; x < imgW; x++)
        {
            var pixel = resized.At<Vec3b>(y, x);
            sum += pixel.Item0 + pixel.Item1 + pixel.Item2;
        }
    }

    Console.WriteLine($"  框 {index}: {imgW}x{imgH}, 像素读取 OK, sum={sum:F0}");
}

static void LogBoxMetrics(int index, Point2f[] points)
{
    static float D(Point2f a, Point2f b)
        => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    var cropWidth = (int)Math.Max(D(points[0], points[1]), D(points[2], points[3]));
    var cropHeight = (int)Math.Max(D(points[0], points[3]), D(points[1], points[2]));
    Console.WriteLine($"  框 {index + 1} 输入: cropWidth={cropWidth}, cropHeight={cropHeight}, 旋转条件={cropHeight / (double)Math.Max(cropWidth, 1):F2} (warp后 Rows/Cols)");
}

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

static Mat CreateSampleImage()
{
    var image = new Mat(480, 640, MatType.CV_8UC3, Scalar.All(255));

    // 两个竖长黑条，模拟竖排/窄高文本区域
    Cv2.Rectangle(image, new Rect(120, 80, 36, 200), Scalar.All(0), -1);
    Cv2.Rectangle(image, new Rect(420, 100, 40, 220), Scalar.All(0), -1);

    Cv2.PutText(image, "A", new Point(128, 180), HersheyFonts.HersheySimplex, 1.2, Scalar.All(255), 2);
    Cv2.PutText(image, "B", new Point(428, 210), HersheyFonts.HersheySimplex, 1.2, Scalar.All(255), 2);

    return image;
}

static List<Point2f[]> CreateTwoRotatedBoxes()
{
    return
    [
        CreateRotatedQuad(new Point2f(120, 80), 36, 200, 15f),
        CreateRotatedQuad(new Point2f(420, 100), 40, 220, -12f),
    ];
}

static Point2f[] CreateRotatedQuad(Point2f origin, float width, float height, float angleDeg)
{
    var rad = angleDeg * Math.PI / 180.0;
    var cos = (float)Math.Cos(rad);
    var sin = (float)Math.Sin(rad);

    Point2f Transform(float x, float y)
        => new(
            origin.X + x * cos - y * sin,
            origin.Y + x * sin + y * cos);

    return
    [
        Transform(0, 0),
        Transform(width, 0),
        Transform(width, height),
        Transform(0, height),
    ];
}
