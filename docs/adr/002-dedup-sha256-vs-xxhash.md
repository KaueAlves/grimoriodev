# ADR 002: Dedup — SHA-256 vs xxHash

## Context
Content-addressable dedup needs a hash function to identify duplicate card content. Choice between cryptographic (SHA-256) and non-cryptographic (xxHash) hashes.

## Decision
Use SHA-256 for content hashing.

## Consequences
- SHA-256: 32 bytes per hash, ~600 MB/s throughput
- xxHash: 8 bytes per hash, ~15 GB/s throughput
- Chosen SHA-256 for: zero collisions in practice, future-proofing, no need for conflict resolution
- Storage overhead is negligible: blobs are stored once, hash is 32 bytes per unique blob
- Acceptable perf hit: hashing is done on write path (background), not read path
