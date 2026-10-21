# State Machines

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01


## Play session

```mermaid
stateDiagram-v2
    [*] --> Candidate
    Candidate --> Playing: valid playing observation
    Candidate --> Ignored: policy/metadata invalid
    Playing --> Paused: paused observation
    Playing --> Unknown: source uncertainty/disconnect grace
    Paused --> Playing: resume same media
    Unknown --> Playing: reliable resume
    Playing --> Eligible: threshold reached
    Paused --> Abandoned: replaced/timeout before eligible
    Unknown --> Abandoned: grace expired before eligible
    Eligible --> Ended: track ends/replaced
    Playing --> Ended: ends after eligibility action created
    Candidate --> Duplicate: matched canonical play
    Playing --> Duplicate: merged into existing play
    Ignored --> [*]
    Abandoned --> [*]
    Duplicate --> [*]
    Ended --> [*]
```

Eligibility is a milestone orthogonal to playback state in implementation; the diagram simplifies it. A play can remain playing after eligibility.

## Queue action

```mermaid
stateDiagram-v2
    [*] --> Pending
    Pending --> InFlight: lease acquired
    InFlight --> Accepted
    InFlight --> ProviderIgnored
    InFlight --> RetryScheduled: transient/rate limit
    InFlight --> AuthBlocked: invalid session
    InFlight --> PermanentFailure: non-retryable
    RetryScheduled --> InFlight: due and lease acquired
    AuthBlocked --> Pending: account reconnected
    Pending --> Cancelled: user cancel
    RetryScheduled --> Cancelled: user cancel
    Accepted --> [*]
    ProviderIgnored --> [*]
    PermanentFailure --> [*]
    Cancelled --> [*]
```

## Provider health

`Unknown → Checking → Healthy | Degraded | Offline | AuthenticationRequired | Suspended`. Health is not inferred solely from one request; transitions use explicit evidence and cooldowns.

## Extension connection

`NotInstalled → InstalledNoHost → HostRegistered → Connecting → Connected → VersionMismatch/PermissionRequired/Disconnected`. Repair actions correspond to each state.

## Persistence requirement

State transitions that affect eligibility, queue, provider outcome, user-visible reason, or recovery must be persisted atomically before publication to UI.
