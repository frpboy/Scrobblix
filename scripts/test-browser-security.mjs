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

assert.ok(serviceWorkerSource.includes("customSiteAuthorization.revoke(message.siteId)"), "Removal must invalidate pending custom-site forwards before storage awaits.");
assert.ok(serviceWorkerSource.includes("serializeCustomSiteMutation"), "Custom-site adds and removals must share one serialized service-worker mutation boundary.");
assert.ok(serviceWorkerSource.includes("companion.add-custom-site"), "The service worker must own custom-site registration writes.");
assert.ok(!popupSource.includes("storage.local.set({ customSites"), "The popup must not write custom-site arrays directly.");
assert.ok(serviceWorkerSource.includes("customSiteAuthorization.authorize("), "Custom-site forwards must use the revocation-aware authorization gate.");
const connectAt = serviceWorkerSource.indexOf("scrobblixWorkerApi.runtime.connectNative(hostName)");
const finalAuthorizationAt = serviceWorkerSource.indexOf("await authorizeCustomSite()", connectAt);
const nativePostAt = serviceWorkerSource.indexOf("port.postMessage({", connectAt);
assert.ok(connectAt >= 0 && finalAuthorizationAt > connectAt && nativePostAt > finalAuthorizationAt, "Permission, registration and revocation must be revalidated after opening the native port and before posting.");
assert.ok(serviceWorkerSource.includes("isPopupMessage(message) && sender.id === scrobblixWorkerApi.runtime.id && !sender.tab"), "Only this extension's popup page may invoke site removal.");
const removeSiteSource = popupSource.slice(popupSource.indexOf("async function removeSite"));
assert.ok(removeSiteSource.indexOf("companion.remove-custom-site") >= 0
  && removeSiteSource.indexOf("companion.remove-custom-site") < removeSiteSource.indexOf("unregisterContentScripts"), "The popup must revoke forwarding before browser cleanup.");

assert.ok(removeSiteSource.includes("cleanupCustomSiteBrowserAccess("),
  "The popup must use the independently-failing browser cleanup helper.");
assert.ok(removeSiteSource.indexOf("cleanupCustomSiteBrowserAccess(") < removeSiteSource.indexOf("getCustomSites()"),
  "The popup must refresh the custom-site list after both browser cleanup attempts.");
assert.ok(removeSiteSource.includes("browser cleanup was incomplete"),
  "The popup must surface incomplete browser cleanup to the user.");

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
let permissionBehavior = async () => true;
let permissionCalls = 0;
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
      set: async (update) => { workerSettings = { ...workerSettings, ...update }; },
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
      return permissionBehavior(details, permissionCalls);
    },
    remove: async () => true,
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
const removeFromWorker = (siteId) => new Promise((resolve) => {
  const keepAlive = workerListener(
    { type: "companion.remove-custom-site", siteId },
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
assert.deepEqual(await removeFromWorker(raceBeforeConnect.id), { removed: true, cleanupSucceeded: true });
initialPermission.resolve(true);
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(connectNativeCalls, 0, "A site revoked during initial authorization must not open a native port.");
assert.equal(forwardedPlaybackMessages.length, 0, "A site revoked during initial authorization must not post playback data.");

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
assert.deepEqual(await removeFromWorker(raceBeforePost.id), { removed: true, cleanupSucceeded: true });
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
const removeOnePending = removeFromWorker(removeSiteOne.id);
await removeReadStarted.promise;
const removeTwoPending = removeFromWorker(removeSiteTwo.id);
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
const removeExistingPending = removeFromWorker(existingSite.id);
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

storageGetBehavior = async (defaults) => ({ ...defaults, ...workerSettings });

// Positive control: a still-registered site with granted browser permission must forward.

const activeSite = {
  id: "site_cccccccccccccccccccccccccccccccc",
  origin: "https://radio.example",
  pattern: "https://radio.example/*",
};
workerSettings = { paused: false, customSites: [activeSite] };
permissionCalls = 0;
permissionBehavior = async () => true;
workerListener(observation, customSender);
await new Promise((resolve) => setTimeout(resolve, 0));
assert.equal(forwardedPlaybackMessages.length, 1, "An active, granted custom site must still forward.");
assert.equal(forwardedPlaybackMessages[0].type, "playback.observation.v1");
assert.equal(forwardedPlaybackMessages[0].payload.siteOrigin, activeSite.origin);
assert.equal(forwardedPlaybackMessages[0].payload.tabId, customSender.tab.id);

console.log("Browser security assertions passed (origin authorization, payload shape, bounds, HTTPS artwork and in-flight custom-site revocation).");
