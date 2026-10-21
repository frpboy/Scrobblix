# Frequently Asked Questions

## Does Scrobblix listen to or record my system audio?

No. The initial product uses media metadata and playback state from Windows and, optionally, a browser extension. Audio capture and acoustic fingerprinting are explicitly outside the v1 scope.

## Do I need the Browser Companion?

Not always. GSMTC can detect many desktop and browser players. Install the companion when browser metadata is missing, multiple tabs are ambiguous, or exact site/tab status is useful.

## Will I lose scrobbles when offline?

Qualifying actions are stored in a durable local queue and retried according to policy. Permanent provider rejection, user deletion, database damage, or unsupported metadata can still prevent submission; those cases must be shown explicitly.

## Why do local statistics differ from Last.fm?

Scrobblix can count detected or eligible local plays, while Last.fm shows provider-accepted scrobbles and applies its own processing. Each local statistic identifies its counting basis.

## Can I use Scrobblix without Last.fm?

Yes. Local-history mode tracks supported playback and statistics without provider submission.

## Can Scrobblix control playback?

The v1 focus is observation and scrobbling. Limited source actions may be considered later, but the app is not intended to replace a media player.

## Does Scrobblix upload my history to its own server?

The baseline architecture has no Scrobblix cloud account or central listening-history service. Provider submissions go only to the service the user explicitly connects. Optional crash/telemetry features are separately controlled and off by default.

## Can I edit a wrong scrobble?

Local display metadata may be editable. Remote correction depends on provider capabilities; Scrobblix must not pretend a local edit altered an already accepted Last.fm record.

## Does it support multiple Last.fm accounts?

The v1 design supports one active account per Windows user. Account switching is explicit and pending actions remain associated with their original account context.

## Where are credentials stored?

Provider session secrets are stored in Windows protected credential storage, not in the history database or plain settings files.
