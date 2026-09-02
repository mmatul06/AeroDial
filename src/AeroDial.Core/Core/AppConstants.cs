// AeroDial — AppConstants.cs
// Single source of truth for application-wide constants.
// Tune geometry and timing values here rather than scattering them through the codebase.

namespace AeroDial.Core;

internal static class AppConstants
{
    // ── Identity ──────────────────────────────────────────────────────────
    public const string AppName            = "AeroDial";
    public const string Version            = "3.0.0";
    public const string Author             = "Muhtasim Mahbub";
    public const string Company            = "3M Design Solutions";
    public const string Website            = "https://3mdesignsolutions.com";
    public const string GitHubUrl          = "https://github.com/mmatul06/AeroDial";
    public const string GitHubReleasesApiUrl = "https://api.github.com/repos/mmatul06/AeroDial/releases/latest";
    public const string LicenseName        = "MIT License";

    // ── IPC ───────────────────────────────────────────────────────────────
    public const string ActivationEventName = "Global\\AeroDial_Activate_3MDS";

    // ── Paths ─────────────────────────────────────────────────────────────
    public static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);

    public static readonly string ConfigPath =
        Path.Combine(AppDataDir, "config.json");

    public static readonly string ConfigBackupPath =
        Path.Combine(AppDataDir, "config.json.bak");

    public static readonly string LogPath =
        Path.Combine(AppDataDir, "aerodial.log");

    public static readonly string ThemesDir =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "themes");

    public static readonly string UserThemesDir =
        Path.Combine(AppDataDir, "themes");

    public static readonly string PresetsDir =
        Path.Combine(AppDataDir, "presets");

    // ── Dynamic submenu magic IDs ─────────────────────────────────────────
    /// <summary>Reserved SubMenuId that builds a live list of visible app windows.</summary>
    public const string ActiveTasksMenuId       = "__active_tasks__";
    /// <summary>Reserved SubMenuId that builds a live list from Windows clipboard history.</summary>
    public const string ClipboardHistoryMenuId  = "__clipboard_history__";

    // ── Overlay geometry ──────────────────────────────────────────────────
    /// <summary>Outer radius of the radial ring in logical pixels (before DPI scale).</summary>
    public const float RingOuterRadius     = 140f;
    /// <summary>Inner dead-zone radius. Cursor here = no slice selected.</summary>
    public const float RingInnerRadius     = 46f;
    /// <summary>Radius at which slice icons are centred.</summary>
    public const float IconOrbitRadius     = 100f;
    /// <summary>Total canvas size in logical pixels — sized to accommodate L2 and L3 rings.</summary>
    public const int   CanvasSize          = 600;

    // ── Child ring geometry (outer concentric ring shown for submenus) ─────
    /// <summary>Gap between the main ring outer edge and the child ring inner edge.</summary>
    public const float ChildRingGap        = 8f;
    /// <summary>Radial depth of the child ring at full size.</summary>
    public const float ChildRingThickness  = 70f;
    /// <summary>Radial depth of the L2 ring when a L3 ring is also present (dynamic ring thinning).</summary>
    public const float ThinChildRingThickness = 44f;

    // ── Animation ─────────────────────────────────────────────────────────
    public const int   AnimOpenMs          = 300;   // ring expand duration (easeOutBack)
    public const int   AnimCloseMs         = 100;   // ring collapse duration
    public const int   HoverDwellMs        = 350;   // hover-to-select dwell time
    public const int   FrameIntervalMs     = 8;     // ~120 fps target; actual ~80-100fps after render overhead
    public const float ShimmerPeriodMs     = 12000f; // shimmer rotation period

    // ── Rendering ─────────────────────────────────────────────────────────
    public const float DefaultGapDegrees   = 2.5f;  // gap between slice arcs
    public const float SubMenuArrowSize    = 8f;
}
