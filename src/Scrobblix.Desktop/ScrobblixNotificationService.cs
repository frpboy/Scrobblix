using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Scrobblix.Domain.Queue;

namespace Scrobblix.Desktop;

/// <summary>
/// Best-effort Windows app notifications. Notifications are disabled by default and must
/// be explicitly enabled in Settings. Toast text never includes credentials or session data.
/// </summary>
internal sealed class ScrobblixNotificationService(Action<string> log, Action activated) : IDisposable
{
    private readonly AppNotificationManager _manager = AppNotificationManager.Default;
    private bool _registered;
    private bool _enabled;
    private bool _includeTrackDetailsInNotifications;
    private bool _disposed;

    public bool IsEnabled => _enabled;

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (enabled == _enabled && (!enabled || _registered))
        {
            return;
        }

        if (enabled)
        {
            _manager.NotificationInvoked += OnNotificationInvoked;
            try
            {
                _manager.Register();
                _registered = true;
                _enabled = true;
                log("Windows app notifications were enabled by the user.");
            }
            catch
            {
                _manager.NotificationInvoked -= OnNotificationInvoked;
                _registered = false;
                _enabled = false;
                throw;
            }

            return;
        }

        _enabled = false;
        if (_registered)
        {
            try
            {
                _manager.Unregister();
            }
            finally
            {
                _manager.NotificationInvoked -= OnNotificationInvoked;
                _registered = false;
            }
        }

        log("Windows app notifications are disabled.");
    }

    public void SetIncludeTrackDetails(bool includeTrackDetails)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _includeTrackDetailsInNotifications = includeTrackDetails;
        log(includeTrackDetails
            ? "Track and artist details are enabled for Windows notifications by the user."
            : "Track and artist details are hidden in Windows notifications.");
    }

    public void NotifyScrobbleAccepted(string? artist, string? title)
    {
        Show(
            "Scrobbled to Last.fm",
            _includeTrackDetailsInNotifications ? FormatTrack(artist, title) : "A listening record was accepted by Last.fm.",
            "The provider accepted this listening record. Open Scrobblix to review your local history.",
            "scrobble.accepted");
    }

    public void NotifyDeliveryNeedsAttention(string? artist, string? title, ProviderActionState state, string reasonCode)
    {
        if (state is not (ProviderActionState.AuthBlocked
            or ProviderActionState.RetryExhausted
            or ProviderActionState.SubmissionOutcomeUnknown
            or ProviderActionState.PermanentFailure))
        {
            return;
        }

        Show(
            "Scrobble needs attention",
            _includeTrackDetailsInNotifications ? FormatTrack(artist, title) : "Track details are hidden by your notification privacy setting.",
            $"Delivery status: {FriendlyState(state)}. Open Scrobblix Queue to review the recorded reason ({LimitText(reasonCode, 100)}).",
            "scrobble.attention");
    }

    private void Show(string title, string track, string detail, string category)
    {
        if (!_enabled || !_registered || _disposed)
        {
            return;
        }

        try
        {
            var notification = new AppNotificationBuilder()
                .AddArgument("action", "show")
                .AddArgument("category", category)
                .AddText(LimitText(title, 120))
                .AddText(LimitText(track, 240))
                .AddText(LimitText(detail, 250))
                .BuildNotification();
            _manager.Show(notification);
        }
        catch (Exception exception)
        {
            log($"Windows notification could not be displayed; category={category}; exception={exception.GetType().Name}; hresult={exception.HResult}.");
        }
    }

    private void OnNotificationInvoked(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
    {
        log("A Scrobblix Windows notification was activated.");
        try
        {
            activated();
        }
        catch (Exception exception)
        {
            log($"Notification activation could not restore the Scrobblix window; exception={exception.GetType().Name}; hresult={exception.HResult}.");
        }
    }

    private static string FormatTrack(string? artist, string? title)
    {
        var cleanArtist = string.IsNullOrWhiteSpace(artist) ? "Unknown artist" : LimitText(artist.Trim(), 100);
        var cleanTitle = string.IsNullOrWhiteSpace(title) ? "Unknown track" : LimitText(title.Trim(), 120);
        return $"{cleanArtist} — {cleanTitle}";
    }

    private static string FriendlyState(ProviderActionState state) => state switch
    {
        ProviderActionState.AuthBlocked => "Last.fm authentication needs attention",
        ProviderActionState.RetryExhausted => "automatic retries were exhausted",
        ProviderActionState.SubmissionOutcomeUnknown => "the provider response was ambiguous",
        ProviderActionState.PermanentFailure => "the provider rejected the action",
        _ => "delivery needs review",
    };

    private static string LimitText(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _enabled = false;
        if (_registered)
        {
            try
            {
                _manager.Unregister();
            }
            catch (Exception exception)
            {
                log($"Windows notification registration cleanup failed; exception={exception.GetType().Name}; hresult={exception.HResult}.");
            }
            finally
            {
                _manager.NotificationInvoked -= OnNotificationInvoked;
                _registered = false;
            }
        }
    }
}
