using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using GamePulseMonitor.Settings;
using Microsoft.Win32;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingSize = System.Drawing.Size;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using IOPath = System.IO.Path;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSaveFileDialog = Microsoft.Win32.SaveFileDialog;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace GamePulseMonitor;

public partial class ScreenshotSelectionWindow : Window
{
    private const double MinimumSelectionSize = 4;
    private const double DefaultMarkerThickness = 3;
    private const int MosaicBlockPixels = 12;

    private readonly AppLanguage _language;
    private readonly HotkeySettings _hotkeys;
    private readonly TaskCompletionSource<ScreenshotResult?> _completion = new();
    private readonly Stack<ScreenshotEditAction> _undoStack = new();
    private readonly Stack<ScreenshotEditAction> _redoStack = new();
    private readonly WriteableBitmap? _frozenBitmap;
    private readonly Int32Rect _frozenScreenRect;
    private WpfColor _markerColor = WpfColor.FromRgb(0xFF, 0x4D, 0x4D);
    private double _markerThickness = DefaultMarkerThickness;
    private double _textFontSize = 18;
    private string _currentRgbText = "RGB --";
    private WpfPoint _start;
    private WpfPoint _annotationStart;
    private Rect _selection;
    private WriteableBitmap? _capturedBitmap;
    private Int32Rect _screenRect;
    private ScreenshotTool _activeTool = ScreenshotTool.None;
    private Polyline? _activePolyline;
    private Line? _activeArrowLine;
    private Polygon? _activeArrowHead;
    private System.Windows.Shapes.Shape? _activeShape;
    private System.Windows.Shapes.Rectangle? _mosaicPreview;
    private ScreenshotShapeKind _shapeKind = ScreenshotShapeKind.Rectangle;
    private bool _isSelecting;
    private bool _isAnnotating;
    private bool _isEditing;
    private bool _isCompletingSelection;

    internal ScreenshotSelectionWindow(AppLanguage language, HotkeySettings hotkeys)
    {
        _language = language;
        _hotkeys = hotkeys.Clone();
        InitializeComponent();
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;
        _frozenScreenRect = CreateVirtualScreenRect();
        _frozenBitmap = CaptureScreenRegion(_frozenScreenRect);
        ApplyLanguage();
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
        FrozenImage.Width = ActualWidth;
        FrozenImage.Height = ActualHeight;
        FrozenImage.Source = _frozenBitmap;
        UpdateDimGeometry(null);
        CanvasSetLeftTop(InfoPanel, 18, 18);
    }

    private void ApplyLanguage()
    {
        InfoText.Text = TextCatalog.Get(_language, "ScreenshotInstruction");
        PenButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotPen"), HotkeyFormatter.Format(_hotkeys.ScreenshotPen));
        ArrowButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotArrow"), HotkeyFormatter.Format(_hotkeys.ScreenshotArrow));
        ShapeButton.ToolTip = TextCatalog.Get(_language, "ScreenshotShape");
        RectangleShapeButton.ToolTip = TextCatalog.Get(_language, "ScreenshotShapeRectangle");
        EllipseShapeButton.ToolTip = TextCatalog.Get(_language, "ScreenshotShapeEllipse");
        StarShapeButton.ToolTip = TextCatalog.Get(_language, "ScreenshotShapeStar");
        TextButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotText"), HotkeyFormatter.Format(_hotkeys.ScreenshotText));
        MosaicButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotMosaic"), HotkeyFormatter.Format(_hotkeys.ScreenshotMosaic));
        EraserButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotEraser"), HotkeyFormatter.Format(_hotkeys.ScreenshotEraser));
        PinButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotPin"), HotkeyFormatter.Format(_hotkeys.ScreenshotPin));
        UndoButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotUndo"), HotkeyFormatter.Format(_hotkeys.ScreenshotUndo));
        RedoButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotRedo"), HotkeyFormatter.Format(_hotkeys.ScreenshotRedo));
        CopyButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotCopy"), $"Enter / {HotkeyFormatter.Format(_hotkeys.ScreenshotCopy)}");
        SaveButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotSave"), HotkeyFormatter.Format(_hotkeys.ScreenshotSave));
        CancelButton.ToolTip = WithShortcut(TextCatalog.Get(_language, "Close"), "Esc");
        SizeDownButton.ToolTip = TextCatalog.Get(_language, "ScreenshotSizeDown");
        SizeUpButton.ToolTip = TextCatalog.Get(_language, "ScreenshotSizeUp");
        RgbPanel.ToolTip = WithShortcut(TextCatalog.Get(_language, "ScreenshotRgbCopy"), HotkeyFormatter.Format(_hotkeys.ScreenshotCopyRgb));
        ColorRedButton.ToolTip = TextCatalog.Get(_language, "ColorRed");
        ColorAmberButton.ToolTip = TextCatalog.Get(_language, "ColorAmber");
        ColorGreenButton.ToolTip = TextCatalog.Get(_language, "ColorGreen");
        ColorCyanButton.ToolTip = TextCatalog.Get(_language, "ColorCyan");
        ColorVioletButton.ToolTip = TextCatalog.Get(_language, "ColorViolet");
        ColorWhiteButton.ToolTip = TextCatalog.Get(_language, "ColorLight");
        RgbText.Text = _currentRgbText;
        UpdateColorButtons();
        UpdateShapeButtons();
        UpdateSizeText();
    }

