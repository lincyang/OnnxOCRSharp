//-----------------------------------------------------------------------
// <copyright file="ExportResultWindow.xaml.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace OnnxOcr.Desktop;

public partial class ExportResultWindow : Window
{
    private readonly string _filePath;

    public ExportResultWindow(string filePath)
    {
        _filePath = filePath;
        InitializeComponent();
        FileNameText.Text = Path.GetFileName(filePath);
        DirectoryText.Text = Path.GetDirectoryName(filePath) ?? "";
    }

    private void OnOpenFileClick(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _filePath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法打开文件：{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!File.Exists(_filePath) && !string.IsNullOrEmpty(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{_filePath}\"",
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"无法打开目录：{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = true;
}
