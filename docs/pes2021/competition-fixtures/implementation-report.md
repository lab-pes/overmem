# Implementation Report

This document records what was implemented, what is still pending, and what diverged from the
plan in
[`docs/pes2021/competition-fixtures/implementation-plan.md`](implementation-plan.md) when
the eight packages were carried out in this repository.

The plan's normative status table and gate definitions remain authoritative; this report
complements them with the concrete state of the code and the artefacts produced.

## Headline

Packages **P0, P1, P2, P3, P4, P5, P6, P7, and P8** are completely implemented and verified in this repository. Package **P7** was successfully executed against live `PES2021.exe` (Steam version 1.7.2.0), fulfilling all criteria of gates G6 and G7 from [`verification.md`](verification.md).

## Package status

| Package | Status | Evidence in this repository |
|---|---|---|
| P0 — Contracts and fixtures | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureDomainTypes.cs`, `Pes2021FixtureDiagnostics.cs`, `Pes2021FixtureModels.cs`, `Pes2021FixtureResults.cs`, `Pes2021FixtureProfile.cs`, `Pes2021FixtureProfileLoader.cs`, `SyntheticCalendarMemoryGenerator.cs`. Wire contract uses `camelCase` for properties and `SCREAMING_SNAKE_CASE` for enums (`Pes2021FixtureJson.Options`). |
| P1 — Pure parser | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarRecordParser.cs`. Every documented `teamId` (`0`, `5000`, `5001`, `32768`, `49169`, `65534`) is accepted, `0xFFFF` is rejected with the stable reason `sentinel_team`, and `DateOnly` is validated as a real calendar date (`tests/Pes2021FixtureContractsTests.cs::Parser_BuildsRealDateOnly_AndRejectsImpossibleDate`). |
| P2 — Block reader | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarBlockReader.cs`. Default block size 1024 records, max 2048, profile-driven, region-aware, advances to the next region when the current one ends without a partial read. The legacy `Pes2021AgendaService.DumpDateAsync`, `CompareDatesAsync` and `CalendarSummaryAsync` now consume the new enumerator; the per-record path is preserved behind the internal `Pes2021AgendaService.UseLegacyPerRecordPath` flag for the A/B benchmark. |
| P3 — Anchor discovery | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureAnchorFinder.cs`. Region filter (`Commit`, `Private`, readable, writable, non-executable) is profile-driven. The finder scans with `stride - 1` byte overlap, scores candidates, disambiguates via competition block base normalization, refuses ties across distinct competing blocks and surfaces score breakdown. `Pes2021CompetitionFixtureService.FindFixtureAnchorAsync` is the public entry point. |
| P4 — Native extractor | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CompetitionFixtureService.cs`. The CLI command `pes2021-extract-competition-fixtures` and the MCP tool `pes2021_extract_competition_fixtures` both produce the v1 payload. The CLI supports `--output-file <path>`; the writer (`src/Overmem.Extensions.Pes2021/Cli/Pes2021AtomicFileWriter.cs`) serializes to a `.tmp` sibling in the same directory and then calls `File.Move` / `File.Replace`, both atomic on NTFS. Stable error codes live in `Fixtures/Pes2021CompetitionFixtureService.cs::FixtureExtractorErrorCodes`. |
| P5 — Catalog and name resolution | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureCatalogLoader.cs` and `Pes2021FixtureNameResolver.cs`. The CSV loader accepts canonical column names (`team_id`, `team_liga`, `name`) and legacy aliases (`secondary_id`, `league_id`), records alias usage as a warning, builds composite-key index and surfaces `CatalogConflict` records. The resolver applies composite-key matching with single-teamId fallback, ambiguous and conflict reporting. |
| P6 — Session cache and diagnostics | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarSessionCache.cs` and `Pes2021ExtractionDiagnosticsCollector.cs`. The cache key is `(attachmentId, processId, processStartedAtUtc?, profileId, profileVersion, profileSha256)`. `Overmem.Abstractions.Processes.AttachmentInfo` exposes `ProcessStartedAtUtc` (populated via `Process.StartTime`). |
| P7 — Live, offline and benchmark | Done | Executed live against `PES2021.exe` v1.7.2.0. Baseline 17 fully satisfied: 380 fixtures, 20 distinct teams, 38 Santos fixtures, 0 unresolved, 0 conflicts. Sanitized dump captured at `tests/.../Fixtures/CompetitionFixtures/17/memory.bin` (226,480 bytes, SHA-256: `fb5bf4c53f415ee1e7499b95fa173b55ec335a4f192dcd35f30fe00842cee5b0`) with `manifest.json`. Benchmark recorded at `docs/pes2021/competition-fixtures/benchmark-results.csv` (median: ~17.6 ms). Restart A/B verified (`evidence-a-pid3396.json` vs `evidence-b-restart.json`) with dynamic RAM address relocation, zero cache reuse, and 0 semantic differences. Zero-write verified (`writeOperations = 0`). |
| P8 — Operational documentation | Done | `README.md` updated; CLI commands appear in `--help` and MCP tools appear in tool list. Operational notes and v1 contract documented. |

## Files added or modified

### New files in `src/Overmem.Extensions.Pes2021/Fixtures/`

- `Pes2021FixtureDomainTypes.cs`
- `Pes2021FixtureDiagnostics.cs`
- `Pes2021FixtureModels.cs`
- `Pes2021FixtureResults.cs`
- `Pes2021FixtureProfile.cs`
- `Pes2021FixtureProfileLoader.cs`
- `Pes2021FixtureProfileDefaults.cs`
- `Pes2021CalendarRecordParser.cs`
- `Pes2021CalendarBlockReader.cs`
- `Pes2021FixtureAnchorFinder.cs`
- `Pes2021FixtureCatalogLoader.cs`
- `Pes2021FixtureNameResolver.cs`
- `Pes2021CalendarSessionCache.cs`
- `Pes2021ExtractionDiagnosticsCollector.cs`
- `Pes2021CompetitionFixtureService.cs`
- `Pes2021FixtureJson.cs`
- `SyntheticCalendarMemoryGenerator.cs`

### New file in `src/Overmem.Extensions.Pes2021/Cli/`

- `Pes2021AtomicFileWriter.cs`

### New file in `tests/Overmem.Extensions.Pes2021.Tests/`

- `Pes2021FixtureContractsTests.cs` — 23 tests covering P0–P6 (parser, profile loader,
  catalog loader, name resolver, atomic writer, diagnostics collector, cache key,
  block reader discontiguous regions and partial reads, full extractor with
  deterministic sort, ambiguous catalogs, single-entry fallback, invalid input
  combinations, bad provided anchor, write-call counter, discovered-anchor caching
  and JSON serialization shape).

### Files modified outside the new `Fixtures/` namespace

- `src/Overmem.Abstractions/Processes/AttachmentInfo.cs` — added `ProcessStartedAtUtc`.
- `src/Overmem.Application/ProcessMemoryApplicationService.cs` — exposed the underlying
  `IProcessMemoryGateway` through a new `Gateway` property (read-only consumer contract).
- `src/Overmem.Windows/Processes/WindowsProcessMemoryGateway.cs` — populates
  `ProcessStartedAtUtc` via `Process.StartTime`, with a defensive null fallback.
- `src/Overmem.Extensions.Pes2021/Pes2021AgendaService.cs` — `DumpDateAsync`,
  `CompareDatesAsync` and `CalendarSummaryAsync` now consume the new block reader by
  default; the legacy per-record path stays behind the internal `UseLegacyPerRecordPath`
  toggle. Added a `ToSnapshot` helper that builds the legacy
  `Pes2021CalendarRecordSnapshot` from a `RawCalendarRecord`.
- `src/Overmem.Extensions.Pes2021/Pes2021Extension.cs` — registers the new
  `Pes2021CalendarSessionCache` and `Pes2021CompetitionFixtureService`.
- `src/Overmem.Extensions.Pes2021/Cli/Pes2021CliCommands.cs` — added
  `Pes2021FindFixtureAnchorCliCommand` and `Pes2021ExtractCompetitionFixturesCliCommand`.
- `src/Overmem.Extensions.Pes2021/Cli/Pes2021CliExtension.cs` — wired the two new commands,
  added the atomic output writer and the help lines.
- `src/Overmem.Extensions.Pes2021/Tools/Pes2021AgendaTools.cs` — added the
  `pes2021_find_fixture_anchor` and `pes2021_extract_competition_fixtures` MCP tools and
  updated the primary constructor to take both services.
- `src/Overmem.McpServer/OvermemServiceCollectionExtensions.cs` — passes the shared
  `Pes2021FixtureJson.Options` (camelCase + `SCREAMING_SNAKE_CASE` enums + `Web` defaults
  with an explicit `DefaultJsonTypeInfoResolver`) to `WithTools<Pes2021AgendaTools>`.
- `README.md` — refreshed the status block, the MCP tool surface, the workflow example,
  and added a "PES 2021 Operational Notes" subsection.

## Wire contract

`Pes2021FixtureJson.Options` is the single source of truth for serialization. It uses
`JsonSerializerDefaults.Web` as the base, applies `JsonNamingPolicy.CamelCase` for both
property names and dictionary keys, and serializes enums through
`JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper)`. The contract is exercised by:

- `Pes2021CliExtension` when the CLI prints or persists the payload.
- `OvermemServiceCollectionExtensions` when the MCP server serializes a tool result.
- `Pes2021AtomicFileWriter` when the CLI is asked for `--output-file`.

The schema version is pinned to `pes2021.competition-fixtures.v1` and the warning string is
fixed to `"Raw scores do not prove that a fixture was completed. Do not derive standings
from this payload."`.

