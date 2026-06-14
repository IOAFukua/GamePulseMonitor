using System.Diagnostics;
using GamePulseMonitor.Interop;

namespace GamePulseMonitor.Monitoring;

internal sealed class HardwareSampler : IDisposable
{
    private readonly SystemCpuUsage _systemCpuUsage = new();
    private readonly Dictionary<int, ProcessCpuSample> _processCpuSamples = new();
    private readonly object _gate = new();
    private PerformanceCounterSet _gpuEngineCounters = PerformanceCounterSet.Empty;
    private PerformanceCounterSet _vramDedicatedCounters = PerformanceCounterSet.Empty;
    private PerformanceCounterSet _vramSharedCounters = PerformanceCounterSet.Empty;
    private int _lastProcessVramPid = -1;
    private PerformanceCounterSet _processVramDedicatedCounters = PerformanceCounterSet.Empty;
    private PerformanceCounterSet _processVramSharedCounters = PerformanceCounterSet.Empty;
    private DateTime _nextCounterRefreshUtc = DateTime.MinValue;

    public HardwareSampler()
    {
        RefreshCounters();
    }

    public HardwareSnapshot Read(ProcessTarget? target)
    {
        lock (_gate)
        {
            if (DateTime.UtcNow >= _nextCounterRefreshUtc)
            {
                RefreshCounters();
            }

            var cpuTotal = _systemCpuUsage.ReadPercent();
            var processCpu = target is null ? null : ReadProcessCpu(target.ProcessId);
            var gpuTotal = Math.Clamp(_gpuEngineCounters.SumNextValue(), 0, 100);
            var vramDedicated = _vramDedicatedCounters.SumNextValue() / 1024d / 1024d;
            var vramShared = _vramSharedCounters.SumNextValue() / 1024d / 1024d;

            var processVramDedicated = target is null ? null : ReadProcessVram(target.ProcessId, dedicated: true);
            var processVramShared = target is null ? null : ReadProcessVram(target.ProcessId, dedicated: false);
            var (memoryUsedMb, memoryPercent) = SystemMemoryStatus.Read();

            return new HardwareSnapshot(
                cpuTotal,
                processCpu,
                gpuTotal,
                vramDedicated,
                vramShared,
                processVramDedicated,
                processVramShared,
                memoryUsedMb,
                memoryPercent);
        }
    }

    public void Dispose()
    {
        _gpuEngineCounters.Dispose();
        _vramDedicatedCounters.Dispose();
        _vramSharedCounters.Dispose();
        _processVramDedicatedCounters.Dispose();
        _processVramSharedCounters.Dispose();
    }

    private void RefreshCounters()
    {
        _gpuEngineCounters.Dispose();
        _vramDedicatedCounters.Dispose();
        _vramSharedCounters.Dispose();

        _gpuEngineCounters = CreateGpuEngineCounters();
        _vramDedicatedCounters = CreateCounters("GPU Adapter Memory", "Dedicated Usage");
        _vramSharedCounters = CreateCounters("GPU Adapter Memory", "Shared Usage");
        _nextCounterRefreshUtc = DateTime.UtcNow + TimeSpan.FromSeconds(10);
    }

