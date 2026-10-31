import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { CustomSiteAuthorization } from "../browser-extension/dist/custom-site-authorization.js";
import { cleanupCustomSiteBrowserAccess } from "../browser-extension/dist/custom-site-cleanup.js";
import { isAuthorizedObservationSender, isPlaybackObservation, isSupportedSender } from "../browser-extension/dist/message-validation.js";

const serviceWorkerSource = readFileSync("browser-extension/src/service-worker.ts", "utf8");
const popupSource = readFileSync("browser-extension/src/popup.ts", "utf8");

assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://open.spotify.com/track/abc" }), true);
assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://music.youtube.com/watch?v=abc" }), true);
assert.equal(isSupportedSender({ tab: { id: 1 }, url: "http://open.spotify.com/track/abc" }), false);
assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://open.spotify.com.evil.example/track/abc" }), false);
assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://example.com/" }), false);
assert.equal(isSupportedSender({ url: "https://open.spotify.com/track/abc" }), false);

assert.equal(isPlaybackObservation({ state: "playing", artist: "Artist", title: "Track" }), true);
assert.equal(isPlaybackObservation({ state: "paused" }), true);
assert.equal(isPlaybackObservation({ state: "unexpected" }), false);
assert.equal(isPlaybackObservation({ state: "playing", title: "x".repeat(1025) }), false);
assert.equal(isPlaybackObservation({ state: "playing", trackNumber: "3" }), false);
assert.equal(isPlaybackObservation({ state: "playing", durationMilliseconds: -1 }), false);
assert.equal(isPlaybackObservation({ state: "playing", artworkUrl: "http://example.com/art.jpg" }), false);
assert.equal(isPlaybackObservation({ state: "playing", artworkUrl: "https://user:pass@example.com/art.jpg" }), false);
assert.equal(isPlaybackObservation({ state: "playing", artworkUrl: "https://example.com/art.jpg" }), true);


const validCustomSender = { tab: { id: 2 }, url: "https://radio.example/player" };
const customSite = {
  id: "site_0123456789abcdef0123456789abcdef",
  origin: "https://radio.example",
  pattern: "https://radio.example/*",
};
assert.equal(isAuthorizedObservationSender(
  { tab: { id: 1 }, url: "https://open.spotify.com/track/abc" }, []), true);
assert.equal(isAuthorizedObservationSender(validCustomSender, [customSite]), true);
assert.equal(isAuthorizedObservationSender(validCustomSender, []), false);
assert.equal(isAuthorizedObservationSender(
  { tab: { id: 2 }, url: "https://radio.example.evil.example/player" }, [customSite]), false);
assert.equal(isAuthorizedObservationSender(
  { tab: { id: 2 }, url: "http://radio.example/player" }, [customSite]), false);
assert.equal(isAuthorizedObservationSender(
  { tab: { id: 2 }, url: "https://radio.example/player" },
  [{ ...customSite, pattern: "https://other.example/*" }]), false);
assert.equal(isAuthorizedObservationSender(
  { tab: { id: 2 }, url: "https://radio.example/player" },
  [{ ...customSite, id: "not-a-site-id" }]), false);

const popupHandlerAt = serviceWorkerSource.indexOf("async function handlePopupMessage");
const removeHandlerAt = serviceWorkerSource.indexOf('if (message.type === "companion.remove-custom-site")', popupHandlerAt);
const revokeAt = serviceWorkerSource.indexOf("customSiteAuthorization.revoke(site.id)", removeHandlerAt);
const persistRevocationAt = serviceWorkerSource.indexOf("persistCustomSiteSessionRevocation(site.id)", removeHandlerAt);
const queuedMutationAt = serviceWorkerSource.indexOf("return serializeCustomSiteMutation", removeHandlerAt);
const awaitSessionRevocationAt = serviceWorkerSource.indexOf("await sessionRevocationPromise", queuedMutationAt);
assert.ok(popupHandlerAt >= 0 && removeHandlerAt > popupHandlerAt && revokeAt > removeHandlerAt
  && persistRevocationAt > revokeAt && queuedMutationAt > persistRevocationAt && awaitSessionRevocationAt > queuedMutationAt,
  "Removal must revoke in-memory authorization and persist its tombstone before the serialized mutation awaits local storage.");
