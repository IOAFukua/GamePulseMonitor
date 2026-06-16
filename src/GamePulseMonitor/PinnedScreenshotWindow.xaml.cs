using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GamePulseMonitor.Settings;
using Microsoft.Win32;
using IOPath = System.IO.Path;
using WpfClipboard = System.Windows.Clipboard;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace GamePulseMonitor;

public partial class PinnedScreenshotWindow : Window
{
    private const double MinPinnedWidth = 80;
    private const double MinPinnedHeight = 48;
    private const double ResizeHitSize = 16;
    private const double WheelScaleStep = 0.08;

    private readonly BitmapSource _bitmap;
    private readonly AppLanguage _language;
    private ResizeEdge _resizeEdge = ResizeEdge.None;
    private WpfPoint _resizeStartScreen;
    private Rect _resizeStartBounds;

    internal PinnedScreenshotWindow(BitmapSource bitmap, Rect placement, AppLanguage language)
    {
        _bitmap = bitmap;
        _language = language;
        InitializeComponent();
        PinnedImage.Source = bitmap;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        ApplyLanguage();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (IsAltPressed())
        {
            BeginResize(e);
            e.Handled = true;
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove can throw when Windows releases the mouse during a fast click.
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_resizeEdge == ResizeEdge.None)
        {
            return;
        }

        _resizeEdge = ResizeEdge.None;
        ReleaseMouseCapture();
        UpdateCursor(e.GetPosition(this));
        e.Handled = true;
    }

    private void OnMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_resizeEdge != ResizeEdge.None)
        {
            ContinueResize(e);
            e.Handled = true;
            return;
        }

        UpdateCursor(e.GetPosition(this));
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        RootBorder.ContextMenu.PlacementTarget = RootBorder;
        RootBorder.ContextMenu.IsOpen = true;
        e.Handled = true;
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!IsAltPressed())
        {
            return;
        }

        var scale = e.Delta > 0 ? 1 + WheelScaleStep : 1 - WheelScaleStep;
        ScaleAround(e.GetPosition(this), scale);
        e.Handled = true;
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.C)
        {
            CopyToClipboard();
            e.Handled = true;
        }
        else if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.S)
        {
            SaveToFile();
            e.Handled = true;
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        CopyToClipboard();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SaveToFile();
    }

    private void OnDestroyClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyLanguage()
    {
        CopyMenuItem.Header = TextCatalog.Get(_language, "ScreenshotCopy");
        SaveMenuItem.Header = TextCatalog.Get(_language, "ScreenshotSave");
        DestroyMenuItem.Header = TextCatalog.Get(_language, "PinnedScreenshotDestroy");
        ToolTip = TextCatalog.Get(_language, "PinnedScreenshotTip");
    }

    private void CopyToClipboard()
    {
        WpfClipboard.SetImage(_bitmap);
    }

    private void SaveToFile()
    {
        var dialog = new WpfSaveFileDialog
        {
            Title = TextCatalog.Get(_language, "ScreenshotSave"),
            Filter = "PNG (*.png)|*.png",
            FileName = $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            InitialDirectory = IOPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "GamePulseMonitor")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        SaveBitmap(_bitmap, dialog.FileName);
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        var directory = IOPath.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private void BeginResize(MouseButtonEventArgs e)
    {
        _resizeEdge = HitTestResizeEdge(e.GetPosition(this));
        if (_resizeEdge == ResizeEdge.None)
        {
            _resizeEdge = ResizeEdge.BottomRight;
        }

        _resizeStartScreen = PointToScreen(e.GetPosition(this));
        _resizeStartBounds = new Rect(Left, Top, Width, Height);
        CaptureMouse();
        UpdateCursor(e.GetPosition(this));
    }

    private void ContinueResize(WpfMouseEventArgs e)
    {
        var current = PointToScreen(e.GetPosition(this));
        var dx = current.X - _resizeStartScreen.X;
        var dy = current.Y - _resizeStartScreen.Y;
        var left = _resizeStartBounds.Left;
        var top = _resizeStartBounds.Top;
        var width = _resizeStartBounds.Width;
        var height = _resizeStartBounds.Height;

        if (_resizeEdge.HasFlag(ResizeEdge.Left))
        {
            left = _resizeStartBounds.Left + dx;
            width = _resizeStartBounds.Width - dx;
            if (width < MinPinnedWidth)
            {
                left = _resizeStartBounds.Right - MinPinnedWidth;
                width = MinPinnedWidth;
            }
        }
        else if (_resizeEdge.HasFlag(ResizeEdge.Right))
        {
            width = Math.Max(MinPinnedWidth, _resizeStartBounds.Width + dx);
        }

        if (_resizeEdge.HasFlag(ResizeEdge.Top))
        {
            top = _resizeStartBounds.Top + dy;
            height = _resizeStartBounds.Height - dy;
            if (height < MinPinnedHeight)
            {
                top = _resizeStartBounds.Bottom - MinPinnedHeight;
                height = MinPinnedHeight;
            }
        }
        else if (_resizeEdge.HasFlag(ResizeEdge.Bottom))
        {
            height = Math.Max(MinPinnedHeight, _resizeStartBounds.Height + dy);
        }

        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    private void ScaleAround(WpfPoint origin, double scale)
    {
        var nextWidth = Math.Max(MinPinnedWidth, Width * scale);
        var nextHeight = Math.Max(MinPinnedHeight, Height * scale);
        var ratioX = ActualWidth > 0 ? origin.X / ActualWidth : 0.5;
        var ratioY = ActualHeight > 0 ? origin.Y / ActualHeight : 0.5;

        Left -= (nextWidth - Width) * ratioX;
        Top -= (nextHeight - Height) * ratioY;
        Width = nextWidth;
        Height = nextHeight;
    }

    private void UpdateCursor(WpfPoint point)
    {
        if (!IsAltPressed())
        {
            Cursor = WpfCursors.SizeAll;
            return;
        }

        Cursor = HitTestResizeEdge(point) switch
        {
            ResizeEdge.TopLeft or ResizeEdge.BottomRight => WpfCursors.SizeNWSE,
            ResizeEdge.TopRight or ResizeEdge.BottomLeft => WpfCursors.SizeNESW,
            ResizeEdge.Left or ResizeEdge.Right => WpfCursors.SizeWE,
            ResizeEdge.Top or ResizeEdge.Bottom => WpfCursors.SizeNS,
            _ => WpfCursors.SizeNWSE
        };
    }

    private ResizeEdge HitTestResizeEdge(WpfPoint point)
    {
        var left = point.X <= ResizeHitSize;
        var right = point.X >= Math.Max(0, ActualWidth - ResizeHitSize);
        var top = point.Y <= ResizeHitSize;
        var bottom = point.Y >= Math.Max(0, ActualHeight - ResizeHitSize);

        var edge = ResizeEdge.None;
        if (left)
        {
            edge |= ResizeEdge.Left;
        }
        else if (right)
        {
            edge |= ResizeEdge.Right;
        }

        if (top)
        {
            edge |= ResizeEdge.Top;
        }
        else if (bottom)
        {
            edge |= ResizeEdge.Bottom;
        }

        return edge;
    }

    private static bool IsAltPressed()
    {
        return (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
    }

    [Flags]
    private enum ResizeEdge
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        TopLeft = Left | Top,
        TopRight = Right | Top,
        BottomLeft = Left | Bottom,
        BottomRight = Right | Bottom
    }
}
