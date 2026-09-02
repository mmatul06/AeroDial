// AeroDial — ThemeEditorPage.cs
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

        stack.Children.Add(PageKit.PageHeader("Theme Editor"));
        stack.Children.Add(PageKit.InfoCard(
            "Design a custom theme. Colors use #AARRGGBB format. " +
            "Gradient fields can be left empty to fall back to flat slice fill. " +
            "Saved themes appear in Appearance → Theme list immediately."));

        // ── Name / description ────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Theme identity"));
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
        stack.Children.Add(PageKit.SubHeader("Start from"));
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
        stack.Children.Add(PageKit.SubHeader("Colors  — click swatch to pick, or type #AARRGGBB"));

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
        stack.Children.Add(PageKit.SubHeader("Other properties"));
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
        stack.Children.Add(PageKit.SubHeader("Label font family"));
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

        _saved = PageKit.SavedBadge();
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
        rightPanel.Children.Add(PageKit.SubHeader("Live preview"));
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
