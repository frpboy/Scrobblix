# Performance Tests

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## Benchmarks

- Observation normalization/arbitration/state processing throughput.
- SQLite insert/query/export with 1k/100k/1m synthetic plays.
- Queue batch selection/completion.
- Analytics rebuild.
- Artwork decode/cache eviction.
- Native-message flood/coalescing.

## System tests

Measure idle and active CPU, private working set, wakeups/timers, disk writes, startup, UI latency, history scrolling/search, extension latency, long-run leak over 24–72 hours.

## Reference hardware

Define at least a low/mid Windows 11 reference device and CI/VM caveats. Record OS/build/runtime/app versions. Regressions above agreed thresholds block release or require documented waiver.
