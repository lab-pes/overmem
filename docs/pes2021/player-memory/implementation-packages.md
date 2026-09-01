# PES 2021 player memory: gated implementation packages

This document is the self-contained handoff for a Minimax M3/OpenCode implementation agent. The feasibility basis and field-status rules are in `feasibility-study.md` and are mandatory input.

`edit-first-decision.md` is also mandatory. P0-P6 implement only the always-loaded `EDIT_BASE` mapping. Master League work is a separate deferred track (M0-M4) and must not start before P6 is accepted.

## Roles and phase rule

- M3/OpenCode implements one package at a time and produces the required evidence.
- Codex audits code, tests, artifacts, and claims before the next dependent package is authorized.
- Willian decides whether and when live PES write validation is allowed.

Do not start a package whose dependency gate is not accepted. In particular, packages P7-P9 do not authorize a live PES write by themselves, and M0-M4 are forbidden before the EDIT read gate P6.

Use these epistemic labels in code comments, profiles, reports, and reviews:

- `CONFIRMED`: repeated evidence supports the exact claim in the stated context;
- `CANDIDATE`: plausible and useful for experiments, but not an API guarantee;
- `UNKNOWN`: mapped bytes without justified semantics;
- `REFUTED`: evidence contradicts the claim.

## Global constraints

1. Work only in `D:\git-lab-pes\overmem`.
2. Do not modify `C:\Users\Willian\Documents\My Cheat Tables`.
3. Do not execute its Lua scripts.
4. Keep the player extension self-contained in Overmem; it may cite source hashes but must not require the external repository at runtime.
5. Do not add code injection, hooks, a Lua runtime, or Cheat Engine as a dependency.
6. Do not write to `PES2021.exe` until Willian explicitly authorizes the specific live package execution.
7. Read commands must be incapable of writing by construction.
8. Treat `0x12C`, `0x12E`, `0x178`, and `0x179` as unknown until promoted by evidence.
9. Do not use `playerId` alone to select a write target.
10. Preserve raw values and expose display conversions separately.
11. Every live address is session-local and must be rediscovered after restart.
12. Do not commit binary dumps containing unrelated process memory; fixtures must be minimal and documented.
13. P0-P6 must be developed and validated with no Master League loaded.
14. The first shipped profile is `pes2021-player-edit-v1.json`; do not create or advertise an ML profile during P0-P6.
15. Complete coverage means classifying every EDIT arena slot and territorially covering `0x000..0x17B`; `UNKNOWN` is valid and silent gaps are not.

## Standard delivery layout

Each package creates:

```text
docs/pes2021/player-memory/deliveries/PX/
  summary.md
  commands.md
  test-results.txt
  evidence.json
  review-request.md
```

`summary.md` must list changed files, decisions, limitations, and rollback. `commands.md` must contain exact commands that another agent can rerun. `evidence.json` must use stable, documented fields and must not contain a claim stronger than its evidence.

