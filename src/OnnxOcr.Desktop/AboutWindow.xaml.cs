//-----------------------------------------------------------------------
// <copyright file="AboutWindow.xaml.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;

namespace OnnxOcr.Desktop;

public partial class AboutWindow : Window
{
    public const string WeChatOfficialAccount = "程序员Linc";
    public const string MiniProgramName = "程序员Linc表格识别";
    public const string GitHubUrl = "https://github.com/lincyang/OnnxOCRSharp";

    public string AppVersion { get; }
    public string VersionLabel { get; }
    public string IntroText { get; }
    public string MiniProgramIntro { get; }

    public AboutWindow()
    {
        AppVersion = ResolveVersion();
        VersionLabel = $"版本 {AppVersion}";
        IntroText =
            "LincOCR 社区桌面版，引擎基于 OnnxOCRSharp（ONNX Runtime + OpenCvSharp）。" +
            "支持 PP-OCRv6 文字识别、表格结构识别与单张导出 Excel，以及 CPU/GPU 推理。" +
            "欢迎关注公众号获取更新与用法。";
        MiniProgramIntro =
            "LincOCR 已支持单张表格识别并导出 Excel。" +
            "也可使用微信小程序「程序员Linc表格识别」随时拍照识表、纯文字识别与一键复制。";

        DataContext = this;
        InitializeComponent();
    }

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus > 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "未知" : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnCopyWeChatClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(WeChatOfficialAccount);
        MessageBox.Show(
            $"已复制公众号名称：{WeChatOfficialAccount}\n请打开微信搜索并关注。",
            "提示",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnCopyMiniProgramClick(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(MiniProgramName);
        MessageBox.Show(
            $"已复制小程序名称：{MiniProgramName}\n请打开微信搜索小程序。",
            "提示",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void OnOpenGitHubClick(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Clipboard.SetText(GitHubUrl);
            MessageBox.Show(
                $"无法打开浏览器，已复制仓库地址。\n{ex.Message}",
                "提示",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
