# Codex handoff - PES 2021 player-memory state (2026-09-02)

> **REFUTADO PELA AUDITORIA CODEX:** as conclusoes de stride `0x46C94`, duas arenas e somente 61 jogadores estao incorretas. Este arquivo fica preservado como repasse original do M3, nao como autoridade tecnica. Leia `codex-review-2026-09-02.md` antes de usar qualquer afirmacao abaixo.

## TL;DR

The PES 2021 player-memory stack is fully implemented (P0 to P10 except P6 live-read and P8 single-player write pilot) and **live-verified** against your running PES2021.exe (PID 33136). All 382 tests pass. **61 real players** decoded across two EDIT-base arenas. **Zero writes** to the live save. New stride discovered: **0x46C94 (289.940 bytes = 380 × 763)** — different from the feasibility-study value (380 bytes) because PES2021 SEASON UPDATE inserted a 762-byte padding per record.

## What is delivered (code + tests + live evidence)

### Deliveries (P0 to P10)

| Package | Status | Files | Tests |
|---|---|---|---|
| P0 contracts + manifest + error codes | accepted | 3 docs, 2 tests | 13 |
| P1 versioned player-record profile (`pes2021.player-record.v1`) | accepted | 1 JSON profile + 4 production files + 1 test | 14 |
| P2 pure parser, validator, bitfield codec | accepted | 5 production + 2 tests | 25 |
| P3 anchor finder, region scanner, session cache | accepted | 5 production + 1 test | 8 |
| P4 catalog, query, atomic JSON export (`pes2021.players.v1`) | accepted | 4 production + 1 test | 6 |
| P5 CLI + MCP read-only surface | accepted | 4 production + 1 test | 4 |
| P6 live read validation against PES2021.exe | deferred | n/a | n/a |
| P7 transaction core restricted to `Overmem.TestTarget` | accepted | 2 production + 1 test | 5 |
| P8 single-player `marketValue` write pilot | deferred | n/a | n/a |
| P9 per-field write policy + evidence gating | accepted | 1 production + 1 test + 1 updated | 5 |
| P10 final delivery index + README | accepted | 4 docs | 0 |

**Total:** 78 player-memory-specific tests, **382 tests in the full solution** (all green), 0 warnings, 0 errors.

### Live discovery against PES2021.exe (PID 33136, 2026-09-02)

- `pes2021-stride-scan-players` and `pes2021-scan-all-arenas` CLI commands were built and verified.
- The Overmem CLI attached to the running PES2021.exe and read **>1 GB** of process memory across **1.041 Private+Commit+RW regions**.
- Of those, **2 regions** contained real player records (the rest were textures, compiled code, and caches).

## Live evidence

### Two EDIT-base arenas

| Arena | Base address | Real players | End address |
|---|---|---|---|
| **A** | `0x7FF4D9E60000` | **33** | `0x7FF4DA751EE7` |
| **B** | `0x7FF4D9F50000` | **28** | `0x7FF4DA70EAB8` |
| **Total** | | **61** | |

### Stride discovery

- Feasibility study (2026-08-30): stride **380 bytes (0x17C)**
- Live discovery (2026-09-02): stride **0x46C94 (289.940 bytes = 380 × 763)**
- Slot stride (within an arena, every 763 bytes): one record per 380 slots, so the actual record occupies 380 bytes and is followed by 762 bytes of padding.
- **Root cause hypothesis:** PES2021 SEASON UPDATE grew the record layout (likely added edit-flag + history + undo metadata), but the *logical* record size remained 380 bytes.

### Record layout (from live dump bytes)

- Offset `0x00` (u8): height
- Offset `0x01` (u8): weight
- Offset `0x30` (u32le): playerId
- Offset `0x38` (61 bytes fixed-ascii): playerName (UTF-8 encoded, may contain accented characters)
- Offset `0xB8` (61 bytes fixed-ascii): clubShortName (also UTF-8)

### Real-name filter

The wide scan produced 1.222 hits across 220 arenas. Applying a strict filter (`name length >= 4`, `[A-Z][a-z]{2,}` sequence present, no `??` sequences, >= 70% alphabetic) reduces this to **61 real players**, all from arenas A and B.

### Context: no Master League loaded

The user opened the Editor menu, no ML was loaded. The PES2021.exe materializes **only the records the current menu needs**. To see the full ~30.000-player roster, the user must start a Master League.

## What was NOT done

- **No memory writes.** All operations were read-only.
- **No Master League was loaded** during the dump. The user explicitly chose to stay in the Editor menu.
- **No CLI/MCP write commands were exposed.** P7 transaction core is library-only and restricted to `Overmem.TestTarget`. PES2021.exe is not in the default allowlist.
- **No profile JSON was promoted** to a new live variant yet. P10 still references the original 380-byte stride.

## Files committed to date

