using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using GamePulseMonitor.Logging;
using GamePulseMonitor.Settings;
using MediaColor = System.Windows.Media.Color;

namespace GamePulseMonitor;

public partial class HistoryWindow : Window
{
    private const double ChartLeft = 58;
    private const double ChartTop = 20;
    private const double ChartRight = 18;
    private const double ChartBottom = 34;

    private readonly MetricsHistoryStore _store;
    private readonly AppSettingsStore _settingsStore;
    private readonly DispatcherTimer _liveTimer;
    private readonly List<RenderedChartNode> _renderedNodes = [];
    private IReadOnlyList<MetricsHistoryPoint> _points = Array.Empty<MetricsHistoryPoint>();
    private HistoryViewMode _mode = HistoryViewMode.Realtime;
    private TimeSpan _realtimeVisibleWindow = TimeSpan.FromMinutes(3);
    private bool _isLoading = true;
    private DateTime _lastObservedHistoryWriteUtc = DateTime.MinValue;
    private DateTimeOffset _lastRealtimeLatestTimestamp = DateTimeOffset.MinValue;

    internal HistoryWindow(MetricsHistoryStore store, AppSettingsStore settingsStore)
    {
        _store = store;
        _settingsStore = settingsStore;
        InitializeComponent();
        _liveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _liveTimer.Tick += OnLiveTimerTick;
        ApplyLanguage(_settingsStore.Current.Language);
        LoadDatesAndDefaultRange();
        _liveTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _liveTimer.Stop();
        _liveTimer.Tick -= OnLiveTimerTick;
        base.OnClosed(e);
    }

    private void ApplyLanguage(AppLanguage language)
    {
        Title = TextCatalog.Get(language, "History");
        TitleText.Text = TextCatalog.Get(language, "History");
        SubtitleText.Text = TextCatalog.Get(language, "HistorySubtitle");
        ModeLabel.Text = TextCatalog.Get(language, "HistoryMode");
        RealtimeModeItem.Content = TextCatalog.Get(language, "RealtimeMode");
        HistoryModeItem.Content = TextCatalog.Get(language, "HistoryModeName");
        DateLabel.Text = TextCatalog.Get(language, "HistoryDate");
        ProcessLabel.Text = TextCatalog.Get(language, "HistoryProcess");
        FromLabel.Text = TextCatalog.Get(language, "HistoryFrom");
        ToLabel.Text = TextCatalog.Get(language, "HistoryTo");
        RefreshButton.Content = TextCatalog.Get(language, "Refresh");
        LatestTwoHoursButton.Content = _mode == HistoryViewMode.Realtime
            ? TextCatalog.Get(language, "LatestThreeMinutes")
            : TextCatalog.Get(language, "LatestTwoHours");
        ApplyButton.Content = TextCatalog.Get(language, "Apply");
        EmptyText.Text = TextCatalog.Get(language, "NoHistoryData");

        FpsCheckBox.Content = TextCatalog.Get(language, "Fps");
        AverageFpsCheckBox.Content = TextCatalog.Get(language, "AverageFps");
        LowFpsCheckBox.Content = TextCatalog.Get(language, "OnePercentLow");
        CpuCheckBox.Content = TextCatalog.Get(language, "Cpu");
        GpuCheckBox.Content = TextCatalog.Get(language, "Gpu");
        VramCheckBox.Content = TextCatalog.Get(language, "Vram");
        MemoryCheckBox.Content = TextCatalog.Get(language, "Memory");
        FrameTimeCheckBox.Content = TextCatalog.Get(language, "FrameTime");
    }

