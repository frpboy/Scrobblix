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
    if (url.protocol !== "https:" || url.username || url.password) return undefined;
    const host = url.hostname.toLowerCase().replace(/^\[|\]$/gu, "").replace(/\.$/u, "");
    if (host === "localhost" || host.endsWith(".localhost") || host.endsWith(".local") || host === "metadata.google.internal") {
      return undefined;
    }
    // URL canonicalizes IPv6 and IPv4-mapped literals before this syntactic filter.
    // DNS resolution/rebinding and redirect targets still require the native connector.
    const mapped = /^::ffff:([0-9a-f]{1,4}):([0-9a-f]{1,4})$/u.exec(host);
    const ipv4Host = mapped
      ? [parseInt(mapped[1], 16) >> 8, parseInt(mapped[1], 16) & 255, parseInt(mapped[2], 16) >> 8, parseInt(mapped[2], 16) & 255].join(".")
      : host;
    const ipv4Parts = ipv4Host.split(".");
    if (ipv4Parts.length === 4 && ipv4Parts.every((p) => /^\d+$/u.test(p) && Number(p) >= 0 && Number(p) <= 255)) {
      const [b0, b1] = ipv4Parts.map(Number);
      if (b0 === 0 || b0 === 10 || b0 === 127 || (b0 === 100 && b1 >= 64 && b1 <= 127) || (b0 === 198 && (b1 === 18 || b1 === 19)) || (b0 === 169 && b1 === 254) || (b0 === 172 && b1 >= 16 && b1 <= 31) || (b0 === 192 && b1 === 168)) {
        return undefined;
      }
    }
    const firstV6 = parseInt(host.split(":")[0] || "0", 16);
    if (host.includes(":") && (host === "::" || host === "::1" || (firstV6 & 0xfe00) === 0xfc00 || (firstV6 & 0xffc0) === 0xfe80 || (firstV6 & 0xff00) === 0xff00)) return undefined;
    return url.href;
  } catch {
    return undefined;
  }
}

function findArtworkUrl(media: HTMLMediaElement, metadata: ExtendedMediaMetadata | undefined): string | undefined {
  // Optimization: Short-circuit on the first valid HTTPS artwork to avoid allocating intermediate
  // arrays, parsing unnecessary URLs, and executing redundant DOM queries during frequent playback updates.
  if (metadata?.artwork) {
    for (const image of metadata.artwork) {
      const candidate = toHttpsArtworkUrl(image.src);
      if (candidate) return candidate;
    }
  }

  // Many web players don't populate MediaSession artwork. Prefer the active video poster,
  // then same-page Open Graph/Twitter imagery. Only HTTPS URLs are sent to the desktop host.
  // Evaluate fallbacks lazily to skip querying the DOM when an earlier candidate matches.
  const poster = media instanceof HTMLVideoElement ? media.poster : undefined;
  return toHttpsArtworkUrl(poster)
    ?? toHttpsArtworkUrl(document.querySelector<HTMLMetaElement>('meta[property="og:image"]')?.content)
    ?? toHttpsArtworkUrl(document.querySelector<HTMLMetaElement>('meta[name="twitter:image"]')?.content);
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