    private double? ReadProcessCpu(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            var now = DateTime.UtcNow;
            var totalProcessorTime = process.TotalProcessorTime;

            if (!_processCpuSamples.TryGetValue(processId, out var last))
            {
                _processCpuSamples[processId] = new ProcessCpuSample(totalProcessorTime, now);
                return null;
            }

            var elapsedMs = (now - last.TimestampUtc).TotalMilliseconds;
            var cpuMs = (totalProcessorTime - last.TotalProcessorTime).TotalMilliseconds;
            _processCpuSamples[processId] = new ProcessCpuSample(totalProcessorTime, now);

            if (elapsedMs <= 0)
            {
                return null;
            }

            return Math.Clamp(cpuMs / elapsedMs / Environment.ProcessorCount * 100d, 0, 100);
        }
        catch
        {
            return null;
        }
    }

    private double? ReadProcessVram(int processId, bool dedicated)
    {
        if (_lastProcessVramPid != processId)
        {
            _processVramDedicatedCounters.Dispose();
            _processVramSharedCounters.Dispose();
            _processVramDedicatedCounters = CreateProcessVramCounters(processId, "Dedicated Usage");
            _processVramSharedCounters = CreateProcessVramCounters(processId, "Shared Usage");
            _lastProcessVramPid = processId;
        }

        var counters = dedicated ? _processVramDedicatedCounters : _processVramSharedCounters;
        if (counters.IsEmpty)
        {
            return null;
        }

        return counters.SumNextValue() / 1024d / 1024d;
    }

    private static PerformanceCounterSet CreateGpuEngineCounters()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
            {
                return PerformanceCounterSet.Empty;
            }

            var category = new PerformanceCounterCategory("GPU Engine");
            var counters = category.GetInstanceNames()
                .Where(name => name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase) ||
                               name.Contains("engtype_Compute", StringComparison.OrdinalIgnoreCase))
                .Select(name => new PerformanceCounter("GPU Engine", "Utilization Percentage", name, readOnly: true))
                .ToArray();

            Prime(counters);
            return new PerformanceCounterSet(counters);
        }
        catch
        {
            return PerformanceCounterSet.Empty;
        }
    }

    private static PerformanceCounterSet CreateCounters(string categoryName, string counterName)
    {
        try
        {
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                return PerformanceCounterSet.Empty;
            }

            var category = new PerformanceCounterCategory(categoryName);
            var counters = category.GetInstanceNames()
                .Select(name => new PerformanceCounter(categoryName, counterName, name, readOnly: true))
                .ToArray();

            Prime(counters);
            return new PerformanceCounterSet(counters);
        }
        catch
        {
            return PerformanceCounterSet.Empty;
        }
    }

    private static PerformanceCounterSet CreateProcessVramCounters(int processId, string counterName)
    {
        try
        {
            const string categoryName = "GPU Process Memory";
            if (!PerformanceCounterCategory.Exists(categoryName))
            {
                return PerformanceCounterSet.Empty;
            }

            var pidToken = $"pid_{processId}_";
            var category = new PerformanceCounterCategory(categoryName);
            var counters = category.GetInstanceNames()
                .Where(name => name.Contains(pidToken, StringComparison.OrdinalIgnoreCase))
                .Select(name => new PerformanceCounter(categoryName, counterName, name, readOnly: true))
                .ToArray();

            Prime(counters);
            return new PerformanceCounterSet(counters);
        }
        catch
        {
            return PerformanceCounterSet.Empty;
        }
    }

    private static void Prime(IEnumerable<PerformanceCounter> counters)
    {
        foreach (var counter in counters)
        {
            try
            {
                counter.NextValue();
            }
            catch
            {
                counter.Dispose();
            }
        }
    }

    private readonly record struct ProcessCpuSample(TimeSpan TotalProcessorTime, DateTime TimestampUtc);
}

internal sealed class PerformanceCounterSet : IDisposable
{
    public static PerformanceCounterSet Empty { get; } = new(Array.Empty<PerformanceCounter>());
    private readonly PerformanceCounter[] _counters;

    public PerformanceCounterSet(PerformanceCounter[] counters)
    {
        _counters = counters;
    }

    public bool IsEmpty => _counters.Length == 0;

    public float SumNextValue()
    {
        var sum = 0f;
        foreach (var counter in _counters)
        {
            try
            {
                sum += counter.NextValue();
            }
            catch
            {
                // Counter instances disappear when games close or GPUs sleep.
            }
        }

        return sum;
    }

    public void Dispose()
    {
        foreach (var counter in _counters)
        {
            counter.Dispose();
        }
    }
}

internal sealed record HardwareSnapshot(
    double CpuTotalPercent,
    double? ProcessCpuPercent,
    double GpuTotalPercent,
    double VramDedicatedMb,
    double VramSharedMb,
    double? ProcessVramDedicatedMb,
    double? ProcessVramSharedMb,
    double MemoryUsedMb,
    double MemoryPercent);
