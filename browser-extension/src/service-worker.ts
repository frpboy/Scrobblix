const scrobblixWorkerApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);

export {};

type PlaybackObservation = {
  artist?: string; title?: string; album?: string; albumArtist?: string; artworkUrl?: string;
  trackNumber?: number; releaseYear?: number; recordingMusicBrainzId?: string; releaseMusicBrainzId?: string;
  durationMilliseconds?: number; positionMilliseconds?: number; state: "playing" | "paused" | "ended" | "unknown";
};

type PopupMessage = { type: "companion.get-state" | "companion.test-native-host" };
type CompanionSettings = { paused: boolean };
type NativeHostStatus = { state: "connected" | "unavailable"; detail: string };

const hostName = "com.frpboy.scrobblix";
let sequence = 0;

scrobblixWorkerApi.runtime.onMessage.addListener((message: PlaybackObservation | PopupMessage, sender, sendResponse) => {
  if (isPopupMessage(message)) {
    void handlePopupMessage(message).then(sendResponse);
    return true;
  }

  void forwardObservation(message, sender);
});

async function forwardObservation(observation: PlaybackObservation, sender: chrome.runtime.MessageSender): Promise<void> {
  if (!sender.tab?.id || (observation.title?.length ?? 0) > 1024 || (observation.artist?.length ?? 0) > 1024 || (observation.artworkUrl?.length ?? 0) > 4096) return;
  const settings = await scrobblixWorkerApi.storage.local.get({ paused: false } satisfies CompanionSettings);
  if (settings.paused) return;
  const port = scrobblixWorkerApi.runtime.connectNative(hostName);
  let finished = false;
  const finish = () => {
    if (!finished) {
      finished = true;
      port.disconnect();
    }
  };
  port.onMessage.addListener(finish);
  port.onDisconnect.addListener(() => { finished = true; });
  port.postMessage({
    protocolVersion: 1,
    messageId: crypto.randomUUID(),
    type: "playback.observation.v1",
    sentAtUtc: new Date().toISOString(),
    browserInstanceId: scrobblixWorkerApi.runtime.id,
    sequence: ++sequence,
    payload: { ...observation, tabId: sender.tab.id, siteOrigin: getOrigin(sender.url) },
  });
}

function isPopupMessage(message: PlaybackObservation | PopupMessage): message is PopupMessage {
  return "type" in message && (message.type === "companion.get-state" || message.type === "companion.test-native-host");
}

async function handlePopupMessage(message: PopupMessage): Promise<CompanionSettings | NativeHostStatus> {
  if (message.type === "companion.get-state") {
    return scrobblixWorkerApi.storage.local.get({ paused: false } satisfies CompanionSettings);
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

function getOrigin(url: string | undefined): string | undefined {
  try { return url ? new URL(url).origin : undefined; } catch { return undefined; }
}
