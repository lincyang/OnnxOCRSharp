# OnnxOCRSharp

OnnxOCR 的 C# 移植版——使用 ONNX Runtime + OpenCvSharp 实现的纯 .NET OCR 方案，支持 PP-OCRv6 系列模型，内含 WPF 示例程序。

## 项目来源

本项目基于 [OnnxOCR](https://github.com/jingsongliujing/OnnxOCR) 项目进行 C# 移植，感谢原作者的贡献。

## 项目结构

```
OnnxOCRSharp/
├── OnnxOcr.sln
├── models/                   # 模型文件目录，下载后目录结构如下
│   └── ppocrv6/
│       ├── PP-OCRv6_tiny_det_onnx/inference.onnx
│       └── PP-OCRv6_tiny_rec_onnx/
│           ├── inference.onnx
│           └── inference.yml
├── src/
│   ├── OnnxOcr.Core/          # OCR 引擎（检测 + 识别）
│   ├── OnnxOcr.App/            # 应用服务层
│   ├── OnnxOcr.Desktop/        # WPF 桌面程序
│   └── OnnxOcr.Console/        # 命令行验证工具
└── test_assets/               # 测试图片
```

## 环境要求

- .NET 8 SDK
- Windows x64

## 快速开始

### Visual Studio 2022

1. 打开 `OnnxOcr.sln`
2. 右键 **`OnnxOcr.Desktop`** → **设为启动项目**（WPF 图形界面）
   - 命令行测试仍可用 **`OnnxOcr.Console`**
3. 按 **F5** 运行

界面功能：添加文件/文件夹（支持多选与拖拽）→ 开始批量识别 → 左队列 / 中预览与检测框 / 右结果 → 复制当前/全部 → 导出。

### Visual Studio 2022（Console）

1. 右键 **`OnnxOcr.Console`** → **设为启动项目**
2. 按 **F5** 运行（默认识别 `test_assets/sample.jpg`）

### 命令行

```bash
# 编译
dotnet build

# 识别图片（PP-OCRv6 tiny）
dotnet run --project src/OnnxOcr.Console -- test_assets/sample.jpg

# 使用 PP-OCRv6 small
dotnet run --project src/OnnxOcr.Console -- --preset v6s test_assets/sample.jpg
```

## 库调用示例

### 推荐：使用 App 服务层

```csharp
using OnnxOcr.App.Services;
using OnnxOcr.App.Models;
using OnnxOcr.Core.Configuration;

// 创建识别服务（自动使用 PP-OCRv6 tiny）
using var service = new OcrService(OcrModelPreset.PpOcrV6Tiny);

// 识别图片
OcrRunResult result = await service.RecognizeAsync("test.jpg");

foreach (var line in result.Lines)
{
    Console.WriteLine($"[{line.Index}] {line.Text} (置信度: {line.Score:P0})");
}
```

### 进阶：使用 Core 层

```csharp
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;

// PP-OCRv6 tiny（模型放在 models/ppocrv6/，自动解析路径）
var options = OcrOptions.ForPpOcrV6Tiny();
using var ocr = new TextSystem(options);

using var image = Cv2.ImRead("test.jpg");
var result = ocr.Run(image);

foreach (var line in result.Lines)
{
    Console.WriteLine($"{line.Text} (置信度: {line.Score:F2})");
}
```

## 模型路径

库会自动从以下位置查找模型（按优先级）：

1. 调用方指定的 `modelsRoot` 目录
2. 程序运行目录向上遍历的 `models/` 目录
3. 当前工作目录向上遍历的 `models/` 目录

### PP-OCRv6 目录布局

兼容魔塔（ModelScope）原样解压目录，无需手动整理文件：

```
models/ppocrv6/
├── PP-OCRv6_tiny_det_onnx/inference.onnx
└── PP-OCRv6_tiny_rec_onnx/
    ├── inference.onnx
    └── inference.yml
```

> **提示**：字典自动从 rec 模型目录的 `inference.yml` 解析，无需手动下载字典文件。

## 模型下载

### 方式一：使用 ModelDownloadService（推荐）

内置 `ModelDownloadService`，从魔塔一键下载模型：

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

支持下载的模型：
- PP-OCRv6 tiny（det + rec + inference.yml）
- PP-OCRv6 small（det + rec + inference.yml）
- PP-OCRv6 medium（det + rec + inference.yml）

### 方式二：手动下载

**魔塔 ModelScope 下载地址：**

- [PP-OCRv6 tiny det](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_tiny_det_onnx)
- [PP-OCRv6 tiny rec](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_tiny_rec_onnx)
- [PP-OCRv6 small det](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_small_det_onnx)
- [PP-OCRv6 small rec](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_small_rec_onnx)
- [PP-OCRv6 medium det](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_medium_det_onnx)
- [PP-OCRv6 medium rec](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_medium_rec_onnx)

解压到 `models/ppocrv6/` 目录即可。

## 支持的模型预设

| 预设 | 说明 |
|------|------|
| `PpOcrV6Tiny` | 轻量版，推荐使用 |
| `PpOcrV6Small` | 平衡版 |
| `PpOcrV6Medium` | 高精度版 |

## 开源许可证

本项目采用 Apache License Version 2.0 许可证，详见 [LICENSE](LICENSE) 文件。

## 联系方式

- GitHub: https://github.com/lincyang/OnnxOCRSharp
- 微信公众号: **程序员Linc**

欢迎关注公众号获取更多技术文章和项目更新！

<p align="center">
  <img src="assets/wechat-qrcode.jpg" alt="微信公众号：程序员Linc" width="240" />
  <br/>
  <sub>微信扫码关注「程序员Linc」</sub>
</p>
