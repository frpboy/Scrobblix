type PlaybackObservation = {
  artist?: string; title?: string; album?: string; durationMilliseconds?: number;
  positionMilliseconds?: number; state: "playing" | "paused" | "unknown";
};

const hostName = "com.frpboy.scrobblix";
let sequence = 0;

chrome.runtime.onMessage.addListener((observation: PlaybackObservation, sender) => {
  if (!sender.tab?.id || (observation.title?.length ?? 0) > 1024 || (observation.artist?.length ?? 0) > 1024) return;
  const port = chrome.runtime.connectNative(hostName);
  port.postMessage({
    protocolVersion: 1,
    messageId: crypto.randomUUID(),
    type: "playback.observation.v1",
    sentAtUtc: new Date().toISOString(),
    browserInstanceId: chrome.runtime.id,
    sequence: ++sequence,
    payload: { ...observation, tabId: sender.tab.id, siteOrigin: getOrigin(sender.url) },
  });
  port.disconnect();
});

function getOrigin(url: string | undefined): string | undefined {
  try { return url ? new URL(url).origin : undefined; } catch { return undefined; }
}
