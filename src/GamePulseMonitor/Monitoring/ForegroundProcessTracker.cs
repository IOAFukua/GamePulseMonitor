using System.Diagnostics;
using System.IO;
using GamePulseMonitor.Interop;

namespace GamePulseMonitor.Monitoring;

internal sealed class ForegroundProcessTracker
{
    private readonly string? _configuredProcessName;
    private readonly int _ownPid = Environment.ProcessId;
    private ProcessTarget? _lastTarget;

    public ForegroundProcessTracker(string? configuredProcessName)
    {
        _configuredProcessName = NormalizeProcessName(configuredProcessName);
    }

    public ProcessTarget? GetTarget()
    {
        if (!string.IsNullOrWhiteSpace(_configuredProcessName))
        {
            return GetConfiguredTarget();
        }

        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == nint.Zero)
        {
            return _lastTarget;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == _ownPid)
        {
            return _lastTarget;
        }

        try
        {
            using var process = Process.GetProcessById((int)pid);
            if (string.IsNullOrWhiteSpace(process.ProcessName))
            {
                return _lastTarget;
            }

            _lastTarget = new ProcessTarget(process.Id, process.ProcessName);
            return _lastTarget;
        }
        catch
        {
            return _lastTarget;
        }
    }

    private ProcessTarget? GetConfiguredTarget()
    {
        var processes = Process.GetProcessesByName(_configuredProcessName);
        try
        {
            var process = processes
                .Where(p => !p.HasExited)
                .OrderByDescending(p => p.MainWindowHandle != nint.Zero)
                .ThenBy(p => p.Id)
                .FirstOrDefault();

            if (process is null)
            {
                return _lastTarget;
            }

            _lastTarget = new ProcessTarget(process.Id, process.ProcessName);
            return _lastTarget;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static string? NormalizeProcessName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return Path.GetFileNameWithoutExtension(name.Trim());
    }
}
