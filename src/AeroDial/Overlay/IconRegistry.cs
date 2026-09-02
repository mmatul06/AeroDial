// AeroDial — IconRegistry.cs
// Loads and caches icons for menu items. Supports:
//   1. Glyphs from the Windows system icon font: "fluent:<name>" / "fluent:<hex>"
//      (Segoe Fluent Icons, falling back to Segoe MDL2 Assets). Legacy names from the
//      old hand-drawn set ("play", "vol_up", ...) are aliased to glyphs.
//   2. Absolute file paths (PNG/JPEG/BMP/ICO)
//   3. Absolute paths to .exe/.dll — shell icon is extracted automatically
//   4. Relative paths inside Assets\Icons (name + ".png")
//
// Glyph icons are rasterized white and tinted by the renderer at draw time; file and
// exe icons keep their own colors. All bitmaps are cached by key.

using System.Runtime.InteropServices;
using SkiaSharp;
using AeroDial.Core;

namespace AeroDial.Overlay;

internal static class IconRegistry
{
    private const int GlyphBitmapSize = 128;

    private static readonly Dictionary<string, SKBitmap?> _cache = new();
    private static readonly object _lock = new();

    // Bitmaps removed from the cache are not disposed immediately: another thread (the
    // overlay render thread, or the settings ring preview on the UI thread) may be in the
    // middle of DrawBitmap with one. They are parked here and freed by DrainRetired() once
    // a grace period has passed — no draw call holds a bitmap anywhere near that long.
    private static readonly List<(SKBitmap Bitmap, long RetiredAt)> _retired = new();
    private const long RetireGraceMs = 2000;

    // Max number of file-based (non-glyph) entries before eviction runs
    private const int MaxFileCacheEntries = 128;

    // ── Icon font ─────────────────────────────────────────────────────────

    private static SKTypeface? _glyphTypeface;
    private static string?     _glyphFamily;
    private static bool        _glyphFontResolved;

    /// <summary>Family name of the icon font actually present on this machine
    /// ("Segoe Fluent Icons" on Windows 11, "Segoe MDL2 Assets" on Windows 10).</summary>
    public static string GlyphFontFamily
    {
        get { EnsureGlyphFont(); return _glyphFamily ?? FluentGlyphs.FontFamilies[0]; }
    }

