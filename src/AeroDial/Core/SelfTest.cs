// AeroDial — SelfTest.cs
// Scripted end-to-end smoke test, run with `AeroDial.exe --selftest`.
//
// Drives the real overlay (open, hover across slices, child rings, click outside,
// center click) through OverlayRenderer's virtual-cursor hooks instead of the real
// mouse, so it can run from a build script without stealing the user's pointer.
// Results are written to the normal log as SELFTEST lines; the process exits when done.

using System.Runtime.InteropServices;
using AeroDial.Overlay;

namespace AeroDial.Core;

internal static class SelfTest
{
    public static bool Enabled { get; set; }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);

    public static void Start()
    {
        if (!Enabled) return;
        new Thread(Run) { IsBackground = true, Name = "AeroDial.SelfTest" }.Start();
    }

    private static void Run()
    {
        try
        {
            Logger.Info("SELFTEST: begin");
            if (!CheckGlyphBounds()) Environment.ExitCode = 2;
            OverlayRenderer.TestInputEnabled = true;

            int cx = GetSystemMetrics(0) / 2, cy = GetSystemMetrics(1) / 2;
            var (_, dpi) = Win32.GetMonitorInfoForPoint(new Win32.POINT { X = cx, Y = cy });
            float s = App.Config.Current.Appearance.Scale * dpi;

            // Ring geometry in physical pixels (see AppConstants / OverlayRenderer.PollInput).
            float ringMid  = (AppConstants.RingInnerRadius + AppConstants.RingOuterRadius) / 2f * s;
            float childMid = (AppConstants.RingOuterRadius + AppConstants.ChildRingGap
                            + AppConstants.ChildRingThickness / 2f) * s;
            int   n        = App.Config.Current.Appearance.SliceCount;
            float arc      = 360f / n;

            (int X, int Y) At(float r, float angleDeg)
            {
                double a = angleDeg * Math.PI / 180.0;
                return (cx + (int)(Math.Cos(a) * r), cy + (int)(Math.Sin(a) * r));
            }
            (int X, int Y) Slice(int i)      => At(ringMid,  -90f + i * arc);
            (int X, int Y) ChildOf(int i)    => At(childMid, -90f + i * arc);

            // Screenshots go to %AppData%\AeroDial\selftest\ so the result can be inspected.
            string shots = Path.Combine(AppConstants.AppDataDir, "selftest");
            Directory.CreateDirectory(shots);
            int   capHalf = (int)(AppConstants.CanvasSize * s / 2f);

            Cursor(cx, cy);
            Ui(() => App.Overlay.OpenAtCursor(new System.Drawing.Point(cx, cy)));
            Sleep(600);
            Shot(() => ScreenCapture.CaptureScreen(cx - capHalf, cy - capHalf, capHalf * 2, capHalf * 2,
                Path.Combine(shots, "overlay-open.png")));

            for (int i = 0; i < Math.Min(n, 3); i++)
            {
                var p = Slice(i); Cursor(p.X, p.Y); Sleep(500);   // hover slice i (opens child ring for submenus)
                if (i == 0)
                    Shot(() => ScreenCapture.CaptureScreen(cx - capHalf, cy - capHalf, capHalf * 2, capHalf * 2,
                        Path.Combine(shots, "overlay-hover.png")));
                var c = ChildOf(i); Cursor(c.X, c.Y); Sleep(600); // sweep into its child ring zone
                if (i == 1)
                    Shot(() => ScreenCapture.CaptureScreen(cx - capHalf, cy - capHalf, capHalf * 2, capHalf * 2,
                        Path.Combine(shots, "overlay-child-ring.png")));
            }

            Cursor(cx + (int)(childMid * 2.5f), cy + (int)(childMid * 1.5f)); Sleep(200);
            Click(); Sleep(700);                                   // outside → CloseOnClickOutside

            Ui(() => App.Overlay.OpenAtCursor(new System.Drawing.Point(cx, cy)));
            Sleep(600);
            Cursor(cx + 2, cy + 2); Sleep(200);
            Click(); Sleep(700);                                   // center at root → NavigateBack → Close

            // Keyboard navigation: open, step Right twice (highlights slice 1), digit 2 (slice 1
            // via digit), Backspace at root (closes the child ring if any, else nothing), Escape closes.
            Logger.Info("SELFTEST: keyboard");
            Ui(() => App.Overlay.OpenAtCursor(new System.Drawing.Point(cx, cy)));
            Sleep(600);
            App.Hooks.SimulateNavKey(0x27); Sleep(300);            // Right → slice 0
            App.Hooks.SimulateNavKey(0x27); Sleep(400);            // Right → slice 1 (Apps submenu → child ring)
            Shot(() => ScreenCapture.CaptureScreen(cx - capHalf, cy - capHalf, capHalf * 2, capHalf * 2,
                Path.Combine(shots, "overlay-keyboard.png")));
            App.Hooks.SimulateNavKey(0x33); Sleep(300);            // digit 3 → slice 2
            App.Hooks.SimulateNavKey(0x1B); Sleep(700);            // Escape → close

            // Settings window: construct it and walk every page. A page whose constructor
            // throws surfaces as an "Unhandled XAML exception" FATAL line in the log.
            Logger.Info("SELFTEST: settings pages");
            Ui(UI.Views.SettingsWindow.ShowOrActivate);
            Sleep(1500);
            foreach (var tag in UI.Views.SettingsWindow.PageTags)
            {
                Ui(() => UI.Views.SettingsWindow.Instance?.NavigateTo(tag));
                Sleep(700);
                Logger.Debug($"SELFTEST: page '{tag}' shown");
                Shot(() => ScreenCapture.CaptureWindow(UI.Views.SettingsWindow.WindowHandle,
                    Path.Combine(shots, $"settings-{tag}.png")));
            }
            Sleep(500);

            Logger.Info("SELFTEST: end");
        }
        catch (Exception ex)
        {
            Logger.Error("SELFTEST: failed", ex);
        }
        finally
        {
            OverlayRenderer.TestInputEnabled = false;
            Logger.FlushNow();
            App.Tray.DispatcherQueue.TryEnqueue(App.RequestShutdown);
        }
    }

    /// <summary>Rasterizes a handful of icon-font glyphs and checks that the ink is whole and
    /// centered: nothing within 2 px of the bitmap edge, ink center within 3 px of the bitmap
    /// center. Guards against the v3.0.0 regression where every glyph lost its left edge.</summary>
    private static bool CheckGlyphBounds()
    {
        bool ok = true;
        var keys = FluentGlyphs.Named.Take(12).Select(g => FluentGlyphs.Prefix + g.Name)
            .Append(FluentGlyphs.Prefix + "E8B7").ToArray();
        foreach (var key in keys)
        {
            var bmp = IconRegistry.Get(key, 1f);
            if (bmp is null) { Logger.Error($"SELFTEST: glyph {key} did not render"); ok = false; continue; }

            int w = bmp.Width, h = bmp.Height;
            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (bmp.GetPixel(x, y).Alpha < 16) continue;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
            if (maxX < 0) { Logger.Error($"SELFTEST: glyph {key} is empty"); ok = false; continue; }

            bool  edge = minX < 2 || minY < 2 || maxX > w - 3 || maxY > h - 3;
            float offX = (minX + maxX) / 2f - (w - 1) / 2f;
            float offY = (minY + maxY) / 2f - (h - 1) / 2f;
            if (edge || Math.Abs(offX) > 3f || Math.Abs(offY) > 3f)
            {
                Logger.Error($"SELFTEST: glyph {key} clipped or off-center: ink x {minX}..{maxX}, y {minY}..{maxY} in {w}x{h}, offset ({offX:F1},{offY:F1})");
                ok = false;
            }
            else
                Logger.Debug($"SELFTEST: glyph {key} ok: ink x {minX}..{maxX}, y {minY}..{maxY}");
        }
        Logger.Info(ok ? "SELFTEST: glyph bounds ok" : "SELFTEST: glyph bounds FAILED");
        return ok;
    }

    private static void Cursor(int x, int y)
    {
        OverlayRenderer.TestCursorX = x;
        OverlayRenderer.TestCursorY = y;
        Logger.Debug($"SELFTEST: cursor -> ({x},{y})");
    }

    private static void Click()
    {
        OverlayRenderer.TestLmbDown = true;
        Sleep(60);
        OverlayRenderer.TestLmbDown = false;
        Logger.Debug("SELFTEST: click");
    }

    private static void Sleep(int ms) => Thread.Sleep(ms);

    private static void Shot(Func<bool> capture)
    {
        try { Logger.Debug(capture() ? "SELFTEST: screenshot saved" : "SELFTEST: screenshot failed"); }
        catch (Exception ex) { Logger.Warn("SELFTEST: screenshot threw", ex); }
    }

    private static void Ui(Action a) => App.Tray.DispatcherQueue.TryEnqueue(() => a());
}
