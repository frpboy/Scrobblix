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

The popup should state what the companion does, provide clear user action to test host reachability or add a site, and show actionable errors. When the user removes a site, the popup first asks the service worker to synchronously invalidate that registration and remove it from the local allow-list. Only after that acknowledgement does the popup unregister the content script and revoke the browser permission. Pending custom-site forwards re-check the browser grant and latest registration after asynchronous permission checks; a removed registration must not reach the native host. The removal message is accepted only from this extension's own popup context, not a content script. A host registration entry does not prove that the extension is currently enabled or delivering observations.

## Release gate

Test the exact Chrome, Edge and Firefox versions on Windows. Record required/optional permission prompts, added-site grant/revoke, extension disable/uninstall, Native Messaging registration and absence of events after permission removal. Keep those manual results `NOT RUN` until performed.

Built-in browser origins (issue #73): the v1 built-in YouTube adapter is YouTube Music (`https://music.youtube.com`), alongside Spotify Web (`https://open.spotify.com`). Generic `https://youtube.com` and `https://www.youtube.com` pages are intentionally excluded from fixed content-script matches and required built-in host permissions. A user may add a standard YouTube origin through the custom-site flow; forwarding then requires both the exact stored registration and the browser's grant for that origin. Standard YouTube videos may not publish artist metadata and must not be described as built-in support.
