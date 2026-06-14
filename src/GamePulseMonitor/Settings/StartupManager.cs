using System.Diagnostics;

namespace GamePulseMonitor.Settings;

internal static class StartupManager
{
    private const string TaskName = "GamePulseMonitor";

    public static bool IsEnabled()
    {
        var result = RunSchtasks("/Query", "/TN", TaskName);
        return result.ExitCode == 0;
    }

    public static void SetEnabled(bool enabled)
    {
        if (enabled)
        {
            Enable();
        }
        else
        {
            Disable();
        }
    }

    private static void Enable()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            throw new InvalidOperationException("Cannot resolve current executable path.");
        }

        var result = RunSchtasks(
            "/Create",
            "/TN", TaskName,
            "/SC", "ONLOGON",
            "/TR", Quote(exe),
            "/RL", "HIGHEST",
            "/F");

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(result.ErrorOrOutput);
        }
    }

    private static void Disable()
    {
        var result = RunSchtasks("/Delete", "/TN", TaskName, "/F");
        if (result.ExitCode != 0 && IsEnabled())
        {
            throw new InvalidOperationException(result.ErrorOrOutput);
        }
    }

    private static ProcessResult RunSchtasks(params string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProcessResult(1, "", "Failed to start schtasks.exe.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);
            return new ProcessResult(process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            return new ProcessResult(1, "", ex.Message);
        }
    }

    private static string Quote(string text)
    {
        return $"\"{text}\"";
    }

    private readonly record struct ProcessResult(int ExitCode, string Output, string Error)
    {
        public string ErrorOrOutput => string.IsNullOrWhiteSpace(Error) ? Output.Trim() : Error.Trim();
    }
}
