using GamePulseMonitor.Interop;
using GamePulseMonitor.Monitoring;
using GamePulseMonitor.Settings;

namespace GamePulseMonitor;

public partial class App : System.Windows.Application
{
    private MetricsService? _metricsService;
    private AppSettingsStore? _settingsStore;

    private void OnStartup(object sender, System.Windows.StartupEventArgs e)
    {
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        StopStaleMonitorProcesses();

        var options = MonitorOptions.Parse(e.Args);
        _settingsStore = new AppSettingsStore();
        _metricsService = new MetricsService(options);

        MainWindow = new MainWindow(_metricsService, options, _settingsStore);
        MainWindow.Show();
        _metricsService.Start();
    }

    private void OnExit(object sender, System.Windows.ExitEventArgs e)
    {
        try
        {
            _metricsService?.Dispose();
        }
        finally
        {
            StopMonitorProcesses();
        }
    }

    private static void StopStaleMonitorProcesses()
    {
        StopMonitorProcesses();
    }

    private static void StopMonitorProcesses()
    {
        var currentPid = Environment.ProcessId;
        ProcessCleanup.KillDescendants(currentPid);
        PresentMonFrameSource.CleanupStaleCollectors();
        ProcessCleanup.KillProcessesByName("PresentMon", currentPid);
        ProcessCleanup.KillProcessesByName("GamePulseMonitor", currentPid);
    }
}
