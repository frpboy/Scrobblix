# v1 Release Plan — Private Beta First

> **Updated:** 2026-10-22  
> **Integration baseline:** `main`  
> **Current release goal:** Prove reliability and installability, not add more scope.

## Principles

- Do not plan against stale feature status. Code being present is distinct from a validated end-to-end behavior.
- Do not claim a test passed without recording environment, app commit/build, exact steps and observed result.
- Do not infer Last.fm acceptance from "eligible", "queued" or an HTTP request being sent. Confirm an accepted provider outcome.
- Keep listening history, credentials, artwork, session keys and support output private by default.
- Do not introduce dates or public commitments until the acceptance criteria are met and maintainer availability is confirmed.

## Delivery order

1. **Mainline truth and test matrix:** update roadmap/release docs; create the real-world test record. Check exact `main` SHA and associated Windows Actions result.
2. **Manual listening pass:** multi-day Spotify desktop runs with change, pause, resume, seek, repeated track, ads, sleep/wake, network interruption and Last.fm outage/recovery.
3. **Browser companion:** Chrome, Edge, Firefox install/reload/restart/delivery, sender-rejection and uninstall. Keep it optional for Spotify desktop.
4. **Soak and recovery:** 8–24 hours, resource/log growth, app restart, retry behavior, queue consistency, backup/restore and integrity.
5. **Diagnostics/supportability:** clear current-track/delivery states, source health and a redacted support bundle tested with canary secrets.
6. **Security and accessibility:** Native Messaging threat review, least-privilege installation, keyboard/Narrator/contrast/scaling, privacy and dependency review.
7. **Installable candidate:** signed MSIX/App Installer or Store package; clean-machine install, upgrade, repair, rollback, uninstall and update checks.
8. **Private beta:** distribute only to a small test group after the release owner reviews the evidence and known-issues list.
9. **Public beta / v1:** only after beta feedback, failure modes, privacy/legal text and release artifacts are reconciled.

## Private beta entry criteria

- [ ] Current code builds/tests cleanly on the exact candidate SHA.
- [ ] Manual checklist includes results—not just planned cases—for ordinary playback and recovery scenarios.
- [ ] No known duplicate-scrobble/lost-qualified-play defect is open without an explicit mitigation.
- [ ] Queue state and Last.fm account history were reconciled for the test run.
- [ ] Backup restore and corruption/schema handling passed on disposable test data.
- [ ] Support-bundle redaction is tested with seeded canary secrets.
- [ ] Native host/browser integration is tested for every browser advertised in the beta notes.
- [ ] Privacy, accessibility, known issues and uninstall/data-removal instructions reviewed.
- [ ] If distribution is signed, signing identity and timestamp are independently verified.

## Release gates still requiring external resources

- Live Spotify and Windows shell tests require a real Windows session.
- Live Last.fm verification requires a dedicated test account and separately configured API application.
- Chrome/Edge/Firefox require their actual installed extension IDs/add-on identity.
- Signed public distribution requires a publisher identity/certificate or a Microsoft Store identity.

