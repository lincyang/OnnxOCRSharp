# OnnxOcrCsharp
C# port of OnnxOCR - Pure .NET implementation using ONNX Runtime + OpenCvSharp. WPF demo included.

## Project Structure
OnnxOCRSharp/
├── OnnxOcr.sln
├── models/ # Model files directory
│ ├── ppocrv5/
│ │ ├── det/
│ │ │ └── det.onnx
│ │ ├── rec/
│ │ │ └── rec.onnx
│ │ └── ppocrv5_dict.txt
│ └── orientation/
│ └── rapid_orientation.onnx
├── src/
│ ├── OnnxOcr.Core/ # OCR engine (detection + recognition)
│ ├── OnnxOcr.App/ # Application service layer
│ ├── OnnxOcr.Desktop/ # WPF desktop application
│ └── OnnxOcr.Console/ # Command-line validation tool
└── test_assets/ # Test images


## Requirements

- .NET 8 SDK
- Windows x64

## Quick Start

### Visual Studio 2022

1. Open `OnnxOcr.sln`
2. Right-click **`OnnxOcr.Desktop`** → **Set as Startup Project** (WPF GUI)
   - Command-line testing can still use **`OnnxOcr.Console`**
3. Press **F5** to run

UI Features: Select image → Start recognition → Preview with detection boxes on the left → Results list on the right → Copy all.

### Visual Studio 2022 (Console)

1. Right-click **`OnnxOcr.Console`** → **Set as Startup Project**
2. Press **F5** to run (recognizes `test_assets/sample.jpg` by default)

### Command Line

```bash
# Build
dotnet build

# Recognize an image
dotnet run --project src/OnnxOcr.Console -- test_assets/sample.jpg
```

## Model Path
Auto-lookup by default (priority order):
- models/ppocrv5/

```
models/ppocrv5/
├── det/det.onnx
├── rec/rec.onnx
└── ppocrv5_dict.txt
```

## License
This project is licensed under the Apache License Version 2.0. See the LICENSE file for details.

## Contact
GitHub: https://github.com/lincyang/OnnxOCRSharp

WeChat Official Account: 程序员Linc

Follow for more technical articles and project updates!

