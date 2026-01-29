namespace GoNhanh.Core;

/// <summary>
/// Windows Virtual Key Codes
/// Maps to Rust core key codes for FFI compatibility
/// Reference: https://docs.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
/// </summary>
public static class KeyCodes
{
    // Letters A-Z (0x41 - 0x5A)
    public const ushort VK_A = 0x41;
    public const ushort VK_B = 0x42;
    public const ushort VK_C = 0x43;
    public const ushort VK_D = 0x44;
    public const ushort VK_E = 0x45;
    public const ushort VK_F = 0x46;
    public const ushort VK_G = 0x47;
    public const ushort VK_H = 0x48;
    public const ushort VK_I = 0x49;
    public const ushort VK_J = 0x4A;
    public const ushort VK_K = 0x4B;
    public const ushort VK_L = 0x4C;
    public const ushort VK_M = 0x4D;
    public const ushort VK_N = 0x4E;
    public const ushort VK_O = 0x4F;
    public const ushort VK_P = 0x50;
    public const ushort VK_Q = 0x51;
    public const ushort VK_R = 0x52;
    public const ushort VK_S = 0x53;
    public const ushort VK_T = 0x54;
    public const ushort VK_U = 0x55;
    public const ushort VK_V = 0x56;
    public const ushort VK_W = 0x57;
    public const ushort VK_X = 0x58;
    public const ushort VK_Y = 0x59;
    public const ushort VK_Z = 0x5A;

    // Numbers 0-9 (0x30 - 0x39)
    public const ushort VK_0 = 0x30;
    public const ushort VK_1 = 0x31;
    public const ushort VK_2 = 0x32;
    public const ushort VK_3 = 0x33;
    public const ushort VK_4 = 0x34;
    public const ushort VK_5 = 0x35;
    public const ushort VK_6 = 0x36;
    public const ushort VK_7 = 0x37;
    public const ushort VK_8 = 0x38;
    public const ushort VK_9 = 0x39;

    // Special keys
    public const ushort VK_BACK = 0x08;      // Backspace
    public const ushort VK_TAB = 0x09;
    public const ushort VK_RETURN = 0x0D;    // Enter
    public const ushort VK_SHIFT = 0x10;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU = 0x12;      // Alt
    public const ushort VK_CAPITAL = 0x14;   // Caps Lock
    public const ushort VK_ESCAPE = 0x1B;
    public const ushort VK_SPACE = 0x20;

    // Arrow keys
    public const ushort VK_LEFT = 0x25;
    public const ushort VK_UP = 0x26;
    public const ushort VK_RIGHT = 0x27;
    public const ushort VK_DOWN = 0x28;

    // Navigation keys
    public const ushort VK_HOME = 0x24;
    public const ushort VK_END = 0x23;
    public const ushort VK_PRIOR = 0x21;  // Page Up
    public const ushort VK_NEXT = 0x22;   // Page Down

    // Punctuation (US keyboard layout)
    public const ushort VK_OEM_1 = 0xBA;     // ;:
    public const ushort VK_OEM_PLUS = 0xBB;  // =+
    public const ushort VK_OEM_COMMA = 0xBC; // ,<
    public const ushort VK_OEM_MINUS = 0xBD; // -_
    public const ushort VK_OEM_PERIOD = 0xBE;// .>
    public const ushort VK_OEM_2 = 0xBF;     // /?
    public const ushort VK_OEM_3 = 0xC0;     // `~
    public const ushort VK_OEM_4 = 0xDB;     // [{
    public const ushort VK_OEM_5 = 0xDC;     // \|
    public const ushort VK_OEM_6 = 0xDD;     // ]}
    public const ushort VK_OEM_7 = 0xDE;     // '"

    // Numpad
    public const ushort VK_NUMPAD0 = 0x60;
    public const ushort VK_NUMPAD1 = 0x61;
    public const ushort VK_NUMPAD2 = 0x62;
    public const ushort VK_NUMPAD3 = 0x63;
    public const ushort VK_NUMPAD4 = 0x64;
    public const ushort VK_NUMPAD5 = 0x65;
    public const ushort VK_NUMPAD6 = 0x66;
    public const ushort VK_NUMPAD7 = 0x67;
    public const ushort VK_NUMPAD8 = 0x68;
    public const ushort VK_NUMPAD9 = 0x69;

