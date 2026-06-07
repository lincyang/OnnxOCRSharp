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

# 识别图片
dotnet run --project src/OnnxOcr.Console -- test_assets/sample.jpg
```


## 模型路径

默认自动查找（优先级）：

1. `models/ppocrv5/`

```
models/ppocrv5/
├── det/det.onnx
├── rec/rec.onnx
└── ppocrv5_dict.txt
```

## 开源许可证

本项目采用 Apache License Version 2.0 许可证，详见 [LICENSE](LICENSE) 文件。

## 联系方式

- GitHub: https://github.com/lincyang/OnnxOCRSharp
- 微信公众号: 程序员Linc

欢迎关注公众号获取更多技术文章和项目更新！

