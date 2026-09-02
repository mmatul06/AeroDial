// AeroDial — ScreenCapture.cs
// PNG screenshots for the self-test: a screen rectangle (what the overlay actually
// looks like composited over the desktop) or a window (via PrintWindow).

using System.Runtime.InteropServices;
using SkiaSharp;

namespace AeroDial.Core;

internal static class ScreenCapture
{
    [DllImport("user32.dll")] private static extern nint GetDC(nint hWnd);
    [DllImport("user32.dll")] private static extern int  ReleaseDC(nint hWnd, nint hDC);
    [DllImport("user32.dll")] private static extern bool PrintWindow(nint hWnd, nint hDC, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hWnd, out Win32.RECT rect);
    [DllImport("gdi32.dll")]  private static extern bool BitBlt(nint hdcDest, int x, int y, int w, int h, nint hdcSrc, int sx, int sy, uint rop);

    private const uint SRCCOPY = 0x00CC0020;
    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    /// <summary>Captures a screen rectangle (physical pixels) to a PNG file.</summary>
    public static bool CaptureScreen(int x, int y, int w, int h, string path)
    {
        if (w <= 0 || h <= 0) return false;
        nint screenDC = GetDC(0);
        try
        {
            return Capture(w, h, path, memDC => BitBlt(memDC, 0, 0, w, h, screenDC, x, y, SRCCOPY));
        }
        finally { ReleaseDC(0, screenDC); }
    }

    /// <summary>Captures a window's rendered content to a PNG file.</summary>
    public static bool CaptureWindow(nint hwnd, string path)
    {
        if (hwnd == 0 || !GetWindowRect(hwnd, out var r)) return false;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        if (w <= 0 || h <= 0) return false;
        return Capture(w, h, path, memDC => PrintWindow(hwnd, memDC, PW_RENDERFULLCONTENT));
    }

    private static bool Capture(int w, int h, string path, Func<nint, bool> draw)
    {
        nint memDC = Win32.CreateCompatibleDC(0);
        var bmi = new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize        = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth       = w,
                biHeight      = -h, // top-down
                biPlanes      = 1,
                biBitCount    = 32,
                biCompression = Win32.BI_RGB,
            }
        };
        nint hBmp = Win32.CreateDIBSection(memDC, ref bmi, Win32.DIB_RGB_COLORS, out nint pBits, 0, 0);
        nint hOld = Win32.SelectObject(memDC, hBmp);
        try
        {
            if (!draw(memDC)) return false;

            var pixels = new byte[w * h * 4];
            Marshal.Copy(pBits, pixels, 0, pixels.Length);
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255; // GDI leaves alpha undefined

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                using var bmp = new SKBitmap();
                bmp.InstallPixels(new SKImageInfo(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque),
                                  handle.AddrOfPinnedObject(), w * 4);
                using var img  = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 90);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var fs = File.Create(path);
                data.SaveTo(fs);
            }
            finally { handle.Free(); }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn($"ScreenCapture failed for {path}", ex);
            return false;
        }
        finally
        {
            Win32.SelectObject(memDC, hOld);
            Win32.DeleteObject(hBmp);
            Win32.DeleteDC(memDC);
        }
    }
}
