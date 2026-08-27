# Overmem

Overmem is a Windows-only .NET 8 process memory platform focused on headless operation. The current repository scope is the backend surface only: shared contracts, application services, a Win32 provider, a one-shot CLI, and a local stdio MCP server. UI work is intentionally out of scope for now.

## Implementation Specifications

- [PES 2021 competition fixture extraction](docs/pes2021/competition-fixtures/README.md): self-contained requirements, contracts, memory profile, CLI/MCP surface, tests, evidence gates, examples, and phased implementation plan. Packages **P0–P6 and P8 are implemented in code**; only the live-process evidence gated by **P7** remains pending. See the plan and the [`Implementation Report`](docs/pes2021/competition-fixtures/implementation-report.md) for the current state.

The project already covers a meaningful subset of Cheat Engine style workflows for process attachment, region/module inspection, typed reads and writes, pointer resolution, pattern scanning, freezing, exact value search, memory tables, and host runtime diagnostics. It does not yet cover the full Cheat Engine feature set.

## Status At A Glance

Ready now:

- Process attach and detach by PID or process name. Attachments now carry `processStartedAtUtc` for the calendar session cache.
- Module listing and virtual memory region enumeration.
- Typed memory read and write for `Bytes`, `Int32`, `Int64`, `Float`, `Double`, `Utf8String`, and `Utf16String`.
- Pointer-chain resolution from an absolute base address.
- Pointer-chain resolution from a module-relative base address.
- Initial absolute pointer discovery with bounded depth and offset scanning.
- Pattern scanning with `??` wildcards.
- Freeze orchestration with automatic cleanup on detach.
- Exact value search sessions with `Exact`, `NotEqual`, `Changed`, `Unchanged`, `Increased`, `Decreased`, `IncreasedBy`, `DecreasedBy`, `ChangedBy`, and `Between` refinement.
- Unknown initial value search sessions: capture all aligned baseline values and narrow with `Changed`, `Unchanged`, `Increased`, `Decreased`, `Between`, etc.
- JSON-backed memory tables with on-demand refresh.
- Host runtime diagnostics for active attachments and recent operations.
- PES 2021 calendar fixture extraction: profile-driven contract types, pure parser, block reader (default 1024 records), region-scoped anchor finder, in-memory session cache keyed by `(attachmentId, processId, processStartedAtUtc?, profileId, profileVersion, profileSha256)`, team/competition catalog loader with composite-key resolution, atomic JSON output for Sider/Lua consumers. CLI: `pes2021-find-fixture-anchor` and `pes2021-extract-competition-fixtures`. MCP: `pes2021_find_fixture_anchor` and `pes2021_extract_competition_fixtures`. The wire payload is `pes2021.competition-fixtures.v1` (`status: FIXTURES_ONLY`, `camelCase` properties, `SCREAMING_SNAKE_CASE` enums). Live-process evidence (P7) is still pending.
- Headless execution through a local CLI.
- Host integration through a local stdio MCP server.

Partially ready:

- CLI value search supports exact one-shot scans only. Multi-step refinement is currently MCP-host only.
- Pointer discovery now supports optional base-module filtering and candidate revalidation, but pointer maps and large-scale ranking/revalidation workflows are not implemented yet.
- Value-search sessions are in-memory host state only. They are not persisted.
- Memory-table refresh is on demand only. There is no background refresh daemon.
- PES 2021 fixture extraction runs against the in-memory synthetic generator and a fake gateway in CI; live dumps from `PES2021.exe` and the cross-restart A/B benchmarks are still pending. The legacy `DumpDateAsync`/`CompareDatesAsync`/`CalendarSummaryAsync` paths now use the new block reader by default and retain the legacy per-record path only behind an internal `UseLegacyPerRecordPath` toggle reserved for the A/B benchmark in P7.

Not implemented yet:

- Any UI surface.
- Cheat Engine `.CT` import and export.
- Full pointer scanner workflows with pointer maps, ranking, and revalidation.
- Advanced value search modes such as grouped searches. Unknown initial value, ranges, deltas, and inequality are already supported.
- Disassembly, instruction-level inspection, breakpoints, tracing, or debugger features.
- Code patching, injection, hooks, speedhack, or instrumentation workflows.
- Lua runtime and compatibility execution.
- Managed-runtime providers for Mono, .NET object inspection, or Java.
- Plugin system or extension model.
- Remote/distributed execution model.
- Kernel or privileged providers.

