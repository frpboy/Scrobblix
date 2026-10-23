import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";

const read = (path) => readFileSync(path, "utf8");
const xaml = read("src/Scrobblix.Desktop/MainWindow.xaml");
const code = read("src/Scrobblix.Desktop/MainWindow.xaml.cs");
const app = read("src/Scrobblix.Desktop/App.xaml.cs");
const prefs = read("src/Scrobblix.Desktop/ScrobblixUserPreferences.cs");
const tray = read("src/Scrobblix.Desktop/TrayIconService.cs");
const registry = read("src/Scrobblix.Desktop/SourceIdentityRegistry.cs");
const proj = read("src/Scrobblix.Desktop/Scrobblix.Desktop.csproj");
const icon = readFileSync("src/Scrobblix.Desktop/Assets/scrobblix.ico");

for (const destination of ["home", "history", "statistics", "loved", "queue", "sources", "diagnostics", "settings"]) {
  assert.ok(xaml.includes(`Tag="${destination}"`), `Navigation destination missing: ${destination}`);
}
assert.ok(xaml.includes('x:Name="NavigationToggleButton"') && xaml.includes('x:Name="CompactNavigationToggleButton"'), "Both rail toggle states must exist.");
assert.ok(xaml.includes('x:Name="NavigationToggleButton" Grid.Column="2" Width="40" Height="40" Padding="0" Click="NavigationToggleButton_Click"') && xaml.includes('x:Name="CompactNavigationToggleButton" Width="40" Height="40" Padding="0" Click="NavigationToggleButton_Click"'), "Both hamburger toggles must remain interactive 40x40 buttons.");
assert.ok((xaml.match(/Glyph="&#xE700;"/g) ?? []).length >= 2 && !xaml.includes('Glyph="&#xE76B;"') && !xaml.includes('Glyph="&#xE76C;"'), "Both sidebar toggles must use the three-line hamburger glyph instead of chevrons.");
assert.ok(xaml.includes('x:Name="CompactNavigationButtons" Grid.Row="2" Visibility="Collapsed"'), "Collapsed navigation must use a dedicated visible icon rail.");
for (const [name, destination, tooltip] of [
  ["CompactHomeButton", "home", "Home"], ["CompactHistoryButton", "history", "History"],
  ["CompactStatisticsButton", "statistics", "Statistics"], ["CompactLovedButton", "loved", "Loved tracks"],
  ["CompactQueueButton", "queue", "Queue"], ["CompactSourcesButton", "sources", "Sources"],
  ["CompactDiagnosticsButton", "diagnostics", "Diagnostics"], ["CompactSettingsButton", "settings", "Settings"],
]) {
  const button = xaml.match(new RegExp('<Button\\b[^>]*x:Name="' + name + '"[^>]*>'))?.[0];
  assert.ok(button && button.includes('Tag="' + destination + '"') && button.includes('Click="CompactNavigationItem_Click"'), `Collapsed navigation button must open ${destination}.`);
  assert.ok(button.includes('ToolTipService.ToolTip="' + tooltip + '"') && button.includes('AutomationProperties.Name="' + tooltip + '"'), `Collapsed ${destination} icon needs a hover tooltip and accessible name.`);
}

