using System.Runtime.InteropServices;

namespace GoNhanh.Core;

/// <summary>
/// Sends text to the active window using Windows SendInput API
/// Handles backspace deletion and Unicode character insertion
/// Supports multiple injection methods for different applications
/// </summary>
public static class TextSender
{
    #region Win32 Constants

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    #endregion

    #region Win32 Imports

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    #endregion

    #region Structures

    // INPUT struct size on x64: 4 (type) + 4 (padding) + 24 (union) = 32 bytes
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    // Union must be 24 bytes to match MOUSEINPUT (largest member)
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUTUNION
    {
        public KEYBDINPUT ki;
        // Padding to make union 24 bytes (KEYBDINPUT is 16 bytes on x64)
        private long _padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    #endregion

    /// <summary>
    /// Send text replacement using default fast method (backward compatible)
    /// </summary>
    public static void SendText(string text, int backspaces)
    {
        SendText(text, backspaces, InjectionMethod.Fast, InjectionDelays.Fast);
    }

    /// <summary>
    /// Send text replacement with specified injection method and delays
    /// </summary>
    public static void SendText(string text, int backspaces, InjectionMethod method, InjectionDelays delays)
    {
        if ((string.IsNullOrEmpty(text) || text.Length == 0) && backspaces == 0)
            return;

        switch (method)
        {
            case InjectionMethod.Selection:
                SendViaSelection(text, backspaces, delays);
                break;
            case InjectionMethod.CharByChar:
                SendCharByChar(text, backspaces, delays);
                break;
            case InjectionMethod.Slow:
            case InjectionMethod.Fast:
            default:
                SendViaBackspace(text, backspaces, delays);
                break;
        }
    }

    /// <summary>
    /// Default method: send backspaces then text (with optional delays)
    /// </summary>
    private static void SendViaBackspace(string text, int backspaces, InjectionDelays delays)
    {
        var inputs = new List<INPUT>();
        var marker = KeyboardHook.GetInjectedKeyMarker();

        // Add backspaces with delays
        for (int i = 0; i < backspaces; i++)
        {
            AddKeyPress(inputs, KeyCodes.VK_BACK, marker);

            if (i < backspaces - 1 && delays.BackspaceDelayMs > 0)
            {
                // Send current batch, wait, then continue
                SendInputs(inputs);
                inputs.Clear();
                Thread.Sleep(delays.BackspaceDelayMs);
            }
        }

        // Wait between backspaces and text
        if (backspaces > 0 && delays.WaitDelayMs > 0)
        {
            SendInputs(inputs);
            inputs.Clear();
            Thread.Sleep(delays.WaitDelayMs);
        }

        // Add text characters
        AddUnicodeText(inputs, text, marker);

        SendInputs(inputs);
    }

    /// <summary>
    /// Selection method: use Shift+Left to select then type replacement
    /// Better for browser address bars and some text fields
    /// </summary>
    private static void SendViaSelection(string text, int backspaces, InjectionDelays delays)
    {
        var marker = KeyboardHook.GetInjectedKeyMarker();

        // Select text with Shift+Left for each character to delete
        for (int i = 0; i < backspaces; i++)
        {
            var inputs = new List<INPUT>();

            // Shift down
            inputs.Add(CreateKeyInput(KeyCodes.VK_SHIFT, 0, 0, marker));
            // Left arrow down
            inputs.Add(CreateKeyInput(KeyCodes.VK_LEFT, 0, 0, marker));
            // Left arrow up
            inputs.Add(CreateKeyInput(KeyCodes.VK_LEFT, 0, KEYEVENTF_KEYUP, marker));
            // Shift up
            inputs.Add(CreateKeyInput(KeyCodes.VK_SHIFT, 0, KEYEVENTF_KEYUP, marker));

            SendInputs(inputs);

            if (delays.BackspaceDelayMs > 0)
                Thread.Sleep(delays.BackspaceDelayMs);
        }

        // Wait before typing
        if (backspaces > 0 && delays.WaitDelayMs > 0)
            Thread.Sleep(delays.WaitDelayMs);

        // Type replacement text (replaces selection)
        var textInputs = new List<INPUT>();
        AddUnicodeText(textInputs, text, marker);
        SendInputs(textInputs);
    }

    /// <summary>
    /// Character-by-character method: send one char at a time with delays
    /// For problematic apps that can't handle fast input
    /// </summary>
    private static void SendCharByChar(string text, int backspaces, InjectionDelays delays)
    {
        var marker = KeyboardHook.GetInjectedKeyMarker();

        // Send backspaces one at a time
        for (int i = 0; i < backspaces; i++)
        {
            var inputs = new List<INPUT>();
            AddKeyPress(inputs, KeyCodes.VK_BACK, marker);
            SendInputs(inputs);

            if (delays.BackspaceDelayMs > 0)
                Thread.Sleep(delays.BackspaceDelayMs);
        }

        // Wait between backspaces and text
        if (backspaces > 0 && delays.WaitDelayMs > 0)
            Thread.Sleep(delays.WaitDelayMs);

        // Send text character by character
        foreach (char c in text)
        {
            var inputs = new List<INPUT>();
            AddUnicodeChar(inputs, c, marker);
            SendInputs(inputs);

            if (delays.TextDelayMs > 0)
                Thread.Sleep(delays.TextDelayMs);
        }
    }

    #region Helper Methods

    private static void AddKeyPress(List<INPUT> inputs, ushort vk, UIntPtr marker)
    {
        // For backspace, use scancode-only mode (wVk=0) - required for Brave browser
        if (vk == KeyCodes.VK_BACK)
        {
            // Scancode 0x0E = Backspace
            inputs.Add(CreateScanCodeInput(0x0E, 0, marker));
            inputs.Add(CreateScanCodeInput(0x0E, KEYEVENTF_KEYUP, marker));
        }
        else
        {
            // Normal key press with virtual key
            inputs.Add(CreateKeyInput(vk, 0, 0, marker));
            inputs.Add(CreateKeyInput(vk, 0, KEYEVENTF_KEYUP, marker));
        }
    }

    private static INPUT CreateScanCodeInput(ushort scanCode, uint flags, UIntPtr marker)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,  // No virtual key - scancode only
                    wScan = scanCode,
                    dwFlags = KEYEVENTF_SCANCODE | flags,
                    time = 0,
                    dwExtraInfo = marker
                }
            }
        };
    }

    private static INPUT CreateKeyInput(ushort vk, ushort scanCode, uint flags, UIntPtr marker)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    wScan = scanCode,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = marker
                }
            }
        };
    }

    private static void AddUnicodeText(List<INPUT> inputs, string text, UIntPtr marker)
    {
        foreach (char c in text)
        {
            AddUnicodeChar(inputs, c, marker);
        }
    }

    private static void AddUnicodeChar(List<INPUT> inputs, char c, UIntPtr marker)
    {
        // Key down
        inputs.Add(new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = marker
                }
            }
        });

        // Key up
        inputs.Add(new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = marker
                }
            }
        });
    }

    private static void SendInputs(List<INPUT> inputs)
    {
        if (inputs.Count > 0)
        {
            var inputArray = inputs.ToArray();
            SendInput((uint)inputArray.Length, inputArray, Marshal.SizeOf<INPUT>());
        }
    }

    #endregion
}
