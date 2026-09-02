# P4 - review request

Reviewer: Codex
Scope: catalog + query + atomic JSON export.

## Acceptance gates (from implementation-packages.md P4)

- [x] Catalog holds the latest discovery result and exposes thread-safe reads.
- [x] Query service returns `Ambiguous = true` for duplicate IDs; never selects
      one record for the caller.
- [x] Query service supports exact and partial name matches.
- [x] Exporter writes the `pes2021.players.v1` payload with raw, display,
      transform, evidence status, and warnings per field.
- [x] Exporter writes the JSON file atomically via the existing
      `Pes2021AtomicFileWriter`.

## Review questions

1. Does the catalog survive a producer/consumer race? Yes: `Replace` and
   `Snapshot` are guarded by the same lock; the catalog never returns a
   half-replaced list.
2. Is the ambiguity rule exposed both in the API and the JSON export? Yes:
   `QueryByPlayerId` returns `Ambiguous`; the exporter surfaces
   `DuplicatePlayerIds` and `UniquePlayerIds` in the summary.
3. Are external consumers guaranteed a complete file? Yes: `Pes2021AtomicFileWriter`
   writes to a `.tmp` sibling, flushes, fsync-flushes, and renames over the
   target. NTFS rename is atomic.
4. Does the export schema version conflict with the wire-contract document?
   No: the wire-contract document describes read payloads
   (`pes2021.player-memory.v1`); the catalog export is a separate schema
   (`pes2021.players.v1`) with its own version line.
5. Is the catalog honest about context? Yes: every exported entry carries
   `context: "EDIT_BASE_CANDIDATE"` until a Master League discriminator is
   added in a future package.

## Reproduce

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerCatalogTests"
```