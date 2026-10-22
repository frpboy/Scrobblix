type PlaybackState = "playing" | "paused" | "ended" | "unknown";

type ExtendedMediaMetadata = MediaMetadata & {
  albumArtist?: string;
  trackNumber?: number;
  releaseYear?: number;
  recordingMusicBrainzId?: string;
  releaseMusicBrainzId?: string;
};

const attachedMedia = new WeakSet<HTMLMediaElement>();
const minimumProgressReportMs = 2_500;

function send(media: HTMLMediaElement, state: PlaybackState): void {
  const metadata = navigator.mediaSession?.metadata as ExtendedMediaMetadata | undefined;
  const artworkUrl = metadata?.artwork
    ?.map((image) => image.src)
    .find((src) => src.startsWith("https://"));
  chrome.runtime.sendMessage({
    artist: metadata?.artist,
    title: metadata?.title,
    album: metadata?.album,
    albumArtist: metadata?.albumArtist,
    trackNumber: metadata?.trackNumber,
    releaseYear: metadata?.releaseYear,
    recordingMusicBrainzId: metadata?.recordingMusicBrainzId,
    releaseMusicBrainzId: metadata?.releaseMusicBrainzId,
    artworkUrl,
    durationMilliseconds: Number.isFinite(media.duration) ? Math.round(media.duration * 1000) : undefined,
    positionMilliseconds: Number.isFinite(media.currentTime) ? Math.round(media.currentTime * 1000) : undefined,
    state,
  });
}

function attach(media: HTMLMediaElement): void {
  if (attachedMedia.has(media)) return;
  attachedMedia.add(media);

  let lastProgressSentAt = 0;
  const emitProgressIfDue = () => {
    if (media.paused || media.ended) return;
    const now = performance.now();
    if (now - lastProgressSentAt < minimumProgressReportMs) return;
    lastProgressSentAt = now;
    send(media, "playing");
  };

  media.addEventListener("playing", () => {
    lastProgressSentAt = performance.now();
    send(media, "playing");
  });
  media.addEventListener("play", () => send(media, media.paused ? "paused" : "playing"));
  media.addEventListener("pause", () => send(media, "paused"));
  media.addEventListener("ended", () => send(media, "ended"));
  media.addEventListener("durationchange", () => send(media, media.paused ? "paused" : "playing"));
  media.addEventListener("timeupdate", emitProgressIfDue);
  send(media, media.paused ? "paused" : "playing");
}

function scan(node: ParentNode): void {
  if (node instanceof HTMLMediaElement) attach(node);
  node.querySelectorAll<HTMLMediaElement>("audio, video").forEach(attach);
}

scan(document);
new MutationObserver((records) => records.forEach((record) => {
  record.addedNodes.forEach((node) => {
    if (node instanceof Element || node instanceof DocumentFragment) scan(node);
  });
})).observe(document.documentElement, { childList: true, subtree: true });
