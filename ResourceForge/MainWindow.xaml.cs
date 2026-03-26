using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ResourceForge.Services;
using ResourceForge.ViewModels;

namespace ResourceForge;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = (MainViewModel)DataContext;
    }

    private void ViewToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.CurrentView == ViewMode.Grid)
        {
            _vm.CurrentView = ViewMode.List;
            ViewToggleIcon.Text = "\uECA5"; // switch icon
        }
        else
        {
            _vm.CurrentView = ViewMode.Grid;
            ViewToggleIcon.Text = "\uE8EF";
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

        int round = NativeMethods.DWMWCP_ROUND;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
            ref round,
            Marshal.SizeOf<int>());

        int darkMode = App.IsDarkMode() ? 1 : 0;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
            ref darkMode,
            Marshal.SizeOf<int>());

        int mica = NativeMethods.DWMSBT_MAINWINDOW;
        NativeMethods.DwmSetWindowAttribute(
            hwnd,
            NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE,
            ref mica,
            Marshal.SizeOf<int>());
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _vm.OpenFileCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            _vm.ReloadFileCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            _vm.SelectResource(null);
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length > 0)
        {
            _vm.HandleDroppedFile(files[0]);
        }
    }

    private void ResourceCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is ResourceItemViewModel clicked)
        {
            _vm.SelectResource(clicked);
        }
    }

    private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            _vm.SelectResource(grid.SelectedItem as ResourceItemViewModel);
        }
    }
}
