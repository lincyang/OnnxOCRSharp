//-----------------------------------------------------------------------
// <copyright file="OcrLineViewModel.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using OnnxOcr.App.Models;

namespace OnnxOcr.Desktop.ViewModels;

public partial class OcrLineViewModel : ObservableObject
{
    public int Index { get; init; }

    public string Text { get; init; } = "";

    public float Score { get; init; }

    public string DisplayText => $"{Index}. {Text}";

    public string ScoreText => Score.ToString("F4");

    public PointCollection Points { get; init; } = new();

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private Brush _strokeBrush = Brushes.LimeGreen;

    [ObservableProperty]
    private double _strokeThickness = 2;

    partial void OnIsSelectedChanged(bool value)
    {
        StrokeBrush = value ? Brushes.DeepSkyBlue : Brushes.LimeGreen;
        StrokeThickness = value ? 3 : 2;
    }

    public static OcrLineViewModel From(OcrLineItem item)
    {
        var points = new PointCollection();
        foreach (var (x, y) in item.Box)
            points.Add(new Point(x, y));

        return new OcrLineViewModel
        {
            Index = item.Index,
            Text = item.Text,
            Score = item.Score,
            Points = points,
        };
    }
}
