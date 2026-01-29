using System.Text;
using Microsoft.Win32;

namespace GoNhanh.Core;

/// <summary>
/// Modifier keys for keyboard shortcuts.
/// Custom enum to avoid dependency on System.Windows.Forms in WPF app.
/// Values match System.Windows.Forms.Keys for compatibility.
/// </summary>
[Flags]
public enum ModifierKeys
{
    None = 0,
    Shift = 0x10000,
    Control = 0x20000,
    Alt = 0x40000
}

/// <summary>
/// Represents a keyboard shortcut with key code and modifiers.
/// Supports persistence via Windows Registry.
/// </summary>
public class KeyboardShortcut
{
    private const string RegistryPath = @"SOFTWARE\GoNhanh";

    /// <summary>Virtual key code (0xFFFF = modifier-only shortcut)</summary>
    public ushort KeyCode { get; set; }

    /// <summary>Modifier keys (Ctrl, Alt, Shift)</summary>
    public ModifierKeys Modifiers { get; set; }

    /// <summary>True if this is a modifier-only shortcut (no main key)</summary>
    public bool IsModifierOnly => KeyCode == 0xFFFF;

    public KeyboardShortcut(ushort keyCode = 0, ModifierKeys modifiers = ModifierKeys.None)
    {
        KeyCode = keyCode;
        Modifiers = modifiers;
    }

    /// <summary>
    /// Check if the given key combination matches this shortcut
    /// </summary>
    public bool Matches(ushort keyCode, bool ctrl, bool alt, bool shift)
    {
        if (IsModifierOnly) return false;
        if (KeyCode != keyCode) return false;

        var expectedCtrl = Modifiers.HasFlag(ModifierKeys.Control);
        var expectedAlt = Modifiers.HasFlag(ModifierKeys.Alt);
        var expectedShift = Modifiers.HasFlag(ModifierKeys.Shift);

        return ctrl == expectedCtrl && alt == expectedAlt && shift == expectedShift;
    }

    /// <summary>
    /// Check if the given modifiers match this modifier-only shortcut
    /// </summary>
    public bool MatchesModifierOnly(bool ctrl, bool alt, bool shift)
    {
        if (!IsModifierOnly) return false;

        var expectedCtrl = Modifiers.HasFlag(ModifierKeys.Control);
        var expectedAlt = Modifiers.HasFlag(ModifierKeys.Alt);
        var expectedShift = Modifiers.HasFlag(ModifierKeys.Shift);

        return ctrl == expectedCtrl && alt == expectedAlt && shift == expectedShift;
    }

    /// <summary>
    /// Human-readable display string (e.g., "Ctrl+Space", "Alt+Shift+A")
    /// </summary>
    public string DisplayString
    {
        get
        {
            var sb = new StringBuilder();
            if (Modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl+");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt+");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift+");

            if (!IsModifierOnly)
            {
                sb.Append(KeyCodeToString(KeyCode));
            }
            else if (sb.Length > 0)
            {
                sb.Length--; // Remove trailing +
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// Convert virtual key code to human-readable string
    /// </summary>
    private static string KeyCodeToString(ushort keyCode) => keyCode switch
    {
        KeyCodes.VK_SPACE => "Space",
        KeyCodes.VK_ESCAPE => "ESC",
        KeyCodes.VK_TAB => "Tab",
        KeyCodes.VK_RETURN => "Enter",
        KeyCodes.VK_BACK => "Backspace",
        >= 0x41 and <= 0x5A => ((char)keyCode).ToString(), // A-Z
        >= 0x30 and <= 0x39 => ((char)keyCode).ToString(), // 0-9
        >= 0x70 and <= 0x7B => $"F{keyCode - 0x6F}",       // F1-F12
        KeyCodes.VK_LEFT => "Left",
        KeyCodes.VK_RIGHT => "Right",
        KeyCodes.VK_UP => "Up",
        KeyCodes.VK_DOWN => "Down",
        _ => $"Key{keyCode:X2}"
    };

    #region Registry Persistence

    /// <summary>
    /// Save shortcut to Windows Registry
    /// </summary>
    public void Save(string keyName)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue($"{keyName}_KeyCode", (int)KeyCode, RegistryValueKind.DWord);
            key?.SetValue($"{keyName}_Modifiers", (int)Modifiers, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"KeyboardShortcut: Failed to save {keyName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Load shortcut from Windows Registry
    /// </summary>
    public static KeyboardShortcut Load(string keyName, KeyboardShortcut defaultValue)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            if (key == null) return defaultValue;

            var keyCodeValue = key.GetValue($"{keyName}_KeyCode");
            var modifiersValue = key.GetValue($"{keyName}_Modifiers");

            if (keyCodeValue is int kc && modifiersValue is int mod)
                return new KeyboardShortcut((ushort)kc, (ModifierKeys)mod);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"KeyboardShortcut: Failed to load {keyName}: {ex.Message}");
        }

        return defaultValue;
    }

    #endregion

    #region Default Shortcuts

    /// <summary>Default toggle shortcut: Ctrl+Space</summary>
    public static KeyboardShortcut DefaultToggle => new(KeyCodes.VK_SPACE, ModifierKeys.Control);

    /// <summary>Default restore shortcut: ESC (no modifiers)</summary>
    public static KeyboardShortcut DefaultRestore => new(KeyCodes.VK_ESCAPE, ModifierKeys.None);

    #endregion
}