assert.ok(xaml.includes('AutomationProperties.Name="Collapse navigation"') && xaml.includes('AutomationProperties.Name="Expand navigation"'), "Rail toggles need accessible names.");
assert.ok(code.includes("NavigationViewPaneDisplayMode.LeftCompact") && code.includes("NavigationViewPaneDisplayMode.Left"), "Expanded navigation must retain WinUI navigation display modes.");
assert.ok(code.includes("AppNavigationView.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;") && code.includes("CompactNavigationButtons.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;"), "Compact mode must swap in the explicit clickable icon rail.");
assert.ok(code.includes("private void CompactNavigationItem_Click") && code.includes("ShowDestination(destination);"), "Compact navigation must switch views even with NavigationView hidden.");
assert.ok(code.includes("_preferredNavigationCompact || _isWidthConstrained"), "Width constraint and saved preference must be separate.");
assert.ok(prefs.includes("PreferCompactNavigation") && app.includes("NavigationCompactPreferenceChanged"), "Navigation preference must persist.");
assert.ok(code.includes('SelectDestination("queue")') && code.includes("UpdateRailAttention") && code.includes("SubmissionOutcomeUnknown"), "Queue attention must be actionable and state-backed.");
assert.ok(code.includes("UpdateScrobbleStatus") && code.includes("SetCurrentPlayback"), "Existing live playback/status rendering must remain present.");
assert.ok(code.includes("MergeTrackMetadataForSamePlay(observation, previousObservation)") && code.includes("ChooseArtworkForSamePlay(current, previous)"), "Repeated polling must retain same-play enriched metadata and prefer usable provider artwork over weak GSMTC thumbnails.");
assert.ok(code.includes("var displayedObservation = MergeTrackMetadataForSamePlay(enrichedObservation, _displayedObservation ?? observation);") && code.includes("_displayedObservation = displayedObservation;"), "Async metadata enrichment must persist in the displayed observation and survive subsequent polling.");
assert.ok(code.includes("currentBitmap.UriSource is { IsFile: true } currentUri") && code.includes("image.Visibility = Visibility.Visible;") && code.includes("image.Source = bitmap;"), "Artwork loading must avoid restarting an unchanged bitmap and keep the image element visible while decoding.");
assert.ok(registry.includes("Uri.UriSchemeHttps") && registry.includes("File.Exists(path)"), "Provider logos must be HTTPS/exact/local-only and optional.");
assert.ok(tray.includes("LoadImageFromFile") && tray.includes("DestroyIcon"), "Tray icon should load owned ICO and release native handle.");
assert.ok(code.includes("appWindow.SetIcon(appIconPath)"), "Desktop title bar should use app ICO when supported.");
assert.ok(proj.includes("Assets\\*.ico") && proj.includes("Assets\\**\\*.svg"), "Scalable icons and ICO must be copied to output.");
for (const asset of ["scrobblix-mark-light.svg","scrobblix-mark-dark.svg","scrobblix-mark-high-contrast.svg"]) {
  assert.ok(existsSync(`src/Scrobblix.Desktop/Assets/${asset}`), `Brand variant missing: ${asset}`);
}
assert.equal(icon.readUInt16LE(0), 0, "ICO reserved field invalid.");
assert.equal(icon.readUInt16LE(2), 1, "ICO type field invalid.");
assert.ok(icon.readUInt16LE(4) >= 2, "ICO should provide multiple resolution entries.");

const navigationMarkup = xaml.slice(xaml.indexOf('<NavigationView x:Name="AppNavigationView"'), xaml.indexOf('</NavigationView>'));
const railLayout = code.slice(code.indexOf("private void ApplyNavigationLayout()"), code.indexOf("private async void NavigationToggleButton_Click"));

const orderedDestinations = ["home", "history", "statistics", "loved", "queue", "sources", "diagnostics", "settings"];
const destinationPositions = orderedDestinations.map((destination) => navigationMarkup.indexOf(`Tag="${destination}"`));
assert.ok(destinationPositions.every((position) => position >= 0), "Every required destination must exist in the navigation rail.");
assert.ok(destinationPositions.every((position, index) => index === 0 || position > destinationPositions[index - 1]), "Navigation destination order must remain Home, History, Statistics, Loved, Queue, Sources, Diagnostics, Settings.");

for (const [label, destination] of [
  ["Home", "home"], ["History", "history"], ["Statistics", "statistics"], ["Loved", "loved"],
  ["Queue", "queue"], ["Sources", "sources"], ["Diagnostics", "diagnostics"], ["Settings", "settings"],
]) {
  const escaped = destination;
  const item = navigationMarkup.match(new RegExp(`<NavigationViewItem\\b[^>]*Tag="${escaped}"[^>]*/?>`))?.[0];
  assert.ok(item, `Navigation item markup missing: ${label}`);
  assert.ok(item.includes(`AutomationProperties.Name="${label}"`) || (label === "Loved" && item.includes('AutomationProperties.Name="Loved tracks"')), `${label} needs an accessible name.`);
  assert.ok(item.includes("ToolTipService.ToolTip="), `${label} needs a tooltip when the rail is compact.`);
}

assert.ok(xaml.includes('x:Name="NavigationToggleButton" Grid.Column="2" Width="40" Height="40'), "Expanded rail toggle must remain a 40x40 minimum target.");
assert.ok(xaml.includes('x:Name="CompactNavigationToggleButton" Width="40" Height="40'), "Compact rail toggle must remain a 40x40 minimum target.");
assert.ok(code.includes("width < 960") && code.includes("width >= 1000"), "Responsive compaction must preserve its 960/1000 hysteresis band.");
assert.ok(code.includes("new GridLength(compact ? 64 : 240)"), "Rail widths must remain 64px compact / 240px expanded.");
assert.ok(xaml.includes('AutomationProperties.Name="Privacy: local listening data stays on this device"') && xaml.includes('x:Name="PrivacyIcon"'), "Privacy statement must remain accessible in both rail states.");
assert.ok(railLayout.includes("AppNavigationView.PaneDisplayMode") && !railLayout.includes("SelectedItem"), "Rail reflow must not reset the selected destination.");
assert.ok(railLayout.includes("NavigationPreferenceStatusText.Text"), "The current saved/temporary navigation mode must be explained.");
assert.ok(code.includes('Title = destination switch') && code.includes('"queue" => "Scrobblix · Queue"'), "The window title must track the active destination after activation.");

