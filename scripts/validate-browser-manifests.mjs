import { readFile, access } from "node:fs/promises";
import { resolve } from "node:path";

const root = process.cwd();
const extensionRoot = resolve(root, "browser-extension");
const readJson = async (path) => JSON.parse(await readFile(resolve(root, path), "utf8"));
const chromium = await readJson("browser-extension/manifest.json");
const firefox = await readJson("browser-extension/manifest.firefox.json");
const requireCondition = (condition, message) => {
  if (!condition) {
    throw new Error(message);
  }
};

requireCondition(chromium.manifest_version === 3, "Chromium companion must use Manifest V3.");
requireCondition(chromium.background?.service_worker === "dist/service-worker.js", "Chromium service worker is missing.");
requireCondition(chromium.action?.default_popup === "popup.html", "Chromium companion controls must have a popup.");
requireCondition(!chromium.background?.scripts, "Chromium Manifest V3 must not use the legacy background.scripts field.");
requireCondition(chromium.content_scripts?.some(script => script.js?.includes("dist/media-observer.js")), "Chromium media observer is missing.");
requireCondition(chromium.content_scripts?.some(script => script.matches?.includes("https://www.youtube.com/*")), "Chromium media observer must support YouTube.");
requireCondition(chromium.content_scripts?.some(script => script.matches?.includes("https://youtube.com/*")), "Chromium media observer must support bare YouTube URLs.");
requireCondition(chromium.host_permissions?.includes("https://www.youtube.com/*"), "Chromium YouTube access must be granted at installation.");
requireCondition(chromium.optional_host_permissions?.includes("https://*/*"), "Chromium companion must request additional sites only when the user chooses them.");
requireCondition(firefox.manifest_version === 3, "Firefox companion must use Manifest V3.");
requireCondition(firefox.background?.scripts?.includes("dist/service-worker.js"), "Firefox background script is missing.");
requireCondition(firefox.action?.default_popup === "popup.html", "Firefox companion controls must have a popup.");
requireCondition(!firefox.background?.service_worker, "Firefox manifest must not depend on a service_worker property.");
requireCondition(firefox.browser_specific_settings?.gecko?.id === "scrobblix@frpboy.in", "Firefox add-on ID must match the registered native-host identity.");
requireCondition(firefox.content_scripts?.some(script => script.js?.includes("dist/media-observer.js")), "Firefox media observer is missing.");
requireCondition(firefox.content_scripts?.some(script => script.matches?.includes("https://www.youtube.com/*")), "Firefox media observer must support YouTube.");

for (const path of ["dist/service-worker.js", "dist/media-observer.js", "dist/message-validation.js", "dist/popup.js", "popup.html", "popup.css"]) {
  await access(resolve(extensionRoot, path));
}

const nativeHostValidator = await readFile(resolve(root, "src/Scrobblix.NativeHost/NativeHostCallerValidator.cs"), "utf8");
const nativeHostProgram = await readFile(resolve(root, "src/Scrobblix.NativeHost/Program.cs"), "utf8");
requireCondition(nativeHostValidator.includes("ReadAllowedFirefoxExtensionIds"), "Native host must read the Firefox add-on allow-list.");
requireCondition(nativeHostValidator.includes("MatchesFirefoxExtensionId"), "Native host must validate Firefox add-on identity.");
requireCondition(nativeHostProgram.includes("firefoxCallerId"), "Native host must parse Firefox native-messaging arguments.");
requireCondition(nativeHostProgram.includes("var trustedEnvelope"), "Native host must compute sender trust before message dispatch.");
requireCondition(nativeHostProgram.indexOf("if (!trustedEnvelope)") < nativeHostProgram.indexOf('else if (type == "ping.v1")'), "Native host must reject an untrusted sender before responding to ping.");
const nativeHostInstaller = await readFile(resolve(root, "scripts/install-native-host.ps1"), "utf8");
const nativeHostUninstaller = await readFile(resolve(root, "scripts/uninstall-native-host.ps1"), "utf8");
requireCondition(nativeHostInstaller.includes("[string[]]$ExtensionId"), "Native-host setup must support different Chrome and Edge extension IDs.");
requireCondition(nativeHostInstaller.includes("allowed_origins = $allowedOrigins"), "Native-host manifest must allow-list every supplied Chromium extension ID.");
requireCondition(nativeHostInstaller.includes("($extensionIds -join ';')"), "Desktop allow-list must contain every registered Chromium extension ID.");
requireCondition(nativeHostInstaller.includes("$RegisterFirefox"), "The native-host installer must support optional Firefox registration.");
requireCondition(nativeHostInstaller.includes("AllowedFirefoxExtensionIds"), "The installer must write the Firefox add-on allow-list.");
requireCondition(nativeHostUninstaller.includes("Mozilla\\NativeMessagingHosts"), "The uninstaller must remove Firefox native messaging registration.");

console.log("Chromium and Firefox manifests, emitted scripts, and Firefox host identity hooks are valid.");

const serviceWorker = await readFile(resolve(root, "browser-extension/src/service-worker.ts"), "utf8");
const popupSource = await readFile(resolve(root, "browser-extension/src/popup.ts"), "utf8");
const popupHtml = await readFile(resolve(root, "browser-extension/popup.html"), "utf8");
requireCondition(serviceWorker.includes('const hostName = "com.frpboy.scrobblix"'), "Extension must request the documented native host name.");
requireCondition(serviceWorker.includes('"HOST_NOT_REGISTERED"'), "Connection diagnostics must classify host-resolution failures.");
requireCondition(serviceWorker.includes('isErrorResponse(response) && response.messageId === messageId'), "Native host diagnostic errors must correlate to the current request ID.");
requireCondition(serviceWorker.includes('"EXTENSION_NOT_AUTHORIZED"'), "Connection diagnostics must distinguish authorization failures.");
requireCondition(serviceWorker.includes('"CONNECTION_TIMEOUT"'), "Connection diagnostics must classify handshake timeout.");
requireCondition(serviceWorker.includes('candidate.messageId === messageId'), "Connection success must validate the reply message ID.");
requireCondition(serviceWorker.includes('candidate.protocolVersion === 1'), "Connection success must validate the protocol version.");
requireCondition(serviceWorker.includes('candidate.type === expectedType'), "Connection success must validate the exact pong response type.");
requireCondition(popupSource.includes("connectionTestRunning"), "Popup test button must prevent overlapping connection tests.");
requireCondition(popupSource.includes("response.code === \"CONNECTION_SUCCESS\""), "Popup must not report success without the explicit success category.");
requireCondition(popupHtml.includes('id="native-recovery"'), "Popup must display actionable recovery instructions.");
requireCondition(popupHtml.includes('id="native-diagnostic"'), "Popup must display a diagnostic identifier.");

const setupScript = await readFile(resolve(root, "scripts/setup-browser-companion.ps1"), "utf8");
requireCondition(setupScript.includes("Build/package and registration are separate steps"), "Setup helper must force package build and browser registration into separate invocations.");
