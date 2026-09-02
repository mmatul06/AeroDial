// AeroDial — Program.cs
// Entry point. Enforces single-instance via Mutex + EventWaitHandle.
// A second launch signals the running instance and exits immediately.

using System.Threading;
using AeroDial.Core;

namespace AeroDial;

internal static class Program
{
    private const string MutexName = "Global\\AeroDial_Instance_3MDS";

    /// <summary>Set by App.OnLaunched after all services are ready.
    /// Invoked on a background thread when a second instance signals activation.</summary>
    internal static Action? ActivationCallback;

    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Logger.Fatal("UnhandledException", e.ExceptionObject as Exception);

        // `--selftest`: scripted overlay smoke test, exits when done (see Core/SelfTest.cs)
        SelfTest.Enabled = Array.Exists(args, a => string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase));

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Logger.Error("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };

        // ── Single-instance guard ─────────────────────────────────────────
        bool createdNew;
        using var mutex = new Mutex(true, MutexName, out createdNew);

        if (!createdNew)
        {
            // Another instance is running — signal it to pop the overlay and exit.
            try
            {
                using var ev = EventWaitHandle.OpenExisting(AppConstants.ActivationEventName);
                ev.Set();
            }
            catch { /* first instance not fully started yet — just exit */ }
            return;
        }

        // Create the activation event so secondary instances can signal us.
        using var activationEvent = new EventWaitHandle(
            false, EventResetMode.AutoReset, AppConstants.ActivationEventName);

        // Background watcher — unblocks when a secondary instance sets the event.
        var watcher = new Thread(() =>
        {
            while (true)
            {
                activationEvent.WaitOne();
                try { ActivationCallback?.Invoke(); }
                catch (Exception ex) { Logger.Error("Activation callback failed", ex); }
            }
        })
        {
            IsBackground = true,
            Name         = "AeroDial.ActivationWatcher",
        };
        watcher.Start();

        global::Microsoft.UI.Xaml.Application.Start(p =>
        {
            var context = new global::Microsoft.UI.Dispatching
                .DispatcherQueueSynchronizationContext(
                global::Microsoft.UI.Dispatching.DispatcherQueue
                    .GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }
}
