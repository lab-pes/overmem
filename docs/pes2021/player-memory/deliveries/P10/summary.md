# PES 2021 player-memory — final delivery index

Date: 2026-08-31
Status: P0–P9 accepted. P6 (live read validation against the running PES 2021
process) and P8 (single-player write pilot for `marketValue`) are deferred
until explicit authorization and a backup of the save are provided.

## Roster

| Package | Goal | Status |
|---|---|---|
| P0 | Provenance + wire contracts + error codes | accepted |
| P1 | Versioned player-memory profile (`pes2021.player-record.v1`) | accepted |
| P2 | Pure parser, validator, and bitfield codec | accepted |
| P3 | Anchor finder, region scanner, session cache (read-only) | accepted |
| P4 | Catalog, query service, atomic JSON export (`pes2021.players.v1`) | accepted |
| P5 | CLI commands and MCP tools (read-only) | accepted |
| P6 | Live read validation against the running PES 2021 process | deferred |
| P7 | Transaction core restricted to `Overmem.TestTarget` | accepted |
| P8 | Single-player write pilot for `marketValue` | deferred |
| P9 | Per-field write policy + evidence gating | accepted |
| P10 | End-to-end documentation and hardening | accepted |

## Build + test verification

- `dotnet build Overmem.slnx` → 0 warnings, 0 errors.
- `dotnet test Overmem.slnx` → 320 PES 2021 tests + 62 Overmem tests = 382 tests,
  all green. The 78 player-memory tests added by P0–P9 are a subset of the
  320 PES 2021 tests.

## What the new player-memory stack can do

- **Discover** the EDIT-base arena of a running PES 2021 process without
  injection, hooks, or Lua.
- **Decode** every structurally valid 380-byte player record, preserving the
  raw bytes and SHA-256.
- **Validate** records with cheap checks first and score-based reasons; no
  silent boolean.
- **Catalog** decoded records in-process; query by ID or name; export
  atomically to JSON.
- **Plan / apply / rollback** writes against `Overmem.TestTarget` (and only
  against `Overmem.TestTarget` by default).
- **Refuse** any write that is not confirmed, not in the right context, not
  authorized, or against an unknown process.

## What the stack still refuses to do

- **Write to a real PES 2021 process.** The default transaction allowlist
  excludes PES 2021; an explicit override plus authorization token are
  required.
- **Run a live, in-process pilot.** P6 and P8 require an explicit go-ahead
  plus a backup of the save with SHA-256.
- **Auto-promote any field to `Confirmed`.** Promotion is gated by per-field
  evidence and only happens after paired before/after data is gathered.

## Files of interest

- `docs/pes2021/player-memory/feasibility-study.md` — original study.
- `docs/pes2021/player-memory/implementation-packages.md` — package list.
- `docs/pes2021/player-memory/edit-first-decision.md` — decision to map EDIT
  first, ML only after the EDIT gate.
- `docs/pes2021/player-memory/wire-contracts.md` — read payload shapes.
- `docs/pes2021/player-memory/error-codes.md` — stable error codes.
- `docs/pes2021/player-memory/source-manifest.json` — provenance and SHA-256
  of every external source.
- `files/pes2021/player-memory/pes2021-player-record-v1.json` — the
  versioned player-record profile.
- `docs/pes2021/player-memory/deliveries/P0`–`P10` — one folder per package
  with `summary.md`, `commands.md`, `evidence.json`, and `review-request.md`.

## How to enable P6 / P8

1. Run `dotnet build Overmem.slnx` and `dotnet test Overmem.slnx --no-build`
   to confirm the current state still passes.
2. Provide:
   - a SHA-256 of the PES 2021 save directory;
   - an explicit, time-bounded authorization token for the fields in scope;
   - a confirmation that the session is `MASTER_LEAGUE_CONFIRMED` (not just
     `EDIT_BASE_CANDIDATE`).
3. Hand the token and the expected profile id/version to
   `Pes2021PlayerTransactionCore.ApplyWithPolicyAsync`. The default allowlist
   must be overridden via the constructor to include `PES2021`.
4. Run with `dryRun: true` first; review the expected bytes; then enable the
   real apply; verify the reread; roll back via the artifact.

Until those four steps happen, the player-memory stack is read-only against
the real PES 2021 process and writes only happen in `Overmem.TestTarget`.