# OnnxOCRSharp

基于 ONNX Runtime + OpenCvSharp 的纯 .NET OCR 库，支持 PP-OCRv6 tiny / small / medium 模型。

## 功能特性

- **文字检测**：基于 DB（Differentiable Binarization）算法的文本检测
- **文字识别**：支持 SVTR_LCNet 等识别算法
- **多模型支持**：PP-OCRv6 tiny / small / medium
- **自动路径解析**：智能查找模型文件，支持 ModelScope 原样解压目录
- **模型下载**：内置魔塔（ModelScope）模型下载服务，一键获取模型
- **简单易用**：几行代码即可完成 OCR 识别
- **批量识别**：`RecognizeManyAsync` 串行处理多图，支持进度回调

## 安装

### NuGet 包管理器

```powershell
Install-Package OnnxOCRSharp
```

### .NET CLI

```bash
dotnet add package OnnxOCRSharp
```

## 快速开始

安装后，只需几行代码即可完成 OCR 识别：

```csharp
using OnnxOcr.App.Services;
using OnnxOcr.App.Models;
using OnnxOcr.Core.Configuration;

// 创建识别服务（自动使用 PP-OCRv6 tiny）
using var service = new OcrService(OcrModelPreset.PpOcrV6Tiny);

// 识别图片
OcrRunResult result = await service.RecognizeAsync("test.jpg");

// 遍历结果
foreach (var line in result.Lines)
{
    Console.WriteLine($"[{line.Index}] {line.Text} (置信度: {line.Score:P0})");
}

Console.WriteLine($"图片尺寸: {result.ImageWidth}x{result.ImageHeight}");
Console.WriteLine($"识别耗时: {result.Elapsed.TotalMilliseconds:F0}ms");
```

### 批量识别多张图片

```csharp
var paths = new[] { "a.jpg", "b.png", "c.webp" };
var progress = new Progress<OcrBatchProgress>(p =>
    Console.WriteLine($"[{p.CurrentIndex}/{p.Total}] {Path.GetFileName(p.CurrentPath)}"));

IReadOnlyList<OcrBatchItemResult> batch =
    await service.RecognizeManyAsync(paths, progress);

foreach (var item in batch)
{
    if (!item.Success)
    {
        Console.WriteLine($"失败: {item.ImagePath} -> {item.ErrorMessage}");
        continue;
    }

    Console.WriteLine($"===== {Path.GetFileName(item.ImagePath)} =====");
    foreach (var line in item.Result!.Lines)
        Console.WriteLine(line.Text);
}
```

> **提示**：`OcrModelPreset` 支持 `PpOcrV6Tiny`（推荐，轻量快速）、`PpOcrV6Small`、`PpOcrV6Medium`（高精度）。

### 指定模型根目录

默认自动从 `models/` 目录查找模型。如需自定义位置：

```csharp
// 自定义模型存放位置
var options = OcrOptions.ForPpOcrV6Tiny(@"D:\myapp\models");
using var service = new OcrService(options);
var result = await service.RecognizeAsync("test.jpg");
```

## 模型路径

库会自动从以下位置查找模型（按优先级）：

1. 调用方指定的 `modelsRoot` 目录
2. 程序运行目录向上遍历的 `models/` 目录
3. 当前工作目录向上遍历的 `models/` 目录

### 目录布局

兼容 ModelScope 原样解压目录，无需手动整理文件：

```
models/ppocrv6/
├── PP-OCRv6_tiny_det_onnx/inference.onnx
└── PP-OCRv6_tiny_rec_onnx/
    ├── inference.onnx
    └── inference.yml

models/orientation/
└── rapid_orientation.onnx   ← 竖排/倾斜文本方向校正（下载时自动获取）
```

> **提示**：字典自动从 rec 模型目录的 `inference.yml` 解析，无需手动下载字典文件。

## 模型下载

内置 `ModelDownloadService`，从魔塔（ModelScope）一键下载模型，自动整理目录结构：

