using System.Reflection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Scrobblix.Domain.Updates;

namespace Scrobblix.Desktop;

/// <summary>
/// Manual update check UI: check, changelog, guided verified download, user-confirmed installer launch.
/// No automatic checks, no silent install, no signing change.
/// </summary>
public sealed partial class MainWindow
{
    private bool _updateCheckInFlight;
    private readonly UpdateCheckService _updateCheck = new();

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
        UpdateCheckStatusText.Text = "Checking the official release channel…";
        try
        {
            var latest = await _updateCheck.FindLatestAsync(CancellationToken.None);
            if (latest is null)
            {
                UpdateCheckStatusText.Text = "No published Windows release was found. Your build is unchanged.";
                return;
            }
            var revision = BuildSourceRevision();
            if (revision is null)
            {
                UpdateCheckStatusText.Text = $"Latest published preview: {latest.Tag}. This build does not record its source revision, so it cannot tell whether it is current.";
                await ShowUpdateDialogAsync(latest,
                    "This build does not record its source revision, so Scrobblix cannot confirm whether you already have this preview. Review the notes and download only if you want it.");
                return;
            }
            var state = await _updateCheck.CompareAsync(revision, latest.Tag, CancellationToken.None);
            switch (state)
            {
                case WindowsUpdateState.UpToDate:
                    UpdateCheckStatusText.Text = $"Scrobblix is up to date ({latest.Tag}).";
                    return;
                case WindowsUpdateState.NewerThanRelease:
                    UpdateCheckStatusText.Text = $"This build is newer than the latest published preview ({latest.Tag}). Nothing to do.";
                    return;
                case WindowsUpdateState.Unknown:
                    UpdateCheckStatusText.Text = $"Latest published preview: {latest.Tag}. Scrobblix could not determine how it compares to your build.";
                    await ShowUpdateDialogAsync(latest,
                        "Scrobblix could not determine whether this preview is newer than your build. Review the notes and download only if you want it.");
                    return;
                default:
                    UpdateCheckStatusText.Text = $"Update available: {latest.Tag}.";
                    await ShowUpdateDialogAsync(latest,
                        "A newer preview is available. Review the notes, then download and verify it. Nothing installs by itself.");
                    return;
            }
        }
        catch (UpdateCheckService.UpdateCheckException failure)
        {
            UpdateCheckStatusText.Text = failure.Message + " Your installed version is unchanged.";
        }
        catch
        {
            UpdateCheckStatusText.Text = "The update check failed before a usable response. Your installed version is unchanged.";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
            _updateCheckInFlight = false;
        }
    }

    /// <summary>The changelog dialog content as an overlay element (used by UI qualification; popups cannot be pixel-captured).
    /// Layout/text evidence only: this is a separate disabled overlay tree, not proof of actual dialog buttons, focus or input.</summary>
    internal UIElement BuildUpdateChangelogOverlay(WindowsRelease release, string lead)
    {
        var notes = release.Notes.Trim();
        if (notes.Length > 4000) notes = notes[..4000] + "…";
        return DialogOverlay($"Update available: {release.Tag}",
            lead + "\n\n" + (notes.Length == 0 ? "This release has no published notes." : notes),
            "Download update", "View on GitHub", "Not now");
    }

    /// <summary>The verified-download confirmation content as an overlay element (UI qualification only).
    /// Layout/text evidence only, not actual dialog behavior proof.</summary>
    internal UIElement BuildInstallerVerifiedOverlay(string installerName) =>
        DialogOverlay("Installer downloaded and verified",
            installerName + " passed integrity verification against the published SHA-256 checksum. A verified copy is in your Downloads folder."
            + "\n\nThe installer is unsigned, so Windows will show a SmartScreen warning. Opening it starts the usual installer; Scrobblix stays running until you close it.",
            "Open installer", null, "Later");

    private static UIElement DialogOverlay(string title, string body, string primary, string? secondary, string close)
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(new Button { Content = primary, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["AccentButtonStyle"], IsEnabled = false });
        if (secondary is not null) buttons.Children.Add(new Button { Content = secondary, IsEnabled = false });
        buttons.Children.Add(new Button { Content = close, IsEnabled = false });
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = title, Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["SubtitleTextBlockStyle"] });
        panel.Children.Add(new ScrollViewer { MaxHeight = 320, Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap } });
        panel.Children.Add(buttons);
        return new Border
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
    }

    private async Task ShowUpdateDialogAsync(WindowsRelease release, string lead)
    {
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
        if (result == ContentDialogResult.Secondary)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://github.com/frpboy/Scrobblix/releases/tag/" + release.Tag) { UseShellExecute = true });
            }
            catch
            {
                UpdateCheckStatusText.Text = "The releases page could not be opened in the browser.";
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
            UpdateCheckStatusText.Text = value.Total is { } total
                ? $"Downloading… {value.Done / 1048576} of {total / 1048576} MB"
                : $"Downloading… {value.Done / 1048576} MB");
        try
        {
            UpdateCheckStatusText.Text = "Downloading the installer…";
            var download = await _updateCheck.DownloadInstallerAsync(release, progress, CancellationToken.None);
            UpdateCheckStatusText.Text = $"Downloaded and verified {download.Installer.Name}.";
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
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            UpdateCheckStatusText.Text = _updateCheck.OpenInstallerVerified(download.StagedPath, download.Sha256)
                ? "The installer was opened. Finish the steps in Windows; Scrobblix keeps running until you close it."
                : "The installer could not be opened, or its file changed after verification." + (download.DownloadsPath is null ? "" : " A verified copy is in your Downloads folder.");
        }
        catch (UpdateCheckService.UpdateCheckException failure)
        {
            UpdateCheckStatusText.Text = failure.Message + " Nothing was installed.";
        }
        catch
        {
            UpdateCheckStatusText.Text = "The download could not be completed. Nothing was installed.";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
        }
    }
}
