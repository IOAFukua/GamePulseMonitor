using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GamePulseMonitor.Settings;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingSize = System.Drawing.Size;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

namespace GamePulseMonitor;

public partial class ScreenshotSelectionWindow : Window
{
    private const double MinimumSelectionSize = 4;

    private readonly AppLanguage _language;
    private readonly TaskCompletionSource<ScreenshotResult?> _completion = new();
    private WpfPoint _start;
    private Rect _selection;
    private bool _isSelecting;
    private bool _isCompleting;

    internal ScreenshotSelectionWindow(AppLanguage language)
    {
        _language = language;
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        InfoText.Text = TextCatalog.Get(_language, "ScreenshotInstruction");
        Loaded += OnLoaded;
    }

    internal Task<ScreenshotResult?> CaptureAsync()
    {
        Show();
        Activate();
        Focus();
        return _completion.Task;
    }

    protected override void OnClosed(EventArgs e)
    {
        _completion.TrySetResult(null);
        base.OnClosed(e);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootCanvas.Width = ActualWidth;
        RootCanvas.Height = ActualHeight;
        UpdateDimGeometry(null);
        CanvasSetLeftTop(InfoPanel, 18, 18);
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCompleting)
        {
            return;
        }

        _isSelecting = true;
        _start = e.GetPosition(RootCanvas);
        _selection = new Rect(_start, _start);
        SelectionBorder.Visibility = Visibility.Visible;
        CaptureMouse();
        UpdateSelection(_start);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isSelecting || _isCompleting)
        {
            return;
        }

        UpdateSelection(e.GetPosition(RootCanvas));
        e.Handled = true;
    }

    private async void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting || _isCompleting)
        {
            return;
        }

        _isSelecting = false;
        ReleaseMouseCapture();
        UpdateSelection(e.GetPosition(RootCanvas));
        e.Handled = true;

        if (_selection.Width < MinimumSelectionSize || _selection.Height < MinimumSelectionSize)
        {
            SelectionBorder.Visibility = Visibility.Collapsed;
            UpdateDimGeometry(null);
            return;
        }

        _isCompleting = true;
        try
        {
            var screenRect = ToScreenPixelRect(_selection);
            Hide();
            await Task.Delay(80);
            var bitmap = CaptureScreenRegion(screenRect);
            var filePath = SaveScreenshot(bitmap);
            System.Windows.Clipboard.SetImage(bitmap);
            _completion.TrySetResult(new ScreenshotResult(filePath, screenRect.Width, screenRect.Height));
        }
        catch (Exception ex)
        {
            _completion.TrySetException(ex);
        }
        finally
        {
            Close();
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        _completion.TrySetResult(null);
        Close();
        e.Handled = true;
    }

    private void UpdateSelection(WpfPoint current)
    {
        _selection = new Rect(_start, current);
        SelectionBorder.Width = _selection.Width;
        SelectionBorder.Height = _selection.Height;
        CanvasSetLeftTop(SelectionBorder, _selection.Left, _selection.Top);
        UpdateDimGeometry(_selection);
        UpdateInfoPanel();
    }

    private void UpdateInfoPanel()
    {
        if (_selection.Width >= MinimumSelectionSize && _selection.Height >= MinimumSelectionSize)
        {
            InfoText.Text = $"{Math.Round(_selection.Width)} x {Math.Round(_selection.Height)}";
        }
        else
        {
            InfoText.Text = TextCatalog.Get(_language, "ScreenshotInstruction");
        }

        InfoPanel.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        var preferredLeft = _selection.Width >= MinimumSelectionSize ? _selection.Left : 18;
        var preferredTop = _selection.Height >= MinimumSelectionSize ? _selection.Top - InfoPanel.DesiredSize.Height - 8 : 18;
        if (preferredTop < 8)
        {
            preferredTop = _selection.Bottom + 8;
        }

        CanvasSetLeftTop(
            InfoPanel,
            Math.Clamp(preferredLeft, 8, Math.Max(8, ActualWidth - InfoPanel.DesiredSize.Width - 8)),
            Math.Clamp(preferredTop, 8, Math.Max(8, ActualHeight - InfoPanel.DesiredSize.Height - 8)));
    }

    private void UpdateDimGeometry(Rect? selection)
    {
        var full = new RectangleGeometry(new Rect(0, 0, Math.Max(ActualWidth, Width), Math.Max(ActualHeight, Height)));
        var group = new GeometryGroup
        {
            FillRule = FillRule.EvenOdd
        };
        group.Children.Add(full);
        if (selection is { Width: >= MinimumSelectionSize, Height: >= MinimumSelectionSize } rect)
        {
            group.Children.Add(new RectangleGeometry(rect));
        }

        DimPath.Data = group;
    }

    private Int32Rect ToScreenPixelRect(Rect rect)
    {
        var topLeft = PointToScreen(new WpfPoint(rect.Left, rect.Top));
        var bottomRight = PointToScreen(new WpfPoint(rect.Right, rect.Bottom));
        var width = Math.Max(1, (int)Math.Round(Math.Abs(bottomRight.X - topLeft.X)));
        var height = Math.Max(1, (int)Math.Round(Math.Abs(bottomRight.Y - topLeft.Y)));
        return new Int32Rect(
            (int)Math.Round(Math.Min(topLeft.X, bottomRight.X)),
            (int)Math.Round(Math.Min(topLeft.Y, bottomRight.Y)),
            width,
            height);
    }

    private static BitmapSource CaptureScreenRegion(Int32Rect rect)
    {
        using var bitmap = new DrawingBitmap(rect.Width, rect.Height, DrawingPixelFormat.Format32bppArgb);
        using (var graphics = DrawingGraphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, new DrawingSize(rect.Width, rect.Height));
        }

        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                nint.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    private static string SaveScreenshot(BitmapSource bitmap)
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "GamePulseMonitor");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
        return path;
    }

    private static void CanvasSetLeftTop(UIElement element, double left, double top)
    {
        System.Windows.Controls.Canvas.SetLeft(element, left);
        System.Windows.Controls.Canvas.SetTop(element, top);
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);
}

internal sealed record ScreenshotResult(string FilePath, int Width, int Height);
