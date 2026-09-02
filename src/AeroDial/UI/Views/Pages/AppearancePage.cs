// AeroDial — AppearancePage.cs
// Split from SettingsPages.cs: one settings page per file.

using AeroDial.Config;
using AeroDial.Core;
using AeroDial.Overlay;
using AeroDial.Themes;
using AeroDial.UI.Views;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace AeroDial.UI.Views.Pages;

// ═══════════════════════════════════════════════════════════════════════════
// AppearancePage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class AppearancePage : Page
{
    private Slider       _scale = null!, _gap = null!, _opacity = null!, _detach = null!, _slices = null!;
    private ToggleSwitch _anim = null!, _sysAnim = null!, _nowPlaying = null!, _visualizer = null!;
    private ComboBox     _volVisibility = null!;
    private TextBlock    _saved = null!;
    private DispatcherTimer? _saveTimer;
    private SKXamlCanvas?    _previewCanvas;

    public AppearancePage() => Build();

    private static Grid TwoColumnGrid()
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4), ColumnSpacing = 16, RowSpacing = 4 };
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.RowDefinitions.Add(new RowDefinition());
        return g;
    }

    private void Build()
    {
        // Two-column layout: controls scroll on the left; ring preview stays pinned on the right.
        var outerGrid = new Grid();
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });

        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 16, 24) };
        var stack  = new StackPanel { Spacing = 6 };
        var cfg    = App.Config.Current.Appearance;

        stack.Children.Add(PageKit.PageHeader("Appearance"));

        // Slice count (3 to 12; the ring geometry is fully generic)
        stack.Children.Add(PageKit.SubHeader("Slices"));
        _slices = PageKit.MakeSlider("Slices per ring", 3, 12, 1, Math.Clamp(cfg.SliceCount, 3, 12));
        _slices.TickFrequency = 1;
        _slices.TickPlacement = Microsoft.UI.Xaml.Controls.Primitives.TickPlacement.BottomRight;
        _slices.SnapsTo       = Microsoft.UI.Xaml.Controls.Primitives.SliderSnapsTo.StepValues;
        // Same two-column grid as the ring sliders below, so this track shares their left edge
        // and length instead of floating in the middle of the page.
        var sliceGrid = TwoColumnGrid();
        Grid.SetColumn(_slices, 0); sliceGrid.Children.Add(_slices);
        stack.Children.Add(sliceGrid);

        // Sliders — compact 2-column grid
        stack.Children.Add(PageKit.SubHeader("Ring properties"));
        _scale   = PageKit.MakeSlider("Scale",           0.6, 1.6, 0.05, cfg.Scale);
        _gap     = PageKit.MakeSlider("Slice gap (°)",   0,   8,   0.5,  cfg.GapDegrees);
        _opacity = PageKit.MakeSlider("Ring opacity",    0.3, 1.0, 0.05, cfg.RingOpacity);
        _detach  = PageKit.MakeSlider("Center gap (px)", 0,   40,  1,    cfg.RingInnerDetach);
        var sliderGrid = TwoColumnGrid();
        sliderGrid.RowDefinitions.Add(new RowDefinition());
        sliderGrid.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(_scale,   0); Grid.SetColumn(_scale,   0); sliderGrid.Children.Add(_scale);
        Grid.SetRow(_gap,     0); Grid.SetColumn(_gap,     1); sliderGrid.Children.Add(_gap);
        Grid.SetRow(_opacity, 1); Grid.SetColumn(_opacity, 0); sliderGrid.Children.Add(_opacity);
        Grid.SetRow(_detach,  1); Grid.SetColumn(_detach,  1); sliderGrid.Children.Add(_detach);
        stack.Children.Add(sliderGrid);

        // Toggles
        stack.Children.Add(PageKit.SubHeader("Animations"));
        _anim = new ToggleSwitch
        {
            Header = "Enable animations", IsOn = cfg.AnimationsEnabled,
            OnContent = "On", OffContent = "Off: instant open/close",
        };
        _sysAnim = new ToggleSwitch
        {
            Header = "Respect Windows animation setting",
            IsOn   = cfg.RespectSystemAnimationSetting,
            OnContent  = "On: disables AeroDial animations if Windows animations are off",
            OffContent = "Off: always animate regardless of Windows setting",
        };
        stack.Children.Add(_anim);
        stack.Children.Add(_sysAnim);

        // Volume ring visibility
        stack.Children.Add(PageKit.SubHeader("Volume ring"));
        _volVisibility = new ComboBox
        {
            Width       = 280,
            ItemsSource = new[] { "Always visible", "Show on change only", "Hidden" },
            SelectedIndex = (int)cfg.VolumeRingVisibility,
        };
        stack.Children.Add(_volVisibility);

        // Media info
        stack.Children.Add(PageKit.SubHeader("Media"));
        _nowPlaying = new ToggleSwitch
        {
            Header = "Show now-playing title below the ring", IsOn = cfg.ShowNowPlaying,
            OnContent = "On", OffContent = "Off",
        };
        _visualizer = new ToggleSwitch
        {
            Header = "Show audio visualizer while media plays", IsOn = cfg.ShowVisualizer,
            OnContent = "On", OffContent = "Off",
        };
        stack.Children.Add(_nowPlaying);
        stack.Children.Add(_visualizer);

        // Auto-save: any change schedules a debounced save and refreshes the preview
        void WireChange() { ScheduleSave(); _previewCanvas?.Invalidate(); }
        foreach (var sl in new[] { _scale, _gap, _opacity, _detach })
            sl.ValueChanged += (_, _) => WireChange();
        _anim.Toggled    += (_, _) => WireChange();
        _sysAnim.Toggled += (_, _) => WireChange();
        _slices.ValueChanged += (_, _) => WireChange();
        _volVisibility.SelectionChanged += (_, _) => WireChange();
        _nowPlaying.Toggled += (_, _) => WireChange();
        _visualizer.Toggled += (_, _) => WireChange();

        _saved = PageKit.SavedBadge();
        _saved.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack;
        Grid.SetColumn(scroll, 0);
        outerGrid.Children.Add(scroll);

        // Right column: pinned preview — stays visible while the left column scrolls
        var rightPanel = new StackPanel { Margin = new Thickness(0, 28, 20, 24), Spacing = 4 };
        rightPanel.Children.Add(PageKit.SubHeader("Preview"));
        _previewCanvas = new SKXamlCanvas
        {
            Width  = 220, Height = 220,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _previewCanvas.PaintSurface += OnPreviewPaint;
        rightPanel.Children.Add(_previewCanvas);
        Grid.SetColumn(rightPanel, 1);
        outerGrid.Children.Add(rightPanel);

        Content = outerGrid;
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        await App.Config.UpdateAsync(cfg =>
        {
            cfg.Appearance.Scale                        = (float)_scale.Value;
            cfg.Appearance.GapDegrees                   = (float)_gap.Value;
            cfg.Appearance.SliceCount                   = Math.Clamp((int)Math.Round(_slices.Value), 3, 12);
            cfg.Appearance.RingOpacity                  = (float)_opacity.Value;
            cfg.Appearance.RingInnerDetach              = (float)_detach.Value;
            cfg.Appearance.AnimationsEnabled            = _anim.IsOn;
            cfg.Appearance.RespectSystemAnimationSetting = _sysAnim.IsOn;
            cfg.Appearance.VolumeRingVisibility         = (AeroDial.Config.VolumeRingVisibility)_volVisibility.SelectedIndex;
            cfg.Appearance.ShowNowPlaying               = _nowPlaying.IsOn;
            cfg.Appearance.ShowVisualizer               = _visualizer.IsOn;
        });
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }

    private void ScheduleSave()
    {
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Stop();
        _saveTimer.Tick -= OnSaveTimerTick;
        _saveTimer.Tick += OnSaveTimerTick;
        _saveTimer.Start();
    }

    private void OnSaveTimerTick(object? s, object e)
    {
        _saveTimer!.Stop();
        Save(null!, null!);
    }

    private void OnPreviewPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var theme      = App.Themes.ActiveTheme;
        float w = e.Info.Width, h = e.Info.Height;
        float cx = w / 2f, cy = h / 2f;
        float minDim   = Math.Min(w, h);
        float outerR   = minDim * 0.42f;
        float innerR   = minDim * 0.18f + (float)(_detach?.Value ?? 0) * 0.4f;

        int   sliceCount = Math.Clamp((int)Math.Round(_slices?.Value ?? 8), 3, 12);
        float gapDeg     = (float)(_gap?.Value ?? AppConstants.DefaultGapDegrees);
        float fullArc    = 360f / sliceCount;
        float sweep      = fullArc - gapDeg;
        float startOff   = -90f - fullArc / 2f;
        float ringOp     = (float)(_opacity?.Value ?? 1.0);

        using var bgPaint = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha(60) };
        canvas.DrawCircle(cx, cy, outerR + 6f, bgPaint);

        using var paint = new SKPaint { IsAntialias = true };
        for (int i = 0; i < sliceCount; i++)
        {
            float start = startOff + i * fullArc + gapDeg / 2f;
            bool  hover = i == 0;

            SKColor fill = hover
                ? (theme.SliceGradientOuterHover.Length > 0 ? theme.ToSKColor(theme.SliceGradientOuterHover) : theme.ToSKColor(theme.SliceFillHover))
                : (theme.SliceGradientOuter.Length     > 0 ? theme.ToSKColor(theme.SliceGradientOuter)      : theme.ToSKColor(theme.SliceFill));
            paint.Style = SKPaintStyle.Fill;
            paint.Color = fill.WithAlpha((byte)(fill.Alpha * ringOp));
            using (var path = PreviewSlicePath(cx, cy, outerR, innerR, start, sweep))
                canvas.DrawPath(path, paint);

            paint.Style       = SKPaintStyle.Stroke;
            paint.StrokeWidth = hover ? 2f : 0.8f;
            var strokeC = hover ? theme.ToSKColor(theme.SliceStrokeHover) : theme.ToSKColor(theme.SliceStroke);
            paint.Color = strokeC.WithAlpha((byte)(strokeC.Alpha * ringOp));
            using (var path = PreviewSlicePath(cx, cy, outerR, innerR, start, sweep))
                canvas.DrawPath(path, paint);
        }

        // Center
        paint.Style = SKPaintStyle.Fill;
        paint.Color = theme.ToSKColor(theme.CenterFill);
        canvas.DrawCircle(cx, cy, innerR, paint);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1f;
        paint.Color = theme.ToSKColor(theme.CenterStroke);
        canvas.DrawCircle(cx, cy, innerR, paint);
    }

    private static SKPath PreviewSlicePath(float cx, float cy,
        float outerR, float innerR, float start, float sweep)
    {
        var path = new SKPath();
        path.ArcTo(new SKRect(cx - outerR, cy - outerR, cx + outerR, cy + outerR), start, sweep, true);
        path.ArcTo(new SKRect(cx - innerR, cy - innerR, cx + innerR, cy + innerR), start + sweep, -sweep, false);
        path.Close();
        return path;
    }
}
