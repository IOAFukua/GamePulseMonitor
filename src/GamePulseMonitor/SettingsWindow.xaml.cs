using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GamePulseMonitor.Settings;

namespace GamePulseMonitor;

public partial class SettingsWindow : Window
{
    private readonly AppSettingsStore _store;
    private bool _isLoading = true;

    internal SettingsWindow(AppSettingsStore store)
    {
        _store = store;
        InitializeComponent();
        SyncStartupState();
        LoadSettings(_store.Current);
        _store.SettingsChanged += OnSettingsChanged;
    }

    protected override void OnClosed(EventArgs e)
    {
        _store.SettingsChanged -= OnSettingsChanged;
        base.OnClosed(e);
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        Dispatcher.InvokeAsync(() => LoadSettings(settings));
    }

    private void SyncStartupState()
    {
        var enabled = StartupManager.IsEnabled();
        if (_store.Current.StartWithWindows != enabled)
        {
            _store.Update(settings => settings.StartWithWindows = enabled);
        }
    }

    private void LoadSettings(AppSettings settings)
    {
        _isLoading = true;
        try
        {
            ApplyLanguage(settings.Language);
            LanguageComboBox.SelectedIndex = settings.Language == AppLanguage.Chinese ? 0 : 1;
            StartWithWindowsCheckBox.IsChecked = settings.StartWithWindows;
            LayoutComboBox.SelectedIndex = settings.Display.Layout == OverlayLayout.Horizontal ? 1 : 0;
            OverlayScaleSlider.Value = settings.Display.OverlayScalePercent;
            BackgroundOpacitySlider.Value = settings.Display.BackgroundOpacityPercent;
            UpdateOverlayValueText(settings.Display);
            UpdateUniformColorSelection(settings.Display);
            UpdateFieldColorButtons(settings.Display);
            ShowTargetCheckBox.IsChecked = settings.Display.ShowTarget;
            ShowStatusCheckBox.IsChecked = settings.Display.ShowStatus;
            ShowFpsCheckBox.IsChecked = settings.Display.ShowFps;
            ShowAverageFpsCheckBox.IsChecked = settings.Display.ShowAverageFps;
            ShowOnePercentLowCheckBox.IsChecked = settings.Display.ShowOnePercentLow;
            ShowCpuCheckBox.IsChecked = settings.Display.ShowCpu;
            ShowGpuCheckBox.IsChecked = settings.Display.ShowGpu;
            ShowVramCheckBox.IsChecked = settings.Display.ShowVram;
            ShowMemoryCheckBox.IsChecked = settings.Display.ShowMemory;
            ShowFrameTimeCheckBox.IsChecked = settings.Display.ShowFrameTime;
            ToggleOverlayHotkeyTextBox.Text = HotkeyFormatter.Format(settings.Hotkeys.ToggleOverlay);
            BenchmarkHotkeyTextBox.Text = HotkeyFormatter.Format(settings.Hotkeys.ToggleBenchmark);
            ExitHotkeyTextBox.Text = HotkeyFormatter.Format(settings.Hotkeys.Exit);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ApplyLanguage(AppLanguage language)
    {
        Title = TextCatalog.Get(language, "Settings");
        TitleText.Text = TextCatalog.Get(language, "Settings");
        LanguageTitleText.Text = TextCatalog.Get(language, "Language");
        ChineseItem.Content = TextCatalog.Get(language, "Chinese");
        EnglishItem.Content = TextCatalog.Get(language, "English");
        StartWithWindowsCheckBox.Content = TextCatalog.Get(language, "StartWithWindows");
        OverlayAppearanceTitleText.Text = TextCatalog.Get(language, "OverlayAppearance");
        LayoutLabel.Text = TextCatalog.Get(language, "Layout");
        VerticalLayoutItem.Content = TextCatalog.Get(language, "VerticalLayout");
        HorizontalLayoutItem.Content = TextCatalog.Get(language, "HorizontalLayout");
        OverlaySizeLabel.Text = TextCatalog.Get(language, "OverlaySize");
        BackgroundOpacityLabel.Text = TextCatalog.Get(language, "BackgroundOpacity");
        UniformColorLabel.Text = TextCatalog.Get(language, "UniformColor");
        UniformCustomItem.Content = TextCatalog.Get(language, "CustomColor");
        UniformLightItem.Content = TextCatalog.Get(language, "ColorLight");
        UniformCyanItem.Content = TextCatalog.Get(language, "ColorCyan");
        UniformGreenItem.Content = TextCatalog.Get(language, "ColorGreen");
        UniformAmberItem.Content = TextCatalog.Get(language, "ColorAmber");
        UniformRedItem.Content = TextCatalog.Get(language, "ColorRed");
        UniformVioletItem.Content = TextCatalog.Get(language, "ColorViolet");
        HotkeysTitleText.Text = TextCatalog.Get(language, "Hotkeys");
        ToggleOverlayHotkeyLabel.Text = TextCatalog.Get(language, "ToggleOverlayHotkey");
        BenchmarkHotkeyLabel.Text = TextCatalog.Get(language, "BenchmarkHotkey");
        ExitHotkeyLabel.Text = TextCatalog.Get(language, "ExitHotkey");
        OverlayFieldsTitleText.Text = TextCatalog.Get(language, "OverlayFields");
        FieldColorHeaderText.Text = TextCatalog.Get(language, "FieldColor");
        ValueColorHeaderText.Text = TextCatalog.Get(language, "ValueColor");
        ShowTargetCheckBox.Content = TextCatalog.Get(language, "TargetProcess");
        ShowStatusCheckBox.Content = TextCatalog.Get(language, "Status");
        ShowFpsCheckBox.Content = TextCatalog.Get(language, "Fps");
        ShowAverageFpsCheckBox.Content = TextCatalog.Get(language, "AverageFps");
        ShowOnePercentLowCheckBox.Content = TextCatalog.Get(language, "OnePercentLow");
        ShowCpuCheckBox.Content = TextCatalog.Get(language, "Cpu");
        ShowGpuCheckBox.Content = TextCatalog.Get(language, "Gpu");
        ShowVramCheckBox.Content = TextCatalog.Get(language, "Vram");
        ShowMemoryCheckBox.Content = TextCatalog.Get(language, "Memory");
        ShowFrameTimeCheckBox.Content = TextCatalog.Get(language, "FrameTime");
        CloseButton.Content = TextCatalog.Get(language, "Close");
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || LanguageComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var language = string.Equals((string?)item.Tag, nameof(AppLanguage.English), StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.English
            : AppLanguage.Chinese;

        _store.Update(settings => settings.Language = language);
        SetStatus(TextCatalog.Get(language, "Saved"));
    }

    private void OnLayoutChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading || LayoutComboBox.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var layout = string.Equals((string?)item.Tag, nameof(OverlayLayout.Horizontal), StringComparison.OrdinalIgnoreCase)
            ? OverlayLayout.Horizontal
            : OverlayLayout.Vertical;

        _store.Update(settings => settings.Display.Layout = layout);
        SetStatus(TextCatalog.Get(_store.Current.Language, "Saved"));
    }

    private void OnUniformColorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoading ||
            UniformColorComboBox.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string hex ||
            string.Equals(hex, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _store.Update(settings =>
        {
            settings.Display.FontColorHex = hex;
            foreach (var fieldId in OverlayFieldDefaults.FieldIds)
            {
                var field = settings.Display.GetFieldSettings(fieldId);
                field.LabelColorHex = hex;
                field.ValueColorHex = hex;
            }
        });
        SetStatus(TextCatalog.Get(_store.Current.Language, "Saved"));
    }

    private void OnFieldColorClick(object sender, RoutedEventArgs e)
    {
        if (_isLoading ||
            sender is not System.Windows.Controls.Button button ||
            button.Tag is not string tag)
        {
            return;
        }

        var parts = tag.Split('|');
        if (parts.Length != 2)
        {
            return;
        }

        var fieldId = parts[0];
        var target = parts[1];
        var current = _store.Current.Display.GetFieldSettings(fieldId);
        var currentHex = string.Equals(target, "Label", StringComparison.OrdinalIgnoreCase)
            ? current.LabelColorHex
            : current.ValueColorHex;

        using var dialog = new System.Windows.Forms.ColorDialog
        {
            FullOpen = true,
            Color = System.Drawing.ColorTranslator.FromHtml(currentHex)
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        var hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
        _store.Update(settings =>
        {
            var field = settings.Display.GetFieldSettings(fieldId);
            if (string.Equals(target, "Label", StringComparison.OrdinalIgnoreCase))
            {
                field.LabelColorHex = hex;
            }
            else
            {
                field.ValueColorHex = hex;
            }
        });
        SetStatus(TextCatalog.Get(_store.Current.Language, "Saved"));
    }

    private void OnOverlaySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoading ||
            OverlayScaleSlider is null ||
            BackgroundOpacitySlider is null ||
            OverlayScaleValueText is null ||
            BackgroundOpacityValueText is null)
        {
            return;
        }

        var scale = (int)Math.Round(OverlayScaleSlider.Value);
        var opacity = (int)Math.Round(BackgroundOpacitySlider.Value);
        OverlayScaleValueText.Text = $"{scale}%";
        BackgroundOpacityValueText.Text = $"{opacity}%";

        _store.Update(settings =>
        {
            settings.Display.OverlayScalePercent = scale;
            settings.Display.BackgroundOpacityPercent = opacity;
        });
        SetStatus(TextCatalog.Get(_store.Current.Language, "Saved"));
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading)
        {
            return;
        }

        if (ReferenceEquals(sender, StartWithWindowsCheckBox))
        {
            UpdateStartup();
            return;
        }

        _store.Update(settings =>
        {
            settings.Display.ShowTarget = ShowTargetCheckBox.IsChecked == true;
            settings.Display.ShowStatus = ShowStatusCheckBox.IsChecked == true;
            settings.Display.ShowFps = ShowFpsCheckBox.IsChecked == true;
            settings.Display.ShowAverageFps = ShowAverageFpsCheckBox.IsChecked == true;
            settings.Display.ShowOnePercentLow = ShowOnePercentLowCheckBox.IsChecked == true;
            settings.Display.ShowCpu = ShowCpuCheckBox.IsChecked == true;
            settings.Display.ShowGpu = ShowGpuCheckBox.IsChecked == true;
            settings.Display.ShowVram = ShowVramCheckBox.IsChecked == true;
            settings.Display.ShowMemory = ShowMemoryCheckBox.IsChecked == true;
            settings.Display.ShowFrameTime = ShowFrameTimeCheckBox.IsChecked == true;
            settings.Display.ShowFooter = settings.Display.ShowMemory || settings.Display.ShowFrameTime;
        });
        SetStatus(TextCatalog.Get(_store.Current.Language, "Saved"));
    }