    private void LoadDatesAndDefaultRange()
    {
        _isLoading = true;
        try
        {
            _mode = HistoryViewMode.Realtime;
            ModeComboBox.SelectedItem = RealtimeModeItem;
            ApplyModeUi();

            var latest = _store.GetLatestPoint();
            if (latest is null)
            {
                LoadDateItems(null);
                ProcessComboBox.Items.Clear();
                SetStatus(TextCatalog.Get(_settingsStore.Current.Language, "NoHistoryData"));
                _points = Array.Empty<MetricsHistoryPoint>();
                RenderChart();
                return;
            }

            var latestDate = DateOnly.FromDateTime(latest.Timestamp.LocalDateTime);
            LoadDateItems(latestDate);
            LoadProcesses(latestDate, latest.ProcessKey);
            SetRange(latestDate, latest.Timestamp - _realtimeVisibleWindow, latest.Timestamp);
        }
        finally
        {
            _isLoading = false;
        }

        LoadAndRender(force: true);
    }

    private void LoadDateItems(DateOnly? preferredDate)
    {
        DateComboBox.Items.Clear();
        foreach (var availableDate in _store.GetAvailableDates())
        {
            DateComboBox.Items.Add(new ComboBoxItem
            {
                Content = availableDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Tag = availableDate
            });
        }

        if (preferredDate is { } date)
        {
            SelectDate(date);
        }
        else if (DateComboBox.Items.Count > 0)
        {
            DateComboBox.SelectedIndex = 0;
        }
    }

