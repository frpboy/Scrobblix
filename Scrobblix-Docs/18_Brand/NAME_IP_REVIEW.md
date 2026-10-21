# Product Name and Intellectual Property Review

> **Status:** Open launch blocker

This is an engineering/legal diligence checklist, not legal advice.

## Name clearance

Before public launch, review:

- GitHub and major package registries.
- Microsoft Store, Chrome Web Store, Edge Add-ons, and Firefox Add-ons.
- Relevant domains and social handles.
- Search-engine results and music/software products with confusingly similar names.
- Applicable trademark databases and professional legal advice where warranted.
- Package identity, application user model ID, protocol/deep-link scheme, and native-host name.

Record evidence, date, jurisdiction, reviewer, and decision. A repository being available does not establish trademark clearance.

## Third-party integration

- Register and use a compliant Last.fm API application.
- Follow current API terms, attribution requirements, and branding restrictions.
- Do not distribute provider API secrets in public client source if the provider model cannot safely support that; document the approved key strategy before release.
- Review browser-store policies for permissions, native messaging, privacy disclosure, and remote code.
- Review Microsoft packaging and signing requirements.

## Source and dependency licensing

Maintain software-bill-of-materials output and third-party notices. Block dependencies that conflict with the intended distribution model or introduce unreviewed data/telemetry obligations.

## User-submitted metadata and artwork

Treat titles, artist names, album names, tags, URLs, and artwork as third-party/user data. Do not claim ownership. Cache and display only as allowed by source terms and normal interoperability needs.