    private static void EnsureGlyphFont()
    {
        if (_glyphFontResolved) return;
        lock (_lock)
        {
            if (_glyphFontResolved) return;
            foreach (var family in FluentGlyphs.FontFamilies)
            {
                var tf = SKFontManager.Default.MatchFamily(family);
                if (tf is not null) { _glyphTypeface = tf; _glyphFamily = family; break; }
            }
            if (_glyphTypeface is null)
                Logger.Warn("IconRegistry: no Segoe icon font found; glyph icons will use the fallback shape.");
            _glyphFontResolved = true;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>True if the key names a white glyph icon (tintable), false for full-color
    /// exe/image icons which should render in their natural colors.</summary>
    public static bool IsBuiltIn(string? key) => FluentGlyphs.IsGlyphKey(key);

    /// <param name="strokeScale">Glyph weight multiplier (theme IconStrokeScale); ignored for image icons.</param>
    public static SKBitmap? Get(string key, float strokeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        string canonical = FluentGlyphs.Canonicalize(key.Trim());
        bool   isGlyph   = FluentGlyphs.IsGlyphKey(canonical);
        string cacheKey  = isGlyph && strokeScale != 1f ? $"{canonical}@{strokeScale:F2}" : canonical;

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
        }

        // Load outside the lock: exe icon extraction is shell I/O and must not stall a
        // render thread that only wants an already-cached bitmap.
        var bmp = Load(canonical, strokeScale);

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var raced))
            {
                // Another thread loaded it first — keep theirs, retire ours.
                if (bmp is not null && !ReferenceEquals(bmp, raced)) Retire(bmp);
                return raced;
            }
            if (_cache.Count >= MaxFileCacheEntries) EvictFileEntries();
            _cache[cacheKey] = bmp;
            return bmp;
        }
    }

    /// <summary>Loads (and caches) every icon in <paramref name="keys"/>. Call from a
    /// background thread before a menu is shown so the render thread never pays for
    /// shell icon extraction inside a frame.</summary>
    public static void Prefetch(IEnumerable<string?> keys, float strokeScale = 1f)
    {
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            try { Get(key, strokeScale); }
            catch (Exception ex) { Logger.Debug($"IconRegistry.Prefetch: '{key}' failed — {ex.Message}"); }
        }
    }

    /// <summary>Removes all absolute-path (exe/file) cache entries, keeping glyph icons.
    /// Must be called under _lock.</summary>
    private static void EvictFileEntries()
    {
        var toRemove = _cache.Keys.Where(Path.IsPathRooted).ToList();
        foreach (var k in toRemove)
        {
            if (_cache[k] is { } bmp) Retire(bmp);
            _cache.Remove(k);
        }
        Logger.Debug($"IconRegistry: evicted {toRemove.Count} file-based cache entries.");
    }

    public static void Invalidate(string key)
    {
        lock (_lock)
        {
            if (_cache.Remove(FluentGlyphs.Canonicalize(key.Trim()), out var bmp) && bmp is not null) Retire(bmp);
        }
    }

    // Must be called under _lock.
    private static void Retire(SKBitmap bmp)
        => _retired.Add((bmp, Environment.TickCount64));

    /// <summary>Disposes retired bitmaps whose grace period has expired. Cheap; call once per
    /// frame from any drawing loop.</summary>
    public static void DrainRetired()
    {
        List<SKBitmap>? due = null;
        lock (_lock)
        {
            if (_retired.Count == 0) return;
            long now = Environment.TickCount64;
            for (int i = _retired.Count - 1; i >= 0; i--)
            {
                if (now - _retired[i].RetiredAt < RetireGraceMs) continue;
                (due ??= new()).Add(_retired[i].Bitmap);
                _retired.RemoveAt(i);
            }
        }
        if (due is null) return;
        foreach (var bmp in due) bmp.Dispose();
    }

    // ── Loader ────────────────────────────────────────────────────────────

    private static SKBitmap? Load(string key, float strokeScale)
    {
        // 1. System icon-font glyph?
        if (FluentGlyphs.TryResolve(key, out int codepoint))
            return DrawGlyph(codepoint, GlyphBitmapSize, strokeScale);

        // 2. Absolute file path?
        if (Path.IsPathRooted(key) && File.Exists(key))
        {
            var ext = Path.GetExtension(key).ToLowerInvariant();
            return ext is ".exe" or ".dll" ? LoadFromExe(key) : LoadFromFile(key);
        }

        // 3. Relative path inside Assets\Icons?
        var candidate = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons", key + ".png");
        if (File.Exists(candidate)) return LoadFromFile(candidate);

        Logger.Warn($"IconRegistry: could not resolve icon '{key}'");
        return DrawFallback(GlyphBitmapSize);
    }

    /// <summary>Rasterizes one icon-font glyph, white on transparent, centered in a square.
    /// Fluent glyphs are drawn as thin outlines by design; a stroke-and-fill pass thickens
    /// them so they read on the ring at 22 px the way the old hand-drawn icons did.
    /// The theme's IconStrokeScale scales that weight (1.0 = default).</summary>
    private static SKBitmap DrawGlyph(int codepoint, int size, float strokeScale)
    {
        EnsureGlyphFont();
        if (_glyphTypeface is null) return DrawFallback(size);

        var bmp    = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);

        float weight = size * 0.024f * Math.Clamp(strokeScale, 0.2f, 3f);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color       = SKColors.White,
            Typeface    = _glyphTypeface,
            TextSize    = size * 0.70f, // glyphs sit inside the em box with margins already
            TextAlign   = SKTextAlign.Center,
            Style       = SKPaintStyle.StrokeAndFill,
            StrokeWidth = weight,
            StrokeJoin  = SKStrokeJoin.Round,
            StrokeCap   = SKStrokeCap.Round,
        };
        string text = char.ConvertFromUtf32(codepoint);
        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        // Center the glyph's ink bounds in the bitmap.
        float x = size / 2f - bounds.MidX;
        float y = size / 2f - bounds.MidY;
        canvas.DrawText(text, x, y, paint);
        canvas.Flush();
        return bmp;
    }

    private static SKBitmap DrawFallback(int size)
    {
        var bmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);
        using var p = new SKPaint { IsAntialias = true, Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = size * 0.06f };
        canvas.DrawCircle(size / 2f, size / 2f, size * 0.32f, p);
        canvas.DrawCircle(size / 2f, size / 2f, size * 0.08f, new SKPaint { IsAntialias = true, Color = SKColors.White });
        canvas.Flush();
        return bmp;
    }

    private static SKBitmap? LoadFromFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return SKBitmap.Decode(stream);
        }
        catch (Exception ex)
        {
            Logger.Warn($"IconRegistry: failed to load '{path}'", ex);
            return null;
        }
    }

    private static SKBitmap? LoadFromExe(string path)
    {
        try
        {
            nint[] large = new nint[1];
            int n = Win32.ExtractIconEx(path, 0, large, null, 1);
            if (n <= 0 || large[0] == 0) return DrawFallback(GlyphBitmapSize);

            nint hIcon = large[0];
            try
            {
                const int size = 128;
                nint hDC  = Win32.CreateCompatibleDC(0);
                var bmi = new Win32.BITMAPINFO
                {
                    bmiHeader = new Win32.BITMAPINFOHEADER
                    {
                        biSize     = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                        biWidth    = size,
                        biHeight   = -size, // top-down
                        biPlanes   = 1,
                        biBitCount = 32,
                        biCompression = Win32.BI_RGB,
                    }
                };
                nint hBmp = Win32.CreateDIBSection(hDC, ref bmi, Win32.DIB_RGB_COLORS, out nint pBits, 0, 0);
                nint hOld = Win32.SelectObject(hDC, hBmp);

                Win32.DrawIconEx(hDC, 0, 0, hIcon, size, size, 0, 0, Win32.DI_NORMAL);

                // Copy pixels into an SKBitmap
                var skBmp = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
                var dst   = new byte[size * size * 4];
                Marshal.Copy(pBits, dst, 0, dst.Length);
                var handle = GCHandle.Alloc(dst, GCHandleType.Pinned);
                try
                {
                    var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
                    skBmp.InstallPixels(info, handle.AddrOfPinnedObject(), size * 4);
                    // InstallPixels does not copy — we must copy out before unpinning
                    var finalBmp = skBmp.Copy();
                    skBmp.Dispose();
                    return finalBmp;
                }
                finally
                {
                    handle.Free();
                    Win32.SelectObject(hDC, hOld);
                    Win32.DeleteObject(hBmp);
                    Win32.DeleteDC(hDC);
                }
            }
            finally
            {
                Win32.DestroyIcon(hIcon);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"IconRegistry: failed to extract exe icon from '{path}'", ex);
            return null;
        }
    }
}
