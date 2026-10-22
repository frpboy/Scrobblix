import assert from "node:assert/strict";
import { isPlaybackObservation, isSupportedSender } from "../browser-extension/dist/message-validation.js";

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

console.log("Browser security assertions passed (origin allow-list, payload shape, bounds and HTTPS artwork).");
