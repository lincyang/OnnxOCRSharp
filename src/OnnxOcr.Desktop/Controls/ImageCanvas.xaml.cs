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
    private const double MinZoom = 0.05;
    private const double MaxZoom = 8.0;
    private const double FitPadding = 20;

    private double _zoom = 1.0;
    private bool _userAdjustedZoom;

    public static readonly DependencyProperty PreviewImageProperty =
        DependencyProperty.Register(nameof(PreviewImage), typeof(ImageSource), typeof(ImageCanvas),
            new PropertyMetadata(null, OnPreviewImageChanged));

    public static readonly DependencyProperty LinesProperty =
        DependencyProperty.Register(nameof(Lines), typeof(IEnumerable), typeof(ImageCanvas),
            new PropertyMetadata(null));

    public ImageCanvas()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            if (!_userAdjustedZoom)
                FitToView();
        };
        Loaded += (_, _) =>
        {
            if (!_userAdjustedZoom)
                FitToView();
        };
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
            canvas._userAdjustedZoom = false;
            canvas.UpdateLayoutSize();
            canvas.Dispatcher.BeginInvoke(canvas.FitToView, System.Windows.Threading.DispatcherPriority.Loaded);
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
        _zoom = Math.Clamp(_zoom + delta, MinZoom, MaxZoom);
        _userAdjustedZoom = true;
        ApplyZoom();
        e.Handled = true;
    }

    private void FitToView()
    {
        if (PreviewImage is not BitmapSource bitmap || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            _zoom = 1.0;
            ApplyZoom();
            return;
        }

        var viewportWidth = ScrollHost.ViewportWidth;
        var viewportHeight = ScrollHost.ViewportHeight;

        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            viewportWidth = ActualWidth;
            viewportHeight = ActualHeight;
        }

        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            _zoom = 1.0;
            ApplyZoom();
            return;
        }

        var availableWidth = Math.Max(1, viewportWidth - FitPadding);
        var availableHeight = Math.Max(1, viewportHeight - FitPadding);
        var scale = Math.Min(availableWidth / bitmap.PixelWidth, availableHeight / bitmap.PixelHeight);
        _zoom = Math.Clamp(scale, MinZoom, MaxZoom);
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        RootCanvas.LayoutTransform = new ScaleTransform(_zoom, _zoom);
    }
}
