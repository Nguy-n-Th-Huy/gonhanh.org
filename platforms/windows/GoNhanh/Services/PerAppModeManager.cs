using System.Runtime.InteropServices;
using Microsoft.Win32;
using GoNhanh.Core;

namespace GoNhanh.Services;

/// <summary>
/// Manages per-application IME state (ON/OFF) with automatic save/restore on app switch.
/// Uses WinEvent hook for event-driven app switch detection (no polling).
/// Similar to macOS RustBridge.swift per-app mode.
/// </summary>
public class PerAppModeManager : IDisposable
{
    private const string RegistryPath = @"SOFTWARE\GoNhanh\PerAppMode";

    #region Win32 Imports

    private delegate void WinEventDelegate(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(
        uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess,
        uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    private const uint EVENT_SYSTEM_FOREGROUND = 3;
    private const uint WINEVENT_OUTOFCONTEXT = 0;

    #endregion

    #region Fields

    private IntPtr _hook;
    private WinEventDelegate? _delegate; // Must keep alive to prevent GC
    private string? _currentProcessName;
    private readonly SettingsService _settings;
    private bool _disposed;
    private readonly object _lock = new();

    #endregion

    #region Properties

    /// <summary>
    /// Enable/disable per-app mode. When disabled, hook is not active.
    /// </summary>
    public bool IsEnabled { get; set; }

    #endregion

    #region Events

    /// <summary>
    /// Fired when IME state is restored for an app. UI should update tray icon.
    /// </summary>
    public event Action<bool>? OnStateRestored;

    #endregion

    #region Constructor

    public PerAppModeManager(SettingsService settings)
    {
        _settings = settings;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Start monitoring app switches via WinEvent hook
    /// </summary>
    public void Start()
    {
        if (!IsEnabled) return;

        lock (_lock)
        {
            if (_hook != IntPtr.Zero) return; // Already started

            // Keep delegate alive to prevent GC collection
            _delegate = OnForegroundChanged;
            _hook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _delegate, 0, 0, WINEVENT_OUTOFCONTEXT);

            // Validate hook was created successfully
            if (_hook == IntPtr.Zero)
            {
                System.Diagnostics.Debug.WriteLine("PerAppModeManager: Failed to create WinEvent hook");
                _delegate = null;
                return;
            }

            // Handle initial foreground app
            var processName = AppDetector.GetForegroundProcessName();
            if (!string.IsNullOrEmpty(processName))
            {
                _currentProcessName = processName;
                RestoreState(processName);
            }
        }
    }

    /// <summary>
    /// Stop monitoring app switches
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
            _delegate = null;
        }
    }

    /// <summary>
    /// Restart monitoring (call after IsEnabled changes)
    /// </summary>
    public void Restart()
    {
        Stop();
        if (IsEnabled)
            Start();
    }

    #endregion

    #region Private Methods

    private void OnForegroundChanged(
        IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
        int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (!IsEnabled) return;

        // Invalidate cache first to get fresh process name
        AppDetector.InvalidateCache();
        var processName = AppDetector.GetForegroundProcessName();

        if (string.IsNullOrEmpty(processName) || processName == _currentProcessName)
            return;

        // Lock entire state transition to prevent race conditions
        lock (_lock)
        {
            // Double-check after acquiring lock
            if (processName == _currentProcessName) return;

            // Save current state before switching
            if (!string.IsNullOrEmpty(_currentProcessName))
                SaveState(_currentProcessName, _settings.IsEnabled);

            _currentProcessName = processName;

            // Clear IME buffer on app switch (rhythm break)
            RustBridge.Clear();

            // Restore or initialize state for new app
            if (HasSavedState(processName))
                RestoreState(processName);
            else
                SaveState(processName, _settings.IsEnabled);
        }
    }

    private bool HasSavedState(string processName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            return key?.GetValue(processName) != null;
        }
        catch
        {
            return false;
        }
    }

    private void SaveState(string processName, bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
            key?.SetValue(processName, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PerAppModeManager: Failed to save state for {processName}: {ex.Message}");
        }
    }

    private void RestoreState(string processName)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var value = key?.GetValue(processName);
            if (value is int intValue)
            {
                var enabled = intValue == 1;
                _settings.IsEnabled = enabled;
                RustBridge.SetEnabled(enabled);
                OnStateRestored?.Invoke(enabled);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PerAppModeManager: Failed to restore state for {processName}: {ex.Message}");
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion
}
