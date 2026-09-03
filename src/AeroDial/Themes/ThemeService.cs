// AeroDial — ThemeService.cs
// Discovers and loads themes from both the built-in /themes directory
// and the user's AppData themes folder. Provides the resolved active theme
// to the overlay renderer.

using System.Text.Json;
using System.Text.Json.Serialization;
using AeroDial.Core;

namespace AeroDial.Themes;

internal sealed class ThemeService
{
    private readonly Dictionary<string, AeroTheme> _themes = new(StringComparer.OrdinalIgnoreCase);

    // Shared with the writer (see ThemeJson): reading and writing must use the same options.
    private static JsonSerializerOptions Json => ThemeJson.Options;

    private Windows.UI.ViewManagement.UISettings? _uiSettings;

    public ThemeService()
    {
        LoadBuiltIn();
        LoadUserThemes();
        RegisterAccentTheme();
        Logger.Info($"ThemeService: {_themes.Count} theme(s) loaded.");
    }

    // ── "Auto (Windows accent)" ───────────────────────────────────────────
    // Derived from the live Windows accent color and rebuilt whenever Windows changes it.

    private void RegisterAccentTheme()
    {
        try
        {
            _uiSettings = new Windows.UI.ViewManagement.UISettings();
            RebuildAccentTheme();
            _uiSettings.ColorValuesChanged += (_, _) => RebuildAccentTheme();
        }
        catch (Exception ex)
        {
            Logger.Warn("Windows accent theme unavailable", ex);
        }
    }

    private void RebuildAccentTheme()
    {
        if (_uiSettings is null) return;
        var c = _uiSettings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
        var theme = AccentThemeBuilder.Build(new SkiaSharp.SKColor(c.R, c.G, c.B));
        theme.IsBuiltIn = true;
        _themes[theme.Name] = theme;
    }

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>All available theme names.</summary>
    public IReadOnlyCollection<string> AvailableThemes => _themes.Keys;

    /// <summary>Resolve the currently configured theme, falling back to Obsidian.</summary>
    public AeroTheme ActiveTheme
    {
        get
        {
            var name = App.Config.Current.Appearance.ThemeName;
            return _themes.TryGetValue(name, out var t) ? t : _themes["Obsidian"];
        }
    }

    /// <summary>Get a theme by name. Returns null if not found.</summary>
    public AeroTheme? Get(string name)
        => _themes.TryGetValue(name, out var t) ? t : null;

    /// <summary>Save a user-defined theme to AppData.</summary>
    public void SaveUserTheme(AeroTheme theme)
    {
        Directory.CreateDirectory(AppConstants.UserThemesDir);
        var path = Path.Combine(AppConstants.UserThemesDir, $"{theme.Name}.json");
        var json = JsonSerializer.Serialize(theme, Json);
        File.WriteAllText(path, json);
        _themes[theme.Name] = theme;
        Logger.Info($"User theme saved: {theme.Name}");
    }

    /// <summary>Copies a theme (built-in or user) into a new user theme with the given name.</summary>
    public AeroTheme Duplicate(AeroTheme source, string newName)
    {
        var json = JsonSerializer.Serialize(source);
        var copy = JsonSerializer.Deserialize<AeroTheme>(json) ?? new AeroTheme();
        copy.Name      = newName;
        copy.IsBuiltIn = false;
        SaveUserTheme(copy);
        return copy;
    }

    /// <summary>Delete a user-defined theme from AppData and from the in-memory registry.</summary>
    public void DeleteUserTheme(string name)
    {
        var path = Path.Combine(AppConstants.UserThemesDir, $"{name}.json");
        if (File.Exists(path)) File.Delete(path);
        _themes.Remove(name);
        Logger.Info($"User theme deleted: {name}");
    }

    // ── Loaders ───────────────────────────────────────────────────────────

    private void LoadBuiltIn()
    {
        // Register programmatically-defined built-in themes.
        // This avoids a hard dependency on the themes folder existing at runtime.
        foreach (var theme in BuiltInThemes.All)
        {
            theme.IsBuiltIn = true;
            _themes[theme.Name] = theme;
        }

        // Also try to load any .json files in the bundled themes directory.
        if (Directory.Exists(AppConstants.ThemesDir))
            LoadFromDirectory(AppConstants.ThemesDir, isBuiltIn: true);
    }

    private void LoadUserThemes()
    {
        if (Directory.Exists(AppConstants.UserThemesDir))
            LoadFromDirectory(AppConstants.UserThemesDir, isBuiltIn: false);
    }

    private void LoadFromDirectory(string dir, bool isBuiltIn)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            try
            {
                var json  = File.ReadAllText(file);
                var theme = JsonSerializer.Deserialize<AeroTheme>(json, Json);
                if (theme is null) continue;
                theme.IsBuiltIn = isBuiltIn;
                _themes[theme.Name] = theme;
            }
            catch (Exception ex)
            {
                Logger.Warn($"Could not load theme: {file}", ex);
            }
        }
    }
}
