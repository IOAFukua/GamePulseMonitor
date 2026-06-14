using System.Globalization;
using System.IO;
using System.Text;
using GamePulseMonitor.Monitoring;

namespace GamePulseMonitor.Logging;

internal sealed class MetricsCsvLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private bool _disposed;

    public MetricsCsvLogger(string logDirectory)
    {
        Directory.CreateDirectory(logDirectory);
        var path = Path.Combine(logDirectory, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
        FilePath = path;
        _writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        _writer.WriteLine("timestamp,target_process,target_pid,fps,frame_time_ms,average_fps,p1_low_fps,benchmark_status,cpu_total_percent,cpu_process_percent,gpu_total_percent,vram_dedicated_mb,vram_shared_mb,process_vram_dedicated_mb,process_vram_shared_mb,ram_used_mb,ram_percent,status");
    }

    public string FilePath { get; }

    public void Write(MetricsSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        var values = new[]
        {
            snapshot.Timestamp.ToString("O", CultureInfo.InvariantCulture),
            snapshot.Target?.Name ?? "",
            snapshot.Target?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "",
            Format(snapshot.Fps),
            Format(snapshot.FrameTimeMs),
            Format(snapshot.AverageFps),
            Format(snapshot.P1LowFps),
            snapshot.BenchmarkStatus,
            Format(snapshot.CpuTotalPercent),
            Format(snapshot.ProcessCpuPercent),
            Format(snapshot.GpuTotalPercent),
            Format(snapshot.VramDedicatedMb),
            Format(snapshot.VramSharedMb),
            Format(snapshot.ProcessVramDedicatedMb),
            Format(snapshot.ProcessVramSharedMb),
            Format(snapshot.MemoryUsedMb),
            Format(snapshot.MemoryPercent),
            snapshot.Status
        };

        _writer.WriteLine(string.Join(",", values.Select(Escape)));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writer.Dispose();
    }

    private static string Format(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
