// AeroDial — BuiltInThemes.cs
// The eleven built-in preset themes, defined in code so they're always available
// even if the /themes folder is missing. These serve as the definitive reference
// for what a well-formed theme looks like.
// (Note: a Frost theme is also defined below but intentionally left out of All.)
//
// All themes support the gradient ring:
//   SliceGradientInner  = color at inner radius (darker, toward centre)
//   SliceGradientOuter  = color at outer radius (slightly lighter)
//   GlowColor           = semi-transparent accent used for the hover blur glow
//   RingBorderColor     = explicit border circle color for L2/L3 rings (empty = use SliceStroke)

namespace AeroDial.Themes;

internal static class BuiltInThemes
{
    public static IReadOnlyList<AeroTheme> All =>
    [
        Obsidian,
        Ember,
        MidnightTeal,
        Chalk,
        Neon,
        Cyberpunk,
        Ocean,
        Sunset,
        Matrix,
        Arctic,
        Sakura,
    ];

    // ── 1. Obsidian (default) ─────────────────────────────────────────────
    public static AeroTheme Obsidian => new()
    {
        Name              = "Obsidian",
        Description       = "Dark background with purple accent. Default AeroDial look.",
        DimColor          = "#55000000",

        // Flat fallbacks (used by custom themes that omit gradient props)
        SliceFill         = "#E61A1A2E",
        SliceFillHover    = "#E6252540",
        // Borders: dark purple instead of white so the ring looks seamless
        SliceStroke       = "#20282845",
        SliceStrokeHover  = "#CC7C6EF7",
        SliceStrokeWidth  = 0.8f,

        //deep navy inner → dark purple outer
        SliceGradientInner      = "#E60C0C1A",
        SliceGradientOuter      = "#E6181828",
        SliceGradientInnerHover = "#E6111132",
        SliceGradientOuterHover = "#E6202042",

        GlowColor         = "#887C6EF7",   // purple glow

        CenterFill        = "#E6080818",
        CenterStroke      = "#20282845",
        IconTint          = "#BBFFFFFF",
        IconTintHover     = "#FFFFFFFF",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99FFFFFF",
        LabelColorHover   = "#FFFFFFFF",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC080818",
        BreadcrumbText    = "#99FFFFFF",
        AccentColor       = "#FF7C6EF7",
        RingBorderColor   = "#20282845",
    };

    // ── 2. Frost ──────────────────────────────────────────────────────────
    public static AeroTheme Frost => new()
    {
        Name              = "Frost",
        Description       = "Light frosted-glass look. Works great on bright desktops.",
        // Slightly more dim so L2/L3 rings stand out against any background
        DimColor          = "#25000000",

        SliceFill         = "#DDFAFCFF",
        SliceFillHover    = "#EEE8F4FF",
        SliceStroke       = "#4464B4F0",
        SliceStrokeHover  = "#BB378ADD",
        SliceStrokeWidth  = 0.8f,

        // Slightly more opaque gradients so L2/L3 rings are visible
        SliceGradientInner      = "#EEE0EBFF",
        SliceGradientOuter      = "#EEF0F8FF",
        SliceGradientInnerHover = "#F0D0E0FF",
        SliceGradientOuterHover = "#F0EAF5FF",

        GlowColor         = "#60378ADD",   // cool blue glow

        CenterFill        = "#EEFCFEFF",
        CenterStroke      = "#3364B4F0",
        // Darker icon tint for better contrast on the bright frost background
        IconTint          = "#EE0A3272",
        IconTintHover     = "#FF052A5E",
        LabelColor        = "#BB185FA5",
        LabelColorHover   = "#FF042C53",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CCF0F8FF",
        BreadcrumbText    = "#BB185FA5",
        AccentColor       = "#FF378ADD",
        RingBorderColor   = "#4464B4F0",
    };

