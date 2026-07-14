# ADR 003: Group Commit WAL vs Per-Entry WAL

## Context
Write-Ahead Log needs to balance durability guarantees with write performance.

## Decision
Use group commit WAL with buffer-and-flush pattern.

## Consequences
- Per-entry WAL: N writes = N fsync (~10ms each) = N × 10ms latency
- Group commit: N writes = 1 fsync = ~10ms total latency
- Trade-off: small risk of losing up to MaxBatchSize (10) entries on crash
- Risk mitigated by: flush timer (3s max), manual flush on Ctrl+S, and flush on clean shutdown
- Implementation: buffer in memory, flush on 10 entries OR 3 seconds OR manual trigger
