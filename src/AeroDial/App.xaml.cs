// AeroDial — App.xaml.cs
// Application root. Boots all services in OnLaunched.
// App starts silently in the system tray — no window on startup.

using AeroDial.Actions;
using AeroDial.Config;
using AeroDial.Core;
using AeroDial.Overlay;
using AeroDial.Themes;
using AeroDial.UI.Views;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace AeroDial;

public partial class App : Application
{
    internal static ConfigService     Config     { get; private set; } = null!;
    internal static ThemeService      Themes     { get; private set; } = null!;
    internal static ActionDispatcher  Dispatcher { get; private set; } = null!;
    internal static TrayService       Tray       { get; private set; } = null!;
    internal static HookService       Hooks      { get; private set; } = null!;
    internal static OverlayController Overlay    { get; private set; } = null!;
    internal static MediaInfoService? MediaInfo  { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
        {
            try
            {
                Logger.Info("AeroDial starting up.");

                // Catch any unhandled exception and log it before the process dies
                AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                    Logger.Fatal("Unhandled exception", e.ExceptionObject as Exception);

                // Set Windows scheduler interrupt period to 1ms so Thread.Sleep in the
                // render thread is accurate to ~1ms instead of the default ~15.625ms.
                // This is safe to call multiple times; each call must be paired with timeEndPeriod.
                Core.Win32.timeBeginPeriod(1);

                Config     = await ConfigService.LoadAsync();
                Logger.SetDebugMode(Config.Current.Behavior.EnableDebugLogging);
                Logger.Info($"Config loaded. Debug logging: {Config.Current.Behavior.EnableDebugLogging}");
                Themes     = new ThemeService();
                MediaInfo  = new MediaInfoService();
                MediaInfo.Start();  // async, event-driven now-playing (safe if it fails)
                Dispatcher = new ActionDispatcher();
                Tray       = new TrayService();
                Tray.Initialize();
                Hooks      = new HookService();
                Overlay    = new OverlayController();
                Hooks.Start();

                // Wire up activation callback for single-instance enforcement.
                // Invoked on a background thread when a second instance launches.
                Program.ActivationCallback = () =>
                    Tray.DispatcherQueue.TryEnqueue(() =>
                    {
                        Core.Win32.GetCursorPos(out var pt);
                        Overlay.OpenAtCursor(new System.Drawing.Point(pt.X, pt.Y));
                    });

                Logger.Info("AeroDial ready. Running in system tray.");
            }
            catch (Exception ex)
            {
                Logger.Fatal("Bootstrap failed", ex);
            }
        });
    }

    internal static void RequestShutdown()
    {
        Logger.Info("Shutdown requested.");
        Core.Win32.timeEndPeriod(1); // restore default Windows scheduler resolution
        Hooks?.Stop();
        Tray?.Dispose();
        Overlay?.Dispose();
        MediaInfo?.Dispose();
        Current.Exit();
    }
}
