import { CustomSiteAuthorization } from "./custom-site-authorization.js";
import { cleanupCustomSiteBrowserAccess } from "./custom-site-cleanup.js";
import { isAuthorizedObservationSender, isPlaybackObservation, type CustomSiteRegistration, type PlaybackObservation } from "./message-validation.js";

const scrobblixWorkerApi = ((globalThis as unknown as { browser?: typeof chrome }).browser ?? chrome);

export {};

type PopupMessage = { type: "companion.get-state" | "companion.test-native-host" }
  | { type: "companion.add-custom-site"; site: CustomSiteRegistration }
  | { type: "companion.remove-custom-site"; site: CustomSiteRegistration };
type CustomSiteMutationResult = { added: boolean; removed?: never; cleanupSucceeded?: boolean; detail?: string }
  | { removed: boolean; added?: never; cleanupSucceeded?: boolean; detail?: string };
type CompanionSettings = { paused: boolean; customSites: CustomSiteRegistration[] };
type NativeHostDiagnosticCode =
  | "HOST_NOT_REGISTERED"
  | "MANIFEST_MISSING_OR_INVALID"
  | "HOST_EXECUTABLE_MISSING"
  | "EXTENSION_NOT_AUTHORIZED"
  | "HOST_STARTUP_FAILED"
  | "PROTOCOL_ERROR"
  | "CONNECTION_TIMEOUT"
  | "CONNECTION_SUCCESS"
  | "UNKNOWN_CONNECTION_ERROR";

type NativeHostStatus = {
  state: "connected" | "unavailable";
  code: NativeHostDiagnosticCode;
  detail: string;
  recovery: string;
  restartRequired: boolean;
  diagnosticId: string;
};

const hostName = "com.frpboy.scrobblix";
const connectionTimeoutMs = 3_000;
let sequence = 0;
const customSiteAuthorization = new CustomSiteAuthorization();
let customSiteMutationQueue: Promise<void> = Promise.resolve();

scrobblixWorkerApi.runtime.onMessage.addListener((message: unknown, sender, sendResponse) => {
  if (isPopupMessage(message) && sender.id === scrobblixWorkerApi.runtime.id && !sender.tab) {
    void handlePopupMessage(message).then(sendResponse).catch(() => {
      if (message.type === "companion.add-custom-site") {
        sendResponse({
          added: false,
          cleanupSucceeded: false,
          detail: "The custom site could not be registered because its stored state or browser permission could not be verified. Retry the operation.",
        });
        return;
      }
      if (message.type === "companion.remove-custom-site") {
        sendResponse({
          removed: false,
          cleanupSucceeded: false,
          detail: "The custom site could not be removed because its stored state could not be read. Removal is not confirmed; retry and review browser permissions.",
        });
        return;
      }
      sendResponse({
        state: "unavailable",
        code: "UNKNOWN_CONNECTION_ERROR",
        detail: "The connection diagnostic could not complete.",
        recovery: "Reload the extension and run Test connection again.",
        restartRequired: false,
        diagnosticId: createDiagnosticId(),
      } satisfies NativeHostStatus);
    });
    return true;
  }

  if (isPlaybackObservation(message)) void forwardObservation(message, sender);
});

