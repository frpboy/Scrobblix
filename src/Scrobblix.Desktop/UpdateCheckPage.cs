using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Scrobblix.Domain.Updates;

namespace Scrobblix.Desktop;

/// <summary>
/// Manual update check UI: check, changelog, guided verified download, user-confirmed installer launch.
/// No automatic checks, no silent install, no signing change.
/// </summary>
public sealed partial class MainWindow : IDisposable
{
    private bool _updateCheckInFlight;
    private bool _updateCheckDisposed;
    private readonly UpdateCheckService _updateCheck = new();
    private readonly CancellationTokenSource _updateCheckCancellation = new();

    /// <summary>Wired once from the constructor's Closed handler: cancel any in-flight check or
    /// download first so no request continues against a disposed client, then dispose the chain.</summary>
    public void Dispose()
    {
        if (_updateCheckDisposed) return;
        _updateCheckDisposed = true;
        _updateCheckCancellation.Cancel();
        _updateCheck.Dispose();
        _updateCheckCancellation.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>Status updates are dropped once the window is closed or the update chain is disposed,
    /// so cancelled continuations never touch dead UI or surface shutdown noise.</summary>
    private void SetUpdateStatus(string message)
    {
        if (_isClosed || _updateCheckDisposed) return;
        UpdateCheckStatusText.Text = message;
    }

    private static string? BuildSourceRevision()
    {
        var information = typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return WindowsUpdateDecision.SourceRevision(information);
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs args)
    {
        if (_updateCheckInFlight) return;
        _updateCheckInFlight = true;
        CheckUpdatesButton.IsEnabled = false;
        SetUpdateStatus("Checking the official release channel…");
        try
        {
            var latest = await _updateCheck.FindLatestAsync(_updateCheckCancellation.Token);
            if (latest is null)
            {
                SetUpdateStatus("No published Windows release was found. Your build is unchanged.");
                return;
            }
            var revision = BuildSourceRevision();
            if (revision is null)
            {
                SetUpdateStatus($"Latest published preview: {latest.Tag}. This build does not record its source revision, so it cannot tell whether it is current.");
                await ShowUpdateDialogAsync(latest,
                    "This build does not record its source revision, so Scrobblix cannot confirm whether you already have this preview. Review the notes and download only if you want it.");
                return;
            }
            var state = await _updateCheck.CompareAsync(revision, latest.Tag, _updateCheckCancellation.Token);
            switch (state)
            {
                case WindowsUpdateState.UpToDate:
                    SetUpdateStatus($"Scrobblix is up to date ({latest.Tag}).");
                    return;
                case WindowsUpdateState.NewerThanRelease:
                    SetUpdateStatus($"This build is newer than the latest published preview ({latest.Tag}). Nothing to do.");
                    return;
                case WindowsUpdateState.Unknown:
                    SetUpdateStatus($"Latest published preview: {latest.Tag}. Scrobblix could not determine how it compares to your build.");
                    await ShowUpdateDialogAsync(latest,
                        "Scrobblix could not determine whether this preview is newer than your build. Review the notes and download only if you want it.");
                    return;
                default:
                    SetUpdateStatus($"Update available: {latest.Tag}.");
                    await ShowUpdateDialogAsync(latest,
                        "A newer preview is available. Review the notes, then download and verify it. Nothing installs by itself.");
                    return;
            }
        }
        catch (OperationCanceledException)
        {
            // Window closing: the cancellation was requested by Dispose. No UI, no error text.
        }
        catch (UpdateCheckService.UpdateCheckException failure)
        {
            SetUpdateStatus(failure.Message + " Your installed version is unchanged.");
        }
        catch
        {
            SetUpdateStatus("The update check failed before a usable response. Your installed version is unchanged.");
        }
        finally
        {
            _updateCheckInFlight = false;
            if (!_isClosed && !_updateCheckDisposed) CheckUpdatesButton.IsEnabled = true;
        }
    }

    /// <summary>The changelog dialog content as an overlay element (used by UI qualification; popups cannot be pixel-captured).
    /// Layout/text evidence only: this is a separate disabled overlay tree, not proof of actual dialog buttons, focus or input.</summary>
    internal static UIElement BuildUpdateChangelogOverlay(WindowsRelease release, string lead)
    {
        var notes = release.Notes.Trim();
        if (notes.Length > 4000) notes = notes[..4000] + "…";
        return DialogOverlay($"Update available: {release.Tag}",
            lead + "\n\n" + (notes.Length == 0 ? "This release has no published notes." : notes),
            "Download update", "View on GitHub", "Not now");
    }

    /// <summary>The verified-download confirmation content as an overlay element (UI qualification only).
    /// Layout/text evidence only, not actual dialog behavior proof.</summary>
    internal static UIElement BuildInstallerVerifiedOverlay(string installerName) =>
        DialogOverlay("Installer downloaded and verified",
            installerName + " passed integrity verification against the published SHA-256 checksum. A verified copy is in your Downloads folder."
            + "\n\nThe installer is unsigned, so Windows will show a SmartScreen warning. Opening it starts the usual installer; Scrobblix stays running until you close it.",
            "Open installer", null, "Later");

    private static Border DialogOverlay(string title, string body, string primary, string? secondary, string close)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(new Button { Content = primary, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"], IsEnabled = false });
        if (secondary is not null) buttons.Children.Add(new Button { Content = secondary, IsEnabled = false });
        buttons.Children.Add(new Button { Content = close, IsEnabled = false });
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = title, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["SubtitleTextBlockStyle"] });
        panel.Children.Add(new ScrollViewer { MaxHeight = 320, Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap } });
        panel.Children.Add(buttons);
        // Hosted in RootLayout, a two-column grid (nav + content): an overlay that stays in
        // column 0 renders as a cramped strip over the rail instead of dimming the window.
        var scrim = new Border
        {
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.4 },
            Child = new Border
            {
                MaxWidth = 560,
                MaxHeight = 480,
                Padding = new Thickness(24),
                CornerRadius = new CornerRadius(8),
                Background = (Microsoft.UI.Xaml.Media.Brush)Microsoft.UI.Xaml.Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = panel,
            },
        };
        Grid.SetColumnSpan(scrim, 2);
        return scrim;
    }

    private async Task ShowUpdateDialogAsync(WindowsRelease release, string lead)
    {
        if (_isClosed || _updateCheckDisposed) return;
        var notes = release.Notes.Trim();
        if (notes.Length > 4000) notes = notes[..4000] + "…";
        var dialog = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = $"Update available: {release.Tag}",
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = lead + "\n\n" + (notes.Length == 0 ? "This release has no published notes." : notes),
                },
            },
            PrimaryButtonText = "Download update",
            SecondaryButtonText = "View on GitHub",
            CloseButtonText = "Not now",
            DefaultButton = ContentDialogButton.Close,
        };
        var result = await dialog.ShowAsync();
        if (_isClosed || _updateCheckDisposed) return;
        if (result == ContentDialogResult.Secondary)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://github.com/frpboy/Scrobblix/releases/tag/" + release.Tag) { UseShellExecute = true });
            }
            catch
            {
                SetUpdateStatus("The releases page could not be opened in the browser.");
            }
            return;
        }
        if (result != ContentDialogResult.Primary) return;
        await DownloadAndVerifyAsync(release);
    }

    private async Task DownloadAndVerifyAsync(WindowsRelease release)
    {
        CheckUpdatesButton.IsEnabled = false;
        var progress = new Progress<(long Done, long? Total)>(value =>
            SetUpdateStatus(value.Total is { } total
                ? $"Downloading… {value.Done / 1048576} of {total / 1048576} MB"
                : $"Downloading… {value.Done / 1048576} MB"));
        try
        {
            SetUpdateStatus("Downloading the installer…");
            var download = await _updateCheck.DownloadInstallerAsync(release, progress, _updateCheckCancellation.Token);
            if (_isClosed || _updateCheckDisposed) return;
            SetUpdateStatus($"Downloaded and verified {download.Installer.Name}.");
            var confirm = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Installer downloaded and verified",
                Content = download.Installer.Name + " passed integrity verification against the published SHA-256 checksum."
                    + (download.DownloadsPath is null ? "" : " A verified copy is in your Downloads folder.")
                    + "\n\nThe installer is unsigned, so Windows will show a SmartScreen warning. Opening it starts the usual installer; Scrobblix stays running until you close it.",
                PrimaryButtonText = "Open installer",
                CloseButtonText = "Later",
                DefaultButton = ContentDialogButton.Close,
            };
            if (_isClosed || _updateCheckDisposed) return;
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            if (_isClosed || _updateCheckDisposed) return;
            SetUpdateStatus(_updateCheck.OpenInstallerVerified(download.StagedPath, download.Sha256)
                ? "The installer was opened. Finish the steps in Windows; Scrobblix keeps running until you close it."
                : "The installer could not be opened, or its file changed after verification." + (download.DownloadsPath is null ? "" : " A verified copy is in your Downloads folder."));
        }
        catch (OperationCanceledException)
        {
            // Window closing: cancelled by Dispose. Nothing was installed.
        }
        catch (UpdateCheckService.UpdateCheckException failure)
        {
            SetUpdateStatus(failure.Message + " Nothing was installed.");
        }
        catch
        {
            SetUpdateStatus("The download could not be completed. Nothing was installed.");
        }
        finally
        {
            if (!_isClosed && !_updateCheckDisposed) CheckUpdatesButton.IsEnabled = true;
        }
    }
}