## Solution Layout

- `src/Overmem.Abstractions`: contracts shared by every layer. `AttachmentInfo` now exposes `ProcessStartedAtUtc`.
- `src/Overmem.Application`: application-facing orchestration and validation. `ProcessMemoryApplicationService` exposes the underlying `IProcessMemoryGateway` for read-only consumers like the PES 2021 fixture reader.
- `src/Overmem.Runtime`: host-level runtime services such as attachment session tracking and recent-operation journals.
- `src/Overmem.Search`: exact value search sessions and refinement logic.
- `src/Overmem.Hosting`: shared dependency injection wiring for local hosts.
- `src/Overmem.Cli`: one-shot command-line host.
- `src/Overmem.Windows`: Win32-backed implementation of the process memory gateway.
- `src/Overmem.McpServer`: local stdio MCP host exposing the headless workflows.
- `src/Overmem.Extensions.Pes2021`: PES 2021 Master League agenda + fixtures. The new `Fixtures/` namespace contains the contract types, profile loader, pure parser, block reader, anchor finder, catalog loader, name resolver, session cache, diagnostics collector, and the orchestrating `Pes2021CompetitionFixtureService`.
- `tests/Overmem.TestTarget`: controlled process used by integration tests.
- `tests/Overmem.Tests`: unit and integration suite.
- `tests/Overmem.Extensions.Pes2021.Tests`: agenda service tests plus the new contract, parser, block-reader, anchor finder, catalog, cache, diagnostics and fixture service tests.

## Implemented Capability Surface

### Process and Runtime

- Attach to a process by PID or by name.
- Detach and release native handles.
- Track active attachments in long-lived hosts.
- Record recent host operations in a bounded in-memory journal.

### Memory Access

- List loaded modules.
- Enumerate virtual memory regions with readable, writable, and executable flags.
- Read typed values from absolute addresses.
- Write typed values to absolute addresses.
- Resolve pointer chains from absolute bases.
- Resolve pointer chains from module-relative bases.
- Scan readable regions for hex signatures with optional `??` wildcards.

### Pointer Discovery

- Discover absolute pointer candidates that can resolve to a known target address.
- Bound discovery by maximum depth.
- Bound discovery by maximum absolute offset per dereference.
- Annotate candidates with module-relative base information when the candidate base falls inside a loaded module.
- Optionally filter candidates by a required base module name.
- Optionally revalidate candidates by resolving the discovered pointer chain before returning it.
- Rank candidates by a heuristic score: revalidated > module-rooted > shorter chain > smaller offsets > aligned offsets.

### Freeze Workflows

- Freeze a value at an absolute address.
- Freeze a value resolved from an absolute-base pointer chain.
- Freeze a value resolved from a module-relative pointer chain.
- List active freezes.
- Cancel freezes explicitly.
- Stop freezes automatically when the owning attachment is detached.

### Value Search


### Pattern Format

- Exact pattern example: `DE AD BE EF 44 99`
- Wildcard pattern example: `DE AD ?? EF 44 99`

### Memory Table Document Format

Memory tables are stored as versioned JSON documents. The current schema version is `1.0`.

Main fields:

- `SchemaVersion`
- `Name`
- `Entries`

Each entry supports:

- `EntryId`
- `Name`
- `ValueKind`
- `AddressKind`: `Absolute`, `Pointer`, or `ModulePointer`
- `AbsoluteAddress`
- `BaseAddress`
- `ModuleName`
- `BaseOffset`
- `Offsets`
- `Size`
- `RefreshIntervalMs`
- `Freeze`: `{ "Value": "...", "IntervalMs": 25 }`

Example:

```json
{
        "SchemaVersion": "1.0",
        "Name": "Player Table",
        "Entries": [
                {
                        "EntryId": "health",
                        "Name": "Player Health",
                        "ValueKind": "Int32",
                        "AddressKind": "ModulePointer",
                        "ModuleName": "game.exe",
                        "BaseOffset": 1193046,
                        "Offsets": [16, 32, 8],
                        "Size": 0,
                        "RefreshIntervalMs": null,
                        "Freeze": {
                                "Value": "999",
                                "IntervalMs": 25
                        }
                }
        ]
}
```

## MCP Integration Guide

### Running The MCP Host Manually

