// AeroDial — ThemeEditorPanel.cs
// Live theme editor hosted inside ThemesPage. Edits a copy of the loaded theme;
// Save writes a user theme (built-in themes are saved as a new copy) and raises Saved.
//
// Layout: the color and numeric fields scroll in the left column; the ring preview,
// identity, label font and the save buttons sit in a right column that stays put, so
// every edit is visible while scrolling through the colors.

using AeroDial.Themes;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace AeroDial.UI.Views.Pages;

public sealed partial class ThemeEditorPanel : UserControl
{
    /// <summary>Raised with the saved theme name after a successful save.</summary>
    public event Action<string>? Saved;

    private TextBox    _nameBox = null!, _descBox = null!;
    private TextBlock  _saved   = null!, _sourceNote = null!;
    private Button     _saveBtn = null!, _saveApplyBtn = null!;
    private ComboBox   _fontFamilyCombo = null!;
    private readonly Dictionary<string, TextBox>   _colorBoxes    = [];
    private readonly Dictionary<string, Button>    _colorSwatches = [];
    private readonly Dictionary<string, NumberBox> _floatBoxes    = [];
    private SKXamlCanvas? _previewCanvas;
    private bool _loading;
    private bool _sourceIsBuiltIn;

    // Sized so the fields column fits beside the pinned preview column at the default
    // window width (settings window 1120 logical px, nav rail 274, theme list 241, side 232).
    private const double LabelColumnWidth = 140;
    private const double SwatchColumnWidth = 28;
    private const double HexBoxWidth = 104;
    private const double SideColumnWidth = 232;
    // A number box spans the swatch column, its gap and the hex box so the right edges line up.
    private const double NumberBoxWidth = SwatchColumnWidth + 10 + HexBoxWidth;

    // Color fields in display order: (property name, display label, group caption)
    private static readonly (string Prop, string Label, string Group)[] ColorFields =
    [
        ("AccentColor",             "Accent",                 "Slices"),
        ("SliceFill",               "Slice fill",             "Slices"),
        ("SliceFillHover",          "Slice fill (hover)",     "Slices"),
        ("SliceGradientInner",      "Gradient inner",         "Slices"),
        ("SliceGradientOuter",      "Gradient outer",         "Slices"),
        ("SliceGradientInnerHover", "Gradient inner (hover)", "Slices"),
        ("SliceGradientOuterHover", "Gradient outer (hover)", "Slices"),
        ("GlowColor",               "Glow",                   "Slices"),
        ("SliceStroke",             "Slice border",           "Slices"),
        ("SliceStrokeHover",        "Slice border (hover)",   "Slices"),
        ("RingBorderColor",         "Ring border (L2/L3)",    "Center and rings"),
        ("CenterFill",              "Center fill",            "Center and rings"),
        ("CenterStroke",            "Center border",          "Center and rings"),
        ("IconTint",                "Icon tint",              "Icons and labels"),
        ("IconTintHover",           "Icon tint (hover)",      "Icons and labels"),
        ("LabelColor",              "Label",                  "Icons and labels"),
        ("LabelColorHover",         "Label (hover)",          "Icons and labels"),
        ("DimColor",                "Background dim",         "Screen"),
    ];

    // Numeric fields: (property name, display label, spin step)
    private static readonly (string Prop, string Label, double Step)[] FloatFields =
    [
        ("SliceStrokeWidth",    "Border width",      0.5),
        ("LabelFontSize",       "Label font size",   1),
        ("VolumeRingThickness", "Volume ring width", 1),
    ];

    public ThemeEditorPanel() => Build();

