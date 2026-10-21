# Localization Workflow

## Source lifecycle

1. Product copy is approved in English.
2. Resource extraction produces a versioned catalogue with context, screenshots, variables, and character constraints only where unavoidable.
3. Translators work from complete sentences and usage context.
4. Automated checks validate missing/extra variables, malformed markup, duplicate IDs, and untranslated critical strings.
5. Linguistic review runs in the application at multiple window sizes and DPI scales.
6. Accessibility and keyboard testing repeat for each supported locale.

## Translation ownership

Community translations require contributor attribution, review rules, and a maintainer who can judge quality. Machine translation may be used only as a draft and must be labeled/reviewed before a supported release.

## Versioning

Each release freezes strings before release-candidate testing. Emergency string changes trigger targeted translation fallback and must not crash resource loading. Unsupported/missing strings fall back to English while preserving diagnostic codes.
