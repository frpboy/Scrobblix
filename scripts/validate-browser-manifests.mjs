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
requireCondition(!chromium.background?.scripts, "Chromium Manifest V3 must not use Firefox background scripts.");
requireCondition(chromium.content_scripts?.some(script => script.js?.includes("dist/media-observer.js")), "Chromium media observer is missing.");
requireCondition(chromium.content_scripts?.some(script => script.matches?.includes("https://www.youtube.com/*")), "Chromium media observer must support YouTube.");
requireCondition(firefox.manifest_version === 3, "Firefox companion must use Manifest V3.");
requireCondition(firefox.background?.scripts?.includes("dist/service-worker.js"), "Firefox background script is missing.");
requireCondition(!firefox.background?.service_worker, "Firefox manifest must not depend on a service_worker property.");
requireCondition(firefox.browser_specific_settings?.gecko?.id === "scrobblix@frpboy.in", "Firefox add-on ID must match the registered native-host identity.");
requireCondition(firefox.content_scripts?.some(script => script.js?.includes("dist/media-observer.js")), "Firefox media observer is missing.");
requireCondition(firefox.content_scripts?.some(script => script.matches?.includes("https://www.youtube.com/*")), "Firefox media observer must support YouTube.");

for (const path of ["dist/service-worker.js", "dist/media-observer.js"]) {
  await access(resolve(extensionRoot, path));
}

const nativeHostValidator = await readFile(resolve(root, "src/Scrobblix.NativeHost/NativeHostCallerValidator.cs"), "utf8");
const nativeHostProgram = await readFile(resolve(root, "src/Scrobblix.NativeHost/Program.cs"), "utf8");
requireCondition(nativeHostValidator.includes("ReadAllowedFirefoxExtensionIds"), "Native host must read the Firefox add-on allow-list.");
requireCondition(nativeHostValidator.includes("MatchesFirefoxExtensionId"), "Native host must validate Firefox add-on identity.");
requireCondition(nativeHostProgram.includes("firefoxCallerId"), "Native host must parse Firefox native-messaging arguments.");
const nativeHostInstaller = await readFile(resolve(root, "scripts/install-native-host.ps1"), "utf8");
const nativeHostUninstaller = await readFile(resolve(root, "scripts/uninstall-native-host.ps1"), "utf8");
requireCondition(nativeHostInstaller.includes("$RegisterFirefox"), "The native-host installer must support optional Firefox registration.");
requireCondition(nativeHostInstaller.includes("AllowedFirefoxExtensionIds"), "The installer must write the Firefox add-on allow-list.");
requireCondition(nativeHostUninstaller.includes("Mozilla\\NativeMessagingHosts"), "The uninstaller must remove Firefox native messaging registration.");

console.log("Chromium and Firefox manifests, emitted scripts, and Firefox host identity hooks are valid.");
