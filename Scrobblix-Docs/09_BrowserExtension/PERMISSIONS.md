# Browser Companion Permissions

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Current manifest policy; real-browser permission UX pending  
> **Last reviewed:** 2026-10-28

## Current manifest permissions

The Chromium manifest currently declares:

| Permission | Purpose | Status/notes |
|---|---|---|
| `nativeMessaging` | Send validated metadata to the local Scrobblix Native Messaging host | Required for desktop integration |
| `storage` | Keep local extension settings/state | Required by extension code |
| `activeTab` | Temporary access associated with explicit user interaction | Must remain tied to the browser-granted interaction |
| `scripting` | Inject/execute the companion observer when permitted | Required for the supported workflow |
| Built-in host permissions | Observe the listed supported playback sites | Static manifest scopes; disclose them plainly |
| Optional `http://*/*` and `https://*/*` host patterns | Allow the user to add another site and grant access to that exact origin through browser UX | Optional permission; never use as silent blanket scraping permission |

The permission list is taken from `browser-extension/manifest.json`; verify the Firefox manifest separately because browser API and permission behavior can differ.

## Least privilege and site grants

Do not add broader required permissions without a concrete feature need, a privacy/security review, and a manifest/test update. The optional HTTP/HTTPS patterns enable user-initiated site additions; the extension should only act on an origin after the browser has granted permission and the user has requested the addition. A generic host permission pattern in `optional_host_permissions` is not permission to silently start observing every site.

Prefer source-specific media metadata over collecting page content. Never read passwords, cookies, form fields, messages, or unrelated browsing history. Never capture system audio or video frames.

## Private browsing

Incognito behavior must remain disabled by default and comply with browser defaults unless the user explicitly enables an extension for private windows. Before advertising private-window support, test it and disclose that media metadata may be sent locally to the desktop and persisted in local history. Do not infer a tested incognito state from manifest absence alone.

## Permission UX and revocation

The popup should state what the companion does, provide clear user action to test host reachability or add a site, and show actionable errors. The popup requests optional-origin permission in response to the user's click, then sends the requested registration to the service worker. The service worker owns both add/remove persistence and browser content-script/permission cleanup behind one serialized mutation queue; popup code must not read/modify/write the full `customSites` array. Removal invalidates the in-memory authorization gate synchronously and records a per-site revocation tombstone in `storage.session` before waiting behind another mutation. The tombstone is checked before custom-site forwarding and survives service-worker restarts. The service worker then persists registration removal and independently unregisters the content script and revokes the exact browser permission. If local storage reads or writes fail, the validated popup site identity is used for exact-origin cleanup; the session tombstone continues blocking stale registrations even when permission cleanup also fails. `storage.session` is temporary and is cleared when the extension is reloaded/disabled or the browser session ends, so if it is the only remaining protection the popup requires manual permission revocation before those events. `storage.session` is available in Chrome/Edge 102+ and Firefox 115+; the Firefox manifest currently requires 121+. The worker feature-detects this API so older Chromium builds retain normal custom-site add/forward behavior; when it is unavailable and both local persistence and browser cleanup fail, the popup explicitly requires manual permission revocation before a worker/browser restart. Pending custom-site forwards re-check the session tombstone, browser grant and latest registration after asynchronous permission checks and again after opening the native port immediately before delivery. Failed cleanup is surfaced to the user; a removed registration is never restored by a stale concurrent write. Mutation messages are accepted only from this extension's own popup context, not a content script. See [issue #87](https://github.com/frpboy/Scrobblix/issues/87) for concurrency regression coverage. A host registration entry does not prove that the extension is currently enabled or delivering observations.

## Release gate

Test the exact Chrome, Edge and Firefox versions on Windows. Record required/optional permission prompts, added-site grant/revoke, extension disable/uninstall, Native Messaging registration and absence of events after permission removal. Keep those manual results `NOT RUN` until performed.

**Origin consistency note (tracked in [issue #73](https://github.com/frpboy/Scrobblix/issues/73)):** The manifests include generic YouTube host matches, but the code-level built-in sender allow-list currently only includes `https://music.youtube.com` and `https://open.spotify.com`. Generic `youtube.com` / `www.youtube.com` observations are not implicitly trusted as built-in origins and may be rejected unless explicitly registered as a custom site. Do not advertise those generic origins as natively supported until the manifest and validation layers are reconciled and tested.
