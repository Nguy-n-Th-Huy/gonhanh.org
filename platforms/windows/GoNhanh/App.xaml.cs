using System.Windows;
using GoNhanh.Core;
using GoNhanh.Services;
using GoNhanh.Views;

namespace GoNhanh;

/// <summary>
/// GoNhanh - Vietnamese Input Method for Windows
/// Main application entry point
/// Matches macOS App.swift flow
/// </summary>
public partial class App : System.Windows.Application
{
    private TrayIcon? _trayIcon;
    private KeyboardHook? _keyboardHook;
    private PerAppModeManager? _perAppModeManager;
    private readonly SettingsService _settings = new();
    private System.Threading.Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent multiple instances
        if (!EnsureSingleInstance())
        {
            Shutdown();
            return;
        }

        // Initialize Rust core engine
        RustBridge.Initialize();

        // Load settings
        _settings.Load();
        ApplySettings();

        // Initialize per-app mode manager
        _perAppModeManager = new PerAppModeManager(_settings);
        _perAppModeManager.IsEnabled = _settings.PerAppModeEnabled;
        _perAppModeManager.OnStateRestored += OnPerAppStateRestored;
        _perAppModeManager.Start();

        // Initialize keyboard hook
        _keyboardHook = new KeyboardHook();
        _keyboardHook.KeyPressed += OnKeyPressed;
        _keyboardHook.ToggleRequested += OnToggleRequested;
        _keyboardHook.RestoreRequested += OnRestoreRequested;

        // Apply shortcut settings to keyboard hook
        _keyboardHook.SetToggleShortcut(_settings.ToggleShortcut);
        _keyboardHook.SetRestoreShortcut(_settings.RestoreShortcut);
        _keyboardHook.SetRestoreShortcutEnabled(_settings.RestoreShortcutEnabled);

        _keyboardHook.Start();

        // Initialize system tray
        _trayIcon = new TrayIcon();
        _trayIcon.OnExitRequested += ExitApplication;
        _trayIcon.OnMethodChanged += ChangeInputMethod;
        _trayIcon.OnEnabledChanged += ToggleEnabled;
        _trayIcon.OnCheckUpdateRequested += ShowUpdateWindow;
        _trayIcon.OnSettingsRequested += ShowSettingsWindow;
        _trayIcon.Initialize(_settings.CurrentMethod, _settings.IsEnabled);

        // Show onboarding if first run (like macOS)
        if (_settings.IsFirstRun)
        {
            ShowOnboarding();
        }
    }

    private bool EnsureSingleInstance()
    {
        _mutex = new System.Threading.Mutex(true, "GoNhanh_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                $"{AppMetadata.Name} đang chạy.\nKiểm tra khay hệ thống (system tray).",
                AppMetadata.Name,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }
        return true;
    }

    private void ApplySettings()
    {
        RustBridge.SetMethod(_settings.CurrentMethod);
        RustBridge.SetEnabled(_settings.IsEnabled);
        RustBridge.SetModernTone(_settings.UseModernTone);
    }

    private void OnKeyPressed(object? sender, KeyPressedEventArgs e)
    {
        if (!_settings.IsEnabled) return;

        // Detect appropriate injection method for current app
        var (method, delays) = AppDetector.DetectMethod();

        var result = RustBridge.ProcessKey(e.VirtualKeyCode, e.Shift, e.CapsLock);

        // Debug log
        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "gonhanh-debug.log");
        var processName = AppDetector.GetForegroundProcessName() ?? "unknown";
        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] [{processName}] VK={e.VirtualKeyCode} -> Action={result.Action}, BS={result.Backspace}, Count={result.Count}, Text='{result.GetText()}', Method={method}\n");

        if (result.Action == ImeAction.Send && result.Count > 0)
        {
            e.Handled = true;
            TextSender.SendText(result.GetText(), result.Backspace, method, delays);
        }
        else if (result.Action == ImeAction.Restore)
        {
            e.Handled = true;
            TextSender.SendText(result.GetText(), result.Backspace, method, delays);
        }
    }

    private void ShowOnboarding()
    {
        var onboarding = new OnboardingWindow(_settings);
        onboarding.ShowDialog();

        // Save settings after onboarding
        _settings.IsFirstRun = false;
        _settings.Save();

        ApplySettings();
        _trayIcon?.UpdateState(_settings.CurrentMethod, _settings.IsEnabled);
    }

    private void ChangeInputMethod(InputMethod method)
    {
        _settings.CurrentMethod = method;
        _settings.Save();
        RustBridge.SetMethod(method);
    }

    private void ToggleEnabled(bool enabled)
    {
        _settings.IsEnabled = enabled;
        _settings.Save();
        RustBridge.SetEnabled(enabled);
    }

    private void OnToggleRequested(object? sender, EventArgs e)
    {
        // Toggle IME enabled state via shortcut (e.g., Ctrl+Space)
        var newState = !_settings.IsEnabled;
        _settings.IsEnabled = newState;
        _settings.Save();
        RustBridge.SetEnabled(newState);
        RustBridge.Clear(); // Clear buffer on toggle
        _trayIcon?.UpdateState(_settings.CurrentMethod, newState);
    }

    private void OnRestoreRequested(object? sender, EventArgs e)
    {
        // Restore original text via shortcut (e.g., ESC)
        // This triggers the Rust engine's restore functionality
        RustBridge.Clear();
    }

    private void OnPerAppStateRestored(bool enabled)
    {
        _trayIcon?.UpdateState(_settings.CurrentMethod, enabled);
    }

    private void ShowUpdateWindow()
    {
        var updateWindow = new UpdateWindow();
        updateWindow.ShowDialog();
    }

    private void ShowSettingsWindow()
    {
        var settingsWindow = new SettingsWindow(_settings, OnSettingsApplied);
        settingsWindow.ShowDialog();
    }

    private void OnSettingsApplied(SettingsService settings)
    {
        // Apply settings to Rust engine
        RustBridge.SetMethod(settings.CurrentMethod);
        RustBridge.SetEnabled(settings.IsEnabled);
        RustBridge.SetModernTone(settings.UseModernTone);
        RustBridge.SetEscRestore(settings.RestoreShortcutEnabled);

        // Update keyboard hook shortcuts
        _keyboardHook?.SetToggleShortcut(settings.ToggleShortcut);
        _keyboardHook?.SetRestoreShortcut(settings.RestoreShortcut);
        _keyboardHook?.SetRestoreShortcutEnabled(settings.RestoreShortcutEnabled);

        // Update per-app mode
        if (_perAppModeManager != null)
            _perAppModeManager.IsEnabled = settings.PerAppModeEnabled;

        // Update tray icon
        _trayIcon?.UpdateState(settings.CurrentMethod, settings.IsEnabled);
    }

    private void ExitApplication()
    {
        _keyboardHook?.Stop();
        _keyboardHook?.Dispose();
        _perAppModeManager?.Dispose();
        _trayIcon?.Dispose();
        RustBridge.Clear();
        _mutex?.Dispose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _keyboardHook?.Dispose();
        _perAppModeManager?.Dispose();
        _trayIcon?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
