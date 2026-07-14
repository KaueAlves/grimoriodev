# ADR 001: Data File vs Per-Card Files

## Context
Storing thousands of cards as individual files causes NTFS MFT overhead, slow directory enumeration, and increased I/O operations.

## Decision
Use a single append-only data file (`data.lz4`) with segment-based addressing instead of per-card files.

## Consequences
- Single file = single NTFS entry, no MFT pressure
- Append-only = sequential writes, no random I/O
- Segment-based (16MB) = bounded mmap views
- Requires index (`idx.bin`) to locate cards within the file
- Vacuum needed to reclaim space from deleted cards
