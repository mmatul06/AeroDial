// AeroDial — SettingsPages.cs
// Settings sub-pages. Clean design without over-explaining every option.

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
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AeroDial.UI.Views.Pages;

// ── Shared helpers ────────────────────────────────────────────────────────────

internal static class UI
{
    public static TextBlock PageHeader(string t) => new()
    {
        Text = t, FontSize = 22,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Margin = new Thickness(0, 0, 0, 16),
    };

    public static TextBlock SubHeader(string t) => new()
    {
        Text = t, FontSize = 13,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 130, 120, 200)),
        Margin = new Thickness(0, 12, 0, 4),
    };

    public static Border InfoCard(string t) => new()
    {
        Background   = new SolidColorBrush(ColorHelper.FromArgb(25, 100, 100, 200)),
        CornerRadius = new CornerRadius(8),
        Padding      = new Thickness(14, 10, 14, 10),
        Margin       = new Thickness(0, 0, 0, 8),
        Child        = new TextBlock
        {
            Text = t, FontSize = 13, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(200, 200, 200, 220)),
        }
    };

    public static TextBlock SavedBadge() => new()
    {
        Text = "✓  Saved", FontSize = 13,
        Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 100, 220, 130)),
        Visibility = Visibility.Collapsed,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static Button SaveButton() => new()
    {
        Content = "Save changes",
        Style   = (Style)Application.Current.Resources["AccentButtonStyle"],
    };

    public static Slider MakeSlider(string header, double min, double max, double step, double val)
        => new() { Header = header, Minimum = min, Maximum = max, StepFrequency = step, Value = val, Width = 340 };
}

// ═══════════════════════════════════════════════════════════════════════════
// TriggerPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class TriggerPage : Page
{
    private TextBlock     _keyDisplay  = null!;
    private Button        _recordBtn   = null!;
    private CheckBox      _ctrl = null!, _alt = null!, _shift = null!;
    private RadioButton   _holdRadio = null!, _toggleRadio = null!;
    private TextBlock     _saved       = null!;
    private bool          _recording;
    private int           _vk;
    private TextBox?      _captureBox;
    private DispatcherTimer? _pollTimer;
    private DispatcherTimer? _saveTimer;

    public TriggerPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(UI.PageHeader("Trigger"));

        // Key recorder
        stack.Children.Add(UI.SubHeader("Activation button"));
        _vk = App.Config.Current.Trigger.VirtualKey;
        _keyDisplay = new TextBlock
        {
            Text = VkName(_vk), FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 160, 140, 255)),
            Margin = new Thickness(0, 0, 0, 6),
        };
        stack.Children.Add(_keyDisplay);

        _recordBtn = new Button { Content = "Record key" };
        _recordBtn.Click += StartRecording;

        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };
        AddQuick(quickRow, "Middle mouse",  0x04);
        AddQuick(quickRow, "Mouse Button 4", 0x05);
        AddQuick(quickRow, "Mouse Button 5", 0x06);

        stack.Children.Add(_recordBtn);
        stack.Children.Add(quickRow);

        // Modifiers
        stack.Children.Add(UI.SubHeader("Required modifiers"));
        _ctrl  = new CheckBox { Content = "Ctrl",  IsChecked = App.Config.Current.Trigger.RequireCtrl  };
        _alt   = new CheckBox { Content = "Alt",   IsChecked = App.Config.Current.Trigger.RequireAlt   };
        _shift = new CheckBox { Content = "Shift", IsChecked = App.Config.Current.Trigger.RequireShift };
        var mods = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        mods.Children.Add(_ctrl); mods.Children.Add(_alt); mods.Children.Add(_shift);
        stack.Children.Add(mods);

        // Hold vs toggle
        stack.Children.Add(UI.SubHeader("Trigger mode"));
        stack.Children.Add(UI.InfoCard(
            "Hold: keep the trigger held, menu stays open while held, release to confirm.\n" +
            "Toggle: tap once to open, tap again to close, select items by clicking or hover dwell."));
        bool isHold = App.Config.Current.Trigger.HoldMode;
        _holdRadio   = new RadioButton { Content = "Hold mode",   GroupName = "TrigMode", IsChecked = isHold  };
        _toggleRadio = new RadioButton { Content = "Toggle mode", GroupName = "TrigMode", IsChecked = !isHold };
        stack.Children.Add(_holdRadio);
        stack.Children.Add(_toggleRadio);

        // Auto-save wiring: any control change schedules a debounced save
        _ctrl.Checked        += (_, _) => ScheduleSave();
        _ctrl.Unchecked      += (_, _) => ScheduleSave();
        _alt.Checked         += (_, _) => ScheduleSave();
        _alt.Unchecked       += (_, _) => ScheduleSave();
        _shift.Checked       += (_, _) => ScheduleSave();
        _shift.Unchecked     += (_, _) => ScheduleSave();
        _holdRadio.Checked   += (_, _) => ScheduleSave();
        _toggleRadio.Checked += (_, _) => ScheduleSave();

        _saved = UI.SavedBadge();
        _saved.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack; Content = scroll;
    }

    private void AddQuick(Panel p, string label, int vk)
    {
        var b = new Button { Content = label };
        b.Click += (_, _) => { _vk = vk; _keyDisplay.Text = VkName(vk); ScheduleSave(); };
        p.Children.Add(b);
    }

    private void StartRecording(object s, RoutedEventArgs e)
    {
        if (_recording) return;
        _recording = true;
        _recordBtn.Content = "Waiting...";
        _keyDisplay.Text   = "Press any key or button";

        // Invisible TextBox to capture keyboard input
        _captureBox = new TextBox { Opacity = 0, Width = 1, Height = 1, IsTabStop = true };
        _captureBox.KeyDown += (_, args) =>
        {
            int vk = (int)args.Key;
            if (vk is 0x10 or 0x11 or 0x12) return; // ignore bare modifier keys
            FinishRecording(vk);
            args.Handled = true;
        };
        if (Content is ScrollViewer sv && sv.Content is Panel pan)
            pan.Children.Add(_captureBox);
        _captureBox.Focus(FocusState.Programmatic);

        // Poll for keys that may not fire TextBox.KeyDown reliably.
        // ScrollViewer intercepts navigation keys (Page Up, arrows) for scrolling, so they
        // never reach the TextBox.KeyDown handler — GetAsyncKeyState catches them here instead.
        // Rising-edge detection: only triggers on press, not on hold.
        int[] pollVks =
        {
            0x04, 0x05, 0x06,           // Middle mouse, Mouse Button 4, Mouse Button 5
            0x21, 0x22, 0x23, 0x24,     // Page Up, Page Down, End, Home
            0x25, 0x26, 0x27, 0x28,     // Arrow Left, Up, Right, Down
            0x2C, 0x2D, 0x2E,           // Print Screen, Insert, Delete
        };
        bool[] prev = new bool[pollVks.Length];
        _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _pollTimer.Tick += (_, _) =>
        {
            if (!_recording) return;
            for (int i = 0; i < pollVks.Length; i++)
            {
                bool pressed = (AeroDial.Core.Win32.GetAsyncKeyState(pollVks[i]) & 0x8000) != 0;
                if (pressed && !prev[i]) { FinishRecording(pollVks[i]); return; }
                prev[i] = pressed;
            }
        };
        _pollTimer.Start();
    }

    private void FinishRecording(int vk)
    {
        _pollTimer?.Stop();
        _pollTimer = null;
        _vk = vk;
        _keyDisplay.Text   = VkName(vk);
        _recordBtn.Content = "Record key";
        _recording         = false;
        if (_captureBox?.Parent is Panel pp) pp.Children.Remove(_captureBox);
        _captureBox = null;
        ScheduleSave();
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        await App.Config.UpdateAsync(cfg =>
        {
            cfg.Trigger.VirtualKey   = _vk;
            cfg.Trigger.RequireCtrl  = _ctrl.IsChecked  == true;
            cfg.Trigger.RequireAlt   = _alt.IsChecked   == true;
            cfg.Trigger.RequireShift = _shift.IsChecked == true;
            cfg.Trigger.HoldMode     = _holdRadio.IsChecked == true;
        });
        App.Hooks.Stop(); App.Hooks.Start();
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

    private static string VkName(int vk) => vk switch
    {
        0x01 => "Left mouse",     0x02 => "Right mouse",
        0x04 => "Middle mouse",      0x05 => "Mouse Button 4",
        0x06 => "Mouse Button 5",      0x08 => "Backspace",
        0x09 => "Tab",            0x0D => "Enter",
        0x1B => "Escape",         0x20 => "Space",
        // Navigation cluster
        0x21 => "Page Up",        0x22 => "Page Down",
        0x23 => "End",            0x24 => "Home",
        0x25 => "Left",           0x26 => "Up",
        0x27 => "Right",          0x28 => "Down",
        0x2C => "Print Screen",   0x2D => "Insert",
        0x2E => "Delete",
        // Numpad
        0x60 => "Numpad 0",  0x61 => "Numpad 1",  0x62 => "Numpad 2",
        0x63 => "Numpad 3",  0x64 => "Numpad 4",  0x65 => "Numpad 5",
        0x66 => "Numpad 6",  0x67 => "Numpad 7",  0x68 => "Numpad 8",
        0x69 => "Numpad 9",  0x6A => "Num *",     0x6B => "Num +",
        0x6D => "Num -",     0x6E => "Num .",      0x6F => "Num /",
        // Function keys
        0x70 => "F1",  0x71 => "F2",  0x72 => "F3",  0x73 => "F4",
        0x74 => "F5",  0x75 => "F6",  0x76 => "F7",  0x77 => "F8",
        0x78 => "F9",  0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
        0x7C => "F13", 0x7D => "F14", 0x7E => "F15", 0x7F => "F16",
        // OEM punctuation (US layout)
        0xBA => ";",    0xBB => "=",   0xBC => ",",   0xBD => "-",
        0xBE => ".",    0xBF => "/",   0xC0 => "`",
        0xDB => "[",    0xDC => "\\",  0xDD => "]",   0xDE => "'",
        // Digits and letters
        >= 0x30 and <= 0x39 => $"{(char)vk}",
        >= 0x41 and <= 0x5A => $"{(char)vk}",
        _ => $"0x{vk:X2}",
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// AppearancePage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class AppearancePage : Page
{
    private Slider       _scale = null!, _gap = null!, _opacity = null!, _detach = null!;
    private ToggleSwitch _anim = null!, _sysAnim = null!, _nowPlaying = null!, _visualizer = null!;
    private RadioButton  _slice4 = null!, _slice6 = null!, _slice8 = null!, _slice10 = null!, _slice12 = null!;
    private ComboBox     _volVisibility = null!;
    private TextBlock    _saved = null!;
    private DispatcherTimer? _saveTimer;
    private SKXamlCanvas?    _previewCanvas;

    public AppearancePage() => Build();

    private void Build()
    {
        // Two-column layout: controls scroll on the left; ring preview stays pinned on the right.
        var outerGrid = new Grid();
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });

        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 16, 24) };
        var stack  = new StackPanel { Spacing = 6 };
        var cfg    = App.Config.Current.Appearance;

        stack.Children.Add(UI.PageHeader("Appearance"));

        // Slice count
        stack.Children.Add(UI.SubHeader("Slice count"));

        int curSlices = cfg.SliceCount;
        _slice4  = new RadioButton { Content = "4",  GroupName = "SliceCount", IsChecked = curSlices == 4  };
        _slice6  = new RadioButton { Content = "6",  GroupName = "SliceCount", IsChecked = curSlices == 6  };
        _slice8  = new RadioButton { Content = "8",  GroupName = "SliceCount", IsChecked = curSlices == 8  };
        _slice10 = new RadioButton { Content = "10", GroupName = "SliceCount", IsChecked = curSlices == 10 };
        _slice12 = new RadioButton { Content = "12", GroupName = "SliceCount",
            IsChecked = curSlices != 4 && curSlices != 6 && curSlices != 8 && curSlices != 10 };
        var sliceRow = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        sliceRow.Children.Add(_slice4);
        sliceRow.Children.Add(_slice6);
        sliceRow.Children.Add(_slice8);
        sliceRow.Children.Add(_slice10);
        sliceRow.Children.Add(_slice12);
        stack.Children.Add(sliceRow);

        // Sliders — compact 2-column grid
        stack.Children.Add(UI.SubHeader("Ring properties"));
        _scale   = UI.MakeSlider("Scale",           0.6, 1.6, 0.05, cfg.Scale);
        _gap     = UI.MakeSlider("Slice gap (°)",   0,   8,   0.5,  cfg.GapDegrees);
        _opacity = UI.MakeSlider("Ring opacity",    0.3, 1.0, 0.05, cfg.RingOpacity);
        _detach  = UI.MakeSlider("Center gap (px)", 0,   40,  1,    cfg.RingInnerDetach);
        foreach (var sl in new[] { _scale, _gap, _opacity, _detach })
        {
            sl.Width  = double.NaN;
            sl.Margin = new Thickness(0, 0, 0, 8);
            sl.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        var sliderGrid = new Grid { Margin = new Thickness(0, 4, 0, 4), ColumnSpacing = 16, RowSpacing = 4 };
        sliderGrid.ColumnDefinitions.Add(new ColumnDefinition());
        sliderGrid.ColumnDefinitions.Add(new ColumnDefinition());
        sliderGrid.RowDefinitions.Add(new RowDefinition());
        sliderGrid.RowDefinitions.Add(new RowDefinition());
        Grid.SetRow(_scale,   0); Grid.SetColumn(_scale,   0); sliderGrid.Children.Add(_scale);
        Grid.SetRow(_gap,     0); Grid.SetColumn(_gap,     1); sliderGrid.Children.Add(_gap);
        Grid.SetRow(_opacity, 1); Grid.SetColumn(_opacity, 0); sliderGrid.Children.Add(_opacity);
        Grid.SetRow(_detach,  1); Grid.SetColumn(_detach,  1); sliderGrid.Children.Add(_detach);
        stack.Children.Add(sliderGrid);

        // Toggles
        stack.Children.Add(UI.SubHeader("Animations"));
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
        stack.Children.Add(UI.SubHeader("Volume ring"));
        _volVisibility = new ComboBox
        {
            Width       = 280,
            ItemsSource = new[] { "Always visible", "Show on change only", "Hidden" },
            SelectedIndex = (int)cfg.VolumeRingVisibility,
        };
        stack.Children.Add(_volVisibility);

        // Media info
        stack.Children.Add(UI.SubHeader("Media"));
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
        _slice4.Checked              += (_, _) => WireChange();
        _slice6.Checked              += (_, _) => WireChange();
        _slice8.Checked              += (_, _) => WireChange();
        _slice10.Checked             += (_, _) => WireChange();
        _slice12.Checked             += (_, _) => WireChange();
        _volVisibility.SelectionChanged += (_, _) => WireChange();
        _nowPlaying.Toggled += (_, _) => WireChange();
        _visualizer.Toggled += (_, _) => WireChange();

        _saved = UI.SavedBadge();
        _saved.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack;
        Grid.SetColumn(scroll, 0);
        outerGrid.Children.Add(scroll);

        // Right column: pinned preview — stays visible while the left column scrolls
        var rightPanel = new StackPanel { Margin = new Thickness(0, 28, 20, 24), Spacing = 4 };
        rightPanel.Children.Add(UI.SubHeader("Preview"));
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
            cfg.Appearance.SliceCount                   = _slice4.IsChecked  == true ? 4
                                                        : _slice6.IsChecked  == true ? 6
                                                        : _slice8.IsChecked  == true ? 8
                                                        : _slice10.IsChecked == true ? 10 : 12;
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

        int   sliceCount = _slice4?.IsChecked  == true ? 4
                         : _slice6?.IsChecked  == true ? 6
                         : _slice8?.IsChecked  == true ? 8
                         : _slice10?.IsChecked == true ? 10 : 12;
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

// ═══════════════════════════════════════════════════════════════════════════
// BehaviorPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class BehaviorPage : Page
{
    // Hold-mode controls
    private ToggleSwitch? _launch;  // only created in Hold mode

    // Toggle-mode controls
    private ComboBox     _mode  = null!;
    private Slider       _dwell = null!;
    private StackPanel   _dwellRow = null!;

    // Always-visible controls
    private ToggleSwitch _close = null!, _startup = null!, _closeOutside = null!;
    private TextBlock    _saved = null!;
    private DispatcherTimer? _saveTimer;

    public BehaviorPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };
        var cfg    = App.Config.Current.Behavior;
        bool holdMode = App.Config.Current.Trigger.HoldMode;

        stack.Children.Add(UI.PageHeader("Behavior"));

        // ── Item selection ────────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Item selection"));

        if (holdMode)
        {
            // Hold mode: trigger release is the primary mechanism, but dwell is also available.
            _launch = new ToggleSwitch
            {
                Header     = "Execute highlighted item on trigger release",
                IsOn       = cfg.LaunchOnRelease,
                OnContent  = "On: release trigger to confirm",
                OffContent = "Off",
            };
            stack.Children.Add(_launch);
            _launch.Toggled += (_, _) => ScheduleSave();

            _mode = new ComboBox
            {
                Width       = 300,
                ItemsSource = new[]
                {
                    "Click: left-click an item",
                    "Hover dwell: auto-executes after a delay",
                },
                SelectedIndex = cfg.SelectionMode == AeroDial.Config.SelectionMode.HoverDwell ? 1 : 0,
            };
            stack.Children.Add(_mode);

            _dwellRow = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            _dwell    = UI.MakeSlider("Hover dwell delay (ms)", 100, 1500, 50, cfg.HoverDwellMs);
            _dwellRow.Children.Add(_dwell);
            stack.Children.Add(_dwellRow);
            _dwellRow.Visibility = cfg.SelectionMode == AeroDial.Config.SelectionMode.HoverDwell
                ? Visibility.Visible : Visibility.Collapsed;

            _mode.SelectionChanged += (_, _) =>
                _dwellRow.Visibility = _mode.SelectedIndex == 1
                    ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            // Toggle mode: trigger only opens/closes; user picks how items are selected.
            stack.Children.Add(UI.InfoCard(
                "In Toggle mode, items are selected by the method below. Not by the trigger button."));

            _mode = new ComboBox
            {
                Width       = 300,
                ItemsSource = new[]
                {
                    "Hover: auto-executes after a delay",
                    "Click: left-click an item",
                    "Flick: aim cursor direction, tap trigger to confirm",
                },
                SelectedIndex = (int)cfg.SelectionMode,
            };
            stack.Children.Add(_mode);

            _dwellRow = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };
            _dwell = UI.MakeSlider("Hover dwell delay (ms)", 100, 1500, 50, cfg.HoverDwellMs);
            _dwellRow.Children.Add(_dwell);
            stack.Children.Add(_dwellRow);
            _dwellRow.Visibility = cfg.SelectionMode == AeroDial.Config.SelectionMode.HoverDwell
                ? Visibility.Visible : Visibility.Collapsed;

            _mode.SelectionChanged += (_, _) =>
                _dwellRow.Visibility = _mode.SelectedIndex == 0
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── After executing ───────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("After executing"));
        _close = new ToggleSwitch
        {
            Header    = "Close menu after executing an action",
            IsOn      = cfg.CloseOnActionExecuted,
            OnContent = "On", OffContent = "Off: menu stays open",
        };
        stack.Children.Add(_close);

        _closeOutside = new ToggleSwitch
        {
            Header     = "Close menu when clicking outside",
            IsOn       = cfg.CloseOnClickOutside,
            OnContent  = "On", OffContent = "Off",
        };
        stack.Children.Add(_closeOutside);

        // ── System ────────────────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("System"));
        _startup = new ToggleSwitch
        {
            Header    = "Start AeroDial with Windows",
            IsOn      = cfg.StartWithWindows,
            OnContent = "On", OffContent = "Off",
        };
        stack.Children.Add(_startup);

        // ── Reset ─────────────────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Reset"));
        stack.Children.Add(UI.InfoCard(
            "Restores all trigger, appearance, and behavior settings to factory defaults. " +
            "Menus are not changed. Reopen Settings to see the updated values."));
        var resetBtn = new Button
        {
            Content    = "Restore all settings to default",
            Style      = (Style)Application.Current.Resources["AccentButtonStyle"],
            Margin     = new Thickness(0, 4, 0, 0),
        };
        var resetStatus = new TextBlock
        {
            Text       = "Settings restored. Reopen Settings to see updated values.",
            FontSize   = 12,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 100, 220, 130)),
            Visibility = Visibility.Collapsed,
            Margin     = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        resetBtn.Click += async (_, _) =>
        {
            var dlg = new ContentDialog
            {
                Title             = "Restore defaults",
                Content           = "This will reset all trigger, appearance, and behavior settings to factory defaults. Your menus will not be changed. Continue?",
                PrimaryButtonText = "Restore",
                CloseButtonText   = "Cancel",
                XamlRoot          = XamlRoot,
            };
            var result = await dlg.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            await App.Config.UpdateAsync(cfg =>
            {
                var d = new AeroDialConfig();
                cfg.Trigger    = d.Trigger;
                cfg.Appearance = d.Appearance;
                cfg.Behavior   = d.Behavior;
            });
            resetStatus.Visibility = Visibility.Visible;
            resetBtn.IsEnabled = false;
        };
        stack.Children.Add(resetBtn);
        stack.Children.Add(resetStatus);

        // Auto-save wiring
        _mode.SelectionChanged  += (_, _) => ScheduleSave();
        _dwell.ValueChanged     += (_, _) => ScheduleSave();
        _close.Toggled          += (_, _) => ScheduleSave();
        _closeOutside.Toggled   += (_, _) => ScheduleSave();
        _startup.Toggled        += (_, _) => ScheduleSave();

        _saved = UI.SavedBadge();
        _saved.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack; Content = scroll;
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        bool holdMode = App.Config.Current.Trigger.HoldMode;
        await App.Config.UpdateAsync(cfg =>
        {
            if (holdMode)
            {
                cfg.Behavior.LaunchOnRelease = _launch!.IsOn;
                cfg.Behavior.HoverDwellMs    = (int)_dwell.Value;
                // 0=Click, 1=HoverDwell
                cfg.Behavior.SelectionMode   = _mode.SelectedIndex == 1
                    ? AeroDial.Config.SelectionMode.HoverDwell
                    : AeroDial.Config.SelectionMode.Click;
            }
            else
            {
                // In Toggle mode, release-execute doesn't apply.
                cfg.Behavior.SelectionMode   = (AeroDial.Config.SelectionMode)_mode.SelectedIndex;
                cfg.Behavior.HoverDwellMs    = (int)_dwell.Value;
                cfg.Behavior.LaunchOnRelease = false;
            }
            cfg.Behavior.CloseOnActionExecuted = _close.IsOn;
            cfg.Behavior.CloseOnClickOutside   = _closeOutside.IsOn;
            cfg.Behavior.StartWithWindows      = _startup.IsOn;
        });
        ApplyStartup(App.Config.Current.Behavior.StartWithWindows);
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }

    private static void ApplyStartup(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enable)
                key?.SetValue(AppConstants.AppName,
                    $"\"{System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName}\"");
            else
                key?.DeleteValue(AppConstants.AppName, throwOnMissingValue: false);
        }
        catch (Exception ex) { Logger.Warn("Could not apply startup setting", ex); }
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

}

