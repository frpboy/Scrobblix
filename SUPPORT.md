# Support

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Pre-release support guidance  
> **Last reviewed:** 2026-10-28


## User support channels

- GitHub Issues: confirmed defects and feature requests.
- GitHub Discussions: setup questions, troubleshooting, compatibility reports, and ideas.
- Security reporting: private vulnerability reporting only.

## Required diagnostic information

Before reporting a missed scrobble, capture:

- Scrobblix version and installation channel;
- Windows version;
- source application/browser and website;
- whether GSMTC and/or the browser companion detected the session;
- track metadata shown by Scrobblix;
- eligibility progress and final reason code;
- queue/API state;
- sanitized diagnostic bundle.

Never paste a Last.fm session key, API secret, full Windows username/path, exact personal listening history, full browsing URLs, raw databases, or unredacted logs into a public issue. Inspect any support ZIP before sharing. The support export is local and is not uploaded automatically.


## Current support scope

Public support reports should include the exact Scrobblix commit/version and installation type. Current public artifacts are unsigned development previews rather than signed production installers. Real-browser, live Last.fm, accessibility and long-duration qualification are not guaranteed by CI alone.