    private static string WithShortcut(string label, string shortcut) => string.IsNullOrWhiteSpace(shortcut) ? label : $"{label} ({shortcut})";

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_isCompletingSelection || ToolPanel.IsMouseOver)
        {
            return;
        }

        if (_isEditing)
        {
            BeginAnnotation(e);
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
        if (_isCompletingSelection)
        {
            return;
        }

        if (_isSelecting)
        {
            UpdateSelection(e.GetPosition(RootCanvas));
            e.Handled = true;
            return;
        }

        if (_isEditing)
        {
            var rootPoint = e.GetPosition(RootCanvas);
            if (_selection.Contains(rootPoint))
            {
                UpdateRgbDisplay(ToLocalPoint(rootPoint));
            }

            UpdateAnnotation(e);
        }
    }

    private async void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isCompletingSelection)
        {
            return;
        }

        if (_isSelecting)
        {
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

            await CompleteSelectionAsync();
            return;
        }

        if (_isEditing && _isAnnotating)
        {
            FinishAnnotation(e);
        }
    }

    private Task CompleteSelectionAsync()
    {
        _isCompletingSelection = true;
        try
        {
            _screenRect = ToScreenPixelRect(_selection);
            _capturedBitmap = CropFrozenBitmap(_screenRect);
            Activate();
            Focus();
            ShowEditor();
        }
        catch (Exception ex)
        {
            _completion.TrySetException(ex);
            Close();
        }
        finally
        {
            _isCompletingSelection = false;
        }

        return Task.CompletedTask;
    }

    private void ShowEditor()
    {
        if (_capturedBitmap is null)
        {
            return;
        }

        _isEditing = true;
        Cursor = System.Windows.Input.Cursors.Arrow;
        CaptureLayer.Width = _selection.Width;
        CaptureLayer.Height = _selection.Height;
        CaptureImage.Width = _selection.Width;
        CaptureImage.Height = _selection.Height;
        AnnotationCanvas.Width = _selection.Width;
        AnnotationCanvas.Height = _selection.Height;
        CaptureImage.Source = _capturedBitmap;
        CaptureLayer.Visibility = Visibility.Visible;
        CanvasSetLeftTop(CaptureLayer, _selection.Left, _selection.Top);
        CanvasSetLeftTop(SelectionBorder, _selection.Left, _selection.Top);
        SelectionBorder.Width = _selection.Width;
        SelectionBorder.Height = _selection.Height;
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateDimGeometry(_selection);
        UpdateInfoPanel();
        PositionRgbPanel();
        RgbPanel.Visibility = Visibility.Visible;
        PositionToolPanel();
        SetActiveTool(ScreenshotTool.None);
        UpdateHistoryButtons();
    }

    private async void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancel();
            e.Handled = true;
            return;
        }

        if (!_isEditing && IsSelectAllHotkey(e))
        {
            await SelectFullScreenAsync();
            e.Handled = true;
            return;
        }

        if (!_isEditing)
        {
            return;
        }

        if (IsTextEditing(e.OriginalSource))
        {
            return;
        }

        if (MatchesHotkey(e, _hotkeys.ScreenshotCopyRgb))
        {
            CopyRgbToClipboard();
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotPen))
        {
            SetActiveTool(ScreenshotTool.Pen);
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotArrow))
        {
            SetActiveTool(ScreenshotTool.Arrow);
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotText))
        {
            SetActiveTool(ScreenshotTool.Text);
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotMosaic))
        {
            SetActiveTool(ScreenshotTool.Mosaic);
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotEraser))
        {
            SetActiveTool(ScreenshotTool.Eraser);
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotPin))
        {
            PinAndClose();
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotUndo))
        {
            Undo();
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotRedo))
        {
            Redo();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter || MatchesHotkey(e, _hotkeys.ScreenshotCopy))
        {
            CopyAndClose();
            e.Handled = true;
        }
        else if (MatchesHotkey(e, _hotkeys.ScreenshotSave))
        {
            SaveAndMaybeClose();
            e.Handled = true;
        }
    }

    private void BeginAnnotation(MouseButtonEventArgs e)
    {
        if (_capturedBitmap is null)
        {
            return;
        }

        var rootPoint = e.GetPosition(RootCanvas);
        if (!_selection.Contains(rootPoint))
        {
            return;
        }

        var point = ToLocalPoint(rootPoint);

        if (_activeTool == ScreenshotTool.Eraser)
        {
            _isAnnotating = true;
            CaptureMouse();
            EraseAt(point);
            e.Handled = true;
            return;
        }

        if (_activeTool == ScreenshotTool.Text)
        {
            AddTextBox(point);
            e.Handled = true;
            return;
        }

        if (_activeTool == ScreenshotTool.None)
        {
            return;
        }

        _annotationStart = point;
        _isAnnotating = true;
        CaptureMouse();

        switch (_activeTool)
        {
            case ScreenshotTool.Pen:
                _activePolyline = new Polyline
                {
                    Stroke = CreateMarkerBrush(),
                    StrokeThickness = _markerThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                    Points = new PointCollection { point }
                };
                AnnotationCanvas.Children.Add(_activePolyline);
                break;
            case ScreenshotTool.Arrow:
                _activeArrowLine = new Line
                {
                    Stroke = CreateMarkerBrush(),
                    StrokeThickness = _markerThickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    X1 = point.X,
                    Y1 = point.Y,
                    X2 = point.X,
                    Y2 = point.Y
                };
                _activeArrowHead = new Polygon
                {
                    Fill = CreateMarkerBrush()
                };
                AnnotationCanvas.Children.Add(_activeArrowLine);
                AnnotationCanvas.Children.Add(_activeArrowHead);
                break;
            case ScreenshotTool.Shape:
                _activeShape = CreateShapeElement();
                AnnotationCanvas.Children.Add(_activeShape);
                UpdateShapeElement(_activeShape, new Rect(_annotationStart, point));
                break;
            case ScreenshotTool.Mosaic:
                _mosaicPreview = new System.Windows.Shapes.Rectangle
                {
                    Stroke = WpfBrushes.White,
                    StrokeThickness = 1,
                    Fill = new SolidColorBrush(WpfColor.FromArgb(0x33, 0xFF, 0xFF, 0xFF))
                };
                AnnotationCanvas.Children.Add(_mosaicPreview);
                UpdateMosaicPreview(point);
                break;
        }

        e.Handled = true;
    }

    private void UpdateAnnotation(System.Windows.Input.MouseEventArgs e)
    {
        var rootPoint = e.GetPosition(RootCanvas);
        if (!_isAnnotating)
        {
            return;
        }

        var point = ClampToSelection(ToLocalPoint(rootPoint));
        switch (_activeTool)
        {
            case ScreenshotTool.Eraser:
                EraseAt(point);
                break;
            case ScreenshotTool.Pen:
                _activePolyline?.Points.Add(point);
                break;
            case ScreenshotTool.Arrow:
                if (_activeArrowLine is not null)
                {
                    _activeArrowLine.X2 = point.X;
                    _activeArrowLine.Y2 = point.Y;
                    UpdateArrowHead(_annotationStart, point);
                }
                break;
            case ScreenshotTool.Shape:
                if (_activeShape is not null)
                {
                    UpdateShapeElement(_activeShape, new Rect(_annotationStart, point));
                }
                break;
            case ScreenshotTool.Mosaic:
                UpdateMosaicPreview(point);
                break;
        }

        e.Handled = true;
    }

    private void FinishAnnotation(MouseButtonEventArgs e)
    {
        _isAnnotating = false;
        ReleaseMouseCapture();
        var rootPoint = e.GetPosition(RootCanvas);
        var point = ClampToSelection(ToLocalPoint(rootPoint));

        if (_activeTool == ScreenshotTool.Mosaic)
        {
            var rect = new Rect(_annotationStart, point);
            if (_mosaicPreview is not null)
            {
                AnnotationCanvas.Children.Remove(_mosaicPreview);
                _mosaicPreview = null;
            }

            ApplyMosaic(rect);
        }
        else if (_activeTool == ScreenshotTool.Pen && _activePolyline is not null)
        {
            if (_activePolyline.Points.Count > 1)
            {
                PushAction(ScreenshotEditAction.AddVisual([_activePolyline]));
            }
            else
            {
                AnnotationCanvas.Children.Remove(_activePolyline);
            }
        }
        else if (_activeTool == ScreenshotTool.Arrow && _activeArrowLine is not null && _activeArrowHead is not null)
        {
            var length = Distance(
                new WpfPoint(_activeArrowLine.X1, _activeArrowLine.Y1),
                new WpfPoint(_activeArrowLine.X2, _activeArrowLine.Y2));
            if (length >= MinimumSelectionSize)
            {
                PushAction(ScreenshotEditAction.AddVisual([_activeArrowLine, _activeArrowHead]));
            }
            else
            {
                AnnotationCanvas.Children.Remove(_activeArrowLine);
                AnnotationCanvas.Children.Remove(_activeArrowHead);
            }
        }
        else if (_activeTool == ScreenshotTool.Shape && _activeShape is not null)
        {
            var rect = new Rect(_annotationStart, point);
            if (rect.Width >= MinimumSelectionSize && rect.Height >= MinimumSelectionSize)
            {
                PushAction(ScreenshotEditAction.AddVisual([_activeShape]));
            }
            else
            {
                AnnotationCanvas.Children.Remove(_activeShape);
            }
        }

        _activePolyline = null;
        _activeArrowLine = null;
        _activeArrowHead = null;
        _activeShape = null;
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
            InfoText.Text = _isEditing
                ? TextCatalog.Get(_language, "ScreenshotEditInstruction")
                : $"{Math.Round(_selection.Width)} x {Math.Round(_selection.Height)}";
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

    private void PositionToolPanel()
    {
        ToolPanel.Visibility = Visibility.Visible;
        ToolPanel.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        var top = _selection.Bottom + 8;
        if (top + ToolPanel.DesiredSize.Height > ActualHeight - 8)
        {
            top = _selection.Top - ToolPanel.DesiredSize.Height - 8;
        }

        CanvasSetLeftTop(
            ToolPanel,
            Math.Clamp(_selection.Right - ToolPanel.DesiredSize.Width, 8, Math.Max(8, ActualWidth - ToolPanel.DesiredSize.Width - 8)),
            Math.Clamp(top, 8, Math.Max(8, ActualHeight - ToolPanel.DesiredSize.Height - 8)));
    }

    private void PositionRgbPanel()
    {
        RgbPanel.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        CanvasSetLeftTop(
            RgbPanel,
            Math.Clamp(_selection.Right - RgbPanel.DesiredSize.Width - 8, 8, Math.Max(8, ActualWidth - RgbPanel.DesiredSize.Width - 8)),
            Math.Clamp(_selection.Top + 8, 8, Math.Max(8, ActualHeight - RgbPanel.DesiredSize.Height - 8)));
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

    private void OnPenClick(object sender, RoutedEventArgs e) => SetActiveTool(ScreenshotTool.Pen);

    private void OnArrowClick(object sender, RoutedEventArgs e) => SetActiveTool(ScreenshotTool.Arrow);

    private void OnShapeClick(object sender, RoutedEventArgs e) => SetActiveTool(ScreenshotTool.Shape);

    private void OnRectangleShapeClick(object sender, RoutedEventArgs e) => SetShapeKind(ScreenshotShapeKind.Rectangle);

    private void OnEllipseShapeClick(object sender, RoutedEventArgs e) => SetShapeKind(ScreenshotShapeKind.Ellipse);

    private void OnStarShapeClick(object sender, RoutedEventArgs e) => SetShapeKind(ScreenshotShapeKind.Star);

    private void OnTextClick(object sender, RoutedEventArgs e) => SetActiveTool(ScreenshotTool.Text);

    private void OnMosaicClick(object sender, RoutedEventArgs e) => SetActiveTool(ScreenshotTool.Mosaic);

    private void OnEraserClick(object sender, RoutedEventArgs e) => SetActiveTool(ScreenshotTool.Eraser);

    private void OnUndoClick(object sender, RoutedEventArgs e) => Undo();

    private void OnRedoClick(object sender, RoutedEventArgs e) => Redo();

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string hex })
        {
            return;
        }

        SetMarkerColor(hex);
    }

    private void OnSizeDownClick(object sender, RoutedEventArgs e) => AdjustActiveSize(-1);

    private void OnSizeUpClick(object sender, RoutedEventArgs e) => AdjustActiveSize(1);

    private void OnCopyClick(object sender, RoutedEventArgs e) => CopyAndClose();

    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveAndMaybeClose();

    private void OnCancelClick(object sender, RoutedEventArgs e) => Cancel();

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        PinAndClose();
    }

    private void PinAndClose()
    {
        if (!_isEditing || _capturedBitmap is null)
        {
            return;
        }

        var bitmap = RenderEditedBitmap();
        var pin = new PinnedScreenshotWindow(bitmap, new Rect(Left + _selection.Left, Top + _selection.Top, _selection.Width, _selection.Height), _language);
        pin.Show();
        _completion.TrySetResult(ScreenshotResult.Pinned(bitmap.PixelWidth, bitmap.PixelHeight));
        Close();
    }

    private void SetActiveTool(ScreenshotTool tool)
    {
        _activeTool = tool;
        Cursor = tool switch
        {
            ScreenshotTool.Eraser => System.Windows.Input.Cursors.Hand,
            ScreenshotTool.None => System.Windows.Input.Cursors.Arrow,
            _ => System.Windows.Input.Cursors.Pen
        };

        MarkButton(PenButton, tool == ScreenshotTool.Pen);
        MarkButton(ArrowButton, tool == ScreenshotTool.Arrow);
        MarkButton(ShapeButton, tool == ScreenshotTool.Shape);
        MarkButton(TextButton, tool == ScreenshotTool.Text);
        MarkButton(MosaicButton, tool == ScreenshotTool.Mosaic);
        MarkButton(EraserButton, tool == ScreenshotTool.Eraser);
        UpdateMarkerOptionsVisibility();
    }

    private static void MarkButton(System.Windows.Controls.Button button, bool active)
    {
        button.Background = active
            ? new SolidColorBrush(WpfColor.FromRgb(0x27, 0x84, 0xC7))
            : new SolidColorBrush(WpfColor.FromRgb(0x26, 0x33, 0x42));
    }

    private void SetMarkerColor(string hex)
    {
        if (System.Windows.Media.ColorConverter.ConvertFromString(hex) is WpfColor color)
        {
            _markerColor = color;
            UpdateColorButtons();
        }
    }

    private void SetShapeKind(ScreenshotShapeKind kind)
    {
        _shapeKind = kind;
        SetActiveTool(ScreenshotTool.Shape);
        UpdateShapeButtons();
    }

    private void UpdateColorButtons()
    {
        MarkColorButton(ColorRedButton);
        MarkColorButton(ColorAmberButton);
        MarkColorButton(ColorGreenButton);
        MarkColorButton(ColorCyanButton);
        MarkColorButton(ColorVioletButton);
        MarkColorButton(ColorWhiteButton);
    }

    private void UpdateShapeButtons()
    {
        MarkButton(RectangleShapeButton, _shapeKind == ScreenshotShapeKind.Rectangle);
        MarkButton(EllipseShapeButton, _shapeKind == ScreenshotShapeKind.Ellipse);
        MarkButton(StarShapeButton, _shapeKind == ScreenshotShapeKind.Star);
    }

    private void MarkColorButton(System.Windows.Controls.Button button)
    {
        var active = button.Tag is string hex &&
                     System.Windows.Media.ColorConverter.ConvertFromString(hex) is WpfColor color &&
                     color.R == _markerColor.R &&
                     color.G == _markerColor.G &&
                     color.B == _markerColor.B;
        button.BorderBrush = active
            ? new SolidColorBrush(WpfColor.FromRgb(0x7D, 0xE3, 0xFF))
            : new SolidColorBrush(WpfColor.FromRgb(0x53, 0x65, 0x7A));
        button.BorderThickness = active ? new Thickness(2) : new Thickness(1);
    }

    private void UpdateMarkerOptionsVisibility()
    {
        var showShapes = _activeTool == ScreenshotTool.Shape;
        var showColors = _activeTool is ScreenshotTool.Pen or ScreenshotTool.Arrow or ScreenshotTool.Shape or ScreenshotTool.Text;
        var showSize = _activeTool is ScreenshotTool.Pen or ScreenshotTool.Arrow or ScreenshotTool.Shape or ScreenshotTool.Text;
        MarkerOptionsPanel.Visibility = showColors || showSize ? Visibility.Visible : Visibility.Collapsed;
        ShapeOptionsPanel.Visibility = showShapes ? Visibility.Visible : Visibility.Collapsed;
        ColorOptionsPanel.Visibility = showColors ? Visibility.Visible : Visibility.Collapsed;
        SizeOptionsPanel.Visibility = showSize ? Visibility.Visible : Visibility.Collapsed;
        UpdateSizeText();
        if (_isEditing)
        {
            PositionToolPanel();
        }
    }

    private void AdjustActiveSize(int direction)
    {
        if (_activeTool == ScreenshotTool.Text)
        {
            _textFontSize = Math.Clamp(_textFontSize + direction * 2, 10, 72);
        }
        else if (_activeTool is ScreenshotTool.Pen or ScreenshotTool.Arrow or ScreenshotTool.Shape)
        {
            _markerThickness = Math.Clamp(_markerThickness + direction, 1, 12);
        }

        UpdateSizeText();
    }

    private void UpdateSizeText()
    {
        SizeValueText.Text = _activeTool == ScreenshotTool.Text
            ? _textFontSize.ToString("0")
            : _markerThickness.ToString("0");
    }

    private void AddTextBox(WpfPoint point)
    {
        var textBox = new WpfTextBox
        {
            Width = Math.Min(220, Math.Max(90, _selection.Width - point.X - 8)),
            MinWidth = 60,
            MinHeight = Math.Max(28, _textFontSize + 12),
            Background = WpfBrushes.Transparent,
            BorderBrush = new SolidColorBrush(WpfColor.FromRgb(0x7D, 0xE3, 0xFF)),
            BorderThickness = new Thickness(1),
            CaretBrush = CreateMarkerBrush(),
            Foreground = CreateMarkerBrush(),
            FontSize = _textFontSize,
            Padding = new Thickness(3, 1, 3, 1),
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true
        };
        textBox.LostFocus += OnTextBoxLostFocus;
        textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
        CanvasSetLeftTop(textBox, point.X, point.Y);
        AnnotationCanvas.Children.Add(textBox);
        PushAction(ScreenshotEditAction.AddVisual([textBox]));
        textBox.Focus();
    }

    private void OnTextBoxPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            AnnotationCanvas.Children.Remove(textBox);
            e.Handled = true;
        }
    }

    private void OnTextBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfTextBox textBox)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(textBox.Text))
        {
            AnnotationCanvas.Children.Remove(textBox);
            return;
        }

            textBox.BorderBrush = WpfBrushes.Transparent;
    }

    private void CommitTextBoxes()
    {
        foreach (var textBox in AnnotationCanvas.Children.OfType<WpfTextBox>().ToArray())
        {
            if (string.IsNullOrWhiteSpace(textBox.Text))
            {
                AnnotationCanvas.Children.Remove(textBox);
            }
            else
            {
                textBox.BorderBrush = WpfBrushes.Transparent;
            }
        }
    }

    private void PushAction(ScreenshotEditAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();
        UpdateHistoryButtons();
    }

    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        var action = _undoStack.Pop();
        ApplyAction(action, undo: true);
        _redoStack.Push(action);
        UpdateHistoryButtons();
    }

    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        var action = _redoStack.Pop();
        ApplyAction(action, undo: false);
        _undoStack.Push(action);
        UpdateHistoryButtons();
    }

    private void ApplyAction(ScreenshotEditAction action, bool undo)
    {
        switch (action.Kind)
        {
            case ScreenshotEditActionKind.AddVisual:
                if (undo)
                {
                    RemoveVisuals(action.Elements);
                }
                else
                {
                    AddVisuals(action.Elements);
                }
                break;
            case ScreenshotEditActionKind.RemoveVisual:
                if (undo)
                {
                    AddVisuals(action.Elements);
                }
                else
                {
                    RemoveVisuals(action.Elements);
                }
                break;
            case ScreenshotEditActionKind.Mosaic:
                RestoreBitmap(undo ? action.BeforePixels : action.AfterPixels, action.Width, action.Height, action.Stride);
                break;
        }
    }

    private void UpdateHistoryButtons()
    {
        UndoButton.IsEnabled = _undoStack.Count > 0;
        RedoButton.IsEnabled = _redoStack.Count > 0;
        UndoButton.Opacity = UndoButton.IsEnabled ? 1 : 0.45;
        RedoButton.Opacity = RedoButton.IsEnabled ? 1 : 0.45;
    }

    private void AddVisuals(IReadOnlyList<UIElement> elements)
    {
        foreach (var element in elements)
        {
            if (!AnnotationCanvas.Children.Contains(element))
            {
                AnnotationCanvas.Children.Add(element);
            }
        }
    }

    private void RemoveVisuals(IReadOnlyList<UIElement> elements)
    {
        foreach (var element in elements)
        {
            AnnotationCanvas.Children.Remove(element);
        }
    }

    private void RestoreBitmap(byte[] pixels, int width, int height, int stride)
    {
        if (_capturedBitmap is null || pixels.Length == 0)
        {
            return;
        }

        _capturedBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        CaptureImage.Source = _capturedBitmap;
    }

    private void EraseAt(WpfPoint point)
    {
        var target = FindEraseTarget(point);
        if (target is null)
        {
            return;
        }

        var action = FindVisualAddAction(target);
        var elements = action?.Elements ?? [target];
        RemoveVisuals(elements);
        PushAction(ScreenshotEditAction.RemoveVisual(elements));
    }

    private UIElement? FindEraseTarget(WpfPoint point)
    {
        for (var i = AnnotationCanvas.Children.Count - 1; i >= 0; i--)
        {
            if (AnnotationCanvas.Children[i] is not UIElement element || ReferenceEquals(element, _mosaicPreview))
            {
                continue;
            }

            if (IsEraseHit(element, point))
            {
                return element;
            }
        }

        return null;
    }

    private ScreenshotEditAction? FindVisualAddAction(UIElement element)
    {
        return _undoStack
            .Concat(_redoStack)
            .FirstOrDefault(action =>
                action.Kind == ScreenshotEditActionKind.AddVisual &&
                action.Elements.Contains(element));
    }

    private static bool IsEraseHit(UIElement element, WpfPoint point)
    {
        const double radius = 10;
        return element switch
        {
            Polyline polyline => IsPolylineHit(polyline, point, radius),
            Line line => DistanceToSegment(
                point,
                new WpfPoint(line.X1, line.Y1),
                new WpfPoint(line.X2, line.Y2)) <= radius,
            Polygon polygon => IsPolygonHit(polygon, point, radius),
            System.Windows.Shapes.Shape shape => IsElementBoundsHit(shape, point, radius),
            WpfTextBox textBox => IsElementBoundsHit(textBox, point, radius),
            _ => false
        };
    }

    private static bool IsElementBoundsHit(FrameworkElement element, WpfPoint point, double radius)
    {
        var left = Canvas.GetLeft(element) - radius;
        var top = Canvas.GetTop(element) - radius;
        var width = double.IsNaN(element.ActualWidth) || element.ActualWidth <= 0 ? element.Width : element.ActualWidth;
        var height = double.IsNaN(element.ActualHeight) || element.ActualHeight <= 0 ? element.Height : element.ActualHeight;
        return point.X >= left && point.X <= left + width + radius * 2 &&
               point.Y >= top && point.Y <= top + height + radius * 2;
    }

    private static bool IsPolylineHit(Polyline polyline, WpfPoint point, double radius)
    {
        if (polyline.Points.Count == 0)
        {
            return false;
        }

        if (polyline.Points.Count == 1)
        {
            return Distance(point, polyline.Points[0]) <= radius;
        }

        for (var i = 1; i < polyline.Points.Count; i++)
        {
            if (DistanceToSegment(point, polyline.Points[i - 1], polyline.Points[i]) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPolygonHit(Polygon polygon, WpfPoint point, double radius)
    {
        if (polygon.Points.Count == 0)
        {
            return false;
        }

        var left = polygon.Points.Min(p => p.X) - radius;
        var top = polygon.Points.Min(p => p.Y) - radius;
        var right = polygon.Points.Max(p => p.X) + radius;
        var bottom = polygon.Points.Max(p => p.Y) + radius;
        return point.X >= left && point.X <= right && point.Y >= top && point.Y <= bottom;
    }

    private static double DistanceToSegment(WpfPoint point, WpfPoint start, WpfPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
        {
            return Distance(point, start);
        }

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / (dx * dx + dy * dy);
        t = Math.Clamp(t, 0, 1);
        return Distance(point, new WpfPoint(start.X + t * dx, start.Y + t * dy));
    }

    private static double Distance(WpfPoint first, WpfPoint second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private void CopyAndClose()
    {
        if (!_isEditing)
        {
            return;
        }

        var bitmap = RenderEditedBitmap();
        System.Windows.Clipboard.SetImage(bitmap);
        _completion.TrySetResult(ScreenshotResult.Copied(bitmap.PixelWidth, bitmap.PixelHeight));
        Close();
    }

    private void SaveAndMaybeClose()
    {
        if (!_isEditing)
        {
            return;
        }

        var path = ShowSaveDialog();
        if (path is null)
        {
            Activate();
            Focus();
            return;
        }

        var bitmap = RenderEditedBitmap();
        SaveScreenshot(bitmap, path);
        _completion.TrySetResult(ScreenshotResult.Saved(path, bitmap.PixelWidth, bitmap.PixelHeight));
        Close();
    }

    private string? ShowSaveDialog()
    {
        var defaultPath = CreateDefaultScreenshotPath();
        var dialog = new WpfSaveFileDialog
        {
            Title = TextCatalog.Get(_language, "ScreenshotSave"),
            Filter = "PNG (*.png)|*.png",
            FileName = IOPath.GetFileName(defaultPath),
            InitialDirectory = IOPath.GetDirectoryName(defaultPath),
            AddExtension = true,
            DefaultExt = ".png",
            OverwritePrompt = true
        };

        return dialog.ShowDialog(this) == true ? dialog.FileName : null;
    }

    private void Cancel()
    {
        _completion.TrySetResult(null);
        Close();
    }

    private BitmapSource RenderEditedBitmap()
    {
        if (_capturedBitmap is null)
        {
            throw new InvalidOperationException("No screenshot has been captured.");
        }

        CommitTextBoxes();
        CaptureLayer.Measure(new WpfSize(_selection.Width, _selection.Height));
        CaptureLayer.Arrange(new Rect(0, 0, _selection.Width, _selection.Height));
        CaptureLayer.UpdateLayout();
        var dpiX = _capturedBitmap.PixelWidth / Math.Max(_selection.Width, 1) * 96d;
        var dpiY = _capturedBitmap.PixelHeight / Math.Max(_selection.Height, 1) * 96d;
        var target = new RenderTargetBitmap(_capturedBitmap.PixelWidth, _capturedBitmap.PixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        target.Render(CaptureLayer);
        target.Freeze();
        return target;
    }

    private void UpdateRgbDisplay(WpfPoint localPoint)
    {
        if (_capturedBitmap is null)
        {
            return;
        }

        var x = Math.Clamp((int)Math.Round(localPoint.X / Math.Max(_selection.Width, 1) * (_capturedBitmap.PixelWidth - 1)), 0, _capturedBitmap.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Round(localPoint.Y / Math.Max(_selection.Height, 1) * (_capturedBitmap.PixelHeight - 1)), 0, _capturedBitmap.PixelHeight - 1);
        var pixels = new byte[4];
        _capturedBitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixels, 4, 0);
        var color = WpfColor.FromRgb(pixels[2], pixels[1], pixels[0]);
        _currentRgbText = $"RGB {color.R}, {color.G}, {color.B}  #{color.R:X2}{color.G:X2}{color.B:X2}";
        RgbText.Text = _currentRgbText;
    }

    private void CopyRgbToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(_currentRgbText) && _currentRgbText != "RGB --")
        {
            System.Windows.Clipboard.SetText(_currentRgbText);
        }
    }

    private static bool IsTextEditing(object source)
    {
        return source is WpfTextBox || Keyboard.FocusedElement is WpfTextBox;
    }

    private static bool MatchesHotkey(System.Windows.Input.KeyEventArgs e, HotkeySetting hotkey)
    {
        if (hotkey.Key == 0)
        {
            return false;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey <= 0 || (uint)virtualKey != hotkey.Key)
        {
            return false;
        }

        return ToHotkeyModifiers(Keyboard.Modifiers) == hotkey.Modifiers;
    }

    private static HotkeyModifiers ToHotkeyModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            result |= HotkeyModifiers.Control;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            result |= HotkeyModifiers.Shift;
        }

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            result |= HotkeyModifiers.Alt;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            result |= HotkeyModifiers.Win;
        }

        return result;
    }

    private void UpdateMosaicPreview(WpfPoint point)
    {
        if (_mosaicPreview is null)
        {
            return;
        }

        var rect = new Rect(_annotationStart, ClampToSelection(point));
        _mosaicPreview.Width = rect.Width;
        _mosaicPreview.Height = rect.Height;
        CanvasSetLeftTop(_mosaicPreview, rect.Left, rect.Top);
    }

    private void ApplyMosaic(Rect localRect)
    {
        if (_capturedBitmap is null)
        {
            return;
        }

        var pixelRect = ToPixelRect(localRect);
        if (pixelRect.Width < 2 || pixelRect.Height < 2)
        {
            return;
        }

        var width = _capturedBitmap.PixelWidth;
        var height = _capturedBitmap.PixelHeight;
        var stride = width * 4;
        var pixels = new byte[stride * height];
        _capturedBitmap.CopyPixels(pixels, stride, 0);
        var beforePixels = pixels.ToArray();

        for (var blockY = pixelRect.Y; blockY < pixelRect.Y + pixelRect.Height; blockY += MosaicBlockPixels)
        {
            for (var blockX = pixelRect.X; blockX < pixelRect.X + pixelRect.Width; blockX += MosaicBlockPixels)
            {
                var blockRight = Math.Min(blockX + MosaicBlockPixels, pixelRect.X + pixelRect.Width);
                var blockBottom = Math.Min(blockY + MosaicBlockPixels, pixelRect.Y + pixelRect.Height);
                AverageBlock(pixels, stride, blockX, blockY, blockRight, blockBottom, out var b, out var g, out var r, out var a);
                FillBlock(pixels, stride, blockX, blockY, blockRight, blockBottom, b, g, r, a);
            }
        }

        _capturedBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        CaptureImage.Source = _capturedBitmap;
        PushAction(ScreenshotEditAction.Mosaic(beforePixels, pixels.ToArray(), width, height, stride));
    }

    private static void AverageBlock(byte[] pixels, int stride, int left, int top, int right, int bottom, out byte b, out byte g, out byte r, out byte a)
    {
        long totalB = 0;
        long totalG = 0;
        long totalR = 0;
        long totalA = 0;
        var count = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var index = y * stride + x * 4;
                totalB += pixels[index];
                totalG += pixels[index + 1];
                totalR += pixels[index + 2];
                totalA += pixels[index + 3];
                count++;
            }
        }

        b = (byte)(totalB / count);
        g = (byte)(totalG / count);
        r = (byte)(totalR / count);
        a = (byte)(totalA / count);
    }

    private static void FillBlock(byte[] pixels, int stride, int left, int top, int right, int bottom, byte b, byte g, byte r, byte a)
    {
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var index = y * stride + x * 4;
                pixels[index] = b;
                pixels[index + 1] = g;
                pixels[index + 2] = r;
                pixels[index + 3] = a;
            }
        }
    }

    private Int32Rect ToPixelRect(Rect localRect)
    {
        if (_capturedBitmap is null)
        {
            return Int32Rect.Empty;
        }

        localRect.Intersect(new Rect(0, 0, _selection.Width, _selection.Height));
        var x = Math.Clamp((int)Math.Floor(localRect.Left / Math.Max(_selection.Width, 1) * _capturedBitmap.PixelWidth), 0, _capturedBitmap.PixelWidth - 1);
        var y = Math.Clamp((int)Math.Floor(localRect.Top / Math.Max(_selection.Height, 1) * _capturedBitmap.PixelHeight), 0, _capturedBitmap.PixelHeight - 1);
        var right = Math.Clamp((int)Math.Ceiling(localRect.Right / Math.Max(_selection.Width, 1) * _capturedBitmap.PixelWidth), x + 1, _capturedBitmap.PixelWidth);
        var bottom = Math.Clamp((int)Math.Ceiling(localRect.Bottom / Math.Max(_selection.Height, 1) * _capturedBitmap.PixelHeight), y + 1, _capturedBitmap.PixelHeight);
        return new Int32Rect(x, y, right - x, bottom - y);
    }

    private WpfPoint ToLocalPoint(WpfPoint rootPoint)
    {
        return new WpfPoint(rootPoint.X - _selection.Left, rootPoint.Y - _selection.Top);
    }

    private WpfPoint ClampToSelection(WpfPoint point)
    {
        return new WpfPoint(
            Math.Clamp(point.X, 0, Math.Max(0, _selection.Width)),
            Math.Clamp(point.Y, 0, Math.Max(0, _selection.Height)));
    }

    private SolidColorBrush CreateMarkerBrush()
    {
        return new SolidColorBrush(_markerColor);
    }

    private System.Windows.Shapes.Shape CreateShapeElement()
    {
        System.Windows.Shapes.Shape shape = _shapeKind switch
        {
            ScreenshotShapeKind.Ellipse => new Ellipse(),
            ScreenshotShapeKind.Star => new Polygon(),
            _ => new System.Windows.Shapes.Rectangle()
        };

        shape.Stroke = CreateMarkerBrush();
        shape.StrokeThickness = _markerThickness;
        shape.Fill = WpfBrushes.Transparent;
        shape.StrokeLineJoin = PenLineJoin.Round;
        return shape;
    }

    private static void UpdateShapeElement(System.Windows.Shapes.Shape shape, Rect rect)
    {
        var width = Math.Max(0, rect.Width);
        var height = Math.Max(0, rect.Height);
        shape.Width = width;
        shape.Height = height;
        CanvasSetLeftTop(shape, rect.Left, rect.Top);

        if (shape is Polygon polygon)
        {
            polygon.Points = CreateStarPoints(width, height);
        }
    }

    private static PointCollection CreateStarPoints(double width, double height)
    {
        var points = new PointCollection();
        var centerX = width / 2d;
        var centerY = height / 2d;
        var outerX = width / 2d;
        var outerY = height / 2d;
        var innerX = outerX * 0.45d;
        var innerY = outerY * 0.45d;

        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2d + i * Math.PI / 5d;
            var radiusX = i % 2 == 0 ? outerX : innerX;
            var radiusY = i % 2 == 0 ? outerY : innerY;
            points.Add(new WpfPoint(centerX + Math.Cos(angle) * radiusX, centerY + Math.Sin(angle) * radiusY));
        }

        return points;
    }

    private void UpdateArrowHead(WpfPoint start, WpfPoint end)
    {
        if (_activeArrowHead is null)
        {
            return;
        }

        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        const double length = 16;
        const double spread = Math.PI / 7;
        var p1 = new WpfPoint(
            end.X - length * Math.Cos(angle - spread),
            end.Y - length * Math.Sin(angle - spread));
        var p2 = new WpfPoint(
            end.X - length * Math.Cos(angle + spread),
            end.Y - length * Math.Sin(angle + spread));
        _activeArrowHead.Points = new PointCollection { end, p1, p2 };
    }

    private async Task SelectFullScreenAsync()
    {
        if (_isCompletingSelection)
        {
            return;
        }

        _isSelecting = false;
        ReleaseMouseCapture();
        _start = new WpfPoint(0, 0);
        _selection = new Rect(0, 0, ActualWidth, ActualHeight);
        SelectionBorder.Width = _selection.Width;
        SelectionBorder.Height = _selection.Height;
        CanvasSetLeftTop(SelectionBorder, 0, 0);
        SelectionBorder.Visibility = Visibility.Visible;
        UpdateDimGeometry(_selection);
        UpdateInfoPanel();
        await CompleteSelectionAsync();
    }

    private static bool IsSelectAllHotkey(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        return key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
    }

    private static Int32Rect CreateVirtualScreenRect()
    {
        return new Int32Rect(
            (int)Math.Round(SystemParameters.VirtualScreenLeft),
            (int)Math.Round(SystemParameters.VirtualScreenTop),
            Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenWidth)),
            Math.Max(1, (int)Math.Round(SystemParameters.VirtualScreenHeight)));
    }

    private WriteableBitmap CropFrozenBitmap(Int32Rect screenRect)
    {
        if (_frozenBitmap is null)
        {
            return CaptureScreenRegion(screenRect);
        }

        var left = Math.Clamp(screenRect.X - _frozenScreenRect.X, 0, Math.Max(0, _frozenBitmap.PixelWidth - 1));
        var top = Math.Clamp(screenRect.Y - _frozenScreenRect.Y, 0, Math.Max(0, _frozenBitmap.PixelHeight - 1));
        var right = Math.Clamp(screenRect.X + screenRect.Width - _frozenScreenRect.X, left + 1, _frozenBitmap.PixelWidth);
        var bottom = Math.Clamp(screenRect.Y + screenRect.Height - _frozenScreenRect.Y, top + 1, _frozenBitmap.PixelHeight);
        var crop = new CroppedBitmap(_frozenBitmap, new Int32Rect(left, top, right - left, bottom - top));
        var converted = new FormatConvertedBitmap(crop, PixelFormats.Bgra32, null, 0);
        return new WriteableBitmap(converted);
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

    private static WriteableBitmap CaptureScreenRegion(Int32Rect rect)
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
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            return new WriteableBitmap(converted);
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    private static string CreateDefaultScreenshotPath()
    {
        var directory = IOPath.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            "GamePulseMonitor");
        Directory.CreateDirectory(directory);
        return IOPath.Combine(directory, $"Screenshot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
    }

    private static void SaveScreenshot(BitmapSource bitmap, string path)
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

    private static void CanvasSetLeftTop(UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);
}

