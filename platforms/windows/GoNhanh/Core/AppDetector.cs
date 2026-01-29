using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GoNhanh.Core;

/// <summary>
/// Injection method for text sending based on target application
/// </summary>
public enum InjectionMethod
{
    Fast,           // Default: minimal delays
    Slow,           // Electron/terminals: higher delays
    Selection,      // Browser address bars: Shift+Left + type
    CharByChar      // Problematic apps: one char at a time
}

/// <summary>
/// Delay configuration for text injection (in microseconds)
/// </summary>
public readonly struct InjectionDelays
{
    /// <summary>Delay between backspace key presses (microseconds)</summary>
    public int BackspaceDelayUs { get; }
    /// <summary>Delay between backspaces and text insertion (microseconds)</summary>
    public int WaitDelayUs { get; }
    /// <summary>Delay between text character insertions (microseconds)</summary>
    public int TextDelayUs { get; }

    public InjectionDelays(int backspaceUs, int waitUs, int textUs)
    {
        BackspaceDelayUs = backspaceUs;
        WaitDelayUs = waitUs;
        TextDelayUs = textUs;
    }

    // Convert microseconds to milliseconds for Thread.Sleep using ceiling division
    public int BackspaceDelayMs => BackspaceDelayUs > 0 ? (BackspaceDelayUs + 999) / 1000 : 0;
    public int WaitDelayMs => WaitDelayUs > 0 ? (WaitDelayUs + 999) / 1000 : 0;
    public int TextDelayMs => TextDelayUs > 0 ? (TextDelayUs + 999) / 1000 : 0;

    public static InjectionDelays Fast => new(200, 800, 500);
    public static InjectionDelays Default => new(1000, 3000, 1500);
    public static InjectionDelays Slow => new(3000, 8000, 3000);
    public static InjectionDelays Electron => new(8000, 25000, 8000);
}

/// <summary>
/// Detects foreground application and determines appropriate text injection method
/// Similar to macOS RustBridge.swift app detection
/// </summary>
public static class AppDetector
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // Cache to avoid repeated process lookups (500ms TTL)
    private static string? _cachedProcessName;
    private static DateTime _cacheTime;
    private static readonly TimeSpan CacheTTL = TimeSpan.FromMilliseconds(500);
    private static readonly object _cacheLock = new();

    /// <summary>
    /// Detect appropriate injection method based on foreground application
    /// </summary>
    public static (InjectionMethod Method, InjectionDelays Delays) DetectMethod()
    {
        var processName = GetForegroundProcessName();
        if (string.IsNullOrEmpty(processName))
            return (InjectionMethod.Fast, InjectionDelays.Fast);

        return processName.ToLowerInvariant() switch
        {
            // Electron apps - higher delays needed
            "code" or "cursor" or "claude" or "slack" or "discord" or "notion" =>
                (InjectionMethod.Slow, InjectionDelays.Electron),

            // Terminals - higher delays
            "windowsterminal" or "cmd" or "powershell" or "pwsh" or 
            "wezterm" or "wezterm-gui" or "alacritty" or "hyper" or "conemu64" =>
                (InjectionMethod.Slow, InjectionDelays.Electron),

            // Browsers - use selection method for address bar compatibility
            "chrome" or "msedge" or "firefox" or "brave" or "opera" or "vivaldi" or "arc" =>
                (InjectionMethod.Selection, InjectionDelays.Default),

            // Office apps - moderate delays
            "winword" or "excel" or "powerpnt" or "outlook" =>
                (InjectionMethod.Slow, InjectionDelays.Slow),

            // Default - fast method for most apps
            _ => (InjectionMethod.Fast, InjectionDelays.Fast)
        };
    }

    /// <summary>
    /// Get the process name of the foreground window with caching
    /// </summary>
    public static string? GetForegroundProcessName()
    {
        lock (_cacheLock)
        {
            // Check cache first
            if (_cachedProcessName != null && DateTime.Now - _cacheTime < CacheTTL)
                return _cachedProcessName;

            try
            {
                var hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return null;

                GetWindowThreadProcessId(hwnd, out uint processId);
                if (processId == 0) return null;

                using var process = Process.GetProcessById((int)processId);
                _cachedProcessName = process.ProcessName;
                _cacheTime = DateTime.Now;
                return _cachedProcessName;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Invalidate the process name cache (call on app switch)
    /// </summary>
    public static void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedProcessName = null;
        }
    }
}
