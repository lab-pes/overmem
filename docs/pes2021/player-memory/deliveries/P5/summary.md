# P5 - CLI and MCP surface for player-memory (read-only)

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: registration, CLI commands, MCP tools, and tests.

## Goal

Expose the catalog, query, and anchor services through the existing CLI surface and
the MCP tool catalog. None of the new commands write to memory.

## Changed files

### New production code

- `src/Overmem.Extensions.Pes2021/Tools/Pes2021PlayerTools.cs`
  - MCP tools: `pes2021_find_player_anchor`, `pes2021_scan_players`,
    `pes2021_query_player`.
- `src/Overmem.Abstractions/Cli/CliOptionParser.cs`
  - Added `ParseUInt32` helper for the player-id options.
- `src/Overmem.Extensions.Pes2021/Cli/Pes2021CliCommands.cs`
  - New CLI records: `Pes2021FindPlayerAnchorCliCommand`,
    `Pes2021ScanPlayersCliCommand`, `Pes2021QueryPlayerCliCommand`,
    `Pes2021ExportPlayerCatalogCliCommand`.
- `src/Overmem.Extensions.Pes2021/Cli/Pes2021CliExtension.cs`
  - Parser routes for the new commands.
  - Dispatch handlers call `Pes2021PlayerAnchorFinder.FindAsync`,
    `Pes2021PlayerCatalogService.RefreshAsync`, and
    `Pes2021PlayerQueryService.QueryByPlayerId`.
  - `ExecutePlayerAttachmentAsync` helper attaches, executes, detaches, and
    clears the catalog on exit.
  - Help lines added for every new command.
- `src/Overmem.Extensions.Pes2021/Pes2021Extension.cs`
  - Registers `Pes2021PlayerSessionCache`, `Pes2021PlayerAnchorFinder`,
    `Pes2021PlayerRegionScanner`, `Pes2021PlayerCatalog`,
    `Pes2021PlayerCatalogService`, and `Pes2021PlayerQueryService`.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerCliSurfaceTests.cs` (4 tests)
  - End-to-end discovery + query round-trips through the catalog service.
  - CLI parser routes all four player-memory commands.
  - CLI parser rejects unknown commands.
  - End-to-end export writes the atomic JSON file.

## Decisions

- **CLI dispatches the same `Pes2021PlayerCatalogService`** that the MCP tools
  consume, so a single source of truth stays in the DI container.
- **`ExecutePlayerAttachmentAsync` always clears the catalog on detach** so a
  reused attachment cannot leak data from a previous session.
- **No write command is exposed.** The catalog export is read-only data; the
  patch-plan / apply / rollback commands are deliberately deferred to P7.

## Limitations

- The MCP tools accept a control player ID but do not yet propagate the actual
  `ProcessInstanceIdentity` from `AttachmentInfo`; they synthesize a placeholder.
  A future package will thread the real process identity.
- The query command returns the catalog result without a process attach, so it
  works only when the catalog has been populated by an earlier scan in the same
  process.

## Rollback

Reverting the five production files and the one test file restores the
repository to its pre-P5 state.