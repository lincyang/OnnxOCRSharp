# OnnxOCRSharp

OnnxOCR 的 C# 移植版——使用 ONNX Runtime + OpenCvSharp 实现的纯 .NET OCR 方案，内含 WPF 示例程序。

## 项目来源

本项目基于 [OnnxOCR](https://github.com/jingsongliujing/OnnxOCR) 项目进行 C# 移植，感谢原作者的贡献。

## 项目结构

```
OnnxOCRSharp/
├── OnnxOcr.sln
├── models/                   # 模型文件目录
│   ├── ppocrv5/
│   │   ├── det/
│   │   │   └── det.onnx
│   │   ├── rec/
│   │   │   └── rec.onnx
│   │   └── ppocrv5_dict.txt
│   ├── ppocrv6/
│   │   ├── PP-OCRv6_tiny_det_onnx/inference.onnx
│   │   ├── PP-OCRv6_tiny_rec_onnx/inference.onnx
│   │   ├── ppocrv6_tiny_dict.txt
│   │   └── ppocrv6_dict.txt
│   └── orientation/
│       └── rapid_orientation.onnx
├── src/
│   ├── OnnxOcr.Core/          # OCR 引擎（检测 + 识别）
│   ├── OnnxOcr.App/           # 应用服务层
│   ├── OnnxOcr.Desktop/       # WPF 桌面程序
│   └── OnnxOcr.Console/       # 命令行验证工具
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

界面功能：选择图片 → 开始识别 → 左侧预览与检测框 → 右侧结果列表 → 复制全部。

### Visual Studio 2022（Console）

1. 右键 **`OnnxOcr.Console`** → **设为启动项目**
2. 按 **F5** 运行（默认识别 `test_assets/sample.jpg`）

### 命令行

```bash
# 编译
dotnet build

# 识别图片（默认 PP-OCRv5）
dotnet run --project src/OnnxOcr.Console -- test_assets/sample.jpg

# 使用 PP-OCRv6 tiny
dotnet run --project src/OnnxOcr.Console -- --preset v6 test_assets/sample.jpg
```

## 库调用示例

```csharp
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;

// PP-OCRv6 tiny（模型放在 models/ppocrv6/，自动解析路径）
var options = OcrOptions.ForPpOcrV6Tiny();
using var ocr = new TextSystem(options);

using var image = Cv2.ImRead("test.jpg");
var result = ocr.Run(image);

// NuGet 引用方指定 models 根目录
var options2 = OcrOptions.ForPpOcrV6Tiny(@"D:\myapp\models");
```

```csharp
using OnnxOcr.App.Services;
using OnnxOcr.Core.Configuration;

using var service = new OcrService(OcrModelPreset.PpOcrV6Tiny);
var result = await service.RecognizeAsync("test.jpg");
```

## 模型路径

### PP-OCRv5（默认）

自动查找 `models/ppocrv5/`：

```
models/ppocrv5/
├── det/det.onnx
├── rec/rec.onnx
└── ppocrv5_dict.txt
```

### PP-OCRv6 tiny

自动查找 `models/ppocrv6/`，兼容魔塔 / ModelScope 原样解压目录：

```
models/ppocrv6/
├── PP-OCRv6_tiny_det_onnx/inference.onnx
├── PP-OCRv6_tiny_rec_onnx/inference.onnx
└── ppocrv6_tiny_dict.txt
```

**国内下载（魔塔 ModelScope）：**

- `PaddlePaddle/PP-OCRv6_tiny_det_onnx`
- `PaddlePaddle/PP-OCRv6_tiny_rec_onnx`

解压到 `models/ppocrv6/` 即可。字典文件 `ppocrv6_tiny_dict.txt` 已随仓库提供（也可运行 `python tools/export_ppocrv6_dict.py` 从 rec 的 `inference.yml` 重新导出；**注意不要用 `.strip()` 处理字典字符，全角空格 `　` 必须保留**）。

### PP-OCRv6 small / medium

small 与 medium 共用统一字典 `ppocrv6_dict.txt`（约 18708 字符，含日文）；**tiny 使用较小的 `ppocrv6_tiny_dict.txt`（约 6904 字符，不含日文）**，请勿混用。

```
models/ppocrv6/
├── PP-OCRv6_small_det_onnx/inference.onnx
├── PP-OCRv6_small_rec_onnx/inference.onnx
└── ppocrv6_dict.txt
```

## 开源许可证

本项目采用 Apache License Version 2.0 许可证，详见 [LICENSE](LICENSE) 文件。

## 联系方式

- GitHub: https://github.com/lincyang/OnnxOCRSharp
- 微信公众号: 程序员Linc

欢迎关注公众号获取更多技术文章和项目更新！

