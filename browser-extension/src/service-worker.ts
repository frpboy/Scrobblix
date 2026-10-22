const scrobblixWorkerApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);

type PlaybackObservation = {
  artist?: string; title?: string; album?: string; albumArtist?: string; artworkUrl?: string;
  trackNumber?: number; releaseYear?: number; recordingMusicBrainzId?: string; releaseMusicBrainzId?: string;
  durationMilliseconds?: number; positionMilliseconds?: number; state: "playing" | "paused" | "ended" | "unknown";
};

const hostName = "com.frpboy.scrobblix";
let sequence = 0;

scrobblixWorkerApi.runtime.onMessage.addListener((observation: PlaybackObservation, sender) => {
  if (!sender.tab?.id || (observation.title?.length ?? 0) > 1024 || (observation.artist?.length ?? 0) > 1024 || (observation.artworkUrl?.length ?? 0) > 4096) return;
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
});

function getOrigin(url: string | undefined): string | undefined {
  try { return url ? new URL(url).origin : undefined; } catch { return undefined; }
}
