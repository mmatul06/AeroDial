// AeroDial — ThemeEditorPanel.cs
// Live theme editor hosted inside ThemesPage. Edits a copy of the loaded theme;
// Save writes a user theme (built-in themes are saved as a new copy) and raises Saved.

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
    private readonly Dictionary<string, TextBox> _colorBoxes    = [];
    private readonly Dictionary<string, Button>  _colorSwatches = [];
    private readonly Dictionary<string, TextBox> _floatBoxes    = [];
    private SKXamlCanvas? _previewCanvas;
    private bool _loading;
    private bool _sourceIsBuiltIn;

    // Color fields in display order: (property name, display label)
    private static readonly (string Prop, string Label)[] ColorFields =
    [
        ("AccentColor",             "Accent"),
        ("SliceFill",               "Slice fill"),
        ("SliceFillHover",          "Slice fill (hover)"),
        ("SliceGradientInner",      "Gradient inner"),
        ("SliceGradientOuter",      "Gradient outer"),
        ("SliceGradientInnerHover", "Gradient inner (hover)"),
        ("SliceGradientOuterHover", "Gradient outer (hover)"),
        ("GlowColor",               "Glow"),
        ("SliceStroke",             "Slice border"),
        ("SliceStrokeHover",        "Slice border (hover)"),
        ("RingBorderColor",         "Ring border (L2/L3)"),
        ("CenterFill",              "Center fill"),
        ("CenterStroke",            "Center border"),
        ("IconTint",                "Icon tint"),
        ("IconTintHover",           "Icon tint (hover)"),
        ("LabelColor",              "Label"),
        ("LabelColorHover",         "Label (hover)"),
        ("DimColor",                "Background dim"),
    ];

    public ThemeEditorPanel() => Build();

    private void Build()
    {
        // Single scrolling column (this panel sits beside the theme list, so width is limited).
        var scroll = new ScrollViewer { Padding = new Thickness(8, 24, 24, 32) };
        var stack  = new StackPanel { Spacing = 6 };

        // Header row: title + live preview side by side
        var headRow = new Grid { ColumnSpacing = 16 };
        headRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headText = new StackPanel { Spacing = 4 };
        headText.Children.Add(PageKit.PageHeader("Edit theme"));
        _sourceNote = Ui.Hint("");
        headText.Children.Add(_sourceNote);

        // ── Name / description ────────────────────────────────────────────
        headText.Children.Add(PageKit.SubHeader("Identity"));
        _nameBox = new TextBox { PlaceholderText = "My theme", Header = "Name" };
        _descBox = new TextBox { PlaceholderText = "A short description", Header = "Description" };
        headText.Children.Add(_nameBox);
        headText.Children.Add(_descBox);
        Grid.SetColumn(headText, 0);
        headRow.Children.Add(headText);

        _previewCanvas = new SKXamlCanvas
        {
            Width = 180, Height = 180,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 0, 0),
        };
        _previewCanvas.PaintSurface += OnPreviewPaint;
        Grid.SetColumn(_previewCanvas, 1);
        headRow.Children.Add(_previewCanvas);
        stack.Children.Add(headRow);

        // ── Color fields ──────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Colors"));
        stack.Children.Add(Ui.Hint("Click a swatch to pick, or type #AARRGGBB. Gradient fields may be empty to use the flat slice fill.", 11));

        var colorGrid = new Grid { ColumnSpacing = 10, RowSpacing = 6, Margin = new Thickness(0, 4, 0, 0) };
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        for (int i = 0; i < ColorFields.Length; i++)
        {
            var (prop, label) = ColorFields[i];
            int col = 0;
            int row = i;
            if (colorGrid.RowDefinitions.Count <= row)
                colorGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lbl = new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, col); Grid.SetRow(lbl, row); colorGrid.Children.Add(lbl);

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
                Placement                   = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.TopEdgeAlignedLeft,
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
            Grid.SetColumn(swatchBtn, col + 1); Grid.SetRow(swatchBtn, row); colorGrid.Children.Add(swatchBtn);

            var box = new TextBox { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            _colorBoxes[prop] = box;
            Grid.SetColumn(box, col + 2); Grid.SetRow(box, row); colorGrid.Children.Add(box);

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
        }
        stack.Children.Add(colorGrid);

        // ── Float properties ──────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Other properties"));
        var floatGrid = new Grid { ColumnSpacing = 12, RowSpacing = 6 };
        for (int i = 0; i < 3; i++) floatGrid.ColumnDefinitions.Add(new ColumnDefinition());
        floatGrid.RowDefinitions.Add(new RowDefinition());
        (string Prop, string Label)[] floatFields =
        [
            ("SliceStrokeWidth",    "Border width"),
            ("LabelFontSize",       "Label font size"),
            ("VolumeRingThickness", "Volume ring width"),
        ];
        for (int i = 0; i < floatFields.Length; i++)
        {
            var (prop, lbl) = floatFields[i];
            var colStack = new StackPanel { Spacing = 4 };
            colStack.Children.Add(new TextBlock { Text = lbl, FontSize = 12 });
            var tb = new TextBox { FontSize = 12 };
            _floatBoxes[prop] = tb;
            colStack.Children.Add(tb);
            Grid.SetColumn(colStack, i); floatGrid.Children.Add(colStack);
        }
        stack.Children.Add(floatGrid);

        // ── Font family ───────────────────────────────────────────────────
        stack.Children.Add(PageKit.SubHeader("Label font"));
        _fontFamilyCombo = new ComboBox { Width = 260 };
        foreach (var f in new[]
        {
            "Segoe UI Variable", "Segoe UI", "Segoe UI Light",
            "Arial", "Calibri", "Consolas", "Tahoma", "Trebuchet MS",
            "Verdana", "Georgia", "Times New Roman", "Impact",
        })
            _fontFamilyCombo.Items.Add(f);
        _fontFamilyCombo.SelectionChanged += (_, _) => _previewCanvas?.Invalidate();
        stack.Children.Add(_fontFamilyCombo);

        // ── Save ──────────────────────────────────────────────────────────
        _saveBtn = new Button { Content = "Save", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        _saveBtn.Click += async (_, _) => { var n = SaveTheme(); if (n is not null) await ShowSavedBadge(); };

        _saveApplyBtn = new Button { Content = "Save and apply" };
        _saveApplyBtn.Click += async (_, _) =>
        {
            var name = SaveTheme();
            if (name is null) return;
            await App.Config.UpdateAsync(cfg => cfg.Appearance.ThemeName = name);
            await ShowSavedBadge();
        };

        _saved = PageKit.SavedBadge();
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 14, 0, 0) };
        saveRow.Children.Add(_saveBtn);
        saveRow.Children.Add(_saveApplyBtn);
        saveRow.Children.Add(_saved);
        stack.Children.Add(saveRow);

        _nameBox.TextChanged += (_, _) => _previewCanvas?.Invalidate();
        foreach (var fb in _floatBoxes.Values) fb.TextChanged += (_, _) => _previewCanvas?.Invalidate();

        scroll.Content = stack;
        Content = scroll;
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

        foreach (var (prop, _) in ColorFields)
        {
            if (!_colorBoxes.TryGetValue(prop, out var box)) continue;
            string val = GetColorProp(t, prop);
            box.Text = val;
            if (_colorSwatches.TryGetValue(prop, out var btn)) btn.Background = HexToBrush(val);
        }
        if (_floatBoxes.TryGetValue("SliceStrokeWidth",    out var fb1)) fb1.Text = t.SliceStrokeWidth.ToString("0.##");
        if (_floatBoxes.TryGetValue("LabelFontSize",       out var fb2)) fb2.Text = t.LabelFontSize.ToString("0.##");
        if (_floatBoxes.TryGetValue("VolumeRingThickness", out var fb3)) fb3.Text = t.VolumeRingThickness.ToString("0.##");
        _fontFamilyCombo.SelectedItem = t.LabelFontFamily;
        if (_fontFamilyCombo.SelectedItem is null) _fontFamilyCombo.SelectedIndex = 0;
        _loading = false;
        _previewCanvas?.Invalidate();
    }

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

        foreach (var (prop, _) in ColorFields)
            if (_colorBoxes.TryGetValue(prop, out var box)) SetColorProp(t, prop, box.Text.Trim());

        if (_floatBoxes.TryGetValue("SliceStrokeWidth", out var fb1) && float.TryParse(fb1.Text, out float strokeW)) t.SliceStrokeWidth = strokeW;
        if (_floatBoxes.TryGetValue("LabelFontSize", out var fb2) && float.TryParse(fb2.Text, out float fontSize)) t.LabelFontSize = fontSize;
        if (_floatBoxes.TryGetValue("VolumeRingThickness", out var fb3) && float.TryParse(fb3.Text, out float volW) && volW > 0f) t.VolumeRingThickness = volW;
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