    // ── 3. Ember ──────────────────────────────────────────────────────────
    public static AeroTheme Ember => new()
    {
        Name              = "Ember",
        Description       = "Dark warm tones with a glowing orange accent.",
        DimColor          = "#55100800",

        SliceFill         = "#E61F120A",
        SliceFillHover    = "#E6301A08",
        // Slightly more visible border so slices are distinguishable
        SliceStroke       = "#40281800",
        SliceStrokeHover  = "#CCE8593C",
        SliceStrokeWidth  = 0.8f,

        // Very dark brown inner → slightly warmer outer
        SliceGradientInner      = "#E60E0700",
        SliceGradientOuter      = "#E61C1005",
        SliceGradientInnerHover = "#E6180C00",
        SliceGradientOuterHover = "#E6281808",

        GlowColor         = "#90E8593C",

        CenterFill        = "#E6090500",
        CenterStroke      = "#30200A00",
        IconTint          = "#BBF5A87A",
        IconTintHover     = "#FFF5A87A",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99F5A87A",
        LabelColorHover   = "#FFF5A87A",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC0C0800",
        BreadcrumbText    = "#99F5A87A",
        AccentColor       = "#FFE8593C",
        RingBorderColor   = "#30281800",
    };

    // ── 4. Midnight Teal ─────────────────────────────────────────────────
    public static AeroTheme MidnightTeal => new()
    {
        Name              = "Midnight Teal",
        Description       = "Deep dark teal with crisp teal accent.",
        DimColor          = "#55000D0D",

        SliceFill         = "#E60D2224",
        SliceFillHover    = "#E6102A2C",
        SliceStroke       = "#1E0C2018",
        SliceStrokeHover  = "#CC1D9E75",
        SliceStrokeWidth  = 0.8f,

        // Near-black teal inner → dark teal outer
        SliceGradientInner      = "#E6040D10",
        SliceGradientOuter      = "#E60A1A1E",
        SliceGradientInnerHover = "#E6061218",
        SliceGradientOuterHover = "#E60E2228",

        GlowColor         = "#801D9E75",   // teal glow

        CenterFill        = "#E6030A0C",
        CenterStroke      = "#16182830",
        IconTint          = "#BB9FE1CB",
        IconTintHover     = "#FF9FE1CB",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#999FE1CB",
        LabelColorHover   = "#FF9FE1CB",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC050D10",
        BreadcrumbText    = "#999FE1CB",
        AccentColor       = "#FF1D9E75",
        RingBorderColor   = "",
    };

    // ── 5. Chalk ─────────────────────────────────────────────────────────
    public static AeroTheme Chalk => new()
    {
        Name              = "Chalk",
        Description       = "Clean, minimal light theme with soft neutral gradient.",
        DimColor          = "#14000000",

        SliceFill         = "#EEF0EDE8",
        SliceFillHover    = "#EEE2DDD5",
        SliceStroke       = "#33B8B0A2",
        SliceStrokeHover  = "#774A4A4A",
        SliceStrokeWidth  = 0.7f,

        // Light sand inner → off-white outer
        SliceGradientInner      = "#EEE2DFDA",
        SliceGradientOuter      = "#EEF4F1ED",
        SliceGradientInnerHover = "#EED6D1C9",
        SliceGradientOuterHover = "#EEE6E2DC",

        GlowColor         = "#385F5E5A",   // subtle warm grey glow

        CenterFill        = "#EEF6F4F0",
        CenterStroke      = "#22000000",
        // Mid-tone dark tint — dark enough to see on light bg, light enough to preserve custom icon colors
        IconTint          = "#CC484844",
        IconTintHover     = "#FF2C2C28",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#AA3A3836",
        LabelColorHover   = "#FF1A1A18",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CCE8E5DF",
        BreadcrumbText    = "#AA3A3836",
        AccentColor       = "#FF5F5E5A",
        RingBorderColor   = "#33000000",
    };