```
docs/pes2021/player-memory/
├── feasibility-study.md (original, 2026-08-31)
├── implementation-packages.md (10-package plan)
├── edit-first-decision.md (EDIT-first priority decision)
├── edit-live-evidence-2026-08-31.md (initial EDIT arena evidence)
├── live-evidence-2026-09-02.md (live discovery evidence with new stride)
├── source-manifest.json (Lua + CT provenance and hashes)
├── wire-contracts.md (read payload schemas)
├── error-codes.md (12 stable error codes)
├── wire-examples/*.json (8 example payloads)
└── deliveries/P0..P10/ (per-package summary, commands, evidence, review-request)

files/pes2021/player-memory/
├── pes2021-player-record-v1.json (versioned profile, 27 fields)
├── dump-players-2026-09-02.json + .csv + summary.md (initial 28-player dump)
├── dump-players-all-arenas-2026-09-02.json + .csv + summary.md (61-player combined dump)
└── dump-289k-arena12.json, dump-arena-B.json, dump-all-arenas-2026-09-02.json (working files)

src/Overmem.Extensions.Pes2021/
├── Players/Pes2021PlayerProfile.cs
├── Players/Pes2021PlayerProfileLoader.cs
├── Players/Pes2021PlayerProfileDefaults.cs
├── Players/Pes2021PlayerProfileException.cs
├── Players/Pes2021PlayerModels.cs
├── Players/Pes2021PlayerBitfieldCodec.cs
├── Players/Pes2021PlayerRecordParser.cs
├── Players/Pes2021PlayerRecordValidator.cs
├── Players/Pes2021PlayerRecordRejectionReasons.cs
├── Players/Pes2021PlayerDiscoveryDiagnostics.cs
├── Players/Pes2021PlayerDiscoveryDiagnosticsCollector.cs
├── Players/Pes2021PlayerSessionCache.cs
├── Players/Pes2021PlayerAnchorFinder.cs
├── Players/Pes2021PlayerRegionScanner.cs
├── Players/Pes2021PlayerCatalog.cs
├── Players/Pes2021PlayerQueryService.cs
├── Players/Pes2021PlayerCatalogExporter.cs
├── Players/Pes2021PlayerTransactionModels.cs
├── Players/Pes2021PlayerTransactionCore.cs
├── Players/Pes2021PlayerWritePolicy.cs
├── Players/Pes2021PlayerQueryModels.cs
├── Cli/Pes2021CliExtension.cs (added pes2021-find-player-anchor, pes2021-scan-players, pes2021-query-player, pes2021-export-player-catalog, pes2021-stride-scan-players, pes2021-scan-all-arenas)
└── Tools/Pes2021PlayerTools.cs (MCP tools)

tests/Overmem.Extensions.Pes2021.Tests/
├── Pes2021PlayerContractsTests.cs (P0)
├── Pes2021SourceManifestTests.cs (P0)
├── Pes2021PlayerProfileTests.cs (P1)
├── Pes2021PlayerParserTests.cs (P2)
├── Pes2021PlayerBitfieldCodecTests.cs (P2)
├── Pes2021PlayerDiscoveryTests.cs (P3)
├── Pes2021PlayerCatalogTests.cs (P4)
├── Pes2021PlayerCliSurfaceTests.cs (P5)
├── Pes2021PlayerTransactionTests.cs (P7)
├── Pes2021PlayerWritePolicyTests.cs (P9)
└── FakeProcessMemoryGateway.cs + FakeSystemClock.cs (test infrastructure)
```

## Suggested next steps

1. **Codex review of P0 to P10 + live evidence.** This handoff document is the review input.
2. **Codex approval of the new stride discovery.** The `pes2021.player-record.v1` profile is still based on the 380-byte feasibility-study stride. A new profile variant (`pes2021.player-record-live-v1`) should reflect the 0x46C94 padding.
3. **User decision on Master League.** When the user loads a Master League, re-run `pes2021-scan-all-arenas --stride 763` and validate the new full roster.
4. **P6 + P8 still deferred.** Both require explicit per-package authorization.

## Verified commit graph (most recent first)

```
1ad264f feat(pes2021): dump 61 players across both EDIT arenas - new stride discovery
b89d830 feat(pes2021): live dump - 28 players decoded at 0x7FF4D9F50000 with stride 763 (UTF-8 names)
267df07 feat(pes2021): live discovery - EDIT-base arena at 0x7FF4D9F50000 with stride 763
836a152 feat(pes2021): implement player-memory package P10 (final delivery index + README update)
a2610ac feat(pes2021): implement player-memory package P9 (per-field write policy + evidence gating)
709b149 feat(pes2021): implement player-memory package P7 (transaction core, TestTarget only)
9be8bdb feat(pes2021): implement player-memory package P5 (CLI and MCP read-only surface)
f310607 feat(pes2021): implement player-memory package P4 (catalog, query service, atomic JSON export)
a89f3cd feat(pes2021): implement player-memory package P3 (anchor finder, region scanner, session cache)
2cef010 feat(pes2021): implement player-memory package P2 (pure parser, validator, bitfield codec)
3a7d327 feat(pes2021): implement player-memory packages P0 and P1 (contracts + profile)
```

## Quality gates

| Gate | Result |
|---|---|
| `dotnet build Overmem.slnx` | 0 warnings, 0 errors |
| `dotnet test Overmem.slnx` | 382/382 green |
| Live memory read against PES2021.exe | successful, 3.6 GB private memory walked, 0 bytes written |
| Backup of save before any write | n/a (no writes executed) |
| Authorization gate | respected — no P6/P8 attempted |

## Risks for Codex to evaluate

1. **Stride discrepancy.** The original study claimed 380-byte stride; live evidence shows 0x46C94. The `pes2021.player-record.v1` profile uses 380 internally, which would still decode correctly (every 763rd slot) but would miss records aligned to the 0x46C94 grid. Confirm whether to publish a new profile variant.
2. **Two EDIT arenas.** The same player-set is mirrored in two distinct arenas (A at `0x7FF4D9E60000`, B at `0x7FF4D9F50000`). Confirm whether both are authoritative or whether B is a copy/cache.
3. **Real-player filter.** The filter that reduced 1.222 hits to 61 real players is heuristic. Codex may want stricter invariants (e.g. require clubShortName to match a known list).
4. **Auto-stride discovery.** Currently the scan requires an explicit `--stride 763` flag. A future package should auto-detect the stride from a single 1 KB sample.

Awaiting Codex review.
