namespace GamePulseMonitor.Settings;

internal sealed class AppSettings
{
    public AppLanguage Language { get; set; } = AppLanguage.Chinese;
    public bool StartWithWindows { get; set; }
    public DisplaySettings Display { get; set; } = new();
    public HotkeySettings Hotkeys { get; set; } = HotkeySettings.Default();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Language = Language,
            StartWithWindows = StartWithWindows,
            Display = Display.Clone(),
            Hotkeys = Hotkeys.Clone()
        };
    }
}

internal enum AppLanguage
{
    Chinese,
    English
}

internal sealed class DisplaySettings
{
    public OverlayLayout Layout { get; set; } = OverlayLayout.Vertical;
    public int OverlayScalePercent { get; set; } = 100;
    public int BackgroundOpacityPercent { get; set; } = 90;
    public string FontColorHex { get; set; } = "#F5F7FB";
    public OverlayPlacement Placement { get; set; } = new();
    public Dictionary<string, OverlayFieldSettings> FieldSettings { get; set; } = OverlayFieldDefaults.CreateSettings("#F5F7FB");
    public bool ShowTarget { get; set; } = true;
    public bool ShowStatus { get; set; } = true;
    public bool ShowFps { get; set; } = true;
    public bool ShowAverageFps { get; set; } = true;
    public bool ShowOnePercentLow { get; set; } = true;
    public bool ShowCpu { get; set; } = true;
    public bool ShowGpu { get; set; } = true;
    public bool ShowVram { get; set; } = true;
    public bool ShowMemory { get; set; } = true;
    public bool ShowFrameTime { get; set; } = true;
    public bool ShowFooter { get; set; } = true;

    public DisplaySettings Clone()
    {
        return new DisplaySettings
        {
            Layout = Layout,
            OverlayScalePercent = OverlayScalePercent,
            BackgroundOpacityPercent = BackgroundOpacityPercent,
            FontColorHex = FontColorHex,
            Placement = Placement.Clone(),
            FieldSettings = FieldSettings.ToDictionary(pair => pair.Key, pair => pair.Value.Clone()),
            ShowTarget = ShowTarget,
            ShowStatus = ShowStatus,
            ShowFps = ShowFps,
            ShowAverageFps = ShowAverageFps,
            ShowOnePercentLow = ShowOnePercentLow,
            ShowCpu = ShowCpu,
            ShowGpu = ShowGpu,
            ShowVram = ShowVram,
            ShowMemory = ShowMemory,
            ShowFrameTime = ShowFrameTime,
            ShowFooter = ShowFooter
        };
    }

    public void Normalize()
    {
        if (!Enum.IsDefined(Layout))
        {
            Layout = OverlayLayout.Vertical;
        }

        OverlayScalePercent = Math.Clamp(OverlayScalePercent, 60, 180);
        BackgroundOpacityPercent = Math.Clamp(BackgroundOpacityPercent, 0, 100);
        FontColorHex = NormalizeHexColor(FontColorHex, "#F5F7FB");
        Placement ??= new OverlayPlacement();
        Placement.Normalize();
        if (!ShowFooter)
        {
            ShowMemory = false;
            ShowFrameTime = false;
        }

        ShowFooter = ShowMemory || ShowFrameTime;
        NormalizeFieldSettings();
    }

    public OverlayFieldSettings GetFieldSettings(string fieldId)
    {
        if (!FieldSettings.TryGetValue(fieldId, out var settings))
        {
            settings = OverlayFieldDefaults.CreateSetting(fieldId, FontColorHex);
            FieldSettings[fieldId] = settings;
        }

        return settings;
    }

    public static string NormalizeHexColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length != 7 ||
            value[0] != '#')
        {
            return fallback;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!Uri.IsHexDigit(value[i]))
            {
                return fallback;
            }
        }

        return value.ToUpperInvariant();
    }

    private void NormalizeFieldSettings()
    {
        FieldSettings ??= new Dictionary<string, OverlayFieldSettings>();
        foreach (var fieldId in OverlayFieldDefaults.FieldIds)
        {
            var defaults = OverlayFieldDefaults.CreateSetting(fieldId, FontColorHex);
            if (!FieldSettings.TryGetValue(fieldId, out var settings) || settings is null)
            {
                FieldSettings[fieldId] = defaults;
                continue;
            }

            settings.HorizontalWidth = Math.Clamp(settings.HorizontalWidth <= 0 ? defaults.HorizontalWidth : settings.HorizontalWidth, 48, 260);
            settings.LabelColorHex = NormalizeHexColor(settings.LabelColorHex, defaults.LabelColorHex);
            settings.ValueColorHex = NormalizeHexColor(settings.ValueColorHex, defaults.ValueColorHex);
        }
    }
}

