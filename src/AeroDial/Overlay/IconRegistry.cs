// AeroDial — IconRegistry.cs
// Loads and caches icons for menu items. Supports:
//   1. Absolute file paths (PNG/JPEG/BMP/ICO)
//   2. Absolute paths to .exe/.dll — shell icon is extracted automatically
//   3. Built-in named icons (drawn programmatically via SkiaSharp — no file needed)
//   4. Relative paths inside Assets\Icons (name + ".png")
//
// All bitmaps are cached by key so each icon is decoded exactly once.

using System.Runtime.InteropServices;
using SkiaSharp;
using AeroDial.Core;

namespace AeroDial.Overlay;

internal static class IconRegistry
{
    private static readonly Dictionary<string, SKBitmap?> _cache = new();
    private static readonly object _lock = new();

    // Bitmaps removed from the cache are not disposed immediately: another thread (the
    // overlay render thread, or the settings ring preview on the UI thread) may be in the
    // middle of DrawBitmap with one. They are parked here and freed by DrainRetired() once
    // a grace period has passed — no draw call holds a bitmap anywhere near that long.
    private static readonly List<(SKBitmap Bitmap, long RetiredAt)> _retired = new();
    private const long RetireGraceMs = 2000;

    // Applied by W() when drawing built-in icons; set/reset inside DrawBuiltIn under _lock.
    private static float _strokeScale = 1f;

    // ── Public API ────────────────────────────────────────────────────────

    // Max number of file-based (non-built-in) entries before eviction runs
    private const int MaxFileCacheEntries = 128;

    public static SKBitmap? Get(string key, float strokeScale = 1f)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        // Built-in icons are keyed with a scale suffix so different scales are cached separately.
        // File-based icons (exe/png) are not stroke-drawn, so scale doesn't apply to them.
        bool isBuiltIn = !Path.IsPathRooted(key);
        string cacheKey = isBuiltIn && strokeScale != 1f
            ? $"{key}@{strokeScale:F2}" : key;

        lock (_lock)
        {
            if (_cache.TryGetValue(cacheKey, out var cached)) return cached;
        }

        // Load outside the lock: exe icon extraction is shell I/O and must not stall a
        // render thread that only wants an already-cached bitmap.
        var bmp = isBuiltIn ? LoadBuiltInLocked(key, strokeScale) : Load(key, strokeScale);

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

