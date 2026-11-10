# Code signing policy

Status on 10 November 2026: preparing an application to SignPath Foundation. Scrobblix has not been accepted, no SignPath signing subscription is configured, and the published Windows beta.9 installer is unsigned. This page does not claim current sponsorship or code-signing coverage.

## Proposed free signing route

We are seeking free code signing provided by SignPath.io, with a certificate by SignPath Foundation, subject to approval and its [published conditions](https://signpath.org/terms.html). If accepted, the certificate publisher would be SignPath Foundation, not the maintainer personally. The Foundation can pause signing or revoke its certificate when its conditions are violated. Signed releases would identify their exact source revision, build provenance and signed artifact hashes.

Only Scrobblix-owned binaries would be signed under this project. Upstream binaries would retain their own signatures and notices and would not be re-signed as our work. Dependency licensing and the permitted System Library exception need review before a signing request. A signature would not mean Microsoft Store certification or a guarantee that every reputation warning disappears.

## Roles and approval

- Repository owner and maintainer: Rahul Muraleedharan (`frpboy`).
- Human code-review and signing-approval assignments: not yet confirmed for the proposed SignPath project. Signing remains disabled until these responsibilities are confirmed.
- GitHub and SignPath multi-factor authentication: required for all project team members under the proposed Foundation route. Compliance has not yet been verified; this page does not assert it is enabled.
- Every release-signing request would require a trusted human approver. Automated tests and reviews do not substitute for that approval.

## Privacy and current limits

See the [Windows privacy notice](WINDOWS_PRIVACY.md). The current application-readiness review includes accurate broker/third-party network disclosure, installer privacy presentation and a user control for remote metadata enrichment, consistent product/version metadata, dependency provenance and verifiable project reputation. These are open checks, not completed compliance claims.

The public [repository](https://github.com/frpboy/Scrobblix), [release page](https://github.com/frpboy/Scrobblix/releases) and CI records document the actual source and downloadable artifacts. Any future signed release must be verified and installation/upgrade-tested using the final signed bytes. Existing unsigned release assets will not be silently replaced.
