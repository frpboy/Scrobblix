import assert from "node:assert/strict";
import { isAuthorizedObservationSender, isPlaybackObservation, isSupportedSender } from "../browser-extension/dist/message-validation.js";

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

console.log("Browser security assertions passed (origin authorization, payload shape, bounds and HTTPS artwork).");
