namespace GamePulseMonitor.Logging;

internal sealed class TrayController : IDisposable
{
    public const int CallbackMessage = 0x0400 + 200;

    private readonly Action _toggleOverlay;
    private readonly Action _openSettings;
    private readonly Action _openHistory;
    private readonly Action _startScreenshot;
    private readonly Action _exit;
    private readonly System.Windows.Forms.NotifyIcon _notifyIcon = new();
    private readonly System.Windows.Forms.ContextMenuStrip _menu = new();
    private readonly System.Windows.Forms.ToolStripMenuItem _toggleOverlayItem = new("Show / hide overlay");
    private readonly System.Windows.Forms.ToolStripMenuItem _settingsItem = new("Settings");
    private readonly System.Windows.Forms.ToolStripMenuItem _historyItem = new("History");
    private readonly System.Windows.Forms.ToolStripMenuItem _screenshotItem = new("Screenshot");
    private readonly System.Windows.Forms.ToolStripMenuItem _exitItem = new("Exit");
    private System.Drawing.Icon? _icon;
    private bool _added;

    public TrayController(Action toggleOverlay, Action openSettings, Action openHistory, Action startScreenshot, Action exit)
    {
        _toggleOverlay = toggleOverlay;
        _openSettings = openSettings;
        _openHistory = openHistory;
        _startScreenshot = startScreenshot;
        _exit = exit;

        _toggleOverlayItem.Click += (_, _) => _toggleOverlay();
        _settingsItem.Click += (_, _) => _openSettings();
        _historyItem.Click += (_, _) => _openHistory();
        _screenshotItem.Click += (_, _) => _startScreenshot();
        _exitItem.Click += (_, _) => _exit();

        _menu.Items.Add(_toggleOverlayItem);
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_historyItem);
        _menu.Items.Add(_screenshotItem);
        _menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _menu.Items.Add(_exitItem);

        _notifyIcon.Text = "GamePulseMonitor";
        _notifyIcon.ContextMenuStrip = _menu;
        _notifyIcon.DoubleClick += (_, _) => _toggleOverlay();
    }

    public void Initialize(nint hwnd)
    {
        if (_added)
        {
            return;
        }

        _ = hwnd;
        _icon = ExtractIcon();
        if (_icon is not null)
        {
            _notifyIcon.Icon = _icon;
        }

        _notifyIcon.Visible = true;
        _added = true;
    }

    public bool HandleMessage(int msg, nint wParam, nint lParam)
    {
        _ = msg;
        _ = wParam;
        _ = lParam;
        return false;
    }

    public void ApplyText(string showHideOverlay, string settings, string history, string screenshot, string exit)
    {
        _toggleOverlayItem.Text = showHideOverlay;
        _settingsItem.Text = settings;
        _historyItem.Text = history;
        _screenshotItem.Text = screenshot;
        _exitItem.Text = exit;
    }

    public void Dispose()
    {
        if (_added)
        {
            _notifyIcon.Visible = false;
            _added = false;
        }

        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon?.Dispose();
    }

    private static System.Drawing.Icon? ExtractIcon()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return System.Drawing.Icon.ExtractAssociatedIcon(path);
        }
        catch
        {
            return null;
        }
    }
}
