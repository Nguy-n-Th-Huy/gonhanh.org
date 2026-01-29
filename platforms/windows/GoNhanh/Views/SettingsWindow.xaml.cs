using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using GoNhanh.Core;
using GoNhanh.Services;

namespace GoNhanh.Views;

// Alias to avoid ambiguity with System.Windows.Input
using WpfModifierKeys = System.Windows.Input.ModifierKeys;

/// <summary>
/// Settings window with tabbed UI for all app settings.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settings;
    private readonly Action<SettingsService>? _onSettingsChanged;
    private bool _isLoading = true;

    public SettingsWindow(SettingsService settings, Action<SettingsService>? onSettingsChanged = null)
    {
        InitializeComponent();
        _settings = settings;
        _onSettingsChanged = onSettingsChanged;

        LoadSettings();
        LoadAboutInfo();
        _isLoading = false;
    }

    private void LoadSettings()
    {
        // General
        MethodCombo.SelectedIndex = (int)_settings.CurrentMethod;
        AutoStartCheck.IsChecked = _settings.AutoStart;
        PerAppModeCheck.IsChecked = _settings.PerAppModeEnabled;

        // Typing
        ModernToneCheck.IsChecked = _settings.UseModernTone;
        BracketCheck.IsChecked = false; // TODO: Add to SettingsService if needed
        AutoRestoreCheck.IsChecked = false; // TODO: Add to SettingsService if needed
        AutoCapCheck.IsChecked = false; // TODO: Add to SettingsService if needed

        // Shortcuts
        ToggleShortcutBox.Text = _settings.ToggleShortcut.DisplayString;
        RestoreEnabledCheck.IsChecked = _settings.RestoreShortcutEnabled;
        RestoreShortcutBox.Text = _settings.RestoreShortcut.DisplayString;
        RestoreShortcutBox.IsEnabled = _settings.RestoreShortcutEnabled;
    }

    private void LoadAboutInfo()
    {
        VersionText.Text = $"Phiên bản {AppMetadata.Version}";
        WebsiteLink.NavigateUri = new Uri(AppMetadata.Website);
        GitHubLink.NavigateUri = new Uri(AppMetadata.Repository);
        IssuesLink.NavigateUri = new Uri(AppMetadata.IssuesUrl);
        CopyrightText.Text = AppMetadata.Copyright;
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        GeneralSection.Visibility = TabGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        TypingSection.Visibility = TabTyping.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ShortcutsSection.Visibility = TabShortcuts.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        AboutSection.Visibility = TabAbout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMethodChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoading) return;

        _settings.CurrentMethod = (Core.InputMethod)MethodCombo.SelectedIndex;
        SaveAndApply();
    }

    private void OnSettingChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoading) return;

        // General
        _settings.AutoStart = AutoStartCheck.IsChecked == true;
        _settings.PerAppModeEnabled = PerAppModeCheck.IsChecked == true;

        // Typing
        _settings.UseModernTone = ModernToneCheck.IsChecked == true;

        // Shortcuts
        _settings.RestoreShortcutEnabled = RestoreEnabledCheck.IsChecked == true;
        RestoreShortcutBox.IsEnabled = _settings.RestoreShortcutEnabled;

        SaveAndApply();
    }

    private void OnShortcutFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox textBox)
        {
            textBox.Text = "Nhấn phím tắt...";
        }
    }

    private void OnShortcutKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox) return;

        e.Handled = true;

        // Get modifiers (WPF ModifierKeys)
        var wpfModifiers = Keyboard.Modifiers;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // ESC to cancel (without modifiers)
        if (key == Key.Escape && wpfModifiers == WpfModifierKeys.None)
        {
            textBox.Text = textBox.Tag?.ToString() == "Toggle"
                ? _settings.ToggleShortcut.DisplayString
                : _settings.RestoreShortcut.DisplayString;
            Keyboard.ClearFocus();
            return;
        }

        // Convert WPF ModifierKeys to Core.ModifierKeys
        var coreModifiers = Core.ModifierKeys.None;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Control))
            coreModifiers |= Core.ModifierKeys.Control;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Alt))
            coreModifiers |= Core.ModifierKeys.Alt;
        if (wpfModifiers.HasFlag(WpfModifierKeys.Shift))
            coreModifiers |= Core.ModifierKeys.Shift;

        // Check if this is a modifier-only press (Ctrl, Shift, Alt keys)
        bool isModifierKey = key == Key.LeftCtrl || key == Key.RightCtrl ||
                             key == Key.LeftAlt || key == Key.RightAlt ||
                             key == Key.LeftShift || key == Key.RightShift ||
                             key == Key.LWin || key == Key.RWin;

        KeyboardShortcut shortcut;
        if (isModifierKey && coreModifiers != Core.ModifierKeys.None)
        {
            // Modifier-only shortcut (e.g., Ctrl+Shift)
            // Need at least 2 modifiers for modifier-only shortcuts
            int modCount = 0;
            if (wpfModifiers.HasFlag(WpfModifierKeys.Control)) modCount++;
            if (wpfModifiers.HasFlag(WpfModifierKeys.Alt)) modCount++;
            if (wpfModifiers.HasFlag(WpfModifierKeys.Shift)) modCount++;

            if (modCount < 2)
            {
                // Single modifier - wait for more
                textBox.Text = coreModifiers.ToString() + "+...";
                return;
            }

            // Create modifier-only shortcut (KeyCode = 0xFFFF)
            shortcut = new KeyboardShortcut(0xFFFF, coreModifiers);
        }
        else
        {
            // Normal shortcut with main key
            var keyCode = (ushort)KeyInterop.VirtualKeyFromKey(key);
            shortcut = new KeyboardShortcut(keyCode, coreModifiers);
        }

        textBox.Text = shortcut.DisplayString;

        if (textBox.Tag?.ToString() == "Toggle")
        {
            _settings.ToggleShortcut = shortcut;
        }
        else
        {
            _settings.RestoreShortcut = shortcut;
        }

        SaveAndApply();
        Keyboard.ClearFocus();
    }

    private void SaveAndApply()
    {
        _settings.Save();
        _onSettingsChanged?.Invoke(_settings);
    }

    private void OnLinkClick(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore errors opening browser
        }
        e.Handled = true;
    }
}
