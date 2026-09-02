// AeroDial — ConfigModel.cs
// All serializable configuration data models.

using System.Text.Json.Serialization;

namespace AeroDial.Config;

// ── Root ──────────────────────────────────────────────────────────────────────

public sealed class AeroDialConfig
{
    /// <summary>Schema version. Documents without it are treated as v1. See ConfigMigrator.</summary>
    public int                    ConfigVersion { get; set; } = ConfigMigrator.CurrentVersion;
    public TriggerConfig          Trigger       { get; set; } = new();
    public AppearanceConfig       Appearance    { get; set; } = new();
    public BehaviorConfig         Behavior      { get; set; } = new();
    public List<RadialMenuConfig> Menus         { get; set; } = [DefaultMainMenu(), DefaultMediaMenu()];
    public string                 ActiveMenuId  { get; set; } = "default";
    public List<AppProfileConfig> AppProfiles   { get; set; } = [];

    private static RadialMenuConfig DefaultMainMenu() => new()
    {
        Id    = "default",
        Name  = "Main Menu",
        Items =
        [
            new() { Label = "Media",       Icon = "fluent:audio",       ActionType = ActionType.SubMenu, SubMenuId = "media"                                       },
            new() { Label = "Active Apps", Icon = "fluent:apps",        ActionType = ActionType.SubMenu, SubMenuId = AeroDial.Core.AppConstants.ActiveTasksMenuId },
            new() { Label = "Volume Up",   Icon = "fluent:volume_up",   ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeUp,   ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Volume Down", Icon = "fluent:volume_down", ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeDown, ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Mute",        Icon = "fluent:mute",        ActionType = ActionType.Media,     MediaAction = MediaActionType.Mute      },
            new() { Label = "Play/Pause",  Icon = "fluent:play",        ActionType = ActionType.Media,     MediaAction = MediaActionType.PlayPause },
            new() { Label = "Settings",    Icon = "fluent:settings",    ActionType = ActionType.OpenSettings                                       },
            new() { Label = "Desktop",     Icon = "fluent:desktop",     ActionType = ActionType.KeyCombo,  KeyCombo    = "Win+D"                   },
        ]
    };

    private static RadialMenuConfig DefaultMediaMenu() => new()
    {
        Id    = "media",
        Name  = "Media",
        Items =
        [
            new() { Label = "Play/Pause", Icon = "fluent:play",        ActionType = ActionType.Media, MediaAction = MediaActionType.PlayPause  },
            new() { Label = "Next",       Icon = "fluent:next",        ActionType = ActionType.Media, MediaAction = MediaActionType.Next       },
            new() { Label = "Previous",   Icon = "fluent:previous",    ActionType = ActionType.Media, MediaAction = MediaActionType.Previous   },
            new() { Label = "Volume Up",  Icon = "fluent:volume_up",   ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeUp,   ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Vol Down",   Icon = "fluent:volume_down", ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeDown, ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Mute",       Icon = "fluent:mute",        ActionType = ActionType.Media, MediaAction = MediaActionType.Mute       },
        ]
    };
}

// ── Trigger ───────────────────────────────────────────────────────────────────

public sealed class TriggerConfig
{
    public int  VirtualKey    { get; set; } = 0x04; // Middle mouse
    public bool RequireCtrl   { get; set; } = false;
    public bool RequireAlt    { get; set; } = false;
    public bool RequireShift  { get; set; } = false;
    public bool HoldMode      { get; set; } = false;
}

// ── Appearance ────────────────────────────────────────────────────────────────

public sealed class AppearanceConfig
{
    public string ThemeName        { get; set; } = "Obsidian";
    public float  Scale            { get; set; } = 1.1f;
    public float  GapDegrees       { get; set; } = 2.5f;
    public int    SliceCount       { get; set; } = 6;
    public bool   AnimationsEnabled { get; set; } = true;
    public bool   RespectSystemAnimationSetting { get; set; } = true;
    public bool   ShowBreadcrumb   { get; set; } = false; // feature removed; kept for JSON compat
    public float  BackgroundDimOpacity { get; set; } = 0f; // feature removed; kept for JSON compat
    public float  RingOpacity          { get; set; } = 0.92f;
    /// <summary>Radial gap (logical px) between the center circle outer edge and slice inner edge.</summary>
    public float  RingInnerDetach      { get; set; } = 11f;

    // ── Volume ring ──────────────────────────────────────────────────────
    /// <summary>Controls when the volume ring arc is visible.</summary>
    public VolumeRingVisibility VolumeRingVisibility { get; set; } = VolumeRingVisibility.Hidden;

    // ── Media info ───────────────────────────────────────────────────────
    /// <summary>Show the now-playing media title below the ring.</summary>
    public bool ShowNowPlaying { get; set; } = true;
    /// <summary>Show a small decorative audio visualizer below the ring while media plays.</summary>
    public bool ShowVisualizer { get; set; } = true;

    // ── Advanced ─────────────────────────────────────────────────────────
    /// <summary>When true, the L2 ring thins when an L3 ring is also visible.</summary>
    public bool DynamicRingThinning { get; set; } = true;
    /// <summary>When true, sub-menu rings fan out only in the sector of their parent slice.</summary>
    public bool PartialArcSubMenu   { get; set; } = true;
}

