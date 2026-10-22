import { isAuthorizedObservationSender, isPlaybackObservation, type CustomSiteRegistration, type PlaybackObservation } from "./message-validation.js";

const scrobblixWorkerApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);

export {};

type PopupMessage = { type: "companion.get-state" | "companion.test-native-host" };
type CompanionSettings = { paused: boolean; customSites: CustomSiteRegistration[] };
type NativeHostStatus = { state: "connected" | "unavailable"; detail: string };

const hostName = "com.frpboy.scrobblix";
let sequence = 0;

scrobblixWorkerApi.runtime.onMessage.addListener((message: unknown, sender, sendResponse) => {
  if (isPopupMessage(message)) {
    void handlePopupMessage(message).then(sendResponse);
    return true;
  }

  if (isPlaybackObservation(message)) void forwardObservation(message, sender);
});

async function forwardObservation(observation: PlaybackObservation, sender: chrome.runtime.MessageSender): Promise<void> {
  if (!sender.tab?.id || !sender.url) return;

  let sourceUrl: URL;
  try {
    sourceUrl = new URL(sender.url);
  } catch {
    return;
  }

  if (sourceUrl.protocol !== "https:" && sourceUrl.protocol !== "http:") return;
  const settings = await scrobblixWorkerApi.storage.local.get({
    paused: false,
    customSites: [],
  } satisfies CompanionSettings);
  if (settings.paused || !isAuthorizedObservationSender(sender, settings.customSites)) return;

  const hasGrantedOrigin = isSupportedPlaybackOrigin(sourceUrl.origin)
    || await scrobblixWorkerApi.permissions.contains({ origins: [`${sourceUrl.origin}/*`] });
  if (!hasGrantedOrigin) return;

  const port = scrobblixWorkerApi.runtime.connectNative(hostName);
  let finished = false;
  const finish = () => {
    if (!finished) {
      finished = true;
      port.disconnect();
    }
  };
  port.onMessage.addListener((response: unknown) => {
    if (typeof response === "object" && response !== null
        && "messageId" in response
        && (response as { messageId?: unknown }).messageId === messageId) {
      finish();
    } else {
      finish();
    }
  });
  port.onDisconnect.addListener(() => { finished = true; });
  const messageId = crypto.randomUUID();
  port.postMessage({
    protocolVersion: 1,
    messageId,
    type: "playback.observation.v1",
    sentAtUtc: new Date().toISOString(),
    browserInstanceId: scrobblixWorkerApi.runtime.id,
    sequence: ++sequence,
    payload: {
      ...observation,
      tabId: sender.tab.id,
      siteOrigin: sourceUrl.origin,
    },
  });
}

function isSupportedPlaybackOrigin(origin: string): boolean {
  return origin === "https://music.youtube.com" || origin === "https://open.spotify.com";
}

function isPopupMessage(message: unknown): message is PopupMessage {
  return typeof message === "object" && message !== null && "type" in message
    && ((message as { type?: unknown }).type === "companion.get-state"
      || (message as { type?: unknown }).type === "companion.test-native-host");
}

async function handlePopupMessage(message: PopupMessage): Promise<CompanionSettings | NativeHostStatus> {
  if (message.type === "companion.get-state") {
    return scrobblixWorkerApi.storage.local.get({ paused: false, customSites: [] } satisfies CompanionSettings);
  }

  return testNativeHost();
}

function testNativeHost(): Promise<NativeHostStatus> {
  return new Promise((resolve) => {
    let settled = false;
    let timeout: ReturnType<typeof setTimeout> | undefined;
    let port: chrome.runtime.Port | undefined;
    const finish = (status: NativeHostStatus) => {
      if (!settled) {
        settled = true;
        if (timeout) clearTimeout(timeout);
        port?.disconnect();
        resolve(status);
      }
    };

    try {
      port = scrobblixWorkerApi.runtime.connectNative(hostName);
    } catch {
      finish({ state: "unavailable", detail: "Native host is not registered." });
      return;
    }

    timeout = setTimeout(() => finish({ state: "unavailable", detail: "Native host did not respond." }), 2_000);
    port.onMessage.addListener((response: { type?: string }) => {
      if (response.type === "pong.v1") finish({ state: "connected", detail: "Native host is ready." });
    });
    port.onDisconnect.addListener(() => finish({ state: "unavailable", detail: scrobblixWorkerApi.runtime.lastError?.message ?? "Native host is unavailable." }));
    port.postMessage({ protocolVersion: 1, messageId: crypto.randomUUID(), type: "ping.v1", browserInstanceId: scrobblixWorkerApi.runtime.id });
  });
}