for (const group of ["General", "Last.fm", "Detection", "Notifications", "Privacy", "Storage"]) {
  assert.ok(xaml.includes(`Text="${group}" Style="{StaticResource SubtitleTextBlockStyle}"`), `Settings group missing: ${group}`);
}
assert.ok(xaml.includes('x:Name="AdvancedSettingsDetails"') && /x:Name="AdvancedSettingsDetails"[^>]*Visibility="Collapsed"/.test(xaml), "Advanced settings must stay collapsed by default.");
assert.ok(xaml.includes('x:Name="ToggleAdvancedDetailsButton"') && code.includes("ToggleAdvancedDetailsButton_Click"), "Advanced settings need a working disclosure control.");
assert.ok(prefs.includes("PreferCompactNavigation = false") || prefs.includes("bool PreferCompactNavigation = false"), "Expanded navigation must remain the first-run wide-window default.");
assert.ok(prefs.includes("File.Move(temporaryPath, path, overwrite: true)"), "Preference writes must replace the file atomically.");
assert.ok(!/ApiKey|SharedSecret|SessionKey|Password/i.test(prefs), "Secret material must not enter the UI preference model.");

assert.ok(code.includes("RootLayout.ActualThemeChanged") && code.includes("AccessibilitySettings_HighContrastChanged"), "Rail app mark should react to theme and high-contrast changes.");
assert.ok(code.includes('"scrobblix-mark-high-contrast.svg"') && code.includes('"scrobblix-mark-dark.svg"') && code.includes('"scrobblix-mark-light.svg"'), "All local mark variants must be selectable.");
assert.ok(registry.includes('"Spotify.exe"') && registry.includes('"open.spotify.com"') && registry.includes('"music.youtube.com"') && registry.includes('"jiosaavn.com"'), "Source identities should be mapped only from known adapter IDs/origins.");
assert.ok(registry.includes("uri.Port != 443") && registry.includes("uri.AbsolutePath != \"/\"") && registry.includes("uri.UserInfo"), "Browser source identity must reject non-origin or credential-bearing URLs.");
assert.ok(registry.includes("File.Exists(path) ? assetRelativePath : null"), "Missing/unapproved local logo assets must fall back instead of failing.");
assert.ok(existsSync("src/Scrobblix.Desktop/Assets/providers/README.md"), "External mark provenance ledger must ship with the project.");
assert.ok(!existsSync("src/Scrobblix.Desktop/Assets/providers/lastfm-mark.svg"), "Do not ship Last.fm marks before recorded written permission.");
const assetLedger = read("src/Scrobblix.Desktop/Assets/providers/README.md");
assert.ok(assetLedger.includes("No third-party brand logo files are shipped yet") && assetLedger.includes("prior written approval"), "The source-asset ledger must explain why unapproved marks use text and neutral icons.");