internal sealed record ScreenshotResult(ScreenshotResultKind Kind, string? FilePath, int Width, int Height)
{
    public static ScreenshotResult Copied(int width, int height) => new(ScreenshotResultKind.Copied, null, width, height);

    public static ScreenshotResult Saved(string filePath, int width, int height) => new(ScreenshotResultKind.Saved, filePath, width, height);

    public static ScreenshotResult Pinned(int width, int height) => new(ScreenshotResultKind.Pinned, null, width, height);
}

internal enum ScreenshotResultKind
{
    Copied,
    Saved,
    Pinned
}

internal enum ScreenshotTool
{
    None,
    Pen,
    Arrow,
    Shape,
    Text,
    Mosaic,
    Eraser
}

internal enum ScreenshotShapeKind
{
    Rectangle,
    Ellipse,
    Star
}

internal sealed record ScreenshotEditAction(
    ScreenshotEditActionKind Kind,
    IReadOnlyList<UIElement> Elements,
    byte[] BeforePixels,
    byte[] AfterPixels,
    int Width,
    int Height,
    int Stride)
{
    public static ScreenshotEditAction AddVisual(IReadOnlyList<UIElement> elements)
    {
        return new ScreenshotEditAction(ScreenshotEditActionKind.AddVisual, elements, [], [], 0, 0, 0);
    }

    public static ScreenshotEditAction RemoveVisual(IReadOnlyList<UIElement> elements)
    {
        return new ScreenshotEditAction(ScreenshotEditActionKind.RemoveVisual, elements, [], [], 0, 0, 0);
    }

    public static ScreenshotEditAction Mosaic(byte[] beforePixels, byte[] afterPixels, int width, int height, int stride)
    {
        return new ScreenshotEditAction(ScreenshotEditActionKind.Mosaic, [], beforePixels, afterPixels, width, height, stride);
    }
}

internal enum ScreenshotEditActionKind
{
    AddVisual,
    RemoveVisual,
    Mosaic
}
