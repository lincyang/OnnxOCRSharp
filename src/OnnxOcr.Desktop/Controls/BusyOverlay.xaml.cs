//-----------------------------------------------------------------------
// <copyright file="BusyOverlay.xaml.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace OnnxOcr.Desktop.Controls;

public partial class BusyOverlay : UserControl
{
    public BusyOverlay()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (FindResource("SpinStoryboard") is not Storyboard storyboard)
            return;

        if (IsVisible)
            storyboard.Begin(this, true);
        else
            storyboard.Stop(this);
    }
}
