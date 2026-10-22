import { isPlaybackObservation, isSupportedSender, type PlaybackObservation } from "./message-validation.js";

const scrobblixWorkerApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);
const hostName = "com.frpboy.scrobblix";
let sequence = 0;

scrobblixWorkerApi.runtime.onMessage.addListener((incoming: unknown, sender) => {
  // Content scripts execute beside untrusted page content. Only accept messages
  // from the explicit supported origins and validate data again at the worker boundary.
  if (!isSupportedSender(sender) || !isPlaybackObservation(incoming)) return;

  const observation: PlaybackObservation = incoming;
  const sourceUrl = new URL(sender.url!);
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
      tabId: sender.tab!.id,
      siteOrigin: sourceUrl.origin,
    },
  });
});