// ── Behavior ──────────────────────────────────────────────────────────────────

public sealed class BehaviorConfig
{
    public SelectionMode SelectionMode       { get; set; } = SelectionMode.Click;
    public int  HoverDwellMs                 { get; set; } = 350;
    public bool LaunchOnRelease              { get; set; } = false;
    public bool CloseOnActionExecuted        { get; set; } = true;
    public bool StartWithWindows             { get; set; } = false;
    /// <summary>When true, mouse clicks are swallowed by AeroDial and not forwarded to the window below.</summary>
    public bool BlockInputWhenOpen           { get; set; } = false;
    /// <summary>When true, clicking outside the ring (LMB or RMB) closes the overlay immediately.</summary>
    public bool CloseOnClickOutside          { get; set; } = true;
    /// <summary>When true, verbose DEBUG entries are written to the log file.</summary>
    public bool EnableDebugLogging           { get; set; } = false;
}

// ── Menu / Items ──────────────────────────────────────────────────────────────

public sealed class RadialMenuConfig
{
    public string               Id    { get; set; } = Guid.NewGuid().ToString();
    public string               Name  { get; set; } = "Menu";
    public List<MenuItemConfig> Items { get; set; } = [];
}

public sealed class MenuItemConfig
{
    public string     Label       { get; set; } = "Item";
    public string     Icon        { get; set; } = "fluent:dial";
    public ActionType ActionType  { get; set; } = ActionType.None;

    public string?          AppPath     { get; set; }
    public string?          AppArgs     { get; set; }
    public string?          Url         { get; set; }
    public string?          KeyCombo    { get; set; }
    public string?          ScriptPath  { get; set; }
    public string?          ClipText    { get; set; }
    public string?          SubMenuId       { get; set; }
    public MediaActionType? MediaAction     { get; set; }

    // OpenFolder — folder (or file, which is selected in Explorer) to open
    public string?          FolderPath  { get; set; }

    // RunCommand — Win+R style command line, optionally elevated
    public string?          Command     { get; set; }
    public bool             RunAsAdmin  { get; set; }

    // Macro — an ordered sequence of keystroke/text/delay steps (ActionType.Macro)
    public List<MacroStep>? Macro           { get; set; }

    // Scroll-wheel secondary actions — fires when dial is open and user scrolls on this slice
    public MediaActionType? ScrollUpAction   { get; set; }
    public MediaActionType? ScrollDownAction { get; set; }

    // FocusWindow — in-memory only, not serialized (HWND is session-specific)
    [JsonIgnore] public nint WindowHandle { get; set; }

    [JsonIgnore]
    public bool IsSubMenu => ActionType == ActionType.SubMenu;

    /// <summary>An empty placeholder slice: it holds a position on the ring (so items
    /// keep their slot when a neighbour is removed) but renders dimmed and does nothing.</summary>
    [JsonIgnore]
    public bool IsEmptySlot => ActionType == ActionType.None && string.IsNullOrWhiteSpace(Label);
}

// ── Macros ──────────────────────────────────────────────────────────────────────

public sealed class MacroStep
{
    public MacroStepType Type    { get; set; } = MacroStepType.TypeText;
    /// <summary>Payload: literal text (TypeText), chord like "Ctrl+S" (KeyPress),
    /// or a single key token like "Shift"/"Enter"/"A" (KeyDown/KeyUp). Unused for Delay.</summary>
    public string        Value   { get; set; } = string.Empty;
    /// <summary>Milliseconds to wait (Delay steps only).</summary>
    public int           DelayMs { get; set; }
}

// ── App profiles ──────────────────────────────────────────────────────────────

public sealed class AppProfileConfig
{
    public string ProcessName { get; set; } = string.Empty;
    public string MenuId      { get; set; } = "default";
}

// ── Enums ─────────────────────────────────────────────────────────────────────

public enum ActionType
{
    None,
    LaunchApp,
    OpenUrl,
    KeyCombo,
    Media,
    RunScript,
    PasteClipboard,
    SubMenu,
    OpenSettings,
    FocusWindow,  // bring an existing window to the foreground (HWND is in-memory only)
    Macro,        // run an ordered sequence of keystroke/text/delay steps
    OpenFolder,   // open a folder in File Explorer (FolderPath)
    RunCommand,   // Win+R semantics: expand env vars, shell-execute (Command, RunAsAdmin)
}

public enum MacroStepType
{
    TypeText,  // type a literal string (sent as Unicode, layout-independent)
    KeyPress,  // press+release a chord, e.g. "Ctrl+S"
    KeyDown,   // press a single key and hold it (paired with a later KeyUp)
    KeyUp,     // release a single key
    Delay,     // wait DelayMs milliseconds
}

public enum MediaActionType
{
    PlayPause,
    Next,
    Previous,
    VolumeUp,
    VolumeDown,
    Mute,
}

public enum SelectionMode
{
    HoverDwell = 0,
    Click      = 1,
    Flick      = 2,  // cursor direction from center determines slice; execute on trigger release/re-tap
}

public enum VolumeRingVisibility
{
    AlwaysVisible = 0,  // always show the volume arc around the ring
    OnChange      = 1,  // show briefly after a scroll-wheel volume action, then fade out
    Hidden        = 2,  // never show the volume arc
}
