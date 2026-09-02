// AeroDial — FluentGlyphs.cs
// Icon keys of the form "fluent:<name>" or "fluent:<hex codepoint>" resolve to glyphs in
// the Windows system icon font (Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on
// Windows 10; both share the same codepoints for everything listed here).
//
// Named entries are the curated, searchable set shown first in the icon picker. Any other
// glyph in the font can still be used by codepoint ("fluent:E8B7"). Legacy names from the
// old hand-drawn icon set ("play", "vol_up", ...) are aliased so existing configs keep
// rendering; ConfigMigrator rewrites them to the fluent form on load.

namespace AeroDial.Core;

public sealed record FluentGlyph(string Name, int Codepoint, string Keywords)
{
    public string Key  => FluentGlyphs.Prefix + Name;
    public string Text => char.ConvertFromUtf32(Codepoint);
}

public static class FluentGlyphs
{
    public const string Prefix = "fluent:";

    /// <summary>Font families to try, in order.</summary>
    public static readonly string[] FontFamilies = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    /// <summary>Curated named glyphs (name, codepoint, search keywords).</summary>
    public static readonly FluentGlyph[] Named =
    [
        // Media
        new("play",         0xE768, "media start"),
        new("pause",        0xE769, "media"),
        new("stop",         0xE71A, "media"),
        new("next",         0xE893, "media skip forward track"),
        new("previous",     0xE892, "media skip back track"),
        new("volume",       0xE767, "sound speaker"),
        new("volume_up",    0xE995, "sound louder speaker"),
        new("volume_down",  0xE994, "sound quieter speaker"),
        new("mute",         0xE74F, "sound silent speaker"),
        new("audio",        0xE8D6, "music sound media"),
        new("music",        0xE90B, "audio song media"),
        new("video",        0xE714, "movie media film"),
        new("microphone",   0xE720, "mic record voice"),
        new("headphones",   0xE7F6, "audio listen"),

        // Apps and system
        new("apps",         0xE71D, "all applications grid"),
        new("settings",     0xE713, "gear options preferences"),
        new("desktop",      0xE7F4, "monitor screen display"),
        new("power",        0xE7E8, "shutdown off"),
        new("lock",         0xE72E, "secure padlock"),
        new("sleep",        0xE708, "moon quiet night"),
        new("home",         0xE80F, "house start"),
        new("search",       0xE721, "find magnifier"),
        new("terminal",     0xE756, "command prompt console shell run"),
        new("code",         0xE943, "script developer"),
        new("bug",          0xEBE8, "debug"),
        new("info",         0xE946, "about help"),
        new("help",         0xE897, "question"),
        new("warning",      0xE7BA, "alert caution"),
        new("clock",        0xE823, "time recent history"),
        new("calendar",     0xE787, "date schedule"),
        new("globe",        0xE774, "web url internet browser"),
        new("link",         0xE71B, "url chain"),
        new("mail",         0xE715, "email message"),
        new("chat",         0xE8BD, "message conversation"),
        new("people",       0xE716, "contacts group"),
        new("contact",      0xE77B, "person user"),
        new("phone",        0xE717, "call telephone"),
        new("camera",       0xE722, "photo screenshot"),
        new("picture",      0xE8B9, "image photo"),
        new("map",          0xE707, "location"),
        new("pin",          0xE718, "location marker"),
        new("shop",         0xE719, "store cart"),
        new("game",         0xE7FC, "controller gamepad"),
        new("print",        0xE749, "printer"),
        new("cloud",        0xE753, "sync online"),
        new("wifi",         0xE701, "wireless network"),
        new("bluetooth",    0xE702, "wireless"),
        new("brightness",   0xE706, "sun display"),
        new("battery",      0xE83F, "power charge"),
        new("keyboard",     0xE765, "keys typing"),
        new("mouse",        0xE962, "pointer"),
        new("devices",      0xE772, "hardware"),
        new("color",        0xE790, "palette paint"),
        new("font",         0xE8D2, "text typography"),

        // Files and folders
        new("folder",       0xE8B7, "directory"),
        new("folder_open",  0xE838, "directory"),
        new("new_folder",   0xE8F4, "directory add"),
        new("file",         0xE8A5, "document page"),
        new("page",         0xE7C3, "document"),
        new("open_file",    0xE8E5, "document"),
        new("library",      0xE8F1, "books collection"),
        new("save",         0xE74E, "disk"),
        new("download",     0xE896, "arrow down"),
        new("upload",       0xE898, "arrow up"),
        new("import",       0xE8B5, "arrow in"),
        new("export",       0xEDE1, "arrow out"),
        new("attach",       0xE723, "paperclip"),
        new("clipboard",    0xF0E3, "list paste history"),
        new("copy",         0xE8C8, "duplicate"),
        new("paste",        0xE77F, "clipboard"),
        new("cut",          0xE8C6, "scissors"),
        new("delete",       0xE74D, "trash remove bin"),
        new("edit",         0xE70F, "pencil rename"),
        new("undo",         0xE7A7, "back revert"),
        new("redo",         0xE7A6, "forward"),
        new("refresh",      0xE72C, "reload sync"),
        new("sync",         0xE895, "refresh"),
        new("filter",       0xE71C, "funnel"),
        new("sort",         0xE8CB, "order"),
        new("list",         0xE8FD, "bullets items"),
        new("tag",          0xE8EC, "label"),
        new("share",        0xE72D, "send"),
        new("send",         0xE724, "submit paper plane"),
        new("star",         0xE734, "favorite"),
        new("star_filled",  0xE735, "favorite"),
        new("heart",        0xEB51, "like favorite"),
        new("flag",         0xE7C1, "mark"),
        new("bookmark",     0xE8A4, "save"),

        // Navigation and controls
        new("back",         0xE72B, "arrow left"),
        new("forward",      0xE72A, "arrow right"),
        new("up",           0xE74A, "arrow"),
        new("down",         0xE74B, "arrow"),
        new("chevron_left", 0xE76B, "arrow"),
        new("chevron_right",0xE76C, "arrow"),
        new("chevron_up",   0xE70E, "arrow"),
        new("chevron_down", 0xE70D, "arrow"),
        new("menu",         0xE700, "hamburger navigation"),
        new("more",         0xE712, "ellipsis dots"),
        new("close",        0xE711, "cancel x"),
        new("check",        0xE73E, "done tick ok"),
        new("accept",       0xE8FB, "done tick ok"),
        new("plus",         0xE710, "add new"),
        new("minus",        0xE738, "remove subtract"),
        new("zoom_in",      0xE8A3, "magnify"),
        new("zoom_out",     0xE71F, "magnify"),
        new("fullscreen",   0xE740, "maximize expand"),
        new("minimize",     0xE921, "window"),
        new("maximize",     0xE922, "window"),
        new("new_window",   0xE8A7, "open window"),
        new("switch_apps",  0xE8F9, "task view windows"),
        new("dialpad",      0xE75F, "numbers keypad"),
        new("dial",         0xE76D, "radial ring"),
        new("touch",        0xE815, "pointer"),
        new("rotate",       0xE7AD, "turn"),
        new("crop",         0xE7A8, "trim"),
        new("magic",        0xE945, "wand auto"),
        new("puzzle",       0xEA86, "extension plugin"),
        new("robot",        0xE99A, "bot"),
        new("emoji",        0xE76E, "smile face"),
    ];

