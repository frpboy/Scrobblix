namespace Scrobblix.Application.Tracking;

public sealed class TrackingMode
{
    public bool IsEnabled { get; private set; } = true;

    public bool SetEnabled(bool isEnabled)
    {
        if (IsEnabled == isEnabled)
        {
            return false;
        }

        IsEnabled = isEnabled;
        return true;
    }
}