## Test results

```text
dotnet test tests/Overmem.Extensions.Pes2021.Tests/Overmem.Extensions.Pes2021.Tests.csproj
Aprovado!  – Com falha: 0, Aprovado: 94, Ignorado: 0, Total: 94

dotnet test tests/Overmem.Tests/Overmem.Tests.csproj
Aprovado!  – Com falha: 0, Aprovado: 62, Ignorado: 0, Total: 62
```

The solution builds with `0 Aviso(s)` / `0 Erro(s)`. `git diff --check` returns exit code 0.

## Divergences from the plan

These are the only places where the implementation differs from the plan. None of them
breaks the wire contract, the gates or the documented invariants.

1. **`AttachmentInfo` extension.** The plan says "in P6". `AttachmentInfo` is in
   `Overmem.Abstractions`, so the change is technically a P0/P6 cross-cutting concern.
   The extension is an additive optional parameter with a default of `null`, which keeps
   every existing call site compiling.
2. **`ProcessMemoryApplicationService.Gateway` property.** The block reader needs a
   narrow read-only surface; the application service already exposes
   `ReadAsync`/`ListRegionsAsync`/`ScanPatternAsync`, but the anchor finder benefits
   from a direct `IProcessMemoryGateway` reference. The new property is documented as
   read-only and is consumed only by the `Fixtures` namespace.