// ═══════════════════════════════════════════════════════════════════════════
// MenusPage — full menu editor
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class MenusPage : Page
{
    // Working copy of menus — not written to config until Save is clicked
    private List<RadialMenuConfig> _menus   = [];
    private int                    _menuIdx = -1;
    private int                    _itemIdx = -1;
    private bool                   _rebuildingList;

    private RadialMenuConfig? Live => _menuIdx >= 0 && _menuIdx < _menus.Count ? _menus[_menuIdx] : null;
    private MenuItemConfig?   Cur  => Live is not null && _itemIdx >= 0 && _itemIdx < Live.Items.Count
                                      ? Live.Items[_itemIdx] : null;

    // Left panel
    private ComboBox    _menuCombo  = null!;
    private SKXamlCanvas _ringCanvas = null!;
    private TextBlock   _saved      = null!;

    // Ring drag-to-move state
    private int _dragSrcSlice   = -1;
    private int _dragHoverSlice = -1;
    private bool _dragging;
    private Windows.Foundation.Point _dragStart;

    // Editor card
    private Border        _editorCard  = null!;
    private TextBox       _labelBox    = null!, _iconBox    = null!;
    private ComboBox      _actionCombo = null!, _iconCombo  = null!;
    private SKXamlCanvas? _iconPreview;
    private bool          _recordingCombo;
    private TextBox?      _comboCaptureBox;
    private CheckBox?     _comboCtrl, _comboAlt, _comboShift, _comboWin;

    // Payload panes (shown/hidden based on selected ActionType)
    private StackPanel _appPane    = null!, _urlPane   = null!, _comboPane  = null!,
                       _mediaPane  = null!, _subPane   = null!, _scriptPane = null!,
                       _clipPane   = null!, _macroPane = null!;
    private TextBox    _appPath    = null!, _appArgs   = null!, _urlBox     = null!,
                       _comboBox   = null!, _scriptBox = null!, _clipBox    = null!;
    private ComboBox   _mediaCombo = null!, _subMenuSel = null!;

    // Macro editor (step list) working state for the currently-edited item
    private StackPanel      _macroRows  = null!;
    private List<MacroStep> _macroSteps = [];

    // Unsaved-changes bar (visibility is the source of truth)
    private Border _dirtyBar = null!;

    // Drill-in breadcrumb: menu ids from root of the current drill path (last = current)
    private readonly List<string> _crumb = [];
    private StackPanel _crumbBar = null!;
    private Border     _profileBadge = null!;

    private static readonly string[] EditableActionTypes =
        ["None", "LaunchApp", "OpenUrl", "KeyCombo", "Macro", "Media", "RunScript", "PasteClipboard", "SubMenu", "OpenSettings"];

    private static readonly string[] MacroStepNames = ["Type text", "Press key", "Key down", "Key up", "Delay"];

    public MenusPage()
    {
        // Deep-clone so edits don't touch the live config until Save
        var json = System.Text.Json.JsonSerializer.Serialize(App.Config.Current.Menus);
        _menus   = System.Text.Json.JsonSerializer.Deserialize<List<RadialMenuConfig>>(json) ?? [];
        Build();
    }

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(28, 20, 28, 20) };
        var root   = new StackPanel { Spacing = 0 };

        root.Children.Add(UI.PageHeader("Menus"));

        // ── Menu picker + management buttons ──────────────────────────────
        root.Children.Add(UI.SubHeader("Active menu"));
        var menuRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        _menuCombo = new ComboBox { Width = 200 };
        PopulateMenuCombo();
        _menuCombo.SelectionChanged += (_, _) =>
        {
            if (!_rebuildingList) SelectMenu(_menuCombo.SelectedIndex);
        };

        Button Btn(string txt, RoutedEventHandler h) { var b = new Button { Content = txt }; b.Click += h; return b; }
        var deleteMenuBtn = new Button { Content = "Delete" };
        deleteMenuBtn.Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 90, 90));
        deleteMenuBtn.Click += (_, _) => DeleteMenuAsync().FireAndForget();

        menuRow.Children.Add(_menuCombo);
        menuRow.Children.Add(Btn("+ New",       AddMenu));
        menuRow.Children.Add(Btn("Rename",      (s, e) => RenameMenuAsync().FireAndForget()));
        menuRow.Children.Add(deleteMenuBtn);
        menuRow.Children.Add(Btn("Presets…",    (s, e) => LoadPresetAsync().FireAndForget()));
        menuRow.Children.Add(Btn("Save preset", (s, e) => SaveAsPresetAsync().FireAndForget()));
        root.Children.Add(menuRow);

        // ── Breadcrumb (drill path) + profile-binding badge ──────────────
        var crumbBadgeRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        crumbBadgeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        crumbBadgeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _crumbBar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _profileBadge = new Border
        {
            Background        = new SolidColorBrush(ColorHelper.FromArgb(40, 120, 110, 200)),
            CornerRadius      = new CornerRadius(10),
            Padding           = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "Not bound to any app", FontSize = 12,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(230, 200, 200, 230)),
            },
        };
        ToolTipService.SetToolTip(_profileBadge, "Manage bindings on the App Profiles page");
        Grid.SetColumn(_crumbBar, 0);
        Grid.SetColumn(_profileBadge, 1);
        crumbBadgeRow.Children.Add(_crumbBar);
        crumbBadgeRow.Children.Add(_profileBadge);
        root.Children.Add(crumbBadgeRow);

        // ── Two-column layout: ring + item list (left) | item editor (right) ──
        var twoCol = new Grid();
        twoCol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });
        twoCol.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Left column — the ring IS the item editor (click to edit, click + to add, drag to move)
        var leftCol = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 14, 0) };

        _ringCanvas = new SKXamlCanvas
        {
            Width  = 240, Height = 240,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        _ringCanvas.PaintSurface    += OnRingPaint;
        _ringCanvas.PointerPressed  += OnRingPointerPressed;
        _ringCanvas.PointerMoved    += OnRingPointerMoved;
        _ringCanvas.PointerReleased += OnRingPointerReleased;
        leftCol.Children.Add(_ringCanvas);

        leftCol.Children.Add(new TextBlock
        {
            Text = "Click a slice to edit. Click a + slot to add. Drag a slice to move or swap.",
            FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 200, 200, 220)),
            Margin = new Thickness(0, 2, 0, 0),
        });

        Grid.SetColumn(leftCol, 0);
        twoCol.Children.Add(leftCol);

        // Right column — editor card
        _editorCard = BuildEditorCard();
        Grid.SetColumn(_editorCard, 1);
        twoCol.Children.Add(_editorCard);

        root.Children.Add(twoCol);

        // ── Save row / dirty bar ──────────────────────────────────────────
        var saveBtn = UI.SaveButton(); _saved = UI.SavedBadge();
        saveBtn.Click += Save;
        var discardBtn = new Button { Content = "Discard changes" };
        discardBtn.Click += Discard;
        var warn = new TextBlock
        {
            Text = "Unsaved changes", FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 240, 190, 90)),
        };
        var dirtyInner = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        dirtyInner.Children.Add(warn);
        dirtyInner.Children.Add(discardBtn);
        _dirtyBar = new Border { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center, Child = dirtyInner };

        var saveRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 14, 0, 0) };
        saveRow.Children.Add(saveBtn);
        saveRow.Children.Add(_dirtyBar);
        saveRow.Children.Add(_saved);
        root.Children.Add(saveRow);

        scroll.Content = root;
        Content = scroll;

        if (_menus.Count > 0) { _menuCombo.SelectedIndex = 0; SelectMenu(0); }
    }

    private Border BuildEditorCard()
    {
        var card = new Border
        {
            Background   = new SolidColorBrush(ColorHelper.FromArgb(20, 100, 100, 200)),
            CornerRadius = new CornerRadius(10),
            Padding      = new Thickness(16, 14, 16, 14),
            Visibility   = Visibility.Collapsed,
        };
        var s = new StackPanel { Spacing = 6 };

        var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerText = new TextBlock
        {
            Text = "Edit item", FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var removeBtn = new Button
        {
            Content    = "Remove",
            Padding    = new Thickness(8, 4, 8, 4),
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 255, 120, 120)),
        };
        removeBtn.Click += RemoveItem;
        Grid.SetColumn(headerText, 0);
        Grid.SetColumn(removeBtn, 1);
        headerRow.Children.Add(headerText);
        headerRow.Children.Add(removeBtn);
        s.Children.Add(headerRow);

        // Label
        s.Children.Add(new TextBlock { Text = "Label", FontSize = 12 });
        _labelBox = new TextBox { PlaceholderText = "Item label" };
        s.Children.Add(_labelBox);

        // Icon
        s.Children.Add(new TextBlock { Text = "Icon", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        var iconRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0, 0, 0, 2) };

        // Small canvas previewing the currently selected icon
        _iconPreview = new SKXamlCanvas { Width = 40, Height = 40, VerticalAlignment = VerticalAlignment.Center };
        _iconPreview.PaintSurface += OnIconPreviewPaint;

        _iconBox = new TextBox { PlaceholderText = "icon name or file path", Width = 140 };
        _iconBox.TextChanged += (_, _) => _iconPreview?.Invalidate();

        // Keep _iconCombo initialised (used in LoadEditor) but don't add it to the visual tree
        _iconCombo = new ComboBox();
        foreach (var n in new[]
        {
            "media","apps","vol_up","vol_down","mute","play","settings","desktop",
            "next","prev","url","script","clipboard","default",
            "power","lock","folder","copy","paste","home","search","mic",
            "close","camera","keyboard","refresh","send","star",
            "pause","stop","back","forward","minimize","zoom_in","zoom_out",
            "trash","edit","download","upload","check","plus","minus",
            "tag","share","list","info","wifi","bluetooth","brightness",
            "clock","alarm","calendar","sleep","screenshot",
        })
            _iconCombo.Items.Add(n);

        // "Built-in icons…" button — opens a flyout with a single SKXamlCanvas rendering all icons.
        // One canvas per flyout open, drawing a 6-column grid of icon tiles with hover highlight.
        string[] allIcons =
        [
            "media","apps","vol_up","vol_down","mute","play","settings","desktop",
            "next","prev","url","script","clipboard","default",
            "power","lock","folder","copy","paste","home","search","mic",
            "close","camera","keyboard","refresh","send","star",
            "pause","stop","back","forward","minimize","zoom_in","zoom_out",
            "trash","edit","download","upload","check","plus","minus",
            "tag","share","list","info","wifi","bluetooth","brightness",
            "clock","alarm","calendar","sleep","screenshot",
        ];
        const int iconCols   = 6;
        const int iconCellPx = 48;  // logical pixels per icon cell
        int iconRows = (allIcons.Length + iconCols - 1) / iconCols;
        int gridW    = iconCols * iconCellPx;
        int gridH    = iconRows * iconCellPx;

        Flyout? iconPickerFlyout = null;
        var pickBtn = new Button { Content = "Built-in…", Padding = new Thickness(8, 4, 8, 4) };
        pickBtn.Click += (_, _) =>
        {
            if (iconPickerFlyout is null)
            {
                // Single canvas renders the entire icon grid; hover and click handled via pointer events.
                int hoveredCell = -1;
                var iconCanvas  = new SKXamlCanvas { Width = gridW, Height = gridH };

                void Repaint() => iconCanvas.Invalidate();

                iconCanvas.PaintSurface += (_, pe) =>
                {
                    var c = pe.Surface.Canvas;
                    c.Clear(new SKColor(28, 28, 40, 255));

                    var theme = App.Themes.ActiveTheme;
                    var tint  = theme.ToSKColor(theme.IconTint);
                    float dpi = (float)(pe.Info.Width / gridW);

                    for (int idx = 0; idx < allIcons.Length; idx++)
                    {
                        int col = idx % iconCols, row = idx / iconCols;
                        float x = col * iconCellPx * dpi, y = row * iconCellPx * dpi;
                        float cellPx = iconCellPx * dpi;

                        // Hover highlight
                        if (idx == hoveredCell)
                        {
                            using var hlPaint = new SKPaint { Color = new SKColor(100, 90, 200, 60) };
                            c.DrawRect(x, y, cellPx, cellPx, hlPaint);
                        }

                        var bmp = IconRegistry.Get(allIcons[idx]);
                        if (bmp is null) continue;

                        float pad  = cellPx * 0.18f;
                        var   dest = new SKRect(x + pad, y + pad, x + cellPx - pad, y + cellPx - pad);
                        using var ip = new SKPaint
                        {
                            IsAntialias = true,
                            FilterQuality = SKFilterQuality.High,
                            ColorFilter = SKColorFilter.CreateBlendMode(
                                tint.WithAlpha((byte)(idx == hoveredCell ? 255 : 200)), SKBlendMode.Modulate),
                        };
                        c.DrawBitmap(bmp, dest, ip);
                    }
                };

                iconCanvas.PointerMoved += (_, me) =>
                {
                    var pt  = me.GetCurrentPoint(iconCanvas).Position;
                    int col = (int)(pt.X / iconCellPx);
                    int row = (int)(pt.Y / iconCellPx);
                    int idx = row * iconCols + col;
                    int newHov = (idx >= 0 && idx < allIcons.Length && col < iconCols) ? idx : -1;
                    if (newHov != hoveredCell) { hoveredCell = newHov; Repaint(); }
                };

                iconCanvas.PointerExited += (_, _) => { hoveredCell = -1; Repaint(); };

                iconCanvas.PointerReleased += (_, me) =>
                {
                    var pt  = me.GetCurrentPoint(iconCanvas).Position;
                    int col = (int)(pt.X / iconCellPx);
                    int row = (int)(pt.Y / iconCellPx);
                    int idx = row * iconCols + col;
                    if (idx >= 0 && idx < allIcons.Length && col < iconCols)
                    {
                        _iconBox.Text = allIcons[idx];
                        iconPickerFlyout?.Hide();
                    }
                };

                iconPickerFlyout = new Flyout
                {
                    Content = new ScrollViewer
                    {
                        Content = iconCanvas,
                        MaxHeight = 320,
                        VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    },
                    ShouldConstrainToRootBounds = false,
                    Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom,
                };
            }
            iconPickerFlyout.ShowAt(pickBtn);
        };

        var browseBtn = new Button { Content = "Browse…", Padding = new Thickness(8, 4, 8, 4) };
        browseBtn.Click += BrowseIconAsync;

        iconRow.Children.Add(_iconPreview);
        iconRow.Children.Add(_iconBox);
        iconRow.Children.Add(pickBtn);
        iconRow.Children.Add(browseBtn);
        s.Children.Add(iconRow);

        // Action type
        s.Children.Add(new TextBlock { Text = "Action type", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        _actionCombo = new ComboBox { Width = 260 };
        foreach (var n in EditableActionTypes) _actionCombo.Items.Add(n);
        _actionCombo.SelectionChanged += OnActionTypeChanged;
        s.Children.Add(_actionCombo);

        // ── Payload panes ─────────────────────────────────────────────────
        var payloads = new StackPanel { Spacing = 4, Margin = new Thickness(0, 4, 0, 0) };

        StackPanel Pane() { var p = new StackPanel { Spacing = 4, Visibility = Visibility.Collapsed }; payloads.Children.Add(p); return p; }

        _appPane = Pane();
        _appPane.Children.Add(new TextBlock { Text = "App path", FontSize = 12 });
        var appPathRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _appPath = new TextBox { PlaceholderText = @"C:\Program Files\app.exe", Width = 200 };
        var browseAppBtn = new Button { Content = "Browse…", Padding = new Thickness(8,4,8,4) };
        browseAppBtn.Click += BrowseAppAsync;
        var useIconBtn = new Button { Content = "Use app icon", Padding = new Thickness(8,4,8,4) };
        ToolTipService.SetToolTip(useIconBtn, "Auto-fill the icon from the exe file above");
        useIconBtn.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(_appPath.Text))
                _iconBox.Text = _appPath.Text.Trim();
        };
        appPathRow.Children.Add(_appPath);
        appPathRow.Children.Add(browseAppBtn);
        appPathRow.Children.Add(useIconBtn);
        _appPane.Children.Add(appPathRow);
        _appPane.Children.Add(new TextBlock { Text = "Arguments (optional)", FontSize = 12 });
        _appArgs = new TextBox { PlaceholderText = "command line args" };
        _appPane.Children.Add(_appArgs);

        _urlPane = Pane();
        _urlPane.Children.Add(new TextBlock { Text = "URL", FontSize = 12 });
        _urlBox  = new TextBox { PlaceholderText = "https://example.com" };
        _urlPane.Children.Add(_urlBox);

        _comboPane = Pane();
        // Modifier checkboxes — always visible; Record key captures only the main key
        _comboPane.Children.Add(new TextBlock { Text = "Modifiers", FontSize = 12 });
        var modRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
                                      Margin = new Thickness(0, 2, 0, 4) };
        _comboCtrl  = new CheckBox { Content = "Ctrl"  };
        _comboAlt   = new CheckBox { Content = "Alt"   };
        _comboShift = new CheckBox { Content = "Shift" };
        _comboWin   = new CheckBox { Content = "Win"   };
        modRow.Children.Add(_comboCtrl);
        modRow.Children.Add(_comboAlt);
        modRow.Children.Add(_comboShift);
        modRow.Children.Add(_comboWin);
        _comboPane.Children.Add(modRow);

        _comboPane.Children.Add(new TextBlock { Text = "Key combo", FontSize = 12 });
        _comboBox = new TextBox { PlaceholderText = "Win+D", Width = 200 };
        var comboInputRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var recordComboBtn = new Button { Content = "Record key", Padding = new Thickness(8, 4, 8, 4) };
        recordComboBtn.Click += (_, e) => StartComboRecording(recordComboBtn, e);
        comboInputRow.Children.Add(_comboBox);
        comboInputRow.Children.Add(recordComboBtn);
        _comboPane.Children.Add(comboInputRow);

        _mediaPane = Pane();
        _mediaPane.Children.Add(new TextBlock { Text = "Media action", FontSize = 12 });
        _mediaCombo = new ComboBox { Width = 200 };
        foreach (var n in Enum.GetNames<MediaActionType>()) _mediaCombo.Items.Add(n);
        _mediaPane.Children.Add(_mediaCombo);

        _subPane = Pane();
        _subPane.Children.Add(new TextBlock { Text = "Target submenu", FontSize = 12 });
        _subMenuSel = new ComboBox { Width = 260 };
        _subPane.Children.Add(_subMenuSel);
        var editSubBtn = new Button { Content = "Open / edit this submenu", Margin = new Thickness(0, 4, 0, 0) };
        editSubBtn.Click += (_, _) =>
        {
            if (_subMenuSel.SelectedItem is ComboBoxItem sci && sci.Tag is string sid) DrillInto(sid);
        };
        _subPane.Children.Add(editSubBtn);

        _scriptPane = Pane();
        _scriptPane.Children.Add(new TextBlock { Text = "Script path  (.bat or .ps1)", FontSize = 12 });
        _scriptBox  = new TextBox { PlaceholderText = @"C:\scripts\script.bat" };
        _scriptPane.Children.Add(_scriptBox);

        _clipPane = Pane();
        _clipPane.Children.Add(new TextBlock { Text = "Text to paste", FontSize = 12 });
        _clipBox  = new TextBox { PlaceholderText = "Text to paste…", AcceptsReturn = true, Height = 70 };
        _clipPane.Children.Add(_clipBox);

        _macroPane = Pane();
        _macroPane.Children.Add(new TextBlock { Text = "Macro steps", FontSize = 12 });
        _macroRows = new StackPanel { Spacing = 4 };
        _macroPane.Children.Add(_macroRows);
        var addStepBtn = new Button { Content = "+ Add step", Padding = new Thickness(8, 4, 8, 4), Margin = new Thickness(0, 4, 0, 0) };
        addStepBtn.Click += (_, _) =>
        {
            _macroSteps.Add(new MacroStep { Type = MacroStepType.TypeText, Value = "" });
            RebuildMacroRows();
            MarkDirty();
        };
        _macroPane.Children.Add(addStepBtn);
        _macroPane.Children.Add(new TextBlock
        {
            Text = "Text steps type literal characters. Use Press key for Enter, Tab, or chords like Ctrl+S. " +
                   "Key down and Key up hold a key across later steps.",
            FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 200, 200, 220)),
            Margin = new Thickness(0, 2, 0, 0),
        });

        s.Children.Add(payloads);

        var applyBtn = new Button
        {
            Content = "Apply to item",
            Style   = (Style)Application.Current.Resources["AccentButtonStyle"],
            Margin  = new Thickness(0, 8, 0, 0),
        };
        applyBtn.Click += ApplyItem;
        s.Children.Add(applyBtn);

        card.Child = s;
        return card;
    }

    // ── Menu management ───────────────────────────────────────────────────

    private void PopulateMenuCombo()
    {
        _rebuildingList = true;
        int prevSel = _menuCombo.SelectedIndex;
        _menuCombo.Items.Clear();
        foreach (var m in _menus) _menuCombo.Items.Add(m.Name);
        if (_menus.Count > 0)
            _menuCombo.SelectedIndex = Math.Clamp(prevSel < 0 ? 0 : prevSel, 0, _menus.Count - 1);
        _rebuildingList = false;
    }

    private void PopulateSubMenuPicker()
    {
        _subMenuSel.Items.Clear();
        _subMenuSel.Items.Add(new ComboBoxItem { Content = "Active Apps (built-in)",       Tag = AppConstants.ActiveTasksMenuId      });
        _subMenuSel.Items.Add(new ComboBoxItem { Content = "Clipboard History (built-in)", Tag = AppConstants.ClipboardHistoryMenuId });
        foreach (var m in _menus)
            _subMenuSel.Items.Add(new ComboBoxItem { Content = m.Name, Tag = m.Id });
    }

    // Entry from the menu dropdown — resets the drill breadcrumb to this menu.
    private void SelectMenu(int idx)
    {
        _crumb.Clear();
        if (idx >= 0 && idx < _menus.Count) _crumb.Add(_menus[idx].Id);
        ShowMenu(idx);
    }

    // Navigate to a menu by index without touching the breadcrumb.
    private void ShowMenu(int idx)
    {
        _menuIdx = idx;
        _itemIdx = -1;
        _editorCard.Visibility = Visibility.Collapsed;
        RefreshItemList();
        PopulateSubMenuPicker();
        _ringCanvas?.Invalidate();
        UpdateCrumb();
        UpdateProfileBadge();
    }

    // Drill into a submenu (only if its target is an editable menu in this config).
    private void DrillInto(string childMenuId)
    {
        int idx = _menus.FindIndex(m => m.Id == childMenuId);
        if (idx < 0) return; // dynamic/built-in target (Active Apps / Clipboard) — not editable here
        _crumb.Add(childMenuId);
        _rebuildingList = true; _menuCombo.SelectedIndex = idx; _rebuildingList = false;
        ShowMenu(idx);
    }

    private void CrumbClickToDepth(int depth)
    {
        if (depth < 0 || depth >= _crumb.Count) return;
        string id = _crumb[depth];
        _crumb.RemoveRange(depth + 1, _crumb.Count - depth - 1);
        int idx = _menus.FindIndex(m => m.Id == id);
        if (idx < 0) return;
        _rebuildingList = true; _menuCombo.SelectedIndex = idx; _rebuildingList = false;
        ShowMenu(idx);
    }

    private void UpdateCrumb()
    {
        if (_crumbBar is null) return;
        _crumbBar.Children.Clear();
        for (int d = 0; d < _crumb.Count; d++)
        {
            int    mi   = _menus.FindIndex(m => m.Id == _crumb[d]);
            string name = mi >= 0 ? _menus[mi].Name : _crumb[d];

            if (d > 0)
                _crumbBar.Children.Add(new TextBlock
                {
                    Text = "›", Margin = new Thickness(4, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 200, 200, 220)),
                });

            if (d == _crumb.Count - 1)
                _crumbBar.Children.Add(new TextBlock
                {
                    Text = name, VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                });
            else
            {
                int depth = d;
                var link = new HyperlinkButton { Content = name, Padding = new Thickness(2, 0, 2, 0) };
                link.Click += (_, _) => CrumbClickToDepth(depth);
                _crumbBar.Children.Add(link);
            }
        }
    }

    private void UpdateProfileBadge()
    {
        if (_profileBadge?.Child is not TextBlock tb) return;
        string? menuId = Live?.Id;
        var apps = menuId is null
            ? new List<string>()
            : App.Config.Current.AppProfiles
                .Where(p => p.MenuId == menuId && !string.IsNullOrWhiteSpace(p.ProcessName))
                .Select(p => p.ProcessName).ToList();
        tb.Text = apps.Count > 0
            ? $"Bound to: {string.Join(", ", apps)}"
            : "Not bound to any app";
    }

    // ── Dirty tracking ────────────────────────────────────────────────────

    private void MarkDirty()
    {
        if (_dirtyBar is not null) _dirtyBar.Visibility = Visibility.Visible;
    }

    private void ClearDirty()
    {
        if (_dirtyBar is not null) _dirtyBar.Visibility = Visibility.Collapsed;
    }

    private void Discard(object sender, RoutedEventArgs e)
    {
        // Re-clone from the live config, throwing away all working-copy edits.
        var json = System.Text.Json.JsonSerializer.Serialize(App.Config.Current.Menus);
        _menus   = System.Text.Json.JsonSerializer.Deserialize<List<RadialMenuConfig>>(json) ?? [];
        _itemIdx = -1;
        _editorCard.Visibility = Visibility.Collapsed;
        PopulateMenuCombo();
        if (_menus.Count > 0) { _menuCombo.SelectedIndex = 0; SelectMenu(0); }
        else { _crumb.Clear(); UpdateCrumb(); UpdateProfileBadge(); RefreshItemList(); }
        ClearDirty();
    }

    // Gradient color with fallback to a flat fill color when the gradient stop is empty.
    private static SKColor GradColor(AeroTheme t, string grad, string fallback)
        => t.ToSKColor(grad.Length > 0 ? grad : fallback);

    private void RefreshItemList() => _ringCanvas?.Invalidate();

    private void SelectSlot(int idx)
    {
        _itemIdx = idx;
        if (Cur is null || Cur.IsEmptySlot)
        {
            _itemIdx = -1;
            _editorCard.Visibility = Visibility.Collapsed;
        }
        else
        {
            _editorCard.Visibility = Visibility.Visible;
            LoadEditor(Cur);
        }
        _ringCanvas?.Invalidate();
    }

    private void AddMenu(object sender, RoutedEventArgs e)
    {
        _menus.Add(new RadialMenuConfig
        {
            Id    = Guid.NewGuid().ToString("N"),
            Name  = $"Menu {_menus.Count + 1}",
            Items = [],
        });
        PopulateMenuCombo();
        _menuCombo.SelectedIndex = _menus.Count - 1;
        MarkDirty();
    }

    private async Task RenameMenuAsync()
    {
        if (Live is null) return;
        var tb  = new TextBox { Text = Live.Name, Width = 260 };
        var dlg = new ContentDialog
        {
            Title             = "Rename menu",
            Content           = tb,
            PrimaryButtonText = "Rename",
            CloseButtonText   = "Cancel",
            XamlRoot          = XamlRoot,
        };
        if (await dlg.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(tb.Text))
        {
            Live.Name = tb.Text.Trim();
            PopulateMenuCombo();
            PopulateSubMenuPicker();
            UpdateCrumb();
            MarkDirty();
        }
    }

    private async Task DeleteMenuAsync()
    {
        if (Live is null) return;
        var dlg = new ContentDialog
        {
            Title             = "Delete menu",
            Content           = $"Delete \"{Live.Name}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText   = "Cancel",
            XamlRoot          = XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        _menus.RemoveAt(_menuIdx);
        _menuIdx = -1;
        _itemIdx = -1;
        _editorCard.Visibility = Visibility.Collapsed;
        PopulateMenuCombo();
        MarkDirty();
    }

    // ── Item management ───────────────────────────────────────────────────

    private void AddItemAt(int slot)
    {
        if (Live is null) return;
        PutAt(slot, new MenuItemConfig { Label = "New item", Icon = "default", ActionType = ActionType.None });
        SelectSlot(slot);
        MarkDirty();
    }

    private void RemoveItem(object sender, RoutedEventArgs e)
    {
        if (Live is null || _itemIdx < 0 || _itemIdx >= Live.Items.Count) return;
        Live.Items[_itemIdx] = NewEmpty();  // leave an empty slot in place — no auto-shift
        TrimTrailingEmpties();
        _itemIdx = -1;
        _editorCard.Visibility = Visibility.Collapsed;
        _ringCanvas.Invalidate();
        MarkDirty();
    }

    // ── Slot helpers (allow empty placeholders at any position, i.e. gaps) ──

    private static MenuItemConfig NewEmpty()
        => new() { Label = "", Icon = "", ActionType = ActionType.None };

    private void PutAt(int slot, MenuItemConfig item)
    {
        if (Live is null) return;
        while (Live.Items.Count <= slot) Live.Items.Add(NewEmpty());
        Live.Items[slot] = item;
    }

    private void TrimTrailingEmpties()
    {
        if (Live is null) return;
        while (Live.Items.Count > 0 && Live.Items[^1].IsEmptySlot)
            Live.Items.RemoveAt(Live.Items.Count - 1);
    }

    private void MoveOrSwap(int src, int dst)
    {
        if (Live is null || src == dst) return;
        int max = Math.Max(src, dst);
        while (Live.Items.Count <= max) Live.Items.Add(NewEmpty());
        (Live.Items[src], Live.Items[dst]) = (Live.Items[dst], Live.Items[src]);
        TrimTrailingEmpties();
        _ringCanvas.Invalidate();
    }

    // ── Editor ────────────────────────────────────────────────────────────

    private void LoadEditor(MenuItemConfig item)
    {
        _labelBox.Text = item.Label;
        _iconBox.Text  = item.Icon ?? "";
        _iconCombo.SelectedIndex = -1;

        _actionCombo.SelectedIndex = Array.IndexOf(EditableActionTypes, item.ActionType.ToString());

        _appPath.Text   = item.AppPath    ?? "";
        _appArgs.Text   = item.AppArgs    ?? "";
        _urlBox.Text    = item.Url        ?? "";
        _comboBox.Text  = item.KeyCombo   ?? "";
        ParseComboToCheckboxes(item.KeyCombo);
        _scriptBox.Text = item.ScriptPath ?? "";
        _clipBox.Text   = item.ClipText   ?? "";

        var mNames = Enum.GetNames<MediaActionType>();
        _mediaCombo.SelectedIndex = item.MediaAction.HasValue
            ? Math.Max(0, Array.IndexOf(mNames, item.MediaAction.Value.ToString())) : 0;

        _subMenuSel.SelectedIndex = -1;
        if (item.SubMenuId is not null)
            for (int i = 0; i < _subMenuSel.Items.Count; i++)
                if (_subMenuSel.Items[i] is ComboBoxItem ci && ci.Tag as string == item.SubMenuId)
                    { _subMenuSel.SelectedIndex = i; break; }

        _macroSteps = (item.Macro ?? new List<MacroStep>())
            .Select(m => new MacroStep { Type = m.Type, Value = m.Value, DelayMs = m.DelayMs }).ToList();
        RebuildMacroRows();

        ShowPane(item.ActionType);
    }

    private void OnActionTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_actionCombo.SelectedItem is string name && Enum.TryParse<ActionType>(name, out var at))
            ShowPane(at);
    }

    private void ShowPane(ActionType at)
    {
        _appPane.Visibility    = at == ActionType.LaunchApp      ? Visibility.Visible : Visibility.Collapsed;
        _urlPane.Visibility    = at == ActionType.OpenUrl        ? Visibility.Visible : Visibility.Collapsed;
        _comboPane.Visibility  = at == ActionType.KeyCombo       ? Visibility.Visible : Visibility.Collapsed;
        _mediaPane.Visibility  = at == ActionType.Media          ? Visibility.Visible : Visibility.Collapsed;
        _subPane.Visibility    = at == ActionType.SubMenu        ? Visibility.Visible : Visibility.Collapsed;
        _scriptPane.Visibility = at == ActionType.RunScript      ? Visibility.Visible : Visibility.Collapsed;
        _clipPane.Visibility   = at == ActionType.PasteClipboard ? Visibility.Visible : Visibility.Collapsed;
        _macroPane.Visibility  = at == ActionType.Macro          ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Macro step editor ─────────────────────────────────────────────────

    private static string MacroPlaceholder(MacroStepType t) => t switch
    {
        MacroStepType.TypeText => "text to type",
        MacroStepType.KeyPress => "Enter, Tab, Ctrl+S…",
        MacroStepType.KeyDown  => "Shift",
        MacroStepType.KeyUp    => "Shift",
        MacroStepType.Delay    => "milliseconds",
        _                      => "",
    };

    private void RebuildMacroRows()
    {
        _macroRows.Children.Clear();
        if (_macroSteps.Count == 0)
        {
            _macroRows.Children.Add(new TextBlock
            {
                Text = "No steps yet. Add one below.", FontSize = 12,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 200, 200, 220)),
            });
            return;
        }

        for (int i = 0; i < _macroSteps.Count; i++)
        {
            int  idx  = i;
            var  step = _macroSteps[i];
            var  row  = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

            row.Children.Add(new TextBlock
            {
                Text = $"{i + 1}", Width = 18, FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 200, 200, 220)),
            });

            var typeCombo = new ComboBox { Width = 110 };
            foreach (var n in MacroStepNames) typeCombo.Items.Add(n);
            typeCombo.SelectedIndex = (int)step.Type;

            var valueBox = new TextBox
            {
                Width           = 150,
                Text            = step.Type == MacroStepType.Delay ? step.DelayMs.ToString() : step.Value,
                PlaceholderText = MacroPlaceholder(step.Type),
            };
            valueBox.TextChanged += (_, _) =>
            {
                if (step.Type == MacroStepType.Delay)
                {
                    int.TryParse(valueBox.Text, out int ms);
                    step.DelayMs = Math.Max(0, ms);
                }
                else step.Value = valueBox.Text;
                MarkDirty();
            };

            typeCombo.SelectionChanged += (_, _) =>
            {
                step.Type = (MacroStepType)typeCombo.SelectedIndex;
                valueBox.PlaceholderText = MacroPlaceholder(step.Type);
                valueBox.Text = step.Type == MacroStepType.Delay ? step.DelayMs.ToString() : step.Value;
                MarkDirty();
            };

            Button MiniBtn(string t) => new() { Content = t, Width = 32, Padding = new Thickness(0, 2, 0, 2) };
            var up   = MiniBtn("↑");
            var down = MiniBtn("↓");
            var del  = MiniBtn("✕");
            up.Click   += (_, _) => { if (idx > 0) { (_macroSteps[idx], _macroSteps[idx - 1]) = (_macroSteps[idx - 1], _macroSteps[idx]); RebuildMacroRows(); MarkDirty(); } };
            down.Click += (_, _) => { if (idx < _macroSteps.Count - 1) { (_macroSteps[idx], _macroSteps[idx + 1]) = (_macroSteps[idx + 1], _macroSteps[idx]); RebuildMacroRows(); MarkDirty(); } };
            del.Click  += (_, _) => { _macroSteps.RemoveAt(idx); RebuildMacroRows(); MarkDirty(); };

            row.Children.Add(typeCombo);
            row.Children.Add(valueBox);
            row.Children.Add(up);
            row.Children.Add(down);
            row.Children.Add(del);
            _macroRows.Children.Add(row);
        }
    }

    private static string? NE(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private void ApplyItem(object sender, RoutedEventArgs e)
    {
        if (Cur is null) return;
        Cur.Label = NE(_labelBox.Text) ?? "Item";
        Cur.Icon  = NE(_iconBox.Text)  ?? "default";

        if (_actionCombo.SelectedItem is string atName && Enum.TryParse<ActionType>(atName, out var at))
        {
            Cur.ActionType = at;
            Cur.AppPath    = at == ActionType.LaunchApp      ? NE(_appPath.Text)   : null;
            Cur.AppArgs    = at == ActionType.LaunchApp      ? NE(_appArgs.Text)   : null;
            Cur.Url        = at == ActionType.OpenUrl        ? NE(_urlBox.Text)    : null;
            Cur.KeyCombo   = at == ActionType.KeyCombo       ? NE(_comboBox.Text)  : null;
            Cur.ScriptPath = at == ActionType.RunScript      ? NE(_scriptBox.Text) : null;
            Cur.ClipText   = at == ActionType.PasteClipboard ? NE(_clipBox.Text)   : null;

            if (at == ActionType.Media && _mediaCombo.SelectedItem is string mn
                && Enum.TryParse<MediaActionType>(mn, out var ma))
                Cur.MediaAction = ma;
            else
                Cur.MediaAction = at == ActionType.Media ? MediaActionType.PlayPause : (MediaActionType?)null;

            Cur.SubMenuId = at == ActionType.SubMenu && _subMenuSel.SelectedItem is ComboBoxItem ci
                ? ci.Tag as string : null;

            Cur.Macro = at == ActionType.Macro
                ? _macroSteps.Select(m => new MacroStep { Type = m.Type, Value = m.Value, DelayMs = m.DelayMs }).ToList()
                : null;
        }

        _ringCanvas?.Invalidate();
        MarkDirty();
    }

    private async void Save(object sender, RoutedEventArgs e)
    {
        if (Cur is not null) ApplyItem(sender, e);

        // Don't persist trailing empty placeholder slots (mid-list gaps are kept on purpose).
        foreach (var m in _menus)
            while (m.Items.Count > 0 && m.Items[^1].IsEmptySlot)
                m.Items.RemoveAt(m.Items.Count - 1);

        var json = System.Text.Json.JsonSerializer.Serialize(_menus);
        var copy = System.Text.Json.JsonSerializer.Deserialize<List<RadialMenuConfig>>(json)!;
        await App.Config.UpdateAsync(cfg => cfg.Menus = copy);

        ClearDirty();
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }

    // ── Ring preview ──────────────────────────────────────────────────────

    private void OnRingPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        var menu   = Live;
        var theme  = App.Themes.ActiveTheme;
        var appear = App.Config.Current.Appearance;

        float w  = e.Info.Width, h = e.Info.Height;
        float cx = w / 2f, cy = h / 2f;
        float minDim  = Math.Min(w, h);
        float outerR  = minDim * 0.44f;
        float innerR  = minDim * 0.17f;
        float iconR   = (outerR + innerR) / 2f;

        int sliceCount = Math.Clamp(appear.SliceCount, 4, 12);
        int itemCount  = menu?.Items.Count ?? 0;
        float fullArc  = 360f / sliceCount;
        float gap      = appear.GapDegrees;
        float sweep    = fullArc - gap;
        float startOff = -90f - fullArc / 2f;

        using var fill   = new SKPaint { IsAntialias = true };
        using var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var tp     = new SKPaint { IsAntialias = true, TextAlign = SKTextAlign.Center };

        for (int i = 0; i < sliceCount; i++)
        {
            bool  sel   = i == _itemIdx;
            bool  empty = i >= itemCount || (menu is not null && menu.Items[i].IsEmptySlot);
            float start = startOff + i * fullArc + gap / 2f;
            float emptyMul = empty ? 0.32f : 1f;

            // Gradient fill matching the overlay's slice look
            SKColor innerC, outerC;
            if (sel && !empty)
            {
                innerC = GradColor(theme, theme.SliceGradientInnerHover, theme.SliceFillHover);
                outerC = GradColor(theme, theme.SliceGradientOuterHover, theme.SliceFillHover);
            }
            else
            {
                innerC = GradColor(theme, theme.SliceGradientInner, theme.SliceFill);
                outerC = GradColor(theme, theme.SliceGradientOuter, theme.SliceFill);
            }
            byte ia = (byte)(innerC.Alpha * emptyMul), oa = (byte)(outerC.Alpha * emptyMul);

            using var path = RingSlicePath(cx, cy, outerR, innerR, start, sweep);
            float gradPos = Math.Clamp(outerR > 0f ? innerR / outerR : 0f, 0f, 0.95f);
            using (var shader = SKShader.CreateRadialGradient(
                new SKPoint(cx, cy), outerR,
                [innerC.WithAlpha(ia), outerC.WithAlpha(oa)],
                [gradPos, 1f], SKShaderTileMode.Clamp))
            {
                fill.Style  = SKPaintStyle.Fill;
                fill.Shader = shader;
                canvas.DrawPath(path, fill);
                fill.Shader = null;
            }

            var strokeC = (sel && !empty ? theme.ToSKColor(theme.AccentColor) : theme.ToSKColor(theme.SliceStroke));
            stroke.Color       = strokeC.WithAlpha((byte)(strokeC.Alpha * emptyMul));
            stroke.StrokeWidth = sel ? 2f : Math.Max(theme.SliceStrokeWidth, 0.5f);
            canvas.DrawPath(path, stroke);

            // Drag-target highlight
            if (_dragging && i == _dragHoverSlice)
            {
                stroke.Color       = theme.ToSKColor(theme.AccentColor);
                stroke.StrokeWidth = 3f;
                canvas.DrawPath(path, stroke);
            }

            float mid = startOff + i * fullArc + fullArc / 2f;
            float rad = mid * MathF.PI / 180f;
            float ix  = cx + MathF.Cos(rad) * iconR;
            float iy  = cy + MathF.Sin(rad) * iconR;

            if (!empty && menu is not null)
            {
                var bmp = IconRegistry.Get(menu.Items[i].Icon, theme.IconStrokeScale);
                if (bmp is not null)
                {
                    float isz  = minDim * 0.12f;
                    var   dest = new SKRect(ix - isz / 2, iy - isz / 2, ix + isz / 2, iy + isz / 2);
                    using var ip = new SKPaint
                    {
                        IsAntialias   = true,
                        FilterQuality = SKFilterQuality.High,
                        ColorFilter   = SKColorFilter.CreateBlendMode(
                            theme.ToSKColor(sel ? theme.IconTintHover : theme.IconTint), SKBlendMode.Modulate),
                    };
                    canvas.DrawBitmap(bmp, dest, ip);
                }
            }
            else if (empty)
            {
                // Empty slot → "+" add affordance
                tp.Color    = theme.ToSKColor(theme.AccentColor).WithAlpha(90);
                tp.TextSize = minDim * 0.11f;
                canvas.DrawText("+", ix, iy + tp.TextSize / 3f, tp);
            }
        }

        // Center circle
        fill.Style  = SKPaintStyle.Fill;
        fill.Color  = theme.ToSKColor(theme.CenterFill);
        canvas.DrawCircle(cx, cy, innerR, fill);
        stroke.Color       = theme.ToSKColor(theme.CenterStroke);
        stroke.StrokeWidth = 1f;
        canvas.DrawCircle(cx, cy, innerR, stroke);

        // Center label = current menu name
        if (menu is not null)
        {
            string nm = menu.Name.Length > 10 ? menu.Name[..10] : menu.Name;
            tp.Color    = theme.ToSKColor(theme.LabelColor).WithAlpha(210);
            tp.TextSize = minDim * 0.05f;
            canvas.DrawText(nm, cx, cy + tp.TextSize / 3f, tp);
        }
    }

    // Returns the slice index under a canvas point, or -1 if outside the ring band.
    private int HitSlice(Windows.Foundation.Point pos)
    {
        float cx = (float)_ringCanvas.ActualWidth  / 2f;
        float cy = (float)_ringCanvas.ActualHeight / 2f;
        float dx = (float)pos.X - cx, dy = (float)pos.Y - cy;
        float dist   = MathF.Sqrt(dx * dx + dy * dy);
        float minDim = (float)Math.Min(_ringCanvas.ActualWidth, _ringCanvas.ActualHeight);
        if (dist < minDim * 0.17f || dist > minDim * 0.44f) return -1;

        int   sliceCount = Math.Clamp(App.Config.Current.Appearance.SliceCount, 4, 12);
        float fullArc    = 360f / sliceCount;
        float angleDeg   = MathF.Atan2(dy, dx) * 180f / MathF.PI;
        if (angleDeg < 0) angleDeg += 360f;
        float topAlign = (angleDeg + 90f + fullArc / 2f) % 360f;
        return (int)(topAlign / fullArc) % sliceCount;
    }

    private static bool SlotFilled(RadialMenuConfig? menu, int slot)
        => menu is not null && slot >= 0 && slot < menu.Items.Count && !menu.Items[slot].IsEmptySlot;

    private void OnRingPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _dragStart      = e.GetCurrentPoint(_ringCanvas).Position;
        _dragSrcSlice   = HitSlice(_dragStart);
        _dragHoverSlice = -1;
        _dragging       = false;
        if (_dragSrcSlice >= 0) _ringCanvas.CapturePointer(e.Pointer);
    }

    private void OnRingPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_dragSrcSlice < 0) return;
        var p = e.GetCurrentPoint(_ringCanvas).Position;
        if (!_dragging)
        {
            // Start a drag only once the pointer moves off the source and the source is filled.
            double move = Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y);
            if (move < 6 || !SlotFilled(Live, _dragSrcSlice)) return;
            _dragging = true;
        }
        int hov = HitSlice(p);
        if (hov != _dragHoverSlice) { _dragHoverSlice = hov; _ringCanvas.Invalidate(); }
    }

    private void OnRingPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _ringCanvas.ReleasePointerCapture(e.Pointer);
        int  src         = _dragSrcSlice;
        int  hov         = _dragHoverSlice;
        bool wasDragging = _dragging;
        _dragSrcSlice = _dragHoverSlice = -1;
        _dragging = false;

        if (wasDragging && src >= 0 && hov >= 0 && hov != src)
        {
            MoveOrSwap(src, hov);
            SelectSlot(hov);
            MarkDirty();
            return;
        }

        // Not a drag → treat as a click on the pressed slice.
        int target = HitSlice(e.GetCurrentPoint(_ringCanvas).Position);
        if (target < 0) target = src;
        if (target < 0) { _ringCanvas.Invalidate(); return; }

        if (SlotFilled(Live, target)) SelectSlot(target);
        else                          AddItemAt(target);
    }

    private static SKPath RingSlicePath(float cx, float cy,
        float outerR, float innerR, float start, float sweep)
    {
        var path = new SKPath();
        path.ArcTo(new SKRect(cx-outerR, cy-outerR, cx+outerR, cy+outerR), start, sweep, true);
        path.ArcTo(new SKRect(cx-innerR, cy-innerR, cx+innerR, cy+innerR), start+sweep, -sweep, false);
        path.Close();
        return path;
    }

    // ── File pickers ──────────────────────────────────────────────────────

    private async void BrowseIconAsync(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, SettingsWindow.WindowHandle);
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".ico");
        picker.FileTypeFilter.Add(".bmp");
        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            _iconBox.Text = file.Path;
            IconRegistry.Invalidate(file.Path);
        }
    }

    private async void BrowseAppAsync(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, SettingsWindow.WindowHandle);
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        picker.FileTypeFilter.Add("*");
        var file = await picker.PickSingleFileAsync();
        if (file is not null) _appPath.Text = file.Path;
    }

    // ── Presets ───────────────────────────────────────────────────────────

    private static readonly (string Name, Func<List<MenuItemConfig>> Factory)[] BuiltInPresets =
    [
        ("Media Controls", () =>
        [
            new() { Label = "Play / Pause", Icon = "play",     ActionType = ActionType.Media, MediaAction = MediaActionType.PlayPause  },
            new() { Label = "Next",          Icon = "next",     ActionType = ActionType.Media, MediaAction = MediaActionType.Next       },
            new() { Label = "Previous",      Icon = "prev",     ActionType = ActionType.Media, MediaAction = MediaActionType.Previous   },
            new() { Label = "Volume Up",     Icon = "vol_up",   ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeUp,
                    ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Volume Down",   Icon = "vol_down", ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeDown,
                    ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Mute",          Icon = "mute",     ActionType = ActionType.Media, MediaAction = MediaActionType.Mute       },
        ]),
        ("System Tools", () =>
        [
            new() { Label = "Settings",   Icon = "settings",   ActionType = ActionType.OpenSettings                                             },
            new() { Label = "Desktop",    Icon = "desktop",    ActionType = ActionType.KeyCombo,  KeyCombo = "Win+D"                           },
            new() { Label = "Lock",       Icon = "lock",       ActionType = ActionType.KeyCombo,  KeyCombo = "Win+L"                           },
            new() { Label = "Screenshot", Icon = "screenshot", ActionType = ActionType.KeyCombo,  KeyCombo = "Win+Shift+S"                     },
            new() { Label = "Task Mgr",   Icon = "list",       ActionType = ActionType.KeyCombo,  KeyCombo = "Ctrl+Shift+Esc"                  },
            new() { Label = "Clipboard",  Icon = "clipboard",  ActionType = ActionType.SubMenu,   SubMenuId = AppConstants.ClipboardHistoryMenuId },
            new() { Label = "Apps",       Icon = "apps",       ActionType = ActionType.SubMenu,   SubMenuId = AppConstants.ActiveTasksMenuId      },
        ]),
        ("Productivity", () =>
        [
            new() { Label = "Copy",       Icon = "copy",     ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+C" },
            new() { Label = "Paste",      Icon = "paste",    ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+V" },
            new() { Label = "Cut",        Icon = "edit",     ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+X" },
            new() { Label = "Undo",       Icon = "back",     ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+Z" },
            new() { Label = "Redo",       Icon = "forward",  ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+Y" },
            new() { Label = "Save",       Icon = "download", ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+S" },
            new() { Label = "Find",       Icon = "search",   ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+F" },
            new() { Label = "Select All", Icon = "check",    ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+A" },
        ]),
    ];

    private async Task LoadPresetAsync()
    {
        if (Live is null) return;

        // Build display list: built-ins, then any JSON files in the presets folder
        var allNames = BuiltInPresets.Select(p => p.Name).ToList();
        var userEntries = new List<(string Name, string Json)>();

        if (Directory.Exists(AppConstants.PresetsDir))
        {
            foreach (var f in Directory.GetFiles(AppConstants.PresetsDir, "*.json").OrderBy(x => x))
            {
                try
                {
                    var txt = await File.ReadAllTextAsync(f);
                    userEntries.Add((Path.GetFileNameWithoutExtension(f), txt));
                    allNames.Add($"[Custom]  {Path.GetFileNameWithoutExtension(f)}");
                }
                catch (Exception ex) { Logger.Warn($"Could not load preset {f}", ex); }
            }
        }

        var listBox = new ListView { Height = 200, SelectionMode = ListViewSelectionMode.Single };
        foreach (var n in allNames) listBox.Items.Add(n);
        listBox.SelectedIndex = 0;

        var hint = new TextBlock
        {
            Text         = "Select a preset — it will replace all items in the current menu.",
            TextWrapping = TextWrapping.Wrap,
            FontSize     = 12,
            Foreground   = new SolidColorBrush(ColorHelper.FromArgb(160, 200, 200, 220)),
        };
        var presetContent = new StackPanel { Spacing = 8 };
        presetContent.Children.Add(hint);
        presetContent.Children.Add(listBox);

        var dlg = new ContentDialog
        {
            Title             = "Load Preset",
            Content           = presetContent,
            PrimaryButtonText = "Load",
            CloseButtonText   = "Cancel",
            XamlRoot          = XamlRoot,
        };

        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
        if (listBox.SelectedIndex < 0) return;

        List<MenuItemConfig> items;
        int idx = listBox.SelectedIndex;

        if (idx < BuiltInPresets.Length)
        {
            items = BuiltInPresets[idx].Factory();
        }
        else
        {
            var (_, json) = userEntries[idx - BuiltInPresets.Length];
            items = System.Text.Json.JsonSerializer.Deserialize<List<MenuItemConfig>>(json) ?? [];
        }

        Live.Items.Clear();
        foreach (var item in items) Live.Items.Add(item);
        RefreshItemList();
        _ringCanvas?.Invalidate();
        MarkDirty();
    }

    private async Task SaveAsPresetAsync()
    {
        if (Live is null) return;

        var tb = new TextBox
        {
            Text            = Live.Name,
            PlaceholderText = "Preset name",
            Width           = 280,
        };
        var dlg = new ContentDialog
        {
            Title             = "Save as Preset",
            Content           = tb,
            PrimaryButtonText = "Save",
            CloseButtonText   = "Cancel",
            XamlRoot          = XamlRoot,
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;

        var name = tb.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;

        // Sanitize name for use as a filename
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        try
        {
            Directory.CreateDirectory(AppConstants.PresetsDir);
            var json = System.Text.Json.JsonSerializer.Serialize(
                Live.Items,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            var path = Path.Combine(AppConstants.PresetsDir, $"{name}.json");
            await File.WriteAllTextAsync(path, json);
            Logger.Info($"Saved preset '{name}' to {path}");
        }
        catch (Exception ex)
        {
            Logger.Warn("Could not save preset", ex);
        }
    }

    // ── Key combo recording ───────────────────────────────────────────────

    private void StartComboRecording(Button btn, RoutedEventArgs e)
    {
        if (_recordingCombo) return;
        _recordingCombo           = true;
        btn.Content               = "Waiting…";
        _comboBox.PlaceholderText = "Press a key…";

        // Invisible TextBox captures keyboard events — only the non-modifier key.
        // Modifier state comes from the checkboxes, which the user sets explicitly.
        // This avoids the timing problem where GetAsyncKeyState may miss modifiers
        // that were released before the KeyDown event fires in a ScrollViewer context.
        _comboCaptureBox = new TextBox { Opacity = 0, Width = 1, Height = 1, IsTabStop = true };
        _comboCaptureBox.KeyDown += (_, args) =>
        {
            int vk = (int)args.Key;
            // Ignore bare modifier key presses — wait for the actual key
            if (vk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C) return;

            var parts = new System.Text.StringBuilder();
            if (_comboCtrl?.IsChecked  == true) parts.Append("Ctrl+");
            if (_comboAlt?.IsChecked   == true) parts.Append("Alt+");
            if (_comboShift?.IsChecked == true) parts.Append("Shift+");
            if (_comboWin?.IsChecked   == true) parts.Append("Win+");
            parts.Append(ComboKeyName(vk));

            FinishComboRecording(btn, parts.ToString());
            args.Handled = true;
        };

        if (Content is ScrollViewer sv && sv.Content is Panel pan)
            pan.Children.Add(_comboCaptureBox);
        _comboCaptureBox.Focus(FocusState.Programmatic);
    }

    private void FinishComboRecording(Button btn, string combo)
    {
        _recordingCombo           = false;
        btn.Content               = "Record key";
        _comboBox.Text            = combo;
        _comboBox.PlaceholderText = "Win+D";
        if (_comboCaptureBox?.Parent is Panel pp) pp.Children.Remove(_comboCaptureBox);
        _comboCaptureBox = null;
    }

    /// <summary>Parses an existing combo string and pre-ticks the modifier checkboxes.</summary>
    private void ParseComboToCheckboxes(string? combo)
    {
        if (_comboCtrl is null) return;
        _comboCtrl.IsChecked  = combo?.Contains("Ctrl+",  StringComparison.OrdinalIgnoreCase) == true;
        _comboAlt!.IsChecked  = combo?.Contains("Alt+",   StringComparison.OrdinalIgnoreCase) == true;
        _comboShift!.IsChecked = combo?.Contains("Shift+", StringComparison.OrdinalIgnoreCase) == true;
        _comboWin!.IsChecked  = combo?.Contains("Win+",   StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string ComboKeyName(int vk) => vk switch
    {
        0x08 => "Backspace", 0x09 => "Tab",    0x0D => "Enter",
        0x1B => "Escape",    0x20 => "Space",
        0x21 => "PageUp",    0x22 => "PageDown", 0x23 => "End",   0x24 => "Home",
        0x25 => "Left",      0x26 => "Up",       0x27 => "Right", 0x28 => "Down",
        0x2C => "PrintScreen", 0x2D => "Insert", 0x2E => "Delete",
        0x60 => "Num0",  0x61 => "Num1",  0x62 => "Num2",  0x63 => "Num3",
        0x64 => "Num4",  0x65 => "Num5",  0x66 => "Num6",  0x67 => "Num7",
        0x68 => "Num8",  0x69 => "Num9",  0x6A => "Num*",  0x6B => "Num+",
        0x6D => "Num-",  0x6E => "Num.",  0x6F => "Num/",
        0x70 => "F1",  0x71 => "F2",  0x72 => "F3",  0x73 => "F4",
        0x74 => "F5",  0x75 => "F6",  0x76 => "F7",  0x77 => "F8",
        0x78 => "F9",  0x79 => "F10", 0x7A => "F11", 0x7B => "F12",
        0xBA => ";",   0xBB => "=",   0xBC => ",",   0xBD => "-",
        0xBE => ".",   0xBF => "/",   0xC0 => "`",
        0xDB => "[",   0xDC => "\\",  0xDD => "]",   0xDE => "'",
        >= 0x30 and <= 0x39 => $"{(char)vk}",
        >= 0x41 and <= 0x5A => $"{(char)vk}",
        _ => $"0x{vk:X2}",
    };

    // ── Icon preview paint ────────────────────────────────────────────────

    private void OnIconPreviewPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(new SKColor(35, 35, 45, 200));

        string key = _iconBox?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(key)) return;

        var bmp = IconRegistry.Get(key);
        if (bmp is null) return;

        var theme = App.Themes.ActiveTheme;
        var tint  = theme.ToSKColor(theme.IconTint);

        float m    = 5f;
        var   dest = new SKRect(m, m, e.Info.Width - m, e.Info.Height - m);
        using var paint = new SKPaint
        {
            IsAntialias = true,
            ColorFilter = SKColorFilter.CreateBlendMode(tint, SKBlendMode.SrcIn),
        };
        canvas.DrawBitmap(bmp, dest, paint);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ThemesPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class ThemesPage : Page
{
    public ThemesPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(UI.PageHeader("Themes"));
        stack.Children.Add(UI.InfoCard(
            "To create a custom theme: copy any .json from the themes\\ folder next to AeroDial.exe " +
            "into %AppData%\\Roaming\\AeroDial\\themes\\, rename it, and edit the colour values (#AARRGGBB format)."));

        var current = App.Config.Current.Appearance.ThemeName;

        foreach (var name in App.Themes.AvailableThemes)
        {
            var t = App.Themes.Get(name);
            if (t is null) continue;

            bool active = name == current;
            var ac = t.ToSKColor(t.AccentColor);

            var card = new Border
            {
                Background   = new SolidColorBrush(active
                    ? ColorHelper.FromArgb(45, ac.Red, ac.Green, ac.Blue)
                    : ColorHelper.FromArgb(18, 120, 110, 180)),
                CornerRadius    = new CornerRadius(10),
                Padding         = new Thickness(16, 12, 16, 12),
                Margin          = new Thickness(0, 5, 0, 0),
                BorderThickness = new Thickness(active ? 1.5 : 0.5),
                BorderBrush     = new SolidColorBrush(active
                    ? ColorHelper.FromArgb(180, ac.Red, ac.Green, ac.Blue)
                    : ColorHelper.FromArgb(35, 140, 130, 180)),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel { Spacing = 3 };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            nameRow.Children.Add(new Border
            {
                Width = 14, Height = 14, CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(
                    ColorHelper.FromArgb(ac.Alpha, ac.Red, ac.Green, ac.Blue)),
            });
            nameRow.Children.Add(new TextBlock
            {
                Text = name, FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            if (active)
                nameRow.Children.Add(new TextBlock
                {
                    Text = "Active", FontSize = 11,
                    Foreground = new SolidColorBrush(
                        ColorHelper.FromArgb(200, ac.Red, ac.Green, ac.Blue)),
                    VerticalAlignment = VerticalAlignment.Center,
                });

            info.Children.Add(nameRow);
            info.Children.Add(new TextBlock
            {
                Text = t.Description, FontSize = 12,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(150, 180, 180, 200)),
            });

            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var actionPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing     = 6,
                VerticalAlignment = VerticalAlignment.Center,
            };

            if (!active)
            {
                var applyBtn = new Button
                {
                    Content = "Apply", VerticalAlignment = VerticalAlignment.Center, Tag = name,
                };
                applyBtn.Click += async (sender, _) =>
                {
                    if (sender is Button b && b.Tag is string n)
                    {
                        await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = n);
                        Build();
                    }
                };
                actionPanel.Children.Add(applyBtn);
            }

            if (!t.IsBuiltIn)
            {
                var deleteBtn = new Button
                {
                    Content      = "Delete",
                    Tag          = name,
                    Background   = new SolidColorBrush(ColorHelper.FromArgb(180, 180, 50, 50)),
                    Foreground   = new SolidColorBrush(Colors.White),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                deleteBtn.Click += async (sender, _) =>
                {
                    if (sender is not Button b || b.Tag is not string n) return;
                    var dlg = new ContentDialog
                    {
                        Title             = "Delete theme?",
                        Content           = $"Remove \"{n}\" permanently from your user themes?",
                        PrimaryButtonText = "Delete",
                        CloseButtonText   = "Cancel",
                        XamlRoot          = b.XamlRoot,
                    };
                    if (await dlg.ShowAsync() != ContentDialogResult.Primary) return;
                    App.Themes.DeleteUserTheme(n);
                    // Fall back to Obsidian if the active theme was deleted
                    if (App.Config.Current.Appearance.ThemeName == n)
                        await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = "Obsidian");
                    Build();
                };
                actionPanel.Children.Add(deleteBtn);
            }

            if (actionPanel.Children.Count > 0)
            {
                Grid.SetColumn(actionPanel, 1);
                grid.Children.Add(actionPanel);
            }

            card.Child = grid;
            stack.Children.Add(card);
        }

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        var openUserThemes = new Button { Content = "Open user themes folder" };
        openUserThemes.Click += (_, _) =>
        {
            var dir = AppConstants.UserThemesDir;
            Directory.CreateDirectory(dir);
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        };
        var openBuiltinThemes = new Button { Content = "Open built-in themes folder" };
        openBuiltinThemes.Click += (_, _) =>
        {
            var dir = AppConstants.ThemesDir;
            if (Directory.Exists(dir))
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
        };
        btnRow.Children.Add(openUserThemes);
        btnRow.Children.Add(openBuiltinThemes);
        stack.Children.Add(btnRow);

        scroll.Content = stack; Content = scroll;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DynamicPage — Scroll-wheel bindings and dynamic content settings
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class DynamicPage : Page
{
    private readonly List<ComboBox> _upBoxes   = [];
    private readonly List<ComboBox> _downBoxes = [];
    private TextBlock               _saved     = null!;
    private string?                 _menuId;
    private DispatcherTimer?        _saveTimer;

    private static readonly string[] ActionNames =
        ["None", "Volume Up", "Volume Down", "Play / Pause", "Next", "Previous", "Mute"];

    private static AeroDial.Config.MediaActionType? IndexToAction(int i) => i switch
    {
        1 => AeroDial.Config.MediaActionType.VolumeUp,
        2 => AeroDial.Config.MediaActionType.VolumeDown,
        3 => AeroDial.Config.MediaActionType.PlayPause,
        4 => AeroDial.Config.MediaActionType.Next,
        5 => AeroDial.Config.MediaActionType.Previous,
        6 => AeroDial.Config.MediaActionType.Mute,
        _ => null,
    };

    private static int ActionToIndex(AeroDial.Config.MediaActionType? a) => a switch
    {
        AeroDial.Config.MediaActionType.VolumeUp   => 1,
        AeroDial.Config.MediaActionType.VolumeDown => 2,
        AeroDial.Config.MediaActionType.PlayPause  => 3,
        AeroDial.Config.MediaActionType.Next       => 4,
        AeroDial.Config.MediaActionType.Previous   => 5,
        AeroDial.Config.MediaActionType.Mute       => 6,
        _                                           => 0,
    };

    public DynamicPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(UI.PageHeader("Dynamic Content"));
        stack.Children.Add(UI.InfoCard(
            "Scroll Wheel Bindings: while the dial is open and your cursor is on a slice, " +
            "scrolling the mouse wheel triggers the assigned action."));

        // Menu picker
        stack.Children.Add(UI.SubHeader("Menu"));
        var menuPicker = new ComboBox { Width = 300 };
        foreach (var m in App.Config.Current.Menus)
            menuPicker.Items.Add(new ComboBoxItem { Content = m.Name, Tag = m.Id });
        menuPicker.SelectedIndex = 0;
        stack.Children.Add(menuPicker);

        // Bindings panel — rebuilt when menu selection changes
        var bindingsPanel = new StackPanel { Spacing = 0, Margin = new Thickness(0, 8, 0, 0) };
        stack.Children.Add(bindingsPanel);

        if (App.Config.Current.Menus.Count > 0)
        {
            _menuId = App.Config.Current.Menus[0].Id;
            BuildBindings(bindingsPanel, App.Config.Current.Menus[0]);
        }

        menuPicker.SelectionChanged += (_, _) =>
        {
            if (menuPicker.SelectedItem is ComboBoxItem li && li.Tag is string id)
            {
                _menuId = id;
                var menu = App.Config.Current.Menus.FirstOrDefault(m => m.Id == id);
                _upBoxes.Clear(); _downBoxes.Clear();
                BuildBindings(bindingsPanel, menu);
            }
        };

        _saved = UI.SavedBadge();
        _saved.Margin = new Thickness(0, 8, 0, 0);
        stack.Children.Add(_saved);

        scroll.Content = stack; Content = scroll;
    }

    private void BuildBindings(StackPanel panel, AeroDial.Config.RadialMenuConfig? menu)
    {
        panel.Children.Clear();
        _upBoxes.Clear(); _downBoxes.Clear();
        if (menu is null || menu.Items.Count == 0)
        {
            panel.Children.Add(new TextBlock
            {
                Text = "This menu has no items.",
                FontSize = 13,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(130, 200, 200, 220)),
            });
            return;
        }

        // Column headers
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });

        void AddHeader(string text, int col)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 11,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(140, 200, 200, 220)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(col == 0 ? 0 : 4, 0, 0, 0),
            };
            Grid.SetColumn(tb, col); headerGrid.Children.Add(tb);
        }
        AddHeader("Slice", 0); AddHeader("Scroll Up →", 1); AddHeader("Scroll Down →", 2);
        panel.Children.Add(headerGrid);

        foreach (var item in menu.Items)
        {
            var rowGrid = new Grid
            {
                Margin = new Thickness(0, 3, 0, 3),
            };
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(155) });

            var label = new TextBlock
            {
                Text = item.Label, FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(label, 0); rowGrid.Children.Add(label);

            var upBox = new ComboBox
            {
                Width = 145, ItemsSource = ActionNames,
                SelectedIndex = ActionToIndex(item.ScrollUpAction),
                Margin = new Thickness(4, 0, 0, 0),
            };
            Grid.SetColumn(upBox, 1); rowGrid.Children.Add(upBox);
            _upBoxes.Add(upBox);
            upBox.SelectionChanged += (_, _) => ScheduleSave();

            var downBox = new ComboBox
            {
                Width = 145, ItemsSource = ActionNames,
                SelectedIndex = ActionToIndex(item.ScrollDownAction),
                Margin = new Thickness(4, 0, 0, 0),
            };
            Grid.SetColumn(downBox, 2); rowGrid.Children.Add(downBox);
            _downBoxes.Add(downBox);
            downBox.SelectionChanged += (_, _) => ScheduleSave();

            panel.Children.Add(rowGrid);
        }
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        var menuId = _menuId;
        var ups    = _upBoxes.Select(b => b.SelectedIndex).ToList();
        var downs  = _downBoxes.Select(b => b.SelectedIndex).ToList();

        await App.Config.UpdateAsync(cfg =>
        {
            var menu = cfg.Menus.FirstOrDefault(m => m.Id == menuId);
            if (menu is null) return;
            for (int i = 0; i < menu.Items.Count && i < ups.Count; i++)
            {
                menu.Items[i].ScrollUpAction   = IndexToAction(ups[i]);
                menu.Items[i].ScrollDownAction = IndexToAction(downs[i]);
            }
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
}

// ═══════════════════════════════════════════════════════════════════════════
// ThemeEditorPage — create and save custom themes
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class ThemeEditorPage : Page
{
    private TextBox    _nameBox = null!, _descBox = null!;
    private TextBlock  _saved   = null!;
    private ComboBox   _fontFamilyCombo = null!;
    private readonly Dictionary<string, TextBox>  _colorBoxes    = [];
    private readonly Dictionary<string, Button>   _colorSwatches = []; // swatch buttons open ColorPicker flyout
    private readonly Dictionary<string, TextBox>  _floatBoxes    = [];
    private SKXamlCanvas? _previewCanvas;

    // Color fields in display order: (property name, display label)
    private static readonly (string Prop, string Label)[] ColorFields =
    [
        ("AccentColor",             "Accent color"),
        ("SliceFill",               "Slice fill"),
        ("SliceFillHover",          "Slice fill (hover)"),
        ("SliceGradientInner",      "Gradient inner"),
        ("SliceGradientOuter",      "Gradient outer"),
        ("SliceGradientInnerHover", "Gradient inner (hover)"),
        ("SliceGradientOuterHover", "Gradient outer (hover)"),
        ("GlowColor",               "Glow color"),
        ("SliceStroke",             "Slice border"),
        ("SliceStrokeHover",        "Slice border (hover)"),
        ("RingBorderColor",         "Ring border (L2/L3)"),
        ("CenterFill",              "Center fill"),
        ("CenterStroke",            "Center border"),
        ("IconTint",                "Icon tint"),
        ("IconTintHover",           "Icon tint (hover)"),
        ("LabelColor",              "Label color"),
        ("LabelColorHover",         "Label color (hover)"),
        ("DimColor",                "Background dim color"),
    ];

    public ThemeEditorPage() => Build();

    private void Build()
    {
        // Two-column layout: color controls scroll left; preview pinned right.
        var outerGrid = new Grid();
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(236) });

        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 16, 32) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(UI.PageHeader("Theme Editor"));
        stack.Children.Add(UI.InfoCard(
            "Design a custom theme. Colors use #AARRGGBB format. " +
            "Gradient fields can be left empty to fall back to flat slice fill. " +
            "Saved themes appear in Appearance → Theme list immediately."));

        // ── Name / description ────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Theme identity"));
        var baseTheme = App.Themes.ActiveTheme;

        _nameBox = new TextBox { PlaceholderText = "My Theme", Text = "Custom " + baseTheme.Name };
        _descBox = new TextBox { PlaceholderText = "A short description", Text = "" };

        var identGrid = new Grid { ColumnSpacing = 12 };
        identGrid.ColumnDefinitions.Add(new ColumnDefinition());
        identGrid.ColumnDefinitions.Add(new ColumnDefinition());
        var nameStack = new StackPanel { Spacing = 4 };
        nameStack.Children.Add(new TextBlock { Text = "Name", FontSize = 12 });
        nameStack.Children.Add(_nameBox);
        var descStack = new StackPanel { Spacing = 4 };
        descStack.Children.Add(new TextBlock { Text = "Description", FontSize = 12 });
        descStack.Children.Add(_descBox);
        Grid.SetColumn(nameStack, 0); identGrid.Children.Add(nameStack);
        Grid.SetColumn(descStack, 1); identGrid.Children.Add(descStack);
        stack.Children.Add(identGrid);

        // ── Load from existing theme ──────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Start from"));
        var loadRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var themePicker = new ComboBox { Width = 200 };
        foreach (var n in App.Themes.AvailableThemes) themePicker.Items.Add(n);
        themePicker.SelectedItem = baseTheme.Name;
        var loadBtn = new Button { Content = "Load theme values" };
        loadBtn.Click += (_, _) =>
        {
            if (themePicker.SelectedItem is string name)
            {
                var t = App.Themes.Get(name);
                if (t is not null) PopulateFromTheme(t);
            }
        };
        loadRow.Children.Add(themePicker);
        loadRow.Children.Add(loadBtn);
        stack.Children.Add(loadRow);

        // ── Color fields ──────────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Colors  — click swatch to pick, or type #AARRGGBB"));

        var colorGrid = new Grid { ColumnSpacing = 10, RowSpacing = 8 };
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(165) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < ColorFields.Length; i++)
        {
            var (prop, label) = ColorFields[i];
            int col = (i % 2) * 3;
            int row = i / 2;
            if (colorGrid.RowDefinitions.Count <= row)
                colorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            string initVal = GetColorProp(baseTheme, prop);

            // Label
            var lbl = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, col); Grid.SetRow(lbl, row); colorGrid.Children.Add(lbl);

            // ColorPicker inside a Flyout — opened by clicking the swatch button
            var picker = new ColorPicker
            {
                Color                   = HexToColor(initVal),
                IsAlphaEnabled          = true,
                IsHexInputVisible       = false, // use our TextBox for hex; avoids format confusion
                IsAlphaTextInputVisible = true,
                Width                   = 300,
            };
            var flyout = new Flyout
            {
                Content                     = picker,
                Placement                   = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedLeft,
                ShouldConstrainToRootBounds = false,
            };

            var swatchBtn = new Button
            {
                Width        = 28,
                Height       = 28,
                Padding      = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                Background   = HexToBrush(initVal),
                Flyout       = flyout,
                VerticalAlignment = VerticalAlignment.Center,
            };
            _colorSwatches[prop] = swatchBtn;
            Grid.SetColumn(swatchBtn, col + 1); Grid.SetRow(swatchBtn, row); colorGrid.Children.Add(swatchBtn);

            // TextBox for direct hex editing
            var box = new TextBox { Text = initVal, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            _colorBoxes[prop] = box;
            Grid.SetColumn(box, col + 2); Grid.SetRow(box, row); colorGrid.Children.Add(box);

            // Two-way sync: TextBox ↔ ColorPicker.
            // `syncing` (per-field closure) breaks the feedback loop that would otherwise
            // occur when one side's update triggers the other's change event.
            bool syncing = false;

            box.TextChanged += (_, _) =>
            {
                if (syncing) return;
                swatchBtn.Background = HexToBrush(box.Text);
                var c = TryHexToColor(box.Text);
                if (c.HasValue)
                {
                    syncing = true;
                    picker.Color = c.Value;
                    syncing = false;
                }
                _previewCanvas?.Invalidate();
            };

            picker.ColorChanged += (_, a) =>
            {
                if (syncing) return;
                var c = a.NewColor;
                syncing = true;
                box.Text = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                syncing = false;
                swatchBtn.Background = new SolidColorBrush(ColorHelper.FromArgb(c.A, c.R, c.G, c.B));
            };
        }
        stack.Children.Add(colorGrid);

        // ── Float properties ──────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Other properties"));
        var floatGrid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        floatGrid.ColumnDefinitions.Add(new ColumnDefinition());
        floatGrid.ColumnDefinitions.Add(new ColumnDefinition());
        floatGrid.ColumnDefinitions.Add(new ColumnDefinition());
        floatGrid.ColumnDefinitions.Add(new ColumnDefinition());
        floatGrid.RowDefinitions.Add(new RowDefinition());

        (string Prop, string Label, string Val)[] floatFields =
        [
            ("SliceStrokeWidth",    "Stroke width",        baseTheme.SliceStrokeWidth.ToString("0.##")),
            ("LabelFontSize",       "Label font size",     baseTheme.LabelFontSize.ToString("0.##")),
            ("VolumeRingThickness", "Volume ring width",   baseTheme.VolumeRingThickness.ToString("0.##")),
            ("IconStrokeScale",     "Icon stroke scale",   baseTheme.IconStrokeScale.ToString("0.##")),
        ];
        for (int i = 0; i < floatFields.Length; i++)
        {
            var (prop, lbl, val) = floatFields[i];
            var colStack = new StackPanel { Spacing = 4 };
            colStack.Children.Add(new TextBlock { Text = lbl, FontSize = 12 });
            var tb = new TextBox { Text = val, FontSize = 12 };
            _floatBoxes[prop] = tb;
            colStack.Children.Add(tb);
            Grid.SetColumn(colStack, i); floatGrid.Children.Add(colStack);
        }
        stack.Children.Add(floatGrid);

        // ── Font family ───────────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Label font family"));
        _fontFamilyCombo = new ComboBox { Width = 260 };
        foreach (var f in new[]
        {
            "Segoe UI Variable", "Segoe UI", "Segoe UI Light",
            "Arial", "Calibri", "Consolas", "Tahoma", "Trebuchet MS",
            "Verdana", "Georgia", "Times New Roman", "Impact",
        })
            _fontFamilyCombo.Items.Add(f);
        _fontFamilyCombo.SelectedItem = baseTheme.LabelFontFamily;
        if (_fontFamilyCombo.SelectedItem is null) _fontFamilyCombo.SelectedIndex = 0;
        _fontFamilyCombo.SelectionChanged += (_, _) => _previewCanvas?.Invalidate();
        stack.Children.Add(_fontFamilyCombo);

        // ── Save ──────────────────────────────────────────────────────────
        var saveBtn = new Button
        {
            Content      = "Save theme",
            Background   = new SolidColorBrush(ColorHelper.FromArgb(220, 100, 80, 220)),
            Foreground   = new SolidColorBrush(Colors.White),
            Padding      = new Thickness(20, 8, 20, 8),
            CornerRadius = new CornerRadius(6),
        };
        saveBtn.Click += async (_, _) =>
        {
            BuildTheme();
            await ShowSavedBadge();
        };

        var saveApplyBtn = new Button
        {
            Content      = "Save and apply",
            Padding      = new Thickness(20, 8, 20, 8),
            CornerRadius = new CornerRadius(6),
        };
        saveApplyBtn.Click += async (_, _) =>
        {
            var name = BuildTheme();
            await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = name);
            await ShowSavedBadge();
        };

        _saved = UI.SavedBadge();
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 14, 0, 0) };
        saveRow.Children.Add(saveBtn);
        saveRow.Children.Add(saveApplyBtn);
        saveRow.Children.Add(_saved);
        stack.Children.Add(saveRow);

        // Populate initial values from base theme
        PopulateFromTheme(baseTheme);

        // Wire float/name boxes to preview
        _nameBox.TextChanged += (_, _) => _previewCanvas?.Invalidate();
        foreach (var fb in _floatBoxes.Values) fb.TextChanged += (_, _) => _previewCanvas?.Invalidate();

        scroll.Content = stack;
        Grid.SetColumn(scroll, 0);
        outerGrid.Children.Add(scroll);

        // Right column: live preview pinned — always visible while color fields scroll
        var rightPanel = new StackPanel { Margin = new Thickness(0, 28, 20, 24), Spacing = 4 };
        rightPanel.Children.Add(UI.SubHeader("Live preview"));
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

    private void PopulateFromTheme(AeroTheme t)
    {
        foreach (var (prop, _) in ColorFields)
        {
            if (!_colorBoxes.TryGetValue(prop, out var box)) continue;
            string val = GetColorProp(t, prop);
            // Setting box.Text triggers TextChanged which syncs the swatch button and picker.
            // Also update the swatch directly as a safety net (in case Text was already the same value).
            box.Text = val;
            if (_colorSwatches.TryGetValue(prop, out var btn)) btn.Background = HexToBrush(val);
        }
        if (_floatBoxes.TryGetValue("SliceStrokeWidth",    out var fb1)) fb1.Text = t.SliceStrokeWidth.ToString("0.##");
        if (_floatBoxes.TryGetValue("LabelFontSize",       out var fb2)) fb2.Text = t.LabelFontSize.ToString("0.##");
        if (_floatBoxes.TryGetValue("VolumeRingThickness", out var fb4)) fb4.Text = t.VolumeRingThickness.ToString("0.##");
        if (_floatBoxes.TryGetValue("IconStrokeScale",     out var fb5)) fb5.Text = t.IconStrokeScale.ToString("0.##");
        if (_fontFamilyCombo is not null)
        {
            _fontFamilyCombo.SelectedItem = t.LabelFontFamily;
            if (_fontFamilyCombo.SelectedItem is null) _fontFamilyCombo.SelectedIndex = 0;
        }
    }

    /// <summary>Builds and saves the theme from current field values. Returns the saved theme name.</summary>
    private string BuildTheme()
    {
        var t = BuildThemeFromFields();
        App.Themes.SaveUserTheme(t);
        return t.Name;
    }

    /// <summary>Builds a temporary AeroTheme from the current editor fields without saving it.</summary>
    private AeroTheme BuildThemeFromFields()
    {
        string name = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = "Custom";

        var t = new AeroTheme { Name = name, Description = _descBox.Text.Trim() };

        foreach (var (prop, _) in ColorFields)
        {
            if (_colorBoxes.TryGetValue(prop, out var box))
                SetColorProp(t, prop, box.Text.Trim());
        }

        if (_floatBoxes.TryGetValue("SliceStrokeWidth", out var fb1) &&
            float.TryParse(fb1.Text, out float strokeW)) t.SliceStrokeWidth = strokeW;
        if (_floatBoxes.TryGetValue("LabelFontSize", out var fb2) &&
            float.TryParse(fb2.Text, out float fontSize)) t.LabelFontSize = fontSize;
        if (_fontFamilyCombo?.SelectedItem is string fontFamily) t.LabelFontFamily = fontFamily;
        if (_floatBoxes.TryGetValue("VolumeRingThickness", out var fb4) &&
            float.TryParse(fb4.Text, out float volW) && volW > 0f) t.VolumeRingThickness = volW;
        if (_floatBoxes.TryGetValue("IconStrokeScale", out var fb5) &&
            float.TryParse(fb5.Text, out float iconScale) && iconScale > 0f) t.IconStrokeScale = iconScale;

        return t;
    }

    private void OnPreviewPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        AeroTheme theme;
        try   { theme = BuildThemeFromFields(); }
        catch { theme = App.Themes.ActiveTheme; }

        var appear  = App.Config.Current.Appearance;
        float w = e.Info.Width, h = e.Info.Height;
        float cx = w / 2f, cy = h / 2f;
        float minDim  = Math.Min(w, h);
        float outerR  = minDim * 0.42f;
        float innerR  = minDim * 0.18f;
        int   slices  = Math.Clamp(appear.SliceCount, 4, 12);
        float fullArc = 360f / slices;
        float gap     = appear.GapDegrees;
        float sweep   = fullArc - gap;
        float startOff = -90f - fullArc / 2f;

        using var bgPaint = new SKPaint { IsAntialias = true, Color = SKColors.Black.WithAlpha(60) };
        canvas.DrawCircle(cx, cy, outerR + 6f, bgPaint);

        using var paint = new SKPaint { IsAntialias = true };
        for (int i = 0; i < slices; i++)
        {
            float start    = startOff + i * fullArc + gap / 2f;
            float midAngle = startOff + i * fullArc + fullArc / 2f;
            float midRad   = midAngle * MathF.PI / 180f;
            bool  isHover  = i == 0;

            // Radial-style gradient from inner colour toward outer colour
            SKColor cInner = isHover && theme.SliceGradientInnerHover.Length > 0
                ? theme.ToSKColor(theme.SliceGradientInnerHover)
                : theme.SliceGradientInner.Length > 0
                    ? theme.ToSKColor(theme.SliceGradientInner)
                    : (isHover ? theme.ToSKColor(theme.SliceFillHover) : theme.ToSKColor(theme.SliceFill));
            SKColor cOuter = isHover && theme.SliceGradientOuterHover.Length > 0
                ? theme.ToSKColor(theme.SliceGradientOuterHover)
                : theme.SliceGradientOuter.Length > 0
                    ? theme.ToSKColor(theme.SliceGradientOuter)
                    : cInner;

            paint.Style = SKPaintStyle.Fill;
            if (theme.SliceGradientOuter.Length > 0 || (isHover && theme.SliceGradientOuterHover.Length > 0))
            {
                float gx1 = cx + MathF.Cos(midRad) * innerR;
                float gy1 = cy + MathF.Sin(midRad) * innerR;
                float gx2 = cx + MathF.Cos(midRad) * outerR;
                float gy2 = cy + MathF.Sin(midRad) * outerR;
                paint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(gx1, gy1), new SKPoint(gx2, gy2),
                    new[] { cInner, cOuter }, SKShaderTileMode.Clamp);
            }
            else
            {
                paint.Shader = null;
                paint.Color  = cInner;
            }

            using (var path = ThemePreviewSlicePath(cx, cy, outerR, innerR, start, sweep))
                canvas.DrawPath(path, paint);
            paint.Shader = null;

            paint.Style       = SKPaintStyle.Stroke;
            paint.StrokeWidth = isHover ? 2f : 0.8f;
            paint.Color       = isHover ? theme.ToSKColor(theme.SliceStrokeHover) : theme.ToSKColor(theme.SliceStroke);
            using (var path = ThemePreviewSlicePath(cx, cy, outerR, innerR, start, sweep))
                canvas.DrawPath(path, paint);
        }

        // Center circle
        paint.Style  = SKPaintStyle.Fill;
        paint.Shader = null;
        paint.Color  = theme.ToSKColor(theme.CenterFill);
        canvas.DrawCircle(cx, cy, innerR, paint);
        paint.Style       = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1f;
        paint.Color       = theme.ToSKColor(theme.CenterStroke);
        canvas.DrawCircle(cx, cy, innerR, paint);

        // Accent dot
        paint.Style = SKPaintStyle.Fill;
        paint.Color = theme.ToSKColor(theme.AccentColor);
        canvas.DrawCircle(cx, cy, 4f, paint);
    }

    private static SKPath ThemePreviewSlicePath(float cx, float cy,
        float outerR, float innerR, float start, float sweep)
    {
        var path = new SKPath();
        path.ArcTo(new SKRect(cx - outerR, cy - outerR, cx + outerR, cy + outerR), start, sweep, true);
        path.ArcTo(new SKRect(cx - innerR, cy - innerR, cx + innerR, cy + innerR), start + sweep, -sweep, false);
        path.Close();
        return path;
    }

    private async Task ShowSavedBadge()
    {
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2500);
        _saved.Visibility = Visibility.Collapsed;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string GetColorProp(AeroTheme t, string prop) => prop switch
    {
        "AccentColor"             => t.AccentColor,
        "SliceFill"               => t.SliceFill,
        "SliceFillHover"          => t.SliceFillHover,
        "SliceGradientInner"      => t.SliceGradientInner,
        "SliceGradientOuter"      => t.SliceGradientOuter,
        "SliceGradientInnerHover" => t.SliceGradientInnerHover,
        "SliceGradientOuterHover" => t.SliceGradientOuterHover,
        "GlowColor"               => t.GlowColor,
        "SliceStroke"             => t.SliceStroke,
        "SliceStrokeHover"        => t.SliceStrokeHover,
        "RingBorderColor"         => t.RingBorderColor,
        "CenterFill"              => t.CenterFill,
        "CenterStroke"            => t.CenterStroke,
        "IconTint"                => t.IconTint,
        "IconTintHover"           => t.IconTintHover,
        "LabelColor"              => t.LabelColor,
        "LabelColorHover"         => t.LabelColorHover,
        "DimColor"                => t.DimColor,
        _                         => "",
    };

    private static void SetColorProp(AeroTheme t, string prop, string val)
    {
        switch (prop)
        {
            case "AccentColor":             t.AccentColor             = val; break;
            case "SliceFill":               t.SliceFill               = val; break;
            case "SliceFillHover":          t.SliceFillHover          = val; break;
            case "SliceGradientInner":      t.SliceGradientInner      = val; break;
            case "SliceGradientOuter":      t.SliceGradientOuter      = val; break;
            case "SliceGradientInnerHover": t.SliceGradientInnerHover = val; break;
            case "SliceGradientOuterHover": t.SliceGradientOuterHover = val; break;
            case "GlowColor":               t.GlowColor               = val; break;
            case "SliceStroke":             t.SliceStroke             = val; break;
            case "SliceStrokeHover":        t.SliceStrokeHover        = val; break;
            case "RingBorderColor":         t.RingBorderColor         = val; break;
            case "CenterFill":              t.CenterFill              = val; break;
            case "CenterStroke":            t.CenterStroke            = val; break;
            case "IconTint":                t.IconTint                = val; break;
            case "IconTintHover":           t.IconTintHover           = val; break;
            case "LabelColor":              t.LabelColor              = val; break;
            case "LabelColorHover":         t.LabelColorHover         = val; break;
            case "DimColor":                t.DimColor                = val; break;
        }
    }

    private static SolidColorBrush HexToBrush(string hex)
    {
        var c = TryHexToColor(hex);
        return c.HasValue
            ? new SolidColorBrush(c.Value)
            : new SolidColorBrush(ColorHelper.FromArgb(60, 140, 130, 200));
    }

    /// <summary>Parses #AARRGGBB (or #RRGGBB) to Windows.UI.Color. Returns null on failure.</summary>
    private static Windows.UI.Color? TryHexToColor(string hex)
    {
        try
        {
            hex = hex.TrimStart('#');
            if (hex.Length == 6) hex = "FF" + hex;
            if (hex.Length == 8 && uint.TryParse(hex,
                    System.Globalization.NumberStyles.HexNumber, null, out var argb))
                return ColorHelper.FromArgb((byte)(argb >> 24), (byte)(argb >> 16),
                                            (byte)(argb >> 8),  (byte)argb);
        }
        catch { }
        return null;
    }

    /// <summary>Converts #AARRGGBB to Windows.UI.Color, falling back to an opaque purple on failure.</summary>
    private static Windows.UI.Color HexToColor(string hex)
        => TryHexToColor(hex) ?? ColorHelper.FromArgb(255, 100, 90, 200);
}

// ═══════════════════════════════════════════════════════════════════════════
// AdvancedPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class AdvancedPage : Page
{
    private ToggleSwitch _thinning = null!, _partialArc = null!, _debugLog = null!;
    private TextBlock    _saved    = null!;
    private DispatcherTimer? _saveTimer;

    public AdvancedPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 6 };
        var cfg    = App.Config.Current.Appearance;

        stack.Children.Add(UI.PageHeader("Advanced"));
        stack.Children.Add(UI.InfoCard(
            "These settings control 3-level menu depth and arc layout. " +
            "Defaults are tuned for the best out-of-box experience."));

        // ── Multi-level ring rendering ─────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Multi-level rings"));

        _thinning = new ToggleSwitch
        {
            Header     = "Dynamic ring thinning",
            IsOn       = cfg.DynamicRingThinning,
            OnContent  = "On: L2 ring narrows when an L3 ring is also visible",
            OffContent = "Off: all rings stay at full width",
        };

        _partialArc = new ToggleSwitch
        {
            Header     = "Partial arc submenus",
            IsOn       = cfg.PartialArcSubMenu,
            OnContent  = "On: child rings fan out only around the parent slice angle",
            OffContent = "Off: child rings are always full 360°",
        };

        stack.Children.Add(_thinning);
        stack.Children.Add(_partialArc);

        stack.Children.Add(UI.InfoCard(
            "Partial arc: with 4 items the ring fans around the parent, " +
            "making the parent-child relationship visually clear. " +
            "With 8+ items it automatically falls back to full 360°."));

        // ── Developer ─────────────────────────────────────────────────────
        stack.Children.Add(UI.SubHeader("Developer"));
        _debugLog = new ToggleSwitch
        {
            Header     = "Verbose debug logging",
            IsOn       = App.Config.Current.Behavior.EnableDebugLogging,
            OnContent  = "On: writes DEBUG entries to %AppData%\\AeroDial\\aerodial.log",
            OffContent = "Off",
        };
        stack.Children.Add(_debugLog);

        // ── Save ──────────────────────────────────────────────────────────
        var saveBtn = UI.SaveButton(); _saved = UI.SavedBadge();
        saveBtn.Click += Save;
        var saveRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 14, 0, 0) };
        saveRow.Children.Add(saveBtn);
        saveRow.Children.Add(_saved);
        stack.Children.Add(saveRow);

        _thinning.Toggled   += (_, _) => ScheduleSave();
        _partialArc.Toggled += (_, _) => ScheduleSave();
        _debugLog.Toggled   += (_, _) => ScheduleSave();

        scroll.Content = stack;
        Content = scroll;
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        await App.Config.UpdateAsync(cfg =>
        {
            cfg.Appearance.DynamicRingThinning = _thinning.IsOn;
            cfg.Appearance.PartialArcSubMenu   = _partialArc.IsOn;
            cfg.Behavior.EnableDebugLogging    = _debugLog.IsOn;
        });
        Logger.SetDebugMode(App.Config.Current.Behavior.EnableDebugLogging);
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }

    private void ScheduleSave()
    {
        _saveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Stop();
        _saveTimer.Tick -= OnTick;
        _saveTimer.Tick += OnTick;
        _saveTimer.Start();
    }

    private void OnTick(object? s, object e) { _saveTimer!.Stop(); Save(null!, null!); }
}

