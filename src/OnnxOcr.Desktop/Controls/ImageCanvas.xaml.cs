//-----------------------------------------------------------------------
// <copyright file="ImageCanvas.xaml.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OnnxOcr.Desktop.Controls;

public partial class ImageCanvas : UserControl
{
    private double _zoom = 1.0;

    public static readonly DependencyProperty PreviewImageProperty =
        DependencyProperty.Register(nameof(PreviewImage), typeof(ImageSource), typeof(ImageCanvas),
            new PropertyMetadata(null, OnPreviewImageChanged));

    public static readonly DependencyProperty LinesProperty =
        DependencyProperty.Register(nameof(Lines), typeof(IEnumerable), typeof(ImageCanvas),
            new PropertyMetadata(null));

    public ImageCanvas()
    {
        InitializeComponent();
    }

    public ImageSource? PreviewImage
    {
        get => (ImageSource?)GetValue(PreviewImageProperty);
        set => SetValue(PreviewImageProperty, value);
    }

    public IEnumerable? Lines
    {
        get => (IEnumerable?)GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    private static void OnPreviewImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageCanvas canvas)
        {
            canvas.UpdateLayoutSize();
            canvas.ResetZoom();
        }
    }

    private void UpdateLayoutSize()
    {
        if (PreviewImage is not BitmapSource bitmap)
        {
            RootCanvas.Width = 0;
            RootCanvas.Height = 0;
            ImageHost.Source = null;
            ImageHost.Width = 0;
            ImageHost.Height = 0;
            OverlayItems.Width = 0;
            OverlayItems.Height = 0;
            return;
        }

        var width = bitmap.PixelWidth;
        var height = bitmap.PixelHeight;

        RootCanvas.Width = width;
        RootCanvas.Height = height;

        ImageHost.Source = bitmap;
        ImageHost.Width = width;
        ImageHost.Height = height;

        OverlayItems.Width = width;
        OverlayItems.Height = height;
    }

    private void ScrollHost_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control || PreviewImage == null)
            return;

        var delta = e.Delta > 0 ? 0.1 : -0.1;
        _zoom = Math.Clamp(_zoom + delta, 0.2, 5.0);
        ApplyZoom();
        e.Handled = true;
    }

    private void ResetZoom()
    {
        _zoom = 1.0;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        RootCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
    }
}