async function forwardObservation(observation: PlaybackObservation, sender: chrome.runtime.MessageSender): Promise<void> {
  if (!sender.tab?.id || !sender.url) return;

  let sourceUrl: URL;
  try {
    sourceUrl = new URL(sender.url);
  } catch {
    return;
  }

  if (sourceUrl.protocol !== "https:" && sourceUrl.protocol !== "http:") return;
  const settings = await readSettings();
  const builtInOrigin = isSupportedPlaybackOrigin(sourceUrl.origin);
  const customSite = settings.customSites.find((site) => site.origin === sourceUrl.origin);
  if (settings.paused || (!builtInOrigin && !customSite)
      || !isAuthorizedObservationSender(sender, settings.customSites)) return;

  const authorizeCustomSite = async (): Promise<boolean> => {
    if (!customSite || await isCustomSiteSessionRevoked(customSite.id)) return false;
    return customSiteAuthorization.authorize(
      customSite.id,
      () => scrobblixWorkerApi.permissions.contains({ origins: [`${sourceUrl.origin}/*`] }),
      async () => {
        const latestSettings = await readSettings();
        return !latestSettings.paused && isAuthorizedObservationSender(sender, latestSettings.customSites);
      },
    );
  };
  const hasGrantedOrigin = builtInOrigin || await authorizeCustomSite();
  if (!hasGrantedOrigin) return;

  let port: chrome.runtime.Port;
  try {
    port = scrobblixWorkerApi.runtime.connectNative(hostName);
  } catch {
    // Playback delivery is best-effort. The popup's explicit diagnostic is the
    // source of actionable error detail, not this background observation path.
    return;
  }

  let finished = false;
  const finish = () => {
    if (!finished) {
      finished = true;
      port.disconnect();
    }
  };
  const messageId = crypto.randomUUID();
  const timeout = setTimeout(finish, connectionTimeoutMs);
  port.onMessage.addListener((response: unknown) => {
    if (isMatchingResponse(response, messageId, "accepted.v1")) finish();
    else finish();
  });
  port.onDisconnect.addListener(() => {
    finished = true;
    clearTimeout(timeout);
  });
  // Revalidate after opening the native port as well. The final permission and
  // registration checks are asynchronous, so the gate checks revocation again
  // after each await before any observation is posted.
  if (customSite) {
    let stillAuthorized = false;
    try {
      stillAuthorized = await authorizeCustomSite();
    } catch {
      finish();
      return;
    }
    if (!stillAuthorized || finished) {
      finish();
      return;
    }
  }
  port.postMessage({
    protocolVersion: 1,
    messageId,
    type: "playback.observation.v1",
    sentAtUtc: new Date().toISOString(),
    browserInstanceId: scrobblixWorkerApi.runtime.id,
    sequence: ++sequence,
    payload: {
      ...observation,
      tabId: sender.tab.id,
      siteOrigin: sourceUrl.origin,
    },
  });
}


async function readSettings(): Promise<CompanionSettings> {
  const stored = await scrobblixWorkerApi.storage.local.get({
    paused: false,
    customSites: [] as CustomSiteRegistration[],
  });
  const value = stored as unknown as Record<string, unknown>;
  const rawSites = value.customSites;
  return {
    paused: value.paused === true,
    customSites: Array.isArray(rawSites) ? rawSites.filter(isCustomSiteRegistration) : [],
  };
}

function isCustomSiteRegistration(value: unknown): value is CustomSiteRegistration {
  if (typeof value !== "object" || value === null || Array.isArray(value)) return false;
  const site = value as Record<string, unknown>;
  if (typeof site.id !== "string" || typeof site.origin !== "string"
      || typeof site.pattern !== "string" || !/^site_[a-f0-9]{32}$/u.test(site.id)) {
    return false;
  }

  try {
    const origin = new URL(site.origin);
    return (origin.protocol === "http:" || origin.protocol === "https:")
      && origin.origin === site.origin
      && site.pattern === `${site.origin}/*`;
  } catch {
    return false;
  }
}

function isSupportedPlaybackOrigin(origin: string): boolean {
  return origin === "https://music.youtube.com" || origin === "https://open.spotify.com";
}

function isPopupMessage(message: unknown): message is PopupMessage {
  if (typeof message !== "object" || message === null || !("type" in message)) return false;
  const candidate = message as { type?: unknown; siteId?: unknown; site?: unknown };
  if (candidate.type === "companion.get-state" || candidate.type === "companion.test-native-host") return true;
  if (candidate.type === "companion.add-custom-site") return isCustomSiteRegistration(candidate.site);
  return candidate.type === "companion.remove-custom-site"
    && isCustomSiteRegistration(candidate.site);
}

function serializeCustomSiteMutation<T>(mutation: () => Promise<T>): Promise<T> {
  const result = customSiteMutationQueue.then(mutation, mutation);
  customSiteMutationQueue = result.then(() => undefined, () => undefined);
  return result;
}

async function getSessionRevokedSiteIds(): Promise<string[]> {
  const stored = await scrobblixWorkerApi.storage.session.get({ revokedCustomSiteIds: [] as string[] });
  const value = (stored as unknown as Record<string, unknown>).revokedCustomSiteIds;
  return Array.isArray(value)
    ? value.filter((siteId): siteId is string => typeof siteId === "string" && /^site_[a-f0-9]{32}$/u.test(siteId))
    : [];
}

