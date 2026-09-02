// AeroDial — StartupNotices.cs
// Two tray notices that run after startup:
//   - a one-time "AeroDial is running, hold <trigger> to open" hint on first launch
//     (the app starts silently in the tray, so a first-time user gets no other feedback)
//   - a daily check of GitHub Releases, with a notice when a newer version exists

namespace AeroDial.Core;

internal static class StartupNotices
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(24);

    public static void Run()
    {
        if (SelfTest.Enabled) return; // keep the self-test deterministic and quiet
        _ = RunAsync();
    }

    private static async Task RunAsync()
    {
        try
        {
            var behavior = App.Config.Current.Behavior;

            if (!behavior.FirstRunShown)
            {
                string trigger = UI.Views.Pages.TriggerPage.VkName(App.Config.Current.Trigger.VirtualKey);
                string verb    = App.Config.Current.Trigger.HoldMode ? "Hold" : "Press";
                App.Tray.ShowBalloon(
                    "AeroDial is running",
                    $"{verb} {trigger} to open the dial. Double-click the tray icon for settings.",
                    onClick: () => UI.Views.SettingsWindow.ShowOrActivate(),
                    warning: false);
                await App.Config.UpdateAsync(cfg => cfg.Behavior.FirstRunShown = true);
            }

            if (behavior.CheckForUpdates && DateTime.UtcNow - behavior.LastUpdateCheckUtc >= UpdateInterval)
            {
                await Task.Delay(TimeSpan.FromSeconds(20)); // never compete with startup
                var (status, latest, url) = await UpdateChecker.CheckAsync();
                await App.Config.UpdateAsync(cfg => cfg.Behavior.LastUpdateCheckUtc = DateTime.UtcNow);

                if (status == UpdateChecker.UpdateStatus.UpdateAvailable && latest is not null)
                {
                    string releaseUrl = url ?? AppConstants.GitHubUrl + "/releases/latest";
                    App.Tray.ShowBalloon(
                        $"AeroDial {latest} is available",
                        "Click to open the download page.",
                        onClick: () => System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo(releaseUrl) { UseShellExecute = true }),
                        warning: false);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn("Startup notices failed", ex);
        }
    }
}
