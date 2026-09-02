# P3 - Anchor finder, region scanner, session cache

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: read-only memory access via the existing gateway abstraction. No writes.

## Goal

Discover the EDIT-base arena inside a real process, decode every structurally valid
380-byte player record, and cache the result so a second call within the same
attachment session reuses it without rescanning.

## Changed files

### New production code

- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerDiscoveryDiagnostics.cs`
  - `PlayerProcessInstanceIdentity`, `PlayerSession`, `PlayerSessionCacheKey`,
    `PlayerSessionCacheEntry`, `PlayerRegionDiagnostic`, `PlayerDiscoveryDiagnostics`,
    `PlayerAnchorCandidate`, `PlayerAnchorConfidence`, `PlayerAnchorResult`,
    `PlayerDiscoveryResult`. Reuses the existing `CacheDisposition` enum from the
    fixture extension to keep wire payloads consistent.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerDiscoveryDiagnosticsCollector.cs`
  - Aggregator: read calls, regions accepted/rejected, records decoded/accepted/
    rejected, duplicate player IDs, ambiguous resolutions, rejection reasons,
    stage durations, warnings.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerSessionCache.cs`
  - In-memory cache keyed by `attachmentId + PID + processStartedAtUtc +
    profileId + profileVersion + profileSha256`. Reuse revalidates the cached
    anchor SHA-256; mismatch invalidates and forces rediscover.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerAnchorFinder.cs`
  - Reads regions through `IProcessMemoryGateway`, scans stride-aligned slots,
    scores each candidate that carries the requested control player ID, hashes
    the 760-byte window at the winner's address for cache revalidation, and
    returns the result with diagnostics.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerRegionScanner.cs`
  - Same region filtering, same chunked scan, decodes every stride-aligned
    record with `Pes2021PlayerRecordParser`, accumulates duplicate player IDs,
    and reports counters via the diagnostics collector.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/FakeProcessMemoryGateway.cs`
  - Synthetic gateway backed by an in-memory byte map; records every read and
    write so tests can assert on the gateway traffic.
- `tests/Overmem.Extensions.Pes2021.Tests/FakeSystemClock.cs`
  - Deterministic clock for deterministic `ValidatedAtUtc` values.
- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerDiscoveryTests.cs` (8 tests)
  - Anchor finder locates the control record by player ID.
  - Anchor finder returns null anchor when no candidate matches.
  - Anchor finder respects the profile region filter (rejected regions are
    counted, no candidates emitted).
  - Region scanner decodes all five control records and reports zero duplicates.
  - Region scanner detects duplicate player IDs across non-adjacent slots.
  - Session cache stores and reuses an entry after revalidation.
  - Session cache invalidates when the bytes at the anchor have changed.
  - Session cache `InvalidateByAttachment` only removes entries for the matching
    attachment.
  - Diagnostics collector aggregates reads, records, duplicates, and rejections.

## Decisions

- **Stride-aligned scan:** the anchor finder walks stride-aligned slots. This is
  deliberate: the player ID is at offset 48, so a raw 4-byte window scan would
  produce candidates at every stride + 48. Aligning to stride keeps candidates
  one per record.
- **Score uses cheap validation:** a candidate gets +5 for passing cheap
  checks, +2 for a non-empty player name, +3 for full validator acceptance.
  The minimum `MediumScore` in the built-in profile is 8, so a single valid
  record is sufficient to anchor.
- **Cache revalidation reads 760 bytes** (2× stride). This is the same probe
  the fixture cache uses; it survives chunk-boundary effects in the OS.
- **Region scanner counts duplicates at the *record* level**, not at the
  *address* level. Two records with the same player ID at different addresses
  each count as one duplicate instance.
- **Region filter uses the existing shape.** Read/writable, non-executable,
  committed, private are the defaults; the profile may override them.

## Limitations

- The scanner does not yet discover sub-arenas (e.g. Master League). All output
  is tagged `EDIT_BASE_CANDIDATE` until an explicit context discriminator is
  added (P6).
- The anchor finder scans for a single control player ID at a time. Multi-anchor
  discovery (consensus from several IDs) is future work.

## Rollback

Reverting the five production files and the two test files restores the
repository to its pre-P3 state. No process memory code was added beyond the
existing gateway abstraction; no writes are issued.