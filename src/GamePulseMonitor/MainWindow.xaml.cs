using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using GamePulseMonitor.Interop;
using GamePulseMonitor.Logging;
using GamePulseMonitor.Monitoring;
using GamePulseMonitor.Settings;

namespace GamePulseMonitor;

public partial class MainWindow : Window
{
    private const double VerticalDesignWidth = 312;
    private const double HorizontalMetricHeight = 44;
    private const double HorizontalGap = 16;
    private const double HorizontalHeaderGap = 6;
    private const double OverlayPadding = 28;
    private const double MinHorizontalFieldWidth = 48;
    private const double MaxHorizontalFieldWidth = 260;
    private const int MinScalePercent = 60;
    private const int MaxScalePercent = 180;

    private readonly MetricsService _metricsService;
    private readonly MonitorOptions _options;
    private readonly AppSettingsStore _settingsStore;
    private readonly TrayController _trayController;
    private readonly DispatcherTimer _altDragTimer;
    private HwndSource? _source;
    private SettingsWindow? _settingsWindow;
    private HistoryWindow? _historyWindow;
    private AppSettings _settings;
    private nint _hwnd;
    private bool _isOverlayVisible = true;
    private bool _isClickThrough;
    private bool _isDragging;
    private bool _benchmarkChordWasPressed;
    private bool _suspendHotkeys;
    private bool _isScreenshotActive;
    private int _backgroundOpacityPercent;
    private double _activeDesignWidth = VerticalDesignWidth;
    private double _activeDesignHeight = 270;
    private System.Windows.Point _dragStartScreen;
    private double _dragStartLeft;
    private double _dragStartTop;

    internal MainWindow(MetricsService metricsService, MonitorOptions options, AppSettingsStore settingsStore)
    {
        _metricsService = metricsService;
        _options = options;
        _settingsStore = settingsStore;
        _settings = settingsStore.Current.Clone();

        InitializeComponent();
        RestoreOverlayPlacement(options);

        _metricsService.SnapshotReady += OnSnapshotReady;
        _settingsStore.SettingsChanged += OnSettingsChanged;
        _trayController = new TrayController(ToggleVisibility, OpenSettings, OpenHistory, StartScreenshotSelection, Close);
        _altDragTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _altDragTimer.Tick += OnAltDragTimerTick;
        ApplyLanguage(_settings.Language);
        ApplyDisplaySettings(_settings.Display);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _hwnd = new WindowInteropHelper(this).Handle;
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);

        _trayController.Initialize(_hwnd);
        NativeMethods.MakeOverlayWindow(_hwnd, _options.ClickThrough);
        _isClickThrough = _options.ClickThrough;
        RegisterHotkeys();
        _altDragTimer.Start();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (CanStartDrag())
        {
            _isDragging = true;
            _dragStartScreen = PointToScreen(e.GetPosition(this));
            _dragStartLeft = Left;
            _dragStartTop = Top;
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        Left = _dragStartLeft + current.X - _dragStartScreen.X;
        Top = _dragStartTop + current.Y - _dragStartScreen.Y;
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();
            UpdateClickThroughForAltState();
            SaveOverlayPlacement();
            e.Handled = true;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _altDragTimer.Stop();
        UnregisterHotkeys(hwnd);
        _source?.RemoveHook(WndProc);
        _settingsStore.SettingsChanged -= OnSettingsChanged;
        SaveOverlayPlacement();
        _settingsWindow?.Close();
        _historyWindow?.Close();
        _trayController.Dispose();
        _metricsService.SnapshotReady -= OnSnapshotReady;
        base.OnClosing(e);
    }