Start the stdio MCP server:

```powershell
dotnet run --project src/Overmem.McpServer
```

The server uses stdio transport and keeps runtime state in process memory, including active attachments, freeze state, operation logs, and value-search sessions.

### Adding Overmem To VS Code MCP

This workspace already uses `.vscode/mcp.json` for MCP configuration. Add an `overmem` server entry alongside the existing ones:

```json
{
        "servers": {
                "overmem": {
                        "type": "stdio",
                        "command": "dotnet",
                        "args": [
                                "run",
                                "--project",
                                "D:\\git\\overmem\\src\\Overmem.McpServer"
                        ]
                }
        }
}
```

If you prefer a faster startup after building once, point to the compiled server executable instead of `dotnet run`.

### Integrating cheat-engine-mcp in this workspace

This workspace keeps a dedicated Cheat Engine bridge profile in `.vscode/cheat-engine-mcp.config.json`.

- `.vscode/mcp.json` points to `D:\git\official-cheat-engine\mcp\cheat-engine-mcp.ps1`.
- The `cheat-engine` entry sets `CE_MCP_CONFIG` to the workspace-specific config.
- Runtime logs are written to `%LOCALAPPDATA%\Overmem\logs\cheat-engine-mcp.log`.
- Log rotation is enabled by config (`maxLogSizeBytes`, `maxLogFiles`).

### MCP Tool Surface

Process tools:

- `attach_process`
- `detach_process`
- `list_modules`

Memory tools:

- `list_regions`
- `discover_pointers`
- `resolve_pointer`
- `resolve_module_pointer`
- `scan_pattern`
- `read_value`
- `write_value`

Freeze tools:

- `freeze_value_at_address`
- `freeze_value_at_pointer`
- `freeze_value_at_module_pointer`
- `unfreeze_value`
- `list_frozen_values`

Runtime tools:

- `list_active_attachments`
- `list_recent_operations`

Value-search tools:

- `start_value_search`
- `start_unknown_value_search`
- `refine_value_search`
- `list_value_search_sessions`
- `list_value_search_results`
- `close_value_search_session`

Memory-table tools:

- `load_memory_table`
- `save_memory_table`
- `refresh_memory_table`

PES 2021 fixture tools (read-only, see [`docs/pes2021/competition-fixtures`](docs/pes2021/competition-fixtures/)):

- `pes2021_find_fixture_anchor` — discover the calendar anchor for a `(competitionId, teamId[, teamLiga])` pair.
- `pes2021_extract_competition_fixtures` — produce a `pes2021.competition-fixtures.v1` FIXTURES_ONLY payload for one competition.

### MCP Workflow Example

Typical workflow for a long-lived agent:

1. Call `attach_process` with `processId` or `processName`.
2. Use `list_modules`, `list_regions`, `read_value`, `resolve_pointer`, `discover_pointers`, or `scan_pattern` to discover targets.
3. If needed, start a long-lived value-search session with `start_value_search` and refine it with `refine_value_search` using `Exact`, `NotEqual`, `Changed`, `Unchanged`, `Increased`, `Decreased`, `IncreasedBy`, `DecreasedBy`, `ChangedBy`, or `Between` as appropriate. `Between` requires both `value` (lower bound) and `secondaryValue` (upper bound).
4. If needed, freeze a resolved address with one of the `freeze_value_*` tools.
5. Inspect host state with `list_active_attachments`, `list_recent_operations`, `list_frozen_values`, or `list_value_search_sessions`.
6. Persist or refresh tables with `load_memory_table`, `save_memory_table`, and `refresh_memory_table`.
7. (Optional) Run the PES 2021 fixture flow with `pes2021_find_fixture_anchor` followed by `pes2021_extract_competition_fixtures`. The result is a read-only JSON payload (`status: FIXTURES_ONLY`) that downstream Lua/Sider modules can consume.
8. End the session with `detach_process` and optionally `close_value_search_session`.

### MCP Operational Notes

- Long-lived workflows are best done through MCP because the host process retains in-memory state.
- Value-search sessions are lost when the MCP host stops.
- Active freezes are also host-local runtime state.
- The PES 2021 calendar session cache lives only in the host process; it is invalidated on detach and rebuilt on the next call.
- The server is local and Windows-only.

## Constraints And Non-Goals