async function isCustomSiteSessionRevoked(siteId: string): Promise<boolean> {
  try {
    return (await getSessionRevokedSiteIds()).includes(siteId);
  } catch {
    // If the session revocation store cannot be read, custom-site forwarding fails closed.
    return true;
  }
}

async function persistCustomSiteSessionRevocation(siteId: string): Promise<boolean> {
  try {
    const siteIds = await getSessionRevokedSiteIds();
    if (!siteIds.includes(siteId)) {
      await scrobblixWorkerApi.storage.session.set({ revokedCustomSiteIds: [...siteIds, siteId] });
    }
    return true;
  } catch {
    return false;
  }
}

async function clearCustomSiteSessionRevocation(siteId: string): Promise<void> {
  try {
    const siteIds = await getSessionRevokedSiteIds();
    if (siteIds.includes(siteId)) {
      await scrobblixWorkerApi.storage.session.set({
        revokedCustomSiteIds: siteIds.filter((entry) => entry !== siteId),
      });
    }
  } catch {
    // A stale session tombstone is safe: it only keeps the removed ID blocked.
  }
}

function cleanupCustomSite(site: CustomSiteRegistration): Promise<boolean> {
  return cleanupCustomSiteBrowserAccess(site.id, site.pattern, {
    unregisterContentScripts: (details) => scrobblixWorkerApi.scripting.unregisterContentScripts(details),
    containsContentScript: async (details) => (await scrobblixWorkerApi.scripting.getRegisteredContentScripts(details)).some((script) => script.id === site.id),
    removePermissions: (details) => scrobblixWorkerApi.permissions.remove(details),
    containsPermissions: (details) => scrobblixWorkerApi.permissions.contains(details),
  });
}