```csharp
using OnnxOcr.Core;
using OnnxOcr.Core.Configuration;

var downloadService = new ModelDownloadService();
downloadService.StatusChanged += msg => Console.WriteLine(msg);
downloadService.ProgressChanged += p => Console.WriteLine($"进度: {p:P0}");

// 下载 PP-OCRv6 tiny 到 models/ppocrv6/
await downloadService.DownloadPresetModelsAsync(
    OcrModelPreset.PpOcrV6Tiny,
    @"D:\myapp\models\ppocrv6");
```

支持的模型：
- PP-OCRv6 tiny（det + rec + inference.yml）
- PP-OCRv6 small（det + rec + inference.yml）
- PP-OCRv6 medium（det + rec + inference.yml）

> 下载完成后即可直接使用，字典自动从 `inference.yml` 解析。

## 配置选项

### 检测参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `DetLimitSideLen` | 960 | 检测图片边长限制 |
| `DetDbThresh` | 0.3 | DB 二值化阈值 |
| `DetDbBoxThresh` | 0.6 | 检测框阈值 |
| `DetDbUnclipRatio` | 1.5 | 检测框扩展比例 |
| `DetDbMaxCandidates` | 1000 | 最大候选框数量 |

### 识别参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `RecBatchNum` | 6 | 识别批处理数量 |
| `DropScore` | 0.5 | 丢弃置信度低于此值的结果 |
| `UseSpaceChar` | true | 是否使用空格字符 |

### 推理参数

| 参数 | 默认值 | 说明 |
|------|--------|------|
| `CpuThreads` | 4 | CPU 推理线程数 |

## 返回结果

### OcrRunResult

```csharp
public sealed class OcrRunResult
{
    public string ImagePath { get; }                     // 图片路径
    public int ImageWidth { get; }                       // 图片宽度
    public int ImageHeight { get; }                      // 图片高度
    public TimeSpan Elapsed { get; }                     // 总耗时
    public IReadOnlyList<OcrLineItem> Lines { get; }     // 识别文本行列表
}
```

### OcrLineItem

```csharp
public sealed class OcrLineItem
{
    public int Index { get; }                        // 行序号
    public string Text { get; }                      // 识别文本
    public float Score { get; }                      // 置信度 (0-1)
    // Box 为四边形检测框坐标 [(x1,y1), (x2,y2), (x3,y3), (x4,y4)]，共 4 个点
    public IReadOnlyList<(double X, double Y)> Box { get; }
}
```

## 依赖项

- [Microsoft.ML.OnnxRuntime](https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime) 1.26.0
- [OpenCvSharp4](https://www.nuget.org/packages/OpenCvSharp4) 4.13.0
- [OpenCvSharp4.runtime.win](https://www.nuget.org/packages/OpenCvSharp4.runtime.win) 4.13.0
- [Clipper2](https://www.nuget.org/packages/Clipper2) 2.0.0

## 环境要求

- .NET 8 及以上
- Windows x64

## 许可证

Apache License Version 2.0

## 更多信息

- GitHub 仓库：https://github.com/lincyang/OnnxOCRSharp
- 微信公众号：**程序员Linc**

<p align="center">
  <img src="https://raw.githubusercontent.com/lincyang/OnnxOCRSharp/main/assets/wechat-qrcode.jpg" alt="微信公众号：程序员Linc" width="240" />
  <br/>
  <sub>微信扫码关注「程序员Linc」</sub>
</p>

---

### 进阶：直接使用 Core 层

如需更细粒度的控制（如直接处理 `OpenCvSharp.Mat`、自定义检测/识别参数），可绕过 `OcrService`，直接使用 `TextSystem`：

```csharp
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;

var options = OcrOptions.ForPpOcrV6Tiny();
options.DetDbThresh = 0.4f;  // 自定义检测阈值
using var ocr = new TextSystem(options);

using var image = Cv2.ImRead("test.jpg");
var result = ocr.Run(image);  // 返回 OcrResult，含 TextLine[]
```