    private void Build()
    {
        var root = new Grid();
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SideColumnWidth) });

        // ── Left: scrolling fields ────────────────────────────────────────
        var scroll = new ScrollViewer { Padding = new Thickness(20, 24, 12, 32) };
        var stack  = new StackPanel { Spacing = 6 };

        stack.Children.Add(PageKit.PageHeader("Edit theme"));
        _sourceNote = Ui.Hint("");
        stack.Children.Add(_sourceNote);

        // ── Color fields ──────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Colors"));
        stack.Children.Add(Ui.Hint("Click a swatch to pick, or type #AARRGGBB. Gradient fields may be empty to use the flat slice fill.", 11));

        var colorGrid = new Grid { ColumnSpacing = 10, RowSpacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumnWidth) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SwatchColumnWidth) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        int row = 0;
        string? group = null;
        foreach (var (prop, label, grp) in ColorFields)
        {
            if (grp != group)
            {
                // Small caption when the group changes so 18 rows read as four short lists.
                group = grp;
                colorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var cap = Ui.Hint(grp, 11);
                cap.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                cap.Margin = new Thickness(0, row == 0 ? 2 : 10, 0, 0);
                Grid.SetRow(cap, row); Grid.SetColumnSpan(cap, 3); colorGrid.Children.Add(cap);
                row++;
            }

            colorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0); Grid.SetRow(lbl, row); colorGrid.Children.Add(lbl);

            var picker = new ColorPicker
            {
                IsAlphaEnabled          = true,
                IsHexInputVisible       = false,
                IsAlphaTextInputVisible = true,
                Width                   = 300,
            };
            var flyout = new Flyout
            {
                Content                     = picker,
                Placement                   = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.RightEdgeAlignedTop,
                ShouldConstrainToRootBounds = false,
            };
            var swatchBtn = new Button
            {
                Width = 28, Height = 28, Padding = new Thickness(0),
                CornerRadius = new CornerRadius(4),
                BorderBrush = Ui.CardStroke, BorderThickness = new Thickness(1),
                Flyout = flyout, VerticalAlignment = VerticalAlignment.Center,
            };
            _colorSwatches[prop] = swatchBtn;
            Grid.SetColumn(swatchBtn, 1); Grid.SetRow(swatchBtn, row); colorGrid.Children.Add(swatchBtn);

            var box = new TextBox
            {
                FontSize = 12, Width = HexBoxWidth,
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas"),
            };
            _colorBoxes[prop] = box;
            Grid.SetColumn(box, 2); Grid.SetRow(box, row); colorGrid.Children.Add(box);

            // Two-way sync TextBox <-> ColorPicker; `syncing` breaks the feedback loop.
            bool syncing = false;
            box.TextChanged += (_, _) =>
            {
                if (syncing) return;
                swatchBtn.Background = HexToBrush(box.Text);
                var c = TryHexToColor(box.Text);
                if (c.HasValue) { syncing = true; picker.Color = c.Value; syncing = false; }
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
                _previewCanvas?.Invalidate();
            };
            row++;
        }
        stack.Children.Add(colorGrid);

        // ── Numeric properties: one per row, spin boxes aligned with the hex fields ──
        stack.Children.Add(PageKit.SubHeader("Other properties"));
        var floatGrid = new Grid { ColumnSpacing = 10, RowSpacing = 6 };
        floatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(LabelColumnWidth) });
        floatGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int i = 0; i < FloatFields.Length; i++)
        {
            var (prop, label, step) = FloatFields[i];
            floatGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0); Grid.SetRow(lbl, i); floatGrid.Children.Add(lbl);

            var nb = new NumberBox
            {
                Width = NumberBoxWidth, FontSize = 12,
                Minimum = 0, SmallChange = step, LargeChange = step * 4,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                VerticalAlignment = VerticalAlignment.Center,
            };
            nb.ValueChanged += (_, _) => _previewCanvas?.Invalidate();
            _floatBoxes[prop] = nb;
            Grid.SetColumn(nb, 1); Grid.SetRow(nb, i); floatGrid.Children.Add(nb);
        }
        stack.Children.Add(floatGrid);

        scroll.Content = stack;
        Grid.SetColumn(scroll, 0);
        root.Children.Add(scroll);

        // ── Right: pinned preview, identity, font, save ───────────────────
        var side = new Grid { Padding = new Thickness(8, 24, 20, 24) };
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        side.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        side.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _previewCanvas = new SKXamlCanvas
        {
            Width = 200, Height = 200,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 12, 0, 4),
        };
        _previewCanvas.PaintSurface += OnPreviewPaint;
        Grid.SetRow(_previewCanvas, 0);
        side.Children.Add(_previewCanvas);

        var sideFields = new StackPanel { Spacing = 6 };
        sideFields.Children.Add(PageKit.SubHeader("Identity"));
        _nameBox = new TextBox { PlaceholderText = "My theme", Header = "Name" };
        _descBox = new TextBox { PlaceholderText = "A short description", Header = "Description" };
        sideFields.Children.Add(_nameBox);
        sideFields.Children.Add(_descBox);

        sideFields.Children.Add(PageKit.SubHeader("Label font"));
        _fontFamilyCombo = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var f in new[]
        {
            "Segoe UI Variable", "Segoe UI", "Segoe UI Light",
            "Arial", "Calibri", "Consolas", "Tahoma", "Trebuchet MS",
            "Verdana", "Georgia", "Times New Roman", "Impact",
        })
            _fontFamilyCombo.Items.Add(f);
        _fontFamilyCombo.SelectionChanged += (_, _) => _previewCanvas?.Invalidate();
        sideFields.Children.Add(_fontFamilyCombo);

        var sideScroll = new ScrollViewer { Content = sideFields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(sideScroll, 1);
        side.Children.Add(sideScroll);

        // ── Save ──────────────────────────────────────────────────────────
        _saveBtn = new Button
        {
            Content = "Save",
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _saveBtn.Click += async (_, _) => { var n = SaveTheme(); if (n is not null) await ShowSavedBadge(); };

        _saveApplyBtn = new Button { Content = "Save and apply", HorizontalAlignment = HorizontalAlignment.Stretch };
        _saveApplyBtn.Click += async (_, _) =>
        {
            var name = SaveTheme();
            if (name is null) return;
            await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = name);
            await ShowSavedBadge();
        };

        _saved = PageKit.SavedBadge();
        _saved.HorizontalAlignment = HorizontalAlignment.Center;
        var saveCol = new StackPanel { Spacing = 8, Margin = new Thickness(0, 16, 0, 0) };
        saveCol.Children.Add(_saveBtn);
        saveCol.Children.Add(_saveApplyBtn);
        saveCol.Children.Add(_saved);
        Grid.SetRow(saveCol, 2);
        side.Children.Add(saveCol);

        Grid.SetColumn(side, 1);
        root.Children.Add(side);

        _nameBox.TextChanged += (_, _) => _previewCanvas?.Invalidate();

        Content = root;
    }

    // ── Load / save ───────────────────────────────────────────────────────

    /// <summary>Fills the editor from a theme. Built-in themes save as a new user theme.</summary>
    public void Load(AeroTheme t)
    {
        _loading = true;
        _sourceIsBuiltIn = t.IsBuiltIn;
        _nameBox.Text = t.IsBuiltIn ? t.Name + " copy" : t.Name;
        _descBox.Text = t.Description;
        _sourceNote.Text = t.IsBuiltIn
            ? $"{t.Name} is built in. Your changes will be saved as a new theme."
            : $"Editing {t.Name}.";
        _saveBtn.Content = t.IsBuiltIn ? "Save as new theme" : "Save";

        foreach (var (prop, _, _) in ColorFields)
        {
            if (!_colorBoxes.TryGetValue(prop, out var box)) continue;
            string val = GetColorProp(t, prop);
            box.Text = val;
            if (_colorSwatches.TryGetValue(prop, out var btn)) btn.Background = HexToBrush(val);
        }
        SetFloat("SliceStrokeWidth",    t.SliceStrokeWidth);
        SetFloat("LabelFontSize",       t.LabelFontSize);
        SetFloat("VolumeRingThickness", t.VolumeRingThickness);
        _fontFamilyCombo.SelectedItem = t.LabelFontFamily;
        if (_fontFamilyCombo.SelectedItem is null) _fontFamilyCombo.SelectedIndex = 0;
        _loading = false;
        _previewCanvas?.Invalidate();
    }

    private void SetFloat(string prop, float value)
    {
        if (_floatBoxes.TryGetValue(prop, out var nb)) nb.Value = value;
    }

    /// <summary>Value of a numeric field, or null when it is empty (NumberBox reports NaN).</summary>
    private float? GetFloat(string prop)
        => _floatBoxes.TryGetValue(prop, out var nb) && !double.IsNaN(nb.Value) ? (float)nb.Value : null;

    /// <summary>Builds and saves the theme from current field values. Returns the saved name, or null.</summary>
    private string? SaveTheme()
    {
        var t = BuildThemeFromFields();
        if (_sourceIsBuiltIn && App.Themes.Get(t.Name)?.IsBuiltIn == true)
        {
            _sourceNote.Text = "Choose a different name: built-in themes cannot be overwritten.";
            return null;
        }
        App.Themes.SaveUserTheme(t);
        _sourceIsBuiltIn = false;
        _saveBtn.Content = "Save";
        _sourceNote.Text = $"Editing {t.Name}.";
        Saved?.Invoke(t.Name);
        return t.Name;
    }

    private AeroTheme BuildThemeFromFields()
    {
        string name = _nameBox.Text.Trim();
        if (string.IsNullOrEmpty(name)) name = "Custom";

        var t = new AeroTheme { Name = name, Description = _descBox.Text.Trim() };

        foreach (var (prop, _, _) in ColorFields)
            if (_colorBoxes.TryGetValue(prop, out var box)) SetColorProp(t, prop, box.Text.Trim());

        if (GetFloat("SliceStrokeWidth")    is { } strokeW)         t.SliceStrokeWidth     = strokeW;
        if (GetFloat("LabelFontSize")       is { } fontSize)        t.LabelFontSize        = fontSize;
        if (GetFloat("VolumeRingThickness") is { } volW && volW > 0f) t.VolumeRingThickness = volW;
        if (_fontFamilyCombo?.SelectedItem is string fontFamily) t.LabelFontFamily = fontFamily;
        return t;
    }

    // ── Preview ───────────────────────────────────────────────────────────

    private void OnPreviewPaint(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        if (_loading) return;

        AeroTheme theme;
        try   { theme = BuildThemeFromFields(); }
        catch { theme = App.Themes.ActiveTheme; }

        var appear  = App.Config.Current.Appearance;
        float w = e.Info.Width, h = e.Info.Height;
        float cx = w / 2f, cy = h / 2f;
        float minDim  = Math.Min(w, h);
        float outerR  = minDim * 0.42f;
        float innerR  = minDim * 0.18f;
        int   slices  = Math.Clamp(appear.SliceCount, 3, 12);
        float fullArc = 360f / slices;
        float gap     = appear.GapDegrees;
        float sweep   = fullArc - gap;
        float startOff = -90f - fullArc / 2f;

        using var bgPaint = new SKPaint { IsAntialias = true, Color = theme.ToSKColor(theme.DimColor) };
        canvas.DrawCircle(cx, cy, outerR + 6f, bgPaint);

        using var paint = new SKPaint { IsAntialias = true };
        for (int i = 0; i < slices; i++)
        {
            float start   = startOff + i * fullArc + gap / 2f;
            bool  isHover = i == 0;

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
            float gradPos = Math.Clamp(innerR / outerR, 0f, 0.95f);
            using (var shader = SKShader.CreateRadialGradient(new SKPoint(cx, cy), outerR,
                       [cInner, cOuter], [gradPos, 1f], SKShaderTileMode.Clamp))
            {
                paint.Shader = shader;
                using var path = SlicePath(cx, cy, outerR, innerR, start, sweep);
                canvas.DrawPath(path, paint);
                paint.Shader = null;
            }

            paint.Style       = SKPaintStyle.Stroke;
            paint.StrokeWidth = isHover ? 2f : Math.Max(theme.SliceStrokeWidth, 0.5f);
            paint.Color       = isHover ? theme.ToSKColor(theme.SliceStrokeHover) : theme.ToSKColor(theme.SliceStroke);
            using (var path = SlicePath(cx, cy, outerR, innerR, start, sweep))
                canvas.DrawPath(path, paint);
        }

        paint.Style  = SKPaintStyle.Fill;
        paint.Shader = null;
        paint.Color  = theme.ToSKColor(theme.CenterFill);
        canvas.DrawCircle(cx, cy, innerR, paint);
        paint.Style       = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1f;
        paint.Color       = theme.ToSKColor(theme.CenterStroke);
        canvas.DrawCircle(cx, cy, innerR, paint);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = theme.ToSKColor(theme.AccentColor);
        canvas.DrawCircle(cx, cy, 4f, paint);
    }

    private static SKPath SlicePath(float cx, float cy, float outerR, float innerR, float start, float sweep)
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

    private static Brush HexToBrush(string hex)
    {
        var c = TryHexToColor(hex);
        return c.HasValue ? new SolidColorBrush(c.Value) : Ui.SubtleFill;
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
}
