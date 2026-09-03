// AeroDial — BuiltInThemes.cs
// The built-in preset themes, defined in code so they're always available
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
        Graphite,
        Nord,
        TokyoNight,
        OnyxIce,
        RosePine,
        Aurora,
        RoyalGold,
        Crimson,
        Porcelain,
        Glass,
        Synthwave,
        Copper,
        SolarizedDark,
        Champagne,
        Dusk,
        HighContrast,
        Ultraviolet,
        Ink,
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

    // ── 12. Graphite ────────────────────────────────────────────
    public static AeroTheme Graphite => new()
    {
        Name              = "Graphite",
        Description       = "Monochrome, high contrast, gets out of the way.",
        DimColor          = "#55000000",

        SliceFill         = "#E61E1E1E",
        SliceFillHover    = "#E63C3C3C",
        SliceStroke       = "#26343434",
        SliceStrokeHover  = "#CCFFFFFF",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E6141414",
        SliceGradientOuter      = "#E61E1E1E",
        SliceGradientInnerHover = "#E62E2E2E",
        SliceGradientOuterHover = "#E63C3C3C",

        GlowColor         = "#66FFFFFF",

        CenterFill        = "#E60E0E0E",
        CenterStroke      = "#26343434",
        IconTint          = "#CCFFFFFF",
        IconTintHover     = "#FFFFFFFF",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCFFFFFF",
        LabelColorHover   = "#FFFFFFFF",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E60E0E0E",
        BreadcrumbText    = "#CCFFFFFF",
        AccentColor       = "#FFEDEDED",
        RingBorderColor   = "#26343434",
    };

    // ── 13. Nord ────────────────────────────────────────────────
    public static AeroTheme Nord => new()
    {
        Name              = "Nord",
        Description       = "Muted arctic blue-grey, easy on the eyes.",
        DimColor          = "#55000000",

        SliceFill         = "#E63B4252",
        SliceFillHover    = "#E64C566A",
        SliceStroke       = "#284C566A",
        SliceStrokeHover  = "#CC88C0D0",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E62E3440",
        SliceGradientOuter      = "#E63B4252",
        SliceGradientInnerHover = "#E63B4252",
        SliceGradientOuterHover = "#E64C566A",

        GlowColor         = "#8888C0D0",

        CenterFill        = "#E62E3440",
        CenterStroke      = "#284C566A",
        IconTint          = "#CCECEFF4",
        IconTintHover     = "#FFECEFF4",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCECEFF4",
        LabelColorHover   = "#FFECEFF4",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E62E3440",
        BreadcrumbText    = "#CCECEFF4",
        AccentColor       = "#FF88C0D0",
        RingBorderColor   = "#284C566A",
    };

    // ── 14. Tokyo Night ─────────────────────────────────────────
    public static AeroTheme TokyoNight => new()
    {
        Name              = "Tokyo Night",
        Description       = "Deep indigo with a cool blue glow.",
        DimColor          = "#55000000",

        SliceFill         = "#E624283B",
        SliceFillHover    = "#E6363C5E",
        SliceStroke       = "#2A414868",
        SliceStrokeHover  = "#CC7AA2F7",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E61A1B26",
        SliceGradientOuter      = "#E624283B",
        SliceGradientInnerHover = "#E62A2E45",
        SliceGradientOuterHover = "#E6363C5E",

        GlowColor         = "#997AA2F7",

        CenterFill        = "#E61A1B26",
        CenterStroke      = "#2A414868",
        IconTint          = "#CCC0CAF5",
        IconTintHover     = "#FFC0CAF5",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCC0CAF5",
        LabelColorHover   = "#FFC0CAF5",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E61A1B26",
        BreadcrumbText    = "#CCC0CAF5",
        AccentColor       = "#FF7AA2F7",
        RingBorderColor   = "#2A414868",
    };

    // ── 15. Onyx Ice ────────────────────────────────────────────
    public static AeroTheme OnyxIce => new()
    {
        Name              = "Onyx Ice",
        Description       = "Black glass with an icy edge.",
        DimColor          = "#5C000000",

        SliceFill         = "#E612161C",
        SliceFillHover    = "#E6243240",
        SliceStroke       = "#242E3846",
        SliceStrokeHover  = "#CC9FE8FF",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E60A0C10",
        SliceGradientOuter      = "#E612161C",
        SliceGradientInnerHover = "#E61A2430",
        SliceGradientOuterHover = "#E6243240",

        GlowColor         = "#997FDFFF",

        CenterFill        = "#E6070910",
        CenterStroke      = "#242E3846",
        IconTint          = "#CCD8ECF7",
        IconTintHover     = "#FFFFFFFF",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCD8ECF7",
        LabelColorHover   = "#FFFFFFFF",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E6070910",
        BreadcrumbText    = "#CCD8ECF7",
        AccentColor       = "#FF7FDFFF",
        RingBorderColor   = "#242E3846",
    };

    // ── 16. Rose Pine ───────────────────────────────────────────
    public static AeroTheme RosePine => new()
    {
        Name              = "Rose Pine",
        Description       = "Muted rose and gold on deep plum.",
        DimColor          = "#55000000",

        SliceFill         = "#E61F1D2E",
        SliceFillHover    = "#E6302B47",
        SliceStroke       = "#2A403D5C",
        SliceStrokeHover  = "#CCEBBCBA",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E6191724",
        SliceGradientOuter      = "#E61F1D2E",
        SliceGradientInnerHover = "#E626233A",
        SliceGradientOuterHover = "#E6302B47",

        GlowColor         = "#99EBBCBA",

        CenterFill        = "#E6191724",
        CenterStroke      = "#2A403D5C",
        IconTint          = "#CCE0DEF4",
        IconTintHover     = "#FFE0DEF4",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCE0DEF4",
        LabelColorHover   = "#FFE0DEF4",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E6191724",
        BreadcrumbText    = "#CCE0DEF4",
        AccentColor       = "#FFEBBCBA",
        RingBorderColor   = "#2A403D5C",
    };

    // ── 17. Aurora ──────────────────────────────────────────────
    public static AeroTheme Aurora => new()
    {
        Name              = "Aurora",
        Description       = "Deep teal to green with a luminous mint edge.",
        DimColor          = "#55000000",

        SliceFill         = "#E60A3A2E",
        SliceFillHover    = "#E6116B4E",
        SliceStroke       = "#2A0F5C46",
        SliceStrokeHover  = "#CC4ADE9B",
        SliceStrokeWidth  = 0.9f,

        SliceGradientInner      = "#E6041F1A",
        SliceGradientOuter      = "#E60A3A2E",
        SliceGradientInnerHover = "#E60B4A38",
        SliceGradientOuterHover = "#E6116B4E",

        GlowColor         = "#AA4ADE9B",

        CenterFill        = "#E604211B",
        CenterStroke      = "#2A0F5C46",
        IconTint          = "#CCD8FFEC",
        IconTintHover     = "#FFEAFFF5",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCD8FFEC",
        LabelColorHover   = "#FFEAFFF5",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E604211B",
        BreadcrumbText    = "#CCD8FFEC",
        AccentColor       = "#FF4ADE9B",
        RingBorderColor   = "#2A0F5C46",
    };

    // ── 18. Royal Gold ──────────────────────────────────────────
    public static AeroTheme RoyalGold => new()
    {
        Name              = "Royal Gold",
        Description       = "Near-black and warm gold, deliberately luxurious.",
        DimColor          = "#66000000",

        SliceFill         = "#E62A2010",
        SliceFillHover    = "#E6573F16",
        SliceStroke       = "#2A5C4520",
        SliceStrokeHover  = "#CCE8C063",
        SliceStrokeWidth  = 0.9f,

        SliceGradientInner      = "#E61A1408",
        SliceGradientOuter      = "#E62A2010",
        SliceGradientInnerHover = "#E63C2D12",
        SliceGradientOuterHover = "#E6573F16",

        GlowColor         = "#AAE8C063",

        CenterFill        = "#E6140F06",
        CenterStroke      = "#2A5C4520",
        IconTint          = "#CCF7ECD2",
        IconTintHover     = "#FFFFF8E7",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCF7ECD2",
        LabelColorHover   = "#FFFFF8E7",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E6140F06",
        BreadcrumbText    = "#CCF7ECD2",
        AccentColor       = "#FFE8C063",
        RingBorderColor   = "#2A5C4520",
    };

    // ── 19. Crimson ─────────────────────────────────────────────
    public static AeroTheme Crimson => new()
    {
        Name              = "Crimson",
        Description       = "Oxblood to scarlet, dramatic without being loud.",
        DimColor          = "#66000000",

        SliceFill         = "#E62C0F16",
        SliceFillHover    = "#E6741B2E",
        SliceStroke       = "#2A5A1A28",
        SliceStrokeHover  = "#CCFF4D6A",
        SliceStrokeWidth  = 0.9f,

        SliceGradientInner      = "#E61A0A0E",
        SliceGradientOuter      = "#E62C0F16",
        SliceGradientInnerHover = "#E64A1220",
        SliceGradientOuterHover = "#E6741B2E",

        GlowColor         = "#AAFF3355",

        CenterFill        = "#E6150809",
        CenterStroke      = "#2A5A1A28",
        IconTint          = "#CCFFDCE2",
        IconTintHover     = "#FFFFF0F3",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCFFDCE2",
        LabelColorHover   = "#FFFFF0F3",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E6150809",
        BreadcrumbText    = "#CCFFDCE2",
        AccentColor       = "#FFFF4D6A",
        RingBorderColor   = "#2A5A1A28",
    };

    // ── 20. Porcelain ───────────────────────────────────────────
    public static AeroTheme Porcelain => new()
    {
        Name              = "Porcelain",
        Description       = "Warm off-white with a bronze accent, for bright desktops.",
        DimColor          = "#22000000",

        SliceFill         = "#F7F1E9D9",
        SliceFillHover    = "#FAE9DCC0",
        SliceStroke       = "#33BFB3A0",
        SliceStrokeHover  = "#CC9A6E24",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#F7FFFCF4",
        SliceGradientOuter      = "#F7F1E9D9",
        SliceGradientInnerHover = "#FAFFFFFF",
        SliceGradientOuterHover = "#FAE9DCC0",

        GlowColor         = "#889A6E24",

        CenterFill        = "#FAFFFDF6",
        CenterStroke      = "#33BFB3A0",
        IconTint          = "#F22E2418",
        IconTintHover     = "#FF1A1208",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#F22E2418",
        LabelColorHover   = "#FF1A1208",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#FAFFFDF6",
        BreadcrumbText    = "#F22E2418",
        AccentColor       = "#FFA9761F",
        RingBorderColor   = "#33BFB3A0",
    };

    // ── 21. Glass ───────────────────────────────────────────────
    public static AeroTheme Glass => new()
    {
        Name              = "Glass",
        Description       = "Smoked glass, the desktop shows through.",
        DimColor          = "#33000000",

        SliceFill         = "#73182028",
        SliceFillHover    = "#8C36434F",
        SliceStroke       = "#24FFFFFF",
        SliceStrokeHover  = "#CCBFD8FF",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#8C121820",
        SliceGradientOuter      = "#73182028",
        SliceGradientInnerHover = "#A62A3644",
        SliceGradientOuterHover = "#8C36434F",

        GlowColor         = "#66A8C8FF",

        CenterFill        = "#A60E141C",
        CenterStroke      = "#24FFFFFF",
        IconTint          = "#DDFFFFFF",
        IconTintHover     = "#FFFFFFFF",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#DDFFFFFF",
        LabelColorHover   = "#FFFFFFFF",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#A60E141C",
        BreadcrumbText    = "#DDFFFFFF",
        AccentColor       = "#FFBFD8FF",
        RingBorderColor   = "#24FFFFFF",
    };

    // ── 22. Synthwave ───────────────────────────────────────────
    public static AeroTheme Synthwave => new()
    {
        Name              = "Synthwave",
        Description       = "Magenta to violet with a cyan rim and heavy glow.",
        DimColor          = "#66120024",

        SliceFill         = "#E6461063",
        SliceFillHover    = "#E6A3199E",
        SliceStroke       = "#2A6B2A96",
        SliceStrokeHover  = "#CC00E5FF",
        SliceStrokeWidth  = 1f,

        SliceGradientInner      = "#E62A0A3E",
        SliceGradientOuter      = "#E6461063",
        SliceGradientInnerHover = "#E66B1490",
        SliceGradientOuterHover = "#E6A3199E",

        GlowColor         = "#AAFF2FD0",

        CenterFill        = "#E61A0426",
        CenterStroke      = "#2A6B2A96",
        IconTint          = "#DDF6D8FF",
        IconTintHover     = "#FFFFFFFF",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#DDF6D8FF",
        LabelColorHover   = "#FFFFFFFF",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E61A0426",
        BreadcrumbText    = "#DDF6D8FF",
        AccentColor       = "#FF00E5FF",
        RingBorderColor   = "#2A6B2A96",
    };

    // ── 23. Copper ──────────────────────────────────────────────
    public static AeroTheme Copper => new()
    {
        Name              = "Copper",
        Description       = "Aged metal, warm without being orange.",
        DimColor          = "#5A0B0603",

        SliceFill         = "#E63A1F0D",
        SliceFillHover    = "#E6884A1C",
        SliceStroke       = "#2A6B3E1E",
        SliceStrokeHover  = "#CCE09A5C",
        SliceStrokeWidth  = 0.9f,

        SliceGradientInner      = "#E6241309",
        SliceGradientOuter      = "#E63A1F0D",
        SliceGradientInnerHover = "#E65C2F12",
        SliceGradientOuterHover = "#E6884A1C",

        GlowColor         = "#99D98A4B",

        CenterFill        = "#E61B0F07",
        CenterStroke      = "#2A6B3E1E",
        IconTint          = "#CCF3D9C2",
        IconTintHover     = "#FFFFF3E6",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCF3D9C2",
        LabelColorHover   = "#FFFFF3E6",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E61B0F07",
        BreadcrumbText    = "#CCF3D9C2",
        AccentColor       = "#FFE09A5C",
        RingBorderColor   = "#2A6B3E1E",
    };

    // ── 24. Solarized Dark ──────────────────────────────────────
    public static AeroTheme SolarizedDark => new()
    {
        Name              = "Solarized Dark",
        Description       = "The classic: teal base, amber accent.",
        DimColor          = "#55001A21",

        SliceFill         = "#E6073642",
        SliceFillHover    = "#E60F5F72",
        SliceStroke       = "#28586E75",
        SliceStrokeHover  = "#CC2AA198",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#E6002B36",
        SliceGradientOuter      = "#E6073642",
        SliceGradientInnerHover = "#E60A4A5A",
        SliceGradientOuterHover = "#E60F5F72",

        GlowColor         = "#992AA198",

        CenterFill        = "#E6002B36",
        CenterStroke      = "#28586E75",
        IconTint          = "#CC93A1A1",
        IconTintHover     = "#FFEEE8D5",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CC93A1A1",
        LabelColorHover   = "#FFEEE8D5",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E6002B36",
        BreadcrumbText    = "#CC93A1A1",
        AccentColor       = "#FFB58900",
        RingBorderColor   = "#28586E75",
    };

    // ── 25. Champagne ───────────────────────────────────────────
    public static AeroTheme Champagne => new()
    {
        Name              = "Champagne",
        Description       = "Pale gold on cream, the light side of Royal Gold.",
        DimColor          = "#1E000000",

        SliceFill         = "#F7F5EAD2",
        SliceFillHover    = "#FAEEDCB4",
        SliceStroke       = "#33C9B994",
        SliceStrokeHover  = "#CC9A7524",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#F7FFFBF0",
        SliceGradientOuter      = "#F7F5EAD2",
        SliceGradientInnerHover = "#FAFFFDF6",
        SliceGradientOuterHover = "#FAEEDCB4",

        GlowColor         = "#88C9A227",

        CenterFill        = "#FAFFFCF3",
        CenterStroke      = "#33C9B994",
        IconTint          = "#F2453212",
        IconTintHover     = "#FF2A1D06",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#F2453212",
        LabelColorHover   = "#FF2A1D06",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#FAFFFCF3",
        BreadcrumbText    = "#F2453212",
        AccentColor       = "#FFB08A2A",
        RingBorderColor   = "#33C9B994",
    };

    // ── 26. Dusk ────────────────────────────────────────────────
    public static AeroTheme Dusk => new()
    {
        Name              = "Dusk",
        Description       = "Teal dusk with warm amber highlights.",
        DimColor          = "#5A00070C",

        SliceFill         = "#E60D2A34",
        SliceFillHover    = "#E61C5460",
        SliceStroke       = "#2A18525E",
        SliceStrokeHover  = "#CCFFB454",
        SliceStrokeWidth  = 0.85f,

        SliceGradientInner      = "#E6071820",
        SliceGradientOuter      = "#E60D2A34",
        SliceGradientInnerHover = "#E6143C46",
        SliceGradientOuterHover = "#E61C5460",

        GlowColor         = "#99FFA53A",

        CenterFill        = "#E6051219",
        CenterStroke      = "#2A18525E",
        IconTint          = "#CCD6E9EE",
        IconTintHover     = "#FFFFE9C8",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCD6E9EE",
        LabelColorHover   = "#FFFFE9C8",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E6051219",
        BreadcrumbText    = "#CCD6E9EE",
        AccentColor       = "#FFFFB454",
        RingBorderColor   = "#2A18525E",
    };

    // ── 27. High Contrast ───────────────────────────────────────
    public static AeroTheme HighContrast => new()
    {
        Name              = "High Contrast",
        Description       = "Black, white and yellow. Readable on anything.",
        DimColor          = "#99000000",

        SliceFill         = "#FF000000",
        SliceFillHover    = "#FFF5C400",
        SliceStroke       = "#FFFFFFFF",
        SliceStrokeHover  = "#FFFFFFFF",
        SliceStrokeWidth  = 1.6f,

        SliceGradientInner      = "#FF000000",
        SliceGradientOuter      = "#FF000000",
        SliceGradientInnerHover = "#FFFFE400",
        SliceGradientOuterHover = "#FFF5C400",

        GlowColor         = "#66FFE400",

        CenterFill        = "#FF000000",
        CenterStroke      = "#FFFFFFFF",
        IconTint          = "#FFFFFFFF",
        IconTintHover     = "#FF000000",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#FFFFFFFF",
        LabelColorHover   = "#FF000000",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#FF000000",
        BreadcrumbText    = "#FFFFFFFF",
        AccentColor       = "#FFFFE400",
        RingBorderColor   = "#FFFFFFFF",
    };

    // ── 28. Ultraviolet ─────────────────────────────────────────
    public static AeroTheme Ultraviolet => new()
    {
        Name              = "Ultraviolet",
        Description       = "Saturated violet to magenta, the boldest option.",
        DimColor          = "#66100024",

        SliceFill         = "#E6300055",
        SliceFillHover    = "#E67B14C8",
        SliceStroke       = "#2A55219A",
        SliceStrokeHover  = "#CCC77DFF",
        SliceStrokeWidth  = 0.9f,

        SliceGradientInner      = "#E61A0033",
        SliceGradientOuter      = "#E6300055",
        SliceGradientInnerHover = "#E64F0A8C",
        SliceGradientOuterHover = "#E67B14C8",

        GlowColor         = "#AA9B30FF",

        CenterFill        = "#E6120021",
        CenterStroke      = "#2A55219A",
        IconTint          = "#CCE9D6FF",
        IconTintHover     = "#FFFFFFFF",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#CCE9D6FF",
        LabelColorHover   = "#FFFFFFFF",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#E6120021",
        BreadcrumbText    = "#CCE9D6FF",
        AccentColor       = "#FFB14DFF",
        RingBorderColor   = "#2A55219A",
    };

    // ── 29. Ink ─────────────────────────────────────────────────
    public static AeroTheme Ink => new()
    {
        Name              = "Ink",
        Description       = "Paper and black ink, one red accent.",
        DimColor          = "#1A000000",

        SliceFill         = "#F7F1EFE9",
        SliceFillHover    = "#FAE7E4DC",
        SliceStroke       = "#2E9A9084",
        SliceStrokeHover  = "#CC1A1A1A",
        SliceStrokeWidth  = 0.8f,

        SliceGradientInner      = "#F7FBFAF7",
        SliceGradientOuter      = "#F7F1EFE9",
        SliceGradientInnerHover = "#FAFFFFFF",
        SliceGradientOuterHover = "#FAE7E4DC",

        GlowColor         = "#66C0392B",

        CenterFill        = "#FAFDFCFA",
        CenterStroke      = "#2E9A9084",
        IconTint          = "#F2101010",
        IconTintHover     = "#FF000000",
        IconStrokeScale   = 0.25f,
        LabelColor        = "#F2101010",
        LabelColorHover   = "#FF000000",
        LabelFontSize     = 11f,
        BreadcrumbFill    = "#FAFDFCFA",
        BreadcrumbText    = "#F2101010",
        AccentColor       = "#FFC0392B",
        RingBorderColor   = "#2E9A9084",
    };

}