    // ── 6. Neon ───────────────────────────────────────────────────────────
    public static AeroTheme Neon => new()
    {
        Name              = "Neon",
        Description       = "Near-black with vivid pink/magenta accent and deep glow.",
        // Lighter dim so the background shows through more
        DimColor          = "#44000000",

        SliceFill         = "#E60A0A14",
        SliceFillHover    = "#E6140A1A",
        SliceStroke       = "#161A1A30",
        SliceStrokeHover  = "#CCD4537E",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E6040410",
        SliceGradientOuter      = "#E60A0A18",
        SliceGradientInnerHover = "#E607071A",
        SliceGradientOuterHover = "#E610102A",

        GlowColor         = "#88D4537E",

        CenterFill        = "#E6020210",
        CenterStroke      = "#161A1A30",
        IconTint          = "#BBED93B1",
        IconTintHover     = "#FFED93B1",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99ED93B1",
        LabelColorHover   = "#FFED93B1",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC04040E",
        BreadcrumbText    = "#99ED93B1",
        AccentColor       = "#FFD4537E",
        RingBorderColor   = "",
    };

    // ── 7. Cyberpunk ──────────────────────────────────────────────────────
    public static AeroTheme Cyberpunk => new()
    {
        Name              = "Cyberpunk",
        Description       = "Near-black chrome with yellow accent and aqua glow.",
        DimColor          = "#55000000",

        // Darker slices for a more cinematic black feel
        SliceFill         = "#E614141C",
        SliceFillHover    = "#4D101820",
        // Very subtle dark border — yellow was too strong; aqua on hover for the 2077 palette
        SliceStroke       = "#28080820",
        SliceStrokeHover  = "#AA00CCEE",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E80A0A12",
        SliceGradientOuter      = "#E8181820",
        SliceGradientInnerHover = "#E810101E",
        SliceGradientOuterHover = "#E81C1C2C",

        // Aqua glow for Cyberpunk 2077 palette
        GlowColor         = "#8000CCEE",

        CenterFill        = "#E8181820",
        CenterStroke      = "#28080820",
        IconTint          = "#BBA0A090",
        IconTintHover     = "#FFF0D020",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99E8E0C0",
        LabelColorHover   = "#FFE8E0C0",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC0A0A12",
        BreadcrumbText    = "#99E8E0C0",
        // Yellow accent stays for dots, indicator arc, center label
        AccentColor       = "#FFF0D020",
        RingBorderColor   = "#2800CCEE",
    };

    // ── 8. Ocean ──────────────────────────────────────────────────────────
    public static AeroTheme Ocean => new()
    {
        Name              = "Ocean",
        Description       = "Deep blue with electric cyan glow.",
        DimColor          = "#55000000",

        SliceFill         = "#E00A1628",
        SliceFillHover    = "#4D00C8FF",
        SliceStroke       = "#200A1626",
        SliceStrokeHover  = "#CC00C8FF",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E0060E1A",
        SliceGradientOuter      = "#E0101E38",
        SliceGradientInnerHover = "#E00A162A",
        SliceGradientOuterHover = "#E0122040",

        GlowColor         = "#8000C8FF",

        CenterFill        = "#E8122440",
        CenterStroke      = "#4D288CC8",
        IconTint          = "#BB70A0C0",
        IconTintHover     = "#FF00C8FF",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99B0D8F0",
        LabelColorHover   = "#FFB0D8F0",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC060E1A",
        BreadcrumbText    = "#99B0D8F0",
        AccentColor       = "#FF00C8FF",
        RingBorderColor   = "",
    };

    // ── 9. Sunset ─────────────────────────────────────────────────────────
    public static AeroTheme Sunset => new()
    {
        Name              = "Sunset",
        Description       = "Warm amber and rose tones on deep crimson.",
        DimColor          = "#66000000",

        SliceFill         = "#E02A1820",
        SliceFillHover    = "#4DFF7848",
        // Subtle border — original was too visible against the warm slices
        SliceStroke       = "#28100808",
        SliceStrokeHover  = "#77FF7848",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E01A0E14",
        SliceGradientOuter      = "#E0321C28",
        SliceGradientInnerHover = "#E0221420",
        SliceGradientOuterHover = "#E03C2432",

        GlowColor         = "#80FF7848",

        CenterFill        = "#E83A2028",
        CenterStroke      = "#28100808",
        IconTint          = "#BBC8A090",
        IconTintHover     = "#FFFF7848",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99F0D0C0",
        LabelColorHover   = "#FFF0D0C0",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC1A0E14",
        BreadcrumbText    = "#99F0D0C0",
        AccentColor       = "#FFFF7848",
        RingBorderColor   = "#25100808",
    };

