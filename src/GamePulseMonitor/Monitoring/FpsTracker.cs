using System.Diagnostics;

namespace GamePulseMonitor.Monitoring;

internal sealed class FpsTracker
{
    private static readonly TimeSpan HistoryWindow = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CurrentWindow = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan StaleWindow = TimeSpan.FromSeconds(3);
    private readonly Dictionary<int, ProcessFrameWindow> _windows = new();
    private readonly object _gate = new();
    private BenchmarkWindow? _benchmark;

    public void RecordPresent(int processId, DateTime timestampUtc)
    {
        RecordFrame(processId, timestampUtc, null);
    }

    public void RecordFrame(int processId, DateTime timestampUtc, double? frameTimeMs)
    {
        if (processId <= 0)
        {
            return;
        }

        var sample = new FrameSample(timestampUtc, NormalizeFrameTime(frameTimeMs));

        lock (_gate)
        {
            if (!_windows.TryGetValue(processId, out var window))
            {
                window = new ProcessFrameWindow();
                _windows[processId] = window;
            }

            window.Samples.Add(sample);
            window.LastSeenUtc = timestampUtc;
            Trim(window, timestampUtc);

            if (_benchmark is { IsActive: true } benchmark && benchmark.ProcessId == processId)
            {
                benchmark.Samples.Add(sample);
            }
        }
    }

    public BenchmarkToggleResult ToggleBenchmark(ProcessTarget? target, DateTime nowUtc)
    {
        lock (_gate)
        {
            if (_benchmark is { IsActive: true } active)
            {
                active.Stop(nowUtc);
                return new BenchmarkToggleResult(true, $"bench stopped: {active.ProcessName}");
            }

            var benchmarkTarget = ResolveBenchmarkTarget(target, nowUtc);
            if (benchmarkTarget is null)
            {
                return new BenchmarkToggleResult(false, "bench: no active FPS target");
            }

            _benchmark = new BenchmarkWindow(benchmarkTarget.ProcessId, benchmarkTarget.Name, nowUtc);
            return new BenchmarkToggleResult(true, $"bench started: {benchmarkTarget.Name}");
        }
    }

    public FpsStats ReadStats(int? processId, DateTime nowUtc)
    {
        lock (_gate)
        {
            Cleanup(nowUtc);

            var realtime = processId is null
                ? RealtimeStats.Empty
                : ReadRealtimeStats(processId.Value, nowUtc);
            if (realtime.Fps is null)
            {
                realtime = ReadLatestRealtimeStats(processId, nowUtc);
            }

            var benchmark = ReadBenchmarkStats();

            return new FpsStats(
                realtime.Fps,
                realtime.FrameTimeMs,
                benchmark.P1LowFps,
                benchmark.AverageFps,
                benchmark.Status);
        }
    }

    private RealtimeStats ReadRealtimeStats(int processId, DateTime nowUtc)
    {
        if (!_windows.TryGetValue(processId, out var window))
        {
            return RealtimeStats.Empty;
        }

        Trim(window, nowUtc);
        var samples = window.Samples
            .OrderBy(sample => sample.TimestampUtc)
            .ToArray();
        if (samples.Length == 0)
        {
            return RealtimeStats.Empty;
        }

        var latestSampleUtc = samples[^1].TimestampUtc;
        if (nowUtc - latestSampleUtc > StaleWindow)
        {
            return RealtimeStats.Empty;
        }

        var currentFrames = SelectRecentWindow(samples, CurrentWindow);
        return new RealtimeStats(CalculateFps(currentFrames), AverageFrameTime(currentFrames));
    }

    private RealtimeStats ReadLatestRealtimeStats(int? excludedProcessId, DateTime nowUtc)
    {
        var latest = _windows
            .Where(pair => pair.Key != excludedProcessId && IsActive(pair.Value, nowUtc))
            .OrderByDescending(pair => pair.Value.LastSeenUtc)
            .FirstOrDefault();

        return latest.Value is null ? RealtimeStats.Empty : ReadRealtimeStats(latest.Key, nowUtc);
    }

    private ProcessTarget? ResolveBenchmarkTarget(ProcessTarget? target, DateTime nowUtc)
    {
        if (target is not null &&
            _windows.TryGetValue(target.ProcessId, out var targetWindow) &&
            IsActive(targetWindow, nowUtc))
        {
            return target;
        }

        var latest = _windows
            .Where(pair => IsActive(pair.Value, nowUtc))
            .OrderByDescending(pair => pair.Value.LastSeenUtc)
            .FirstOrDefault();

        if (latest.Value is null)
        {
            return null;
        }

        return new ProcessTarget(latest.Key, GetProcessName(latest.Key));
    }

    private static bool IsActive(ProcessFrameWindow window, DateTime nowUtc)
    {
        return nowUtc - window.LastSeenUtc <= StaleWindow && window.Samples.Count > 0;
    }

