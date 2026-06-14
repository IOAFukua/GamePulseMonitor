using System.IO;

namespace GamePulseMonitor.Monitoring;

internal sealed record MonitorOptions(
    string? ProcessName,
    bool ClickThrough,
    double Left,
    double Top,
    string LogDirectory)
{
    public static MonitorOptions Parse(string[] args)
    {
        string? processName = null;
        var clickThrough = true;
        var left = 24d;
        var top = 24d;
        var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.Equals("--process", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                processName = args[++i];
            }
            else if (arg.StartsWith("--process=", StringComparison.OrdinalIgnoreCase))
            {
                processName = arg["--process=".Length..];
            }
            else if (arg.Equals("--no-clickthrough", StringComparison.OrdinalIgnoreCase))
            {
                clickThrough = false;
            }
            else if (arg.Equals("--left", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && double.TryParse(args[++i], out var parsedLeft))
            {
                left = parsedLeft;
            }
            else if (arg.Equals("--top", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length && double.TryParse(args[++i], out var parsedTop))
            {
                top = parsedTop;
            }
            else if (arg.Equals("--log-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                logDirectory = Path.GetFullPath(args[++i]);
            }
        }

        return new MonitorOptions(processName, clickThrough, left, top, logDirectory);
    }
}
