using System.Runtime.InteropServices;
using System.Text;

namespace GoNhanh.Core;

/// <summary>
/// P/Invoke bridge to Rust core library (gonhanh_core.dll)
/// FFI contract matches core/src/lib.rs exports
/// </summary>
public static class RustBridge
{
    private const string DllName = "gonhanh_core.dll";

    #region Native Imports

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_init();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_clear();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_free(IntPtr result);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_method(byte method);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_enabled([MarshalAs(UnmanagedType.U1)] bool enabled);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_modern([MarshalAs(UnmanagedType.U1)] bool modern);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_bracket_shortcut([MarshalAs(UnmanagedType.U1)] bool enabled);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_esc_restore([MarshalAs(UnmanagedType.U1)] bool enabled);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_english_auto_restore([MarshalAs(UnmanagedType.U1)] bool enabled);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void ime_auto_capitalize([MarshalAs(UnmanagedType.U1)] bool enabled);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr ime_key(ushort keycode, [MarshalAs(UnmanagedType.U1)] bool shift, [MarshalAs(UnmanagedType.U1)] bool capslock);

    #endregion

    #region Public API

    /// <summary>
    /// Initialize the IME engine. Call once at startup.
    /// </summary>
    public static void Initialize()
    {
        ime_init();
    }

    /// <summary>
    /// Clear the typing buffer.
    /// </summary>
    public static void Clear()
    {
        ime_clear();
    }

    /// <summary>
    /// Set input method (Telex=0, VNI=1)
    /// </summary>
    public static void SetMethod(InputMethod method)
    {
        ime_method((byte)method);
    }

    /// <summary>
    /// Enable or disable IME processing
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        ime_enabled(enabled);
    }

    /// <summary>
    /// Set tone style (modern=true: hòa, old=false: hoà)
    /// </summary>
    public static void SetModernTone(bool modern)
    {
        ime_modern(modern);
    }

    /// <summary>
    /// Enable bracket shortcuts (] → ư, [ → ơ)
    /// </summary>
    public static void SetBracketShortcut(bool enabled)
    {
        ime_bracket_shortcut(enabled);
    }

    /// <summary>
    /// Enable ESC key to restore original text
    /// </summary>
    public static void SetEscRestore(bool enabled)
    {
        ime_esc_restore(enabled);
    }

    /// <summary>
    /// Enable auto-restore for English words
    /// </summary>
    public static void SetEnglishAutoRestore(bool enabled)
    {
        ime_english_auto_restore(enabled);
    }

    /// <summary>
    /// Enable auto-capitalize after punctuation
    /// </summary>
    public static void SetAutoCapitalize(bool enabled)
    {
        ime_auto_capitalize(enabled);
    }

    /// <summary>
    /// Process a keystroke and get the result
    /// </summary>
    public static ImeResult ProcessKey(ushort keycode, bool shift, bool capslock)
    {
        // Convert Windows VK code to macOS keycode for Rust engine
        ushort macKeyCode = KeyCodes.ToMacKeyCode(keycode);

        // Rust ime_key expects: (key, caps, ctrl) - we pass shift as ctrl=false
        IntPtr ptr = ime_key(macKeyCode, capslock, false);
        if (ptr == IntPtr.Zero)
        {
            return ImeResult.Empty;
        }

        try
        {
            var native = Marshal.PtrToStructure<NativeResult>(ptr);
            return ImeResult.FromNative(native);
        }
        finally
        {
            ime_free(ptr);
        }
    }

    #endregion
}

/// <summary>
/// Input method type
/// </summary>
public enum InputMethod : byte
{
    Telex = 0,
    VNI = 1
}

/// <summary>
/// IME action type
/// </summary>
public enum ImeAction : byte
{
    None = 0,    // No action needed
    Send = 1,    // Send text replacement
    Restore = 2  // Restore original text
}

/// <summary>
/// Native result structure from Rust (must match core/src/lib.rs)
/// Size: 256 UInt32 chars (1024 bytes) + 4 bytes = 1028 bytes
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeResult
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public uint[] chars;
    public byte action;
    public byte backspace;
    public byte count;
    public byte _pad;
}

/// <summary>
/// Managed IME result
/// </summary>
public readonly struct ImeResult
{
    public readonly ImeAction Action;
    public readonly byte Backspace;
    public readonly byte Count;
    private readonly uint[] _chars;

    public static readonly ImeResult Empty = new(ImeAction.None, 0, 0, Array.Empty<uint>());

    private ImeResult(ImeAction action, byte backspace, byte count, uint[] chars)
    {
        Action = action;
        Backspace = backspace;
        Count = count;
        _chars = chars;
    }

    internal static ImeResult FromNative(NativeResult native)
    {
        return new ImeResult(
            (ImeAction)native.action,
            native.backspace,
            native.count,
            native.chars ?? Array.Empty<uint>()
        );
    }

    /// <summary>
    /// Get the result text as a string
    /// </summary>
    public string GetText()
    {
        if (Count == 0 || _chars == null)
            return string.Empty;

        var sb = new StringBuilder(Count);
        for (int i = 0; i < Count && i < _chars.Length; i++)
        {
            if (_chars[i] > 0)
            {
                sb.Append(char.ConvertFromUtf32((int)_chars[i]));
            }
        }
        return sb.ToString();
    }
}
