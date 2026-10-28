# Legal and API Compliance Checklist

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Release checklist, not evidence of legal approval  
> **Last reviewed:** 2026-10-28

## Last.fm API

Before each public or beta release:
- Review the current official Last.fm API terms, authentication documentation, rate limits and allowed caching/display rules.
- Maintain a valid API account/key and protect the shared secret; do not publish credentials or put them in preview bundles.
- Use documented APIs/authentication and honor rate-limit/Retry-After behavior.
- Verify when and how Last.fm attribution/linking is required, and never imply Last.fm sponsorship or endorsement.
- Check allowed artwork handling/caching and any restrictions on derived/displayed API data.
- Use the user's browser-based approval flow rather than collecting their Last.fm password.
- Test real provider behavior with a dedicated profile; source/CI does not prove account authorization or accepted submission.
- Recheck all terms and policies before public release; this checklist is engineering guidance, not legal advice.

## Open source and third-party assets

- MIT covers Scrobblix code only, not third-party services/data, trademarks, artwork, fonts, icons or dependencies.
- Generate/review third-party package licenses and notices from the actual release dependency graph; the existing notice file alone is not proof of completed audit.
- Do not redistribute copyrighted album-art/test media without permission; use synthetic fixtures where practical.
- Review provenance and permission for shipped source/provider logos.
- Produce/review a software bill of materials (SBOM) before a production release.

## Browser store and Windows distribution

- Permission declarations, supported-origin list and privacy disclosure must agree. The known generic YouTube manifest/sender allow-list inconsistency is tracked in [issue #73](https://github.com/frpboy/Scrobblix/issues/73); do not advertise unsupported origins before it is resolved and tested.
- Do not use remote executable code or broaden page access without review and user-granted permission.
- Browser-store approval is not established by repository manifests or ZIP artifacts.
- Current Windows bundles are unsigned development previews. Publisher identity, signing, secure update path and install/upgrade/rollback qualification are future gates, not current release capabilities.

## Branding and name/IP diligence

Perform product-name, domain, package/store-name and trademark checks before significant promotion. Add a clear independent-project / no-endorsement disclaimer where necessary. Do not use Last.fm logos/marks outside their current permitted conditions.

## Required release evidence

Keep the date and source of policy review, exact package/commit SHA, package/license report, SBOM, privacy/permission review, branding decision, known issues and sign-off record. A checked checklist is not legal clearance; obtain qualified counsel when legal risk warrants it.
