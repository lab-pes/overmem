# P0 - Provenance, terminology, public contracts

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: docs and contract tests only. No production memory code changed.

## Goal

Turn the feasibility study into enforceable repository contracts without
implementing memory access. Establish the wire shapes, error codes, and source
manifest that downstream packages (P2-P10) will rely on.

## Changed files

### New docs

- `docs/pes2021/player-memory/source-manifest.json`
  - Records the source paths, capture date, and SHA-256 of every Lua script and
    the CT. Marks `runtimeDependency: false` on every entry. Declares
    `byteIdenticalCopy: true` for the CT copy at
    `files\PES 2021 - v21.1.0.CT`.
- `docs/pes2021/player-memory/wire-contracts.md`
  - Draft JSON shapes for `player_anchor`, `player_scan`, `player_snapshot`,
    `player_query`, `patch_plan`, `apply_result`, `rollback_result`.
  - Documents global rules: session identity must include process start time,
    every payload carries raw + display + evidenceStatus, ambiguous IDs are
    never silently resolved, no absolute live address appears as a default.
- `docs/pes2021/player-memory/error-codes.md`
  - Registers the 12 stable codes from `implementation-packages.md` lines 92-103
    with one-line semantics and conventions for adding new codes.
- `docs/pes2021/player-memory/wire-examples/*.json`
  - Eight example payloads used by the contract tests to verify round-trip and
    naming conventions. All use the placeholder address `0x0` so no live address
    leaks into tests.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerContractsTests.cs`
  - 8 tests verifying: anchor/scan/snapshot/query/patch-plan/apply-result/rollback-result
    contracts round-trip through `System.Text.Json`, distinguish raw vs display,
    surface evidence status, return ambiguous results with an `ambiguous: true`
    flag, expose `planId` and `rollbackId` hashes, and never embed
    `0x7FF4D908...` or live player IDs as constants.
- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021SourceManifestTests.cs`
  - 5 tests verifying: manifest schema version, every source has
    `runtimeDependency: false`, the CT path resolves to the working-tree copy
    and its SHA-256 matches the documented expected hash, no path embeds an
    absolute live address, and every constraint flag is enabled.

## Decisions

- **Naming convention:** `camelCase` properties and `SCREAMING_SNAKE_CASE`
  enums. Mirrors the existing fixture wire schema `pes2021.competition-fixtures.v1`.
- **Wire example addresses:** all examples use `0x0` as a placeholder. This
  keeps the tests free of session-local live addresses and demonstrates the
  "address is always session-local" rule from the wire-contracts document.
- **Source-manifest path resolution:** the manifest uses Windows-style paths
  (`C:\Users\Willian\...`) for external sources because those are the original
  capture paths. The Overmem CT path uses a repo-relative path
  (`files\PES 2021 - v21.1.0.CT`).

## Limitations

- No production memory code exists yet. The wire contracts are draft only.
- The wire examples intentionally use placeholder data. Real P2-P5 payloads
  will substitute live values per session.

## Rollback

Reverting the three docs and three tests restores the repository to its
pre-P0 state. No other file is touched.
