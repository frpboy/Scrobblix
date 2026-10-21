# Queue State Diagram

```mermaid
stateDiagram-v2
  [*] --> Pending
  Pending --> InFlight
  InFlight --> Accepted
  InFlight --> ProviderIgnored
  InFlight --> RetryScheduled
  InFlight --> AuthBlocked
  InFlight --> PermanentFailure
  RetryScheduled --> InFlight
  AuthBlocked --> Pending
  Pending --> Cancelled
  RetryScheduled --> Cancelled
```