async function handlePopupMessage(message: PopupMessage): Promise<CompanionSettings | NativeHostStatus | CustomSiteMutationResult> {
  if (message.type === "companion.add-custom-site") {
    return serializeCustomSiteMutation(async () => {
      let settings: CompanionSettings;
      try {
        settings = await readSettings();
      } catch {
        const cleanupSucceeded = await cleanupCustomSite(message.site);
        return {
          added: false,
          cleanupSucceeded,
          detail: cleanupSucceeded
            ? "The custom site could not be registered because stored state could not be read. Browser access was cleaned up."
            : "The custom site could not be registered because stored state could not be read and browser cleanup is incomplete. Review extension permissions.",
        };
      }
      if (settings.customSites.some((site) => site.id === message.site.id || site.origin === message.site.origin)) {
        return { added: false, detail: "This site is already registered." };
      }
      if (await isCustomSiteSessionRevoked(message.site.id)) {
        return { added: false, detail: "This registration ID has been revoked. Refresh the popup and try again." };
      }

      const permissionGranted = await scrobblixWorkerApi.permissions.contains({ origins: [message.site.pattern] });
      if (!permissionGranted) return { added: false, detail: "The browser permission is no longer granted. Grant access and try again." };

      try {
        await scrobblixWorkerApi.scripting.registerContentScripts([{
          id: message.site.id,
          matches: [message.site.pattern],
          js: ["dist/media-observer.js"],
          runAt: "document_idle",
          persistAcrossSessions: true,
        }]);
        await scrobblixWorkerApi.storage.local.set({ customSites: [...settings.customSites, message.site] });
      } catch {
        const cleanupSucceeded = await cleanupCustomSite(message.site);
        return {
          added: false,
          cleanupSucceeded,
          detail: cleanupSucceeded
            ? "The site could not be registered. Browser access was cleaned up."
            : "The site could not be registered and browser cleanup is incomplete. Review this site's extension permissions.",
        };
      }

      return { added: true };
    });
  }

  if (message.type === "companion.remove-custom-site") {
    const site = message.site;
    // Revoke in memory and in session storage before waiting behind another mutation.
    customSiteAuthorization.revoke(site.id);
    const sessionRevocationPromise = persistCustomSiteSessionRevocation(site.id);
    return serializeCustomSiteMutation(async () => {
      const sessionRevocationPersisted = await sessionRevocationPromise;
      let settings: CompanionSettings;
      try {
        settings = await readSettings();
      } catch {
        const cleanupSucceeded = await cleanupCustomSite(site);
        return {
          removed: false,
          cleanupSucceeded,
          detail: sessionRevocationPersisted
            ? "Stored registrations could not be read. This site is blocked for the current browser session; retry removal."
            : cleanupSucceeded
              ? "Stored registrations could not be read, but browser access was revoked. Retry removal."
              : "Stored registrations could not be read and session/browser revocation could not be confirmed. Manually revoke this site's browser permission and retry.",
        };
      }

      const storedSite = settings.customSites.find((entry) => entry.id === site.id);
      if (!storedSite) {
        const cleanupSucceeded = await cleanupCustomSite(site);
        return {
          removed: false,
          cleanupSucceeded,
          detail: cleanupSucceeded
            ? "This site is already absent from Scrobblix; browser access was cleaned up."
            : "This site is absent from Scrobblix, but browser cleanup is incomplete. Review extension permissions.",
        };
      }
      if (storedSite.origin !== site.origin || storedSite.pattern !== site.pattern) {
        return { removed: false, cleanupSucceeded: false, detail: "The stored site identity changed. Refresh the popup and retry removal." };
      }

      try {
        await scrobblixWorkerApi.storage.local.set({
          customSites: settings.customSites.filter((entry) => entry.id !== site.id),
        });
      } catch {
        const cleanupSucceeded = await cleanupCustomSite(site);
        return {
          removed: false,
          cleanupSucceeded,
          detail: sessionRevocationPersisted
            ? "The registration could not be removed from storage, but this site remains blocked for the current browser session. Retry removal."
            : cleanupSucceeded
              ? "The registration could not be removed from storage, but browser access was revoked. Retry removal."
              : "The registration could not be removed from storage and session/browser revocation could not be confirmed. Manually revoke this site's browser permission and retry.",
        };
      }

      await clearCustomSiteSessionRevocation(site.id);
      const cleanupSucceeded = await cleanupCustomSite(site);
      return cleanupSucceeded
        ? { removed: true, cleanupSucceeded: true }
        : {
          removed: true,
          cleanupSucceeded: false,
          detail: "The Scrobblix registration was removed, but browser cleanup is incomplete.",
        };
    });
  }

  if (message.type === "companion.get-state") return readSettings();
  return testNativeHost();
}

