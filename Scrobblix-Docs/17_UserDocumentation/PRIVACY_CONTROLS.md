# Privacy Controls for Users

## Control categories

Scrobblix exposes separate controls for:

- local listening history;
- observation/diagnostic retention;
- artwork cache;
- browser source detection;
- provider submission;
- crash reporting;
- product telemetry;
- support-bundle export.

Turning off telemetry must not disable local history or Last.fm scrobbling. Disconnecting Last.fm must not silently delete local history.

## Source exclusions

Users can disable individual applications, browser origins/adapters, or all browser detection. Exclusions apply prospectively and should explain whether existing local records remain.

## Retention and deletion

The user can choose supported retention periods, delete selected history ranges, clear caches, purge diagnostic detail, and request full local-data deletion. The app must preview the scope and explain irreversible effects before destructive operations.

## Telemetry

Telemetry is off by default. When enabled, the settings page lists categories, destination, retention, identifiers, and a sample payload. Listening titles, artists, albums, URLs, account names, and provider secrets are excluded unless a separate explicit, narrowly scoped opt-in is introduced.

## Support sharing

Support bundles are local files until the user chooses how to share them. The export screen lists contents and redaction status. Scrobblix does not silently upload a support bundle.