// ═══════════════════════════════════════════════════════════════════════════
// AboutPage
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class AboutPage : Page
{
    public AboutPage() => Content = AboutContent.Build();
}

// ═══════════════════════════════════════════════════════════════════════════
// ProfilesPage — per-app menu profiles (context-aware dial)
// ═══════════════════════════════════════════════════════════════════════════

public sealed partial class ProfilesPage : Page
{
    // Working copy — not written to config until Save.
    private List<AppProfileConfig> _profiles = [];
    private StackPanel _rows  = null!;
    private TextBlock   _saved = null!;

    public ProfilesPage() => Build();

    private void Build()
    {
        var scroll = new ScrollViewer { Padding = new Thickness(32, 24, 32, 24) };
        var stack  = new StackPanel { Spacing = 8 };

        stack.Children.Add(UI.PageHeader("App Profiles"));
        stack.Children.Add(UI.InfoCard(
            "Assign a menu to an app. When that app is in the foreground and you open the dial, " +
            "it shows the assigned menu instead of the default. Apps are matched by process name."));

        _profiles = App.Config.Current.AppProfiles
            .Select(p => new AppProfileConfig { ProcessName = p.ProcessName, MenuId = p.MenuId })
            .ToList();

        _rows = new StackPanel { Spacing = 6, Margin = new Thickness(0, 4, 0, 4) };
        stack.Children.Add(_rows);
        RebuildRows();

        var addBtn = new Button { Content = "Add profile" };
        addBtn.Click += (_, _) =>
        {
            _profiles.Add(new AppProfileConfig { ProcessName = "", MenuId = DefaultMenuId() });
            RebuildRows();
        };

        var detectBtn    = new Button { Content = "Add from running app" };
        var detectFlyout = new MenuFlyout();
        detectBtn.Flyout = detectFlyout;
        detectFlyout.Opening += (_, _) => PopulateRunningApps(detectFlyout);

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        btnRow.Children.Add(addBtn);
        btnRow.Children.Add(detectBtn);
        stack.Children.Add(btnRow);

        var saveBtn = UI.SaveButton();
        saveBtn.Margin = new Thickness(0, 16, 0, 0);
        saveBtn.Click += Save;
        _saved = UI.SavedBadge();
        _saved.Margin = new Thickness(0, 16, 0, 0);
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        saveRow.Children.Add(saveBtn);
        saveRow.Children.Add(_saved);
        stack.Children.Add(saveRow);

        scroll.Content = stack;
        Content = scroll;
    }

