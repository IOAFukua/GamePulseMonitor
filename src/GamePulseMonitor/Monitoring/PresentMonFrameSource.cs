using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace GamePulseMonitor.Monitoring;

internal sealed class PresentMonFrameSource : IDisposable
{
    private const string SessionName = "GamePulseMonitor";
    private const string RawSessionName = "GamePulseMonitorRaw";
    private readonly Action<int, DateTime, double?> _onFrame;
    private readonly string? _processName;
    private readonly string _logDirectory;
    private readonly object _statusGate = new();
    private readonly CancellationTokenSource _stop = new();
    private Process? _process;
    private Dictionary<string, int>? _header;
    private int _selectedFrameSource;
    private string _status = "PresentMon starting";
    private string? _lastError;
    private long _frameCount;
    private bool _disposed;

    public PresentMonFrameSource(string? processName, string logDirectory, Action<int, DateTime, double?> onFrame)
    {
        _processName = NormalizeProcessName(processName);
        _logDirectory = logDirectory;
        _onFrame = onFrame;
    }

    public string Status
    {
        get
        {
            var frames = Interlocked.Read(ref _frameCount);
            lock (_statusGate)
            {
                return frames == 0 ? _status : $"{_status}; frames {frames}";
            }
        }
    }

    public void Start()
    {
        if (_disposed || _process is not null)
        {
            return;
        }

        StartCapture(rawFallback: false);
    }

    private void StartCapture(bool rawFallback)
    {
        var exe = ResolvePresentMonPath();
        if (!File.Exists(exe))
        {
            SetStatus($"PresentMon missing: {exe}");
            return;
        }

        try
        {
            Directory.CreateDirectory(_logDirectory);
            var sessionName = rawFallback ? RawSessionName : SessionName;
            _header = null;
            _lastError = null;
            Volatile.Write(ref _selectedFrameSource, 0);

            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            CleanupStaleCollectors();
            AddArguments(startInfo, sessionName, rawFallback);
            _process = Process.Start(startInfo);
            if (_process is null)
            {
                SetStatus("PresentMon failed to start");
                return;
            }

            SetStatus(rawFallback ? "PresentMon raw fallback" : "PresentMon active");
            _ = Task.Run(() => ReadStdoutAsync(_process, _stop.Token));
            _ = Task.Run(() => ReadStderrAsync(_process, _stop.Token));
            _ = Task.Run(() => WatchExitAsync(_process, rawFallback, _stop.Token));
            if (!rawFallback)
            {
                _ = Task.Run(() => RestartRawIfNoFramesAsync(_process, _stop.Token));
            }
        }
        catch (Exception ex)
        {
            SetStatus($"PresentMon error: {ex.GetType().Name}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stop.Cancel();

        var process = _process;
        _process = null;
        StopProcessTree(process);
        process?.Dispose();
        CleanupStaleCollectors();
        _stop.Dispose();
    }

    internal static void CleanupStaleCollectors()
    {
        StopStaleTraceSessions();

        var exe = ResolvePresentMonPath();
        if (!File.Exists(exe))
        {
            return;
        }

        TerminateExistingSession(exe, SessionName);
        TerminateExistingSession(exe, RawSessionName);
    }

    private void AddArguments(ProcessStartInfo startInfo, string sessionName, bool rawFallback)
    {
        startInfo.ArgumentList.Add("-output_stdout");
        startInfo.ArgumentList.Add("-no_top");
        startInfo.ArgumentList.Add("-session_name");
        startInfo.ArgumentList.Add(sessionName);
        startInfo.ArgumentList.Add("-stop_existing_session");
        if (rawFallback)
        {
            startInfo.ArgumentList.Add("-no_track_display");
        }

        startInfo.ArgumentList.Add("-exclude");
        startInfo.ArgumentList.Add("GamePulseMonitor.exe");

        if (!string.IsNullOrWhiteSpace(_processName))
        {
            startInfo.ArgumentList.Add("-process_name");
            startInfo.ArgumentList.Add(_processName);
        }
    }

    private static void TerminateExistingSession(string exe, string sessionName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("-session_name");
            startInfo.ArgumentList.Add(sessionName);
            startInfo.ArgumentList.Add("-terminate_existing");

            using var process = Process.Start(startInfo);
            process?.WaitForExit(2000);
        }
        catch
        {
            // Best-effort cleanup; -stop_existing_session still runs on capture start.
        }
    }

    private static void StopProcessTree(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
            process.WaitForExit(3000);
        }
        catch
        {
            // The process may exit while the application is closing.
        }
    }

    private static void StopStaleTraceSessions()
    {
        foreach (var sessionName in QueryStaleTraceSessionNames())
        {
            StopTraceSession(sessionName);
        }
    }

