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
assert.ok(xaml.includes('AutomationProperties.Name="Collapse navigation"') && xaml.includes('AutomationProperties.Name="Expand navigation"'), "Rail toggles need accessible names.");
assert.ok(code.includes("NavigationViewPaneDisplayMode.LeftCompact") && code.includes("NavigationViewPaneDisplayMode.Left"), "Responsive layout must use WinUI navigation display modes.");
assert.ok(code.includes("_preferredNavigationCompact || _isWidthConstrained"), "Width constraint and saved preference must be separate.");
assert.ok(prefs.includes("PreferCompactNavigation") && app.includes("NavigationCompactPreferenceChanged"), "Navigation preference must persist.");
assert.ok(code.includes('SelectDestination("queue")') && code.includes("UpdateRailAttention") && code.includes("SubmissionOutcomeUnknown"), "Queue attention must be actionable and state-backed.");
assert.ok(code.includes("UpdateScrobbleStatus") && code.includes("SetCurrentPlayback"), "Existing live playback/status rendering must remain present.");
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
assert.ok(xaml.includes('Orientation="Horizontal" Spacing="8" HorizontalAlignment="Left"') && xaml.includes('<Button x:Name="TrackingToggleButton"') && xaml.includes('<Button x:Name="CurrentTrackLoveButton"') && xaml.includes('<Button x:Name="CurrentTrackUnloveButton"'), "Tracking and Love/Unlove actions should share one compact row.");
assert.ok(xaml.includes('Border Padding="20" CornerRadius="8"') && !xaml.includes('Border Padding="24"'), "Keep the primary Now Playing card slightly roomier and compact secondary cards.");

assert.ok(!xaml.includes("<Storyboard") && !xaml.includes("AcrylicBrush"), "The utility rail must not introduce decorative motion or unsupported new surface treatments.");

// P0 lifecycle ownership regression: App owns disposable synchronization handles and a tray icon.
assert.ok(/partial class App : Microsoft\.UI\.Xaml\.Application, IDisposable/.test(app), "App must implement IDisposable for owned disposable resources.");
for (const resource of ["Semaphore? _singleInstanceSemaphore", "EventWaitHandle? _singleInstanceActivationEvent", "CancellationTokenSource? _singleInstanceListenerCancellation", "TrayIconService? _trayIcon"]) {
  assert.ok(app.includes(resource), `App-owned disposable field missing: ${resource}`);
}
assert.ok(app.includes("if (!TryAcquireSingleInstance())") && app.includes("Exit();"), "Duplicate-instance startup must signal the existing instance and exit.");
assert.ok(/catch\s*\{\s*Dispose\(\);\s*throw;\s*\}/.test(app), "Window construction failure must clean up owned lifecycle resources.");
assert.ok(app.includes("StopSingleInstanceActivationListener();") && app.includes("public void Dispose()"), "Normal close and Dispose must share deterministic cleanup.");
for (const cleanup of ["_singleInstanceActivationEvent?.Dispose();", "_singleInstanceListenerCancellation?.Dispose();", "_singleInstanceSemaphore.Dispose();", "_trayIcon?.Dispose();"]) {
  assert.ok(app.includes(cleanup), `Missing deterministic cleanup: ${cleanup}`);
}

console.log("Desktop UI and App lifecycle contract checks passed. Source-level checks do not replace real Windows UI/accessibility verification.");
