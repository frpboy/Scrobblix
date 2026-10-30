import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { CustomSiteAuthorization } from "../browser-extension/dist/custom-site-authorization.js";
import { isAuthorizedObservationSender, isBuiltInSupportedOrigin, isPlaybackObservation, isSupportedSender } from "../browser-extension/dist/message-validation.js";

assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://open.spotify.com/track/abc" }), true);
assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://music.youtube.com/watch?v=abc" }), true);
assert.equal(isBuiltInSupportedOrigin("https://open.spotify.com"), true);
assert.equal(isBuiltInSupportedOrigin("https://music.youtube.com"), true);
assert.equal(isBuiltInSupportedOrigin("https://youtube.com"), false);
assert.equal(isBuiltInSupportedOrigin("https://www.youtube.com"), false);
assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://youtube.com/watch?v=abc" }), false);
assert.equal(isSupportedSender({ tab: { id: 1 }, url: "https://www.youtube.com/watch?v=abc" }), false);
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

const genericYoutubeSender = { tab: { id: 3 }, url: "https://www.youtube.com/watch?v=abc" };
const youtubeCustomSite = {
  id: "site_fedcba9876543210fedcba9876543210",
  origin: "https://www.youtube.com",
  pattern: "https://www.youtube.com/*",
};
assert.equal(isAuthorizedObservationSender(genericYoutubeSender, []), false);
assert.equal(isAuthorizedObservationSender(genericYoutubeSender, [youtubeCustomSite]), true);

const serviceWorkerSource = readFileSync("browser-extension/src/service-worker.ts", "utf8");
const popupSource = readFileSync("browser-extension/src/popup.ts", "utf8");
assert.ok(serviceWorkerSource.includes("isBuiltInSupportedOrigin(sourceUrl.origin)"), "Service worker must use the canonical built-in origin policy.");
assert.ok(serviceWorkerSource.includes("isAuthorizedObservationSender(sender, settings.customSites)"), "Non-built-in origins must have explicit custom-site registration.");
assert.ok(serviceWorkerSource.includes("scrobblixWorkerApi.permissions.contains({ origins: ["), "Custom origins must be checked against browser-granted permissions.");
assert.ok(serviceWorkerSource.includes("${sourceUrl.origin}/*"), "Custom-site permission checks must be limited to the exact origin.");
assert.ok(serviceWorkerSource.includes("customSiteAuthorization.revoke(message.siteId)"), "Removal must invalidate the site in the service-worker authorization gate before awaiting storage.");
assert.ok(serviceWorkerSource.includes("customSiteAuthorization.authorize("), "Custom-site forwarding must pass through the revocation-aware authorization gate.");
const removeSiteSource = popupSource.slice(popupSource.indexOf("async function removeSite"));
assert.ok(removeSiteSource.indexOf("companion.remove-custom-site") >= 0 && removeSiteSource.indexOf("companion.remove-custom-site") < removeSiteSource.indexOf("unregisterContentScripts"), "Popup removal must revoke forwarding before unregistering content scripts or permissions.");
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


// Deterministically reproduce removal while the browser permission check is pending.
const deferred = () => {
  let resolve;
  const promise = new Promise((complete) => { resolve = complete; });
  return { promise, resolve };
};
const authorization = new CustomSiteAuthorization();
const permissionCheck = deferred<boolean>();
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
assert.equal(await pendingForward, false, "A pending custom-site authorization must fail after removal starts.");
assert.equal(nativeHostPosts, 0, "A removed custom site must not post its in-flight observation to the native host.");

const activeAuthorization = new CustomSiteAuthorization();
assert.equal(await activeAuthorization.authorize("site_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", async () => true, async () => true), true,
  "A registered custom site with browser permission must remain forwardable.");
const removalDuringFinalRead = new CustomSiteAuthorization();
assert.equal(await removalDuringFinalRead.authorize(
  "site_cccccccccccccccccccccccccccccccc",
  async () => true,
  async () => { removalDuringFinalRead.revoke("site_cccccccccccccccccccccccccccccccc"); return true; },
), false, "Revocation during the final registration read must also prevent forwarding.");

console.log("Browser security assertions passed (origin authorization, payload shape, bounds, HTTPS artwork and in-flight custom-site revocation).");
