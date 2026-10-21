# Onboarding

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Principles

- Complete in under five minutes for a typical user.
- Explain before requesting permission.
- Support skip and local-only mode.
- Verify each integration rather than assuming installation succeeded.

## Steps

### 1. Welcome

Explain reliable scrobbling, local history, diagnostics, and no audio recording.

### 2. Privacy choices

Local history on by default; telemetry off. Offer concise links to full policy and data locations.

### 3. Connect Last.fm

Open authorization, show pending state, handle cancellation, verify username, store session securely.

### 4. Detect sources

Scan GSMTC sessions and show detected apps. Explain that absence is normal when nothing is playing.

### 5. Browser Companion

Optional. Show benefits, exact permissions, supported browsers, install link/manual steps, then run native-host ping.

### 6. Startup and notifications

Choose launch at sign-in and essential notifications. Avoid asking for every setting.

### 7. Verification

Ask user to start a track or use a safe synthetic test event. Show source, metadata, and progress. Do not send a fake scrobble.

### 8. Finish

Summarize connected account, enabled paths, data location, and where to diagnose problems.

## Failure recovery

Every failed step can be retried or skipped. Onboarding state persists without storing transient authorization secrets in plaintext.
