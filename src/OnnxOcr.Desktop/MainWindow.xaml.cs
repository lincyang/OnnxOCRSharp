//-----------------------------------------------------------------------
// <copyright file="MainWindow.xaml.cs" company="程序员Linc">
// Copyright (c) 程序员Linc. All rights reserved.
// </copyright>
// <author>程序员Linc</author>
// <website>
// https://github.com/lincyang/OnnxOCRSharp
// </website>
// <wechat>公众号：程序员Linc</wechat>
//-----------------------------------------------------------------------
using System.Windows;
using OnnxOcr.Desktop.ViewModels;

namespace OnnxOcr.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        Loaded += OnLoadedAsync;
        Closed += OnClosed;
    }

    private async void OnLoadedAsync(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedAsync;
        await _viewModel.InitializeAsync();
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        await _viewModel.DisposeAsync();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0)
            return;

        _viewModel.AddDroppedPaths(paths);
        e.Handled = true;
    }

    private void OnWeChatPromoClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_viewModel.ShowAboutCommand.CanExecute(null))
            _viewModel.ShowAboutCommand.Execute(null);
    }
}
