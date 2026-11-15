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
            var latest = await UpdateCheckService.FindLatestAsync(CancellationToken.None);
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
            var state = await UpdateCheckService.CompareAsync(revision, latest.Tag, CancellationToken.None);
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
            var download = await UpdateCheckService.DownloadInstallerAsync(release, progress, CancellationToken.None);
            UpdateCheckStatusText.Text = $"Downloaded and verified {download.Installer.Name}.";
            var confirm = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Installer downloaded and verified",
                Content = download.Installer.Name + " was saved to your Downloads folder and its SHA-256 matches the published checksum."
                    + "\n\nThe installer is unsigned, so Windows will show a SmartScreen warning. Opening it starts the usual installer; Scrobblix stays running until you close it.",
                PrimaryButtonText = "Open installer",
                CloseButtonText = "Later",
                DefaultButton = ContentDialogButton.Close,
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            UpdateCheckStatusText.Text = UpdateCheckService.OpenInstaller(download.Path)
                ? "The installer was opened. Finish the steps in Windows; Scrobblix keeps running until you close it."
                : "The installer could not be opened. It is saved in your Downloads folder.";
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
