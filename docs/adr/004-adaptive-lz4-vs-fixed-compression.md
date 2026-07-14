# ADR 004: Adaptive LZ4 vs Fixed Compression Level

## Context
Compression needs to balance write speed (real-time) with storage efficiency (vacuum).

## Decision
Use adaptive LZ4 compression: Level 0 (Fast) for real-time writes, Level 9 (High) for vacuum.

## Consequences
- LZ4 Level 0: ~700 MB/s compress, moderate ratio (used for real-time writes)
- LZ4 Level 9: ~100 MB/s compress, ~15% better ratio (used for vacuum only)
- Fixed L0 would be fast but waste storage long-term
- Fixed L9 would be too slow for interactive use
- Adaptive gives best of both: fast writes during active editing, compact storage after vacuum
- Vacuum runs in background, user never waits for L9 compression
