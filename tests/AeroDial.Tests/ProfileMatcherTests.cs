using AeroDial.Config;

namespace AeroDial.Tests;

public class ProfileMatcherTests
{
    private static AeroDialConfig Cfg()
    {
        var cfg = new AeroDialConfig
        {
            Menus =
            [
                new RadialMenuConfig { Id = "default", Name = "Main" },
                new RadialMenuConfig { Id = "cad",     Name = "CAD"  },
            ],
            ActiveMenuId = "default",
            AppProfiles  =
            [
                new AppProfileConfig { ProcessName = "acad",  MenuId = "cad" },
                new AppProfileConfig { ProcessName = "game",  MenuId = ProfileMatcher.DisabledMenuId },
                new AppProfileConfig { ProcessName = "stale", MenuId = "deleted-menu" },
            ],
        };
        return cfg;
    }

    [Fact]
    public void No_process_returns_active_menu()
        => Assert.Equal("default", ProfileMatcher.GetActiveMenu(Cfg(), null)!.Id);

    [Fact]
    public void Matching_profile_returns_bound_menu_case_insensitively()
        => Assert.Equal("cad", ProfileMatcher.GetActiveMenu(Cfg(), "ACAD")!.Id);

    [Fact]
    public void Unknown_process_falls_back_to_active_menu()
        => Assert.Equal("default", ProfileMatcher.GetActiveMenu(Cfg(), "notepad")!.Id);

    [Fact]
    public void Profile_bound_to_a_deleted_menu_falls_back_to_active_menu()
        => Assert.Equal("default", ProfileMatcher.GetActiveMenu(Cfg(), "stale")!.Id);

    [Fact]
    public void Disabled_profile_returns_null_and_is_reported()
    {
        Assert.Null(ProfileMatcher.GetActiveMenu(Cfg(), "game"));
        Assert.True(ProfileMatcher.IsDisabledFor(Cfg(), "Game"));
        Assert.False(ProfileMatcher.IsDisabledFor(Cfg(), "acad"));
    }

    [Fact]
    public void Missing_active_menu_falls_back_to_first_menu()
    {
        var cfg = Cfg();
        cfg.ActiveMenuId = "nope";
        Assert.Equal("default", ProfileMatcher.GetActiveMenu(cfg, null)!.Id);
    }
}