    /// <summary>
    /// Check if a key code is a letter (A-Z)
    /// </summary>
    public static bool IsLetter(ushort keyCode) => keyCode >= VK_A && keyCode <= VK_Z;

    /// <summary>
    /// Check if a key code is a number (0-9)
    /// </summary>
    public static bool IsNumber(ushort keyCode) => keyCode >= VK_0 && keyCode <= VK_9;

    /// <summary>
    /// Check if a key code is relevant for Vietnamese input
    /// Letters, numbers (for VNI), and word-breaking keys
    /// </summary>
    public static bool IsRelevantKey(ushort keyCode)
    {
        return IsLetter(keyCode) ||
               IsNumber(keyCode) ||
               keyCode == VK_SPACE ||
               keyCode == VK_RETURN ||
               keyCode == VK_BACK ||
               keyCode == VK_OEM_4 ||  // [
               keyCode == VK_OEM_6;    // ]
    }

    /// <summary>
    /// Check if a key should clear the IME buffer (word boundaries and cursor movement)
    /// </summary>
    public static bool IsBufferClearKey(ushort keyCode)
    {
        return keyCode == VK_SPACE ||
               keyCode == VK_RETURN ||
               keyCode == VK_TAB ||
               keyCode == VK_ESCAPE ||
               // Arrow keys - cursor movement breaks the typing flow
               keyCode == VK_LEFT ||
               keyCode == VK_RIGHT ||
               keyCode == VK_UP ||
               keyCode == VK_DOWN ||
               // Navigation keys
               keyCode == VK_HOME ||
               keyCode == VK_END ||
               keyCode == VK_PRIOR ||  // Page Up
               keyCode == VK_NEXT;     // Page Down
    }

    /// <summary>
    /// Convert Windows Virtual Key code to macOS keycode for Rust engine
    /// macOS keycodes are different from Windows VK codes
    /// </summary>
    public static ushort ToMacKeyCode(ushort windowsVK)
    {
        return windowsVK switch
        {
            // Letters - macOS uses different layout
            VK_A => 0,   // A
            VK_S => 1,   // S
            VK_D => 2,   // D
            VK_F => 3,   // F
            VK_H => 4,   // H
            VK_G => 5,   // G
            VK_Z => 6,   // Z
            VK_X => 7,   // X
            VK_C => 8,   // C
            VK_V => 9,   // V
            VK_B => 11,  // B
            VK_Q => 12,  // Q
            VK_W => 13,  // W
            VK_E => 14,  // E
            VK_R => 15,  // R
            VK_Y => 16,  // Y
            VK_T => 17,  // T
            VK_O => 31,  // O
            VK_U => 32,  // U
            VK_I => 34,  // I
            VK_P => 35,  // P
            VK_L => 37,  // L
            VK_J => 38,  // J
            VK_K => 40,  // K
            VK_N => 45,  // N
            VK_M => 46,  // M

            // Numbers
            VK_1 => 18,  // 1
            VK_2 => 19,  // 2
            VK_3 => 20,  // 3
            VK_4 => 21,  // 4
            VK_5 => 23,  // 5
            VK_6 => 22,  // 6
            VK_7 => 26,  // 7
            VK_8 => 28,  // 8
            VK_9 => 25,  // 9
            VK_0 => 29,  // 0

            // Special keys
            VK_SPACE => 49,   // Space
            VK_BACK => 51,    // Backspace/Delete
            VK_TAB => 48,     // Tab
            VK_RETURN => 36,  // Return/Enter
            VK_ESCAPE => 53,  // Escape

            // Arrow keys
            VK_LEFT => 123,
            VK_RIGHT => 124,
            VK_DOWN => 125,
            VK_UP => 126,

            // Punctuation
            VK_OEM_PERIOD => 47,  // .
            VK_OEM_COMMA => 43,   // ,
            VK_OEM_2 => 44,       // /
            VK_OEM_1 => 41,       // ;
            VK_OEM_7 => 39,       // '
            VK_OEM_4 => 33,       // [
            VK_OEM_6 => 30,       // ]

            // Unknown - return as-is (will likely be ignored by engine)
            _ => windowsVK
        };
    }
}
