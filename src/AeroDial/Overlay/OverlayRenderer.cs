#pragma warning disable CA1416 // Windows-only APIs — AeroDial is Windows-only by design

// AeroDial — OverlayRenderer.cs
// SkiaSharp ring renderer with Win32-based cursor polling for input.
// XAML pointer events are unreliable on WS_EX_LAYERED windows so we poll
// GetCursorPos() and GetAsyncKeyState() directly on the render timer.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using AeroDial.Config;
using AeroDial.Core;
using AeroDial.Themes;
using SkiaSharp;
using static AeroDial.Core.RingGeometry; // GetArcLayout, HitTestArc, SplitCenterLabel, SliceIndexAt

namespace AeroDial.Overlay;

internal sealed class OverlayRenderer : IDisposable
{
    public event Action<int>? HoveredIndexChanged;
    public event Action<int>? ItemClicked;
    public event Action?      CenterClicked;
    public event Action<int>? ChildItemClicked;
    public event Action<int>? ChildHoveredIndexChanged;
    public event Action<int>? L3ItemClicked;
    public event Action<int>? L3HoveredIndexChanged;
    public event Action?      ClickedOutside;

    // Raw Win32 layered window — no WinUI compositor involvement
    private nint _hwnd;

    // DIBSection-backed bitmap: Skia renders directly into GDI memory, zero-copy to DWM
    private SKBitmap?  _bitmap;
    private SKCanvas?  _bitmapCanvas;
    private nint       _hMemDC;
    private nint       _hDibBitmap;
    private nint       _hOldBitmap;
    private nint       _pBits;
    private int        _currentBitmapSize;

    private readonly object _lock = new();

    private RadialMenuConfig? _menu;
    private float   _dpiScale     = 1f;
    private AnimState _animState  = AnimState.Hidden;
    private float   _animProgress = 0f;
    private int     _hoveredIndex = -1;
    private long    _hoverStart   = 0;
    private Action? _hideCallback;

    // Child ring state — L2 (outer concentric ring shown when a SubMenu slice is hovered)
    private RadialMenuConfig? _childMenu;
    private int   _childParentIndex  = -1;
    private int   _childHoveredIndex = -1;
    private float _childAnimProgress = 0f;
    private long  _childAnimStart;
    private bool  _childAnimating;
    private long  _childHoverStart   = 0;

    // L3 ring state — shown when hovering an L2 SubMenu item
    private RadialMenuConfig? _l3Menu;
    private int   _l3ParentIndex  = -1;
    private int   _l3HoveredIndex = -1;
    private float _l3AnimProgress = 0f;
    private long  _l3AnimStart;
    private bool  _l3Animating;
    private long  _l3HoverStart  = 0;

    private int  _winLeft, _winTop, _winSize;
    private bool _wasLeftPressed;
    private bool _wasRightPressed;
    private volatile bool _hasParent;

    // Dedicated render thread — eliminates thread-pool scheduling jitter and makes
    // concurrent RenderFrame calls physically impossible (fixes 0xc0000409 crash).
    private Thread?       _renderThread;
    private volatile bool _renderRunning;
    private int           _renderGeneration; // incremented on each BeginShow to evict stale threads
    private readonly ManualResetEventSlim _firstFrameDone = new(false);

    // ── Input events → UI thread ──────────────────────────────────────────
    // PollInput runs on the render thread but the controller's menu state machine must
    // run on exactly one thread. Semantic events (hover changed, item clicked, ...) are
    // queued here and drained on the UI thread via DispatcherQueue, in order. Purely
    // visual hover feedback stays renderer-local so it remains zero-latency.
    private enum InputKind { Hover, Click, Center, ChildClick, ChildHover, L3Click, L3Hover, Outside }
    private readonly System.Collections.Concurrent.ConcurrentQueue<(InputKind Kind, int Index)> _events = new();
    private int _drainPending;

    private void Post(InputKind kind, int index = -1)
    {
        _events.Enqueue((kind, index));
        if (Interlocked.Exchange(ref _drainPending, 1) == 0)
            App.Tray.DispatcherQueue.TryEnqueue(DrainEvents);
    }

    private void DrainEvents()
    {
        Interlocked.Exchange(ref _drainPending, 0);
        while (_events.TryDequeue(out var e))
        {
            if (e.Kind is not (InputKind.Hover or InputKind.ChildHover or InputKind.L3Hover))
                Logger.Debug($"Input event: {e.Kind} {e.Index}");
            switch (e.Kind)
            {
                case InputKind.Hover:      HoveredIndexChanged?.Invoke(e.Index);      break;
                case InputKind.Click:      ItemClicked?.Invoke(e.Index);              break;
                case InputKind.Center:     CenterClicked?.Invoke();                   break;
                case InputKind.ChildClick: ChildItemClicked?.Invoke(e.Index);         break;
                case InputKind.ChildHover: ChildHoveredIndexChanged?.Invoke(e.Index); break;
                case InputKind.L3Click:    L3ItemClicked?.Invoke(e.Index);            break;
                case InputKind.L3Hover:    L3HoveredIndexChanged?.Invoke(e.Index);    break;
                case InputKind.Outside:    ClickedOutside?.Invoke();                  break;
            }
        }
    }

    // ── Dirty flag + static layer ─────────────────────────────────────────
    // The ring is only re-rasterized when something changed (hover, menu, animation).
    // Between changes the loop keeps polling input at full rate but re-composites the
    // cached ring layer at a low idle cadence for the continuous effects (shimmer,
    // visualizer, volume lerp).
    private int  _dirty = 1;
    private long _lastFrameAt = long.MinValue / 2;
    private const int IdleFrameIntervalMs = 42; // ~24 fps
    private SKBitmap? _staticLayer;
    private SKCanvas? _staticCanvas;
    private bool      _staticValid;
    private bool      _transientGradients;
    private readonly SKPaint _blitPaint = new() { BlendMode = SKBlendMode.Src };

    private void MarkDirty() => Interlocked.Exchange(ref _dirty, 1);

    // Debug-logging diagnostics (only emitted when debug logging is enabled)
    private long _pollHeartbeatAt = long.MinValue / 2;
    private long _pollCount, _frameCount;

    // ── Test hooks (Core/SelfTest.cs) ─────────────────────────────────────
    // When enabled, PollInput reads this virtual cursor / button state instead of the
    // real mouse, so the full input path can be exercised without moving the pointer.
    internal static volatile bool TestInputEnabled;
    internal static volatile int  TestCursorX, TestCursorY;
    internal static volatile bool TestLmbDown;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _animStart;
    // long can't be volatile in C# (only ≤32-bit types are allowed).
    // Use Interlocked for safe cross-thread access — write from UI thread, read from render thread.
    private long _volumeFlashAt       = long.MinValue / 2;
    private long _showVolumeLabelUntil = long.MinValue / 2; // show "Vol X%" in center until this ms

    // Volume level — polled every 200ms in PollInput (render thread only, no Interlocked needed)
    private float _volumeLevel        = 0.5f;
    private float _volumeDisplayLevel = 0.5f; // smoothly lerped toward _volumeLevel each frame
    private long  _volumeUpdateStamp  = long.MinValue / 2;

    /// <summary>Triggers a brief accent-colored ring flash just outside the main ring
    /// and shows "Vol X%" in the center for 2 seconds.
    /// Called by OverlayController when a scroll-wheel volume action fires.</summary>
    public void TriggerVolumeFlash()
    {
        long now = _clock.ElapsedMilliseconds;
        Interlocked.Exchange(ref _volumeFlashAt, now);
        Interlocked.Exchange(ref _showVolumeLabelUntil, now + 2000);
        MarkDirty();
    }

    private readonly SKPaint _fill   = new() { IsAntialias = true };
    private readonly SKPaint _stroke = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _text   = new() { IsAntialias = true };

    private SKTypeface? _cachedTypeface;
    private string?     _cachedFontFamily;

    // ── Pooled draw objects ───────────────────────────────────────────────
    // The render loop runs at ~100+ fps; allocating SKPaint/SKPath/SKShader/
    // SKMaskFilter/SKColorFilter every frame produced GC hitches. These reusable
    // objects and small caches eliminate the per-frame native allocations.
    // Everything here is touched only on the single render thread, so no locks
    // are needed — but each cache MUST dispose the native object it replaces
    // (same discipline as _cachedTypeface) and Dispose() disposes them all.
    private readonly SKPath  _path        = new();
    private readonly SKPath  _arcPath     = new();
    private readonly SKPaint _glowFill    = new() { IsAntialias = true };
    private readonly SKPaint _arcStroke   = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _iconPaint   = new() { IsAntialias = true, FilterQuality = SKFilterQuality.High };
    private readonly SKPaint _shimmerPaint = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    private readonly Dictionary<int, SKMaskFilter>                 _blurCache       = new();
    private readonly Dictionary<(int, uint, uint, int), SKShader>  _gradientCache   = new();
    private readonly Dictionary<uint, SKColorFilter>               _iconFilterCache = new();
    private SKPathEffect? _shimmerDash;
    private float         _shimmerDashScale = -1f;
    private float         _gradCacheCx = float.NaN, _gradCacheCy = float.NaN;

