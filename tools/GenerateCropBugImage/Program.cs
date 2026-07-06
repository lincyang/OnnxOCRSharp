using OpenCvSharp;

// 生成供桌面端 / 原版程序验证的测试图：
// 两个独立的竖排文本列，PP-OCR 检测后应得到 2 个高>宽的框，
// 裁剪后 Rows/Cols >= 1.5 会走 Rotate90 分支（旧代码会崩）。

var repoRoot = FindRepoRoot();
var assetDir = Path.Combine(repoRoot, "test_assets");
Directory.CreateDirectory(assetDir);

var outputPath = Path.Combine(assetDir, "crop_bug_vertical_2cols.png");
var skewPath = Path.Combine(assetDir, "crop_bug_vertical_2cols_skew.png");

using (var image = CreateVerticalTwoColumnImage())
    Cv2.ImWrite(outputPath, image);

using (var skew = CreateSkewedVerticalTwoColumnImage())
    Cv2.ImWrite(skewPath, skew);

Console.WriteLine("已生成测试图（请用原版 Desktop 打开识别）:");
Console.WriteLine($"  1) {outputPath}");
Console.WriteLine($"  2) {skewPath}  （推荐先试这张，竖条带倾斜更接近粉丝场景）");
Console.WriteLine();
Console.WriteLine("图片说明:");
Console.WriteLine("  - 800x600 白底");
Console.WriteLine("  - 左列竖排: ONNX");
Console.WriteLine("  - 右列竖排: OCRv6");
Console.WriteLine("  - 两列间距足够大，检测应出 2 个竖长框");
Console.WriteLine("  - 每列裁剪后高/宽 >= 1.5，会触发 ImageCropper 内 90° 旋转");
Console.WriteLine();
Console.WriteLine("旧版 bug 现象: 识别崩溃 / AccessViolation / 第二个框异常");
Console.WriteLine("修复后: 正常识别出 2 行（内容可能不准，重点是流程不崩）");

using var previewImage = Cv2.ImRead(outputPath);

// 用与检测框近似的竖长四边形，本地预估是否会走旋转分支
Console.WriteLine();
Console.WriteLine("本地预估（模拟检测框裁剪，非真实 det 输出）:");
PreviewCropRatio(previewImage, new Point2f[]
{
    new(95, 60), new(175, 60), new(175, 420), new(95, 420),
}, "左列");
PreviewCropRatio(previewImage, new Point2f[]
{
    new(495, 50), new(575, 50), new(575, 430), new(495, 430),
}, "右列");

static Mat CreateSkewedVerticalTwoColumnImage()
{
    const int width = 800;
    const int height = 600;
    var image = new Mat(height, width, MatType.CV_8UC3, new Scalar(255, 255, 255));

    DrawSkewedVerticalColumn(image, "ONNX", new Point2f(100, 65), 42, 400, 14f);
    DrawSkewedVerticalColumn(image, "OCRv6", new Point2f(490, 50), 46, 420, -12f);

    return image;
}

static void DrawSkewedVerticalColumn(
    Mat image,
    string text,
    Point2f origin,
    float columnWidth,
    float columnHeight,
    float skewDeg)
{
    using var column = new Mat((int)columnHeight + 80, (int)columnWidth + 40, MatType.CV_8UC3, new Scalar(255, 255, 255));
    DrawVerticalColumn(column, text, new Point(18, 48), fontScale: 2.0, thickness: 3, lineGap: 58);

    var rad = skewDeg * Math.PI / 180.0;
    var cos = Math.Cos(rad);
    var sin = Math.Sin(rad);
    var center = new Point2f(column.Cols / 2f, column.Rows / 2f);
    var affine = new Mat(2, 3, MatType.CV_64FC1);
    affine.Set(0, 0, cos);
    affine.Set(0, 1, -sin);
    affine.Set(0, 2, center.X * (1 - cos) + center.Y * sin);
    affine.Set(1, 0, sin);
    affine.Set(1, 1, cos);
    affine.Set(1, 2, center.Y * (1 - cos) - center.X * sin);

    using var warped = new Mat();
    Cv2.WarpAffine(column, warped, affine, column.Size(), InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(255));

    var roi = new Rect((int)origin.X, (int)origin.Y, warped.Cols, warped.Rows);
    roi.X = Math.Clamp(roi.X, 0, image.Cols - 1);
    roi.Y = Math.Clamp(roi.Y, 0, image.Rows - 1);
    roi.Width = Math.Min(roi.Width, image.Cols - roi.X);
    roi.Height = Math.Min(roi.Height, image.Rows - roi.Y);

    warped[new Rect(0, 0, roi.Width, roi.Height)].CopyTo(new Mat(image, roi));
}

static void PreviewCropRatio(Mat image, Point2f[] box, string label)
{
    var cropWidth = (int)Math.Max(Distance(box[0], box[1]), Distance(box[2], box[3]));
    var cropHeight = (int)Math.Max(Distance(box[0], box[3]), Distance(box[1], box[2]));

    using var matrix = Cv2.GetPerspectiveTransform(
        InputArray.Create(box),
        InputArray.Create(new[]
        {
            new Point2f(0, 0),
            new Point2f(cropWidth, 0),
            new Point2f(cropWidth, cropHeight),
            new Point2f(0, cropHeight),
        }));

    using var cropped = new Mat();
    Cv2.WarpPerspective(image, cropped, matrix, new Size(cropWidth, cropHeight));
    var ratio = cropped.Rows / (double)Math.Max(cropped.Cols, 1);
    var willRotate = ratio >= 1.5 ? "会旋转" : "不旋转";
    Console.WriteLine($"  {label}: warp 后 {cropped.Cols}x{cropped.Rows}, 高/宽={ratio:F2} -> {willRotate}");
}

static Mat CreateVerticalTwoColumnImage()
{
    const int width = 800;
    const int height = 600;
    var image = new Mat(height, width, MatType.CV_8UC3, new Scalar(255, 255, 255));

    // 左列：竖排英文（OpenCV 默认字体不支持中文，用英文保证笔画可见）
    DrawVerticalColumn(image, "ONNX", new Point(108, 68), fontScale: 2.6, thickness: 4, lineGap: 72);

    // 右列：另一组竖排英文，与左列拉开距离
    DrawVerticalColumn(image, "OCRv6", new Point(508, 58), fontScale: 2.4, thickness: 4, lineGap: 68);

    return image;
}

static void DrawVerticalColumn(
    Mat image,
    string text,
    Point topLeft,
    double fontScale,
    int thickness,
    int lineGap,
    Scalar? color = null)
{
    var ink = color ?? new Scalar(0, 0, 0);
    var font = HersheyFonts.HersheySimplex;

    for (var i = 0; i < text.Length; i++)
    {
        var ch = text[i].ToString();
        var y = topLeft.Y + i * lineGap;
        Cv2.PutText(image, ch, new Point(topLeft.X, y), font, fontScale, ink, thickness, LineTypes.AntiAlias);
    }
}

static float Distance(Point2f a, Point2f b)
    => (float)Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

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
