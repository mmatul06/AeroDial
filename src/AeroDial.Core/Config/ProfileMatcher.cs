// AeroDial — ProfileMatcher.cs
// Resolves which menu the dial should open for a given foreground process.

namespace AeroDial.Config;

public static class ProfileMatcher
{
    /// <summary>Reserved profile target: the dial does not open at all for this app.</summary>
    public const string DisabledMenuId = "__disabled__";

    /// <summary>The app profile whose process name matches (case-insensitive, exact), or null.</summary>
    public static AppProfileConfig? FindProfile(AeroDialConfig config, string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return null;
        var name = processName.Trim();
        return config.AppProfiles.FirstOrDefault(p =>
            string.Equals(p.ProcessName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True when a profile explicitly disables the dial for this process.</summary>
    public static bool IsDisabledFor(AeroDialConfig config, string? processName)
        => FindProfile(config, processName)?.MenuId == DisabledMenuId;

    /// <summary>Menu for the process: the bound profile menu if one exists and resolves,
    /// otherwise the configured active menu, otherwise the first menu.
    /// Returns null only when the profile disables the dial.</summary>
    public static RadialMenuConfig? GetActiveMenu(AeroDialConfig config, string? processName)
    {
        var profile = FindProfile(config, processName);
        if (profile is not null)
        {
            if (profile.MenuId == DisabledMenuId) return null;
            var bound = GetMenu(config, profile.MenuId);
            if (bound is not null) return bound;
        }
        return GetMenu(config, config.ActiveMenuId) ?? config.Menus.First();
    }

    public static RadialMenuConfig? GetMenu(AeroDialConfig config, string? id)
        => id is null ? null : config.Menus.FirstOrDefault(m => m.Id == id);
}
