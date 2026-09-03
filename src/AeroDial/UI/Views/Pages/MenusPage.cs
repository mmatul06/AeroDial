// AeroDial — MenusPage.cs
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
    private ComboBox      _actionCombo = null!;
    private SKXamlCanvas? _iconPreview;
    private Flyout?       _iconPickerFlyout;
    private bool          _recordingCombo;
    private TextBox?      _comboCaptureBox;
    private CheckBox?     _comboCtrl, _comboAlt, _comboShift, _comboWin;
    private ActionType    _paneActionType = ActionType.None; // type the panes currently show

    // Payload panes (shown/hidden based on selected ActionType)
    private StackPanel _appPane    = null!, _urlPane   = null!, _comboPane  = null!,
                       _mediaPane  = null!, _subPane   = null!, _scriptPane = null!,
                       _clipPane   = null!, _macroPane = null!, _folderPane = null!,
                       _commandPane = null!;
    private TextBox    _appPath    = null!, _appArgs   = null!, _urlBox     = null!,
                       _comboBox   = null!, _scriptBox = null!, _clipBox    = null!,
                       _folderPath = null!, _commandBox = null!;
    private CheckBox   _runAsAdmin = null!;
    private ComboBox   _mediaCombo = null!, _subMenuSel = null!;

    // Scroll-wheel bindings (any non-submenu slice): scrolling while hovering fires a media action
    private StackPanel _scrollPane = null!;
    private ComboBox   _scrollUp   = null!, _scrollDown = null!;
    private static readonly string[] ScrollActionNames =
        ["None", "Volume up", "Volume down", "Play / pause", "Next", "Previous", "Mute"];
    private static MediaActionType? ScrollIndexToAction(int i) => i switch
    {
        1 => MediaActionType.VolumeUp, 2 => MediaActionType.VolumeDown, 3 => MediaActionType.PlayPause,
        4 => MediaActionType.Next,     5 => MediaActionType.Previous,   6 => MediaActionType.Mute,
        _ => null,
    };
    private static int ScrollActionToIndex(MediaActionType? a) => a switch
    {
        MediaActionType.VolumeUp => 1, MediaActionType.VolumeDown => 2, MediaActionType.PlayPause => 3,
        MediaActionType.Next     => 4, MediaActionType.Previous   => 5, MediaActionType.Mute      => 6,
        _ => 0,
    };

    // Macro editor (step list) working state for the currently-edited item
    private StackPanel      _macroRows  = null!;
    private List<MacroStep> _macroSteps = [];

    // Unsaved-changes bar (visibility is the source of truth)
    private Border _dirtyBar = null!;

    // Drill-in breadcrumb: menu ids from root of the current drill path (last = current)
    private readonly List<string> _crumb = [];
    private StackPanel _crumbBar = null!;
    private Border     _profileBadge = null!;

    private static readonly string[] MacroStepNames = ["Type text", "Press key", "Key down", "Key up", "Delay"];

    /// <summary>Action type currently chosen in the editor combo (items carry the enum in Tag).</summary>
    private ActionType? SelectedAction
        => _actionCombo.SelectedItem is ComboBoxItem ci && ci.Tag is ActionType at ? at : null;

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

        root.Children.Add(PageKit.PageHeader("Menus"));

        // ── Menu picker + management buttons ──────────────────────────────
        root.Children.Add(PageKit.SubHeader("Active menu"));
        var menuRow = new StackPanel
            { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
        _menuCombo = new ComboBox { Width = 200 };
        PopulateMenuCombo();
        _menuCombo.SelectionChanged += (_, _) =>
        {
            if (!_rebuildingList) SelectMenu(_menuCombo.SelectedIndex);
        };

        Button Btn(string txt, RoutedEventHandler h) { var b = new Button { Content = txt }; b.Click += h; return b; }
        var deleteMenuBtn = PageKit.DangerButton("Delete");
        deleteMenuBtn.Click += (_, _) => DeleteMenuAsync().FireAndForget();

        var presetsFlyout = new MenuFlyout();
        var loadPreset = new MenuFlyoutItem { Text = "Load a preset into this menu…" };
        loadPreset.Click += (_, _) => LoadPresetAsync().FireAndForget();
        var savePreset = new MenuFlyoutItem { Text = "Save this menu as a preset…" };
        savePreset.Click += (_, _) => SaveAsPresetAsync().FireAndForget();
        presetsFlyout.Items.Add(loadPreset);
        presetsFlyout.Items.Add(savePreset);
        var presetsBtn = new DropDownButton { Content = "Presets", Flyout = presetsFlyout };

        menuRow.Children.Add(_menuCombo);
        menuRow.Children.Add(Btn("New",    AddMenu));
        menuRow.Children.Add(Btn("Rename", (s, e) => RenameMenuAsync().FireAndForget()));
        menuRow.Children.Add(deleteMenuBtn);
        menuRow.Children.Add(presetsBtn);
        root.Children.Add(menuRow);

        // ── Breadcrumb (drill path) + profile-binding badge ──────────────
        var crumbBadgeRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        crumbBadgeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        crumbBadgeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _crumbBar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        _profileBadge = new Border
        {
            Background        = Ui.SubtleFill,
            BorderBrush       = Ui.CardStroke,
            BorderThickness   = new Thickness(1),
            CornerRadius      = new CornerRadius(10),
            Padding           = new Thickness(10, 3, 10, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = "Not bound to any app", FontSize = 12,
                Foreground = Ui.TextSecondary,
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
            Foreground = Ui.TextSecondary,
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
        var saveBtn = PageKit.SaveButton(); _saved = PageKit.SavedBadge();
        saveBtn.Click += Save;
        var discardBtn = new Button { Content = "Discard changes" };
        discardBtn.Click += Discard;
        var warn = new TextBlock
        {
            Text = "Unsaved changes", FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Ui.Caution,
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
        var s    = new StackPanel { Spacing = 6 };
        var card = Ui.Card(s, new Thickness(16, 14, 16, 14));
        card.Visibility = Visibility.Collapsed;

        var headerRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var headerText = new TextBlock
        {
            Text = "Edit item", FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var removeBtn = PageKit.DangerButton("Remove");
        removeBtn.Padding = new Thickness(8, 4, 8, 4);
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

        _iconBox = new TextBox { PlaceholderText = "fluent:name, or a file path", Width = 140 };
        _iconBox.TextChanged += (_, _) => _iconPreview?.Invalidate();

        // Searchable grid of system icon-font glyphs (see FluentGlyphs)
        var pickBtn = new Button { Content = "Choose…", Padding = new Thickness(8, 4, 8, 4) };
        pickBtn.Click += (_, _) =>
        {
            _iconPickerFlyout ??= BuildIconPickerFlyout();
            _iconPickerFlyout.ShowAt(pickBtn);
        };

        var browseBtn = new Button { Content = "Browse…", Padding = new Thickness(8, 4, 8, 4) };
        ToolTipService.SetToolTip(browseBtn, "Use an image file (.png, .jpg, .ico, .bmp)");
        browseBtn.Click += BrowseIconAsync;

        iconRow.Children.Add(_iconPreview);
        iconRow.Children.Add(_iconBox);
        iconRow.Children.Add(pickBtn);
        iconRow.Children.Add(browseBtn);
        s.Children.Add(iconRow);

        // Action type (friendly labels; the enum rides along in Tag)
        s.Children.Add(new TextBlock { Text = "Action type", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        _actionCombo = new ComboBox { Width = 260 };
        foreach (var a in ActionCatalog.Editable)
        {
            var item = new ComboBoxItem { Content = a.Label, Tag = a.Type };
            ToolTipService.SetToolTip(item, a.Description);
            _actionCombo.Items.Add(item);
        }
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

        _folderPane = Pane();
        _folderPane.Children.Add(new TextBlock { Text = "Folder path", FontSize = 12 });
        var folderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        _folderPath = new TextBox { PlaceholderText = @"C:\Users\you\Documents  or  %USERPROFILE%\Downloads", Width = 260 };
        var browseFolderBtn = new Button { Content = "Browse…", Padding = new Thickness(8, 4, 8, 4) };
        browseFolderBtn.Click += BrowseFolderAsync;
        folderRow.Children.Add(_folderPath);
        folderRow.Children.Add(browseFolderBtn);
        _folderPane.Children.Add(folderRow);
        _folderPane.Children.Add(new TextBlock
        {
            Text = "Opens in File Explorer. A file path selects that file in its folder.",
            FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.TextSecondary,
        });

        _commandPane = Pane();
        _commandPane.Children.Add(new TextBlock { Text = "Command", FontSize = 12 });
        _commandBox = new TextBox { PlaceholderText = "regedit    ms-settings:display    cmd /k dir    shell:startup" };
        _commandPane.Children.Add(_commandBox);
        _runAsAdmin = new CheckBox { Content = "Run as administrator", Margin = new Thickness(0, 2, 0, 0) };
        _commandPane.Children.Add(_runAsAdmin);
        _commandPane.Children.Add(new TextBlock
        {
            Text = "Works like the Windows Run box (Win+R): programs, URIs, shell: folders, and %VARIABLES%. " +
                   "Run as administrator shows a UAC prompt.",
            FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Foreground = Ui.TextSecondary,
        });

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
            Foreground = Ui.TextSecondary,
            Margin = new Thickness(0, 2, 0, 0),
        });

        s.Children.Add(payloads);

        // ── Scroll wheel (moved here from the old Dynamic page) ───────────
        _scrollPane = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        _scrollPane.Children.Add(new TextBlock { Text = "Scroll wheel on this slice", FontSize = 12 });
        var scrollRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _scrollUp   = new ComboBox { Width = 150, ItemsSource = ScrollActionNames, Header = "Scroll up" };
        _scrollDown = new ComboBox { Width = 150, ItemsSource = ScrollActionNames, Header = "Scroll down" };
        scrollRow.Children.Add(_scrollUp);
        scrollRow.Children.Add(_scrollDown);
        _scrollPane.Children.Add(scrollRow);
        _scrollPane.Children.Add(Ui.Hint("While the dial is open and the cursor is on this slice, the wheel runs these without closing the menu.", 11));
        s.Children.Add(_scrollPane);

        var applyBtn = new Button
        {
            Content = "Apply to item",
            Style   = (Style)Application.Current.Resources["AccentButtonStyle"],
            Margin  = new Thickness(0, 8, 0, 0),
        };
        applyBtn.Click += ApplyItem;
        s.Children.Add(applyBtn);

        return card;
    }

    /// <summary>Searchable grid of every curated icon-font glyph. Click a tile to use it.</summary>
    private Flyout BuildIconPickerFlyout()
    {
        var flyout = new Flyout
        {
            ShouldConstrainToRootBounds = false,
            Placement = Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.Bottom,
        };

        var family = new FontFamily(IconRegistry.GlyphFontFamily);
        var search = new TextBox { PlaceholderText = "Search icons (name or keyword)", Width = 344 };
        var grid   = new GridView
        {
            Width = 344, MaxHeight = 300,
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = true,
        };

        void Fill(string? query)
        {
            grid.Items.Clear();
            foreach (var g in FluentGlyphs.Search(query))
            {
                var tile = new Border
                {
                    Width = 40, Height = 40, Tag = g.Key,
                    Child = new FontIcon
                    {
                        Glyph = g.Text, FontFamily = family, FontSize = 20,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center,
                    },
                };
                ToolTipService.SetToolTip(tile, g.Name);
                grid.Items.Add(tile);
            }
        }
        Fill(null);
        search.TextChanged += (_, _) => Fill(search.Text);
        grid.ItemClick += (_, e) =>
        {
            if (e.ClickedItem is Border b && b.Tag is string key)
            {
                _iconBox.Text = key;
                flyout.Hide();
            }
        };

        var panel = new StackPanel { Spacing = 8 };
        panel.Children.Add(search);
        panel.Children.Add(grid);
        panel.Children.Add(new TextBlock
        {
            Text = "Any glyph from the Segoe Fluent Icons font works: type fluent:E8B7 (its hex code) in the icon box.",
            FontSize = 11, TextWrapping = TextWrapping.Wrap, Width = 344,
            Foreground = Ui.TextSecondary,
        });
        flyout.Content = panel;
        flyout.Opened += (_, _) => search.Focus(FocusState.Programmatic);
        return flyout;
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
                    Foreground = Ui.TextSecondary,
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
        PutAt(slot, new MenuItemConfig { Label = "New item", Icon = "fluent:dial", ActionType = ActionType.None });
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

    // Thin wrappers over AeroDial.Core MenuSlots so the slot rules are testable.
    private static MenuItemConfig NewEmpty() => MenuSlots.NewEmpty();

    private void PutAt(int slot, MenuItemConfig item)
    {
        if (Live is null) return;
        MenuSlots.PutAt(Live.Items, slot, item);
    }

    private void TrimTrailingEmpties()
    {
        if (Live is null) return;
        MenuSlots.TrimTrailingEmpties(Live.Items);
    }

    private void MoveOrSwap(int src, int dst)
    {
        if (Live is null || src == dst) return;
        MenuSlots.MoveOrSwap(Live.Items, src, dst);
        _ringCanvas.Invalidate();
    }

    // ── Editor ────────────────────────────────────────────────────────────

    private void LoadEditor(MenuItemConfig item)
    {
        _labelBox.Text = item.Label;
        _iconBox.Text  = item.Icon ?? "";

        _paneActionType = item.ActionType;
        _actionCombo.SelectedIndex = ActionCatalog.IndexOf(item.ActionType);

        _appPath.Text    = item.AppPath    ?? "";
        _appArgs.Text    = item.AppArgs    ?? "";
        _urlBox.Text     = item.Url        ?? "";
        _comboBox.Text   = item.KeyCombo   ?? "";
        ParseComboToCheckboxes(item.KeyCombo);
        _scriptBox.Text  = item.ScriptPath ?? "";
        _clipBox.Text    = item.ClipText   ?? "";
        _folderPath.Text = item.FolderPath ?? "";
        _commandBox.Text = item.Command    ?? "";
        _runAsAdmin.IsChecked = item.RunAsAdmin;
        _scrollUp.SelectedIndex   = ScrollActionToIndex(item.ScrollUpAction);
        _scrollDown.SelectedIndex = ScrollActionToIndex(item.ScrollDownAction);

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
        if (SelectedAction is not ActionType at) return;

        // If the icon is still the previous type's default (or empty), follow the new type.
        string current  = _iconBox.Text.Trim();
        string? prevDef = ActionCatalog.Find(_paneActionType)?.DefaultIcon;
        string? newDef  = ActionCatalog.Find(at)?.DefaultIcon;
        bool auto = current.Length == 0
                 || string.Equals(current, prevDef, StringComparison.OrdinalIgnoreCase)
                 || string.Equals(FluentGlyphs.Canonicalize(current), "fluent:dial", StringComparison.OrdinalIgnoreCase);
        if (auto && newDef is not null && at != _paneActionType) _iconBox.Text = newDef;

        _paneActionType = at;
        ShowPane(at);
    }

    private void ShowPane(ActionType at)
    {
        _appPane.Visibility     = at == ActionType.LaunchApp      ? Visibility.Visible : Visibility.Collapsed;
        _folderPane.Visibility  = at == ActionType.OpenFolder     ? Visibility.Visible : Visibility.Collapsed;
        _commandPane.Visibility = at == ActionType.RunCommand     ? Visibility.Visible : Visibility.Collapsed;
        _urlPane.Visibility     = at == ActionType.OpenUrl        ? Visibility.Visible : Visibility.Collapsed;
        _comboPane.Visibility   = at == ActionType.KeyCombo       ? Visibility.Visible : Visibility.Collapsed;
        _mediaPane.Visibility   = at == ActionType.Media          ? Visibility.Visible : Visibility.Collapsed;
        _subPane.Visibility     = at == ActionType.SubMenu        ? Visibility.Visible : Visibility.Collapsed;
        _scriptPane.Visibility  = at == ActionType.RunScript      ? Visibility.Visible : Visibility.Collapsed;
        _clipPane.Visibility    = at == ActionType.PasteClipboard ? Visibility.Visible : Visibility.Collapsed;
        _macroPane.Visibility   = at == ActionType.Macro          ? Visibility.Visible : Visibility.Collapsed;
        _scrollPane.Visibility  = at is not (ActionType.SubMenu or ActionType.None) ? Visibility.Visible : Visibility.Collapsed;
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
                Foreground = Ui.TextSecondary,
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
                Foreground = Ui.TextSecondary,
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
        Cur.Icon  = NE(_iconBox.Text)  ?? "fluent:dial";

        if (SelectedAction is ActionType at)
        {
            Cur.ActionType = at;
            Cur.AppPath    = at == ActionType.LaunchApp      ? NE(_appPath.Text)    : null;
            Cur.AppArgs    = at == ActionType.LaunchApp      ? NE(_appArgs.Text)    : null;
            Cur.FolderPath = at == ActionType.OpenFolder     ? NE(_folderPath.Text) : null;
            Cur.Command    = at == ActionType.RunCommand     ? NE(_commandBox.Text) : null;
            Cur.RunAsAdmin = at == ActionType.RunCommand     && _runAsAdmin.IsChecked == true;
            Cur.Url        = at == ActionType.OpenUrl        ? NE(_urlBox.Text)     : null;
            Cur.KeyCombo   = at == ActionType.KeyCombo       ? NE(_comboBox.Text)   : null;
            Cur.ScriptPath = at == ActionType.RunScript      ? NE(_scriptBox.Text)  : null;
            Cur.ClipText   = at == ActionType.PasteClipboard ? NE(_clipBox.Text)    : null;

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

            bool scrollable = at is not (ActionType.SubMenu or ActionType.None);
            Cur.ScrollUpAction   = scrollable ? ScrollIndexToAction(_scrollUp.SelectedIndex)   : null;
            Cur.ScrollDownAction = scrollable ? ScrollIndexToAction(_scrollDown.SelectedIndex) : null;
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
        IconRegistry.DrainRetired(); // free bitmaps invalidated by the icon picker (grace period elapsed)
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

        int sliceCount = Math.Clamp(appear.SliceCount, 3, 12);
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
                var bmp = IconRegistry.Get(menu.Items[i].Icon, theme.StrokeScale);
                if (bmp is not null)
                {
                    float isz  = minDim * 0.12f * RingGeometry.IconSizeMul(sliceCount) * theme.SizeScale; // same shrink as the overlay
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

        // Same slice math as the overlay (RingGeometry), so the editor and the dial agree.
        int sliceCount = Math.Clamp(App.Config.Current.Appearance.SliceCount, 3, 12);
        return RingGeometry.SliceIndexAt(dx, dy, sliceCount);
    }

    private static bool SlotFilled(RadialMenuConfig? menu, int slot) => MenuSlots.SlotFilled(menu, slot);

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

    // Both handlers are async void (event handlers must be), so nothing may escape them:
    // Pickers.* already swallows picker failures, and the try/catch covers the UI update.

    private async void BrowseIconAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await Pickers.PickFileAsync(".png", ".jpg", ".jpeg", ".ico", ".bmp");
            if (path is null) return;
            _iconBox.Text = path;
            IconRegistry.Invalidate(path);
        }
        catch (Exception ex) { Logger.Error("BrowseIcon failed", ex); }
    }

    private async void BrowseAppAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await Pickers.PickFileAsync(".exe", ".lnk", ".bat", ".cmd");
            if (path is not null) _appPath.Text = path;
        }
        catch (Exception ex) { Logger.Error("BrowseApp failed", ex); }
    }

    private async void BrowseFolderAsync(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await Pickers.PickFolderAsync();
            if (path is not null) _folderPath.Text = path;
        }
        catch (Exception ex) { Logger.Error("BrowseFolder failed", ex); }
    }

    // ── Presets ───────────────────────────────────────────────────────────

    private static readonly (string Name, Func<List<MenuItemConfig>> Factory)[] BuiltInPresets =
    [
        ("Media Controls", () =>
        [
            new() { Label = "Play / Pause", Icon = "fluent:play",        ActionType = ActionType.Media, MediaAction = MediaActionType.PlayPause  },
            new() { Label = "Next",          Icon = "fluent:next",        ActionType = ActionType.Media, MediaAction = MediaActionType.Next       },
            new() { Label = "Previous",      Icon = "fluent:previous",    ActionType = ActionType.Media, MediaAction = MediaActionType.Previous   },
            new() { Label = "Volume Up",     Icon = "fluent:volume_up",   ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeUp,
                    ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Volume Down",   Icon = "fluent:volume_down", ActionType = ActionType.Media, MediaAction = MediaActionType.VolumeDown,
                    ScrollUpAction = MediaActionType.VolumeUp, ScrollDownAction = MediaActionType.VolumeDown },
            new() { Label = "Mute",          Icon = "fluent:mute",        ActionType = ActionType.Media, MediaAction = MediaActionType.Mute       },
        ]),
        ("System Tools", () =>
        [
            new() { Label = "Settings",   Icon = "fluent:settings",  ActionType = ActionType.OpenSettings                                              },
            new() { Label = "Desktop",    Icon = "fluent:desktop",   ActionType = ActionType.KeyCombo,   KeyCombo = "Win+D"                            },
            new() { Label = "Lock",       Icon = "fluent:lock",      ActionType = ActionType.KeyCombo,   KeyCombo = "Win+L"                            },
            new() { Label = "Screenshot", Icon = "fluent:camera",    ActionType = ActionType.KeyCombo,   KeyCombo = "Win+Shift+S"                      },
            new() { Label = "Task Mgr",   Icon = "fluent:list",      ActionType = ActionType.RunCommand, Command = "taskmgr"                           },
            new() { Label = "Registry",   Icon = "fluent:terminal",  ActionType = ActionType.RunCommand, Command = "regedit", RunAsAdmin = true        },
            new() { Label = "Downloads",  Icon = "fluent:folder",    ActionType = ActionType.OpenFolder, FolderPath = "%USERPROFILE%\\Downloads"       },
            new() { Label = "Apps",       Icon = "fluent:apps",      ActionType = ActionType.SubMenu,    SubMenuId = AppConstants.ActiveTasksMenuId    },
        ]),
        ("Productivity", () =>
        [
            new() { Label = "Copy",       Icon = "fluent:copy",   ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+C" },
            new() { Label = "Paste",      Icon = "fluent:paste",  ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+V" },
            new() { Label = "Cut",        Icon = "fluent:cut",    ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+X" },
            new() { Label = "Undo",       Icon = "fluent:undo",   ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+Z" },
            new() { Label = "Redo",       Icon = "fluent:redo",   ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+Y" },
            new() { Label = "Save",       Icon = "fluent:save",   ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+S" },
            new() { Label = "Find",       Icon = "fluent:search", ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+F" },
            new() { Label = "Select All", Icon = "fluent:check",  ActionType = ActionType.KeyCombo, KeyCombo = "Ctrl+A" },
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
            Foreground   = Ui.TextSecondary,
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
        // Preview on the dial theme's own center color so the tinted glyph reads the way it will on the ring.
        canvas.Clear(App.Themes.ActiveTheme.ToSKColor(App.Themes.ActiveTheme.CenterFill).WithAlpha(255));

        string key = _iconBox?.Text.Trim() ?? "";
        if (string.IsNullOrEmpty(key)) return;

        var theme = App.Themes.ActiveTheme;
        var bmp = IconRegistry.Get(key, theme.StrokeScale);
        if (bmp is null) return;

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
