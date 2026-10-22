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

function isHttpsArtwork(value: unknown): boolean {
  if (value === undefined) return true;
  if (typeof value !== "string" || value.length > 4096) return false;

  try {
    const url = new URL(value);
    return url.protocol === "https:" && !url.username && !url.password;
  } catch {
    return false;
  }
}

export function isSupportedSender(sender: chrome.runtime.MessageSender): boolean {
  if (!sender.tab || !Number.isSafeInteger(sender.tab.id) || !sender.url) return false;

  try {
    const url = new URL(sender.url);
    return url.protocol === "https:" && supportedOrigins.has(url.origin);
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