assert.ok(persistRevocationAt >= 0,
  "Removal must persist a per-site session tombstone before local storage mutation.");
assert.ok(serviceWorkerSource.includes("serializeCustomSiteMutation"), "Custom-site adds and removals must share one serialized service-worker mutation boundary.");
assert.ok(serviceWorkerSource.includes("companion.add-custom-site"), "The service worker must own custom-site registration writes.");
assert.ok(!popupSource.includes("storage.local.set({ customSites"), "The popup must not write custom-site arrays directly.");
assert.ok(serviceWorkerSource.includes("customSiteAuthorization.authorize("), "Custom-site forwards must use the revocation-aware authorization gate.");
const connectAt = serviceWorkerSource.indexOf("scrobblixWorkerApi.runtime.connectNative(hostName)");
const finalAuthorizationAt = serviceWorkerSource.indexOf("await authorizeCustomSite()", connectAt);
const nativePostAt = serviceWorkerSource.indexOf("port.postMessage({", connectAt);
assert.ok(connectAt >= 0 && finalAuthorizationAt > connectAt && nativePostAt > finalAuthorizationAt, "Permission, registration and revocation must be revalidated after opening the native port and before posting.");
assert.ok(serviceWorkerSource.includes("isPopupMessage(message) && sender.id === scrobblixWorkerApi.runtime.id && !sender.tab"), "Only this extension's popup page may invoke site removal.");
const trackSiteSource = popupSource.slice(
  popupSource.indexOf("async function trackCurrentSite"),
  popupSource.indexOf("function renderCustomSites"),
);
assert.ok(trackSiteSource.includes("companion.add-custom-site"),
  "The popup must send custom-site additions to the service worker.");
assert.ok(!trackSiteSource.includes("storage.local.set") && !trackSiteSource.includes("registerContentScripts"),
  "The popup must not persist registrations or register scripts outside the serialized mutation boundary.");
const removeSiteSource = popupSource.slice(
  popupSource.indexOf("async function removeSite"),
  popupSource.indexOf("async function testConnection"),
);
assert.ok(removeSiteSource.includes("companion.remove-custom-site"),
  "The popup must route removal through the service worker.");
assert.ok(!removeSiteSource.includes("storage.local.set")
  && !removeSiteSource.includes("unregisterContentScripts")
  && !removeSiteSource.includes("permissions.remove"),
  "The popup must not mutate registration storage or browser access directly.");
const workerRemovalAt = serviceWorkerSource.indexOf('if (message.type === "companion.remove-custom-site")');
const workerPersistAt = serviceWorkerSource.indexOf("storage.local.set", workerRemovalAt);
const workerCleanupAt = serviceWorkerSource.indexOf("cleanupCustomSite(site)", workerPersistAt);
assert.ok(workerRemovalAt >= 0 && workerPersistAt > workerRemovalAt && workerCleanupAt > workerPersistAt,
  "The worker must persist removal before attempting independent browser cleanup.");

const cleanupOrder = [];
const cleanupSucceeded = await cleanupCustomSiteBrowserAccess(
  "site_eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
  "https://radio.example/*",
  {
    unregisterContentScripts: async ({ ids }) => {
      cleanupOrder.push(`unregister:${ids[0]}`);
      throw new Error("synthetic unregister failure");
    },
    containsContentScript: async () => true,
    removePermissions: async ({ origins }) => {
      cleanupOrder.push(`remove-permission:${origins[0]}`);
      return true;
    },
    containsPermissions: async () => false,
  },
);
assert.equal(cleanupSucceeded, false, "A failed content-script cleanup must be reported.");
assert.deepEqual(cleanupOrder, [
  "unregister:site_eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee",
  "remove-permission:https://radio.example/*",
], "Permission removal must still run after content-script cleanup rejects, in the required order.");

