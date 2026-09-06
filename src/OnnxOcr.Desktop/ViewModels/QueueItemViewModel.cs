//-----------------------------------------------------------------------
// <copyright file="QueueItemViewModel.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using OnnxOcr.App.Models;

namespace OnnxOcr.Desktop.ViewModels;

public enum QueueItemStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
}

public partial class QueueItemViewModel : ObservableObject
{
    public string FilePath { get; }
    public string FileName { get; }

    public ObservableCollection<OcrLineViewModel> Lines { get; } = new();

    [ObservableProperty]
    private QueueItemStatus _status = QueueItemStatus.Pending;

    [ObservableProperty]
    private string _statusText = "等待";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _elapsedText = "-";

    [ObservableProperty]
    private int _lineCount;

    public QueueItemViewModel(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
    }

    public void MarkRunning()
    {
        Status = QueueItemStatus.Running;
        StatusText = "识别中";
        ErrorMessage = null;
    }

    public void MarkSucceeded(OcrRunResult result)
    {
        Lines.Clear();
        foreach (var line in result.Lines)
            Lines.Add(OcrLineViewModel.From(line));

        LineCount = Lines.Count;
        ElapsedText = $"{result.Elapsed.TotalSeconds:F2}s";
        Status = QueueItemStatus.Succeeded;
        StatusText = LineCount > 0 ? $"完成 ({LineCount})" : "无文字";
        ErrorMessage = null;
    }

    public void MarkFailed(string message)
    {
        Lines.Clear();
        LineCount = 0;
        ElapsedText = "-";
        Status = QueueItemStatus.Failed;
        StatusText = "失败";
        ErrorMessage = message;
    }

    public void ResetForRerun()
    {
        if (Status is QueueItemStatus.Succeeded or QueueItemStatus.Failed)
        {
            Status = QueueItemStatus.Pending;
            StatusText = "等待";
            ErrorMessage = null;
            ElapsedText = "-";
            Lines.Clear();
            LineCount = 0;
        }
    }
}
