// AeroDial — KeyComboParser.cs
// Turns key-combo strings ("Ctrl+Shift+S", "Win+D", "F5", "Enter") into virtual-key
// codes. Pure: no input injection here. ActionDispatcher turns the result into
// SendInput calls; the macro engine reuses it for KeyPress / KeyDown / KeyUp steps.

namespace AeroDial.Core;

/// <summary>A parsed chord: modifiers first (pressed first, released last), then keys.</summary>
public readonly record struct KeyChord(IReadOnlyList<byte> Modifiers, IReadOnlyList<byte> Keys)
{
    public bool IsEmpty => Modifiers.Count == 0 && Keys.Count == 0;

    /// <summary>All virtual keys in press order (modifiers, then keys).</summary>
    public IEnumerable<byte> PressOrder => Modifiers.Concat(Keys);
}

public static class KeyComboParser
{
    public const byte VK_SHIFT   = 0x10;
    public const byte VK_CONTROL = 0x11;
    public const byte VK_MENU    = 0x12; // Alt
    public const byte VK_LWIN    = 0x5B;

    public static bool IsModifier(byte vk) => vk is VK_LWIN or VK_CONTROL or VK_MENU or VK_SHIFT;

    /// <summary>Parses "A+B+C". Unknown tokens are skipped; check <see cref="KeyChord.IsEmpty"/>
    /// to detect a combo with nothing AeroDial understands.</summary>
    public static KeyChord Parse(string chord)
    {
        var modifiers = new List<byte>();
        var keys      = new List<byte>();
        foreach (var part in chord.Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            byte vk = TokenToVk(part);
            if (vk == 0) continue;
            if (IsModifier(vk)) modifiers.Add(vk);
            else                keys.Add(vk);
        }
        return new KeyChord(modifiers, keys);
    }

    /// <summary>Maps a key token ("Ctrl", "Enter", "A", "F5", "5") to a virtual-key code.
    /// Returns 0 for unrecognized tokens.</summary>
    public static byte TokenToVk(string token)
    {
        string t = token.Trim().ToUpperInvariant();
        return t switch
        {
            "WIN"   or "WINDOWS"  => VK_LWIN,
            "CTRL"  or "CONTROL"  => VK_CONTROL,
            "ALT"                 => VK_MENU,
            "SHIFT"               => VK_SHIFT,
            "TAB"                 => 0x09,
            "ENTER" or "RETURN"   => 0x0D,
            "ESC"   or "ESCAPE"   => 0x1B,
            "SPACE"               => 0x20,
            "DEL"   or "DELETE"   => 0x2E,
            "BACKSPACE" or "BKSP" => 0x08,
            "HOME"                => 0x24,
            "END"                 => 0x23,
            "LEFT"                => 0x25,
            "UP"                  => 0x26,
            "RIGHT"               => 0x27,
            "DOWN"                => 0x28,
            "INSERT" or "INS"     => 0x2D,
            "PAGEUP" or "PGUP"    => 0x21,
            "PAGEDOWN" or "PGDN"  => 0x22,
            "PRINTSCREEN" or "PRTSC" => 0x2C,
            "["                   => 0xDB, // VK_OEM_4
            "]"                   => 0xDD, // VK_OEM_6
            "-" or "MINUS"        => 0xBD, // VK_OEM_MINUS
            "=" or "PLUS" or "EQUALS" => 0xBB, // VK_OEM_PLUS
            "," or "COMMA"        => 0xBC, // VK_OEM_COMMA
            "." or "PERIOD"       => 0xBE, // VK_OEM_PERIOD
            "/" or "SLASH"        => 0xBF, // VK_OEM_2
            ";" or "SEMICOLON"    => 0xBA, // VK_OEM_1
            _ when t.Length == 1 && char.IsAsciiLetterOrDigit(t[0]) => (byte)t[0],
            _ when t.Length >= 2 && t[0] == 'F' && int.TryParse(t.AsSpan(1), out int fn)
                                 && fn is >= 1 and <= 24        => (byte)(0x6F + fn),
            _                     => 0,
        };
    }
}
