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
let nativeHostPosts = 0;
const pendingForward = authorization.authorize(
  "site_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  () => permissionCheck.promise,
  async () => true,
).then((allowed) => {
  if (allowed) nativeHostPosts += 1;
  return allowed;
});
authorization.revoke("site_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
permissionCheck.resolve(true);
assert.equal(await pendingForward, false, "A pending authorization must fail when its site is revoked.");
assert.equal(nativeHostPosts, 0, "A removed site must not post its in-flight observation.");
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

console.log("Browser security assertions passed (origin authorization, payload shape, bounds, HTTPS artwork and in-flight custom-site revocation).");