    private void LoadProcesses(DateOnly date, string? preferredProcessKey)
    {
        ProcessComboBox.Items.Clear();
        ProcessComboBox.Items.Add(new ComboBoxItem
        {
            Content = TextCatalog.Get(_settingsStore.Current.Language, "AllProcesses"),
            Tag = MetricsHistoryStore.AllProcesses
        });

        foreach (var process in _store.GetProcesses(date))
        {
            ProcessComboBox.Items.Add(new ComboBoxItem
            {
                Content = process.DisplayName,
                Tag = process.ProcessKey
            });
        }

        var preferred = ProcessComboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals((string?)item.Tag, preferredProcessKey, StringComparison.OrdinalIgnoreCase));
        ProcessComboBox.SelectedItem = preferred ?? ProcessComboBox.Items[0];
    }

    private void SelectDate(DateOnly date)
    {
        foreach (ComboBoxItem item in DateComboBox.Items)
        {
            if (item.Tag is DateOnly itemDate && itemDate == date)
            {
                DateComboBox.SelectedItem = item;
                return;
            }
        }

        if (DateComboBox.Items.Count > 0)
        {
            DateComboBox.SelectedIndex = 0;
        }
    }

    private void OnDateChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || GetSelectedDate() is not { } date)
        {
            return;
        }

        _isLoading = true;
        try
        {
            var latest = GetLatestPoint(date, MetricsHistoryStore.AllProcesses);
            LoadProcesses(date, latest?.ProcessKey);
            if (_mode == HistoryViewMode.History && latest is not null)
            {
                SetRange(date, latest.Timestamp.AddHours(-2), latest.Timestamp);
            }
        }
        finally
        {
            _isLoading = false;
        }

        if (_mode == HistoryViewMode.Realtime)
        {
            SetLatestRealtimeRangeForSelection();
        }

        LoadAndRender(force: true);
    }

    private void OnProcessChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (_mode == HistoryViewMode.Realtime)
        {
            SetLatestRealtimeRangeForSelection();
        }

        LoadAndRender(force: true);
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        RefreshFilterListsPreservingSelection();
        if (_mode == HistoryViewMode.Realtime)
        {
            SetLatestRealtimeRangeForSelection();
        }

        LoadAndRender(force: true);
    }

    private void OnLatestTwoHoursClick(object sender, RoutedEventArgs e)
    {
        if (_mode == HistoryViewMode.Realtime)
        {
            _realtimeVisibleWindow = TimeSpan.FromMinutes(3);
            SetLatestRealtimeRangeForSelection();
        }
        else
        {
            SetLatestTwoHourRangeForSelection();
        }

        LoadAndRender(force: true);
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        if (_mode != HistoryViewMode.History)
        {
            return;
        }

        LoadAndRender(force: true);
    }

    private void OnMetricSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!_isLoading)
        {
            RenderChart();
        }
    }

    private void OnChartSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderChart();
    }

    private void OnChartPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 ||
            GetSelectedDate() is not { } date)
        {
            return;
        }

        e.Handled = true;
        var factor = e.Delta > 0 ? 0.8 : 1.25;

        if (_mode == HistoryViewMode.Realtime)
        {
            _realtimeVisibleWindow = ClampTimeSpan(
                TimeSpan.FromTicks((long)(_realtimeVisibleWindow.Ticks * factor)),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(3));
            SetLatestRealtimeRangeForSelection();
            LoadAndRender(force: true);
            return;
        }

        var from = ParseTimeText(date, FromTextBox.Text, TimeSpan.Zero);
        var to = ParseTimeText(date, ToTextBox.Text, new TimeSpan(23, 59, 59));
        if (to <= from)
        {
            to = from.AddMinutes(1);
        }

        var currentSpan = to - from;
        var nextSpan = ClampTimeSpan(
            TimeSpan.FromTicks((long)(currentSpan.Ticks * factor)),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromDays(1));

        var plotWidth = Math.Max(1, ChartCanvas.ActualWidth - ChartLeft - ChartRight);
        var x = e.GetPosition(ChartCanvas).X;
        var ratio = Math.Clamp((x - ChartLeft) / plotWidth, 0, 1);
        var center = from + TimeSpan.FromTicks((long)(currentSpan.Ticks * ratio));
        var nextFrom = center - TimeSpan.FromTicks((long)(nextSpan.Ticks * ratio));
        var nextTo = nextFrom + nextSpan;
        var dayStart = CreateDateTimeOffset(date, TimeSpan.Zero);
        var dayEnd = CreateDateTimeOffset(date, new TimeSpan(23, 59, 59));

        if (nextFrom < dayStart)
        {
            nextTo += dayStart - nextFrom;
            nextFrom = dayStart;
        }

        if (nextTo > dayEnd)
        {
            nextFrom -= nextTo - dayEnd;
            nextTo = dayEnd;
        }

        if (nextFrom < dayStart)
        {
            nextFrom = dayStart;
        }

        SetRange(date, nextFrom, nextTo);
        LoadAndRender(force: true);
    }

    private void OnChartMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_renderedNodes.Count == 0)
        {
            HideChartHover();
            return;
        }

        var position = e.GetPosition(ChartCanvas);
        const double maxDistance = 12;
        var maxDistanceSquared = maxDistance * maxDistance;
        RenderedChartNode? nearest = null;
        var nearestDistanceSquared = double.MaxValue;
        foreach (var node in _renderedNodes)
        {
            var dx = node.X - position.X;
            if (Math.Abs(dx) > maxDistance)
            {
                continue;
            }

            var dy = node.Y - position.Y;
            if (Math.Abs(dy) > maxDistance)
            {
                continue;
            }

            var distanceSquared = dx * dx + dy * dy;
            if (distanceSquared <= maxDistanceSquared && distanceSquared < nearestDistanceSquared)
            {
                nearest = node;
                nearestDistanceSquared = distanceSquared;
            }
        }

        if (nearest is not { } hoverNode)
        {
            HideChartHover();
            return;
        }

        ShowChartHover(hoverNode, position);
    }

    private void OnChartMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HideChartHover();
    }

    private void OnModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || ModeComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        _mode = string.Equals((string?)item.Tag, "History", StringComparison.OrdinalIgnoreCase)
            ? HistoryViewMode.History
            : HistoryViewMode.Realtime;
        ApplyModeUi();

        if (_mode == HistoryViewMode.Realtime)
        {
            SetLatestRealtimeRangeForSelection();
        }
        else
        {
            SetLatestTwoHourRangeForSelection();
        }

        LoadAndRender(force: true);
    }

    private void OnLiveTimerTick(object? sender, EventArgs e)
    {
        if (!IsVisible ||
            _isLoading ||
            _mode != HistoryViewMode.Realtime)
        {
            return;
        }

        var latest = GetLatestPointAcrossDates(GetSelectedProcessKey());
        if (latest is null || latest.Timestamp <= _lastRealtimeLatestTimestamp)
        {
            return;
        }

        _lastRealtimeLatestTimestamp = latest.Timestamp;
        SetLatestRealtimeRangeForSelection(latest);
        LoadAndRender(force: true);
    }

    private void ApplyModeUi()
    {
        var isHistory = _mode == HistoryViewMode.History;
        DateComboBox.IsEnabled = isHistory;
        FromTextBox.IsEnabled = isHistory;
        ToTextBox.IsEnabled = isHistory;
        ApplyButton.IsEnabled = isHistory;
        LatestTwoHoursButton.Content = isHistory
            ? TextCatalog.Get(_settingsStore.Current.Language, "LatestTwoHours")
            : TextCatalog.Get(_settingsStore.Current.Language, "LatestThreeMinutes");
    }

    private void RefreshFilterListsPreservingSelection()
    {
        var selectedDate = GetSelectedDate();
        var selectedProcessKey = GetSelectedProcessKey();

        _isLoading = true;
        try
        {
            LoadDateItems(selectedDate);
            if (GetSelectedDate() is { } date)
            {
                LoadProcesses(date, selectedProcessKey);
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void SetLatestTwoHourRangeForSelection()
    {
        if (GetSelectedDate() is not { } date)
        {
            return;
        }

        var latest = GetLatestPoint(date, GetSelectedProcessKey());
        if (latest is not null)
        {
            SetRange(date, latest.Timestamp.AddHours(-2), latest.Timestamp);
        }
    }

    private void SetLatestRealtimeRangeForSelection()
    {
        var latest = GetLatestPointAcrossDates(GetSelectedProcessKey());
        if (latest is null)
        {
            return;
        }

        SetLatestRealtimeRangeForSelection(latest);
    }

    private void SetLatestRealtimeRangeForSelection(MetricsHistoryPoint latest)
    {
        _lastRealtimeLatestTimestamp = latest.Timestamp;
        var date = DateOnly.FromDateTime(latest.Timestamp.LocalDateTime);
        _isLoading = true;
        try
        {
            LoadDateItems(date);
            LoadProcesses(date, GetSelectedProcessKey());
        }
        finally
        {
            _isLoading = false;
        }

        SetRange(date, latest.Timestamp - _realtimeVisibleWindow, latest.Timestamp);
    }

    private MetricsHistoryPoint? GetLatestPointAcrossDates(string processKey)
    {
        if (processKey == MetricsHistoryStore.AllProcesses)
        {
            return _store.GetLatestPoint();
        }

        foreach (var date in _store.GetAvailableDates())
        {
            var from = CreateDateTimeOffset(date, TimeSpan.Zero);
            var to = CreateDateTimeOffset(date, new TimeSpan(23, 59, 59));
            var latest = _store.Query(date, processKey, from, to)
                .OrderByDescending(point => point.Timestamp)
                .FirstOrDefault();
            if (latest is not null)
            {
                return latest;
            }
        }

        return null;
    }

    private MetricsHistoryPoint? GetLatestPoint(DateOnly date, string processKey)
    {
        var from = CreateDateTimeOffset(date, TimeSpan.Zero);
        var to = CreateDateTimeOffset(date, new TimeSpan(23, 59, 59));
        return _store.Query(date, processKey, from, to)
            .OrderByDescending(point => point.Timestamp)
            .FirstOrDefault();
    }

    private void LoadAndRender(bool force = false)
    {
        if (GetSelectedDate() is not { } date)
        {
            _points = Array.Empty<MetricsHistoryPoint>();
            RenderChart();
            return;
        }

        if (!force && !HasSelectedHistoryFileChanged())
        {
            RenderChart();
            return;
        }

        var from = ParseTimeText(date, FromTextBox.Text, TimeSpan.Zero);
        var to = ParseTimeText(date, ToTextBox.Text, new TimeSpan(23, 59, 59));
        _points = _store.Query(date, GetSelectedProcessKey(), from, to);
        UpdateObservedHistoryFileTimestamp(date);
        SetStatus($"{_points.Count} {TextCatalog.Get(_settingsStore.Current.Language, "HistoryPoints")}");
        RenderChart();
    }

    private void UpdateObservedHistoryFileTimestamp(DateOnly date)
    {
        var path = System.IO.Path.Combine(_store.DirectoryPath, $"{date:yyyy-MM-dd}.csv");
        if (File.Exists(path))
        {
            _lastObservedHistoryWriteUtc = File.GetLastWriteTimeUtc(path);
        }
    }

    private bool HasSelectedHistoryFileChanged()
    {
        if (GetSelectedDate() is not { } date)
        {
            return false;
        }

        var path = System.IO.Path.Combine(_store.DirectoryPath, $"{date:yyyy-MM-dd}.csv");
        if (!File.Exists(path))
        {
            return false;
        }

        var writeUtc = File.GetLastWriteTimeUtc(path);
        if (writeUtc <= _lastObservedHistoryWriteUtc)
        {
            return false;
        }

        _lastObservedHistoryWriteUtc = writeUtc;
        return true;
    }

    private void RenderChart()
    {
        if (ChartCanvas is null || EmptyText is null)
        {
            return;
        }

        ChartCanvas.Children.Clear();
        LegendPanel.Children.Clear();
        _renderedNodes.Clear();
        HideChartHover();

        var width = ChartCanvas.ActualWidth;
        var height = ChartCanvas.ActualHeight;
        if (width < 120 || height < 120)
        {
            return;
        }

        var selectedMetrics = GetMetrics()
            .Where(metric => metric.IsSelected())
            .ToArray();

        if (_points.Count == 0 || selectedMetrics.Length == 0 || GetSelectedDate() is not { } date)
        {
            EmptyText.Visibility = Visibility.Visible;
            return;
        }

        var from = ParseTimeText(date, FromTextBox.Text, TimeSpan.Zero);
        var to = ParseTimeText(date, ToTextBox.Text, new TimeSpan(23, 59, 59));
        if (to <= from)
        {
            to = from.AddMinutes(1);
        }

        var plotWidth = Math.Max(1, width - ChartLeft - ChartRight);
        var plotHeight = Math.Max(1, height - ChartTop - ChartBottom);

        var yAxisScale = CreateYAxisScale(selectedMetrics);
        DrawGrid(ChartLeft, ChartTop, plotWidth, plotHeight, from, to, yAxisScale);

        var drewAny = false;
        foreach (var metric in selectedMetrics)
        {
            var values = _points
                .Select(point => new { Point = point, Value = metric.Read(point) })
                .Where(item => item.Value is not null)
                .Select(item => new ChartValue(item.Point.Timestamp, item.Value!.Value))
                .ToArray();

            if (values.Length == 0)
            {
                continue;
            }

            DrawSeries(metric, values, ChartLeft, ChartTop, plotWidth, plotHeight, from, to);
            drewAny = true;
        }

        EmptyText.Visibility = drewAny ? Visibility.Collapsed : Visibility.Visible;
    }

    private ChartScale CreateYAxisScale(IReadOnlyList<MetricDefinition> selectedMetrics)
    {
        var metric = selectedMetrics.FirstOrDefault(metric => string.Equals(metric.Key, "fps", StringComparison.OrdinalIgnoreCase))
            ?? selectedMetrics[0];
        var values = _points
            .Select(metric.Read)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();

        if (values.Length == 0)
        {
            return new ChartScale(0, 1, metric.Label, metric.Unit);
        }

        var (min, max) = ExpandRange(values.Min(), values.Max());
        return new ChartScale(min, max, metric.Label, metric.Unit);
    }

    private void DrawGrid(
        double left,
        double top,
        double plotWidth,
        double plotHeight,
        DateTimeOffset from,
        DateTimeOffset to,
        ChartScale yAxisScale)
    {
        var gridBrush = new SolidColorBrush(MediaColor.FromRgb(0x26, 0x34, 0x4A));
        var labelBrush = new SolidColorBrush(MediaColor.FromRgb(0x8E, 0xA0, 0xBA));
        var axisTitle = new TextBlock
        {
            Text = yAxisScale.Label,
            Foreground = labelBrush,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Width = left - 8,
            TextAlignment = TextAlignment.Right
        };
        Canvas.SetLeft(axisTitle, 0);
        Canvas.SetTop(axisTitle, Math.Max(0, top - 17));
        ChartCanvas.Children.Add(axisTitle);

        for (var i = 0; i <= 4; i++)
        {
            var y = top + plotHeight * i / 4d;
            ChartCanvas.Children.Add(new Line
            {
                X1 = left,
                X2 = left + plotWidth,
                Y1 = y,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            });

            var value = yAxisScale.Max - (yAxisScale.Max - yAxisScale.Min) * i / 4d;
            var label = new TextBlock
            {
                Text = FormatAxisValue(value, yAxisScale.Unit),
                Foreground = labelBrush,
                FontSize = 11,
                Width = left - 8,
                TextAlignment = TextAlignment.Right
            };
            Canvas.SetLeft(label, 0);
            Canvas.SetTop(label, Math.Clamp(y - 8, top - 8, top + plotHeight - 8));
            ChartCanvas.Children.Add(label);
        }

        for (var i = 0; i <= 4; i++)
        {
            var x = left + plotWidth * i / 4d;
            ChartCanvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = top,
                Y2 = top + plotHeight,
                Stroke = gridBrush,
                StrokeThickness = 1
            });

            var tick = from.AddTicks((long)((to - from).Ticks * i / 4d));
            var label = new TextBlock
            {
                Text = tick.ToString("HH:mm", CultureInfo.CurrentCulture),
                Foreground = labelBrush,
                FontSize = 11
            };
            Canvas.SetLeft(label, Math.Clamp(x - 18, 0, ChartCanvas.ActualWidth - 40));
            Canvas.SetTop(label, top + plotHeight + 8);
            ChartCanvas.Children.Add(label);
        }
    }

    private void DrawSeries(MetricDefinition metric, IReadOnlyList<ChartValue> values, double left, double top, double plotWidth, double plotHeight, DateTimeOffset from, DateTimeOffset to)
    {
        var min = values.Min(value => value.Value);
        var max = values.Max(value => value.Value);
        (min, max) = ExpandRange(min, max);

        var timeSpan = Math.Max(1, (to - from).TotalSeconds);
        var valueSpan = Math.Max(0.001, max - min);
        var points = new PointCollection();
        foreach (var value in values)
        {
            var x = left + Math.Clamp((value.Timestamp - from).TotalSeconds / timeSpan, 0, 1) * plotWidth;
            var y = top + plotHeight - ((value.Value - min) / valueSpan) * plotHeight;
            points.Add(new System.Windows.Point(x, y));
            _renderedNodes.Add(new RenderedChartNode(
                metric.Label,
                metric.Unit,
                value.Timestamp,
                value.Value,
                metric.Color,
                x,
                y));
        }

        var brush = new SolidColorBrush(metric.Color);
        ChartCanvas.Children.Add(new Polyline
        {
            Points = points,
            Stroke = brush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        });

        if (points.Count == 1)
        {
            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                Fill = brush
            };
            Canvas.SetLeft(dot, points[0].X - 2.5);
            Canvas.SetTop(dot, points[0].Y - 2.5);
            ChartCanvas.Children.Add(dot);
        }

        var latest = values[^1].Value;
        AddLegend(metric, latest, min, max);
    }

    private void AddLegend(MetricDefinition metric, double latest, double min, double max)
    {
        var panel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            Margin = new Thickness(0, 0, 16, 6)
        };
        panel.Children.Add(new Border
        {
            Width = 10,
            Height = 10,
            Margin = new Thickness(0, 2, 6, 0),
            Background = new SolidColorBrush(metric.Color),
            CornerRadius = new CornerRadius(2)
        });
        panel.Children.Add(new TextBlock
        {
            Foreground = new SolidColorBrush(MediaColor.FromRgb(0xE8, 0xEE, 0xF8)),
            FontSize = 12,
            Text = $"{metric.Label} {latest:0.#}{metric.Unit} ({min:0.#}-{max:0.#})"
        });
        LegendPanel.Children.Add(panel);
    }

    private void ShowChartHover(RenderedChartNode node, System.Windows.Point mousePosition)
    {
        if (HoverMarker is null || HoverTip is null || HoverTipText is null)
        {
            return;
        }

        var brush = new SolidColorBrush(node.Color);
        HoverMarker.Stroke = brush;
        HoverMarker.Visibility = Visibility.Visible;
        HoverMarker.Margin = new Thickness(node.X - 4, node.Y - 4, 0, 0);

        HoverTipText.Text = string.Create(
            CultureInfo.CurrentCulture,
            $"{node.Timestamp.LocalDateTime:HH:mm:ss}\n{node.Label}: {FormatHoverValue(node.Value, node.Unit)}");
        HoverTip.BorderBrush = brush;
        HoverTip.Visibility = Visibility.Visible;
        HoverTip.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));

        var tipWidth = double.IsNaN(HoverTip.DesiredSize.Width) || HoverTip.DesiredSize.Width <= 0
            ? 132
            : HoverTip.DesiredSize.Width;
        var tipHeight = double.IsNaN(HoverTip.DesiredSize.Height) || HoverTip.DesiredSize.Height <= 0
            ? 44
            : HoverTip.DesiredSize.Height;
        var x = mousePosition.X + 14;
        var y = mousePosition.Y + 14;
        if (x + tipWidth + 4 > ChartCanvas.ActualWidth)
        {
            x = mousePosition.X - tipWidth - 14;
        }

        if (y + tipHeight + 4 > ChartCanvas.ActualHeight)
        {
            y = mousePosition.Y - tipHeight - 14;
        }

        HoverTip.Margin = new Thickness(
            Math.Clamp(x, 4, Math.Max(4, ChartCanvas.ActualWidth - tipWidth - 4)),
            Math.Clamp(y, 4, Math.Max(4, ChartCanvas.ActualHeight - tipHeight - 4)),
            0,
            0);
    }

    private void HideChartHover()
    {
        if (HoverMarker is not null)
        {
            HoverMarker.Visibility = Visibility.Collapsed;
        }

        if (HoverTip is not null)
        {
            HoverTip.Visibility = Visibility.Collapsed;
        }
    }

    private MetricDefinition[] GetMetrics()
    {
        var language = _settingsStore.Current.Language;
        return
        [
            new MetricDefinition("fps", TextCatalog.Get(language, "Fps"), "", MediaColor.FromRgb(0x7D, 0xE3, 0xFF), () => FpsCheckBox.IsChecked == true, point => point.Fps),
            new MetricDefinition("average_fps", TextCatalog.Get(language, "AverageFps"), "", MediaColor.FromRgb(0xFF, 0xD1, 0x66), () => AverageFpsCheckBox.IsChecked == true, point => point.AverageFps),
            new MetricDefinition("p1_low_fps", TextCatalog.Get(language, "OnePercentLow"), "", MediaColor.FromRgb(0xFF, 0x6B, 0x6B), () => LowFpsCheckBox.IsChecked == true, point => point.P1LowFps),
            new MetricDefinition("cpu", TextCatalog.Get(language, "Cpu"), "%", MediaColor.FromRgb(0x58, 0xD6, 0x8D), () => CpuCheckBox.IsChecked == true, point => point.CpuTotalPercent),
            new MetricDefinition("gpu", TextCatalog.Get(language, "Gpu"), "%", MediaColor.FromRgb(0xC9, 0xA7, 0xFF), () => GpuCheckBox.IsChecked == true, point => point.GpuTotalPercent),
            new MetricDefinition("vram", TextCatalog.Get(language, "Vram"), " MB", MediaColor.FromRgb(0xAA, 0xB3, 0xC5), () => VramCheckBox.IsChecked == true, point => point.VramDedicatedMb),
            new MetricDefinition("memory", TextCatalog.Get(language, "Memory"), " MB", MediaColor.FromRgb(0xF5, 0xF7, 0xFB), () => MemoryCheckBox.IsChecked == true, point => point.MemoryUsedMb),
            new MetricDefinition("frame_time", TextCatalog.Get(language, "FrameTime"), " ms", MediaColor.FromRgb(0xFF, 0x9F, 0x43), () => FrameTimeCheckBox.IsChecked == true, point => point.FrameTimeMs)
        ];
    }

    private DateOnly? GetSelectedDate()
    {
        return DateComboBox.SelectedItem is ComboBoxItem { Tag: DateOnly date } ? date : null;
    }

    private string GetSelectedProcessKey()
    {
        return ProcessComboBox.SelectedItem is ComboBoxItem { Tag: string key }
            ? key
            : MetricsHistoryStore.AllProcesses;
    }

    private void SetRange(DateOnly date, DateTimeOffset from, DateTimeOffset to)
    {
        var startOfDay = CreateDateTimeOffset(date, TimeSpan.Zero);
        var endOfDay = CreateDateTimeOffset(date, new TimeSpan(23, 59, 59));
        if (from < startOfDay)
        {
            from = startOfDay;
        }

        if (to > endOfDay)
        {
            to = endOfDay;
        }

        FromTextBox.Text = from.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        ToTextBox.Text = to.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private static DateTimeOffset ParseTimeText(DateOnly date, string text, TimeSpan fallback)
    {
        if (!TimeSpan.TryParse(text, CultureInfo.CurrentCulture, out var time) &&
            !TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out time))
        {
            time = fallback;
        }

        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }
        else if (time >= TimeSpan.FromDays(1))
        {
            time = new TimeSpan(23, 59, 59);
        }

        return CreateDateTimeOffset(date, time);
    }

    private static DateTimeOffset CreateDateTimeOffset(DateOnly date, TimeSpan time)
    {
        var local = date.ToDateTime(TimeOnly.MinValue).Add(time);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    private static (double Min, double Max) ExpandRange(double min, double max)
    {
        if (double.IsNaN(min) || double.IsInfinity(min) ||
            double.IsNaN(max) || double.IsInfinity(max))
        {
            return (0, 1);
        }

        if (Math.Abs(max - min) < 0.001)
        {
            min -= 1;
            max += 1;
        }

        return (min, max);
    }

    private static string FormatAxisValue(double value, string unit)
    {
        var format = Math.Abs(value) >= 100 || string.Equals(unit.Trim(), "MB", StringComparison.OrdinalIgnoreCase)
            ? "0"
            : "0.#";
        return value.ToString(format, CultureInfo.CurrentCulture) + unit;
    }

    private static string FormatHoverValue(double value, string unit)
    {
        var format = Math.Abs(value) >= 1000
            ? "0.#"
            : "0.###";
        return value.ToString(format, CultureInfo.CurrentCulture) + unit;
    }

    private static TimeSpan ClampTimeSpan(TimeSpan value, TimeSpan min, TimeSpan max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private sealed record MetricDefinition(string Key, string Label, string Unit, MediaColor Color, Func<bool> IsSelected, Func<MetricsHistoryPoint, double?> Read);

    private sealed record ChartScale(double Min, double Max, string Label, string Unit);

    private readonly record struct ChartValue(DateTimeOffset Timestamp, double Value);

    private readonly record struct RenderedChartNode(
        string Label,
        string Unit,
        DateTimeOffset Timestamp,
        double Value,
        MediaColor Color,
        double X,
        double Y);

    private enum HistoryViewMode
    {
        Realtime,
        History
    }
}
