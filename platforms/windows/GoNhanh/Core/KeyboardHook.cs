using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GoNhanh.Core;

/// <summary>
/// Low-level Windows keyboard hook for system-wide key interception
/// Similar to CGEventTap on macOS
/// </summary>
public class KeyboardHook : IDisposable
{
    #region Win32 Constants

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;
    private const uint LLKHF_INJECTED = 0x10;

    #endregion

    #region Win32 Imports

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    #endregion

    #region Structures

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    #endregion

    #region Fields

    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    // Flag to prevent recursive processing of injected keys
    private bool _isProcessing;

    // Identifier for our injected keys (to skip processing them)
    private static readonly UIntPtr InjectedKeyMarker = new UIntPtr(0x474E4820); // "GNH " in hex

    // Custom shortcuts
    private KeyboardShortcut _toggleShortcut = KeyboardShortcut.DefaultToggle;
    private KeyboardShortcut _restoreShortcut = KeyboardShortcut.DefaultRestore;
    private bool _restoreShortcutEnabled = true;

    // Track modifier-only shortcut state (for Ctrl+Shift toggle on release)
    private bool _modifierShortcutPending;
    private bool _otherKeyPressed;

    #endregion

    #region Events

    public event EventHandler<KeyPressedEventArgs>? KeyPressed;
    public event EventHandler? ToggleRequested;
    public event EventHandler? RestoreRequested;

    #endregion

    #region Shortcut Configuration

