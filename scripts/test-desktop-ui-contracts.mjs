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
console.log("Desktop UI contract checks passed. Source-level checks do not replace real Windows UI/accessibility verification.");