const scriptAlreadyAbsent = await cleanupCustomSiteBrowserAccess(
  "site_33333333333333333333333333333333",
  "https://radio.example/*",
  {
    unregisterContentScripts: async () => { throw new Error("already absent"); },
    containsContentScript: async () => false,
    removePermissions: async () => true,
    containsPermissions: async () => false,
  },
);
assert.equal(scriptAlreadyAbsent, true, "An already-unregistered content script is a successful final state.");

const permissionFailure = await cleanupCustomSiteBrowserAccess(
  "site_ffffffffffffffffffffffffffffffff",
  "https://radio.example/*",
  {
    unregisterContentScripts: async () => undefined,
    containsContentScript: async () => false,
    removePermissions: async () => false,
    containsPermissions: async () => true,
  },
);
assert.equal(permissionFailure, false, "A permission that remains granted after removal must be reported.");

const permissionAlreadyAbsent = await cleanupCustomSiteBrowserAccess(
  "site_22222222222222222222222222222222",
  "https://radio.example/*",
  {
    unregisterContentScripts: async () => undefined,
    removePermissions: async () => false,
    containsPermissions: async () => false,
  },
);
assert.equal(permissionAlreadyAbsent, true, "An already-absent browser permission is a successful final state.");

const cleanupSuccess = await cleanupCustomSiteBrowserAccess(
  "site_11111111111111111111111111111111",
  "https://radio.example/*",
  {
    unregisterContentScripts: async () => undefined,
    containsContentScript: async () => false,
    removePermissions: async () => true,
    containsPermissions: async () => false,
  },
);
assert.equal(cleanupSuccess, true, "Both successful browser cleanup operations must report success.");

