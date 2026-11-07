export type PlaybackState = "playing" | "paused" | "ended" | "unknown";

export type PlaybackObservation = {
  artist?: string;
  title?: string;
  album?: string;
  albumArtist?: string;
  artworkUrl?: string;
  trackNumber?: number;
  releaseYear?: number;
  recordingMusicBrainzId?: string;
  releaseMusicBrainzId?: string;
  durationMilliseconds?: number;
  positionMilliseconds?: number;
  state: PlaybackState;
};

const supportedOrigins = new Set([
  "https://music.youtube.com",
  "https://open.spotify.com",
]);

export function isBuiltInSupportedOrigin(origin: string): boolean {
  return supportedOrigins.has(origin);
}

function isOptionalString(value: unknown, maxLength: number): boolean {
  return value === undefined || (
    typeof value === "string" &&
    value.length <= maxLength &&
    !/[\u0000-\u0008\u000B\u000C\u000E-\u001F]/u.test(value)
  );
}

function isOptionalInteger(value: unknown, min: number, max: number): boolean {
  return value === undefined || (
    typeof value === "number" &&
    Number.isSafeInteger(value) &&
    value >= min &&
    value <= max
  );
}

// Defense-in-depth syntax filter for local/private literal targets and known local names.
// DNS rebinding and redirect targets remain the native artwork connector's responsibility.
export function isSafeHttpsArtworkUrl(value: string): boolean {
  if (value.length > 4096) return false;
  try {
    const url = new URL(value);
    if (url.protocol !== "https:" || url.username || url.password) return false;
    const host = url.hostname.toLowerCase().replace(/^\[|\]$/gu, "").replace(/\.$/u, "");
    if (host === "localhost" || host.endsWith(".localhost") || host.endsWith(".local") || host === "metadata.google.internal") {
      return false;
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
      if (b0 === 0 || b0 === 10 || b0 === 127 || (b0 === 169 && b1 === 254) || (b0 === 172 && b1 >= 16 && b1 <= 31) || (b0 === 192 && b1 === 168)) {
        return false;
      }
    }
    const firstV6 = parseInt(host.split(":")[0] || "0", 16);
    if (host.includes(":") && (host === "::" || host === "::1" || (firstV6 & 0xfe00) === 0xfc00 || (firstV6 & 0xffc0) === 0xfe80 || (firstV6 & 0xff00) === 0xff00)) return false;
    return true;
  } catch {
    return false;
  }
}

function isHttpsArtwork(value: unknown): boolean {
  if (value === undefined) return true;
  return typeof value === "string" && isSafeHttpsArtworkUrl(value);
}

export function isSupportedSender(sender: chrome.runtime.MessageSender): boolean {
  if (!sender.tab || !Number.isSafeInteger(sender.tab.id) || !sender.url) return false;

  try {
    const url = new URL(sender.url);
    return url.protocol === "https:" && isBuiltInSupportedOrigin(url.origin);
  } catch {
    return false;
  }
}

export type CustomSiteRegistration = { origin: string; pattern: string; id: string };

export function isAuthorizedObservationSender(
  sender: chrome.runtime.MessageSender,
  customSites: CustomSiteRegistration[],
): boolean {
  if (isSupportedSender(sender)) return true;
  if (!sender.tab || !Number.isSafeInteger(sender.tab.id) || !sender.url) return false;

  try {
    const url = new URL(sender.url);
    if (url.protocol !== "https:" && url.protocol !== "http:") return false;
    // Optional host permission alone is not authorization: this origin must have
    // an explicit user-created registration in extension storage.
    return customSites.some((site) => {
      try {
        const registered = new URL(site.origin);
        return registered.origin === site.origin
          && registered.origin === url.origin
          && site.pattern === `${url.origin}/*`
          && /^site_[a-f0-9]{32}$/u.test(site.id);
      } catch {
        return false;
      }
    });
  } catch {
    return false;
  }
}

export function isPlaybackObservation(value: unknown): value is PlaybackObservation {
  if (typeof value !== "object" || value === null || Array.isArray(value)) return false;

  const candidate = value as Record<string, unknown>;
  if (candidate.state !== "playing" && candidate.state !== "paused"
      && candidate.state !== "ended" && candidate.state !== "unknown") return false;

  return isOptionalString(candidate.artist, 1024)
    && isOptionalString(candidate.title, 1024)
    && isOptionalString(candidate.album, 1024)
    && isOptionalString(candidate.albumArtist, 1024)
    && isOptionalString(candidate.recordingMusicBrainzId, 128)
    && isOptionalString(candidate.releaseMusicBrainzId, 128)
    && isOptionalInteger(candidate.trackNumber, 1, 10000)
    && isOptionalInteger(candidate.releaseYear, 1000, 9999)
    && isOptionalInteger(candidate.durationMilliseconds, 1, 86_400_000)
    && isOptionalInteger(candidate.positionMilliseconds, 0, 604_800_000)
    && isHttpsArtwork(candidate.artworkUrl);
}