    private void OnSnapshotReady(object? sender, MetricsSnapshot snapshot)
    {
        Dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(MetricsSnapshot snapshot)
    {
        TargetText.Text = snapshot.Target is null
            ? TextCatalog.Get(_settings.Language, "AutoTarget")
            : $"{snapshot.Target.Name} ({snapshot.Target.ProcessId})";

        StatusText.Text = snapshot.Status;
        FpsText.Text = Format(snapshot.Fps, "0");
        AverageText.Text = Format(snapshot.AverageFps, "0");
        LowText.Text = Format(snapshot.P1LowFps, "0");
        CpuText.Text = snapshot.ProcessCpuPercent is null
            ? $"{snapshot.CpuTotalPercent:0}%"
            : $"{snapshot.CpuTotalPercent:0}% / {snapshot.ProcessCpuPercent:0}%";
        GpuText.Text = $"{snapshot.GpuTotalPercent:0}%";

        var processVram = snapshot.ProcessVramDedicatedMb is null
            ? ""
            : $" / P {snapshot.ProcessVramDedicatedMb.Value:0} MB";
        VramText.Text = $"{snapshot.VramDedicatedMb:0} MB{processVram}";

        MemoryText.Text = $"{snapshot.MemoryUsedMb / 1024:0.0} GB / {snapshot.MemoryPercent:0}%";
        FrameTimeText.Text = $"{Format(snapshot.FrameTimeMs, "0.0")} ms";
        var footerParts = new List<string>();
        if (_settings.Display.ShowMemory)
        {
            footerParts.Add($"{GetMemoryLabel(_settings.Language)} {MemoryText.Text}");
        }

        if (_settings.Display.ShowFrameTime)
        {
            footerParts.Add($"{GetFrameTimeLabel(_settings.Language)} {FrameTimeText.Text}");
        }

        FooterText.Text = string.Join("  ", footerParts);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _settings = settings.Clone();
            ApplyLanguage(_settings.Language);
            ApplyDisplaySettings(_settings.Display);
            if (_hwnd != nint.Zero && !_suspendHotkeys)
            {
                RegisterHotkeys();
            }
        });
    }

    private void RestoreOverlayPlacement(MonitorOptions options)
    {
        var placement = _settings.Display.Placement;
        if (placement.HasPlacement)
        {
            Left = placement.Left;
            Top = placement.Top;
            return;
        }

        Left = options.Left;
        Top = options.Top;
    }

    private void SaveOverlayPlacement()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top))
        {
            return;
        }

        var left = Left;
        var top = Top;
        _settings.Display.Placement.HasPlacement = true;
        _settings.Display.Placement.Left = left;
        _settings.Display.Placement.Top = top;
        _settingsStore.Update(settings =>
        {
            settings.Display.Placement.HasPlacement = true;
            settings.Display.Placement.Left = left;
            settings.Display.Placement.Top = top;
        });
    }

    private void ApplyLanguage(AppLanguage language)
    {
        FpsLabel.Text = TextCatalog.Get(language, "Fps");
        AverageLabel.Text = TextCatalog.Get(language, "AverageFps");
        LowLabel.Text = TextCatalog.Get(language, "OnePercentLow");
        CpuLabel.Text = TextCatalog.Get(language, "Cpu");
        GpuLabel.Text = TextCatalog.Get(language, "Gpu");
        VramLabel.Text = TextCatalog.Get(language, "Vram");
        MemoryLabel.Text = GetMemoryLabel(language);
        FrameTimeLabel.Text = GetFrameTimeLabel(language);
        _trayController.ApplyText(
            TextCatalog.Get(language, "ShowHideOverlay"),
            TextCatalog.Get(language, "SettingsMenu"),
            TextCatalog.Get(language, "HistoryMenu"),
            TextCatalog.Get(language, "ScreenshotMenu"),
            TextCatalog.Get(language, "Exit"));
        MinimizeButton.ToolTip = TextCatalog.Get(language, "MinimizeToTray");

        TargetText.ToolTip = TextCatalog.Get(language, "TooltipTarget");
        StatusText.ToolTip = TextCatalog.Get(language, "TooltipStatus");
        HeaderRow.ToolTip = TextCatalog.Get(language, "TooltipTarget");
        FpsRow.ToolTip = TextCatalog.Get(language, "TooltipFps");
        AverageRow.ToolTip = TextCatalog.Get(language, "TooltipAverageFps");
        LowRow.ToolTip = TextCatalog.Get(language, "TooltipOnePercentLow");
        CpuRow.ToolTip = TextCatalog.Get(language, "TooltipCpu");
        GpuRow.ToolTip = TextCatalog.Get(language, "TooltipGpu");
        VramRow.ToolTip = TextCatalog.Get(language, "TooltipVram");
        MemoryRow.ToolTip = TextCatalog.Get(language, "TooltipFooter");
        FrameTimeRow.ToolTip = TextCatalog.Get(language, "TooltipFooter");
        FooterText.ToolTip = TextCatalog.Get(language, "TooltipFooter");
        SetTooltipTiming(HeaderRow);
        SetTooltipTiming(TargetText);
        SetTooltipTiming(StatusText);
        SetTooltipTiming(FpsRow);
        SetTooltipTiming(AverageRow);
        SetTooltipTiming(LowRow);
        SetTooltipTiming(CpuRow);
        SetTooltipTiming(GpuRow);
        SetTooltipTiming(VramRow);
        SetTooltipTiming(MemoryRow);
        SetTooltipTiming(FrameTimeRow);
        SetTooltipTiming(FooterText);
    }

    private void ApplyDisplaySettings(DisplaySettings display)
    {
        TargetText.Visibility = ToVisibility(display.ShowTarget);
        StatusText.Visibility = ToVisibility(display.ShowStatus);
        HeaderRow.Visibility = ToVisibility(display.ShowTarget || display.ShowStatus);
        FpsRow.Visibility = ToVisibility(display.ShowFps);
        AverageRow.Visibility = ToVisibility(display.ShowAverageFps);
        LowRow.Visibility = ToVisibility(display.ShowOnePercentLow);
        CpuRow.Visibility = ToVisibility(display.ShowCpu);
        GpuRow.Visibility = ToVisibility(display.ShowGpu);
        VramRow.Visibility = ToVisibility(display.ShowVram);
        FooterText.Visibility = ToVisibility((display.ShowMemory || display.ShowFrameTime) && display.Layout == OverlayLayout.Vertical);
        MemoryRow.Visibility = ToVisibility(display.ShowMemory && display.Layout == OverlayLayout.Horizontal);
        FrameTimeRow.Visibility = ToVisibility(display.ShowFrameTime && display.Layout == OverlayLayout.Horizontal);
        ApplyOverlayBackground(display.BackgroundOpacityPercent);
        ApplyOverlayForeground(display);
        ApplyOverlayLayout(display);
    }

    private void ApplyOverlayBackground(int opacityPercent)
    {
        _backgroundOpacityPercent = Math.Clamp(opacityPercent, 0, 100);
        UpdateOverlayBackground(NativeMethods.IsAltPressed());
    }

    private void UpdateOverlayBackground(bool isAltPressed)
    {
        var alpha = (byte)Math.Round(_backgroundOpacityPercent / 100d * 255);
        var backgroundAlpha = alpha == 0 && isAltPressed ? (byte)1 : alpha;
        OverlayBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(backgroundAlpha, 0x0B, 0x11, 0x1D));
        OverlayBorder.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x42, 0x51, 0x6A));
    }

    private void ApplyOverlayForeground(DisplaySettings display)
    {
        var color = ParseHexColor(display.FontColorHex);
        var valueBrush = new SolidColorBrush(color);
        var subtleBrush = new SolidColorBrush(WithAlpha(color, 0x86));
        var targetSettings = display.GetFieldSettings(OverlayFieldIds.Target);
        var statusSettings = display.GetFieldSettings(OverlayFieldIds.Status);

        TargetText.Foreground = new SolidColorBrush(ParseHexColor(targetSettings.LabelColorHex));
        LiveText.Foreground = new SolidColorBrush(ParseHexColor(targetSettings.ValueColorHex));
        MinimizeButton.Foreground = valueBrush;
        MinimizeButton.Background = System.Windows.Media.Brushes.Transparent;
        MinimizeButton.BorderBrush = System.Windows.Media.Brushes.Transparent;
        StatusText.Foreground = new SolidColorBrush(ParseHexColor(statusSettings.ValueColorHex));
        FooterText.Foreground = subtleBrush;

        foreach (var metric in GetMetricRows())
        {
            var fieldSettings = display.GetFieldSettings(metric.Id);
            metric.Label.Foreground = new SolidColorBrush(ParseHexColor(fieldSettings.LabelColorHex));
            metric.Value.Foreground = new SolidColorBrush(ParseHexColor(fieldSettings.ValueColorHex));
        }
    }

    private void ApplyOverlayLayout(DisplaySettings display)
    {
        if (display.Layout == OverlayLayout.Horizontal)
        {
            ApplyHorizontalLayout(display);
        }
        else
        {
            ApplyVerticalLayout(display);
        }

        ApplyOverlayScale(display.OverlayScalePercent);
    }

    private void ApplyVerticalLayout(DisplaySettings display)
    {
        OverlayGrid.RowDefinitions.Clear();
        OverlayGrid.ColumnDefinitions.Clear();
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderRow.Visibility == Visibility.Visible && HasVisibleMetrics() ? 10 : 0) });
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FooterText.Visibility == Visibility.Visible && (HeaderRow.Visibility == Visibility.Visible || HasVisibleMetrics()) ? 8 : 0) });
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetRow(HeaderRow, 0);
        Grid.SetColumn(HeaderRow, 0);
        Grid.SetColumnSpan(HeaderRow, 1);
        Grid.SetRow(MetricPanel, 2);
        Grid.SetColumn(MetricPanel, 0);
        Grid.SetColumnSpan(MetricPanel, 1);
        Grid.SetRow(FooterText, 4);
        Grid.SetColumn(FooterText, 0);
        Grid.SetColumnSpan(FooterText, 1);

        HeaderRow.Width = double.NaN;
        HeaderRow.Margin = new Thickness(0, 0, 28, 0);
        MetricPanel.Orientation = System.Windows.Controls.Orientation.Vertical;
        MetricPanel.Margin = new Thickness(0);
        FooterText.Width = double.NaN;
        FooterText.Margin = new Thickness(0);
        FooterText.TextAlignment = TextAlignment.Left;
        FooterText.VerticalAlignment = VerticalAlignment.Center;

        foreach (var metric in GetMetricRows())
        {
            ApplyMetricRowLayout(metric, display, horizontal: false);
        }

        _activeDesignWidth = VerticalDesignWidth;
        _activeDesignHeight = CalculateVerticalDesignHeight(display);
        OverlayBorder.Width = _activeDesignWidth;
        OverlayBorder.Height = _activeDesignHeight;
    }

    private void ApplyHorizontalLayout(DisplaySettings display)
    {
        OverlayGrid.RowDefinitions.Clear();
        OverlayGrid.ColumnDefinitions.Clear();
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HeaderRow.Visibility == Visibility.Visible && (HasVisibleMetrics() || FooterText.Visibility == Visibility.Visible) ? HorizontalHeaderGap : 0) });
        OverlayGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        OverlayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetRow(HeaderRow, 0);
        Grid.SetColumn(HeaderRow, 0);
        Grid.SetColumnSpan(HeaderRow, 1);
        Grid.SetRow(MetricPanel, 2);
        Grid.SetColumn(MetricPanel, 0);
        Grid.SetColumnSpan(MetricPanel, 1);
        Grid.SetRow(FooterText, 2);
        Grid.SetColumn(FooterText, 0);
        Grid.SetColumnSpan(FooterText, 1);

        HeaderRow.Width = double.NaN;
        HeaderRow.Margin = HeaderRow.Visibility == Visibility.Visible && (HasVisibleMetrics() || FooterText.Visibility == Visibility.Visible)
            ? new Thickness(0, 0, 28, 0)
            : new Thickness(0);
        HeaderRow.VerticalAlignment = VerticalAlignment.Center;

        MetricPanel.Orientation = System.Windows.Controls.Orientation.Horizontal;
        MetricPanel.Margin = new Thickness(0);
        MetricPanel.VerticalAlignment = VerticalAlignment.Center;
        FooterText.Width = double.NaN;
        FooterText.Margin = new Thickness(0);
        FooterText.TextAlignment = TextAlignment.Left;
        FooterText.VerticalAlignment = VerticalAlignment.Center;

        var visibleMetrics = GetMetricRows()
            .Where(metric => metric.Row.Visibility == Visibility.Visible)
            .ToArray();
        for (var i = 0; i < visibleMetrics.Length; i++)
        {
            var metric = visibleMetrics[i];
            ApplyMetricRowLayout(metric, display, horizontal: true);
            if (i == visibleMetrics.Length - 1)
            {
                metric.Row.Margin = new Thickness(0);
            }
        }

        _activeDesignWidth = CalculateHorizontalDesignWidth(display);
        _activeDesignHeight = CalculateHorizontalDesignHeight(display);
        OverlayBorder.Width = _activeDesignWidth;
        OverlayBorder.Height = _activeDesignHeight;
        UpdateFieldBoundaryVisibility(NativeMethods.IsAltPressed());
    }

    private void ApplyMetricRowLayout(MetricRow metric, DisplaySettings display, bool horizontal)
    {
        var row = metric.Row;
        var label = metric.Label;
        var value = metric.Value;
        row.RowDefinitions.Clear();
        row.ColumnDefinitions.Clear();
        row.Margin = new Thickness(0);

        if (horizontal)
        {
            row.Width = display.GetFieldSettings(metric.Id).HorizontalWidth;
            row.Height = HorizontalMetricHeight;
            row.Margin = row.Visibility == Visibility.Visible ? new Thickness(0, 0, HorizontalGap, 0) : new Thickness(0);
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetRow(value, 1);
            Grid.SetColumn(value, 0);
            label.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            label.TextAlignment = TextAlignment.Center;
            value.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            value.TextAlignment = TextAlignment.Center;
            value.Margin = new Thickness(0, 2, 0, 0);
            EnsureFieldResizeThumb(row, metric.Id);
        }
        else
        {
            row.Width = double.NaN;
            row.Height = 30;
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(98) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetRow(value, 0);
            Grid.SetColumn(value, 1);
            label.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            label.TextAlignment = TextAlignment.Left;
            value.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            value.TextAlignment = TextAlignment.Right;
            value.Margin = new Thickness(0);
            if (FindFieldResizeThumb(row) is { } thumb)
            {
                thumb.Visibility = Visibility.Collapsed;
            }
        }
    }

    private Thumb EnsureFieldResizeThumb(Grid row, string fieldId)
    {
        if (FindFieldResizeThumb(row) is { } existing)
        {
            existing.Tag = fieldId;
            return existing;
        }

        var thumb = new Thumb
        {
            Tag = fieldId,
            Width = 12,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0, 0, -6, 0),
            Cursor = System.Windows.Input.Cursors.SizeWE,
            Visibility = Visibility.Collapsed,
            Template = CreateFieldResizeThumbTemplate()
        };
        thumb.DragDelta += OnFieldWidthDragDelta;
        thumb.DragCompleted += OnFieldWidthDragCompleted;
        Grid.SetRow(thumb, 0);
        Grid.SetRowSpan(thumb, 2);
        System.Windows.Controls.Panel.SetZIndex(thumb, 40);
        row.Children.Add(thumb);
        return thumb;
    }

    private static Thumb? FindFieldResizeThumb(Grid row)
    {
        return row.Children.OfType<Thumb>().FirstOrDefault(thumb => thumb.Tag is string);
    }

    private static ControlTemplate CreateFieldResizeThumbTemplate()
    {
        var template = new ControlTemplate(typeof(Thumb));
        var root = new FrameworkElementFactory(typeof(Grid));
        root.SetValue(System.Windows.Controls.Panel.BackgroundProperty, System.Windows.Media.Brushes.Transparent);

        var line = new FrameworkElementFactory(typeof(Border));
        line.SetValue(FrameworkElement.WidthProperty, 1.0);
        line.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        line.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Stretch);
        line.SetValue(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xAA, 0xF5, 0xF7, 0xFB)));
        root.AppendChild(line);

        template.VisualTree = root;
        return template;
    }

    private void UpdateFieldBoundaryVisibility(bool isAltPressed)
    {
        var visibleMetrics = GetMetricRows()
            .Where(metric => metric.Row.Visibility == Visibility.Visible)
            .ToArray();
        var showBoundaries = isAltPressed && _settings.Display.Layout == OverlayLayout.Horizontal;

        foreach (var metric in GetMetricRows())
        {
            if (FindFieldResizeThumb(metric.Row) is { } thumb)
            {
                thumb.Visibility = Visibility.Collapsed;
            }
        }

        if (!showBoundaries)
        {
            return;
        }

        for (var i = 0; i < visibleMetrics.Length - 1; i++)
        {
            if (FindFieldResizeThumb(visibleMetrics[i].Row) is { } thumb)
            {
                thumb.Visibility = Visibility.Visible;
            }
        }
    }

    private void OnFieldWidthDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (!NativeMethods.IsAltPressed() ||
            sender is not Thumb { Tag: string fieldId })
        {
            return;
        }

        var fieldSettings = _settings.Display.GetFieldSettings(fieldId);
        var scale = Math.Max(Width / Math.Max(_activeDesignWidth, 1), 0.1);
        var delta = e.HorizontalChange / scale;
        var nextWidth = Math.Clamp(fieldSettings.HorizontalWidth + delta, MinHorizontalFieldWidth, MaxHorizontalFieldWidth);
        if (Math.Abs(nextWidth - fieldSettings.HorizontalWidth) < 0.1)
        {
            return;
        }

        fieldSettings.HorizontalWidth = nextWidth;
        ApplyOverlayLayout(_settings.Display);
        e.Handled = true;
    }

    private void OnFieldWidthDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is not Thumb { Tag: string fieldId })
        {
            return;
        }

        var width = _settings.Display.GetFieldSettings(fieldId).HorizontalWidth;
        _settingsStore.Update(settings => settings.Display.GetFieldSettings(fieldId).HorizontalWidth = width);
        e.Handled = true;
    }

    private void ApplyOverlayScale(int scalePercent)
    {
        scalePercent = Math.Clamp(scalePercent, MinScalePercent, MaxScalePercent);
        var scale = scalePercent / 100d;
        Width = _activeDesignWidth * scale;
        Height = _activeDesignHeight * scale;
    }

    private double CalculateVerticalDesignHeight(DisplaySettings display)
    {
        var metricCount = GetMetricRows().Count(metric => metric.Row.Visibility == Visibility.Visible);
        var hasHeader = display.ShowTarget || display.ShowStatus;
        var hasFooter = display.ShowMemory || display.ShowFrameTime;
        var height = OverlayPadding;

        if (hasHeader)
        {
            height += display.ShowTarget && display.ShowStatus ? 36 : 20;
        }

        if (hasHeader && metricCount > 0)
        {
            height += 10;
        }

        height += metricCount * 30;

        if (hasFooter)
        {
            if (hasHeader || metricCount > 0)
            {
                height += 8;
            }

            height += 14;
        }

        return Math.Max(64, height);
    }

    private double CalculateHorizontalDesignWidth(DisplaySettings display)
    {
        var metricsWidth = CalculateHorizontalMetricsWidth();
        var width = OverlayPadding + metricsWidth;

        if (display.ShowTarget || display.ShowStatus)
        {
            width = Math.Max(width, 420);
        }

        return Math.Max(220, width);
    }

    private double CalculateHorizontalDesignHeight(DisplaySettings display)
    {
        var hasHeader = display.ShowTarget || display.ShowStatus;
        var hasBody = HasVisibleMetrics();
        var height = OverlayPadding;

        if (hasHeader)
        {
            height += display.ShowTarget && display.ShowStatus ? 36 : 20;
        }

        if (hasHeader && hasBody)
        {
            height += HorizontalHeaderGap;
        }

        if (hasBody)
        {
            height += HorizontalMetricHeight;
        }

        return Math.Max(64, height);
    }

    private double CalculateHorizontalMetricsWidth()
    {
        var visibleMetrics = GetMetricRows()
            .Where(metric => metric.Row.Visibility == Visibility.Visible)
            .ToArray();

        return visibleMetrics.Sum(metric => _settings.Display.GetFieldSettings(metric.Id).HorizontalWidth) +
               Math.Max(0, visibleMetrics.Length - 1) * HorizontalGap;
    }

    private bool HasVisibleMetrics()
    {
        return FpsRow.Visibility == Visibility.Visible ||
               AverageRow.Visibility == Visibility.Visible ||
               LowRow.Visibility == Visibility.Visible ||
               CpuRow.Visibility == Visibility.Visible ||
               GpuRow.Visibility == Visibility.Visible ||
               VramRow.Visibility == Visibility.Visible ||
               MemoryRow.Visibility == Visibility.Visible ||
               FrameTimeRow.Visibility == Visibility.Visible;
    }

    private MetricRow[] GetMetricRows()
    {
        return
        [
            new MetricRow(OverlayFieldIds.Fps, FpsRow, FpsLabel, FpsText),
            new MetricRow(OverlayFieldIds.AverageFps, AverageRow, AverageLabel, AverageText),
            new MetricRow(OverlayFieldIds.OnePercentLow, LowRow, LowLabel, LowText),
            new MetricRow(OverlayFieldIds.Cpu, CpuRow, CpuLabel, CpuText),
            new MetricRow(OverlayFieldIds.Gpu, GpuRow, GpuLabel, GpuText),
            new MetricRow(OverlayFieldIds.Vram, VramRow, VramLabel, VramText),
            new MetricRow(OverlayFieldIds.Memory, MemoryRow, MemoryLabel, MemoryText),
            new MetricRow(OverlayFieldIds.FrameTime, FrameTimeRow, FrameTimeLabel, FrameTimeText)
        ];
    }

    private void RegisterHotkeys()
    {
        if (_hwnd == nint.Zero)
        {
            return;
        }

        UnregisterHotkeys(_hwnd);
        var failed = new List<string>();
        RegisterHotkey(NativeMethods.HotkeyToggleOverlay, _settings.Hotkeys.ToggleOverlay, TextCatalog.Get(_settings.Language, "ToggleOverlayHotkey"), failed);
        RegisterHotkey(NativeMethods.HotkeyExit, _settings.Hotkeys.Exit, TextCatalog.Get(_settings.Language, "ExitHotkey"), failed);
        RegisterHotkey(NativeMethods.HotkeyToggleBenchmark, _settings.Hotkeys.ToggleBenchmark, TextCatalog.Get(_settings.Language, "BenchmarkHotkey"), failed);
        RegisterHotkey(NativeMethods.HotkeyScreenshot, _settings.Hotkeys.Screenshot, TextCatalog.Get(_settings.Language, "ScreenshotHotkey"), failed);

        if (failed.Count > 0)
        {
            StatusText.Text = $"{TextCatalog.Get(_settings.Language, "HotkeyRegisterFailed")}: {string.Join(", ", failed)}";
        }
    }

    private void RegisterHotkey(int id, HotkeySetting hotkey, string name, ICollection<string> failed)
    {
        if (hotkey.Key == 0 ||
            !NativeMethods.RegisterHotKey(_hwnd, id, (uint)hotkey.Modifiers, hotkey.Key))
        {
            failed.Add($"{name} {HotkeyFormatter.Format(hotkey)}");
        }
    }

    private static void UnregisterHotkeys(nint hwnd)
    {
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyToggleOverlay);
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyExit);
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyToggleBenchmark);
        NativeMethods.UnregisterHotKey(hwnd, NativeMethods.HotkeyScreenshot);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_trayController.HandleMessage(msg, wParam, lParam))
        {
            handled = true;
            return nint.Zero;
        }

        if (msg == NativeMethods.WmHotkey)
        {
            var id = wParam.ToInt32();
            if (id == NativeMethods.HotkeyToggleOverlay)
            {
                ToggleVisibility();
                handled = true;
            }
            else if (id == NativeMethods.HotkeyExit)
            {
                Close();
                handled = true;
            }
            else if (id == NativeMethods.HotkeyToggleBenchmark)
            {
                _benchmarkChordWasPressed = true;
                ToggleBenchmarkFromInput();
                handled = true;
            }
            else if (id == NativeMethods.HotkeyScreenshot)
            {
                StartScreenshotSelection();
                handled = true;
            }
        }

        return nint.Zero;
    }

    private void ToggleVisibility()
    {
        Dispatcher.InvokeAsync(() =>
        {
            _isOverlayVisible = !_isOverlayVisible;
            Visibility = _isOverlayVisible ? Visibility.Visible : Visibility.Hidden;
        });
    }

    private void OpenSettings()
    {
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (_settingsWindow is { IsVisible: true })
                {
                    BringSettingsToFront(_settingsWindow);
                    return;
                }

                _suspendHotkeys = true;
                UnregisterHotkeys(_hwnd);
                _settingsWindow = new SettingsWindow(_settingsStore);
                _settingsWindow.Closed += (_, _) =>
                {
                    _settingsWindow = null;
                    _suspendHotkeys = false;
                    RegisterHotkeys();
                };
                _settingsWindow.Show();
                BringSettingsToFront(_settingsWindow);
            }
            catch (Exception ex)
            {
                _suspendHotkeys = false;
                _settingsWindow = null;
                RegisterHotkeys();
                StatusText.Text = $"Settings error: {ex.Message}";
                System.Windows.MessageBox.Show(
                    ex.Message,
                    TextCatalog.Get(_settings.Language, "Settings"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        });
    }

    private void OpenHistory()
    {
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
                if (_historyWindow is { IsVisible: true })
                {
                    BringSettingsToFront(_historyWindow);
                    return;
                }

                _historyWindow = new HistoryWindow(_metricsService.HistoryStore, _settingsStore);
                _historyWindow.Closed += (_, _) => _historyWindow = null;
                _historyWindow.Show();
                BringSettingsToFront(_historyWindow);
            }
            catch (Exception ex)
            {
                _historyWindow = null;
                StatusText.Text = $"History error: {ex.Message}";
                System.Windows.MessageBox.Show(
                    ex.Message,
                    TextCatalog.Get(_settings.Language, "History"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        });
    }

    private void StartScreenshotSelection()
    {
        Dispatcher.InvokeAsync(async () =>
        {
            if (_isScreenshotActive)
            {
                return;
            }

            _isScreenshotActive = true;
            var restoreOverlay = _isOverlayVisible && Visibility == Visibility.Visible;
            try
            {
                if (restoreOverlay)
                {
                    Visibility = Visibility.Hidden;
                }

                var window = new ScreenshotSelectionWindow(_settings.Language, _settings.Hotkeys);
                var result = await window.CaptureAsync();
                if (result is null)
                {
                    StatusText.Text = TextCatalog.Get(_settings.Language, "ScreenshotCancelled");
                    return;
                }

                StatusText.Text = result.Kind switch
                {
                    ScreenshotResultKind.Copied => TextCatalog.Get(_settings.Language, "ScreenshotCopied"),
                    ScreenshotResultKind.Pinned => TextCatalog.Get(_settings.Language, "ScreenshotPinned"),
                    ScreenshotResultKind.Saved => string.Format(
                        TextCatalog.Get(_settings.Language, "ScreenshotSaved"),
                        result.FilePath),
                    _ => TextCatalog.Get(_settings.Language, "ScreenshotMenu")
                };
            }
            catch (Exception ex)
            {
                StatusText.Text = $"{TextCatalog.Get(_settings.Language, "ScreenshotFailed")}: {ex.Message}";
                System.Windows.MessageBox.Show(
                    ex.Message,
                    TextCatalog.Get(_settings.Language, "ScreenshotMenu"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                if (restoreOverlay)
                {
                    Visibility = Visibility.Visible;
                }

                _isScreenshotActive = false;
            }
        });
    }

    private static void BringSettingsToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    private void OnAltDragTimerTick(object? sender, EventArgs e)
    {
        UpdateClickThroughForAltState();
        UpdateResizeGripVisibility();
        UpdateBenchmarkHotkeyFallback();
    }

    private void UpdateResizeGripVisibility()
    {
        var isAltPressed = NativeMethods.IsAltPressed();
        ResizeThumb.Visibility = isAltPressed ? Visibility.Visible : Visibility.Collapsed;
        UpdateOverlayBackground(isAltPressed);
        UpdateFieldBoundaryVisibility(isAltPressed);
        MinimizeButton.Visibility = isAltPressed ? Visibility.Visible : Visibility.Hidden;
        MinimizeButton.IsHitTestVisible = isAltPressed;
        MinimizeButton.Opacity = isAltPressed ? 0.95 : 0;
    }

    private void UpdateClickThroughForAltState()
    {
        if (_hwnd == nint.Zero || !_options.ClickThrough)
        {
            return;
        }

        var shouldClickThrough = !NativeMethods.IsAltPressed() && !_isDragging;
        if (shouldClickThrough == _isClickThrough)
        {
            return;
        }

        NativeMethods.SetOverlayClickThrough(_hwnd, shouldClickThrough);
        _isClickThrough = shouldClickThrough;
        Cursor = shouldClickThrough ? System.Windows.Input.Cursors.Arrow : System.Windows.Input.Cursors.SizeAll;
    }

    private bool CanStartDrag()
    {
        return !_options.ClickThrough || NativeMethods.IsAltPressed();
    }

    private void UpdateBenchmarkHotkeyFallback()
    {
        if (_suspendHotkeys)
        {
            return;
        }

        var isPressed = HotkeyFormatter.IsPressed(_settings.Hotkeys.ToggleBenchmark);
        if (isPressed && !_benchmarkChordWasPressed)
        {
            _benchmarkChordWasPressed = true;
            ToggleBenchmarkFromInput();
        }
        else if (!isPressed)
        {
            _benchmarkChordWasPressed = false;
        }
    }

    private void ToggleBenchmarkFromInput()
    {
        var message = _metricsService.ToggleBenchmark();
        StatusText.Text = message;

        if (message.StartsWith(TextCatalog.Get(AppLanguage.English, "BenchStartedPrefix"), StringComparison.OrdinalIgnoreCase))
        {
            AverageText.Text = "--";
            LowText.Text = "--";
        }
    }

    private void OnMinimizePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!NativeMethods.IsAltPressed())
        {
            e.Handled = true;
        }
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        if (!NativeMethods.IsAltPressed())
        {
            return;
        }

        _isOverlayVisible = false;
        Visibility = Visibility.Hidden;
    }

    private void OnResizeThumbDragDelta(object sender, DragDeltaEventArgs e)
    {
        var widthScale = (Width + e.HorizontalChange) / _activeDesignWidth;
        var heightScale = (Height + e.VerticalChange) / _activeDesignHeight;
        var nextScale = (int)Math.Round(Math.Max(widthScale, heightScale) * 100);
        nextScale = Math.Clamp(nextScale, MinScalePercent, MaxScalePercent);

        _settings.Display.OverlayScalePercent = nextScale;
        ApplyOverlayScale(nextScale);
    }

    private void OnResizeThumbDragCompleted(object sender, DragCompletedEventArgs e)
    {
        var scalePercent = _settings.Display.OverlayScalePercent;
        _settingsStore.Update(settings => settings.Display.OverlayScalePercent = scalePercent);
    }

    private static string GetMemoryLabel(AppLanguage language) => language == AppLanguage.Chinese ? "\u5185\u5b58" : "RAM";

    private static string GetFrameTimeLabel(AppLanguage language) => language == AppLanguage.Chinese ? "\u5e27\u65f6" : "FT";

    private static string Format(double? value, string format) => value is null ? "--" : value.Value.ToString(format);

    private static Visibility ToVisibility(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;

    private static System.Windows.Media.Color ParseHexColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) ||
            hex.Length != 7 ||
            hex[0] != '#')
        {
            return System.Windows.Media.Color.FromRgb(0xF5, 0xF7, 0xFB);
        }

        try
        {
            return System.Windows.Media.Color.FromRgb(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16));
        }
        catch
        {
            return System.Windows.Media.Color.FromRgb(0xF5, 0xF7, 0xFB);
        }
    }

    private static System.Windows.Media.Color WithAlpha(System.Windows.Media.Color color, byte alpha)
    {
        return System.Windows.Media.Color.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static void SetTooltipTiming(DependencyObject element)
    {
        ToolTipService.SetInitialShowDelay(element, 0);
        ToolTipService.SetShowDuration(element, 30000);
    }

    private readonly record struct MetricRow(string Id, Grid Row, TextBlock Label, TextBlock Value);
}
