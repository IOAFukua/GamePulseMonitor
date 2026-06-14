using System.Globalization;
using System.IO;
using System.Text;
using GamePulseMonitor.Monitoring;

namespace GamePulseMonitor.Logging;

internal sealed class MetricsHistoryStore : IDisposable
{
    private const string AllProcessesKey = "__ALL__";
    private const string Header = "timestamp,target_process,target_pid,fps,frame_time_ms,average_fps,p1_low_fps,cpu_total_percent,cpu_process_percent,gpu_total_percent,vram_dedicated_mb,process_vram_dedicated_mb,ram_used_mb,ram_percent";

    private readonly object _gate = new();
    private StreamWriter? _writer;
    private string? _writerDateKey;
    private bool _disposed;

    public MetricsHistoryStore()
    {
        DirectoryPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GamePulseMonitor",
            "history");
        Directory.CreateDirectory(DirectoryPath);
    }

    public string DirectoryPath { get; }
    public static string AllProcesses => AllProcessesKey;

    public void Write(MetricsSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            var writer = GetWriter(snapshot.Timestamp);
            var values = new[]
            {
                snapshot.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                snapshot.Target?.Name ?? "",
                snapshot.Target?.ProcessId.ToString(CultureInfo.InvariantCulture) ?? "",
                Format(snapshot.Fps),
                Format(snapshot.FrameTimeMs),
                Format(snapshot.AverageFps),
                Format(snapshot.P1LowFps),
                Format(snapshot.CpuTotalPercent),
                Format(snapshot.ProcessCpuPercent),
                Format(snapshot.GpuTotalPercent),
                Format(snapshot.VramDedicatedMb),
                Format(snapshot.ProcessVramDedicatedMb),
                Format(snapshot.MemoryUsedMb),
                Format(snapshot.MemoryPercent)
            };

            writer.WriteLine(string.Join(",", values.Select(Escape)));
        }
    }

    public IReadOnlyList<DateOnly> GetAvailableDates()
    {
        if (!Directory.Exists(DirectoryPath))
        {
            return Array.Empty<DateOnly>();
        }

        return Directory.EnumerateFiles(DirectoryPath, "*.csv")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Select(name => DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : (DateOnly?)null)
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .OrderDescending()
            .ToArray();
    }

    public IReadOnlyList<HistoryProcess> GetProcesses(DateOnly date)
    {
        return ReadDay(date)
            .Where(point => !string.IsNullOrWhiteSpace(point.ProcessName))
            .GroupBy(point => point.ProcessKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First().Process)
            .OrderBy(process => process.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public MetricsHistoryPoint? GetLatestPoint()
    {
        foreach (var date in GetAvailableDates())
        {
            var latest = ReadDay(date)
                .OrderByDescending(point => point.Timestamp)
                .FirstOrDefault();
            if (latest is not null)
            {
                return latest;
            }
        }

        return null;
    }

    public IReadOnlyList<MetricsHistoryPoint> Query(DateOnly date, string processKey, DateTimeOffset from, DateTimeOffset to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return ReadDay(date)
            .Where(point => point.Timestamp >= from && point.Timestamp <= to)
            .Where(point => processKey == AllProcessesKey || string.Equals(point.ProcessKey, processKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(point => point.Timestamp)
            .ToArray();
    }

    public IReadOnlyList<MetricsHistoryPoint> ReadDay(DateOnly date)
    {
        var path = GetPath(date);
        if (!File.Exists(path))
        {
            return Array.Empty<MetricsHistoryPoint>();
        }

        var points = new List<MetricsHistoryPoint>();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        _ = reader.ReadLine();

        while (reader.ReadLine() is { } line)
        {
            if (TryParsePoint(line, out var point))
            {
                points.Add(point);
            }
        }

        return points;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_gate)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }

    public static string CreateProcessKey(string processName, int? processId)
    {
        return $"{processName}|{processId?.ToString(CultureInfo.InvariantCulture) ?? ""}";
    }

    private StreamWriter GetWriter(DateTimeOffset timestamp)
    {
        var dateKey = timestamp.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_writer is not null && string.Equals(_writerDateKey, dateKey, StringComparison.Ordinal))
        {
            return _writer;
        }

        _writer?.Dispose();
        Directory.CreateDirectory(DirectoryPath);
        var path = Path.Combine(DirectoryPath, $"{dateKey}.csv");
        var writeHeader = !File.Exists(path) || new FileInfo(path).Length == 0;
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
        {
            AutoFlush = true
        };
        _writerDateKey = dateKey;
        if (writeHeader)
        {
            _writer.WriteLine(Header);
        }

        return _writer;
    }

    private string GetPath(DateOnly date)
    {
        return Path.Combine(DirectoryPath, $"{date:yyyy-MM-dd}.csv");
    }

    private static bool TryParsePoint(string line, out MetricsHistoryPoint point)
    {
        point = default!;
        var fields = ParseCsvLine(line);
        if (fields.Count < 14 ||
            !DateTimeOffset.TryParse(fields[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
        {
            return false;
        }

        var processName = fields[1];
        var processId = ParseInt(fields[2]);
        point = new MetricsHistoryPoint(
            timestamp,
            new HistoryProcess(processName, processId),
            ParseDouble(fields[3]),
            ParseDouble(fields[4]),
            ParseDouble(fields[5]),
            ParseDouble(fields[6]),
            ParseDouble(fields[7]),
            ParseDouble(fields[8]),
            ParseDouble(fields[9]),
            ParseDouble(fields[10]),
            ParseDouble(fields[11]),
            ParseDouble(fields[12]),
            ParseDouble(fields[13]));
        return true;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == ',')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static string Format(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";

    private static double? ParseDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int? ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

internal sealed record HistoryProcess(string ProcessName, int? ProcessId)
{
    public string ProcessKey => MetricsHistoryStore.CreateProcessKey(ProcessName, ProcessId);
    public string DisplayName => string.IsNullOrWhiteSpace(ProcessName)
        ? "Unknown"
        : ProcessId is null
            ? ProcessName
            : $"{ProcessName} ({ProcessId.Value})";
}

internal sealed record MetricsHistoryPoint(
    DateTimeOffset Timestamp,
    HistoryProcess Process,
    double? Fps,
    double? FrameTimeMs,
    double? AverageFps,
    double? P1LowFps,
    double? CpuTotalPercent,
    double? ProcessCpuPercent,
    double? GpuTotalPercent,
    double? VramDedicatedMb,
    double? ProcessVramDedicatedMb,
    double? MemoryUsedMb,
    double? MemoryPercent)
{
    public string ProcessName => Process.ProcessName;
    public int? ProcessId => Process.ProcessId;
    public string ProcessKey => Process.ProcessKey;
}