internal enum OverlayLayout
{
    Vertical,
    Horizontal
}

internal sealed class OverlayPlacement
{
    public bool HasPlacement { get; set; }
    public double Left { get; set; }
    public double Top { get; set; }

    public OverlayPlacement Clone()
    {
        return new OverlayPlacement
        {
            HasPlacement = HasPlacement,
            Left = Left,
            Top = Top
        };
    }

    public void Normalize()
    {
        if (!double.IsFinite(Left) || !double.IsFinite(Top))
        {
            HasPlacement = false;
            Left = 0;
            Top = 0;
        }
    }
}

internal sealed class OverlayFieldSettings
{
    public double HorizontalWidth { get; set; }
    public string LabelColorHex { get; set; } = "#F5F7FB";
    public string ValueColorHex { get; set; } = "#F5F7FB";

    public OverlayFieldSettings Clone()
    {
        return new OverlayFieldSettings
        {
            HorizontalWidth = HorizontalWidth,
            LabelColorHex = LabelColorHex,
            ValueColorHex = ValueColorHex
        };
    }
}

internal static class OverlayFieldIds
{
    public const string Fps = "Fps";
    public const string AverageFps = "AverageFps";
    public const string OnePercentLow = "OnePercentLow";
    public const string Cpu = "Cpu";
    public const string Gpu = "Gpu";
    public const string Vram = "Vram";
    public const string Memory = "Memory";
    public const string FrameTime = "FrameTime";
}

internal static class OverlayFieldDefaults
{
    public static readonly string[] FieldIds =
    [
        OverlayFieldIds.Fps,
        OverlayFieldIds.AverageFps,
        OverlayFieldIds.OnePercentLow,
        OverlayFieldIds.Cpu,
        OverlayFieldIds.Gpu,
        OverlayFieldIds.Vram,
        OverlayFieldIds.Memory,
        OverlayFieldIds.FrameTime
    ];

    public static Dictionary<string, OverlayFieldSettings> CreateSettings(string colorHex)
    {
        return FieldIds.ToDictionary(fieldId => fieldId, fieldId => CreateSetting(fieldId, colorHex));
    }

    public static OverlayFieldSettings CreateSetting(string fieldId, string colorHex)
    {
        return new OverlayFieldSettings
        {
            HorizontalWidth = fieldId switch
            {
                OverlayFieldIds.Fps => 72,
                OverlayFieldIds.AverageFps => 92,
                OverlayFieldIds.OnePercentLow => 90,
                OverlayFieldIds.Cpu => 112,
                OverlayFieldIds.Gpu => 72,
                OverlayFieldIds.Vram => 150,
                OverlayFieldIds.Memory => 116,
                OverlayFieldIds.FrameTime => 92,
                _ => 92
            },
            LabelColorHex = colorHex,
            ValueColorHex = colorHex
        };
    }
}

internal sealed class HotkeySettings
{
    public HotkeySetting ToggleOverlay { get; set; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7A);
    public HotkeySetting Exit { get; set; } = new(HotkeyModifiers.Control | HotkeyModifiers.Shift, 0x7B);
    public HotkeySetting ToggleBenchmark { get; set; } = new(HotkeyModifiers.Alt, 0x41);

    public static HotkeySettings Default() => new();

    public HotkeySettings Clone()
    {
        return new HotkeySettings
        {
            ToggleOverlay = ToggleOverlay.Clone(),
            Exit = Exit.Clone(),
            ToggleBenchmark = ToggleBenchmark.Clone()
        };
    }
}

[Flags]
internal enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Win = 0x0008
}

internal sealed class HotkeySetting : IEquatable<HotkeySetting>
{
    public HotkeySetting()
    {
    }

    public HotkeySetting(HotkeyModifiers modifiers, uint key)
    {
        Modifiers = modifiers;
        Key = key;
    }

    public HotkeyModifiers Modifiers { get; set; }
    public uint Key { get; set; }

    public HotkeySetting Clone() => new(Modifiers, Key);

    public bool Equals(HotkeySetting? other)
    {
        return other is not null && Modifiers == other.Modifiers && Key == other.Key;
    }

    public override bool Equals(object? obj) => Equals(obj as HotkeySetting);

    public override int GetHashCode() => HashCode.Combine(Modifiers, Key);
}