- Windows only.
- User mode only.
- No driver or kernel support.
- No anti-cheat bypass or protected-process bypass.
- No code injection, hook engine, or patching workflows yet.
- No UI in the current repository scope.

## Roadmap Direction

The next backend milestones with the highest leverage are:

- richer value-search modes and persisted search artifacts;
- pointer discovery and pointer-map workflows;
- persistent CLI host mode for long-lived operations;
- durable session and artifact services;
- Lua compatibility and script-host support;
- debug, disassembly, and instrumentation primitives.

The annual inventory also now exposes an explicit `dayRole` field to avoid mixing "real calendar day" with "annual semantic event":

- `calendar-match-day`
  - valid materialized day in the main calendar with a real fixture
  - example: `2026-12-01`
- `semantic-event-day`
  - visible annual/admin day whose primary meaning is the semantic event itself
  - example: `2026-12-02` as the world best player award announcement
- `mixed-match-and-semantic-day`
  - both a real fixture and a semantic annual event share the same date
- `calendar-marker-day`
  - secondary/header-backed marker day with no closed semantic meaning yet

This keeps `2026-12-01` as a valid league-match day without allowing it to inherit the award semantics from `2026-12-02`.

#### Current Runtime Event Taxonomy

The `pes2021_classify_runtime_day_variant` command is intentionally heuristic, but it already captures a useful business taxonomy for stop-days that share the same semantic event while living in different schedule contexts:

- `placeholder_special_runtime`
  - example: `2026-09-22`
  - business meaning: special stop-day with no main-array fixture on the day itself and a distinctive runtime family, currently the cleanest fingerprint for the national-team callup announcement case
- `placeholder_organized_runtime`
  - example: `2026-10-30`
  - business meaning: stop-day with a small bridge payload toward the next day and a simple organized runtime family
- `no_games_runtime`
  - example: `2026-03-16`
  - business meaning: stop-day with no surrounding matches and a single clean runtime family
- `agenda_defined_organized_runtime`
  - example: `2026-06-23`
  - business meaning: stop-day whose following schedule is already well-defined and still materializes in a compact organized runtime family
- `agenda_defined_noisy_runtime`
  - examples: `2026-05-26`, `2026-08-21`
  - business meaning: stop-day whose following schedule is already loaded in a much heavier runtime context, making the event harder to isolate from the surrounding agenda noise

This taxonomy should be treated as a working operational model, not as the final semantic truth of the engine. It is meant to guide further probing of other stop-day types such as transfer-window milestones, negotiation deadlines, or competition phase changes.

The same annual blind-audit pass that isolated the national-team callup dates also exposed a smaller set of administrative boundary dates whose header-event mix does not look like ordinary fixture bridging. In the current live save, the following transfer-window boundaries are confirmed:

- `2026-01-01` transfer-window start
- `2026-01-31` transfer-window end
- `2026-07-01` transfer-window start
- `2026-08-31` transfer-window end

Additional transfer-window prelude candidates that still need semantic confirmation cluster around:

- `2026-01-30`
- `2026-08-30`

These dates are useful blind-audit anchors for future work on transfer-window preludes and other non-match stop-days. The dates listed above as confirmed anchors should be treated as semantic labels already validated in the current save context.

The current classifier also exposes working candidate labels for the still-unconfirmed transfer-window preludes:

- `transfer_window_boundary_prelude_candidate`
  - examples: `2026-01-30`, `2026-08-30`

These candidate labels are not final semantic truth yet. They exist to make annual blind-audits and future grouping work repeatable instead of leaving the remaining dates as undifferentiated `unknown_event` results.

The current annual backlog is no longer a flat `unknown_event` bucket. The recurring business-facing families already visible in the 2026 inventory are:

- `next_day_schedule_bridge_candidate`
  - day has no own fixture, but the secondary payload clearly bridges into a populated `d+1` schedule
- `placeholder_bridge_candidate`
  - same bridging idea, but with `0x003F` placeholder markers present
- `standalone_secondary_payload_candidate`
  - no surrounding main-array matches, but the day still carries a local secondary payload
- `post_match_followup_payload_candidate`
  - small secondary payload that appears immediately after a prior-day match block
- `rare_header_marker_candidate`
  - days with unusual header signatures that likely correspond to more specific semantic event types still waiting to be named

This means the next blind-audit cycle should focus less on "does this date matter at all?" and more on "which repeated business family does this date belong to?".

Suggested flow:

1. Attach to `PES2021.exe` with the existing `attach_process` tool.
2. Run `pes2021_agenda_guide` to confirm the calendar references and known offsets.
3. Run `pes2021_find_calendar_base` if you need the calendar anchor resolved from a visible date or season anchor.
4. Use `pes2021_dump_calendar_date` or `pes2021_calendar_summary` to inspect fixtures and schedule-like agenda data. Both now go through the new block reader (default 1024 records per call).
5. Use `pes2021_find_secondary_calendar_base_by_date` when a date exists outside the main array and you need to resolve the real `secondary_calendar` base from a raw day-header hit.
6. Use `pes2021_dump_secondary_calendar_day` to inspect the resolved `DayEntry`, including header events, item indices, and `count`.
7. Use `pes2021_scan_runtime_day_index_clusters` when the UI still shows a visible line or stop-day that is not explained by the save-side structures alone.
8. Use `pes2021_dump_runtime_day_payload_family` when the generic runtime scan is too noisy and you need the focused `472`/`528` style heap-family dump plus preview record decode.
9. Use `pes2021_compare_runtime_day_payload_family` to isolate IDs that are unique to the current day compared with the previous and next days.
10. Use `pes2021_dump_runtime_day_payload_cluster_detail` when one runtime cluster looks promising and you need the local `Int32` windows around each hit.
11. Use `pes2021_analyze_runtime_day_payload_cluster` when you want a reusable signature for that cluster type across other dates.
12. Use `pes2021_classify_runtime_day_variant` when you want a provisional subtype label for the selected day, derived from the secondary-calendar day shape plus the focused runtime-family scan. Treat this as heuristic output and verify against the returned reasons.
13. Use the lower-level secondary-calendar candidate tools when the date-based resolution is still not enough and you need to probe broader agenda structures.
14. Use `pes2021_inventory_annual_events` when you want the year-level list of special days first and only then decide which ones deserve deeper runtime analysis.
15. Use `pes2021_find_fixture_anchor` to locate the calendar anchor without absolute addresses. The call requires `--competition-id` and `--team-id`; `--team-liga` and a profile path are optional.
16. Use `pes2021_extract_competition_fixtures` to produce a `pes2021.competition-fixtures.v1` payload (`status: FIXTURES_ONLY`). Pass `--output-file <path>` to persist the payload atomically; the file is written via `.tmp` + rename so external consumers (Sider/Lua) never observe a partial file.

#### PES 2021 Operational Notes

- The block reader and anchor finder are read-only; they never call `WriteAsync`. The test fake gateway throws on any write attempt as an architectural guard for G6.
- The session cache is keyed by `(attachmentId, processId, processStartedAtUtc?, profileId, profileVersion, profileSha256)`. A PES restart that changes the process start time or the PID invalidates the entry automatically.
- `pes2021-extract-competition-fixtures` accepts at most one of `--calendar-base-address` and `--competition-block-base-address`. Combining both is rejected with `PES2021_INPUT_INVALID`.
- `teamId` and `teamLiga` are accepted as any `u16` except `0xFFFF`; the legacy `IsStrongRecord` ceiling of 5000 is gone.
- When the MCP Server (`Overmem.McpServer.exe`) is running, `dotnet build Overmem.slnx` may fail with `MSB3026` (file locked). Either stop the running MCP host or use the focused command `dotnet test tests/Overmem.Extensions.Pes2021.Tests/Overmem.Extensions.Pes2021.Tests.csproj --no-build` for the test cycle.
- The live-process evidence gated by P7 (baseline 17 with 380 games, restart A/B, second non-Brazilian competition, benchmark legacy/512/1024) is still pending. Until P7 closes, the gates G6 and G7 from [`verification.md`](docs/pes2021/competition-fixtures/verification.md) are not satisfied.

#### Next Analysis Directions


- Windows only.
- User mode only.
- No driver or kernel support.
- No anti-cheat bypass or protected-process bypass.
- No code injection, hook engine, or patching workflows yet.
- No UI in the current repository scope.

## Roadmap Direction

The next backend milestones with the highest leverage are:

- richer value-search modes and persisted search artifacts;
- pointer discovery and pointer-map workflows;
- persistent CLI host mode for long-lived operations;
- durable session and artifact services;
- Lua compatibility and script-host support;
- debug, disassembly, and instrumentation primitives.
