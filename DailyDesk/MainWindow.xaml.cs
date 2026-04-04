using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Input;
using DailyDesk.ViewModels;

namespace DailyDesk;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        SizeChanged += MainWindow_SizeChanged;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        ApplyWindowChromeTheme();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        _viewModel.UpdateShellLayout(ActualWidth);
        _ = _viewModel.InitializeAsync();
    }

    private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        _viewModel.UpdateShellLayout(e.NewSize.Width);
    }

    private void NestedScrollViewer_OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer)
        {
            return;
        }

        var nextOffset = scrollViewer.VerticalOffset - e.Delta;
        scrollViewer.ScrollToVerticalOffset(nextOffset);
        e.Handled = true;
    }

    private void ApplyWindowChromeTheme()
    {
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var useDark = 1;
            var captionColor = ColorToColorRef(0x0A, 0x0E, 0x14);
            var textColor = ColorToColorRef(0xED, 0xE6, 0xDA);
            var borderColor = ColorToColorRef(0x1E, 0x2A, 0x38);

            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
            _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColor, sizeof(uint));
            _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref textColor, sizeof(uint));
            _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref borderColor, sizeof(uint));
        }
        catch
        {
            // Keep the default window chrome if themed caption attributes are unavailable.
        }
    }

    private static uint ColorToColorRef(byte red, byte green, byte blue) =>
        (uint)(red | (green << 8) | (blue << 16));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute
    );

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref uint pvAttribute,
        int cbAttribute
    );
}
