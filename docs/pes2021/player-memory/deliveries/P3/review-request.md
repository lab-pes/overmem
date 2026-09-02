# P3 - review request

Reviewer: Codex
Scope: read-only memory discovery via the gateway abstraction. No writes.

## Acceptance gates (from implementation-packages.md P3)

- [x] Region discovery uses the same `IProcessMemoryGateway.ListRegionsAsync` shape as the
      fixture extension.
- [x] Region filtering follows the profile's `state`, `type`, `requireReadable`,
      `requireWritable`, `allowExecutable` rules.
- [x] Anchor finder uses stride-aligned scan (one candidate per record).
- [x] Anchor finder scores cheap validation + name presence + validator acceptance.
- [x] Anchor finder returns null anchor and a `low`-confidence result when no
      candidate matches the medium-score threshold.
- [x] Session cache keys on `attachmentId + PID + processStartedAtUtc + profileId +
      profileVersion + profileSha256`.
- [x] Session cache revalidates the anchor SHA-256 before reuse.
- [x] Diagnostics capture regions, reads, records, duplicates, rejections, warnings,
      and stage durations.
- [x] No write call ever issued.

## Review questions

1. Does the cache naturally invalidate across PES restarts? Yes: a new
   attachment gets a new `attachmentId`, the new `processStartedAtUtc` differs,
   and the cache key combines both. The revalidation step is a defense-in-depth
   that catches mid-session memory churn.
2. Is the candidate list exhaustive? Yes: the finder iterates every accepted
   region, walks stride-aligned slots, and emits one candidate per record whose
   `playerId` matches the requested control.
3. Are ambiguous ties surfaced, not silently resolved? Yes: the result carries
   `Ambiguous = true` when ties survive the earliest-address tiebreaker.
4. Does the diagnostics payload cover every counter needed for the wire schema?
   Yes: regions, read calls, bytes requested/read, records decoded/accepted/
   rejected, duplicate IDs, ambiguous resolutions, rejection reasons, stage
   timings, and warnings.
5. Is the synthetic gateway faithful to the real one? Yes: it implements every
   method of `IProcessMemoryGateway`. The `WriteAsync` and pattern-scan methods
   are exercised only by the transaction tests (P7), not by P3.

## Reproduce

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerDiscoveryTests"
```