Before requesting review, run from the repository root:

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build
git diff --check
git status --short
```

If a process locks build output, document the lock and run the narrow test project without killing user processes.

## Gate summary

| Package | Goal | Depends on | Gate unlocks |
|---|---|---|---|
| P0 | EDIT provenance, two-context contracts, and coverage schemas | none | P1 |
| P1 | territorial EDIT profile loader and field schema | P0 | P2-P3 |
| P2 | pure EDIT parser, validators, and bit codec | P1 | P3, P7 |
| P3 | injection-free EDIT arena discovery and complete slot classification | P1-P2 | P4 |
| P4 | EDIT catalog, identity, query, and atomic export | P3 | P5 |
| P5 | CLI/MCP EDIT read surface | P4 | P6 |
| P6 | read-only EDIT live/restart and 100% coverage gate | P5 | consideration of P7 and M0 |
| P7 | write transaction core on TestTarget only | P2, P5 | P8 |
| P8 | optional single-player EDIT market-value pilot | P6-P7 plus explicit live authorization | P9 |
| P9 | EDIT field-by-field promotion and guarded batch writes | P8 plus per-field gates | optional EDIT write release |
| P10 | EDIT hardening and documentation | accepted P6; P8-P9 optional | EDIT release decision |
| M0-M4 | separate ML discovery, comparison, profile, reads, then writes | accepted P6 | later ML release |

## P0 - Provenance, terminology, and public contracts

### Objective

Turn the study and EDIT-first decision into enforceable repository contracts without implementing memory access.

### Scope

- Add `docs/pes2021/player-memory/source-manifest.json` with the source paths, SHA-256 values, capture date, and the fact that the Overmem CT copy is byte-identical.
- Add the JSON wire-contract draft for anchor, scan, player snapshot, diagnostics, patch plan, apply result, and rollback result.
- Define `PlayerRecordContext`, `EvidenceStatus`, and error/status codes in documentation.
- Document raw versus display values and ambiguous identity behavior.
- Define separate `EDIT_BASE` and `MASTER_LEAGUE` contexts, while exposing only EDIT in the first implementation track.
- Define schemas for territorial record coverage, arena-segment coverage, slot classification, collision reports, and zero-write proof.

Required error codes include:

- `PES2021_PLAYER_PROFILE_INVALID`;
- `PES2021_PLAYER_ANCHOR_NOT_FOUND`;
- `PES2021_PLAYER_ANCHOR_AMBIGUOUS`;
- `PES2021_PLAYER_RECORD_INVALID`;
- `PES2021_PLAYER_ID_AMBIGUOUS`;
- `PES2021_PLAYER_CONTEXT_INCOMPATIBLE`;
- `PES2021_PLAYER_STALE_SESSION`;
- `PES2021_PLAYER_WRITE_NOT_AUTHORIZED`;
- `PES2021_PLAYER_EXPECTED_BYTES_MISMATCH`;
- `PES2021_PLAYER_VERIFY_FAILED`;
- `PES2021_PLAYER_ROLLBACK_FAILED`.
- `PES2021_EDIT_ARENA_INCOMPLETE`;
- `PES2021_EDIT_TERRITORY_INCOMPLETE`.

### Required evidence

- manifest hashes recomputed from current files;
- JSON examples pass `System.Text.Json` round-trip in a small contract test;
- review explains why no runtime dependency points to the external CT repository.

### Acceptance

- zero production memory code changed;
- contracts distinguish candidate semantics from confirmed semantics;
- no absolute live address appears as a default or constant.

### Review questions

1. Can a consumer distinguish raw, display, and evidence status?
2. Can an ambiguous ID be represented without silently choosing one record?
3. Is every source/provenance statement reproducible?

## P1 - Versioned player-memory profile

### Objective

Implement a validated, immutable territorial profile for the EDIT `0x17C` record without yet reading a process.

### Expected files

```text
files/pes2021/player-memory/pes2021-player-edit-v1.json
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerProfile.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerProfileLoader.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerProfileDefaults.cs
tests/Overmem.Extensions.Pes2021.Tests/Players/Pes2021PlayerProfileTests.cs
```

### Requirements

- Record stride exactly 380 (`0x17C`).
- Field definitions include offset, width/type, endianness, signedness, bit start/length, display offset/scale, read status, write status, valid contexts, and evidence notes.
- Reject overlaps unless explicitly marked as a shared bitfield container.
- Reject fields outside the record.
- Reject invalid transforms and bit ranges.
- Source hash metadata must match P0.
- Default status for unproved fields is `CANDIDATE` or `UNKNOWN`, never implicit `CONFIRMED`.
- Store `unknown_12c`, `unknown_12e`, `unknown_178`, and `unknown_179` under those neutral names.
- Partition every byte from `0x000` through `0x17B` as a field, shared bit container, padding/reserved, or `UNKNOWN` range.
- Reject a profile whose first covered byte is not `0x000`, whose last covered byte is not `0x17B`, that has any uncovered byte, or that has an unjustified overlap.
- Do not call `+0x15C` authoritative salary in the EDIT profile; retain a neutral raw field/status until ML evidence exists.

### Tests

- valid profile loads;
- missing stride, invalid offset, bit overflow, duplicate field key, unjustified overlap, unsupported type, and bad evidence status fail with `PES2021_PLAYER_PROFILE_INVALID`;
- serialize/reload preserves the semantic profile;
- source CT is not needed at runtime.
- complete territorial coverage succeeds with `UNKNOWN` ranges and reports zero uncovered bytes;
- a one-byte gap and an unjustified overlap both fail.

### Acceptance

All profile tests pass and a field-table generator can render the profile into Markdown without handwritten offset duplication.

### Review questions

1. Does the profile encode independent read and write promotion?
2. Are context requirements machine-enforceable?
3. Are byte/bit overlaps intentional and tested?

## P2 - Pure record parser, validator, and bitfield codec

### Objective

Decode and encode EDIT player records deterministically without a process gateway.

### Expected files

```text
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerRecordParser.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerRecordValidator.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerBitfieldCodec.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerModels.cs
tests/Overmem.Extensions.Pes2021.Tests/Players/Pes2021PlayerParserTests.cs
tests/Overmem.Extensions.Pes2021.Tests/Players/Pes2021PlayerBitfieldCodecTests.cs
tests/Overmem.Extensions.Pes2021.Tests/Fixtures/Players/
```

### Requirements

- Parser consumes exactly one `ReadOnlySpan<byte>` of at least 380 bytes.
- Decode fixed-width little-endian integers and bounded strings explicitly.
- Decode `playerId` as an opaque `u32`. Marker bits such as `0x40000000` and `0x80000000` are preserved and reported as `UNKNOWN`; they are not a validation failure.
- Preserve the raw 380 bytes or their SHA-256 in the snapshot.
- Return per-field raw value, optional display value, evidence status, and decode warnings.
- Return the record context as `EDIT_BASE_CANDIDATE` unless the caller supplies accepted session-context evidence.
- Validator produces a score and reasons; it must not return only a boolean.
- Cheap checks precede expensive string/neighbor checks.
- Implement bitfield read-modify-write over a copy of the record.
- A bitfield patch must prove that every non-target bit is unchanged.

### Fixture policy

Create minimal synthetic records plus a narrowly cropped, documented live record only if it contains no unrelated memory. The fixture manifest records source, address only as historical evidence, timestamp, record SHA-256, and redaction decision.

### Tests

- decode the five observed control records and synthetic boundary cases;
- malformed names, impossible height/weight, zero IDs, truncated buffers, and invalid market values receive explicit reasons;
- fixtures cover IDs below 300,000, from 300,000 through 499,999, above 500,000, with `0x40000000`, and with `0x80000000`;
- every profile bitfield has round-trip and neighbor-bit preservation tests;
- signed byte and signed Int32 boundaries are tested;
- market raw/display conversion is labeled candidate until P6.

### Acceptance

- pure tests need no Windows process;
- parser agrees with CT/v5 offsets, not v4 semantic output;
- mutation tests demonstrate non-target bytes/bits stay identical.

### Review questions

1. Does invalid input fail without out-of-range reads?
2. Can warnings coexist with a structurally valid record?
3. Is a packed-field patch byte-for-byte minimal?

## P3 - Injection-free anchor and player-family discovery

### Objective

Find and delimit the complete EDIT `0x17C` arena using only `IProcessMemoryGateway` reads.

### Expected files

```text
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerAnchorFinder.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerRegionScanner.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerSessionCache.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerDiagnostics.cs
tests/Overmem.Extensions.Pes2021.Tests/Players/Pes2021PlayerDiscoveryTests.cs
```

### Algorithm contract

1. Enumerate readable, non-executable private regions.
2. Search for an exact requested player ID or a bounded set of control IDs.
3. For every ID hit, calculate candidate base `hit - profile.playerId.offset` with checked arithmetic.
4. Parse/validate the candidate.
5. Score at least two neighbors on each side at `0x17C` when readable.
6. Reject or return ambiguity when top candidates are not decisively separated.
7. Read the containing region in bounded overlapping blocks.
8. Find valid records in 4-byte alignment, then cluster by address residue modulo `0x17C` and contiguous/multiple-of-stride relationships.
9. Delimit every accepted EDIT arena segment and calculate its theoretical slots.
10. Revisit every theoretical slot and classify it as `VALID_PLAYER`, `INVALID_OR_EMPTY_SLOT`, `HOLE`, `UNREADABLE`, `PARTIAL_READ`, or `AMBIGUOUS_RECORD`.
11. Deduplicate overlapping block hits.
12. Report segment boundaries, theoretical slots, every classification count, duplicates, rejected candidates/reasons, partial reads, bytes scanned, elapsed time, and all coverage denominators.

The single-session baseline in `edit-live-evidence-2026-08-31.md` is a required regression fixture for contracts, not a set of constants: 30,001 territorial slots, 25,005 populated slots, and 4,996 byte-identical empty/reserved slots. The implementation must rediscover those boundaries and may report different counts for a different database/mod, but it must never silently omit high-bit IDs or the reserved tail.

Do not copy the Lua's unbounded `skipSize=10000` behavior. It changes stride residue and is not a reliable array traversal rule.

Cache key must include attachment ID, PID, process start time, profile ID/version/SHA-256, and discovery parameters. Never reuse after restart.

### Tests

- synthetic region with a clean 24-record run;
- holes of one and several strides;
- false ID hits outside records;
- two competing record families causing ambiguity;
- records split across block boundaries;
- unreadable/partial regions;
- duplicate IDs and duplicate block hits;
- multiple EDIT arena segments with the same stride residue;
- complete theoretical-slot accounting with no silent omission;
- opaque `u32` IDs with `0x40000000` and `0x80000000` remain valid structural candidates;
- a populated run followed by a byte-identical reserved tail and then unrelated data is delimited without absorbing the unrelated structure;
- one intentionally unreadable page represented as `UNREADABLE`, not skipped;
- stale cache after process-start change;
- cancellation and scan-budget enforcement.

### Performance target

On synthetic data, scan at least 32 MiB in bounded memory and without per-address gateway calls. Use block reads and span parsing. Record benchmark numbers; do not invent a live target before P6 measures it.

### Acceptance

- zero calls to `WriteAsync` in discovery paths, proven with a spy gateway;
- deterministic results independent of block boundary placement;
- ambiguity is explicit;
- diagnostics explain every rejected anchor.
- accepted segments have `classifiedSlots == theoreticalSlots` and zero unaccounted slots.

### Review questions

1. Can an arbitrary Int32 occurrence become an anchor without neighbor evidence?
2. Is memory use bounded by block size rather than region size?
3. Does restart invalidate every cached address?

## P4 - Player catalog, identity, query, and atomic export

### Objective

Turn discovered EDIT addresses into a queryable, collision-aware session catalog.

### Expected files

```text
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerCatalogService.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerQuery.cs
src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerJson.cs
src/Overmem.Extensions.Pes2021/Cli/Pes2021PlayerAtomicFileWriter.cs
tests/Overmem.Extensions.Pes2021.Tests/Players/Pes2021PlayerCatalogTests.cs
```

### Requirements

- Define session identity as process/session/profile plus address, player ID, and fingerprint.
- Query by ID returns zero, one, or many; never first-match selection.
- Support filters only over decoded fields and expose candidate/unknown status.
- Paginate MCP-oriented results and bound maximum records returned inline.
- Export a stable wire schema such as `pes2021.edit-players.v1`.
- Atomic file output: write temp, flush, then replace/move.
- Include scan diagnostics, collisions, unresolved records, context classification, profile metadata, and raw/display values.
- Do not infer team/league names from `0x12C/0x12E`.
- Emit the complete artifact set required by `edit-first-decision.md`, including arena, slot, territorial, collision, rejection, zero-write, and SHA-256 manifests.

### Tests

- duplicate ID collision;
- fingerprint mismatch;
- stable ordering;
- pagination boundaries;
- JSON enum/property casing;
- atomic output leaves no partial final file on simulated failure;
- unknown fields remain present as raw values without semantic aliases.

### Acceptance

The historical three duplicate IDs can be represented without data loss, and an agent cannot obtain a unique write identity from an ambiguous ID query.

## P5 - CLI and MCP read-only surface

### Objective

Expose EDIT discovery and read/query/export operations through both hosts with equivalent contracts.

### Changes

- Register player services in `Pes2021Extension`.
- Add player cases to `Pes2021CliExtension` and executable command classes.
- Add a separate `Pes2021PlayerTools` MCP tool class and register it with `Pes2021PlayerJson.Options`.
- Keep agenda/fixture tools backward-compatible.

### Required commands/tools

```text
pes2021-find-player-anchor     / pes2021_find_player_anchor
pes2021-scan-players          / pes2021_scan_players
pes2021-get-player            / pes2021_get_player
pes2021-query-players         / pes2021_query_players
pes2021-export-players        / pes2021_export_players
```

Every command accepts an optional profile path and bounded scan options. Commands that need a unique record require the full session identity or address plus expected ID/fingerprint.

Every command also accepts `--context edit`; only `edit` is valid in P5. Supplying `ml` must return a stable not-implemented/context-unavailable error rather than silently scanning EDIT.

### Required verification after implementation

```powershell
dotnet run --project src\Overmem.Cli -- pes2021-find-player-anchor --name PES2021 --context edit --player-id 58120
dotnet run --project src\Overmem.Cli -- pes2021-scan-players --name PES2021 --context edit --player-id 58120 --output-directory .\artifacts\pes2021\player-memory\edit
dotnet run --project src\Overmem.Cli -- pes2021-get-player --name PES2021 --context edit --address <rediscovered-address> --expected-player-id 58120
```

These commands are for P6 live validation. They must not be run against a live process as part of P5 code review unless read-only execution has been approved; unit/integration fakes are sufficient for P5.

### Tests

- CLI parse and execution with fake gateway;
- MCP reflection/schema tests;
- cancellation propagation;
- invalid profile and ambiguity error mapping;
- spy gateway proves all five operations make zero writes.

### Acceptance

CLI and MCP return semantically equivalent payloads, and no read operation contains a hidden write/freeze path.

## P6 - Read-only EDIT live validation, complete coverage, and restart gate

### Objective

Prove complete Overmem-native EDIT discovery against PES 2021 without Cheat Engine, without Master League loaded, and without writes.

### Preconditions

- P0-P5 accepted;
- PES 2021 is running with no Master League loaded, confirmed and recorded by the operator;
- executable path/version/hash and active mods are recorded;
- operator provides visible EDIT control values for at least five players, including one duplicated-ID or negative control when practical.

### Run A

1. Capture process identity and memory-region manifest.
2. Find anchor by a known ID.
3. Discover every EDIT arena segment and classify every theoretical slot.
4. Export all valid players plus invalid/empty, hole, unreadable, partial, ambiguous, duplicate, and rejected classifications.
5. Compare count, IDs, names, stride distribution, duplicates, and rejected records with the historical 23,253-row artifact without using that count as an automatic denominator.
6. Correlate names, IDs, height, weight, nationality, base attributes, positions/styles/skills, and raw/display market value against the EDIT UI for at least five players.
7. Generate the territorial map for every byte `0x000..0x17B`, allowing `UNKNOWN` but allowing no uncovered byte or unjustified overlap.
8. Classify every accepted observed record as `EDIT_BASE_CONFIRMED` for this session.
9. Record zero-write proof from the operation journal and spy/instrumented service boundary.

### Run B after full game restart

Restart the full game, again load no ML, and repeat discovery without reusing any Run A address. Compare arena segmentation, complete slot accounting, player fingerprints, coverage, and semantic values. Record moved addresses as expected, not as failure.

### Evidence files

```text
docs/pes2021/player-memory/evidence/edit-read-a-<timestamp>.json
docs/pes2021/player-memory/evidence/edit-read-b-restart-<timestamp>.json
docs/pes2021/player-memory/evidence/edit-read-validation-report.md
```

### Promotion gate

- EDIT arena discovery: two no-ML runs, no stale-address reuse, stable control-player decode;
- every accepted arena has `classifiedSlots == theoreticalSlots` in both runs;
- territorial map starts at `0x000`, ends at `0x17B`, has zero uncovered bytes, and has zero unjustified overlaps;
- a field becomes `CONFIRMED_READ` only after multiple-player UI correlation and restart consistency in its declared context;
- market display scale becomes confirmed only if raw/display pairs agree exactly;
- salary, contract, current form, and other ML semantics remain candidate/unknown and are not P6 acceptance requirements;
- coverage report declares its denominator and has zero unexplained silent omissions.

### Acceptance

Codex signs off the EDIT read report. Failure to confirm one semantic field does not invalidate structural or territorial completeness; it keeps that field candidate/unknown. P6 acceptance authorizes consideration of M0, not automatic ML implementation.

## P7 - Transactional write core on TestTarget only

### Objective

Implement the complete safety mechanism without writing to PES 2021.

### Expected files

```text
src/Overmem.Extensions.Pes2021/Players/Writes/Pes2021PlayerPatchPlanner.cs
src/Overmem.Extensions.Pes2021/Players/Writes/Pes2021PlayerPatchExecutor.cs
src/Overmem.Extensions.Pes2021/Players/Writes/Pes2021PlayerPatchRollback.cs
src/Overmem.Extensions.Pes2021/Players/Writes/Pes2021PlayerWritePolicy.cs
tests/Overmem.Extensions.Pes2021.Tests/Players/Writes/
```

### Contract

- `plan` is read-only and returns exact address/old/new byte ranges plus SHA-256 plan ID.
- `apply` requires plan ID, same process start/profile, non-expired authorization token, expected original bytes, and policy allowlist.
- persist rollback material atomically before the first write;
- coalesce only adjacent patches that do not obscure field boundaries;
- after each write, reread and verify;
- on any failure, stop and attempt rollback of already-applied patches;
- rollback is idempotent and itself verified;
- operation journal records plan/apply/verify/rollback outcomes without logging secrets.

### Tests

- happy path against `Overmem.TestTarget` or a deterministic fake gateway;
- stale session/profile/plan;
- original-byte mismatch;
- partial write;
- verify mismatch;
- rollback success and rollback failure;
- bitfield neighbor preservation;
- target limit;
- ambiguous identity rejection;
- cancellation at each phase;
- crash-safe rollback artifact exists before first gateway write.

### Acceptance

- no P7 test attaches to PES 2021;
- every failure mode returns a stable code and audit result;
- repeated rollback cannot corrupt the original value;
- dry run is the default at every host boundary.

## P8 - Optional single-player EDIT market-value write pilot

### Objective

Promote only market value (`+0x174`, `i32`) through a controlled single-record write to an `EDIT_BASE_CONFIRMED` record. This package is optional and is not part of the initial EDIT read release.

### Hard preconditions

- P6 and P7 accepted;
- `market_value` is `CONFIRMED_READ` for `EDIT_BASE_CONFIRMED` context;
- market raw/display scale is confirmed;
- Willian explicitly authorizes this live test at execution time;
- save backup exists and its path, size, timestamp, and SHA-256 are recorded;
- exactly one unambiguous player is selected;
- the test value and rollback value are agreed before apply.

### Required host surface

```text
pes2021-plan-player-patch     / pes2021_plan_player_patch
pes2021-apply-player-patch    / pes2021_apply_player_patch
pes2021-rollback-player-patch / pes2021_rollback_player_patch
```

Write tools must be registered only when the transaction policy is present; they remain dry-run unless the explicit apply parameters are supplied.

### Procedure

1. Rediscover the player; do not use a saved address.
2. Capture full 380-byte before image and focused field bytes.
3. Create a one-field plan and review its exact four changed bytes.
4. Apply with compare-and-swap.
5. Reread and verify raw value.
6. Confirm the visible UI value and any required screen refresh.
7. Roll back to the original value.
8. Reread, confirm UI restoration, and compare full record before/after rollback.
9. If later persistence is being tested, do so as a separately authorized step with save backup comparison.

### Acceptance

- exactly four target bytes changed during apply;
- full record is byte-identical after rollback;
- UI correlation agrees with the confirmed scale;
- no other player changed;
- audit and rollback artifacts are complete;
- market value alone becomes `CONFIRMED_WRITE` in the validated context/version.

Any mismatch blocks P9. Do not “fix forward” with ad hoc generic writes.

## P9 - EDIT field-by-field promotion and guarded batch writes

### Objective

Expand EDIT capability one field at a time, then add bounded batch operations only for independently confirmed EDIT fields.

### Promotion order

1. aligned scalar EDIT fields already confirmed by P6;
2. base attribute bitfields;
3. positions, playing style, COM styles, and skills;
4. bounded batch market-value operations;
5. bounded batch operations for other independently confirmed EDIT fields.

Salary, contract, affection, current form, listings, personality, impact, unavailable days, and other ML-context semantics are explicitly out of P9 scope. They belong to M4 after an ML profile is validated.

This is an order of investigation, not a statement that the fields are already correct.

### Per-field gate

Each field needs its own evidence document containing:

- authoritative record context;
- raw type and display transform;
- two or more controlled before/after values;
- neighboring-byte/bit diff;
- UI effect;
- rollback proof;
- restart behavior;
- save persistence behavior if claimed;
- version/mod scope;
- decision: confirmed, candidate, unknown, or refuted.

### Batch safety

- plan and export every target before apply;
- explicit maximum target count and `--allow-batch`;
- reject duplicate/ambiguous identities;
- stable plan ordering;
- one plan hash covers all targets and original bytes;
- fail before first write if any expected bytes changed;
- verified rollback for every target;
- summary counts for planned/applied/skipped/failed/rolled back;
- no automatic “all players” default.

### Acceptance

Only allowlisted `CONFIRMED_WRITE` fields can reach the executor. Unknown/candidate fields are rejected even if the caller knows their raw offset.

## P10 - EDIT hardening, documentation, and release audit

### Objective

Make the accepted EDIT read capability, and any separately accepted EDIT write capability, maintainable and safe for agent use.

### Scope

- Update root README capability tables accurately.
- Generate field-map documentation from the profile.
- Add examples for read-only discovery, query, export, dry-run patch, apply, and rollback.
- Document context classification and ambiguity handling.
- Add benchmark and memory-allocation reports.
- Add threat/failure analysis: stale address, wrong copy, wrong profile, partial read/write, UI cache, duplicate ID, bitfield corruption, process restart, save persistence mismatch.
- Add EDIT compatibility matrix by executable hash/version/mod set.
- Add end-to-end read tests and TestTarget transaction tests to CI.
- Audit that unimplemented/candidate capabilities are not advertised as ready.

### Final commands

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet run --project src\Overmem.Cli -- help
git diff --check
git status --short
```

