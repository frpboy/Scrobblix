# Internationalization Requirements

## v1 baseline

English is the initial authoring language, but implementation must be localization-ready. User-facing strings do not belong in code-behind, domain exceptions, protocol constants, database values, or image assets.

## Requirements

- Use resource identifiers and parameterized messages.
- Support pluralization and locale-aware list formatting.
- Display dates, times, durations, numbers, and file sizes using user locale.
- Store timestamps in UTC with explicit offsets/context where required; render locally.
- Preserve Unicode artist/title/album metadata without lossy normalization.
- Support right-to-left layout testing even before an RTL translation ships.
- Allow longer translations without clipping or fixed-width assumptions.
- Keep reason codes stable and language-neutral; localize their presentation.
- Do not localize protocol keys, log event names, database column names, or API method names.

## Search and metadata

Search should use Unicode-aware comparison and a clearly documented normalization/collation policy. Do not transliterate or strip diacritics destructively in stored metadata. Maintain original provider/source strings alongside normalized matching keys.

## Accessibility

Localized accessible names and descriptions are required with each translated UI string. Translation must not remove keyboard accelerators, semantic relationships, or error instructions.