function testNativeHost(): Promise<NativeHostStatus> {
  return new Promise((resolve) => {
    let settled = false;
    let timeout: ReturnType<typeof setTimeout> | undefined;
    let port: chrome.runtime.Port | undefined;
    const messageId = crypto.randomUUID();
    const diagnosticId = createDiagnosticId();

    const finish = (status: NativeHostStatus) => {
      if (settled) return;
      settled = true;
      if (timeout) clearTimeout(timeout);
      try { port?.disconnect(); } catch { /* already disconnected */ }
      resolve({ ...status, diagnosticId });
    };

    try {
      port = scrobblixWorkerApi.runtime.connectNative(hostName);
    } catch {
      finish(diagnostic(
        "HOST_NOT_REGISTERED",
        "The browser cannot find the Scrobblix native messaging host registration.",
        "Run the Scrobblix Browser Companion setup script for this browser and the extension's actual ID, then fully restart the browser.",
        true,
      ));
      return;
    }

    timeout = setTimeout(() => finish(diagnostic(
      "CONNECTION_TIMEOUT",
      "The native host started but did not return a valid response before the diagnostic timed out.",
      "Close stale Scrobblix/browser processes, confirm the installed host files exist, then restart the browser and retry.",
      true,
    )), connectionTimeoutMs);

    port.onMessage.addListener((response: unknown) => {
      if (isMatchingResponse(response, messageId, "pong.v1")) {
        finish({
          state: "connected",
          code: "CONNECTION_SUCCESS",
          detail: "Native host connection verified.",
          recovery: "No action required.",
          restartRequired: false,
          diagnosticId,
        });
        return;
      }

      if (isErrorResponse(response) && response.messageId === messageId) {
        const code = response.code;
        if (code === "extension.untrusted_sender") {
          finish(diagnostic(
            "EXTENSION_NOT_AUTHORIZED",
            "The native host is reachable, but this extension ID is not authorized.",
            "Register the exact extension ID shown in this browser. Do not guess or reuse another browser's ID.",
            true,
          ));
        } else {
          finish(diagnostic(
            "PROTOCOL_ERROR",
            "The native host replied, but the response did not match the expected connection protocol.",
            "Update the desktop native host and browser companion from the same Scrobblix build, then retry.",
            true,
          ));
        }
        return;
      }

      finish(diagnostic(
        "PROTOCOL_ERROR",
        "The native host returned an unexpected or malformed response.",
        "Update the desktop native host and browser companion from the same Scrobblix build, then retry.",
        true,
      ));
    });

    port.onDisconnect.addListener(() => {
      const firefoxPortError = (port as (chrome.runtime.Port & { error?: { message?: string } }) | undefined)?.error?.message;
      const browserError = scrobblixWorkerApi.runtime.lastError?.message ?? firefoxPortError;
      if (settled) return;
      if (!browserError) {
        finish(diagnostic(
          "HOST_STARTUP_FAILED",
          "The browser found the host registration, but the native host exited before completing the handshake.",
          "Verify the installed native-host executable and its dependencies, then run the host setup/repair script again.",
          true,
        ));
      } else if (/specified native messaging host not found|native messaging host.*not found|no such native application/i.test(browserError)) {
        finish(diagnostic(
          "HOST_NOT_REGISTERED",
          "The browser cannot resolve the registered Scrobblix native messaging host.",
          "Run setup/repair for this browser, verify its Scrobblix registration and manifest path, and fully restart the browser.",
          true,
        ));
      } else if (/failed to start native messaging host|native messaging host.*failed to start/i.test(browserError)) {
        finish(diagnostic(
          "HOST_STARTUP_FAILED",
          "The browser found the registration but could not start the Scrobblix native host.",
          "Verify the registered manifest path and executable exist, then reinstall or repair the native host before retrying.",
          true,
        ));
      } else if (/forbidden|access.*denied|not allowed|permission|unauthorized/i.test(browserError)) {
        finish(diagnostic(
          "EXTENSION_NOT_AUTHORIZED",
          "The browser refused native messaging access for this extension or browser policy.",
          "Register this browser's actual extension ID and check the browser's enterprise native-messaging policy.",
          true,
        ));
      } else {
        finish(diagnostic(
          "UNKNOWN_CONNECTION_ERROR",
          "The browser could not complete the native-host handshake.",
          "Run setup/repair for this browser, restart it, and retry. Include the diagnostic ID when reporting the problem.",
          true,
        ));
      }
    });

    try {
      port.postMessage({
        protocolVersion: 1,
        messageId,
        type: "ping.v1",
        browserInstanceId: scrobblixWorkerApi.runtime.id,
      });
    } catch {
      finish(diagnostic(
        "HOST_STARTUP_FAILED",
        "The native host connection closed before the test message could be sent.",
        "Reinstall or repair the native host, then restart the browser.",
        true,
      ));
    }
  });
}

function diagnostic(
  code: Exclude<NativeHostDiagnosticCode, "CONNECTION_SUCCESS">,
  detail: string,
  recovery: string,
  restartRequired: boolean,
): NativeHostStatus {
  return { state: "unavailable", code, detail, recovery, restartRequired, diagnosticId: createDiagnosticId() };
}

function createDiagnosticId(): string {
  return `NM-${Date.now().toString(36).toUpperCase()}-${crypto.randomUUID().slice(0, 8).toUpperCase()}`;
}

function isMatchingResponse(response: unknown, messageId: string, expectedType: string): boolean {
  if (typeof response !== "object" || response === null) return false;
  const candidate = response as { type?: unknown; messageId?: unknown; protocolVersion?: unknown };
  return candidate.type === expectedType
    && candidate.messageId === messageId
    && candidate.protocolVersion === 1;
}

function isErrorResponse(response: unknown): response is { type: "error.v1"; messageId?: string; code?: string } {
  if (typeof response !== "object" || response === null) return false;
  const candidate = response as { type?: unknown; code?: unknown };
  return candidate.type === "error.v1" && typeof candidate.code === "string";
}
