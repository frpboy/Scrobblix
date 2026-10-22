const popupApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);

export {};

type CustomSite = { id: string; origin: string; pattern: string };
type NativeHostStatus = { state: "connected" | "unavailable"; detail: string };

const statusText = document.querySelector<HTMLElement>("#native-status")!;
const currentSiteText = document.querySelector<HTMLElement>("#current-site")!;
const pauseButton = document.querySelector<HTMLButtonElement>("#pause")!;
const trackButton = document.querySelector<HTMLButtonElement>("#track-site")!;
const customSites = document.querySelector<HTMLUListElement>("#custom-sites")!;
const connectionButton = document.querySelector<HTMLButtonElement>("#test-connection")!;

let currentSite: { origin: string; pattern: string } | undefined;

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
  connectionButton.addEventListener("click", () => void testConnection());
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
  try {
    const response = await popupApi.runtime.sendMessage({ type: "companion.test-native-host" }) as NativeHostStatus;
    statusText.textContent = response.detail;
    statusText.dataset.state = response.state;
  } catch {
    statusText.textContent = "Native host is unavailable.";
    statusText.dataset.state = "unavailable";
  }
}
