using System.Windows.Input;
using GamePulseMonitor.Interop;

namespace GamePulseMonitor.Settings;

internal static class HotkeyFormatter
{
    public static string Format(HotkeySetting hotkey)
    {
        if (hotkey.Key == 0)
        {
            return "";
        }

        var parts = new List<string>();
        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (hotkey.Modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        parts.Add(FormatKey(hotkey.Key));
        return string.Join("+", parts);
    }

    public static HotkeySetting FromKeyEvent(System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }

        if (IsModifierOnlyKey(key))
        {
            return new HotkeySetting();
        }

        var modifiers = HotkeyModifiers.None;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return virtualKey <= 0 ? new HotkeySetting() : new HotkeySetting(modifiers, (uint)virtualKey);
    }

    public static bool IsPressed(HotkeySetting hotkey)
    {
        if (hotkey.Key == 0)
        {
            return false;
        }

        return IsModifierPressed(HotkeyModifiers.Control) == hotkey.Modifiers.HasFlag(HotkeyModifiers.Control) &&
               IsModifierPressed(HotkeyModifiers.Shift) == hotkey.Modifiers.HasFlag(HotkeyModifiers.Shift) &&
               IsModifierPressed(HotkeyModifiers.Alt) == hotkey.Modifiers.HasFlag(HotkeyModifiers.Alt) &&
               IsModifierPressed(HotkeyModifiers.Win) == hotkey.Modifiers.HasFlag(HotkeyModifiers.Win) &&
               NativeMethods.IsKeyPressed((int)hotkey.Key);
    }

    private static bool IsModifierPressed(HotkeyModifiers modifier)
    {
        return modifier switch
        {
            HotkeyModifiers.Control => NativeMethods.IsKeyPressed(0x11),
            HotkeyModifiers.Shift => NativeMethods.IsKeyPressed(0x10),
            HotkeyModifiers.Alt => NativeMethods.IsAltPressed(),
            HotkeyModifiers.Win => NativeMethods.IsKeyPressed(0x5B) || NativeMethods.IsKeyPressed(0x5C),
            _ => false
        };
    }

    private static string FormatKey(uint virtualKey)
    {
        return virtualKey switch
        {
            >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
            >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
            >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
            0x20 => "Space",
            0x21 => "PageUp",
            0x22 => "PageDown",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left",
            0x26 => "Up",
            0x27 => "Right",
            0x28 => "Down",
            0x2D => "Insert",
            0x2E => "Delete",
            _ => KeyInterop.KeyFromVirtualKey((int)virtualKey).ToString()
        };
    }

    private static bool IsModifierOnlyKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftShift or Key.RightShift or
            Key.LeftAlt or Key.RightAlt or
            Key.LWin or Key.RWin or
            Key.System or Key.None;
    }
}