for (const view of ["HomeView", "HistoryView", "StatisticsView", "LovedView", "QueueView", "SourcesView", "DiagnosticsView", "SettingsView"]) {
  assert.ok(xaml.includes(`x:Name="${view}" Grid.Column="1"`), `Core page missing from the scrollable workspace: ${view}`);
}
assert.ok(xaml.includes('Text="Source: not detected"') && xaml.includes('x:Name="CurrentTrackSourceIcon"'), "Unknown playback must retain a readable neutral source fallback.");
assert.ok(xaml.includes('Content="Show artist and track title in notifications (off by default)"'), "Notification track-detail privacy must remain opt-in.");
assert.ok(!xaml.includes('Padding="32"'), "Core pages should not waste workspace with the former 32px outer inset.");
assert.equal((xaml.match(/<StackPanel Padding="24" Spacing="16" MaxWidth=/g) ?? []).length, 8, "All eight page roots should share the compact 24px / 16px content rhythm.");
assert.equal((xaml.match(/<StackPanel Padding="24" Spacing="16" MaxWidth="[^"]+" HorizontalAlignment="Stretch">/g) ?? []).length, 8, "All eight page roots should stretch to use the available workspace instead of leaving a blank right column.");
assert.equal((xaml.match(/<ScrollViewer x:Name="(?:HomeView|HistoryView|StatisticsView|LovedView|QueueView|SourcesView|DiagnosticsView|SettingsView)" Grid.Column="1" HorizontalScrollMode="Disabled" HorizontalScrollBarVisibility="Disabled"/g) ?? []).length, 8, "All core page scroll views should use the viewport width and avoid horizontal scroll waste.");
assert.ok(xaml.includes('x:Name="StartupInfo" IsOpen="False" IsClosable="True"'), "Routine startup/playback status must not reserve Home vertical space.");
assert.ok(code.includes("StartupInfo.IsOpen = isAttention || isTrackingAction;"), "The Home status banner should appear only for attention and explicit pause/resume actions.");
assert.ok(xaml.includes('<StackPanel Spacing="12">') && xaml.includes('Border Padding="12" CornerRadius="6"'), "Now Playing internal rhythm should be compact without removing the focal card.");
assert.ok(xaml.includes('Orientation="Horizontal" Spacing="8" HorizontalAlignment="Left"') && xaml.includes('<Button x:Name="TrackingToggleButton"') && xaml.includes('<Button x:Name="CurrentTrackLoveButton"') && xaml.includes('x:Name="CurrentTrackLoveIcon"') && !xaml.includes('CurrentTrackUnloveButton') && code.includes("_currentTrackLoveIntent == ProviderActionType.Love") && code.includes("Microsoft.UI.Colors.Red"), "Tracking and the single red/neutral Love toggle should share one compact row.");
assert.ok(xaml.includes('Border Padding="20" CornerRadius="8"') && !xaml.includes('Border Padding="24"'), "Keep the primary Now Playing card slightly roomier and compact secondary cards.");

assert.ok(!xaml.includes("<Storyboard") && !xaml.includes("AcrylicBrush"), "The utility rail must not introduce decorative motion or unsupported new surface treatments.");

// P0 lifecycle ownership regression: App owns disposable synchronization handles and a tray icon.
assert.ok(/public partial class App : Microsoft\.UI\.Xaml\.Application, IDisposable/.test(app), "App must implement IDisposable for owned disposable resources.");
for (const resource of ["Semaphore? _singleInstanceSemaphore", "EventWaitHandle? _singleInstanceActivationEvent", "CancellationTokenSource? _singleInstanceListenerCancellation", "TrayIconService? _trayIcon"]) {
  assert.ok(app.includes(resource), `App-owned disposable field missing: ${resource}`);
}
assert.ok(app.includes("if (!TryAcquireSingleInstance())") && app.includes("Exit();"), "Duplicate-instance startup must exit after signaling the existing instance.");
assert.ok(app.includes("_singleInstanceActivationEvent.Set();") && app.includes("_singleInstanceSemaphore.WaitOne(0)"), "Duplicate-instance activation must signal the running instance.");
assert.ok(/catch\s*\{\s*Dispose\(\);\s*throw;\s*\}/.test(app), "Window construction failure must clean up owned lifecycle resources.");
assert.ok(app.includes("StartSingleInstanceActivationListener();"), "The activation listener must start after successful instance acquisition.");
assert.ok(app.includes("StopSingleInstanceActivationListener();") && app.includes("public void Dispose()"), "Normal close and Dispose must share deterministic cleanup.");
for (const cleanup of ["_singleInstanceActivationEvent?.Dispose();", "_singleInstanceListenerCancellation?.Dispose();", "_singleInstanceSemaphore.Dispose();", "_trayIcon?.Dispose();"]) {
  assert.ok(app.includes(cleanup), `Missing deterministic cleanup: ${cleanup}`);
}


// State-aware system tray context-menu contracts.
const trayMain = read("src/Scrobblix.Desktop/TrayIconService.cs");
const queueReader = read("src/Scrobblix.Infrastructure/Storage/SqliteQueueReader.cs");
const manualMatrix = read("Scrobblix-Docs/12_Testing/REAL_WORLD_MANUAL_TEST_MATRIX.md");
const trayImplementationDoc = read("docs/TRAY_CONTEXT_MENU_IMPLEMENTATION.md");

for (const label of [
  "Scrobblix — Running", "Open Scrobblix", "Pause Tracking", "Resume Tracking",
  "Current Track", "Love Track", "Unlove Track", "Copy Track Details",
  "Scrobble Queue", "Retry Failed Scrobbles", "Open Queue",
  "Open Settings", "Open Diagnostics", "Open Log Folder", "Exit Scrobblix",
]) {
  assert.ok((trayMain + "\n" + code).includes(label), `Tray context menu entry missing: ${label}`);
}
for (const stateBinding of [
  "_window.TrayLastFmStatusText", "_window.TrayPlaybackStatusText",
  "_window.TrayPendingQueueStatusText", "_window.TrayTrackingEnabled",
  "_window.TrayCanLoveCurrentTrack", "_window.TrayCanCopyCurrentTrackDetails",
  "_window.TrayCanRetryFailedScrobbles", "_window.TrayCanOpenLogFolder",
  "_window.TrayQueueDeliveryStatusText",
]) {
  assert.ok(trayMain.includes(stateBinding), `Tray action/status is not bound to application state: ${stateBinding}`);
}
assert.ok(trayMain.includes("MfString | MfGrayed | MfDisabled"), "Tray status lines must be non-actionable.");
assert.ok(trayMain.includes("case OpenCommand:") && trayMain.includes("RestoreWindow();"), "Open must restore the existing window.");
assert.ok(trayMain.includes('NavigateAndRestore("queue")') && trayMain.includes('NavigateAndRestore("settings")') && trayMain.includes('NavigateAndRestore("diagnostics")'), "Tray navigation must activate the existing window and navigate to each existing page.");
assert.ok(trayMain.includes("case ExitCommand:") && trayMain.includes("_window.Close();"), "Exit must use the existing close/shutdown path.");
assert.ok(trayMain.includes("Interlocked.Exchange(ref _retryingFailedScrobbles, 1)"), "Tray queue retry needs a duplicate-operation guard.");
assert.ok(trayMain.includes("DestroyMenu(menu)") && trayMain.includes("PostMessage("), "Native context menu resources and focus handling must be cleaned up.");
assert.ok(code.includes("public async Task RequestCurrentTrackLoveFromTrayAsync()") && code.includes("public void CopyTrackDetailsFromTray()"), "Current-track tray commands must call real application handlers.");
assert.ok(code.includes("GetNextCurrentTrackLoveAction()") && code.includes("ProviderActionState.SubmissionOutcomeUnknown") && code.includes("confirmed by Last.fm"), "Love/Unlove must distinguish intent, uncertain outcomes, and confirmed provider acceptance.");
assert.ok(code.includes("_queueSummary.AcceptedCount") && code.includes("_queueSummary.AuthBlockedCount"), "Tray queue status must distinguish completed and authentication-blocked actions.");
assert.ok(code.includes("Clipboard.SetContent(package)") && code.includes('ReportTrayActionFailure("Copy Track Details"'), "Copy Track Details must use the clipboard and handle failures.");
assert.ok(code.includes("public void OpenLogFolderFromTray()") && code.includes("UseShellExecute = true") && code.includes('ReportTrayActionFailure("Open Log Folder"'), "Log Folder must use the configured path and guard launch failures.");
assert.ok(app.includes("_window?.MarkShutdownStarted();") && app.includes("TrayRetryFailedScrobblesRequested += async"), "Tray actions must be shutdown-aware and wired from the application composition root.");
assert.ok(queueReader.includes('command.Parameters.AddWithValue("$provider", "lastfm")') && queueReader.includes("a.action_type = $scrobble") && queueReader.includes("a.state = $retryExhausted"), "Tray retry read model must select only retry-exhausted Last.fm scrobbles.");
assert.ok(!queueReader.includes("a.state IN ($retryScheduled") && !queueReader.includes("state = $outcomeUnknown"), "Tray retry read model must not include scheduled or ambiguous-outcome actions.");
assert.ok(read("tests/Scrobblix.Domain.Tests/Program.cs").includes("Tray retry candidates include only exhausted Last.fm scrobbles"), "The durable queue eligibility regression assertion must remain in the test executable.");
for (const testId of ["TRAY-01", "TRAY-02", "TRAY-03", "TRAY-04", "TRAY-05", "TRAY-06", "TRAY-07", "TRAY-08", "TRAY-09"]) {
  assert.ok(manualMatrix.includes(`| ${testId} |`), `Windows tray manual test missing: ${testId}`);
}
assert.ok(trayImplementationDoc.includes("Manual Windows acceptance: **NOT RUN**"), "Documentation must not imply real Windows acceptance has run.");

console.log("Desktop UI contract checks passed. Source-level checks do not replace real Windows UI/accessibility verification.");
