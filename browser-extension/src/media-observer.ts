const scrobblixMediaApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);

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

function toHttpsArtworkUrl(candidate: string | undefined): string | undefined {
  if (!candidate) return undefined;

  try {
    const url = new URL(candidate, document.baseURI);
    return url.protocol === "https:" ? url.href : undefined;
  } catch {
    return undefined;
  }
}

function findArtworkUrl(media: HTMLMediaElement, metadata: ExtendedMediaMetadata | undefined): string | undefined {
  const sessionArtwork = metadata?.artwork
    ?.map((image) => toHttpsArtworkUrl(image.src))
    .find((src): src is string => Boolean(src));
  if (sessionArtwork) return sessionArtwork;

  // Many web players don't populate MediaSession artwork. Prefer the active video poster,
  // then same-page Open Graph/Twitter imagery. Only HTTPS URLs are sent to the desktop host.
  const poster = media instanceof HTMLVideoElement ? media.poster : undefined;
  const pageArtwork = [
    poster,
    document.querySelector<HTMLMetaElement>('meta[property="og:image"]')?.content,
    document.querySelector<HTMLMetaElement>('meta[name="twitter:image"]')?.content,
  ]
    .map((candidate) => toHttpsArtworkUrl(candidate))
    .find((src): src is string => Boolean(src));

  return pageArtwork;
}

function send(media: HTMLMediaElement, state: PlaybackState): void {
  const metadata = navigator.mediaSession?.metadata as ExtendedMediaMetadata | undefined;
  const artworkUrl = findArtworkUrl(media, metadata);
  scrobblixMediaApi.runtime.sendMessage({
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
