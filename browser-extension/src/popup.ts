const popupApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);

export {};

type CustomSite = { id: string; origin: string; pattern: string };
type DiagnosticCode =
  | "HOST_NOT_REGISTERED"
  | "MANIFEST_MISSING_OR_INVALID"
  | "HOST_EXECUTABLE_MISSING"
  | "EXTENSION_NOT_AUTHORIZED"
  | "HOST_STARTUP_FAILED"
  | "PROTOCOL_ERROR"
  | "CONNECTION_TIMEOUT"
  | "CONNECTION_SUCCESS"
  | "UNKNOWN_CONNECTION_ERROR";

type NativeHostStatus = {
  state: "connected" | "unavailable";
  code: DiagnosticCode;
  detail: string;
  recovery: string;
  restartRequired: boolean;
  diagnosticId: string;
};

const statusText = document.querySelector<HTMLElement>("#native-status")!;
const currentSiteText = document.querySelector<HTMLElement>("#current-site")!;
const pauseButton = document.querySelector<HTMLButtonElement>("#pause")!;
const trackButton = document.querySelector<HTMLButtonElement>("#track-site")!;
const customSites = document.querySelector<HTMLUListElement>("#custom-sites")!;
const connectionButton = document.querySelector<HTMLButtonElement>("#test-connection")!;
const connectionRecovery = document.querySelector<HTMLElement>("#native-recovery")!;
const connectionCode = document.querySelector<HTMLElement>("#native-diagnostic")!;

let currentSite: { origin: string; pattern: string } | undefined;
let connectionTestRunning = false;

// Wire the diagnostic control synchronously before async storage, tab, or permission
// initialization. It must remain usable even when one of those setup calls stalls.
connectionButton.addEventListener("click", () => void testConnection());

void initialise();

async function initialise(): Promise<void> {
  const [settings, tab] = await Promise.all([
    popupApi.storage.local.get({ paused: false, customSites: [] as CustomSite[] }),
    popupApi.tabs.query({ active: true, lastFocusedWindow: true }),
  ]);

  pauseButton.textContent = settings.paused ? "Resume tracking" : "Pause tracking";
  pauseButton.addEventListener("click", async () => {
    const paused = !((await popupApi.storage.local.get({ paused: false })).paused);
    await popupApi.storage.local.set({ paused });
    pauseButton.textContent = paused ? "Resume tracking" : "Pause tracking";
  });

  currentSite = toSite(tab[0]?.url);
  currentSiteText.textContent = currentSite?.origin ?? "This page cannot be tracked";
  await refreshTrackAction(settings.customSites);
  renderCustomSites(settings.customSites);
  trackButton.addEventListener("click", () => void trackCurrentSite());
  await testConnection();
}

function toSite(url: string | undefined): { origin: string; pattern: string } | undefined {
  try {
    const parsed = new URL(url ?? "");
    if (parsed.protocol !== "https:" && parsed.protocol !== "http:") return undefined;
    return { origin: parsed.origin, pattern: `${parsed.origin}/*` };
  } catch {
    return undefined;
  }
}

async function refreshTrackAction(sites: CustomSite[]): Promise<void> {
  if (!currentSite) {
    trackButton.disabled = true;
    return;
  }

  const granted = await popupApi.permissions.contains({ origins: [currentSite.pattern] });
  const added = sites.some((site) => site.origin === currentSite?.origin);
  trackButton.textContent = granted || added ? "Tracking this site" : "Track this site";
  trackButton.disabled = granted || added;
}

async function trackCurrentSite(): Promise<void> {
  if (!currentSite) return;
  const granted = await popupApi.permissions.request({ origins: [currentSite.pattern] });
  if (!granted) {
    statusText.textContent = "Site access was not granted.";
    return;
  }

  const sites = (await popupApi.storage.local.get({ customSites: [] as CustomSite[] })).customSites;
  const site: CustomSite = { id: `site_${crypto.randomUUID().replaceAll("-", "")}`, ...currentSite };
  await popupApi.scripting.registerContentScripts([{ id: site.id, matches: [site.pattern], js: ["dist/media-observer.js"], runAt: "document_idle", persistAcrossSessions: true }]);
  const nextSites = [...sites, site];
  await popupApi.storage.local.set({ customSites: nextSites });
  await refreshTrackAction(nextSites);
  renderCustomSites(nextSites);
  statusText.textContent = "This site is ready. Reload its tab once.";
}

function renderCustomSites(sites: CustomSite[]): void {
  customSites.replaceChildren(...sites.map((site) => {
    const item = document.createElement("li");
    const label = document.createElement("span");
    label.textContent = site.origin;
    const remove = document.createElement("button");
    remove.type = "button";
    remove.textContent = "Remove";
    remove.addEventListener("click", () => void removeSite(site));
    item.append(label, remove);
    return item;
  }));
}

async function removeSite(site: CustomSite): Promise<void> {
  await popupApi.scripting.unregisterContentScripts({ ids: [site.id] });
  await popupApi.permissions.remove({ origins: [site.pattern] });
  const sites = (await popupApi.storage.local.get({ customSites: [] as CustomSite[] })).customSites.filter((entry: CustomSite) => entry.id !== site.id);
  await popupApi.storage.local.set({ customSites: sites });
  renderCustomSites(sites);
  await refreshTrackAction(sites);
}

async function testConnection(): Promise<void> {
  if (connectionTestRunning) return;
  connectionTestRunning = true;
  connectionButton.disabled = true;
  connectionButton.textContent = "Testing connection…";
  statusText.textContent = "Checking native messaging host…";
  statusText.dataset.state = "checking";
  connectionRecovery.textContent = "";
  connectionCode.textContent = "";

  try {
    const response = await popupApi.runtime.sendMessage({ type: "companion.test-native-host" }) as NativeHostStatus | undefined;
    if (!response || typeof response !== "object" || typeof response.detail !== "string"
        || typeof response.code !== "string" || typeof response.diagnosticId !== "string") {
      renderFailure({
        state: "unavailable",
        code: "UNKNOWN_CONNECTION_ERROR",
        detail: "The extension service worker returned an invalid diagnostic response.",
        recovery: "Reload the Scrobblix extension and retry Test connection.",
        restartRequired: false,
        diagnosticId: `NM-UI-${Date.now().toString(36).toUpperCase()}`,
      });
      return;
    }

    statusText.textContent = response.detail;
    statusText.dataset.state = response.state === "connected" && response.code === "CONNECTION_SUCCESS" ? "connected" : "unavailable";
    connectionRecovery.textContent = response.recovery;
    connectionCode.textContent = `${response.code} · ${response.diagnosticId}${response.restartRequired ? " · Restart required after repair" : ""}`;
  } catch {
    renderFailure({
      state: "unavailable",
      code: "UNKNOWN_CONNECTION_ERROR",
      detail: "The extension could not complete its connection diagnostic.",
      recovery: "Reload the extension. If the problem continues, open the extension's service-worker console and report the diagnostic time.",
      restartRequired: false,
      diagnosticId: `NM-UI-${Date.now().toString(36).toUpperCase()}`,
    });
  } finally {
    connectionTestRunning = false;
    connectionButton.disabled = false;
    connectionButton.textContent = "Test connection";
  }
}

function renderFailure(status: NativeHostStatus): void {
  statusText.textContent = status.detail;
  statusText.dataset.state = "unavailable";
  connectionRecovery.textContent = status.recovery;
  connectionCode.textContent = `${status.code} · ${status.diagnosticId}${status.restartRequired ? " · Restart required after repair" : ""}`;
}