    // Built-in drawing uses the shared _strokeScale field, so it must run under the lock.
    private static SKBitmap? LoadBuiltInLocked(string key, float strokeScale)
    {
        lock (_lock) return Load(key, strokeScale);
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

    /// <summary>Removes all absolute-path (exe/file) cache entries, keeping built-in named icons.
    /// Must be called under _lock.</summary>
    private static void EvictFileEntries()
    {
        var toRemove = _cache.Keys.Where(k => Path.IsPathRooted(k)).ToList();
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
            if (_cache.Remove(key, out var bmp) && bmp is not null) Retire(bmp);
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

    private static SKBitmap? Load(string key, float strokeScale = 1f)
    {
        // 1. Built-in programmatic icon?
        var builtin = DrawBuiltIn(key, 128, strokeScale);
        if (builtin is not null) return builtin;

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
        return DrawFallback(128);
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
            if (n <= 0 || large[0] == 0) return DrawFallback(128);

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
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(dst, GCHandleType.Pinned);
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

    // ── Built-in programmatic icons ───────────────────────────────────────
    // All drawn white at size×size; renderer applies tint ColorFilter at draw time.

    // The full set of built-in vector icon names (drawn white, then tinted at render time).
    // Keep in sync with the DrawBuiltIn switch below. Used by IsBuiltIn so the renderer knows
    // which icons are safe to recolor vs. which are full-color exe/image icons to leave as-is.
    private static readonly HashSet<string> s_builtInNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "media", "apps", "vol_up", "vol_down", "mute", "play", "settings", "desktop",
        "next", "prev", "url", "script", "clipboard", "default",
        "power", "lock", "folder", "copy", "paste", "home", "search", "mic",
        "close", "camera", "keyboard", "refresh", "send", "star",
        "pause", "stop", "back", "forward", "minimize", "zoom_in", "zoom_out",
        "trash", "edit", "download", "upload", "check", "plus", "minus",
        "tag", "share", "list", "info", "wifi", "bluetooth", "brightness",
        "clock", "alarm", "calendar", "sleep", "screenshot",
    };

    /// <summary>True if the key names a built-in white vector icon (tintable), false for
    /// full-color exe/image icons which should render in their natural colors.</summary>
    public static bool IsBuiltIn(string? key) => key is not null && s_builtInNames.Contains(key);

    private static SKBitmap? DrawBuiltIn(string key, int size, float strokeScale = 1f)
    {
        Action<SKCanvas, float>? draw = key.ToLowerInvariant() switch
        {
            // ── Original set ──────────────────────────────────────────────
            "media"       => DrawMedia,
            "apps"        => DrawApps,
            "vol_up"      => DrawVolUp,
            "vol_down"    => DrawVolDown,
            "mute"        => DrawMute,
            "play"        => DrawPlay,
            "settings"    => DrawSettings,
            "desktop"     => DrawDesktop,
            "next"        => DrawNext,
            "prev"        => DrawPrev,
            "url"         => DrawUrl,
            "script"      => DrawScript,
            "clipboard"   => DrawClipboard,
            "default"     => DrawDefault,
            // ── Extended set (session 2) ───────────────────────────────────
            "power"       => DrawPower,
            "lock"        => DrawLock,
            "folder"      => DrawFolder,
            "copy"        => DrawCopy,
            "paste"       => DrawPaste,
            "home"        => DrawHome,
            "search"      => DrawSearch,
            "mic"         => DrawMic,
            "close"       => DrawClose,
            "camera"      => DrawCamera,
            "keyboard"    => DrawKeyboard,
            "refresh"     => DrawRefresh,
            "send"        => DrawSend,
            "star"        => DrawStar,
            // ── Extended set (session 3) ───────────────────────────────────
            "pause"       => DrawPause,
            "stop"        => DrawStop,
            "back"        => DrawBack,
            "forward"     => DrawForward,
            "minimize"    => DrawMinimize,
            "zoom_in"     => DrawZoomIn,
            "zoom_out"    => DrawZoomOut,
            "trash"       => DrawTrash,
            "edit"        => DrawEdit,
            "download"    => DrawDownload,
            "upload"      => DrawUpload,
            "check"       => DrawCheck,
            "plus"        => DrawPlus,
            "minus"       => DrawMinus,
            "tag"         => DrawTag,
            "share"       => DrawShare,
            "list"        => DrawList,
            "info"        => DrawInfo,
            "wifi"        => DrawWifi,
            "bluetooth"   => DrawBluetooth,
            "brightness"  => DrawBrightness,
            "clock"       => DrawClock,
            "alarm"       => DrawAlarm,
            "calendar"    => DrawCalendar,
            "sleep"       => DrawSleep,
            "screenshot"  => DrawScreenshot,
            _             => null,
        };

        if (draw is null) return null;
        _strokeScale = Math.Max(0.1f, strokeScale); // set before drawing (protected by _lock)
        var bmp    = new SKBitmap(size, size);
        var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);
        draw(canvas, size);
        canvas.Flush();
        _strokeScale = 1f; // reset after drawing
        return bmp;
    }

    // ── Paint factory ─────────────────────────────────────────────────────

    private static SKPaint W(float strokeW = 0) => new()
    {
        Color       = SKColors.White,
        IsAntialias = true,
        Style       = strokeW > 0 ? SKPaintStyle.Stroke : SKPaintStyle.Fill,
        StrokeWidth = strokeW > 0 ? strokeW * _strokeScale : 0f,
        StrokeCap   = SKStrokeCap.Round,
        StrokeJoin  = SKStrokeJoin.Round,
    };

    // ── Original icon draw routines ───────────────────────────────────────

    private static void DrawMedia(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawLine(s*.35f, s*.25f, s*.35f, s*.65f, p);
        c.DrawLine(s*.35f, s*.25f, s*.7f,  s*.15f, p);
        c.DrawLine(s*.7f,  s*.15f, s*.7f,  s*.55f, p);
        using var f = W();
        c.DrawCircle(s*.28f, s*.67f, s*.1f, f);
        c.DrawCircle(s*.63f, s*.57f, s*.1f, f);
    }

    private static void DrawApps(SKCanvas c, float s)
    {
        using var f = W();
        float r = s*.08f, g = s*.12f, off = s*.2f;
        foreach (float row in new[]{ off, off+r*2+g, off+r*4+g*2 })
            foreach (float col in new[]{ off, off+r*2+g, off+r*4+g*2 })
                c.DrawRoundRect(col, row, r*2, r*2, r*.4f, r*.4f, f);
    }

    private static void DrawVolUp(SKCanvas c, float s)
    {
        using var f = W(); using var p = W(2f);
        c.DrawPath(SpeakerCone(s), f);
        c.DrawArc(new SKRect(s*.52f,s*.22f,s*.78f,s*.78f), -60, 120, false, p);
        c.DrawArc(new SKRect(s*.58f,s*.3f, s*.82f,s*.7f),  -50, 100, false, p);
        c.DrawLine(s*.75f, s*.1f, s*.75f, s*.22f, p);
        c.DrawLine(s*.69f, s*.16f, s*.81f, s*.16f, p);
    }

    private static void DrawVolDown(SKCanvas c, float s)
    {
        using var f = W(); using var p = W(2f);
        c.DrawPath(SpeakerCone(s), f);
        c.DrawArc(new SKRect(s*.52f,s*.22f,s*.78f,s*.78f), -60, 120, false, p);
        c.DrawLine(s*.69f, s*.16f, s*.81f, s*.16f, p);
    }

    private static SKPath SpeakerCone(float s)
    {
        var path = new SKPath();
        path.MoveTo(s*.18f, s*.38f); path.LineTo(s*.34f, s*.38f);
        path.LineTo(s*.5f,  s*.22f); path.LineTo(s*.5f,  s*.78f);
        path.LineTo(s*.34f, s*.62f); path.LineTo(s*.18f, s*.62f);
        path.Close();
        return path;
    }

    private static void DrawMute(SKCanvas c, float s)
    {
        using var f = W(); using var p = W(2f);
        c.DrawPath(SpeakerCone(s), f);
        c.DrawLine(s*.58f, s*.3f, s*.82f, s*.7f, p);
        c.DrawLine(s*.82f, s*.3f, s*.58f, s*.7f, p);
    }

    private static void DrawPlay(SKCanvas c, float s)
    {
        using var f = W();
        var path = new SKPath();
        path.MoveTo(s*.25f, s*.18f); path.LineTo(s*.78f, s*.5f); path.LineTo(s*.25f, s*.82f);
        path.Close(); c.DrawPath(path, f);
    }

    private static void DrawSettings(SKCanvas c, float s)
    {
        // Proper cogwheel: 8 flat-topped teeth, center hole, via EvenOdd fill
        using var f = new SKPaint { Color = SKColors.White, IsAntialias = true,
                                    Style = SKPaintStyle.Fill };
        f.PathEffect = null;
        float cx = s / 2, cy = s / 2;
        float outerR = s * .40f, innerR = s * .29f, holeR = s * .13f;
        const int teeth    = 8;
        float     halfTooth = MathF.PI / teeth * 0.5f;

        var path = new SKPath { FillType = SKPathFillType.EvenOdd };
        for (int i = 0; i < teeth; i++)
        {
            float mid = i * 2f * MathF.PI / teeth;
            float a0  = mid - halfTooth;
            float a1  = mid + halfTooth;

            if (i == 0)
                path.MoveTo(cx + MathF.Cos(a0) * innerR, cy + MathF.Sin(a0) * innerR);
            else
                path.LineTo(cx + MathF.Cos(a0) * innerR, cy + MathF.Sin(a0) * innerR);

            path.LineTo(cx + MathF.Cos(a0) * outerR, cy + MathF.Sin(a0) * outerR);
            path.LineTo(cx + MathF.Cos(a1) * outerR, cy + MathF.Sin(a1) * outerR);
            path.LineTo(cx + MathF.Cos(a1) * innerR, cy + MathF.Sin(a1) * innerR);

            float nextA0 = (i + 1) * 2f * MathF.PI / teeth - halfTooth;
            float sweepDeg = (nextA0 - a1) * 180f / MathF.PI;
            path.ArcTo(
                new SKRect(cx - innerR, cy - innerR, cx + innerR, cy + innerR),
                a1 * 180f / MathF.PI, sweepDeg, false);
        }
        path.Close();
        path.AddCircle(cx, cy, holeR); // EvenOdd cuts the center hole out
        c.DrawPath(path, f);
    }

    private static void DrawDesktop(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawRoundRect(s*.1f, s*.15f, s*.8f, s*.55f, 3, 3, p);
        using var f = W();
        c.DrawRect(s*.38f, s*.7f, s*.24f, s*.1f, f);
        c.DrawRect(s*.25f, s*.8f, s*.5f, s*.05f, f);
    }

    private static void DrawNext(SKCanvas c, float s)
    {
        using var f = W(); using var p = W(2.5f);
        var path = new SKPath();
        path.MoveTo(s*.18f, s*.2f); path.LineTo(s*.55f, s*.5f); path.LineTo(s*.18f, s*.8f);
        path.Close(); c.DrawPath(path, f);
        c.DrawLine(s*.62f, s*.2f, s*.62f, s*.8f, p);
    }

    private static void DrawPrev(SKCanvas c, float s)
    {
        using var f = W(); using var p = W(2.5f);
        var path = new SKPath();
        path.MoveTo(s*.82f, s*.2f); path.LineTo(s*.45f, s*.5f); path.LineTo(s*.82f, s*.8f);
        path.Close(); c.DrawPath(path, f);
        c.DrawLine(s*.38f, s*.2f, s*.38f, s*.8f, p);
    }

    private static void DrawUrl(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawOval(s*.5f, s*.5f, s*.3f, s*.3f, p);
        c.DrawLine(s*.2f, s*.5f, s*.8f, s*.5f, p);
        c.DrawArc(new SKRect(s*.3f,s*.2f,s*.7f,s*.8f), 0, 180, false, p);
        c.DrawArc(new SKRect(s*.3f,s*.2f,s*.7f,s*.8f), 180, 180, false, p);
    }

    private static void DrawScript(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawText(">_", s*.12f, s*.62f, new SKFont(SKTypeface.FromFamilyName("Consolas"), s*.38f), p);
    }

    private static void DrawClipboard(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        c.DrawRoundRect(s*.2f, s*.25f, s*.6f, s*.65f, 3, 3, p);
        c.DrawRect(s*.36f, s*.15f, s*.28f, s*.18f, f);
        c.DrawLine(s*.32f, s*.45f, s*.68f, s*.45f, p);
        c.DrawLine(s*.32f, s*.57f, s*.68f, s*.57f, p);
        c.DrawLine(s*.32f, s*.69f, s*.55f, s*.69f, p);
    }

    private static void DrawDefault(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawCircle(s/2, s/2, s*.32f, p);
        c.DrawLine(s/2, s*.18f, s/2, s*.5f, p);
        c.DrawCircle(s/2, s*.68f, s*.04f, new SKPaint { Color = SKColors.White, IsAntialias = true });
    }

    // ── Extended set (session 2) ──────────────────────────────────────────

    private static void DrawPower(SKCanvas c, float s)
    {
        using var p = W(2.5f);
        float cx = s/2, cy = s/2, r = s*.32f;
        c.DrawArc(new SKRect(cx-r, cy-r, cx+r, cy+r), -240, 300, false, p);
        c.DrawLine(cx, cy-r, cx, cy-s*.12f, p);
    }

    private static void DrawLock(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        c.DrawRoundRect(s*.22f, s*.48f, s*.56f, s*.38f, 4, 4, p);
        c.DrawArc(new SKRect(s*.3f, s*.18f, s*.7f, s*.52f), 180, 180, false, p);
        c.DrawCircle(s*.5f, s*.66f, s*.06f, f);
    }

    private static void DrawFolder(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawRoundRect(s*.12f, s*.35f, s*.76f, s*.46f, 3, 3, p);
        var tab = new SKPath();
        tab.MoveTo(s*.12f, s*.35f); tab.LineTo(s*.12f, s*.28f);
        tab.LineTo(s*.38f, s*.28f); tab.LineTo(s*.45f, s*.35f);
        c.DrawPath(tab, p);
    }

    private static void DrawCopy(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawRoundRect(s*.28f, s*.15f, s*.52f, s*.52f, 3, 3, p);
        c.DrawRoundRect(s*.18f, s*.28f, s*.52f, s*.52f, 3, 3, p);
    }

    private static void DrawPaste(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        c.DrawRoundRect(s*.22f, s*.28f, s*.56f, s*.58f, 3, 3, p);
        c.DrawRect(s*.36f, s*.18f, s*.28f, s*.16f, f);
        c.DrawLine(s*.32f, s*.46f, s*.66f, s*.46f, p);
        c.DrawLine(s*.32f, s*.57f, s*.66f, s*.57f, p);
    }

    private static void DrawHome(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        var roof = new SKPath();
        roof.MoveTo(s*.5f,  s*.18f); roof.LineTo(s*.82f, s*.52f); roof.LineTo(s*.18f, s*.52f);
        roof.Close(); c.DrawPath(roof, p);
        c.DrawRoundRect(s*.32f, s*.52f, s*.36f, s*.32f, 2, 2, p);
        c.DrawRect(s*.42f, s*.66f, s*.16f, s*.18f, f);
    }

    private static void DrawSearch(SKCanvas c, float s)
    {
        using var p = W(2.5f);
        c.DrawCircle(s*.42f, s*.42f, s*.22f, p);
        c.DrawLine(s*.58f, s*.58f, s*.8f, s*.8f, p);
    }

    private static void DrawMic(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawRoundRect(s*.38f, s*.14f, s*.24f, s*.38f, s*.12f, s*.12f, p);
        c.DrawArc(new SKRect(s*.28f, s*.38f, s*.72f, s*.72f), 0, 180, false, p);
        c.DrawLine(s*.5f, s*.72f, s*.5f, s*.84f, p);
        c.DrawLine(s*.35f, s*.84f, s*.65f, s*.84f, p);
    }

    private static void DrawClose(SKCanvas c, float s)
    {
        using var p = W(2.5f);
        c.DrawLine(s*.22f, s*.22f, s*.78f, s*.78f, p);
        c.DrawLine(s*.78f, s*.22f, s*.22f, s*.78f, p);
    }

    private static void DrawCamera(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawRoundRect(s*.12f, s*.3f, s*.76f, s*.5f, 4, 4, p);
        var bump = new SKPath();
        bump.MoveTo(s*.34f, s*.3f); bump.LineTo(s*.38f, s*.2f);
        bump.LineTo(s*.62f, s*.2f); bump.LineTo(s*.66f, s*.3f);
        c.DrawPath(bump, p);
        c.DrawCircle(s*.5f, s*.55f, s*.13f, p);
    }

    private static void DrawKeyboard(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        c.DrawRoundRect(s*.12f, s*.3f, s*.76f, s*.4f, 3, 3, p);
        for (int i = 0; i < 5; i++) c.DrawRect(s*.2f + i*s*.12f, s*.38f, s*.08f, s*.06f, f);
        c.DrawRect(s*.3f, s*.5f, s*.4f, s*.06f, f);
    }

    private static void DrawRefresh(SKCanvas c, float s)
    {
        using var p = W(2.5f);
        c.DrawArc(new SKRect(s*.2f, s*.2f, s*.8f, s*.8f), -60, 270, false, p);
        var arr = new SKPath();
        arr.MoveTo(s*.72f, s*.16f); arr.LineTo(s*.82f, s*.28f); arr.LineTo(s*.6f, s*.3f);
        arr.Close(); using var f = W(); c.DrawPath(arr, f);
    }

    private static void DrawSend(SKCanvas c, float s)
    {
        using var f = W();
        var path = new SKPath();
        path.MoveTo(s*.15f, s*.22f); path.LineTo(s*.85f, s*.5f);
        path.LineTo(s*.15f, s*.78f); path.LineTo(s*.28f, s*.5f); path.Close();
        c.DrawPath(path, f);
    }

    private static void DrawStar(SKCanvas c, float s)
    {
        using var f = W();
        float cx = s/2, cy = s/2, outerR = s*.38f, innerR = s*.16f;
        var path = new SKPath();
        for (int i = 0; i < 10; i++)
        {
            float angle = (i*36f - 90f) * MathF.PI / 180f;
            float r     = i%2 == 0 ? outerR : innerR;
            float x = cx + MathF.Cos(angle)*r, y = cy + MathF.Sin(angle)*r;
            if (i == 0) path.MoveTo(x, y); else path.LineTo(x, y);
        }
        path.Close(); c.DrawPath(path, f);
    }

    // ── Extended set (session 3) ──────────────────────────────────────────

    private static void DrawPause(SKCanvas c, float s)
    {
        using var f = W();
        c.DrawRect(s*.25f, s*.2f, s*.16f, s*.6f, f);
        c.DrawRect(s*.58f, s*.2f, s*.16f, s*.6f, f);
    }

    private static void DrawStop(SKCanvas c, float s)
    {
        using var f = W();
        c.DrawRoundRect(s*.22f, s*.22f, s*.56f, s*.56f, 4, 4, f);
    }

    private static void DrawBack(SKCanvas c, float s)
    {
        using var f = W(); using var p = W(2.5f);
        var t = new SKPath();
        t.MoveTo(s*.52f, s*.2f); t.LineTo(s*.18f, s*.5f); t.LineTo(s*.52f, s*.8f);
        t.Close(); c.DrawPath(t, f);
        c.DrawLine(s*.38f, s*.2f, s*.38f, s*.8f, p);
    }

    private static void DrawForward(SKCanvas c, float s)
    {
        using var f = W(); using var p = W(2.5f);
        var t = new SKPath();
        t.MoveTo(s*.48f, s*.2f); t.LineTo(s*.82f, s*.5f); t.LineTo(s*.48f, s*.8f);
        t.Close(); c.DrawPath(t, f);
        c.DrawLine(s*.62f, s*.2f, s*.62f, s*.8f, p);
    }

    private static void DrawMinimize(SKCanvas c, float s)
    {
        using var p = W(3f);
        c.DrawLine(s*.18f, s*.72f, s*.82f, s*.72f, p);
    }

    private static void DrawZoomIn(SKCanvas c, float s)
    {
        using var p = W(2.5f);
        c.DrawCircle(s*.42f, s*.42f, s*.22f, p);
        c.DrawLine(s*.34f, s*.42f, s*.5f, s*.42f, p);
        c.DrawLine(s*.42f, s*.34f, s*.42f, s*.5f, p);
        c.DrawLine(s*.58f, s*.58f, s*.8f, s*.8f, p);
    }

    private static void DrawZoomOut(SKCanvas c, float s)
    {
        using var p = W(2.5f);
        c.DrawCircle(s*.42f, s*.42f, s*.22f, p);
        c.DrawLine(s*.34f, s*.42f, s*.5f, s*.42f, p);
        c.DrawLine(s*.58f, s*.58f, s*.8f, s*.8f, p);
    }

    private static void DrawTrash(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        // Bin body
        c.DrawRoundRect(s*.2f, s*.32f, s*.6f, s*.54f, 2, 2, p);
        // Lid
        c.DrawLine(s*.14f, s*.32f, s*.86f, s*.32f, p);
        c.DrawLine(s*.36f, s*.32f, s*.4f,  s*.22f, p);
        c.DrawLine(s*.64f, s*.32f, s*.6f,  s*.22f, p);
        c.DrawLine(s*.4f,  s*.22f, s*.6f,  s*.22f, p);
        // Lines inside bin
        c.DrawLine(s*.38f, s*.42f, s*.38f, s*.76f, p);
        c.DrawLine(s*.5f,  s*.42f, s*.5f,  s*.76f, p);
        c.DrawLine(s*.62f, s*.42f, s*.62f, s*.76f, p);
    }

    private static void DrawEdit(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        // Pencil body diagonal
        var body = new SKPath();
        body.MoveTo(s*.62f, s*.18f); body.LineTo(s*.82f, s*.38f);
        body.LineTo(s*.28f, s*.82f); body.LineTo(s*.18f, s*.82f);
        body.LineTo(s*.18f, s*.72f); body.Close();
        c.DrawPath(body, p);
        // Tip
        c.DrawLine(s*.18f, s*.72f, s*.28f, s*.82f, p);
        // Eraser cap
        using var cap = W(3f);
        cap.Color = SKColors.White;
        c.DrawLine(s*.66f, s*.14f, s*.86f, s*.34f, cap);
    }

    private static void DrawDownload(SKCanvas c, float s)
    {
        using var p = W(2.5f); using var f = W();
        c.DrawLine(s*.5f, s*.18f, s*.5f, s*.65f, p);
        var arr = new SKPath();
        arr.MoveTo(s*.28f, s*.48f); arr.LineTo(s*.5f, s*.68f); arr.LineTo(s*.72f, s*.48f);
        c.DrawPath(arr, p);
        c.DrawLine(s*.18f, s*.82f, s*.82f, s*.82f, p);
    }

    private static void DrawUpload(SKCanvas c, float s)
    {
        using var p = W(2.5f);
        c.DrawLine(s*.5f, s*.65f, s*.5f, s*.18f, p);
        var arr = new SKPath();
        arr.MoveTo(s*.28f, s*.36f); arr.LineTo(s*.5f, s*.16f); arr.LineTo(s*.72f, s*.36f);
        c.DrawPath(arr, p);
        c.DrawLine(s*.18f, s*.82f, s*.82f, s*.82f, p);
    }

    private static void DrawCheck(SKCanvas c, float s)
    {
        using var p = W(3f);
        c.DrawLine(s*.16f, s*.52f, s*.38f, s*.74f, p);
        c.DrawLine(s*.38f, s*.74f, s*.82f, s*.26f, p);
    }

    private static void DrawPlus(SKCanvas c, float s)
    {
        using var p = W(3f);
        c.DrawLine(s*.5f,  s*.18f, s*.5f,  s*.82f, p);
        c.DrawLine(s*.18f, s*.5f,  s*.82f, s*.5f,  p);
    }

    private static void DrawMinus(SKCanvas c, float s)
    {
        using var p = W(3f);
        c.DrawLine(s*.18f, s*.5f, s*.82f, s*.5f, p);
    }

    private static void DrawTag(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        var path = new SKPath();
        path.MoveTo(s*.18f, s*.18f); path.LineTo(s*.52f, s*.18f);
        path.LineTo(s*.82f, s*.5f);  path.LineTo(s*.52f, s*.82f);
        path.LineTo(s*.18f, s*.82f); path.Close();
        c.DrawPath(path, p);
        c.DrawCircle(s*.34f, s*.34f, s*.06f, f);
    }

    private static void DrawShare(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        c.DrawCircle(s*.74f, s*.28f, s*.09f, p);
        c.DrawCircle(s*.74f, s*.72f, s*.09f, p);
        c.DrawCircle(s*.26f, s*.5f,  s*.09f, p);
        c.DrawLine(s*.35f, s*.46f, s*.65f, s*.32f, p);
        c.DrawLine(s*.35f, s*.54f, s*.65f, s*.68f, p);
    }

    private static void DrawList(SKCanvas c, float s)
    {
        using var p = W(2.5f); using var f = W();
        float[] ys = { s*.3f, s*.5f, s*.7f };
        foreach (var y in ys)
        {
            c.DrawCircle(s*.22f, y, s*.04f, f);
            c.DrawLine(s*.34f, y, s*.8f, y, p);
        }
    }

    private static void DrawInfo(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        c.DrawCircle(s*.5f, s*.5f, s*.34f, p);
        c.DrawCircle(s*.5f, s*.33f, s*.04f, f);
        c.DrawLine(s*.5f, s*.44f, s*.5f, s*.68f, p);
    }

    private static void DrawWifi(SKCanvas c, float s)
    {
        using var p = W(2.5f); using var f = W();
        c.DrawArc(new SKRect(s*.12f, s*.14f, s*.88f, s*.9f),  -150, 120, false, p);
        c.DrawArc(new SKRect(s*.24f, s*.28f, s*.76f, s*.8f),  -145, 110, false, p);
        c.DrawArc(new SKRect(s*.36f, s*.42f, s*.64f, s*.7f),  -140, 100, false, p);
        c.DrawCircle(s*.5f, s*.72f, s*.05f, f);
    }

    private static void DrawBluetooth(SKCanvas c, float s)
    {
        using var p = W(2f);
        float cx = s/2;
        // Vertical stem
        c.DrawLine(cx, s*.18f, cx, s*.82f, p);
        // Right top
        c.DrawLine(cx, s*.18f, cx+s*.2f, s*.38f, p);
        c.DrawLine(cx+s*.2f, s*.38f, cx-s*.2f, s*.62f, p);
        // Right bottom
        c.DrawLine(cx, s*.82f, cx+s*.2f, s*.62f, p);
        c.DrawLine(cx+s*.2f, s*.62f, cx-s*.2f, s*.38f, p);
    }

    private static void DrawBrightness(SKCanvas c, float s)
    {
        // Sun icon (circle + 8 short rays)
        using var p = W(2f);
        float cx = s/2, cy = s/2;
        c.DrawCircle(cx, cy, s*.18f, p);
        for (int i = 0; i < 8; i++)
        {
            float a  = i * 45f * MathF.PI / 180f;
            float x1 = cx + MathF.Cos(a) * s*.28f, y1 = cy + MathF.Sin(a) * s*.28f;
            float x2 = cx + MathF.Cos(a) * s*.40f, y2 = cy + MathF.Sin(a) * s*.40f;
            c.DrawLine(x1, y1, x2, y2, p);
        }
    }

    private static void DrawClock(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawCircle(s*.5f, s*.5f, s*.34f, p);
        c.DrawLine(s*.5f, s*.5f, s*.5f, s*.26f, p);
        c.DrawLine(s*.5f, s*.5f, s*.66f, s*.58f, p);
    }

    private static void DrawAlarm(SKCanvas c, float s)
    {
        using var p = W(2f);
        c.DrawCircle(s*.5f, s*.52f, s*.3f, p);
        c.DrawLine(s*.5f, s*.52f, s*.5f, s*.3f, p);
        c.DrawLine(s*.5f, s*.52f, s*.64f, s*.6f, p);
        // Bell bumps
        c.DrawLine(s*.22f, s*.36f, s*.32f, s*.26f, p);
        c.DrawLine(s*.78f, s*.36f, s*.68f, s*.26f, p);
    }

    private static void DrawCalendar(SKCanvas c, float s)
    {
        using var p = W(2f); using var f = W();
        c.DrawRoundRect(s*.14f, s*.24f, s*.72f, s*.62f, 3, 3, p);
        c.DrawLine(s*.14f, s*.38f, s*.86f, s*.38f, p);
        c.DrawLine(s*.36f, s*.18f, s*.36f, s*.32f, p);
        c.DrawLine(s*.64f, s*.18f, s*.64f, s*.32f, p);
        float[] gx = { s*.28f, s*.46f, s*.64f };
        float[] gy = { s*.5f, s*.64f, s*.78f };
        foreach (var gxv in gx) foreach (var gyv in gy) c.DrawCircle(gxv, gyv, s*.03f, f);
    }

    private static void DrawSleep(SKCanvas c, float s)
    {
        // Crescent moon
        using var f = W();
        var path = new SKPath();
        // Outer arc of crescent
        path.ArcTo(new SKRect(s*.16f, s*.14f, s*.82f, s*.86f), -120, -210, true);
        // Inner arc (cutout circle offset to the right)
        path.ArcTo(new SKRect(s*.28f, s*.2f, s*.78f, s*.78f), 90, 210, false);
        path.Close();
        c.DrawPath(path, f);
    }

    private static void DrawScreenshot(SKCanvas c, float s)
    {
        using var p = W(2f);
        // Monitor outline
        c.DrawRoundRect(s*.1f, s*.2f, s*.8f, s*.52f, 3, 3, p);
        // Crosshair in center
        float cx = s*.5f, cy = s*.46f;
        c.DrawLine(cx-s*.1f, cy, cx+s*.1f, cy, p);
        c.DrawLine(cx, cy-s*.1f, cx, cy+s*.1f, p);
        // Corner brackets
        float bw = s*.08f;
        float[] xs = { s*.18f, s*.82f-bw };
        float[] ys = { s*.28f, s*.64f-bw };
        foreach (var bx in xs) foreach (var by in ys)
        {
            bool right = bx > s*.5f, bottom = by > s*.5f;
            float ex = right ? bx+bw : bx, ey = bottom ? by+bw : by;
            c.DrawLine(bx, ey, ex, ey, p);
            c.DrawLine(ex, by, ex, ey, p);
        }
        // Stand
        c.DrawLine(s*.38f, s*.72f, s*.62f, s*.72f, p);
        c.DrawLine(s*.5f,  s*.72f, s*.5f,  s*.8f,  p);
    }

    // ── Fallback ──────────────────────────────────────────────────────────

    private static SKBitmap? DrawFallback(int size)
    {
        var bmp    = new SKBitmap(size, size);
        var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.Transparent);
        DrawDefault(canvas, size);
        canvas.Flush();
        return bmp;
    }
}