    /// <summary>Old built-in icon name → curated fluent name.</summary>
    public static readonly IReadOnlyDictionary<string, string> LegacyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["media"] = "audio",       ["apps"] = "apps",          ["vol_up"] = "volume_up",
            ["vol_down"] = "volume_down", ["mute"] = "mute",       ["play"] = "play",
            ["settings"] = "settings", ["desktop"] = "desktop",    ["next"] = "next",
            ["prev"] = "previous",     ["url"] = "globe",          ["script"] = "terminal",
            ["clipboard"] = "clipboard", ["default"] = "dial",     ["power"] = "power",
            ["lock"] = "lock",         ["folder"] = "folder",      ["copy"] = "copy",
            ["paste"] = "paste",       ["home"] = "home",          ["search"] = "search",
            ["mic"] = "microphone",    ["close"] = "close",        ["camera"] = "camera",
            ["keyboard"] = "keyboard", ["refresh"] = "refresh",    ["send"] = "send",
            ["star"] = "star",         ["pause"] = "pause",        ["stop"] = "stop",
            ["back"] = "back",         ["forward"] = "forward",    ["minimize"] = "minimize",
            ["zoom_in"] = "zoom_in",   ["zoom_out"] = "zoom_out",  ["trash"] = "delete",
            ["edit"] = "edit",         ["download"] = "download",  ["upload"] = "upload",
            ["check"] = "check",       ["plus"] = "plus",          ["minus"] = "minus",
            ["tag"] = "tag",           ["share"] = "share",        ["list"] = "list",
            ["info"] = "info",         ["wifi"] = "wifi",          ["bluetooth"] = "bluetooth",
            ["brightness"] = "brightness", ["clock"] = "clock",    ["alarm"] = "clock",
            ["calendar"] = "calendar", ["sleep"] = "sleep",        ["screenshot"] = "camera",
        };

    private static readonly Dictionary<string, FluentGlyph> s_byName =
        Named.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

    /// <summary>True for "fluent:..." keys and for legacy built-in names.</summary>
    public static bool IsGlyphKey(string? key)
        => key is not null
        && (key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || LegacyAliases.ContainsKey(key));

    /// <summary>Canonical "fluent:name" for a legacy name, or the key unchanged.</summary>
    public static string Canonicalize(string key)
        => LegacyAliases.TryGetValue(key, out var name) ? Prefix + name : key;

    /// <summary>Resolves a glyph key ("fluent:name", "fluent:E8B7", or a legacy name) to a codepoint.</summary>
    public static bool TryResolve(string? key, out int codepoint)
    {
        codepoint = 0;
        if (string.IsNullOrWhiteSpace(key)) return false;

        string k = key.Trim();
        if (LegacyAliases.TryGetValue(k, out var alias)) k = Prefix + alias;
        if (!k.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)) return false;

        string rest = k[Prefix.Length..].Trim();
        if (s_byName.TryGetValue(rest, out var g)) { codepoint = g.Codepoint; return true; }

        if (rest.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) rest = rest[2..];
        if (rest.Length is >= 4 and <= 5
            && int.TryParse(rest, System.Globalization.NumberStyles.HexNumber, null, out int cp)
            && cp is >= 0xE000 and <= 0xF8FF)
        {
            codepoint = cp;
            return true;
        }
        return false;
    }

    /// <summary>Case-insensitive search over names and keywords; empty query returns everything.</summary>
    public static IEnumerable<FluentGlyph> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Named;
        string q = query.Trim();
        return Named.Where(g =>
            g.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            g.Keywords.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            g.Codepoint.ToString("X4").Contains(q, StringComparison.OrdinalIgnoreCase));
    }
}