    private static uint Pack(SKColor c) =>
        ((uint)c.Alpha << 24) | ((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue;

    // Blur mask filter cached by sigma (quantized to 0.1) so the same blur radius
    // is built once. Covers slice glow, accent-arc glow, and volume-tip glow.
    private SKMaskFilter GetBlur(float sigma)
    {
        int key = Math.Max(1, (int)MathF.Round(sigma * 10f));
        if (!_blurCache.TryGetValue(key, out var mf))
        {
            if (_blurCache.Count > 128) { foreach (var m in _blurCache.Values) m.Dispose(); _blurCache.Clear(); }
            mf = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, key / 10f);
            _blurCache[key] = mf;
        }
        return mf;
    }

    // Radial-gradient shader cached by (radius, both colors, gradient split).
    // In the steady Open state radius+colors are stable, so this hits every frame
    // across all rings. The center (cx,cy) is baked into the shader, so the cache
    // is cleared if the center moves (window resize / monitor change).
    private SKShader GetGradient(float cx, float cy, float outerR, SKColor c0, SKColor c1, float gradPos)
    {
        if (_gradCacheCx != cx || _gradCacheCy != cy) { ClearGradientCache(); _gradCacheCx = cx; _gradCacheCy = cy; }
        var key = ((int)MathF.Round(outerR), Pack(c0), Pack(c1), (int)MathF.Round(gradPos * 1000f));
        if (!_gradientCache.TryGetValue(key, out var sh))
        {
            if (_gradientCache.Count > 64) ClearGradientCache();
            sh = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), outerR,
                [c0, c1], [gradPos, 1.0f], SKShaderTileMode.Clamp);
            _gradientCache[key] = sh;
        }
        return sh;
    }

    private void ClearGradientCache()
    {
        foreach (var s in _gradientCache.Values) s.Dispose();
        _gradientCache.Clear();
    }

    // Icon tint (Modulate blend) cached by packed color. Steady state is one entry.
    private SKColorFilter GetIconFilter(SKColor tint)
    {
        uint key = Pack(tint);
        if (!_iconFilterCache.TryGetValue(key, out var cf))
        {
            if (_iconFilterCache.Count > 64) { foreach (var f in _iconFilterCache.Values) f.Dispose(); _iconFilterCache.Clear(); }
            cf = SKColorFilter.CreateBlendMode(tint, SKBlendMode.Modulate);
            _iconFilterCache[key] = cf;
        }
        return cf;
    }

    // Shimmer dash effect — rebuilt only when scale changes.
    private SKPathEffect GetShimmerDash(float scale)
    {
        if (_shimmerDash is null || _shimmerDashScale != scale)
        {
            _shimmerDash?.Dispose();
            _shimmerDash      = SKPathEffect.CreateDash([4f * scale, 12f * scale], 0f);
            _shimmerDashScale = scale;
        }
        return _shimmerDash;
    }

    public OverlayRenderer() { }

    public void SetHwnd(nint hwnd) => _hwnd = hwnd;

    private void EnsureDIBSection(int size)
    {
        if (_currentBitmapSize == size && _hMemDC != 0) return;
        ReleaseDIBSection();
        _currentBitmapSize = size;

        _hMemDC = Win32.CreateCompatibleDC(0);

        var bmi = new Win32.BITMAPINFO
        {
            bmiHeader = new Win32.BITMAPINFOHEADER
            {
                biSize        = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth       = size,
                biHeight      = -size, // negative = top-down scanline order (matches Skia)
                biPlanes      = 1,
                biBitCount    = 32,
                biCompression = Win32.BI_RGB,
            }
        };

        _hDibBitmap = Win32.CreateDIBSection(_hMemDC, ref bmi, Win32.DIB_RGB_COLORS,
                                              out _pBits, 0, 0);
        _hOldBitmap = Win32.SelectObject(_hMemDC, _hDibBitmap);

        // Point SKBitmap directly at the DIBSection pixel memory — Skia renders zero-copy into GDI
        var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        _bitmap = new SKBitmap();
        _bitmap.InstallPixels(info, _pBits, size * 4);
        _bitmapCanvas = new SKCanvas(_bitmap);

        // Offscreen layer holding the rasterized ring (same format as the DIB so the
        // per-frame blit is a straight pixel copy).
        _staticLayer  = new SKBitmap(size, size, SKColorType.Bgra8888, SKAlphaType.Premul);
        _staticCanvas = new SKCanvas(_staticLayer);
        _staticValid  = false;
    }

    private void ReleaseDIBSection()
    {
        _staticCanvas?.Dispose();
        _staticCanvas = null;
        _staticLayer?.Dispose();
        _staticLayer  = null;
        _staticValid  = false;

        _bitmapCanvas?.Dispose();
        _bitmapCanvas = null;
        _bitmap?.Dispose(); // does not free _pBits — DIBSection owns it
        _bitmap = null;

        if (_hMemDC != 0)
        {
            Win32.SelectObject(_hMemDC, _hOldBitmap);
            Win32.DeleteObject(_hDibBitmap);
            Win32.DeleteDC(_hMemDC);
            _hMemDC = _hDibBitmap = _hOldBitmap = _pBits = 0;
        }
        _currentBitmapSize = 0;
    }

    private void FlushToWindow()
    {
        if (_hwnd == 0 || _hMemDC == 0 || _pBits == 0) return;
        int size  = _currentBitmapSize;
        int left, top;
        lock (_lock) { left = _winLeft; top = _winTop; }

        var ptDst = new Win32.POINT { X = left, Y = top };
        var sz    = new Win32.SIZE  { cx = size, cy = size };
        var ptSrc = new Win32.POINT { X = 0, Y = 0 };
        var blend = new Win32.BLENDFUNCTION
        {
            BlendOp             = Win32.AC_SRC_OVER,
            BlendFlags          = 0,
            SourceConstantAlpha = 255,
            AlphaFormat         = Win32.AC_SRC_ALPHA, // use per-pixel premultiplied alpha
        };
        Win32.UpdateLayeredWindow(_hwnd, 0, ref ptDst, ref sz, _hMemDC, ref ptSrc, 0, ref blend, Win32.ULW_ALPHA);
    }

    // ── Show / Hide / Navigate ────────────────────────────────────────────

    public void BeginShow(RadialMenuConfig menu, System.Drawing.Point origin, float dpiScale, bool hasParent)
    {
        // Evict any running render thread from the previous overlay session.
        // The thread checks _renderGeneration on each iteration and exits when it no longer matches.
        // PreRender() will Join the old thread before rendering to prevent concurrent DIBSection writes.
        _renderRunning = false;
        Interlocked.Increment(ref _renderGeneration);

        lock (_lock)
        {
            _menu              = menu;
            _dpiScale          = dpiScale;
            _hoveredIndex      = -1;
            _animState         = AnimState.Opening;
            _animProgress      = 0f;
            _animStart         = _clock.ElapsedMilliseconds;
            _wasLeftPressed    = false;
            _wasRightPressed   = false;
            _hasParent         = hasParent;
            _childMenu         = null;
            _childParentIndex  = -1;
            _childHoveredIndex = -1;
            _childAnimProgress = 0f;
            _childAnimating    = false;
            _l3Menu         = null;
            _l3ParentIndex  = -1;
            _l3HoveredIndex = -1;
            _l3AnimProgress = 0f;
            _l3Animating    = false;
        }
        MarkDirty();
    }

    public void BeginHide(Action onComplete)
    {
        lock (_lock)
        {
            _hideCallback  = onComplete;
            _animState     = AnimState.Closing;
            _animStart     = _clock.ElapsedMilliseconds;
            _childMenu     = null;
            _childAnimProgress = 0f;
            _childAnimating = false;
        }
        MarkDirty();
    }

    public void NavigateTo(RadialMenuConfig menu, bool hasParent)
    {
        lock (_lock)
        {
            _menu          = menu;
            _hoveredIndex  = -1;
            _animProgress  = 0.7f;
            _animState     = AnimState.Open;
            _wasLeftPressed = false;
            _hasParent     = hasParent;
            _childMenu     = null;
            _childParentIndex = -1;
            _childHoveredIndex = -1;
            _childAnimProgress = 0f;
            _childAnimating = false;
            _l3Menu         = null;
            _l3ParentIndex  = -1;
            _l3HoveredIndex = -1;
            _l3AnimProgress = 0f;
            _l3Animating    = false;
        }
        MarkDirty();
    }

    /// <summary>Swaps the main ring's menu without resetting navigation or animation state
    /// (used when a dynamic menu finishes building after it was already shown).</summary>
    public void ReplaceMenu(RadialMenuConfig menu)
    {
        lock (_lock)
        {
            _menu         = menu;
            _hoveredIndex = -1;
        }
        MarkDirty();
    }

    public void ShowChildMenu(RadialMenuConfig menu, int parentIndex)
    {
        lock (_lock)
        {
            _childMenu         = menu;
            _childParentIndex  = parentIndex;
            _childHoveredIndex = -1;
            _childHoverStart   = 0;
            _childAnimStart    = _clock.ElapsedMilliseconds;
            _childAnimating    = true;
            _childAnimProgress = 0f;
        }
        MarkDirty();
    }

    /// <summary>Swaps the child ring's menu in place, keeping its pop-out animation state.</summary>
    public void ReplaceChildMenu(RadialMenuConfig menu)
    {
        lock (_lock)
        {
            if (_childMenu is null) return;
            _childMenu         = menu;
            _childHoveredIndex = -1;
            _childHoverStart   = 0;
        }
        MarkDirty();
    }

    public void HideChildMenu()
    {
        lock (_lock)
        {
            _childMenu         = null;
            _childParentIndex  = -1;
            _childHoveredIndex = -1;
            _childAnimProgress = 0f;
            _childAnimating    = false;
            // Hiding L2 also dismisses L3
            _l3Menu         = null;
            _l3ParentIndex  = -1;
            _l3HoveredIndex = -1;
            _l3AnimProgress = 0f;
            _l3Animating    = false;
        }
        MarkDirty();
    }

    public void ShowL3Menu(RadialMenuConfig menu, int l2ParentIndex)
    {
        lock (_lock)
        {
            _l3Menu         = menu;
            _l3ParentIndex  = l2ParentIndex;
            _l3HoveredIndex = -1;
            _l3HoverStart   = 0;
            _l3AnimStart    = _clock.ElapsedMilliseconds;
            _l3Animating    = true;
            _l3AnimProgress = 0f;
        }
        MarkDirty();
    }

    public void HideL3Menu()
    {
        lock (_lock)
        {
            _l3Menu         = null;
            _l3ParentIndex  = -1;
            _l3HoveredIndex = -1;
            _l3AnimProgress = 0f;
            _l3Animating    = false;
        }
        MarkDirty();
    }

    public void UpdateWindowRect(int left, int top, int size)
    {
        lock (_lock) { _winLeft = left; _winTop = top; _winSize = size; }
        MarkDirty();
    }

    /// <summary>
    /// Launches the dedicated render thread and waits (briefly) for it to flush the first
    /// frame, so the layered window has pixel content the instant it becomes visible.
    /// Nothing touches the DIBSection or the Skia caches from the calling thread.
    /// Must be called after <see cref="BeginShow"/> and before <c>ShowWindow</c>.
    /// </summary>
    public void PreRender()
    {
        // The previous render thread (if any) was told to exit by BeginShow bumping the
        // generation; it leaves after its current frame (a few ms). Wait so two threads
        // never write the DIBSection concurrently.
        Thread? old = _renderThread;
        if (old is not null && old.IsAlive) old.Join(100);

        _firstFrameDone.Reset();
        MarkDirty();

        // Capturing _renderGeneration before thread start ensures the thread always has
        // the correct generation even if BeginShow is called again before it reads the field.
        _renderRunning = true;
        int gen = _renderGeneration;
        var t = new Thread(RenderLoop) { IsBackground = true, Name = "AeroDial.RenderThread" };
        t.Start(gen);
        _renderThread = t;

        // Bounded wait: a stalled render thread must never hang the UI thread.
        _firstFrameDone.Wait(80);
    }

    // ── Render thread loop ────────────────────────────────────────────────

    private void RenderLoop(object? state)
    {
        int  myGeneration  = (int)state!;
        var  sw            = new Stopwatch();
        bool firstSignaled = false;
        Logger.Debug($"Render loop started (gen {myGeneration}).");

        while (_renderRunning && _renderGeneration == myGeneration)
        {
            sw.Restart();
            try
            {
                IconRegistry.DrainRetired();
                PollInput();

                bool animating;
                lock (_lock)
                {
                    animating = _animState is AnimState.Opening or AnimState.Closing
                             || _childAnimating || _l3Animating;
                }

                bool dirty    = Interlocked.Exchange(ref _dirty, 0) == 1 || animating;
                long now      = _clock.ElapsedMilliseconds;
                bool idleTick = now - _lastFrameAt >= IdleFrameIntervalMs;

                if (dirty || idleTick)
                {
                    RenderFrame(rebuildStatic: dirty);
                    _lastFrameAt = now;
                    _frameCount++;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("OverlayRenderer render error", ex);
            }

            if (!firstSignaled) { _firstFrameDone.Set(); firstSignaled = true; }

            AnimState st;
            lock (_lock) { st = _animState; }
            if (st == AnimState.Hidden) break; // closing animation completed — exit loop

            // Sleep for the remaining frame budget.
            // timeBeginPeriod(1) (called at app startup) makes Thread.Sleep accurate to ~1ms.
            long remaining = AppConstants.FrameIntervalMs - sw.ElapsedMilliseconds;
            if (remaining > 0) Thread.Sleep((int)remaining);
        }

        Logger.Debug($"Render loop exited (gen {myGeneration}, running={_renderRunning}, currentGen={_renderGeneration}).");
        _renderRunning = false;
        if (!firstSignaled) _firstFrameDone.Set();
    }

    // ── Input polling ─────────────────────────────────────────────────────

    private void PollInput()
    {
        try
        {
            RadialMenuConfig? menu, childMenu;
            int winLeft, winTop, winSize;
            AnimState animState;
            float dpiScale;
            int childParentIdx;

            lock (_lock)
            {
                menu         = _menu;
                childMenu    = _childMenu;
                childParentIdx = _childParentIndex;
                winLeft      = _winLeft;
                winTop       = _winTop;
                winSize      = _winSize;
                animState    = _animState;
                dpiScale     = _dpiScale;
            }

            if (menu is null || animState == AnimState.Hidden || winSize == 0) return;

            Win32.POINT screenPt;
            if (TestInputEnabled) screenPt = new Win32.POINT { X = TestCursorX, Y = TestCursorY };
            else                  Win32.GetCursorPos(out screenPt);

            float scale   = App.Config.Current.Appearance.Scale;
            float logical = winSize / dpiScale;
            float cx      = logical / 2f;
            float cy      = logical / 2f;
            float localX  = (screenPt.X - winLeft) / dpiScale;
            float localY  = (screenPt.Y - winTop)  / dpiScale;
            float dx      = localX - cx;
            float dy      = localY - cy;
            float dist    = MathF.Sqrt(dx * dx + dy * dy);
            float innerR  = AppConstants.RingInnerRadius * scale;
            float outerR  = AppConstants.RingOuterRadius * scale;

            if (_clock.ElapsedMilliseconds - _pollHeartbeatAt >= 5000)
            {
                _pollHeartbeatAt = _clock.ElapsedMilliseconds;
                Logger.Debug($"Poll heartbeat: cursor=({screenPt.X},{screenPt.Y}) win=({winLeft},{winTop},{winSize}) local=({localX:F0},{localY:F0}) dist={dist:F0} state={animState} polls={_pollCount} frames={_frameCount}");
            }
            _pollCount++;

            var   appearance  = App.Config.Current.Appearance;
            int   sliceCount  = appearance.SliceCount;
            bool  isFlick     = App.Config.Current.Behavior.SelectionMode == SelectionMode.Flick;
            bool  partial     = appearance.PartialArcSubMenu;
            bool  thinning    = appearance.DynamicRingThinning;

            // Read L3 state
            RadialMenuConfig? l3Menu;
            int l3ParentIdx;
            lock (_lock) { l3Menu = _l3Menu; l3ParentIdx = _l3ParentIndex; }

            // Compute L2 ring bounds (thinned when L3 is present and thinning is on)
            float l2Thickness = (thinning && l3Menu != null && childMenu != null)
                ? AppConstants.ThinChildRingThickness
                : AppConstants.ChildRingThickness;
            float cInnerR = outerR + AppConstants.ChildRingGap * scale;
            float cOuterR = cInnerR + l2Thickness * scale;

            // Compute L3 ring bounds
            float l3InnerR = cOuterR + AppConstants.ChildRingGap * scale;
            float l3OuterR = l3InnerR + AppConstants.ChildRingThickness * scale;

            // ── L3 hit test (outermost ring — highest priority) ────────────
            int  newL3Hovered = -1;
            bool inL3Zone     = false;

            if (l3Menu is not null && l3Menu.Items.Count > 0)
            {
                if (dist >= cOuterR - 4f && dist <= l3OuterR + 10f)
                {
                    inL3Zone = true;
                    if (dist >= l3InnerR)
                    {
                        float angleDeg = MathF.Atan2(dy, dx) * 180f / MathF.PI;
                        if (angleDeg < 0) angleDeg += 360f;

                        // Compute L2 parent angle for partial-arc layout
                        float l1MidAngle = -90f + childParentIdx * (360f / sliceCount);
                        float l2MidAngle;
                        if (partial && childMenu is not null)
                        {
                            var (l2Start, l2Seg, _) = GetArcLayout(childMenu.Items.Count, l1MidAngle, true);
                            l2MidAngle = l2Start + l3ParentIdx * l2Seg + l2Seg / 2f;
                        }
                        else
                        {
                            float l2Seg = 360f / (childMenu?.Items.Count ?? 1);
                            l2MidAngle = -90f + l3ParentIdx * l2Seg;
                        }

                        var (startL3, segL3, totalL3) = GetArcLayout(l3Menu.Items.Count, l2MidAngle, partial);
                        newL3Hovered = HitTestArc(angleDeg, startL3, segL3, l3Menu.Items.Count, totalL3);
                    }
                }
            }

            bool l3HoverChanged;
            lock (_lock)
            {
                l3HoverChanged = newL3Hovered != _l3HoveredIndex;
                if (l3HoverChanged) { _l3HoveredIndex = newL3Hovered; _l3HoverStart = _clock.ElapsedMilliseconds; }
            }
            if (l3HoverChanged) { MarkDirty(); Post(InputKind.L3Hover, newL3Hovered); }

            // ── L2 child ring hit test ────────────────────────────────────
            // inChildZone = cursor is between outerR and cOuterR (includes gap before L2 ring)
            int  newChildHovered = -1;
            bool inChildZone     = inL3Zone; // L3 zone implies child zone (cursor is beyond L2)

            if (!inL3Zone && childMenu is not null && childMenu.Items.Count > 0)
            {
                if (dist >= outerR - 4f && dist <= cOuterR + 10f)
                {
                    inChildZone = true;
                    if (dist >= cInnerR)
                    {
                        float angleDeg = MathF.Atan2(dy, dx) * 180f / MathF.PI;
                        if (angleDeg < 0) angleDeg += 360f;
                        float l1MidAngle = -90f + childParentIdx * (360f / sliceCount);
                        var (startC, segC, totalC) = GetArcLayout(childMenu.Items.Count, l1MidAngle, partial);
                        newChildHovered = HitTestArc(angleDeg, startC, segC, childMenu.Items.Count, totalC);
                    }
                }
            }

            // When in L3 zone, keep L2's parent index highlighted
            if (inL3Zone) newChildHovered = l3ParentIdx;

            bool childHoverChanged;
            lock (_lock)
            {
                childHoverChanged = newChildHovered != _childHoveredIndex;
                if (childHoverChanged) { _childHoveredIndex = newChildHovered; _childHoverStart = _clock.ElapsedMilliseconds; }
            }
            if (childHoverChanged) { MarkDirty(); Post(InputKind.ChildHover, newChildHovered); }

            // ── Main ring hit test ────────────────────────────────────────
            int  newHovered = -1;
            bool isCenter   = false;

            if (!inChildZone)
            {
                if (dist < innerR)
                {
                    isCenter = true;
                }
                else if (dist <= outerR || isFlick)
                {
                    newHovered = SliceIndexAt(dx, dy, sliceCount);
                }
            }
            else
            {
                // Keep L1 parent highlighted while cursor is in any child zone
                newHovered = childParentIdx;
            }

            bool hoverChanged;
            lock (_lock)
            {
                hoverChanged = newHovered != _hoveredIndex;
                if (hoverChanged) { _hoveredIndex = newHovered; _hoverStart = _clock.ElapsedMilliseconds; }
            }
            if (hoverChanged) MarkDirty();
            if (hoverChanged && !inChildZone) Post(InputKind.Hover, newHovered);

            // ── Click detection ───────────────────────────────────────────
            bool lmbDown = TestInputEnabled ? TestLmbDown : (Win32.GetAsyncKeyState(0x01) & 0x8000) != 0;
            bool rmbDown = !TestInputEnabled && (Win32.GetAsyncKeyState(0x02) & 0x8000) != 0;
            bool wasLmb, wasRmb;
            lock (_lock) { wasLmb = _wasLeftPressed; wasRmb = _wasRightPressed; }

            bool lmbReleased = !lmbDown && wasLmb;
            bool rmbReleased = !rmbDown && wasRmb;

            if (App.Config.Current.Trigger.VirtualKey != 0x01 && lmbReleased)
            {
                Logger.Debug($"LMB release at dist={dist:F0} hovered={newHovered} child={newChildHovered} l3={newL3Hovered} center={isCenter} childZone={inChildZone}");
                // Priority: L3 > L2 > center > L1
                if (newL3Hovered >= 0)
                    Post(InputKind.L3Click, newL3Hovered);
                else if (newChildHovered >= 0 && !inL3Zone)
                    Post(InputKind.ChildClick, newChildHovered);
                else if (isCenter)
                    Post(InputKind.Center);
                else if (newHovered >= 0
                         && App.Config.Current.Behavior.SelectionMode == SelectionMode.Click)
                    Post(InputKind.Click, newHovered);
                else if (newHovered < 0 && !isCenter && !inChildZone
                         && App.Config.Current.Behavior.CloseOnClickOutside)
                    Post(InputKind.Outside);
            }

            if (rmbReleased && !isCenter && newHovered < 0 && !inChildZone
                && App.Config.Current.Behavior.CloseOnClickOutside)
                Post(InputKind.Outside);

            lock (_lock) { _wasLeftPressed = lmbDown; _wasRightPressed = rmbDown; }

            // Poll system volume every 200ms, and only when something on screen uses it
            // (volume ring, or the visualizer while media plays). Render thread only.
            long nowMs = _clock.ElapsedMilliseconds;
            bool needVolume = appearance.VolumeRingVisibility != VolumeRingVisibility.Hidden
                           || (appearance.ShowVisualizer && App.MediaInfo?.IsPlaying == true);
            if (needVolume && nowMs - _volumeUpdateStamp >= 200)
            {
                _volumeUpdateStamp = nowMs;
                float level = AeroDial.Core.AudioService.GetMasterVolume();
                if (level != _volumeLevel) { _volumeLevel = level; MarkDirty(); }
            }

            CheckDwell(newHovered, newChildHovered, newL3Hovered);
        }
        catch (Exception ex)
        {
            Logger.Error("PollInput error", ex);
        }
    }

    // ── Render frame ──────────────────────────────────────────────────────
    // Two layers. The ring itself (glow, slices, accent arc, dots, icons, child
    // rings) is drawn into _staticLayer only when something changed. Every frame
    // then blits that layer and draws the cheap continuous effects on top (shimmer,
    // volume arc, center label, now-playing, visualizer) before flushing to DWM.
    // In the steady open state this turns a full ring redraw into one bitmap copy.

    private void RenderFrame(bool rebuildStatic)
    {
        if (_hwnd == 0) return;

        int winSize;
        lock (_lock) { winSize = _winSize; }
        if (winSize == 0) return;

        EnsureDIBSection(winSize);
        if (_bitmapCanvas is null || _staticCanvas is null || _staticLayer is null) return;

        try
        {
            RadialMenuConfig? menu, childMenu, l3Menu;
            float dpiScale;
            int hovered, childHovered, childParentIdx, l3Hovered, l3ParentIdx;
            AnimState state; long animStart; bool hasParent;
            bool ringsAnimating;
            lock (_lock)
            {
                menu      = _menu; dpiScale = _dpiScale; hovered = _hoveredIndex;
                state = _animState; animStart = _animStart; hasParent = _hasParent;
                childMenu = _childMenu; childHovered = _childHoveredIndex;
                childParentIdx = _childParentIndex;
                l3Menu = _l3Menu; l3Hovered = _l3HoveredIndex; l3ParentIdx = _l3ParentIndex;

                // Update L2 child ring animation
                if (_childMenu != null && _childAnimating)
                {
                    float ct = ((float)(_clock.ElapsedMilliseconds - _childAnimStart) / 300f).Clamp(0f, 1f);
                    _childAnimProgress = ct.EaseOutBack();
                    if (ct >= 1f) _childAnimating = false;
                }
                // Update L3 ring animation
                if (_l3Menu != null && _l3Animating)
                {
                    float ct = ((float)(_clock.ElapsedMilliseconds - _l3AnimStart) / 300f).Clamp(0f, 1f);
                    _l3AnimProgress = ct.EaseOutBack();
                    if (ct >= 1f) _l3Animating = false;
                }
                ringsAnimating = state is AnimState.Opening or AnimState.Closing || _childAnimating || _l3Animating;
            }

            var canvas = _bitmapCanvas;
            if (menu is null || state == AnimState.Hidden)
            {
                canvas.Clear(SKColors.Transparent);
                FlushToWindow();
                return;
            }

            bool animated = IsAnimEnabled();
            float t = animated
                ? ((float)(_clock.ElapsedMilliseconds - animStart) / GetDuration(state)).Clamp(0f, 1f)
                : 1f;

            // Opening uses easeOutBack (spring overshoot); closing uses easeOutCubic (no overshoot)
            float eased = state == AnimState.Closing ? 1f - t.EaseOutCubic() : t.EaseOutBack();
            float alpha = Math.Min(eased, 1f); // clamped for opacity; eased used raw for scale
            lock (_lock) { _animProgress = alpha; }

            if (t >= 1f)
            {
                if (state == AnimState.Closing)
                {
                    canvas.Clear(SKColors.Transparent);
                    FlushToWindow(); // transparent frame before callback hides the window
                    lock (_lock) { _animState = AnimState.Hidden; }
                    // RenderLoop checks AnimState.Hidden after each frame and exits the loop
                    _hideCallback?.Invoke();
                    _hideCallback = null;
                    return;
                }
                lock (_lock) { _animState = AnimState.Open; }
            }

            float childAnimProg, l3AnimProg;
            lock (_lock)
            {
                childAnimProg = _childMenu != null ? _childAnimProgress : 0f;
                l3AnimProg    = _l3Menu    != null ? _l3AnimProgress    : 0f;
            }

            var   appearance  = App.Config.Current.Appearance;
            float scale       = appearance.Scale;
            float ringOpacity = Math.Clamp(appearance.RingOpacity, 0f, 1f);
            float rAlpha      = alpha * ringOpacity; // animation fade x ring opacity, used for all ring elements
            float logical     = _currentBitmapSize / dpiScale;
            float cx = logical / 2f, cy = logical / 2f;
            float outerR      = AppConstants.RingOuterRadius * eased * scale;
            float innerR      = AppConstants.RingInnerRadius * scale;
            // sliceInnerR = inner edge of slices; detached from the center circle by RingInnerDetach.
            // At 0 the slices touch the center circle (default look).
            float sliceInnerR = innerR + appearance.RingInnerDetach * scale;
            float iconR       = AppConstants.IconOrbitRadius  * scale;

            var theme = App.Themes.ActiveTheme;

            int   sliceCount = Math.Clamp(appearance.SliceCount, 3, 12);
            int   itemCount  = menu.Items.Count;
            float fullArc    = 360f / sliceCount;
            float gap        = appearance.GapDegrees;
            float sweep      = fullArc - gap;
            float startOff   = -90f - fullArc / 2f;

            // Gradient shaders are cached by radius. While a ring is animating the radius
            // changes every frame, which would just churn the cache, so build them transiently.
            _transientGradients = ringsAnimating;

            // ── Static layer: the ring geometry ───────────────────────────
            if (rebuildStatic || !_staticValid)
            {
                var sc = _staticCanvas;
                sc.Clear(SKColors.Transparent);
                sc.Save();
                sc.Scale(dpiScale);

                // When the hovered parent has an open child ring, its outward glow would bleed
                // across the small gap into the child-ring band and tint it, by an amount that
                // varies with the parent angle. Suppress that parent glow so the child ring
                // looks identical regardless of which parent opened it.
                bool parentHasChild = childMenu != null && hovered == childParentIdx;

                // Glow pass (drawn before slices so glow sits behind everything)
                if (hovered >= 0 && hovered < sliceCount && !parentHasChild)
                {
                    float glowStart = startOff + hovered * fullArc + gap / 2f;
                    DrawSliceGlow(sc, cx, cy, outerR, sliceInnerR, glowStart, sweep, theme, rAlpha, scale);
                }

                // Main ring slices
                for (int i = 0; i < sliceCount; i++)
                {
                    bool  hov   = i == hovered;
                    bool  empty = i >= itemCount || menu.Items[i].IsEmptySlot;
                    float start = startOff + i * fullArc + gap / 2f;
                    bool  outerGlow = !(hov && parentHasChild); // also suppress the outer accent-arc glow
                    DrawSlice(sc, cx, cy, outerR, sliceInnerR, start, sweep, theme, hov, rAlpha, scale, empty, outerGlow);
                }

                // Inner accent arc on the hovered slice (inner edge of slice, not center circle)
                if (hovered >= 0 && hovered < itemCount)
                {
                    float arcStart = startOff + hovered * fullArc + gap / 2f + 2f;
                    float arcSweep = sweep - 4f;
                    var   accent   = theme.ToSKColor(theme.AccentColor);
                    _arcStroke.Style       = SKPaintStyle.Stroke;
                    _arcStroke.StrokeCap   = SKStrokeCap.Butt;
                    _arcStroke.StrokeWidth = 2.5f * scale;
                    _arcStroke.Color       = accent.WithAlpha((byte)(200 * rAlpha));
                    _arcStroke.MaskFilter  = GetBlur(2f * scale);
                    _arcPath.Rewind();
                    _arcPath.ArcTo(
                        new SKRect(cx - sliceInnerR - 1f, cy - sliceInnerR - 1f, cx + sliceInnerR + 1f, cy + sliceInnerR + 1f),
                        arcStart, arcSweep, true);
                    sc.DrawPath(_arcPath, _arcStroke);
                    _arcStroke.MaskFilter = null;
                }

                // SubMenu indicator dots on outer rim
                DrawIndicatorDots(sc, cx, cy, outerR, menu, sliceCount, startOff, fullArc, hovered, theme, rAlpha, scale);

                // Icons
                {
                    int labelCount = Math.Min(sliceCount, itemCount);
                    for (int i = 0; i < labelCount; i++)
                    {
                        var   item = menu.Items[i];
                        if (item.IsEmptySlot) continue;
                        bool  hov  = i == hovered;
                        float mid  = startOff + i * fullArc + fullArc / 2f;
                        float rad  = mid.ToRadians();
                        float ix   = cx + MathF.Cos(rad) * iconR;
                        float iy   = cy + MathF.Sin(rad) * iconR;

                        DrawIcon(sc, ix, iy, item, theme, hov, rAlpha, scale);
                        if (item.ScrollUpAction.HasValue || item.ScrollDownAction.HasValue)
                            DrawScrollIndicator(sc, cx, cy, iconR, mid, theme, hov, rAlpha, scale);
                    }
                }

                // L2 child ring (outer concentric ring for the hovered submenu)
                bool   hasThinning  = appearance.DynamicRingThinning && l3Menu != null;
                float  l2Thickness  = hasThinning ? AppConstants.ThinChildRingThickness : AppConstants.ChildRingThickness;
                float  l1MidAngle   = -90f + childParentIdx * (360f / sliceCount);
                bool   partialArc   = appearance.PartialArcSubMenu;

                if (childMenu != null && childAnimProg > 0.01f)
                {
                    float cAlpha     = Math.Min(childAnimProg, 1f) * rAlpha;
                    float scaleF     = 0.85f + 0.15f * Math.Min(childAnimProg, 1f);
                    float baseOuterR = AppConstants.RingOuterRadius * scale;

                    DrawChildRing(sc, cx, cy, baseOuterR, childMenu, childHovered,
                        theme, cAlpha, scale, l1MidAngle, l2Thickness, partialArc, scaleF);
                }

                // L3 ring (second outer ring shown when hovering an L2 SubMenu item)
                if (l3Menu != null && l3AnimProg > 0.01f && childMenu != null)
                {
                    float l3Alpha    = Math.Min(l3AnimProg, 1f) * rAlpha;
                    float scaleF     = 0.85f + 0.15f * Math.Min(l3AnimProg, 1f);
                    float baseOuterR = AppConstants.RingOuterRadius * scale;

                    // Compute L2 item mid angle for partial-arc centering of L3
                    float l2MidAngle;
                    if (partialArc)
                    {
                        var (l2Start, l2Seg, _) = GetArcLayout(childMenu.Items.Count, l1MidAngle, true);
                        l2MidAngle = l2Start + l3ParentIdx * l2Seg + l2Seg / 2f;
                    }
                    else
                    {
                        float l2Seg = 360f / childMenu.Items.Count;
                        l2MidAngle = -90f + l3ParentIdx * l2Seg;
                    }

                    DrawL3Ring(sc, cx, cy, baseOuterR, l2Thickness, l3Menu, l3Hovered,
                        l2MidAngle, theme, l3Alpha, scale, partialArc, scaleF);
                }

                sc.Restore();
                sc.Flush();
                _staticValid = true;
            }

            // ── Per-frame layer: blit the ring, then the continuous effects ──
            canvas.DrawBitmap(_staticLayer, 0f, 0f, _blitPaint); // Src blend: replaces every pixel, no Clear needed
            canvas.Save();
            canvas.Scale(dpiScale);

            // Shimmer: rotating dashed arc on outer ring edge
            DrawShimmer(canvas, cx, cy, outerR, theme, rAlpha, scale);

            // Smooth volume animation: lerp displayed level toward polled level each frame.
            {
                float volDiff = _volumeLevel - _volumeDisplayLevel;
                _volumeDisplayLevel = MathF.Abs(volDiff) < 0.001f
                    ? _volumeLevel
                    : _volumeDisplayLevel + volDiff * 0.10f;
            }

            // Volume level arc: persistent thin arc just outside the ring showing current volume %
            DrawVolumeArc(canvas, cx, cy, AppConstants.RingOuterRadius * eased * scale, theme, rAlpha, scale);

            // Center circle + label
            string centerLabel;
            float  labelAlpha;
            if (childMenu != null && childHovered >= 0 && childHovered < childMenu.Items.Count)
            {
                centerLabel = childMenu.Items[childHovered].Label;
                labelAlpha  = 1f;
            }
            else if (hovered >= 0 && hovered < itemCount)
            {
                centerLabel = menu.Items[hovered].Label;
                labelAlpha  = 1f;
            }
            else if (childMenu != null && childParentIdx >= 0 && childParentIdx < itemCount)
            {
                centerLabel = menu.Items[childParentIdx].Label;
                labelAlpha  = 0.65f;
            }
            else
            {
                centerLabel = AppConstants.AppName;
                labelAlpha  = 0.4f;
            }

            // Show "Vol X%" for 2 s after a scroll-wheel volume action, overriding the normal label
            if (_clock.ElapsedMilliseconds < Interlocked.Read(ref _showVolumeLabelUntil))
            {
                centerLabel = $"Vol {(int)Math.Round(_volumeDisplayLevel * 100f)}%";
                labelAlpha  = 1f;
            }

            bool showBackArrow = hasParent || childMenu != null;
            DrawCenter(canvas, cx, cy, innerR, theme, rAlpha, scale, centerLabel, labelAlpha, showBackArrow);

            // Now-playing title + decorative visualizer, drawn below the ring
            var media = App.MediaInfo;
            if (media is not null)
            {
                float baseR = AppConstants.RingOuterRadius * scale;
                // Only show the title while media is actually playing (hide when paused/stopped).
                if (appearance.ShowNowPlaying && media.IsPlaying)
                {
                    string np = media.NowPlaying;
                    if (np.Length > 0)
                        DrawNowPlaying(canvas, cx, cy + baseR + 24f * scale, np, theme, rAlpha, scale);
                }
                if (appearance.ShowVisualizer && media.IsPlaying)
                    DrawVisualizer(canvas, cx, cy + baseR + 50f * scale, theme, rAlpha, scale, _volumeDisplayLevel);
            }

            canvas.Restore();
            FlushToWindow();
        }
        catch (Exception ex)
        {
            Logger.Error("RenderFrame error", ex);
            try
            {
                _bitmapCanvas?.Clear(SKColors.Transparent);
                FlushToWindow();
            }
            catch { /* ignore secondary failure */ }
        }
    }

    // ── Drawing helpers ───────────────────────────────────────────────────

    // Fills the given scratch path with a slice arc segment (no allocation).
    private static void BuildSlicePath(SKPath path, float cx, float cy,
        float outerR, float innerR, float start, float sweep)
    {
        path.Rewind();
        path.ArcTo(new SKRect(cx-outerR, cy-outerR, cx+outerR, cy+outerR), start, sweep, true);
        path.ArcTo(new SKRect(cx-innerR, cy-innerR, cx+innerR, cy+innerR), start+sweep, -sweep, false);
        path.Close();
    }

    private void DrawSliceGlow(SKCanvas canvas, float cx, float cy,
        float outerR, float innerR, float start, float sweep,
        AeroTheme theme, float alpha, float scale)
    {
        SKColor glow;
        if (theme.GlowColor.Length > 0)
            glow = theme.ToSKColor(theme.GlowColor);
        else
        {
            var ac = theme.ToSKColor(theme.AccentColor);
            glow = ac.WithAlpha(80);
        }
        glow = glow.WithAlpha((byte)(glow.Alpha * alpha));
        if (glow.Alpha < 4) return;

        BuildSlicePath(_path, cx, cy, outerR + 6f * scale, innerR - 4f * scale, start, sweep);
        _glowFill.Style      = SKPaintStyle.Fill;
        _glowFill.Color      = glow;
        _glowFill.MaskFilter = GetBlur(18f * scale);
        canvas.DrawPath(_path, _glowFill);
        _glowFill.MaskFilter = null;
    }

    private void DrawSlice(SKCanvas canvas, float cx, float cy,
        float outerR, float innerR, float start, float sweep,
        AeroTheme theme, bool hov, float alpha, float scale, bool empty = false, bool outerGlow = true)
    {
        BuildSlicePath(_path, cx, cy, outerR, innerR, start, sweep);

        // Resolve fill colors
        SKColor innerC, outerC;
        if (hov && !empty)
        {
            innerC = theme.SliceGradientInnerHover.Length > 0
                ? theme.ToSKColor(theme.SliceGradientInnerHover)
                : theme.ToSKColor(theme.SliceFillHover);
            outerC = theme.SliceGradientOuterHover.Length > 0
                ? theme.ToSKColor(theme.SliceGradientOuterHover)
                : theme.ToSKColor(theme.SliceFillHover);
        }
        else
        {
            innerC = theme.SliceGradientInner.Length > 0
                ? theme.ToSKColor(theme.SliceGradientInner)
                : theme.ToSKColor(theme.SliceFill);
            outerC = theme.SliceGradientOuter.Length > 0
                ? theme.ToSKColor(theme.SliceGradientOuter)
                : theme.ToSKColor(theme.SliceFill);
        }

        float emptyMul = empty ? 0.35f : 1f;
        byte  ia       = (byte)(innerC.Alpha * alpha * emptyMul);
        byte  oa       = (byte)(outerC.Alpha * alpha * emptyMul);

        // Radial gradient spanning the ring's own inner→outer radius.
        // All rings (L1, L2, L3) use this path — each gets a gradient matched to its own radii.
        // The shader is cached (keyed on radius + colors) so it isn't rebuilt every frame,
        // except while a ring is animating: then the radius differs every frame and a
        // throwaway shader is cheaper than churning the cache.
        float gradPos = (outerR > 0f ? innerR / outerR : 0f).Clamp(0f, 0.95f);
        _fill.Color = SKColors.White; // opaque: alpha is baked into the gradient stops, so a
                                      // leftover paint alpha (e.g. from DrawIndicatorDots) must
                                      // not attenuate the shader.
        if (_transientGradients)
        {
            using var sh = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), outerR,
                [innerC.WithAlpha(ia), outerC.WithAlpha(oa)], [gradPos, 1.0f], SKShaderTileMode.Clamp);
            _fill.Shader = sh;
            canvas.DrawPath(_path, _fill);
            _fill.Shader = null;
        }
        else
        {
            _fill.Shader = GetGradient(cx, cy, outerR, innerC.WithAlpha(ia), outerC.WithAlpha(oa), gradPos);
            canvas.DrawPath(_path, _fill);
            _fill.Shader = null;
        }

        // Border stroke — minimum 0.5px so arcs never collapse to sub-pixel jaggies
        _stroke.Color = theme.ToSKColor(hov && !empty ? theme.SliceStrokeHover : theme.SliceStroke)
                            .WithAlpha((byte)(255 * alpha * emptyMul));
        _stroke.StrokeWidth = Math.Max(theme.SliceStrokeWidth * scale, 0.5f);
        canvas.DrawPath(_path, _stroke);

        // Outer edge accent arc with glow on hovered slice
        if (hov && !empty)
        {
            var accent = theme.ToSKColor(theme.AccentColor);
            _arcPath.Rewind();
            _arcPath.ArcTo(
                new SKRect(cx-outerR+1.5f, cy-outerR+1.5f, cx+outerR-1.5f, cy+outerR-1.5f),
                start+2f, sweep-4f, true);

            _arcStroke.Style     = SKPaintStyle.Stroke;
            _arcStroke.StrokeCap = SKStrokeCap.Round;

            // Glow layer (skipped when a child ring is open — its blur would bleed outward)
            if (outerGlow)
            {
                _arcStroke.StrokeWidth = 4f * scale;
                _arcStroke.Color       = accent.WithAlpha((byte)(100 * alpha));
                _arcStroke.MaskFilter  = GetBlur(5f * scale);
                canvas.DrawPath(_arcPath, _arcStroke);
            }

            // Sharp edge layer
            _arcStroke.StrokeWidth = 2f * scale;
            _arcStroke.Color       = accent.WithAlpha((byte)(210 * alpha));
            _arcStroke.MaskFilter  = null;
            canvas.DrawPath(_arcPath, _arcStroke);
        }
    }

    // Volume arc — smooth arc sweeping clockwise 0-360° for 0-100% volume.
    // Flashes brighter when a scroll-wheel volume action fires.
    // Visibility is controlled by AppearanceConfig.VolumeRingVisibility.
    private void DrawVolumeArc(SKCanvas canvas, float cx, float cy,
        float outerR, AeroTheme theme, float animAlpha, float scale)
    {
        // Elapsed ms since the last scroll-wheel volume action
        long flashElapsed = _clock.ElapsedMilliseconds - Interlocked.Read(ref _volumeFlashAt);

        // Visibility gate — check before any paint work
        var visibility = App.Config.Current.Appearance.VolumeRingVisibility;
        if (visibility == VolumeRingVisibility.Hidden) return;
        if (visibility == VolumeRingVisibility.OnChange && flashElapsed > 1400) return;

        float vol  = _volumeDisplayLevel; // lerped for smooth animation
        float arcR = outerR + 4f * scale;

        // Flash boost: 0→1 for first 800ms after a scroll event, then 1→0 over next 600ms
        float flash = 0f;
        if (flashElapsed >= 0 && flashElapsed < 1400)
            flash = flashElapsed < 800
                ? flashElapsed / 800f
                : 1f - (flashElapsed - 800f) / 600f;

        // AccentColor @ 75% base; boosted to 100% during flash
        var   ac       = theme.ToSKColor(theme.AccentColor);
        float baseA    = 0.75f;
        float boostedA = baseA + (1f - baseA) * flash;
        var   arcColor = ac.WithAlpha((byte)(ac.Alpha * boostedA));

        // Stroke width from theme; pulses wider on flash
        float baseW  = theme.VolumeRingThickness * scale;
        float flashW = baseW + 1.4f * scale * flash;

        // Dim track ring (full 360°) — always drawn as a ghost behind the arc
        byte trackA = (byte)(ac.Alpha * 0.18f * animAlpha);
        if (trackA > 0)
        {
            _arcStroke.Style       = SKPaintStyle.Stroke;
            _arcStroke.StrokeCap   = SKStrokeCap.Butt;
            _arcStroke.StrokeWidth = baseW;
            _arcStroke.MaskFilter  = null;
            _arcStroke.Color       = arcColor.WithAlpha(trackA);
            canvas.DrawCircle(cx, cy, arcR, _arcStroke);
        }

        if (vol > 0.005f)
        {
            float sweepDeg = vol * 360f;
            _arcStroke.Style       = SKPaintStyle.Stroke;
            _arcStroke.StrokeCap   = SKStrokeCap.Round;
            _arcStroke.StrokeWidth = flashW;
            _arcStroke.MaskFilter  = null;
            _arcStroke.Color       = arcColor.WithAlpha((byte)(ac.Alpha * boostedA * animAlpha));
            _arcPath.Rewind();
            _arcPath.ArcTo(
                new SKRect(cx - arcR, cy - arcR, cx + arcR, cy + arcR),
                -90f, sweepDeg, true);
            canvas.DrawPath(_arcPath, _arcStroke);

            // Glow dot at arc tip
            float tipAngle = (-90f + sweepDeg).ToRadians();
            float tx = cx + MathF.Cos(tipAngle) * arcR;
            float ty = cy + MathF.Sin(tipAngle) * arcR;

            _glowFill.Style      = SKPaintStyle.Fill;
            _glowFill.Color      = arcColor.WithAlpha((byte)(ac.Alpha * (0.35f + 0.25f * flash) * animAlpha));
            _glowFill.MaskFilter = GetBlur((4f + flash * 3f) * scale);
            canvas.DrawCircle(tx, ty, (3.5f + flash * 1.5f) * scale, _glowFill);
            _glowFill.MaskFilter = null;

            // Solid tip dot (no blur) — reuse the shared fill paint
            _fill.Shader = null;
            _fill.Color  = arcColor.WithAlpha((byte)(ac.Alpha * boostedA * animAlpha));
            canvas.DrawCircle(tx, ty, (2f + flash * 0.8f) * scale, _fill);
        }
    }

    // Shimmer — slowly rotating dashed arc on the outer ring
    private void DrawShimmer(SKCanvas canvas, float cx, float cy,
        float outerR, AeroTheme theme, float alpha, float scale)
    {
        float rotDeg = (float)((_clock.ElapsedMilliseconds % (long)AppConstants.ShimmerPeriodMs)
            / AppConstants.ShimmerPeriodMs * 360.0);
        var accent = theme.ToSKColor(theme.AccentColor);

        _shimmerPaint.Style       = SKPaintStyle.Stroke;
        _shimmerPaint.StrokeWidth = 0.5f * scale;
        _shimmerPaint.Color       = accent.WithAlpha((byte)(22 * alpha));
        _shimmerPaint.PathEffect  = GetShimmerDash(scale);
        canvas.Save();
        canvas.RotateDegrees(rotDeg, cx, cy);
        canvas.DrawCircle(cx, cy, outerR, _shimmerPaint);
        canvas.Restore();
    }

    // Small accent dot on the outer rim for slices that have submenus
    private void DrawIndicatorDots(SKCanvas canvas, float cx, float cy,
        float outerR, RadialMenuConfig menu, int sliceCount,
        float startOff, float fullArc, int hoveredIndex,
        AeroTheme theme, float alpha, float scale)
    {
        float dotRadius = outerR + 6f * scale;
        var   accent    = theme.ToSKColor(theme.AccentColor);
        int   count     = Math.Min(sliceCount, menu.Items.Count);

        for (int i = 0; i < count; i++)
        {
            if (menu.Items[i].ActionType != ActionType.SubMenu) continue;
            float mid      = (startOff + i * fullArc + fullArc / 2f).ToRadians();
            float dotAlpha = i == hoveredIndex ? 1f : 0.45f;
            _fill.Color = accent.WithAlpha((byte)(255 * alpha * dotAlpha));
            canvas.DrawCircle(
                cx + MathF.Cos(mid) * dotRadius,
                cy + MathF.Sin(mid) * dotRadius,
                3f * scale, _fill);
        }
    }

    // Outer concentric ring drawn when a SubMenu slice is hovered
    private void DrawChildRing(SKCanvas canvas, float cx, float cy,
        float parentOuterR, RadialMenuConfig childMenu, int childHoveredIndex,
        AeroTheme theme, float alpha, float scale,
        float parentAngleDeg = -90f, float thickness = AppConstants.ChildRingThickness,
        bool partial = false, float animScale = 1f)
    {
        int count = childMenu.Items.Count;
        if (count == 0) return;

        // Scale all radii from center to reproduce the spring-out animation without canvas transforms.
        // animScale: 0.85 (start) → 1.0 (fully open), giving a natural pop-out effect.
        float naturalInnerR = parentOuterR + AppConstants.ChildRingGap * scale;
        float naturalOuterR = naturalInnerR + thickness * scale;
        float cInnerR = naturalInnerR * animScale;
        float cOuterR = naturalOuterR * animScale;

        var (startOff, segAngle, _) = GetArcLayout(count, parentAngleDeg, partial);
        float gap   = 2f;
        float sweep = segAngle - gap;
        float iconR = (cInnerR + cOuterR) / 2f;

        if (childHoveredIndex >= 0 && childHoveredIndex < count)
        {
            float glowStart = startOff + childHoveredIndex * segAngle + gap / 2f;
            DrawSliceGlow(canvas, cx, cy, cOuterR, cInnerR, glowStart, sweep, theme, alpha, scale);
        }

        for (int i = 0; i < count; i++)
        {
            bool  hov   = i == childHoveredIndex;
            float start = startOff + i * segAngle + gap / 2f;
            DrawSlice(canvas, cx, cy, cOuterR, cInnerR, start, sweep, theme, hov, alpha, scale, childMenu.Items[i].IsEmptySlot);
        }

        for (int i = 0; i < count; i++)
        {
            var   item = childMenu.Items[i];
            if (item.IsEmptySlot) continue;
            bool  hov  = i == childHoveredIndex;
            float mid  = startOff + i * segAngle + segAngle / 2f;
            float rad  = mid.ToRadians();
            DrawIcon(canvas,
                cx + MathF.Cos(rad) * iconR,
                cy + MathF.Sin(rad) * iconR,
                item, theme, hov, alpha, scale);
        }
    }

    // L3 ring — second concentric outer ring for 3-level menus
    private void DrawL3Ring(SKCanvas canvas, float cx, float cy,
        float parentOuterR, float l2Thickness,
        RadialMenuConfig l3Menu, int l3HoveredIndex, float l2ParentAngleDeg,
        AeroTheme theme, float alpha, float scale, bool partial, float animScale = 1f)
    {
        int count = l3Menu.Items.Count;
        if (count == 0) return;

        float naturalL3InnerR = parentOuterR
                              + AppConstants.ChildRingGap * scale
                              + l2Thickness * scale
                              + AppConstants.ChildRingGap * scale;
        float naturalL3OuterR = naturalL3InnerR + AppConstants.ChildRingThickness * scale;
        float l3InnerR = naturalL3InnerR * animScale;
        float l3OuterR = naturalL3OuterR * animScale;

        var (startOff, segAngle, _) = GetArcLayout(count, l2ParentAngleDeg, partial);
        float gap   = 2f;
        float sweep = segAngle - gap;
        float iconR = (l3InnerR + l3OuterR) / 2f;

        if (l3HoveredIndex >= 0 && l3HoveredIndex < count)
        {
            float glowStart = startOff + l3HoveredIndex * segAngle + gap / 2f;
            DrawSliceGlow(canvas, cx, cy, l3OuterR, l3InnerR, glowStart, sweep, theme, alpha, scale);
        }

        for (int i = 0; i < count; i++)
        {
            bool  hov   = i == l3HoveredIndex;
            float start = startOff + i * segAngle + gap / 2f;
            DrawSlice(canvas, cx, cy, l3OuterR, l3InnerR, start, sweep, theme, hov, alpha, scale, l3Menu.Items[i].IsEmptySlot);
        }

        for (int i = 0; i < count; i++)
        {
            var   item = l3Menu.Items[i];
            if (item.IsEmptySlot) continue;
            bool  hov  = i == l3HoveredIndex;
            float mid  = startOff + i * segAngle + segAngle / 2f;
            float rad  = mid.ToRadians();
            DrawIcon(canvas,
                cx + MathF.Cos(rad) * iconR,
                cy + MathF.Sin(rad) * iconR,
                item, theme, hov, alpha, scale * 0.88f);
        }
    }

    // GetArcLayout / HitTestArc live in AeroDial.Core.RingGeometry (shared with the ring editor).

    private void DrawScrollIndicator(SKCanvas canvas, float cx, float cy,
        float iconR, float angleDeg, AeroTheme theme, bool hov, float alpha, float scale)
    {
        float rad   = angleDeg.ToRadians();
        float ox    = cx + MathF.Cos(rad) * (iconR + 30f * scale);
        float oy    = cy + MathF.Sin(rad) * (iconR + 30f * scale);
        var   color = theme.ToSKColor(theme.AccentColor).WithAlpha((byte)(120 * alpha));
        _text.TextSize  = 9f * scale;
        _text.Typeface  = GetTypeface(theme.LabelFontFamily);
        _text.TextAlign = SKTextAlign.Center;
        _text.Color     = color;
        canvas.DrawText("↕", ox, oy + 3f * scale, _text);
    }

    private void DrawCenter(SKCanvas canvas, float cx, float cy,
        float r, AeroTheme theme, float alpha, float scale,
        string centerLabel, float labelAlpha, bool showBackArrow)
    {
        // Circle fill
        _fill.Color = theme.ToSKColor(theme.CenterFill).WithAlpha((byte)(255 * alpha));
        canvas.DrawCircle(cx, cy, r, _fill);
        _stroke.Color       = theme.ToSKColor(theme.CenterStroke).WithAlpha((byte)(255 * alpha));
        _stroke.StrokeWidth = 0.8f;
        canvas.DrawCircle(cx, cy, r, _stroke);

        float textY = cy;

        if (showBackArrow)
        {
            // Back arrow — left-pointing chevron, shifted above text
            float aw  = 6f * scale;
            float ah  = 4.5f * scale;
            float ay  = cy - 9f * scale;
            _arcPath.Rewind();
            _arcPath.MoveTo(cx + aw * 0.4f, ay - ah);
            _arcPath.LineTo(cx - aw * 0.5f, ay);
            _arcPath.LineTo(cx + aw * 0.4f, ay + ah);
            _arcStroke.Style       = SKPaintStyle.Stroke;
            _arcStroke.StrokeWidth = 2f * scale;
            _arcStroke.StrokeCap   = SKStrokeCap.Round;
            _arcStroke.StrokeJoin  = SKStrokeJoin.Round;
            _arcStroke.MaskFilter  = null;
            _arcStroke.Color       = theme.ToSKColor(theme.AccentColor).WithAlpha((byte)(210 * alpha));
            canvas.DrawPath(_arcPath, _arcStroke);
            textY = cy + 8f * scale;
        }

        // Center label (item name or "AeroDial") — up to two lines
        if (!string.IsNullOrEmpty(centerLabel))
        {
            _text.TextSize  = 11f * scale;
            _text.Typeface  = GetTypeface(theme.LabelFontFamily);
            _text.TextAlign = SKTextAlign.Center;
            _text.Color     = theme.ToSKColor(theme.LabelColor)
                                  .WithAlpha((byte)(255 * alpha * labelAlpha));

            var (line1, line2) = SplitCenterLabel(centerLabel);
            if (line2 is null)
            {
                canvas.DrawText(line1, cx, textY + 5f * scale, _text);
            }
            else
            {
                float lineH = 13f * scale;
                canvas.DrawText(line1, cx, textY - 1f * scale, _text);
                canvas.DrawText(line2, cx, textY + lineH - 1f * scale, _text);
            }
        }
        else if (!showBackArrow)
        {
            // Fallback: small accent dot when no label and no back arrow
            _fill.Color = theme.ToSKColor(theme.AccentColor).WithAlpha((byte)(130 * alpha));
            canvas.DrawCircle(cx, cy, 4f, _fill);
        }
    }

    private void DrawIcon(SKCanvas canvas, float x, float y,
        MenuItemConfig item, AeroTheme theme, bool hov, float alpha, float scale)
    {
        var bmp = IconRegistry.Get(item.Icon, theme.IconStrokeScale);
        if (bmp is null) return;
        float size = (hov ? 27f : 22f) * scale;
        var dest = new SKRect(x-size/2, y-size/2, x+size/2, y+size/2);
        // Built-in icons are drawn white, so Modulate with the theme tint recolors them.
        // Full-color exe/image icons must NOT be tinted (a dark tint in a light theme would
        // multiply them toward black) — use white so Modulate preserves their real colors
        // while still applying the ring fade via alpha.
        var tint = IconRegistry.IsBuiltIn(item.Icon)
            ? theme.ToSKColor(hov ? theme.IconTintHover : theme.IconTint)
            : SKColors.White;
        _iconPaint.ColorFilter = GetIconFilter(tint.WithAlpha((byte)(255 * alpha)));
        canvas.DrawBitmap(bmp, dest, _iconPaint);
    }

    // Now-playing media title, centered below the ring.
    private void DrawNowPlaying(SKCanvas canvas, float cx, float y, string text,
        AeroTheme theme, float alpha, float scale)
    {
        string t = text.Length > 40 ? text[..39] + "…" : text;
        _text.TextSize  = 11f * scale;
        _text.Typeface  = GetTypeface(theme.LabelFontFamily);
        _text.TextAlign = SKTextAlign.Center;
        _text.Color     = theme.ToSKColor(theme.LabelColor).WithAlpha((byte)(210 * alpha));
        canvas.DrawText(t, cx, y, _text);
    }

    // Small decorative audio visualizer — bars driven by the polled volume level + a per-bar
    // sine wave. Theme-accent colored, subtle. Not a real spectrum (no capture/FFT), ~free.
    private void DrawVisualizer(SKCanvas canvas, float cx, float baselineY,
        AeroTheme theme, float alpha, float scale, float level)
    {
        const int bars = 7;
        float bw    = 3f * scale;
        float gap   = 3f * scale;
        float maxH  = 15f * scale;
        float total = bars * bw + (bars - 1) * gap;
        float x0    = cx - total / 2f;
        long  t     = _clock.ElapsedMilliseconds;
        float lvl   = Math.Clamp(level, 0.06f, 1f);

        var accent = theme.ToSKColor(theme.AccentColor);
        _fill.Shader = null;
        _fill.Color  = accent.WithAlpha((byte)(190 * alpha));
        for (int i = 0; i < bars; i++)
        {
            float phase = t / 260f + i * 0.8f;
            float wave  = 0.30f + 0.70f * (0.5f + 0.5f * MathF.Sin(phase));
            float h     = MathF.Max(1.5f * scale, maxH * lvl * wave);
            float bx    = x0 + i * (bw + gap);
            canvas.DrawRoundRect(bx, baselineY - h, bw, h, bw * 0.4f, bw * 0.4f, _fill);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void CheckDwell(int hov, int childHov, int l3Hov)
    {
        if (App.Config.Current.Behavior.SelectionMode != SelectionMode.HoverDwell) return;

        long now = _clock.ElapsedMilliseconds;
        RadialMenuConfig? menu, childMenu, l3Menu;
        long dwell, hoverStart, childHoverStart, l3HoverStart;
        lock (_lock)
        {
            menu            = _menu;
            childMenu       = _childMenu;
            l3Menu          = _l3Menu;
            dwell           = App.Config.Current.Behavior.HoverDwellMs;
            hoverStart      = _hoverStart;
            childHoverStart = _childHoverStart;
            l3HoverStart    = _l3HoverStart;
        }

        // L3 dwell (highest priority)
        if (l3Hov >= 0 && l3Menu is not null && l3Hov < l3Menu.Items.Count)
        {
            if (now - l3HoverStart >= dwell)
            {
                Post(InputKind.L3Click, l3Hov);
                lock (_lock) { _l3HoverStart = long.MaxValue; }
            }
        }

        // L2 child ring dwell
        if (childHov >= 0 && childMenu is not null && childHov < childMenu.Items.Count
            && childMenu.Items[childHov].ActionType != ActionType.SubMenu)
        {
            if (now - childHoverStart >= dwell)
            {
                Post(InputKind.ChildClick, childHov);
                lock (_lock) { _childHoverStart = long.MaxValue; }
            }
        }

        // Main ring dwell — skip SubMenu items (child ring handles them on hover)
        if (hov >= 0 && menu is not null && hov < menu.Items.Count
            && menu.Items[hov].ActionType != ActionType.SubMenu)
        {
            if (now - hoverStart >= dwell)
            {
                Post(InputKind.Click, hov);
                lock (_lock) { _hoverStart = long.MaxValue; }
            }
        }
    }

    private SKTypeface GetTypeface(string family)
    {
        if (_cachedTypeface is null || _cachedFontFamily != family)
        {
            _cachedTypeface?.Dispose();
            _cachedTypeface   = SKTypeface.FromFamilyName(family);
            _cachedFontFamily = family;
        }
        return _cachedTypeface;
    }

    private static bool IsAnimEnabled()
    {
        var a = App.Config.Current.Appearance;
        if (!a.AnimationsEnabled) return false;
        if (a.RespectSystemAnimationSetting && !SystemParameters.MenuAnimation) return false;
        return true;
    }

    private static float GetDuration(AnimState s)
        => s == AnimState.Closing ? AppConstants.AnimCloseMs : AppConstants.AnimOpenMs;

    public void Dispose()
    {
        _renderRunning = false;
        Interlocked.Increment(ref _renderGeneration);
        _renderThread?.Join(200); // wait for render thread to finish its current frame

        // Drop references to cached effects before disposing paints, then dispose
        // the caches themselves (they own the native effect handles).
        _fill.Shader = null;
        _glowFill.MaskFilter = null;
        _arcStroke.MaskFilter = null;
        _shimmerPaint.PathEffect = null;
        _iconPaint.ColorFilter = null;

        _fill.Dispose(); _stroke.Dispose(); _text.Dispose();
        _glowFill.Dispose(); _arcStroke.Dispose(); _iconPaint.Dispose(); _shimmerPaint.Dispose();
        _blitPaint.Dispose();
        _path.Dispose(); _arcPath.Dispose();

        foreach (var m in _blurCache.Values) m.Dispose(); _blurCache.Clear();
        ClearGradientCache();
        foreach (var f in _iconFilterCache.Values) f.Dispose(); _iconFilterCache.Clear();
        _shimmerDash?.Dispose();
        _cachedTypeface?.Dispose();
        ReleaseDIBSection();
        _firstFrameDone.Dispose();
    }
}

internal enum AnimState { Hidden, Opening, Open, Closing }

internal static class SystemParameters
{
    // SystemParametersInfo is a user32 syscall; it was being issued every frame.
    // The setting only changes when the user edits Windows settings, so cache it briefly.
    private static bool _menuAnimation = true;
    private static long _menuAnimationStamp = long.MinValue / 2;
    private const  long CacheMs = 1000;

    public static bool MenuAnimation
    {
        get
        {
            long now = Environment.TickCount64;
            if (now - _menuAnimationStamp >= CacheMs)
            {
                bool r = true;
                SystemParametersInfo(0x1002, 0, ref r, 0);
                _menuAnimation = r;
                _menuAnimationStamp = now;
            }
            return _menuAnimation;
        }
    }
    [DllImport("user32.dll")]
    private static extern bool SystemParametersInfo(uint a, uint b, ref bool c, uint d);
}
