// AeroDial — ThemeModel.cs
// Theme data model. Every visual property of the overlay is here.
// Themes are stored as JSON files in the /themes folder.

using System.Text.Json.Serialization;
using SkiaSharp;

namespace AeroDial.Themes;

public sealed class AeroTheme
{
    // ── Identity ──────────────────────────────────────────────────────────
    public string Name        { get; set; } = "Custom";
    public string Description { get; set; } = "";
    public bool   IsBuiltIn   { get; set; } = false;

    // ── Background dim ────────────────────────────────────────────────────
    /// <summary>ARGB hex color of the full-screen dim overlay behind the ring.</summary>
    public string DimColor    { get; set; } = "#66000000";

    // ── Ring (flat fill — used as fallback when gradient properties are empty) ────
    public string SliceFill        { get; set; } = "#CC1A1A2E";
    public string SliceFillHover   { get; set; } = "#CC2D2D50";
    public string SliceStroke      { get; set; } = "#44FFFFFF";
    public string SliceStrokeHover { get; set; } = "#AA7C6EF7";
    public float  SliceStrokeWidth { get; set; } = 0.8f;
    public float  SliceCornerBlend { get; set; } = 0f; // 0=sharp, 1=pill (future)

    // ── Gradient (radial fill — leave empty to fall back to flat SliceFill) ──
    /// <summary>Slice color at the inner radius. Darker = more depth.</summary>
    public string SliceGradientInner      { get; set; } = "";
    /// <summary>Slice color at the outer radius. Slightly lighter than inner.</summary>
    public string SliceGradientOuter      { get; set; } = "";
    /// <summary>Hovered inner color.</summary>
    public string SliceGradientInnerHover { get; set; } = "";
    /// <summary>Hovered outer color.</summary>
    public string SliceGradientOuterHover { get; set; } = "";

    // ── Glow ─────────────────────────────────────────────────────────────────────
    /// <summary>Color of the blurred outer glow on the hovered slice. Falls back to AccentColor@40% if empty.</summary>
    public string GlowColor               { get; set; } = "";

    // ── Center circle ─────────────────────────────────────────────────────
    public string CenterFill       { get; set; } = "#CC111122";
    public string CenterStroke     { get; set; } = "#33FFFFFF";

    // ── Icon & label ──────────────────────────────────────────────────────
    public string IconTint         { get; set; } = "#CCFFFFFF";
    public string IconTintHover    { get; set; } = "#FFFFFFFF";
    /// <summary>
    /// Multiplier applied to built-in icon stroke widths. 1.0 = original, 1.5 = 50% thicker.
    /// Has no effect on raster icons (.exe, .png, etc.) — those are always drawn at full size.
    /// Clamp with <see cref="StrokeScale"/> before use.
    /// </summary>
    public float  IconStrokeScale  { get; set; } = 1.0f;
    /// <summary>
    /// Multiplier applied to the drawn icon size on the ring. 1.0 = default (22 px at scale 1).
    /// Applies to every icon kind, including exe and image icons. Clamp with <see cref="SizeScale"/>.
    /// </summary>
    public float  IconSizeScale    { get; set; } = 1.0f;

    /// <summary>Icon stroke multiplier clamped to the range the glyph rasterizer accepts.</summary>
    [JsonIgnore] public float StrokeScale => Math.Clamp(IconStrokeScale, 0.2f, 3f);

    /// <summary>Icon size multiplier clamped so icons stay inside their slice.</summary>
    [JsonIgnore] public float SizeScale => Math.Clamp(IconSizeScale, 0.5f, 2f);
    public string LabelColor       { get; set; } = "#AAFFFFFF";
    public string LabelColorHover  { get; set; } = "#FFFFFFFF";
    public float  LabelFontSize    { get; set; } = 11f;
    public string LabelFontFamily  { get; set; } = "Segoe UI Variable";

    // ── Breadcrumb ────────────────────────────────────────────────────────
    public string BreadcrumbFill   { get; set; } = "#BB111122";
    public string BreadcrumbText   { get; set; } = "#AAFFFFFF";

    // ── Volume ring ───────────────────────────────────────────────────────
    /// <summary>Stroke width of the volume level arc drawn just outside the ring. Default 3.0f.</summary>
    public float VolumeRingThickness { get; set; } = 3.0f;

    // ── Ring border ───────────────────────────────────────────────────────
    /// <summary>
    /// Color for the explicit circular border lines drawn around child (L2/L3) rings.
    /// Leave empty to fall back to SliceStroke.
    /// </summary>
    public string RingBorderColor { get; set; } = "";

    // ── Accent (submenu indicator arrow, active dot, etc.) ───────────────
    public string AccentColor      { get; set; } = "#FF7C6EF7";

    // ── Helpers ───────────────────────────────────────────────────────────

    // The renderer resolves ~150 colors per frame; parsing allocated two strings each time.
    // Theme colors are a small fixed set, so a process-wide cache keyed by the hex string
    // turns every call after the first into a dictionary lookup.
    private static readonly Dictionary<string, SKColor> s_colorCache = new();
    private static readonly object s_colorLock = new();

    public SKColor ToSKColor(string hex)
    {
        lock (s_colorLock)
        {
            if (s_colorCache.TryGetValue(hex, out var cached)) return cached;
            var parsed = ParseColor(hex);
            if (s_colorCache.Count > 512) s_colorCache.Clear(); // theme editor can churn values
            s_colorCache[hex] = parsed;
            return parsed;
        }
    }

    private static SKColor ParseColor(string hex)
    {
        var span = hex.AsSpan().TrimStart('#');
        uint argb;
        bool ok = span.Length switch
        {
            6 => uint.TryParse(span, System.Globalization.NumberStyles.HexNumber, null, out argb) && (argb |= 0xFF000000u) != 0,
            8 => uint.TryParse(span, System.Globalization.NumberStyles.HexNumber, null, out argb),
            _ => Fail(out argb),
        };
        if (!ok) return SKColors.White;
        return new SKColor(
            red:   (byte)((argb >> 16) & 0xFF),
            green: (byte)((argb >>  8) & 0xFF),
            blue:  (byte)( argb        & 0xFF),
            alpha: (byte)((argb >> 24) & 0xFF));

        static bool Fail(out uint v) { v = 0; return false; }
    }

    public SKPaint MakePaint(string hexColor, SKPaintStyle style = SKPaintStyle.Fill)
        => new() { Color = ToSKColor(hexColor), Style = style, IsAntialias = true };
}
