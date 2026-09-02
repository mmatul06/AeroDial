// AeroDial — ActionCatalog.cs
// User-facing metadata for every action type the menu editor offers: label,
// one-line description, and the icon a new item of that type starts with.

namespace AeroDial.Config;

public sealed record ActionInfo(ActionType Type, string Label, string Description, string DefaultIcon);

public static class ActionCatalog
{
    /// <summary>Action types the editor lets the user pick, in display order.
    /// FocusWindow is deliberately absent (its window handle is session-only).</summary>
    public static readonly ActionInfo[] Editable =
    [
        new(ActionType.None,           "No action",             "A placeholder slice that does nothing.",                      "fluent:dial"),
        new(ActionType.LaunchApp,      "Launch app",            "Start a program or shortcut, with optional arguments.",        "fluent:apps"),
        new(ActionType.OpenFolder,     "Open folder",           "Open a folder in File Explorer (or select a file).",           "fluent:folder"),
        new(ActionType.RunCommand,     "Run command",           "Run anything you would type into Win+R, optionally as admin.", "fluent:terminal"),
        new(ActionType.OpenUrl,        "Open URL",              "Open a web address in the default browser.",                   "fluent:globe"),
        new(ActionType.KeyCombo,       "Key combo",             "Press a keyboard shortcut such as Ctrl+S or Win+D.",           "fluent:keyboard"),
        new(ActionType.Macro,          "Macro",                 "Type text and press keys in sequence, with delays.",           "fluent:magic"),
        new(ActionType.Media,          "Media control",         "Play, pause, skip, or change the volume.",                     "fluent:play"),
        new(ActionType.RunScript,      "Run script",            "Run a .bat, .cmd, or .ps1 script.",                            "fluent:code"),
        new(ActionType.PasteClipboard, "Paste text",            "Put text on the clipboard and paste it.",                      "fluent:paste"),
        new(ActionType.SubMenu,        "Submenu",               "Open another menu as a ring around this slice.",               "fluent:more"),
        new(ActionType.OpenSettings,   "Open AeroDial settings","Show this settings window.",                                   "fluent:settings"),
    ];

    private static readonly Dictionary<ActionType, ActionInfo> s_byType = Editable.ToDictionary(a => a.Type);

    public static ActionInfo? Find(ActionType type) => s_byType.GetValueOrDefault(type);

    public static string LabelOf(ActionType type) => Find(type)?.Label ?? type.ToString();

    /// <summary>Index into <see cref="Editable"/> for a type, or -1.</summary>
    public static int IndexOf(ActionType type) => Array.FindIndex(Editable, a => a.Type == type);
}
