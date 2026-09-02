// AeroDial — ConfigService.cs
// Responsible for loading, saving, and live-reloading the configuration file.
// Uses System.Text.Json with indented output so the file is human-readable
// and editable by power users.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AeroDial.Core;

namespace AeroDial.Config;

internal sealed class ConfigService
{
    // ── State ─────────────────────────────────────────────────────────────
    public AeroDialConfig Current { get; private set; }

    public event Action? ConfigChanged;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        WriteIndented         = true,
        PropertyNamingPolicy  = JsonNamingPolicy.CamelCase,
        Converters            = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling   = JsonCommentHandling.Skip,
        AllowTrailingCommas   = true,
    };

    private ConfigService(AeroDialConfig config) => Current = config;

    // ── Bootstrap ─────────────────────────────────────────────────────────

    public static async Task<ConfigService> LoadAsync()
    {
        Directory.CreateDirectory(AppConstants.AppDataDir);

        AeroDialConfig config;

        if (File.Exists(AppConstants.ConfigPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(AppConstants.ConfigPath);
                var root = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions
                {
                    CommentHandling     = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }) as JsonObject ?? throw new JsonException("config.json root is not an object");

                int fromVersion = ConfigMigrator.Migrate(root);
                if (fromVersion < ConfigMigrator.CurrentVersion)
                    Logger.Info($"Config migrated from v{fromVersion} to v{ConfigMigrator.CurrentVersion}.");

                config = root.Deserialize<AeroDialConfig>(s_jsonOptions) ?? new AeroDialConfig();
                Sanitize(config);
                Logger.Info($"Config loaded from {AppConstants.ConfigPath}");
            }
            catch (Exception ex)
            {
                // Never silently overwrite a file we couldn't read: keep it for recovery.
                var corruptPath = Path.Combine(AppConstants.AppDataDir,
                    $"config.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                try { File.Move(AppConstants.ConfigPath, corruptPath, overwrite: true); }
                catch (Exception moveEx) { Logger.Warn("Could not set aside corrupt config", moveEx); }

                Logger.Error($"Failed to parse config.json — using defaults. Original kept at {corruptPath}", ex);
                config = new AeroDialConfig();
            }
        }
        else
        {
            config = new AeroDialConfig();
            Logger.Info("No config found — writing defaults.");
        }

        var service = new ConfigService(config);
        await service.SaveAsync(); // always flush so file stays in sync with model
        return service;
    }

    /// <summary>Clamps values a hand-edited file could put out of range.</summary>
    private static void Sanitize(AeroDialConfig config)
    {
        config.Appearance.SliceCount = Math.Clamp(config.Appearance.SliceCount, 3, 12);
        config.Appearance.Scale      = Math.Clamp(config.Appearance.Scale, 0.4f, 2.0f);
        if (config.Menus.Count == 0) config.Menus = new AeroDialConfig().Menus;
    }

    // ── Persistence ───────────────────────────────────────────────────────

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current, s_jsonOptions);
            // Atomic write: write to temp file then replace, so a crash mid-write
            // never corrupts the config.
            var tmpPath = AppConstants.ConfigPath + ".tmp";
            await File.WriteAllTextAsync(tmpPath, json);

            // Keep the previous good file as config.json.bak before replacing it.
            if (File.Exists(AppConstants.ConfigPath))
                File.Copy(AppConstants.ConfigPath, AppConstants.ConfigBackupPath, overwrite: true);

            File.Move(tmpPath, AppConstants.ConfigPath, overwrite: true);
            Logger.Info("Config saved.");
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to save config", ex);
        }
    }

    // ── Export / import ───────────────────────────────────────────────────

    /// <summary>Everything a user would want to move to another machine: menus, app
    /// profiles, appearance and behavior settings, and their custom theme files.</summary>
    public sealed class SettingsBundle
    {
        public int    BundleVersion { get; set; } = 1;
        public string AppVersion    { get; set; } = AppConstants.Version;
        public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;
        public List<RadialMenuConfig> Menus       { get; set; } = [];
        public string                 ActiveMenuId { get; set; } = "default";
        public List<AppProfileConfig> AppProfiles { get; set; } = [];
        public TriggerConfig?         Trigger     { get; set; }
        public AppearanceConfig?      Appearance  { get; set; }
        public BehaviorConfig?        Behavior    { get; set; }
        /// <summary>User theme name → theme JSON text.</summary>
        public Dictionary<string, string> UserThemes { get; set; } = new();
    }

    public async Task ExportBundleAsync(string path)
    {
        var bundle = new SettingsBundle
        {
            Menus        = Current.Menus,
            ActiveMenuId = Current.ActiveMenuId,
            AppProfiles  = Current.AppProfiles,
            Trigger      = Current.Trigger,
            Appearance   = Current.Appearance,
            Behavior     = Current.Behavior,
        };
        if (Directory.Exists(AppConstants.UserThemesDir))
            foreach (var f in Directory.EnumerateFiles(AppConstants.UserThemesDir, "*.json"))
                bundle.UserThemes[Path.GetFileNameWithoutExtension(f)] = await File.ReadAllTextAsync(f);

        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(bundle, s_jsonOptions));
        Logger.Info($"Settings exported to {path}");
    }

    /// <summary>Imports a bundle. Menus and profiles replace the current ones (a backup of the
    /// current config is written first); trigger/appearance/behavior are applied when present.
    /// Returns the number of menus imported.</summary>
    public async Task<int> ImportBundleAsync(string path, bool includeSettings)
    {
        var json   = await File.ReadAllTextAsync(path);
        var bundle = JsonSerializer.Deserialize<SettingsBundle>(json, s_jsonOptions)
                     ?? throw new JsonException("Not an AeroDial settings file.");
        if (bundle.Menus.Count == 0) throw new JsonException("The file contains no menus.");

        // Migrate icon names etc. the same way config.json is migrated.
        foreach (var m in bundle.Menus)
            foreach (var i in m.Items)
                i.Icon = FluentGlyphs.Canonicalize(i.Icon ?? "");

        if (File.Exists(AppConstants.ConfigPath))
            File.Copy(AppConstants.ConfigPath, AppConstants.ConfigBackupPath, overwrite: true);

        Directory.CreateDirectory(AppConstants.UserThemesDir);
        foreach (var (name, themeJson) in bundle.UserThemes)
        {
            var safe = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
            if (safe.Length == 0) continue;
            await File.WriteAllTextAsync(Path.Combine(AppConstants.UserThemesDir, safe + ".json"), themeJson);
        }

        await UpdateAsync(cfg =>
        {
            cfg.Menus        = bundle.Menus;
            cfg.ActiveMenuId = bundle.Menus.Any(m => m.Id == bundle.ActiveMenuId) ? bundle.ActiveMenuId : bundle.Menus[0].Id;
            cfg.AppProfiles  = bundle.AppProfiles;
            if (includeSettings)
            {
                if (bundle.Trigger    is not null) cfg.Trigger    = bundle.Trigger;
                if (bundle.Appearance is not null) cfg.Appearance = bundle.Appearance;
                if (bundle.Behavior   is not null) cfg.Behavior   = bundle.Behavior;
            }
            Sanitize(cfg);
        });
        Logger.Info($"Settings imported from {path}: {bundle.Menus.Count} menu(s), {bundle.UserThemes.Count} theme(s)");
        return bundle.Menus.Count;
    }

    // ── Mutation helpers ─────────────────────────────────────────────────

    /// <summary>Apply a mutation, persist, and raise ConfigChanged.</summary>
    public async Task UpdateAsync(Action<AeroDialConfig> mutation)
    {
        mutation(Current);
        await SaveAsync();
        ConfigChanged?.Invoke();
    }

    /// <summary>Returns the active menu for the given foreground process name, or null when an
    /// app profile disables the dial for that process (see ProfileMatcher).</summary>
    public RadialMenuConfig? GetActiveMenu(string? processName = null)
        => ProfileMatcher.GetActiveMenu(Current, processName);

    /// <summary>Resolve a submenu by id.</summary>
    public RadialMenuConfig? GetMenu(string id)
        => ProfileMatcher.GetMenu(Current, id);
}