    private static string DefaultMenuId()
        => App.Config.Current.Menus.FirstOrDefault()?.Id ?? "default";

    private void RebuildRows()
    {
        _rows.Children.Clear();

        if (_profiles.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text       = "No app profiles yet. Add one below.",
                FontSize   = 13,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(160, 200, 200, 220)),
            });
            return;
        }

        var menus = App.Config.Current.Menus;
        foreach (var prof in _profiles)
        {
            var row = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                Spacing           = 8,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var nameBox = new TextBox
            {
                Text            = prof.ProcessName,
                PlaceholderText = "process name (e.g. acad)",
                Width           = 220,
            };
            nameBox.TextChanged += (_, _) => prof.ProcessName = nameBox.Text.Trim();

            var arrow = new TextBlock { Text = "→", VerticalAlignment = VerticalAlignment.Center, FontSize = 15 };

            var combo = new ComboBox { Width = 200 };
            int selected = -1;
            for (int m = 0; m < menus.Count; m++)
            {
                combo.Items.Add(new ComboBoxItem { Content = menus[m].Name, Tag = menus[m].Id });
                if (menus[m].Id == prof.MenuId) selected = m;
            }
            combo.SelectedIndex = selected >= 0 ? selected : 0;
            if (selected < 0 && menus.Count > 0) prof.MenuId = menus[0].Id; // fell back — keep model in sync
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is ComboBoxItem ci && ci.Tag is string id) prof.MenuId = id;
            };

            var del = new Button { Content = "✕", Width = 40 };
            del.Click += (_, _) => { _profiles.Remove(prof); RebuildRows(); };

            row.Children.Add(nameBox);
            row.Children.Add(arrow);
            row.Children.Add(combo);
            row.Children.Add(del);
            _rows.Children.Add(row);
        }
    }

    private void PopulateRunningApps(MenuFlyout flyout)
    {
        flyout.Items.Clear();
        var names = GetRunningAppProcessNames();
        if (names.Count == 0)
        {
            flyout.Items.Add(new MenuFlyoutItem { Text = "No running apps found", IsEnabled = false });
            return;
        }
        foreach (var name in names)
        {
            var mi = new MenuFlyoutItem { Text = name };
            mi.Click += (_, _) =>
            {
                if (!_profiles.Any(p => string.Equals(p.ProcessName, name, StringComparison.OrdinalIgnoreCase)))
                    _profiles.Add(new AppProfileConfig { ProcessName = name, MenuId = DefaultMenuId() });
                RebuildRows();
            };
            flyout.Items.Add(mi);
        }
    }

    // Running processes that own a visible top-level window, by process name.
    private static List<string> GetRunningAppProcessNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in System.Diagnostics.Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                    names.Add(p.ProcessName);
            }
            catch { /* protected/elevated process — skip */ }
            finally { p.Dispose(); }
        }
        names.Remove(AppConstants.AppName); // don't offer AeroDial itself
        return names.ToList();
    }

    private async void Save(object s, RoutedEventArgs e)
    {
        var cleaned = _profiles
            .Where(p => !string.IsNullOrWhiteSpace(p.ProcessName))
            .Select(p => new AppProfileConfig { ProcessName = p.ProcessName.Trim(), MenuId = p.MenuId })
            .ToList();

        await App.Config.UpdateAsync(cfg => cfg.AppProfiles = cleaned);
        _saved.Visibility = Visibility.Visible;
        await Task.Delay(2000);
        _saved.Visibility = Visibility.Collapsed;
    }
}