    /// <summary>Set the toggle shortcut (e.g., Ctrl+Space)</summary>
    public void SetToggleShortcut(KeyboardShortcut shortcut)
    {
        _toggleShortcut = shortcut;
        var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "gonhanh-debug.log");
        System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] SetToggleShortcut: KeyCode={shortcut.KeyCode:X}, Modifiers={shortcut.Modifiers}, Display={shortcut.DisplayString}\n");
    }

    /// <summary>Set the restore shortcut (e.g., ESC)</summary>
    public void SetRestoreShortcut(KeyboardShortcut shortcut) => _restoreShortcut = shortcut;

    /// <summary>Enable/disable the restore shortcut</summary>
    public void SetRestoreShortcutEnabled(bool enabled) => _restoreShortcutEnabled = enabled;

    #endregion

    #region Public Methods

    /// <summary>
    /// Start the keyboard hook
    /// </summary>
    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        _proc = HookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule!;

        _hookId = SetWindowsHookEx(
            WH_KEYBOARD_LL,
            _proc,
            GetModuleHandle(curModule.ModuleName!),
            0);

        if (_hookId == IntPtr.Zero)
        {
            int error = Marshal.GetLastWin32Error();
            throw new System.ComponentModel.Win32Exception(error, $"Failed to install keyboard hook. Error: {error}");
        }
    }

    /// <summary>
    /// Stop the keyboard hook
    /// </summary>
    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    #endregion

    #region Private Methods

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Don't process if already processing (prevents recursion)
        if (_isProcessing)
        {
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        if (nCode >= 0)
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Skip our own injected keys
            if (hookStruct.dwExtraInfo == InjectedKeyMarker)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Skip injected keys from other sources (optional, for safety)
            if ((hookStruct.flags & LLKHF_INJECTED) != 0)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            ushort keyCode = (ushort)hookStruct.vkCode;
            bool isKeyDown = wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN;
            bool isKeyUp = wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP;

            // Get modifier states for shortcut checking
            bool ctrl = IsKeyDown(KeyCodes.VK_CONTROL);
            bool alt = IsKeyDown(KeyCodes.VK_MENU);
            bool shift = IsKeyDown(KeyCodes.VK_SHIFT);

            // Handle modifier-only shortcuts (e.g., Ctrl+Shift)
            // Toggle on key release, only if no other keys were pressed
            if (_toggleShortcut.IsModifierOnly)
            {
                if (isKeyDown)
                {
                    if (IsModifierKey(keyCode))
                    {
                        // Check if modifiers match (need to check AFTER this key is pressed)
                        // For Ctrl+Shift: when Shift is pressed while Ctrl is held, or vice versa
                        bool willMatch = false;
                        var expectedCtrl = _toggleShortcut.Modifiers.HasFlag(ModifierKeys.Control);
                        var expectedAlt = _toggleShortcut.Modifiers.HasFlag(ModifierKeys.Alt);
                        var expectedShift = _toggleShortcut.Modifiers.HasFlag(ModifierKeys.Shift);

                        // Include the current key being pressed
                        bool ctrlActive = ctrl || keyCode == KeyCodes.VK_CONTROL || keyCode == 0xA2 || keyCode == 0xA3;
                        bool shiftActive = shift || keyCode == KeyCodes.VK_SHIFT || keyCode == 0xA0 || keyCode == 0xA1;
                        bool altActive = alt || keyCode == KeyCodes.VK_MENU || keyCode == 0xA4 || keyCode == 0xA5;

                        willMatch = (ctrlActive == expectedCtrl) && (shiftActive == expectedShift) && (altActive == expectedAlt);

                        if (willMatch)
                        {
                            _modifierShortcutPending = true;
                            _otherKeyPressed = false;
                        }
                    }
                    else
                    {
                        // Non-modifier key pressed, cancel pending shortcut
                        _otherKeyPressed = true;
                        _modifierShortcutPending = false;
                    }
                }
                else if (isKeyUp && IsModifierKey(keyCode))
                {
                    // Modifier released - check if we should toggle
                    if (_modifierShortcutPending && !_otherKeyPressed)
                    {
                        ToggleRequested?.Invoke(this, EventArgs.Empty);
                    }
                    _modifierShortcutPending = false;
                    _otherKeyPressed = false;
                }
            }

            // Process key down events
            if (isKeyDown)
            {
                // Check toggle shortcut (normal shortcuts with main key)
                if (!_toggleShortcut.IsModifierOnly && _toggleShortcut.Matches(keyCode, ctrl, alt, shift))
                {
                    ToggleRequested?.Invoke(this, EventArgs.Empty);
                    return (IntPtr)1; // Block key
                }

                // Check restore shortcut (e.g., ESC) - only if enabled
                bool restoreMatched = _restoreShortcut.IsModifierOnly
                    ? false  // Modifier-only restore not supported yet
                    : _restoreShortcut.Matches(keyCode, ctrl, alt, shift);

                if (_restoreShortcutEnabled && restoreMatched)
                {
                    RestoreRequested?.Invoke(this, EventArgs.Empty);
                    // Don't block ESC - let it pass through after triggering restore
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                // Issue #150: Control key alone clears buffer (rhythm break like EVKey)
                // Skip if toggle shortcut is modifier-only with Ctrl (to avoid clearing on toggle)
                if (keyCode == KeyCodes.VK_CONTROL && !(_toggleShortcut.IsModifierOnly && _toggleShortcut.Modifiers.HasFlag(ModifierKeys.Control)))
                {
                    RustBridge.Clear();
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                // Handle buffer-clearing keys (arrow keys, navigation, etc.)
                // Must be checked BEFORE IsRelevantKey since arrow keys are not "relevant" for input
                if (KeyCodes.IsBufferClearKey(keyCode))
                {
                    RustBridge.Clear();
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                // Only process relevant keys for Vietnamese input
                if (KeyCodes.IsRelevantKey(keyCode))
                {
                    bool capsLock = IsCapsLockOn();

                    // Skip if Ctrl or Alt is pressed (shortcuts)
                    if (ctrl || alt)
                    {
                        // Clear buffer on Ctrl+key combinations
                        if (ctrl)
                        {
                            RustBridge.Clear();
                        }
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    // Process the key through IME
                    var args = new KeyPressedEventArgs(keyCode, shift, capsLock);

                    try
                    {
                        _isProcessing = true;
                        KeyPressed?.Invoke(this, args);
                    }
                    finally
                    {
                        _isProcessing = false;
                    }

                    // Block the original key if handled
                    if (args.Handled)
                    {
                        return (IntPtr)1;
                    }
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsKeyDown(int vKey)
    {
        return (GetAsyncKeyState(vKey) & 0x8000) != 0;
    }

    private static bool IsCapsLockOn()
    {
        return (GetKeyState(KeyCodes.VK_CAPITAL) & 0x0001) != 0;
    }

    private static bool IsModifierKey(ushort keyCode)
    {
        return keyCode == KeyCodes.VK_CONTROL ||
               keyCode == KeyCodes.VK_SHIFT ||
               keyCode == KeyCodes.VK_MENU ||  // Alt
               keyCode == 0xA0 || keyCode == 0xA1 ||  // Left/Right Shift
               keyCode == 0xA2 || keyCode == 0xA3 ||  // Left/Right Control
               keyCode == 0xA4 || keyCode == 0xA5;    // Left/Right Alt
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            Stop();
            _disposed = true;
        }
    }

    ~KeyboardHook()
    {
        Dispose(false);
    }

    #endregion

    /// <summary>
    /// Get the marker used to identify injected keys from this application
    /// </summary>
    public static UIntPtr GetInjectedKeyMarker() => InjectedKeyMarker;
}

/// <summary>
/// Event args for key press events
/// </summary>
public class KeyPressedEventArgs : EventArgs
{
    public ushort VirtualKeyCode { get; }
    public bool Shift { get; }
    public bool CapsLock { get; }
    public bool Handled { get; set; }

    public KeyPressedEventArgs(ushort vkCode, bool shift, bool capsLock)
    {
        VirtualKeyCode = vkCode;
        Shift = shift;
        CapsLock = capsLock;
        Handled = false;
    }
}
