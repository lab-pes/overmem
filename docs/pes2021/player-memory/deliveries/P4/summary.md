# P4 - Player catalog, query service, JSON atomic export

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: read-only catalog over the existing P3 discovery surface.

## Goal

Wrap the discovery result in a catalog object, expose query helpers that respect the
ambiguity rule from the wire contracts, and serialize the catalog to JSON atomically
under the schema `pes2021.players.v1` so external consumers (Sider modules, Lua
scripts) can consume it without ever observing a partial file.

## Changed files

### New production code

- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerQueryModels.cs`
  - `PlayerQueryResult`, `PlayerNameQueryResult`, `PlayerCatalogExport`,
    `PlayerCatalogSummary`, `PlayerCatalogEntry`, `PlayerCatalogField`.
  - `PlayerCatalogExport` carries the schema version (`pes2021.players.v1`) and
    kind (`player_catalog`) plus the session, summary, players, diagnostics, and
    warnings.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerCatalog.cs`
  - `Pes2021PlayerCatalog` is a thread-safe in-memory holder of the latest
    discovery result. Producers call `Replace(...)`; consumers call `Snapshot()`
    or `Result`.
  - `Pes2021PlayerCatalogService` orchestrates anchor discovery + region scan +
    session cache and writes the result into the catalog.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerQueryService.cs`
  - `QueryByPlayerId(uint)` returns `Ambiguous = true` when duplicates exist; the
    caller must narrow by `(recordAddress, fingerprint)`.
  - `QueryByName(string, exactMatch)` returns empty results when no match exists.
  - `Snapshot()` exposes the full decoded list.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerCatalogExporter.cs`
  - `Build(result)` constructs the export payload from a discovery result.
  - The exporter preserves raw + display + transform + evidence status per
    field. It collapses warnings across players and computes the duplicate
    and unique counts in the summary.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerCatalogTests.cs` (6 tests)
  - Catalog `Replace` then `Snapshot` round-trips records.
  - Query service `QueryByPlayerId` returns a single record for an unambiguous ID.
  - Query service flags duplicates as `Ambiguous = true`.
  - Query service `QueryByName` supports exact and partial matches.
  - Exporter builds the `pes2021.players.v1` payload with the schema version
    and kind.
  - Exporter writes an atomic file via `Pes2021AtomicFileWriter`, parses the
    result, and asserts the schema version round-trips.

## Decisions

- **Atomic file write is reused.** The exporter does not reinvent the
  `.tmp → rename` dance; it delegates to the existing
  `Pes2021AtomicFileWriter` so the catalog export follows the same contract as
  the fixture export.
- **Ambiguity rule is enforced in code, not in JSON.** `QueryByPlayerId`
  returns `Ambiguous = true` for duplicates; the exporter preserves the full
  result list. Nothing picks a winner silently.
- **Context is `EDIT_BASE_CANDIDATE` for now.** Master League context tagging
  is future work (P6) — the catalog exposes the field, the value is honest.
- **Schema version is `pes2021.players.v1`.** This intentionally differs from
  the wire-contracts document (which described the read payloads) to keep
  catalog exports versioned separately.

## Limitations

- The catalog is process-local; there is no persistence layer.
- The query service does not yet support fuzzy or normalized name matching.
- Per-field evidence upgrades (P9) are not surfaced in the export beyond
  the per-field status; there is no field-level promotion log.

## Rollback

Reverting the four production files and the one test file restores the
repository to its pre-P4 state. No memory code was changed; only the
catalog, query, and export layers are touched.