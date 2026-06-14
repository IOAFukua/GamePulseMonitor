using System.IO;
using GamePulseMonitor.Logging;

namespace GamePulseMonitor.Monitoring;

internal sealed class MetricsService : IDisposable
{
    private readonly MonitorOptions _options;
    private readonly FpsTracker _fpsTracker = new();
    private readonly PresentMonFrameSource _frameSource;
    private readonly ForegroundProcessTracker _processTracker;
    private readonly HardwareSampler _hardwareSampler = new();
    private readonly MetricsCsvLogger _logger;
    private readonly MetricsHistoryStore _historyStore = new();
    private readonly System.Threading.Timer _timer;
    private string? _lastUserFeedback;
    private DateTime _feedbackUntilUtc;
    private int _isSampling;
    private bool _disposed;

    public MetricsService(MonitorOptions options)
    {
        _options = options;
        _frameSource = new PresentMonFrameSource(options.ProcessName, options.LogDirectory, _fpsTracker.RecordFrame);
        _processTracker = new ForegroundProcessTracker(options.ProcessName);
        _logger = new MetricsCsvLogger(options.LogDirectory);
        _timer = new System.Threading.Timer(Sample, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event EventHandler<MetricsSnapshot>? SnapshotReady;
    public MetricsHistoryStore HistoryStore => _historyStore;

    public void Start()
    {
        _frameSource.Start();
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    public string ToggleBenchmark()
    {
        var target = _processTracker.GetTarget();
        var result = _fpsTracker.ToggleBenchmark(target, DateTime.UtcNow);
        SetFeedback(result.Message);
        return result.Message;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Dispose();
        _frameSource.Dispose();
        _hardwareSampler.Dispose();
        _logger.Dispose();
        _historyStore.Dispose();
    }

    private void Sample(object? state)
    {
        if (Interlocked.Exchange(ref _isSampling, 1) == 1)
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.Now;
            var target = _processTracker.GetTarget();
            var fpsStats = _fpsTracker.ReadStats(target?.ProcessId, DateTime.UtcNow);
            var hardware = _hardwareSampler.Read(target);
            var status = BuildStatus(target, fpsStats);

            var snapshot = new MetricsSnapshot(
                now,
                target,
                fpsStats.Fps,
                fpsStats.FrameTimeMs,
                fpsStats.P1LowFps,
                fpsStats.AverageFps,
                fpsStats.BenchmarkStatus,
                hardware.CpuTotalPercent,
                hardware.ProcessCpuPercent,
                hardware.GpuTotalPercent,
                hardware.VramDedicatedMb,
                hardware.VramSharedMb,
                hardware.ProcessVramDedicatedMb,
                hardware.ProcessVramSharedMb,
                hardware.MemoryUsedMb,
                hardware.MemoryPercent,
                status);

            _logger.Write(snapshot);
            _historyStore.Write(snapshot);
            SnapshotReady?.Invoke(this, snapshot);
        }
        finally
        {
            Volatile.Write(ref _isSampling, 0);
        }
    }

    private string BuildStatus(ProcessTarget? target, FpsStats fpsStats)
    {
        if (target is null)
        {
            var noTargetFeedback = ReadFeedback();
            var noTargetFeedbackPart = string.IsNullOrWhiteSpace(noTargetFeedback) ? "" : $"{noTargetFeedback}; ";
            return $"{noTargetFeedbackPart}{_frameSource.Status}; waiting for foreground game; {fpsStats.BenchmarkStatus}";
        }

        var source = string.IsNullOrWhiteSpace(_options.ProcessName) ? "auto" : "locked";
        var fpsState = fpsStats.Fps is null ? "no present events yet" : "recording";
        var feedback = ReadFeedback();
        var feedbackPart = string.IsNullOrWhiteSpace(feedback) ? "" : $"{feedback}; ";
        return $"{feedbackPart}{_frameSource.Status}; {source}; {fpsState}; {fpsStats.BenchmarkStatus}; log {Path.GetFileName(_logger.FilePath)}";
    }

    private void SetFeedback(string message)
    {
        _lastUserFeedback = message;
        _feedbackUntilUtc = DateTime.UtcNow + TimeSpan.FromSeconds(3);
    }

    private string? ReadFeedback()
    {
        return DateTime.UtcNow <= _feedbackUntilUtc ? _lastUserFeedback : null;
    }
}

internal sealed record MetricsSnapshot(
    DateTimeOffset Timestamp,
    ProcessTarget? Target,
    double? Fps,
    double? FrameTimeMs,
    double? P1LowFps,
    double? AverageFps,
    string BenchmarkStatus,
    double CpuTotalPercent,
    double? ProcessCpuPercent,
    double GpuTotalPercent,
    double VramDedicatedMb,
    double VramSharedMb,
    double? ProcessVramDedicatedMb,
    double? ProcessVramSharedMb,
    double MemoryUsedMb,
    double MemoryPercent,
    string Status);

internal sealed record ProcessTarget(int ProcessId, string Name);