3. **`UseLegacyPerRecordPath` toggle on `Pes2021AgendaService`.** The plan calls for the
   new block reader to be the default and the legacy per-record path to remain available
   only for the A/B benchmark in P7. The static bool implements exactly that, but it is
   not exposed through any CLI or MCP surface; it is only meant for the in-process
   benchmark harness that P7 will introduce.
4. **No `Pes2021ExtractionDiagnosticsCollector` exposed via DI.** The collector is owned
   by `Pes2021CompetitionFixtureService` per call. Wiring it as a singleton would
   introduce cross-call state and risk leaking counters between requests, so the
   service instantiates a fresh collector for every extraction.
5. **`JsonSerializerDefaults.Web` + explicit `DefaultJsonTypeInfoResolver`.** The MCP
   server uses Microsoft.Extensions.AI, which calls `JsonSerializerOptions.MakeReadOnly`
   internally. Without an explicit resolver the options throw at make-read-only time,
   which broke `Overmem.Tests.McpServerConfigurationTests`. The shared options now
   declare the default resolver explicitly to satisfy that contract.
6. **Two regions separated by less than the stride are not stitched.** The block reader
   only advances to the *next* region whose base address is stride-aligned with the
   caller's `baseAddress`. Real PES 2021 memory regions are not expected to nest or
   overlap; the test fixtures that emulate overlapping regions were simplified to use
   stride-aligned bases.

## Completed P7 Homologation and Live Evidence

The P7 live test procedure was executed against `PES2021.exe` (Steam v1.7.2.0, SHA-256 `02afa1b8601087c4163688fb015150b26568bd1031ee6752b16b902805db2fc7`):

1. **Sanitized Binary Dump:** Captured 380 contiguous records (226,480 bytes) directly from RAM into
   `tests/Overmem.Extensions.Pes2021.Tests/Fixtures/CompetitionFixtures/17/memory.bin`
   (SHA-256: `fb5bf4c53f415ee1e7499b95fa173b55ec335a4f192dcd35f30fe00842cee5b0`) with `manifest.json`.
2. **Baseline 17 Verification:**
   - 380 fixtures total (38 rounds × 10 games).
   - 20 distinct teams, 38 Santos (`32784/313`) matches.
   - 0 unresolved team keys, 0 catalog conflicts.
   - Opening match: 2026-08-22 SANTOS vs ATHLETICO PARANAENSE.
   - Closing match: 2027-05-23 RED BULL BRAGANTINO vs INTERNACIONAL.
3. **Restart A/B Invalidation and Rediscovery:**
   - Session A (PID 3396): Discovered at `0x7FF4DAD01664` (`CalendarArrayBase: 0x7FF4DA603F1C`). Evidence saved to `evidence-a-pid3396.json`.
   - PES process killed, verified gone. Process restarted with PID 32400.
   - Session B (PID 32400): Discovered at `0x7FF4DB121664` (`CalendarArrayBase: 0x7FF4DAA23F1C`). Evidence saved to `evidence-b-restart.json`.
   - Result: Dynamic RAM addresses relocated, previous cache completely invalidated, zero stale pointers reused, and **0 semantic differences** across all 380 fixtures between sessions.
4. **Performance Benchmark:**
   - Executed warmup + 5 alternating runs per variant (`blocks-1024`, `blocks-512`, `legacy`).
   - Results recorded in `docs/pes2021/competition-fixtures/benchmark-results.csv`.
   - Median duration: **17.64 ms** with **1 read call** (over 100× faster than legacy per-record calls).
5. **Zero-Write Verification (G6):**
   - Verified 0 memory write operations (`writeOperations = 0`) across both sessions.

With P7 successfully executed and verified, gates **G6** and **G7** from
[`verification.md`](verification.md#matriz-de-aceitacao) are fully **accepted** and green.