    // ── 10. Matrix ────────────────────────────────────────────────────────
    public static AeroTheme Matrix => new()
    {
        Name              = "Matrix",
        Description       = "Pure black with neon green accent.",
        DimColor          = "#66000000",

        SliceFill         = "#EB081208",
        SliceFillHover    = "#4D00FF60",
        // Very subtle dark border — original green border was too visible
        SliceStroke       = "#22080C08",
        SliceStrokeHover  = "#7700FF60",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#EB040A04",
        SliceGradientOuter      = "#EB0C180C",
        SliceGradientInnerHover = "#EB081008",
        SliceGradientOuterHover = "#EB101C10",

        GlowColor         = "#8000FF60",

        CenterFill        = "#E8102010",
        CenterStroke      = "#22080C08",
        IconTint          = "#BB60A870",
        IconTintHover     = "#FF00FF60",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99A0E0B0",
        LabelColorHover   = "#FFA0E0B0",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC040A04",
        BreadcrumbText    = "#99A0E0B0",
        AccentColor       = "#FF00FF60",
        RingBorderColor   = "#20080C08",
    };

    // ── 11. Arctic ────────────────────────────────────────────────────────
    public static AeroTheme Arctic => new()
    {
        Name              = "Arctic",
        Description       = "Icy blue-white crystal. Bright and clean.",
        DimColor          = "#18000000",

        SliceFill         = "#D9DCE8F4",
        SliceFillHover    = "#4D40A0E0",
        SliceStroke       = "#3396A8BE",
        SliceStrokeHover  = "#CC40A0E0",
        SliceStrokeWidth  = 0.7f,

        SliceGradientInner      = "#D9C8D8EC",
        SliceGradientOuter      = "#D9E4EEF8",
        SliceGradientInnerHover = "#D9B8CCE0",
        SliceGradientOuterHover = "#D9D0E4F4",

        GlowColor         = "#6040A0E0",

        CenterFill        = "#F0F0F6FC",
        CenterStroke      = "#333C78B4",
        // Medium steel-blue tint — visible on ice-white bg, preserves custom icon color information
        IconTint          = "#FF0284CC",
        IconTintHover     = "#FF40A0E0",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CC1A3A58",
        LabelColorHover   = "#FF0A2040",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CCC8D8EC",
        BreadcrumbText    = "#CC1A3A58",
        AccentColor       = "#FF40A0E0",
        RingBorderColor   = "",
    };

    // ── 12. Sakura ────────────────────────────────────────────────────────
    public static AeroTheme Sakura => new()
    {
        Name              = "Sakura",
        Description       = "Soft cherry blossom pink on deep burgundy.",
        DimColor          = "#55100008",

        SliceFill         = "#E02A1A24",
        SliceFillHover    = "#4DF080B0",
        // Subtle dark wine border — original saturated pink was too strong
        SliceStroke       = "#22180A14",
        SliceStrokeHover  = "#88F080B0",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E01A0E18",
        SliceGradientOuter      = "#E0321A2C",
        SliceGradientInnerHover = "#E020121E",
        SliceGradientOuterHover = "#E03C2236",

        GlowColor         = "#80F080B0",

        CenterFill        = "#E83A2232",
        CenterStroke      = "#22180A14",
        IconTint          = "#BBC090A8",
        IconTintHover     = "#FFF080B0",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#99F0D0E0",
        LabelColorHover   = "#FFF0D0E0",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#CC1A0E18",
        BreadcrumbText    = "#99F0D0E0",
        AccentColor       = "#FFF080B0",
        RingBorderColor   = "#20180A14",
    };
}
