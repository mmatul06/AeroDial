// AeroDial — MediaInfoService.cs
// Now-playing media info via Windows GlobalSystemMediaTransportControlsSessionManager (GSMTC).
// Event-driven (no polling): subscribes to the active session so the title updates live when
// the track changes — including when Next/Previous is triggered from the overlay. Exposes a
// cached string the render thread reads each frame (same discipline as the volume level).

using System;
using System.Threading.Tasks;
using Windows.Media.Control;

namespace AeroDial.Core;

internal sealed class MediaInfoService : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession?        _session;

    // string is a reference type — assignment is atomic, so the render thread can read freely.
    private volatile string _nowPlaying = string.Empty;
    private volatile bool   _isPlaying;

    /// <summary>"Title • Artist" (or just the title) of the current media, or "" if nothing.</summary>
    public string NowPlaying => _nowPlaying;

    /// <summary>True when a media session reports the Playing state.</summary>
    public bool IsPlaying => _isPlaying;

    public async void Start()
    {
        try
        {
            _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            _manager.CurrentSessionChanged += OnCurrentSessionChanged;
            HookSession(_manager.GetCurrentSession());
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Logger.Warn("MediaInfoService could not start (now-playing disabled).", ex);
        }
    }

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        HookSession(sender.GetCurrentSession());
        _ = RefreshAsync();
    }

    private void HookSession(GlobalSystemMediaTransportControlsSession? session)
    {
        if (ReferenceEquals(session, _session)) return;

        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged    -= OnPlaybackInfoChanged;
        }

        _session = session;

        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged    += OnPlaybackInfoChanged;
        }
    }

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession s, MediaPropertiesChangedEventArgs a) => _ = RefreshAsync();

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession s, PlaybackInfoChangedEventArgs a) => _ = RefreshAsync();

    private async Task RefreshAsync()
    {
        try
        {
            var session = _session;
            if (session is null) { _nowPlaying = string.Empty; _isPlaying = false; return; }

            var playback = session.GetPlaybackInfo();
            _isPlaying = playback?.PlaybackStatus
                == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

            var props  = await session.TryGetMediaPropertiesAsync();
            var title  = props?.Title?.Trim()  ?? string.Empty;
            var artist = props?.Artist?.Trim() ?? string.Empty;

            _nowPlaying = title.Length == 0
                ? string.Empty
                : artist.Length > 0 ? $"{title}  •  {artist}" : title;
        }
        catch (Exception ex)
        {
            Logger.Warn("MediaInfoService refresh failed.", ex);
            _nowPlaying = string.Empty;
        }
    }

    public void Dispose()
    {
        try
        {
            if (_session is not null)
            {
                _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _session.PlaybackInfoChanged    -= OnPlaybackInfoChanged;
            }
            if (_manager is not null)
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }
        catch { /* best effort */ }
    }
}
