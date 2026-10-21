type PlaybackState = "playing" | "paused" | "unknown";

function send(media: HTMLMediaElement, state: PlaybackState): void {
  const metadata = navigator.mediaSession?.metadata;
  chrome.runtime.sendMessage({
    artist: metadata?.artist,
    title: metadata?.title,
    album: metadata?.album,
    durationMilliseconds: Number.isFinite(media.duration) ? Math.round(media.duration * 1000) : undefined,
    positionMilliseconds: Number.isFinite(media.currentTime) ? Math.round(media.currentTime * 1000) : undefined,
    state,
  });
}

function attach(media: HTMLMediaElement): void {
  media.addEventListener("playing", () => send(media, "playing"));
  media.addEventListener("pause", () => send(media, "paused"));
  media.addEventListener("ended", () => send(media, "paused"));
  media.addEventListener("durationchange", () => send(media, media.paused ? "paused" : "playing"));
  send(media, media.paused ? "paused" : "playing");
}

for (const media of document.querySelectorAll<HTMLMediaElement>("audio, video")) attach(media);
new MutationObserver((records) => records.flatMap((record) => [...record.addedNodes])
  .filter((node): node is Element => node instanceof Element)
  .forEach((node) => node.querySelectorAll<HTMLMediaElement>("audio, video").forEach(attach)))
  .observe(document.documentElement, { childList: true, subtree: true });