If live read validation is in scope for the release audit, rerun the P6 commands after a fresh restart with no ML loaded. Live write tests are not part of routine CI and require explicit authorization every time.

### Acceptance

- documentation separates implemented, confirmed, candidate, and planned capability;
- no external source repository is needed;
- read and write APIs have independent security and evidence gates;
- all accepted packages have complete delivery artifacts and review decisions.

## Deferred Master League track - M0 through M4

This track is documented now but must not be delegated before P6 is accepted.

### M0 - Paired capture protocol

Capture A with no ML loaded, then load a known ML and capture B. Rediscover all addresses in B. Verify whether the accepted EDIT arena remains present and inventory additional record families/caches. Produce process, region, arena, and zero-write manifests for both states.

Acceptance: captures are comparable by executable/profile/process evidence; no A address is assumed valid in B; all newly observed families remain `MASTER_LEAGUE_CANDIDATE`.

### M1 - EDIT-to-ML relational comparer

Join records by collision-aware fingerprint rather than address or player ID alone. Produce byte/bit diffs, stride/segment analysis, common versus context-specific fields, missing/extra records, and ambiguity reports.

Acceptance: every joined, unjoined, and ambiguous record is accounted for; no semantic name is assigned from difference alone.

### M2 - ML profile and parser

Create `pes2021-player-ml-v1.json` only after M0-M1 evidence. It may reuse structural codecs internally, but evidence status, valid context, display transform, and write status remain independent from EDIT.

Acceptance: full territorial coverage for every ML record/overlay structure claimed by the profile, with `UNKNOWN` ranges allowed and no uncovered bytes.

### M3 - ML read surface and live/restart/load validation

Expose `--context ml` only after the ML profile exists. Correlate salary, contract, function, affection, listings, unavailable days, current form, and related fields against the ML UI across players, save reload, and full restart.

Acceptance: a field is `CONFIRMED_READ` only in the exact ML context/version where repeated evidence supports it; UI caches and authoritative ML records are distinguished.

### M4 - ML write promotion

Promote fields individually through the P7 transaction core. Each field needs controlled before/after evidence, minimal byte/bit diff, UI effect, save/reload behavior if claimed, and verified rollback. No batch ML writes until single-record promotion is accepted per field.

Acceptance: only `CONFIRMED_WRITE` ML fields enter the allowlist; EDIT permissions do not transfer automatically to ML and vice versa.

## Recommended first delegation

Delegate P0 first using `delegation-p0-edit-contracts.md`. It is bounded, does not touch process memory, and forces agreement on EDIT/ML separation and coverage contracts. After Codex accepts P0, delegate P1; after P1 is accepted, delegate P2. Do not combine P0-P3 into a single M3 request, and do not delegate M0-M4 before P6.
