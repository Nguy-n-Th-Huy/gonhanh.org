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

    public static InjectionDelays Fast => new(1000, 2000, 1000);
    public static InjectionDelays Default => new(2000, 5000, 2000);
    public static InjectionDelays Slow => new(5000, 10000, 5000);
    public static InjectionDelays Electron => new(10000, 30000, 10000);
    public static InjectionDelays Browser => new(15000, 50000, 15000);
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
            // Electron apps - use slow method with electron delays
            "code" or "cursor" or "claude" or "slack" or "discord" or "notion" =>
                (InjectionMethod.Slow, InjectionDelays.Electron),

            // Terminals - use slow method with electron delays
            "windowsterminal" or "cmd" or "powershell" or "pwsh" or
            "wezterm" or "wezterm-gui" or "alacritty" or "hyper" or "conemu64" or
            "mintty" =>
                (InjectionMethod.Slow, InjectionDelays.Electron),

            // Browsers - use slow method with browser delays
            "chrome" or "msedge" or "firefox" or "brave" or "opera" or "vivaldi" or "arc" =>
                (InjectionMethod.Slow, InjectionDelays.Browser),

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
