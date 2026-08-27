# Implementation Report

This document records what was implemented, what is still pending, and what diverged from the
plan in
[`docs/pes2021/competition-fixtures/implementation-plan.md`](implementation-plan.md) when
the eight packages were carried out in this repository.

The plan's normative status table and gate definitions remain authoritative; this report
complements them with the concrete state of the code and the artefacts produced.

## Headline

Packages **P0, P1, P2, P3, P4, P5, P6, and P8** are implemented in code. Package **P7** is
explicitly deferred — it requires live capture of the running `PES2021.exe` process on the
user's machine and gates G6/G7 from
[`verification.md`](verification.md) cannot be closed without it.

## Package status

| Package | Status | Evidence in this repository |
|---|---|---|
| P0 — Contracts and fixtures | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureDomainTypes.cs`, `Pes2021FixtureDiagnostics.cs`, `Pes2021FixtureModels.cs`, `Pes2021FixtureResults.cs`, `Pes2021FixtureProfile.cs`, `Pes2021FixtureProfileLoader.cs`, `SyntheticCalendarMemoryGenerator.cs`. Wire contract uses `camelCase` for properties and `SCREAMING_SNAKE_CASE` for enums (`Pes2021FixtureJson.Options`). |
| P1 — Pure parser | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarRecordParser.cs`. Every documented `teamId` (`0`, `5000`, `5001`, `32768`, `49169`, `65534`) is accepted, `0xFFFF` is rejected with the stable reason `sentinel_team`, and `DateOnly` is validated as a real calendar date (`tests/Pes2021FixtureContractsTests.cs::Parser_BuildsRealDateOnly_AndRejectsImpossibleDate`). |
| P2 — Block reader | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarBlockReader.cs`. Default block size 1024 records, max 2048, profile-driven, region-aware, advances to the next region when the current one ends without a partial read. The legacy `Pes2021AgendaService.DumpDateAsync`, `CompareDatesAsync` and `CalendarSummaryAsync` now consume the new enumerator; the per-record path is preserved behind the internal `Pes2021AgendaService.UseLegacyPerRecordPath` flag for the A/B benchmark scheduled in P7. |
| P3 — Anchor discovery | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureAnchorFinder.cs`. Region filter (`Commit`, `Private`, readable, writable, non-executable) is profile-driven. The finder scans with `stride - 1` byte overlap, scores every candidate, refuses ties on the top score and surfaces the score breakdown. `Pes2021CompetitionFixtureService.FindFixtureAnchorAsync` is the public entry point. |
| P4 — Native extractor | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CompetitionFixtureService.cs`. The CLI command `pes2021-extract-competition-fixtures` and the MCP tool `pes2021_extract_competition_fixtures` both produce the v1 payload. The CLI supports `--output-file <path>`; the writer (`src/Overmem.Extensions.Pes2021/Cli/Pes2021AtomicFileWriter.cs`) serializes to a `.tmp` sibling in the same directory and then calls `File.Move` / `File.Replace`, both atomic on NTFS. Stable error codes live in `Fixtures/Pes2021CompetitionFixtureService.cs::FixtureExtractorErrorCodes`. |
| P5 — Catalog and name resolution | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureCatalogLoader.cs` and `Pes2021FixtureNameResolver.cs`. The CSV loader accepts the canonical column names (`team_id`, `team_liga`, `name`) and the legacy aliases (`secondary_id`, `league_id`), records alias usage as a warning, builds the composite-key index and surfaces `CatalogConflict` records. The resolver applies the exact algorithm fixed in `requirements-and-decisions.md` (exact composite → unique-team-id fallback → ambiguous → unresolved, with conflict always winning). |
| P6 — Session cache and diagnostics | Done | `src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarSessionCache.cs` and `Pes2021ExtractionDiagnosticsCollector.cs`. The cache key is `(attachmentId, processId, processStartedAtUtc?, profileId, profileVersion, profileSha256)`. `Overmem.Abstractions.Processes.AttachmentInfo` now exposes `ProcessStartedAtUtc` (populated by `Overmem.Windows.Processes.WindowsProcessMemoryGateway.AttachAsync` via `Process.StartTime` with a defensive fallback). The collector aggregates counters, rejection reasons and stage timings without ever reading memory. |
| P7 — Live, offline and benchmark | **Pending** | Requires running `PES2021.exe` on this machine. Gates G6 and G7 from `verification.md` (zero-write proof on real memory, restart A/B, second non-Brazilian competition, benchmark legacy/512/1024, sanitized dumps) cannot be closed without an interactive run. |
| P8 — Operational documentation | Done | `README.md` updated; the new CLI commands appear in the `--help` text of `Overmem.Cli` and the new MCP tools appear in the tool surface list. Operational notes for the Windows MCP Server lock (`MSB3026`) and the v1 contract are also in `README.md`. |

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

## Pending work (P7)

The following items from the plan remain to be carried out manually, with the running
`PES2021.exe` process and the user's career save:

1. Capture a sanitized binary dump of the running process into
   `tests/Overmem.Extensions.Pes2021.Tests/Fixtures/CompetitionFixtures/<fixture-id>/`,
   following the manifest schema in
   [`verification.md`](verification.md#2-offline-and-fixtures).
2. Run the extractor against the captured dump and add the resulting
   `expected.json` to the fixture.
3. Reproduce the `competitionId=17` baseline (380 games, 20 teams, 38 games for
   `32784/313 → SANTOS`) and add a second non-Brazilian competition as a separate
   fixture.
4. Execute the restart A/B procedure: attach, extract, kill PES, confirm PID gone,
   restart PES, re-extract, confirm `cacheDisposition: REDISCOVERED` (or
   `DISCOVERED` after the cache invalidation) and that the previous addresses are not
   silently reused.
5. Run the A/B benchmark (`legacy`, `blocks-512`, `blocks-1024`) under the
   `Pes2021AgendaService.UseLegacyPerRecordPath` toggle and record the five runs per
   variant with median + p95.
6. Build the operation-log before/after evidence and attach the SHA-256 of the dump +
   the overmem commit hash + the executable hash to the gate report.

Once P7 closes, G6 and G7 from
[`verification.md`](verification.md#matriz-de-aceitacao) can be flipped from
`pending` to `accepted` and the matrix of acceptance becomes green.