    private void UpdateStartup()
    {
        var enabled = StartWithWindowsCheckBox.IsChecked == true;
        try
        {
            StartupManager.SetEnabled(enabled);
            _store.Update(settings => settings.StartWithWindows = enabled);
            SetStatus(TextCatalog.Get(_store.Current.Language, enabled ? "StartupEnabled" : "StartupDisabled"));
        }
        catch (Exception ex)
        {
            _isLoading = true;
            try
            {
                StartWithWindowsCheckBox.IsChecked = _store.Current.StartWithWindows;
            }
            finally
            {
                _isLoading = false;
            }

            SetStatus($"{TextCatalog.Get(_store.Current.Language, "StartupFailed")}: {ex.Message}");
        }
    }

    private void OnHotkeyGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.Text = TextCatalog.Get(_store.Current.Language, "PressShortcut");
            textBox.SelectAll();
        }
    }

    private void OnHotkeyPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        e.Handled = true;
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        var target = GetHotkeyTarget(textBox);
        if (target is null)
        {
            return;
        }

        var hotkey = HotkeyFormatter.FromKeyEvent(e);
        if (hotkey.Key == 0)
        {
            textBox.Text = TextCatalog.Get(_store.Current.Language, "PressShortcut");
            return;
        }

        if (IsDuplicateHotkey(target.Value, hotkey))
        {
            LoadSettings(_store.Current);
            SetStatus(TextCatalog.Get(_store.Current.Language, "DuplicateHotkey"));
            return;
        }

        _store.Update(settings =>
        {
            switch (target.Value)
            {
                case HotkeyTarget.ToggleOverlay:
                    settings.Hotkeys.ToggleOverlay = hotkey;
                    break;
                case HotkeyTarget.ToggleBenchmark:
                    settings.Hotkeys.ToggleBenchmark = hotkey;
                    break;
                case HotkeyTarget.Exit:
                    settings.Hotkeys.Exit = hotkey;
                    break;
            }
        });
        SetStatus($"{TextCatalog.Get(_store.Current.Language, "HotkeySaved")}: {HotkeyFormatter.Format(hotkey)}");
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateOverlayValueText(DisplaySettings display)
    {
        OverlayScaleValueText.Text = $"{display.OverlayScalePercent}%";
        BackgroundOpacityValueText.Text = $"{display.BackgroundOpacityPercent}%";
    }

    private void UpdateUniformColorSelection(DisplaySettings display)
    {
        var colors = OverlayFieldDefaults.FieldIds
            .SelectMany(fieldId =>
            {
                var field = display.GetFieldSettings(fieldId);
                return new[] { field.LabelColorHex, field.ValueColorHex };
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (colors.Length == 1)
        {
            foreach (ComboBoxItem item in UniformColorComboBox.Items)
            {
                if (string.Equals((string?)item.Tag, colors[0], StringComparison.OrdinalIgnoreCase))
                {
                    UniformColorComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        UniformColorComboBox.SelectedItem = UniformCustomItem;
    }

    private void UpdateFieldColorButtons(DisplaySettings display)
    {
        foreach (var button in GetFieldColorButtons())
        {
            if (button.Tag is not string tag)
            {
                continue;
            }

            var parts = tag.Split('|');
            if (parts.Length != 2)
            {
                continue;
            }

            var field = display.GetFieldSettings(parts[0]);
            var hex = string.Equals(parts[1], "Label", StringComparison.OrdinalIgnoreCase)
                ? field.LabelColorHex
                : field.ValueColorHex;
            SetColorButton(button, hex);
        }
    }

    private static void SetColorButton(System.Windows.Controls.Button button, string hex)
    {
        var color = System.Windows.Media.ColorConverter.ConvertFromString(hex) is System.Windows.Media.Color parsed
            ? parsed
            : System.Windows.Media.Color.FromRgb(0xF5, 0xF7, 0xFB);
        button.Background = new System.Windows.Media.SolidColorBrush(color);
        button.BorderThickness = new Thickness(1);
        button.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 58, 82));
        button.ToolTip = hex;
    }

    private System.Windows.Controls.Button[] GetFieldColorButtons()
    {
        return
        [
            FpsLabelColorButton,
            FpsValueColorButton,
            AverageFpsLabelColorButton,
            AverageFpsValueColorButton,
            OnePercentLowLabelColorButton,
            OnePercentLowValueColorButton,
            CpuLabelColorButton,
            CpuValueColorButton,
            GpuLabelColorButton,
            GpuValueColorButton,
            VramLabelColorButton,
            VramValueColorButton,
            MemoryLabelColorButton,
            MemoryValueColorButton,
            FrameTimeLabelColorButton,
            FrameTimeValueColorButton
        ];
    }

    private HotkeyTarget? GetHotkeyTarget(System.Windows.Controls.TextBox textBox)
    {
        if (ReferenceEquals(textBox, ToggleOverlayHotkeyTextBox))
        {
            return HotkeyTarget.ToggleOverlay;
        }

        if (ReferenceEquals(textBox, BenchmarkHotkeyTextBox))
        {
            return HotkeyTarget.ToggleBenchmark;
        }

        if (ReferenceEquals(textBox, ExitHotkeyTextBox))
        {
            return HotkeyTarget.Exit;
        }

        return null;
    }

    private bool IsDuplicateHotkey(HotkeyTarget target, HotkeySetting hotkey)
    {
        var current = _store.Current.Hotkeys;
        return target != HotkeyTarget.ToggleOverlay && hotkey.Equals(current.ToggleOverlay) ||
               target != HotkeyTarget.ToggleBenchmark && hotkey.Equals(current.ToggleBenchmark) ||
               target != HotkeyTarget.Exit && hotkey.Equals(current.Exit);
    }

    private enum HotkeyTarget
    {
        ToggleOverlay,
        ToggleBenchmark,
        Exit
    }
}