    private static IReadOnlyList<string> QueryStaleTraceSessionNames()
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "logman.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("query");
            startInfo.ArgumentList.Add("-ets");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return Array.Empty<string>();
            }

            var sessions = new List<string>();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(2000);

            foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("GamePulseMonitor", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(name))
                {
                    sessions.Add(name);
                }
            }

            return sessions;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static void StopTraceSession(string sessionName)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "logman.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add("stop");
            startInfo.ArgumentList.Add(sessionName);
            startInfo.ArgumentList.Add("-ets");

            using var process = Process.Start(startInfo);
            process?.WaitForExit(2000);
        }
        catch
        {
            // This cleanup only targets stale sessions from previous monitor runs.
        }
    }

    private async Task ReadStdoutAsync(Process process, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await process.StandardOutput.ReadLineAsync(token);
                if (line is null)
                {
                    break;
                }

                ProcessCsvLine(line, frameSource: 2);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"PresentMon stdout error: {ex.GetType().Name}");
        }
    }

    private async Task ReadStderrAsync(Process process, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await process.StandardError.ReadLineAsync(token);
                if (line is null)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var text = line.Trim();
                    _lastError = text;
                    SetStatus($"PresentMon: {text}");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Stderr is diagnostic only.
        }
    }

    private async Task WatchExitAsync(Process process, bool rawFallback, CancellationToken token)
    {
        try
        {
            await process.WaitForExitAsync(token);
            if (!token.IsCancellationRequested && ReferenceEquals(_process, process))
            {
                var error = _lastError;
                var detail = string.IsNullOrWhiteSpace(error) ? "" : $": {error}";
                SetStatus($"{(rawFallback ? "PresentMon raw" : "PresentMon")} exited: {process.ExitCode}{detail}");
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RestartRawIfNoFramesAsync(Process process, CancellationToken token)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            if (token.IsCancellationRequested ||
                !ReferenceEquals(_process, process) ||
                Interlocked.Read(ref _frameCount) > 0)
            {
                return;
            }

            SetStatus("PresentMon display mode no frames; retry raw");
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(token);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
            }

            if (ReferenceEquals(_process, process))
            {
                _process.Dispose();
                _process = null;
            }

            StartCapture(rawFallback: true);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ProcessCsvLine(string line, int frameSource)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var fields = SplitCsv(line);
        if (fields.Count == 0)
        {
            return;
        }

        if (_header is null)
        {
            TrySetHeader(fields);
            return;
        }

        var processId = ReadInt(fields, "ProcessID", "ProcessId", "PID");
        if (processId <= 0)
        {
            return;
        }

        var frameTimeMs = ReadDisplayedFrameTime(fields);
        var timestampUtc = DateTime.UtcNow;
        if (!TrySelectFrameSource(frameSource))
        {
            return;
        }

        Interlocked.Increment(ref _frameCount);
        _onFrame(processId, timestampUtc, frameTimeMs);
    }

    private bool TrySelectFrameSource(int frameSource)
    {
        var selected = Volatile.Read(ref _selectedFrameSource);
        if (selected == frameSource)
        {
            return true;
        }

        if (selected != 0)
        {
            return false;
        }

        var original = Interlocked.CompareExchange(ref _selectedFrameSource, frameSource, 0);
        return original == 0 || original == frameSource;
    }

    private void TrySetHeader(IReadOnlyList<string> fields)
    {
        var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < fields.Count; i++)
        {
            var name = fields[i].Trim().TrimStart('\uFEFF');
            if (!string.IsNullOrWhiteSpace(name) && !header.ContainsKey(name))
            {
                header.Add(name, i);
            }

            var normalizedName = NormalizeColumnName(name);
            if (!string.IsNullOrWhiteSpace(normalizedName) && !header.ContainsKey(normalizedName))
            {
                header.Add(normalizedName, i);
            }
        }

        if (HasColumn(header, "ProcessID") && HasAnyDisplayTimingColumn(header))
        {
            _header = header;
            SetStatus("PresentMon streaming");
        }
    }

    private int ReadInt(IReadOnlyList<string> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (_header is not null &&
                TryGetColumnIndex(_header, name, out var index) &&
                index < fields.Count &&
                int.TryParse(fields[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return 0;
    }

    private double? ReadDouble(IReadOnlyList<string> fields, params string[] names)
    {
        foreach (var name in names)
        {
            if (_header is null || !TryGetColumnIndex(_header, name, out var index) || index >= fields.Count)
            {
                continue;
            }

            var text = fields[index];
            if (text.Equals("NA", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private double? ReadDisplayedFrameTime(IReadOnlyList<string> fields)
    {
        return ReadDouble(fields, "MsBetweenDisplayChange", "DisplayedTime", "MsBetweenPresents");
    }

    private static bool HasAnyDisplayTimingColumn(IReadOnlyDictionary<string, int> header)
    {
        return HasColumn(header, "MsBetweenDisplayChange") ||
               HasColumn(header, "DisplayedTime") ||
               HasColumn(header, "MsBetweenPresents");
    }

    private static bool HasColumn(IReadOnlyDictionary<string, int> header, string name)
    {
        return header.ContainsKey(name) || header.ContainsKey(NormalizeColumnName(name));
    }

    private static bool TryGetColumnIndex(IReadOnlyDictionary<string, int> header, string name, out int index)
    {
        return header.TryGetValue(name, out index) ||
               header.TryGetValue(NormalizeColumnName(name), out index);
    }

    private static string NormalizeColumnName(string name)
    {
        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private void SetStatus(string status)
    {
        lock (_statusGate)
        {
            _status = status;
        }
    }

    private static string ResolvePresentMonPath()
    {
        var outputPath = Path.Combine(AppContext.BaseDirectory, "tools", "presentmon", "PresentMon.exe");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", "presentmon", "PresentMon.exe");
    }

    private static string TrimSessionName(string sessionName)
    {
        const int maxLength = 60;
        return sessionName.Length <= maxLength ? sessionName : sessionName[..maxLength];
    }

    private static string? NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        var fileName = Path.GetFileName(processName.Trim());
        return Path.HasExtension(fileName) ? fileName : $"{fileName}.exe";
    }

    private static List<string> SplitCsv(string line)
    {
        var fields = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    builder.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(builder.ToString());
                builder.Clear();
            }
            else
            {
                builder.Append(c);
            }
        }

        fields.Add(builder.ToString());
        return fields;
    }
}
