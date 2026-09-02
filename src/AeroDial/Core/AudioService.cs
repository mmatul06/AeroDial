// AeroDial — AudioService.cs
// Reads the Windows master playback volume via IAudioEndpointVolume COM.
// Uses Type.GetTypeFromCLSID so no NuGet package is required.
//
// The endpoint is acquired once and kept. Default-device changes are picked up
// through IMMNotificationClient (a COM callback) instead of re-creating the COM
// objects on a timer — the old 2 s re-init ran CoCreateInstance on the render
// thread and showed up as a periodic frame hitch.

using System.Runtime.InteropServices;

namespace AeroDial.Core;

internal static class AudioService
{
    // ── COM interface declarations ────────────────────────────────────────
    // Method ordering must exactly match the Windows SDK vtable.
    // IUnknown methods (QueryInterface/AddRef/Release) are implicit with
    // InterfaceIsIUnknown — the first declared method maps to vtable slot 4.

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out nint ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out nint ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient pClient);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(
            ref Guid iid, uint dwClsCtx, nint pActivationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(nint pNotify);
        [PreserveSig] int UnregisterControlChangeNotify(nint pNotify);
        [PreserveSig] int GetChannelCount(out uint pnChannelCount);
        [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
        [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
        [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY { public Guid fmtid; public uint pid; }

    [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMNotificationClient
    {
        [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, uint dwNewState);
        [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
        [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string? pwstrDefaultDeviceId);
        [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PROPERTYKEY key);
    }

    // Callback sink. Runs on a COM worker thread — only flips a flag.
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    private sealed class NotificationClient : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string id, uint state) => 0;
        public int OnDeviceAdded(string id) => 0;
        public int OnDeviceRemoved(string id) => 0;
        public int OnDefaultDeviceChanged(int flow, int role, string? id)
        {
            if (flow == 0 /* eRender */) _deviceChanged = true;
            return 0;
        }
        public int OnPropertyValueChanged(string id, PROPERTYKEY key) => 0;
    }

    private static readonly Guid ClsidMMDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidAudioEndpointVolume  = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    // ── State ─────────────────────────────────────────────────────────────

    private static IMMDeviceEnumerator?  _enumerator;
    private static IAudioEndpointVolume? _endpoint;
    private static NotificationClient?   _sink;      // kept alive for the CCW
    private static bool                  _initialized;
    private static volatile bool         _deviceChanged;

    // Fallback when the notification callback could not be registered: re-acquire on a
    // slow timer so a device switch is still eventually reflected.
    private static bool _notificationsActive;
    private static long _lastReinit      = long.MinValue / 2;
    private const  long ReinitIntervalMs = 15000;

    // Retry cadence after a failed init so a stopped audio service is not hammered every poll.
    private static long _lastInitAttempt = long.MinValue / 2;
    private const  long InitRetryMs      = 3000;

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the current Windows master playback volume as a 0.0–1.0 scalar.
    /// Safe to call from any thread. Falls back to 0.5 if the Windows Audio service
    /// cannot be reached.
    /// </summary>
    public static float GetMasterVolume()
    {
        long now = Environment.TickCount64;

        bool stale = _deviceChanged
                  || (!_notificationsActive && now - _lastReinit >= ReinitIntervalMs);
        if (stale)
        {
            _deviceChanged = false;
            _lastReinit    = now;
            _endpoint      = null;
            _initialized   = false;
        }

        EnsureInitialized(now);
        if (_endpoint is null) return 0.5f;
        try
        {
            _endpoint.GetMasterVolumeLevelScalar(out float level);
            return Math.Clamp(level, 0f, 1f);
        }
        catch
        {
            // COM interface stale (service restart, device removal) — retry next poll.
            _endpoint    = null;
            _initialized = false;
            return 0.5f;
        }
    }

    // ── Internals ─────────────────────────────────────────────────────────

    private static void EnsureInitialized(long now)
    {
        if (_initialized) return;
        if (now - _lastInitAttempt < InitRetryMs) return;
        _lastInitAttempt = now;
        _initialized     = true; // set optimistically; reset below if init fails
        try
        {
            if (_enumerator is null)
            {
                var type = Type.GetTypeFromCLSID(ClsidMMDeviceEnumerator)
                           ?? throw new COMException("MMDeviceEnumerator CLSID not found");
                _enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
                RegisterNotifications();
            }

            _enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 0 /* eConsole */, out var device);

            var iid = IidAudioEndpointVolume;
            device.Activate(ref iid, 23 /* CLSCTX_ALL */, 0, out var volObj);
            _endpoint = (IAudioEndpointVolume)volObj;

            Logger.Debug("AudioService: IAudioEndpointVolume acquired.");
        }
        catch (Exception ex)
        {
            _initialized = false;
            Logger.Warn("AudioService: could not open IAudioEndpointVolume — will retry.", ex);
        }
    }

    private static void RegisterNotifications()
    {
        try
        {
            _sink = new NotificationClient();
            int hr = _enumerator!.RegisterEndpointNotificationCallback(_sink);
            _notificationsActive = hr == 0;
            if (hr != 0)
                Logger.Warn($"AudioService: RegisterEndpointNotificationCallback failed (0x{hr:X8}); using periodic re-init.");
        }
        catch (Exception ex)
        {
            _notificationsActive = false;
            Logger.Warn("AudioService: device notifications unavailable; using periodic re-init.", ex);
        }
    }
}