    private static string GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch
        {
            return $"pid {processId}";
        }
    }

    private BenchmarkStats ReadBenchmarkStats()
    {
        if (_benchmark is null)
        {
            return BenchmarkStats.Off;
        }

        var samples = _benchmark.Samples.ToArray();
        var status = _benchmark.IsActive
            ? $"bench rec {_benchmark.ProcessName}"
            : $"bench stop {_benchmark.ProcessName}";

        if (samples.Length == 0)
        {
            return new BenchmarkStats(null, null, status);
        }

        return new BenchmarkStats(
            CalculateOnePercentLow(samples),
            CalculateFps(samples),
            status);
    }

    private static double? CalculateFps(IReadOnlyList<FrameSample> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        var measured = GetMeasuredFrameTimes(samples);
        if (measured.Length > 0)
        {
            var averageMs = measured.Average();
            return averageMs <= 0 ? null : 1000d / averageMs;
        }

        return samples.Count / CurrentWindow.TotalSeconds;
    }

    private static FrameSample[] SelectRecentWindow(IReadOnlyList<FrameSample> samples, TimeSpan window)
    {
        if (samples.Count == 0)
        {
            return Array.Empty<FrameSample>();
        }

        var selected = new List<FrameSample>();
        var accumulatedMs = 0d;

        for (var i = samples.Count - 1; i >= 0; i--)
        {
            selected.Add(samples[i]);

            var frameTimeMs = samples[i].FrameTimeMs;
            if (frameTimeMs is null && i > 0)
            {
                var timestampDeltaMs = (samples[i].TimestampUtc - samples[i - 1].TimestampUtc).TotalMilliseconds;
                if (timestampDeltaMs is >= 0.5 and <= 1000)
                {
                    frameTimeMs = timestampDeltaMs;
                }
            }

            if (frameTimeMs is not null)
            {
                accumulatedMs += frameTimeMs.Value;
            }

            if (selected.Count > 1 && accumulatedMs >= window.TotalMilliseconds)
            {
                break;
            }
        }

        selected.Reverse();
        return selected.ToArray();
    }

    private static double? AverageFrameTime(IReadOnlyList<FrameSample> samples)
    {
        if (samples.Count == 0)
        {
            return null;
        }

        var measured = GetMeasuredFrameTimes(samples);
        if (measured.Length > 0)
        {
            return measured.Average();
        }

        var intervals = BuildIntervals(samples);
        return intervals.Count == 0 ? null : intervals.Average();
    }

    private static double? CalculateOnePercentLow(IReadOnlyList<FrameSample> samples)
    {
        if (samples.Count < 3)
        {
            return null;
        }

        var intervals = GetMeasuredFrameTimes(samples).ToList();
        if (intervals.Count == 0)
        {
            intervals = BuildIntervals(samples);
        }

        if (intervals.Count == 0)
        {
            return null;
        }

        var slowFrameCount = Math.Max(1, (int)Math.Ceiling(intervals.Count * 0.01d));
        var slowAverageMs = intervals
            .OrderByDescending(value => value)
            .Take(slowFrameCount)
            .Average();

        return slowAverageMs <= 0 ? null : 1000d / slowAverageMs;
    }

    private static List<double> BuildIntervals(IReadOnlyList<FrameSample> samples)
    {
        var intervals = new List<double>(Math.Max(0, samples.Count - 1));
        for (var i = 1; i < samples.Count; i++)
        {
            var ms = (samples[i].TimestampUtc - samples[i - 1].TimestampUtc).TotalMilliseconds;
            if (ms is >= 0.5 and <= 1000)
            {
                intervals.Add(ms);
            }
        }

        return intervals;
    }

    private static double[] GetMeasuredFrameTimes(IReadOnlyList<FrameSample> samples)
    {
        return samples
            .Select(sample => sample.FrameTimeMs)
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .ToArray();
    }

    private void Cleanup(DateTime nowUtc)
    {
        var staleBefore = nowUtc - TimeSpan.FromMinutes(2);
        foreach (var stale in _windows.Where(pair => pair.Value.LastSeenUtc < staleBefore).Select(pair => pair.Key).ToArray())
        {
            _windows.Remove(stale);
        }
    }

    private static void Trim(ProcessFrameWindow window, DateTime nowUtc)
    {
        var cutoff = nowUtc - HistoryWindow;
        window.Samples.RemoveAll(sample => sample.TimestampUtc < cutoff);
    }

    private static double? NormalizeFrameTime(double? frameTimeMs)
    {
        return frameTimeMs is >= 0.5 and <= 1000 ? frameTimeMs : null;
    }

    private sealed class ProcessFrameWindow
    {
        public List<FrameSample> Samples { get; } = new();
        public DateTime LastSeenUtc { get; set; }
    }

    private sealed class BenchmarkWindow
    {
        public BenchmarkWindow(int processId, string processName, DateTime startedUtc)
        {
            ProcessId = processId;
            ProcessName = processName;
            StartedUtc = startedUtc;
        }

        public int ProcessId { get; }
        public string ProcessName { get; }
        public DateTime StartedUtc { get; }
        public DateTime? StoppedUtc { get; private set; }
        public bool IsActive => StoppedUtc is null;
        public List<FrameSample> Samples { get; } = new();

        public void Stop(DateTime stoppedUtc)
        {
            StoppedUtc = stoppedUtc;
        }
    }

    private readonly record struct FrameSample(DateTime TimestampUtc, double? FrameTimeMs);
    private readonly record struct RealtimeStats(double? Fps, double? FrameTimeMs)
    {
        public static RealtimeStats Empty { get; } = new(null, null);
    }

    private readonly record struct BenchmarkStats(double? P1LowFps, double? AverageFps, string Status)
    {
        public static BenchmarkStats Off { get; } = new(null, null, "bench off");
    }
}

internal sealed record FpsStats(
    double? Fps,
    double? FrameTimeMs,
    double? P1LowFps,
    double? AverageFps,
    string BenchmarkStatus)
{
    public static FpsStats Empty { get; } = new(null, null, null, null, "bench off");
}

internal sealed record BenchmarkToggleResult(bool Changed, string Message);