const deferred = () => {
  let resolve;
  const promise = new Promise((complete) => { resolve = complete; });
  return { promise, resolve };
};
const authorization = new CustomSiteAuthorization();
const permissionCheck = deferred();
const pendingAuthorization = authorization.authorize(
  "site_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  () => permissionCheck.promise,
  async () => true,
);
authorization.revoke("site_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
permissionCheck.resolve(true);
assert.equal(await pendingAuthorization, false, "The authorization gate must reject a pending site after revocation.");
const postAuthorizationGate = new CustomSiteAuthorization();
assert.equal(await postAuthorizationGate.authorize("site_dddddddddddddddddddddddddddddddd", async () => true, async () => true), true);
postAuthorizationGate.revoke("site_dddddddddddddddddddddddddddddddd");
assert.equal(postAuthorizationGate.isRevoked("site_dddddddddddddddddddddddddddddddd"), true,
  "The final pre-delivery check must observe a revocation that occurs after authorization returns.");
assert.equal(await postAuthorizationGate.authorize("site_dddddddddddddddddddddddddddddddd", async () => true, async () => true), false,
  "The second authorization immediately before native-host delivery must reject a site revoked after the first authorization.");

const activeAuthorization = new CustomSiteAuthorization();
assert.equal(await activeAuthorization.authorize("site_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", async () => true, async () => true), true,
  "A registered custom site with browser permission remains forwardable.");
const finalReadAuthorization = new CustomSiteAuthorization();
assert.equal(await finalReadAuthorization.authorize(
  "site_cccccccccccccccccccccccccccccccc",
  async () => true,
  async () => { finalReadAuthorization.revoke("site_cccccccccccccccccccccccccccccccc"); return true; },
), false, "Revocation during the final registration read must prevent forwarding.");


const workerListeners = [];
let workerSettings = { paused: false, customSites: [] };
let storageGetBehavior = async (defaults) => ({ ...defaults, ...workerSettings });
let storageSetBehavior = async (update) => { workerSettings = { ...workerSettings, ...update }; };
let permissionBehavior = async () => true;
let permissionCalls = 0;
let permissionRemoveCalls = 0;
let permissionRemoveResult = true;
let removedPermissionOrigin;
const sessionRevokedSiteIds = new Set();
let connectNativeCalls = 0;
const forwardedPlaybackMessages = [];

const workerApi = {
  runtime: {
    id: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
    onMessage: { addListener: (listener) => workerListeners.push(listener) },
    connectNative: () => {
      connectNativeCalls += 1;
      let responseListener;
      const disconnectListeners = [];
      return {
        onMessage: { addListener: (listener) => { responseListener = listener; } },
        onDisconnect: { addListener: (listener) => disconnectListeners.push(listener) },
        postMessage: (message) => {
          forwardedPlaybackMessages.push(message);
          if (message.type === "playback.observation.v1") {
            responseListener?.({
              protocolVersion: 1,
              type: "accepted.v1",
              messageId: message.messageId,
            });
          }
        },
        disconnect: () => disconnectListeners.forEach((listener) => listener()),
      };
    },
  },
  storage: {
    local: {
      get: async (defaults) => storageGetBehavior(defaults),
      set: async (update) => storageSetBehavior(update),
    },
    session: {
      get: async (key) => {
        const keys = typeof key === "string" ? [key] : Array.isArray(key) ? key : Object.keys(key ?? {});
        return Object.fromEntries(keys.filter((entry) => sessionRevokedSiteIds.has(entry)).map((entry) => [entry, true]));
      },
      set: async (update) => {
        for (const [key, value] of Object.entries(update)) {
          if (value === true) sessionRevokedSiteIds.add(key);
          else sessionRevokedSiteIds.delete(key);
        }
      },
      remove: async (key) => {
        const keys = typeof key === "string" ? [key] : key;
        for (const entry of keys) sessionRevokedSiteIds.delete(entry);
      },
    },
  },
  scripting: {
    registerContentScripts: async () => undefined,
    unregisterContentScripts: async () => undefined,
    getRegisteredContentScripts: async () => [],
  },
  permissions: {
    contains: async (details) => {
      permissionCalls += 1;
      if (details.origins?.includes(removedPermissionOrigin)) return false;
      return permissionBehavior(details, permissionCalls);
    },
    remove: async (details) => {
      permissionRemoveCalls += 1;
      if (permissionRemoveResult) removedPermissionOrigin = details.origins[0];
      return permissionRemoveResult;
    },
  },
};

globalThis.browser = workerApi;
await import("../browser-extension/dist/service-worker.js?custom-site-forwarding-integration");

assert.equal(workerListeners.length, 1, "The production service worker must register its message listener.");
const workerListener = workerListeners[0];
const customSender = { tab: { id: 7 }, url: "https://radio.example/player" };
const observation = { state: "playing", artist: "Artist", title: "Track" };
const raceBeforeConnect = {
  id: "site_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  origin: "https://radio.example",
  pattern: "https://radio.example/*",
};
const removeFromWorker = (site) => new Promise((resolve) => {
  const keepAlive = workerListener(
    { type: "companion.remove-custom-site", site },
    { id: workerApi.runtime.id },
    resolve,
  );
  assert.equal(keepAlive, true, "The service worker must keep the popup removal response channel open.");
});
const addToWorker = (site) => new Promise((resolve) => {
  const keepAlive = workerListener(
    { type: "companion.add-custom-site", site },
    { id: workerApi.runtime.id },
    resolve,
  );
  assert.equal(keepAlive, true, "The service worker must keep the popup add response channel open.");
});


// Exercise the actual forwarding listener while its first permission check is pending.
workerSettings = { paused: false, customSites: [raceBeforeConnect] };
permissionCalls = 0;
const initialPermissionStarted = deferred();
const initialPermission = deferred();
permissionBehavior = async () => {
  initialPermissionStarted.resolve();
  return initialPermission.promise;
};
workerListener(observation, customSender);
await initialPermissionStarted.promise;
const removedBeforeConnect = await removeFromWorker(raceBeforeConnect);
assert.equal(removedBeforeConnect.removed, true);
assert.equal(removedBeforeConnect.cleanupSucceeded, true);
initialPermission.resolve(true);
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(connectNativeCalls, 0, "A site revoked during initial authorization must not open a native port.");
assert.equal(forwardedPlaybackMessages.length, 0, "A site revoked during initial authorization must not post playback data.");
removedPermissionOrigin = undefined;

// Exercise the final pre-delivery check after a native port has already been opened.
const raceBeforePost = {
  id: "site_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
  origin: "https://radio.example",
  pattern: "https://radio.example/*",
};
workerSettings = { paused: false, customSites: [raceBeforePost] };
permissionCalls = 0;
const finalPermissionStarted = deferred();
const finalPermission = deferred();
permissionBehavior = async (_details, callNumber) => {
  if (callNumber === 2) {
    finalPermissionStarted.resolve();
    return finalPermission.promise;
  }
  return true;
};
workerListener(observation, customSender);
await finalPermissionStarted.promise;
assert.equal(connectNativeCalls, 1, "The final authorization race should occur after the native port is opened.");
const removedBeforePost = await removeFromWorker(raceBeforePost);
assert.equal(removedBeforePost.removed, true);
assert.equal(removedBeforePost.cleanupSucceeded, true);
finalPermission.resolve(true);
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(forwardedPlaybackMessages.length, 0, "A site revoked during final authorization must not post playback data.");

// Overlapping adds must serialize at the service-worker storage boundary.
const addSiteOne = {
  id: "site_11111111111111111111111111111111",
  origin: "https://one.example",
  pattern: "https://one.example/*",
};
const addSiteTwo = {
  id: "site_22222222222222222222222222222222",
  origin: "https://two.example",
  pattern: "https://two.example/*",
};
workerSettings = { paused: false, customSites: [] };
permissionCalls = 0;
permissionBehavior = async () => true;
let storageReads = 0;
const addReadStarted = deferred();
const releaseAddRead = deferred();
storageGetBehavior = async (defaults) => {
  storageReads += 1;
  if (storageReads === 1) {
    addReadStarted.resolve();
    await releaseAddRead.promise;
  }
  return { ...defaults, ...workerSettings };
};
const addOnePending = addToWorker(addSiteOne);
await addReadStarted.promise;
const addTwoPending = addToWorker(addSiteTwo);
await Promise.resolve();
assert.equal(storageReads, 1, "A second add must wait instead of reading a stale registration snapshot.");
releaseAddRead.resolve();
const addResults = await Promise.all([addOnePending, addTwoPending]);
assert.deepEqual(addResults.map((result) => result.added), [true, true]);
assert.deepEqual(
  workerSettings.customSites.map((site) => site.id).sort(),
  [addSiteOne.id, addSiteTwo.id].sort(),
  "Concurrent adds must preserve both successful registrations.",
);

// Overlapping removals must not reintroduce a registration removed by the first operation.
const removeSiteOne = { ...addSiteOne, id: "site_33333333333333333333333333333333" };
const removeSiteTwo = { ...addSiteTwo, id: "site_44444444444444444444444444444444" };
workerSettings = { paused: false, customSites: [removeSiteOne, removeSiteTwo] };
storageReads = 0;
const removeReadStarted = deferred();
const releaseRemoveRead = deferred();
storageGetBehavior = async (defaults) => {
  storageReads += 1;
  if (storageReads === 1) {
    removeReadStarted.resolve();
    await releaseRemoveRead.promise;
  }
  return { ...defaults, ...workerSettings };
};
const removeOnePending = removeFromWorker(removeSiteOne);
await removeReadStarted.promise;
const removeTwoPending = removeFromWorker(removeSiteTwo);
await Promise.resolve();
assert.equal(storageReads, 1, "A second removal must wait for the first persisted mutation.");
releaseRemoveRead.resolve();
const removeResults = await Promise.all([removeOnePending, removeTwoPending]);
assert.deepEqual(removeResults.map((result) => result.removed), [true, true]);
assert.deepEqual(workerSettings.customSites, [], "Concurrent removals must leave both removed IDs absent.");

// An add queued behind a removal must preserve the new, unrelated registration.
const existingSite = { ...removeSiteOne, id: "site_55555555555555555555555555555555" };
const newlyAddedSite = {
  id: "site_66666666666666666666666666666666",
  origin: "https://three.example",
  pattern: "https://three.example/*",
};
workerSettings = { paused: false, customSites: [existingSite] };
storageReads = 0;
const interleaveReadStarted = deferred();
const releaseInterleaveRead = deferred();
storageGetBehavior = async (defaults) => {
  storageReads += 1;
  if (storageReads === 1) {
    interleaveReadStarted.resolve();
    await releaseInterleaveRead.promise;
  }
  return { ...defaults, ...workerSettings };
};
const removeExistingPending = removeFromWorker(existingSite);
await interleaveReadStarted.promise;
const addNewPending = addToWorker(newlyAddedSite);
await Promise.resolve();
assert.equal(storageReads, 1, "The add must wait behind the in-flight removal.");
releaseInterleaveRead.resolve();
const [removeExistingResult, addNewResult] = await Promise.all([removeExistingPending, addNewPending]);
assert.equal(removeExistingResult.removed, true);
assert.equal(addNewResult.added, true);
assert.deepEqual(workerSettings.customSites.map((site) => site.id), [newlyAddedSite.id],
  "An add queued after a removal must remain stored without restoring the removed ID.");

// If persistence fails during removal, browser access must still be revoked so a worker restart cannot resume forwarding.
const storageFailureSite = {
  id: "site_77777777777777777777777777777777",
  origin: "https://storage-failure.example",
  pattern: "https://storage-failure.example/*",
};
workerSettings = { paused: false, customSites: [storageFailureSite] };
permissionRemoveCalls = 0;
permissionRemoveResult = false;
removedPermissionOrigin = undefined;
storageSetBehavior = async () => { throw new Error("synthetic storage write failure"); };
const storageFailureResult = await removeFromWorker(storageFailureSite);
assert.equal(storageFailureResult.removed, false, "A failed storage write must not be reported as a completed removal.");
assert.equal(storageFailureResult.cleanupSucceeded, false, "A failed browser permission removal must be reported.");
assert.match(storageFailureResult.detail, /blocked only for this extension session/i,
  "The popup must explain that a session tombstone is blocking the stale registration.");
assert.deepEqual(workerSettings.customSites, [storageFailureSite], "A failed storage write must not pretend the persisted registration was removed.");
assert.equal(permissionRemoveCalls, 1, "Browser permission removal must be attempted even when persistence fails.");
assert.equal(await workerApi.permissions.contains({ origins: [storageFailureSite.pattern] }), true,
  "The test must retain the browser grant to exercise the session tombstone rather than permission cleanup.");
assert.ok(sessionRevokedSiteIds.has(`revokedCustomSite:${storageFailureSite.id}`),
  "The failed removal must persist a session tombstone before the worker can restart.");
const beforeRestartPosts = forwardedPlaybackMessages.length;
const beforeRestartConnections = connectNativeCalls;
await import("../browser-extension/dist/service-worker.js?storage-failure-worker-restart");
assert.equal(workerListeners.length, 2, "The restarted production service worker must register a fresh listener.");
workerListeners[1](observation, { tab: { id: 8 }, url: `${storageFailureSite.origin}/player` });
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(connectNativeCalls, beforeRestartConnections,
  "A worker restart must not restore forwarding when the browser grant remains but a session tombstone exists.");
assert.equal(forwardedPlaybackMessages.length, beforeRestartPosts,
  "A worker restart must not post an observation for a stale registration with a session tombstone.");
assert.match(storageFailureResult.detail, /manually revoke this site's browser permission before closing\/restarting the browser/i,
  "The incomplete-cleanup response must require manual permission revocation before the session tombstone is cleared.");
sessionRevokedSiteIds.delete(`revokedCustomSite:${storageFailureSite.id}`);
const beforeBrowserRestartPosts = forwardedPlaybackMessages.length;
const beforeBrowserRestartConnections = connectNativeCalls;
await import("../browser-extension/dist/service-worker.js?browser-session-cleared");
assert.equal(workerListeners.length, 3, "A simulated browser restart must create a fresh worker after session storage is cleared.");
workerListeners[2](observation, { tab: { id: 12 }, url: `${storageFailureSite.origin}/player` });
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(connectNativeCalls, beforeBrowserRestartConnections + 1,
  "After session storage is cleared, a stale registration and still-granted permission can authorize again; the manual-revocation warning is therefore required.");
assert.equal(forwardedPlaybackMessages.length, beforeBrowserRestartPosts + 1,
  "The browser-restart simulation must demonstrate the stale-grant fallback risk.");
permissionRemoveResult = true;
storageSetBehavior = async (update) => { workerSettings = { ...workerSettings, ...update }; };

// Storage-read failures must still use validated popup site data to revoke browser access.
const readFailureSite = {
  id: "site_88888888888888888888888888888888",
  origin: "https://read-failure.example",
  pattern: "https://read-failure.example/*",
};
workerSettings = { paused: false, customSites: [readFailureSite] };
storageGetBehavior = async () => { throw new Error("synthetic storage read failure"); };
permissionRemoveCalls = 0;
const addReadFailure = await addToWorker(readFailureSite);
assert.equal(addReadFailure.added, false, "A failed registration-state read must not report a successful add.");
assert.match(addReadFailure.detail, /custom site could not be registered/i,
  "The popup must receive a registration-specific error when storage cannot be read.");
permissionRemoveResult = false;
removedPermissionOrigin = undefined;
const removeReadFailure = await removeFromWorker(readFailureSite);
assert.equal(removeReadFailure.removed, false, "A failed registration-state read must not report a successful removal.");
assert.equal(removeReadFailure.cleanupSucceeded, false, "The failed permission cleanup must be reported.");
assert.match(removeReadFailure.detail, /blocked only for this extension session/i,
  "The popup must receive a removal-specific session-revocation result rather than a connection diagnostic.");
assert.equal(permissionRemoveCalls, 2, "Both failed add and failed remove storage reads must attempt exact-origin permission cleanup.");
assert.ok(sessionRevokedSiteIds.has(`revokedCustomSite:${readFailureSite.id}`),
  "A removal whose storage read fails must persist a session tombstone.");
const beforeReadRestartPosts = forwardedPlaybackMessages.length;
const beforeReadRestartConnections = connectNativeCalls;
storageGetBehavior = async (defaults) => ({ ...defaults, ...workerSettings });
await import("../browser-extension/dist/service-worker.js?storage-read-failure-worker-restart");
assert.equal(workerListeners.length, 4, "A storage-read-failure worker restart must register a fresh listener.");
workerListeners[3](observation, { tab: { id: 9 }, url: `${readFailureSite.origin}/player` });
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(connectNativeCalls, beforeReadRestartConnections,
  "A worker restart must not restore forwarding after a storage-read failure when the browser grant remains.");
assert.equal(forwardedPlaybackMessages.length, beforeReadRestartPosts,
  "A worker restart must not post from a stale registration after a storage-read failure.");
permissionRemoveResult = true;
storageGetBehavior = async (defaults) => ({ ...defaults, ...workerSettings });

// Concurrent failed removals must not overwrite one another's session revocation tombstones.
const concurrentRevocationSiteOne = {
  id: "site_99999999999999999999999999999999",
  origin: "https://session-one.example",
  pattern: "https://session-one.example/*",
};
const concurrentRevocationSiteTwo = {
  id: "site_dddddddddddddddddddddddddddddddd",
  origin: "https://session-two.example",
  pattern: "https://session-two.example/*",
};
workerSettings = { paused: false, customSites: [concurrentRevocationSiteOne, concurrentRevocationSiteTwo] };
storageSetBehavior = async () => { throw new Error("synthetic concurrent storage write failure"); };
permissionRemoveResult = false;
removedPermissionOrigin = undefined;
const concurrentRemovalResults = await Promise.all([
  removeFromWorker(concurrentRevocationSiteOne),
  removeFromWorker(concurrentRevocationSiteTwo),
]);
assert.deepEqual(concurrentRemovalResults.map((result) => result.removed), [false, false],
  "Neither failed persistence operation may be reported as a completed removal.");
assert.ok(sessionRevokedSiteIds.has(`revokedCustomSite:${concurrentRevocationSiteOne.id}`),
  "The first concurrent removal must retain its own session tombstone.");
assert.ok(sessionRevokedSiteIds.has(`revokedCustomSite:${concurrentRevocationSiteTwo.id}`),
  "The second concurrent removal must retain its own session tombstone.");
const beforeConcurrentRestartPosts = forwardedPlaybackMessages.length;
const beforeConcurrentRestartConnections = connectNativeCalls;
await import("../browser-extension/dist/service-worker.js?concurrent-session-revocation-restart");
assert.equal(workerListeners.length, 5, "A concurrent-removal worker restart must register a fresh listener.");
workerListeners[4](observation, { tab: { id: 10 }, url: `${concurrentRevocationSiteOne.origin}/player` });
workerListeners[4](observation, { tab: { id: 11 }, url: `${concurrentRevocationSiteTwo.origin}/player` });
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(connectNativeCalls, beforeConcurrentRestartConnections,
  "Neither concurrent stale registration may open a native port after restart.");
assert.equal(forwardedPlaybackMessages.length, beforeConcurrentRestartPosts,
  "Neither concurrent stale registration may post after restart.");
storageSetBehavior = async (update) => { workerSettings = { ...workerSettings, ...update }; };
permissionRemoveResult = true;

// Positive control: a still-registered site with granted browser permission must forward.

const activeSite = {
  id: "site_cccccccccccccccccccccccccccccccc",
  origin: "https://radio.example",
  pattern: "https://radio.example/*",
};
workerSettings = { paused: false, customSites: [activeSite] };
permissionCalls = 0;
permissionBehavior = async () => true;
const beforeActiveForward = forwardedPlaybackMessages.length;
workerListener(observation, customSender);
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(forwardedPlaybackMessages.length, beforeActiveForward + 1, "An active, granted custom site must still forward.");
const activeForward = forwardedPlaybackMessages.at(-1);
assert.equal(activeForward.type, "playback.observation.v1");
assert.equal(activeForward.payload.siteOrigin, activeSite.origin);
assert.equal(activeForward.payload.tabId, customSender.tab.id);

// Older Chromium builds may not expose storage.session. Custom-site add/forward must
// remain available, while incomplete removal explicitly requires manual permission cleanup.
const originalSessionStorage = workerApi.storage.session;
delete workerApi.storage.session;
const noSessionSite = {
  id: "site_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  origin: "https://no-session.example",
  pattern: "https://no-session.example/*",
};
workerSettings = { paused: false, customSites: [] };
storageSetBehavior = async (update) => { workerSettings = { ...workerSettings, ...update }; };
permissionRemoveResult = true;
removedPermissionOrigin = undefined;
const noSessionAdd = await addToWorker(noSessionSite);
assert.equal(noSessionAdd.added, true, "Custom-site registration must remain functional when storage.session is unavailable.");
const beforeNoSessionForward = forwardedPlaybackMessages.length;
workerListener(observation, { tab: { id: 13 }, url: `${noSessionSite.origin}/player` });
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(forwardedPlaybackMessages.length, beforeNoSessionForward + 1,
  "Custom-site forwarding must remain functional without storage.session.");
storageSetBehavior = async () => { throw new Error("synthetic storage write failure without session storage"); };
permissionRemoveResult = false;
removedPermissionOrigin = undefined;
const noSessionRemoval = await removeFromWorker(noSessionSite);
assert.equal(noSessionRemoval.removed, false);
assert.equal(noSessionRemoval.cleanupSucceeded, false);
assert.match(noSessionRemoval.detail, /manually revoke this site's browser permission before closing\/restarting the browser/i,
  "Without storage.session, failed persistence and permission cleanup must explicitly require manual revocation.");
workerApi.storage.session = originalSessionStorage;
storageSetBehavior = async (update) => { workerSettings = { ...workerSettings, ...update }; };
permissionRemoveResult = true;

console.log("Browser security assertions passed (origin authorization, payload shape, bounds, HTTPS artwork, serialized mutations, session tombstones, browser restart fallback and legacy compatibility).");
