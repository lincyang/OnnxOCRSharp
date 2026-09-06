# OnnxOCRSharp

C# port of OnnxOCR - Pure .NET implementation using ONNX Runtime + OpenCvSharp. Supports PP-OCRv6 models. WPF demo included.

## Project Source

This project is a C# port of [OnnxOCR](https://github.com/jingsongliujing/OnnxOCR), thanks to the original author.

## Project Structure

```
OnnxOCRSharp/
├── OnnxOcr.sln
├── models/                   # Model files directory
│   └── ppocrv6/
│       ├── PP-OCRv6_tiny_det_onnx/inference.onnx
│       └── PP-OCRv6_tiny_rec_onnx/
│           ├── inference.onnx
│           └── inference.yml
├── src/
│   ├── OnnxOcr.Core/          # OCR engine (detection + recognition)
│   ├── OnnxOcr.App/            # Application service layer
│   ├── OnnxOcr.Desktop/        # WPF desktop application
│   └── OnnxOcr.Console/        # Command-line validation tool
└── test_assets/               # Test images
```

## Requirements

- .NET 8 SDK
- Windows x64

## Quick Start

### Visual Studio 2022

1. Open `OnnxOcr.sln`
2. Right-click **`OnnxOcr.Desktop`** → **Set as Startup Project** (WPF GUI)
   - Command-line testing can still use **`OnnxOcr.Console`**
3. Press **F5** to run

UI Features: Add files/folder (multi-select + drag-drop) → Batch recognize → Queue / preview with boxes / results → Copy current or all → Export.

### Visual Studio 2022 (Console)

1. Right-click **`OnnxOcr.Console`** → **Set as Startup Project**
2. Press **F5** to run (recognizes `test_assets/sample.jpg` by default)

### Command Line

```bash
# Build
dotnet build

# Recognize an image (PP-OCRv6 tiny)
dotnet run --project src/OnnxOcr.Console -- test_assets/sample.jpg

# Use PP-OCRv6 small
dotnet run --project src/OnnxOcr.Console -- --preset v6s test_assets/sample.jpg
```

## Library Usage Examples

### Recommended: Use App Service Layer

```csharp
using OnnxOcr.App.Services;
using OnnxOcr.App.Models;
using OnnxOcr.Core.Configuration;

// Create recognition service (PP-OCRv6 tiny by default)
using var service = new OcrService(OcrModelPreset.PpOcrV6Tiny);

// Recognize image
OcrRunResult result = await service.RecognizeAsync("test.jpg");

foreach (var line in result.Lines)
{
    Console.WriteLine($"[{line.Index}] {line.Text} (Score: {line.Score:P0})");
}
```

### Advanced: Use Core Layer

```csharp
using OnnxOcr.Core.Configuration;
using OnnxOcr.Core.Pipeline;
using OpenCvSharp;

// PP-OCRv6 tiny (models in models/ppocrv6/, auto-resolved)
var options = OcrOptions.ForPpOcrV6Tiny();
using var ocr = new TextSystem(options);

using var image = Cv2.ImRead("test.jpg");
var result = ocr.Run(image);

foreach (var line in result.Lines)
{
    Console.WriteLine($"{line.Text} (Score: {line.Score:F2})");
}
```

## Model Path

The library automatically searches for models in the following locations (by priority):

1. User-specified `modelsRoot` directory
2. `models/` directory traversed upward from the application directory
3. `models/` directory traversed upward from the current working directory

### PP-OCRv6 Directory Layout

Compatible with ModelScope download - no manual file arrangement needed:

```
models/ppocrv6/
├── PP-OCRv6_tiny_det_onnx/inference.onnx
└── PP-OCRv6_tiny_rec_onnx/
    ├── inference.onnx
    └── inference.yml
```

> **Tip**: Dictionary is automatically parsed from `inference.yml` in the rec model directory. No manual dictionary file download required.

## Model Download

### Option 1: Use ModelDownloadService (Recommended)

Built-in `ModelDownloadService` to download models from ModelScope:

```csharp
using OnnxOcr.Core;
using OnnxOcr.Core.Configuration;

var downloadService = new ModelDownloadService();
downloadService.StatusChanged += msg => Console.WriteLine(msg);
downloadService.ProgressChanged += p => Console.WriteLine($"Progress: {p:P0}");

// Download PP-OCRv6 tiny to models/ppocrv6/
await downloadService.DownloadPresetModelsAsync(
    OcrModelPreset.PpOcrV6Tiny,
    @"D:\myapp\models\ppocrv6");
```

Supported models:
- PP-OCRv6 tiny (det + rec + inference.yml)
- PP-OCRv6 small (det + rec + inference.yml)
- PP-OCRv6 medium (det + rec + inference.yml)

### Option 2: Manual Download

**ModelScope Download Links:**

- [PP-OCRv6 tiny det](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_tiny_det_onnx)
- [PP-OCRv6 tiny rec](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_tiny_rec_onnx)
- [PP-OCRv6 small det](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_small_det_onnx)
- [PP-OCRv6 small rec](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_small_rec_onnx)
- [PP-OCRv6 medium det](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_medium_det_onnx)
- [PP-OCRv6 medium rec](https://www.modelscope.cn/models/PaddlePaddle/PP-OCRv6_medium_rec_onnx)

Extract to `models/ppocrv6/` directory.

## Supported Model Presets

| Preset | Description |
|--------|-------------|
| `PpOcrV6Tiny` | Lightweight, recommended |
| `PpOcrV6Small` | Balanced |
| `PpOcrV6Medium` | High precision |

## License

Apache License Version 2.0

## Contact

- GitHub: https://github.com/lincyang/OnnxOCRSharp
- WeChat Official Account: **程序员Linc**

Scan the QR code below to follow:

<p align="center">
  <img src="assets/wechat-qrcode.jpg" alt="WeChat Official Account: 程序员Linc" width="240" />
  <br/>
  <sub>WeChat · 程序员Linc</sub>
</p>
