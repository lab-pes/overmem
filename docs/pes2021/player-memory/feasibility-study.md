# PES 2021 player memory in Overmem: feasibility study

Date: 2026-08-31  
Status: architecture and implementation plan approved for future work; no player-memory implementation exists yet  
Runtime safety of this study: read-only; no memory write, freeze, hook, injection, or Cheat Engine script execution was performed
Priority decision: map the always-loaded EDIT player arena first; map Master League as a separate later context

## Decision

It is technically feasible to bring the PES 2021 player-record mapping into `Overmem.Extensions.Pes2021` and expose player-specific read and write operations.

The operator confirmed that no Master League was loaded during the live read. The observed record family is therefore the competition-independent EDIT/base database, not an ML record copy. This correction strengthens the EDIT discovery evidence and splits the target into two independent mappings: `EDIT_BASE` first and `MASTER_LEAGUE` later.

The feasibility is supported by five independent observations:

1. The Cheat Table describes a player record with stride `0x17C` and 129 entries rooted at `ptrPlayer`.
2. The working Lua scanner uses the same stride, validates record candidates, and has already exported 23,253 distinct record addresses.
3. In that export, 22,971 of 23,252 adjacent-address differences (98.79%) are exactly `0x17C`; the remaining common differences are integer multiples of `0x17C`.
4. A read-only Overmem scan against the running `PES2021.exe` found a valid record without using Cheat Engine and decoded five consecutive players at `0x17C` intervals.
5. A territorial read of the containing region delimited 30,001 EDIT slots: 25,005 populated records followed by 4,996 byte-identical reserved/empty records.

This does **not** mean that every field name in the Lua or CT is already semantically proven. Discovery of the record family is strong. Field authority, context, scaling, persistence, and write safety must be promoted independently.

## Scope of the proposed extension

The target is a self-contained PES 2021 extension inside Overmem that can:

- rediscover the player-record family on every process session without requiring Cheat Engine;
- discover and classify EDIT and ML record families independently instead of merging their authority;
- enumerate and export valid player records with explicit coverage and collision diagnostics;
- read a versioned, typed schema including packed bitfields;
- distinguish raw stored values from display values;
- query a player by a non-ambiguous session identity;
- plan, preview, apply, verify, and roll back guarded writes;
- expose the same domain operations through CLI and MCP;
- retain evidence about executable identity, profile hash, process start, discovery method, and validation status.

The target is not a Lua runtime, CT executor, debugger, or code-injection facility. The CT and Lua are evidence sources only.

## Sources and provenance

The source repository was read at:

`C:\Users\Willian\Documents\My Cheat Tables`

The Overmem working tree already contains an identical copy of the principal CT at:

`files\PES 2021 - v21.1.0.CT`

| Source | SHA-256 | Use in this study |
|---|---|---|
| `scripts\players\ZerarValorMercado.lua` | `27A8486E0145725EB8D9C370566038C50B98BC5E5AEB8009927E0C076DF1D809` | Entry point and active v4 wiring |
| `scripts\players\player_tool\operations.lua` | `A3F540FCE2E698914BF2DB4669CE1082F2FB931DD62F1CF6D80F4E407C7B1CC7` | Scan, validation, export, recalibration, and write behavior |
| `scripts\players\player_tool\reader_v5.lua` | `D9F9B7919BF4CD8EAAF07983E23397FE1580EE2820D088E814A97A73563AD115` | Candidate canonical decoding behavior |
| `scripts\players\player_tool\schema_v5.lua` | `6BD22B451085FE4D4209D7DB5FA93152CE78683439D760CA88D33BFC7144050E` | Candidate canonical offsets and bitfields |
| `PES 2021 - v21.1.0.CT` | `DA67EB5C8F7B13243AD5BE654D618EA5E4BAEB52449FECBC453144AF6C89AF7C` | Primary structure reference |
| `jogadores_pes2021.txt` | `0C771B409267009D28C6CC21C093113FB23749A97532676F07CB22EEA7047408` | Historical discovery evidence |

The external CT and `files\PES 2021 - v21.1.0.CT` have the same SHA-256.

## What the Lua actually proves

`ZerarValorMercado.lua` is a wrapper. It loads the v4 schema, reader, presentation, and shared operations module. The significant behavior lives in `operations.lua`:

- obtains one seed record through the CT symbol `ptrPlayer`;
- walks backward and forward in steps of `0x17C`;
- validates candidates using player ID, height, weight, name, and market-value plausibility;
- optionally rescans the address range in 4-byte steps;
- writes a signed 32-bit value at record offset `0x174`;
- saves raw session addresses and later attempts constant-delta recalibration.

The portable knowledge is the record layout, stride, validators, and scan strategy. The following are not portable:

- `ptrPlayer`, because the CT allocates and populates it through code injection;
- absolute addresses saved in April 2026;
- the assumption that every process restart is a single constant address delta;
- the assumption that every structurally valid EDIT record is authoritative for Master League fields.

Overmem must rediscover records from process memory and validate the record family on each session.

## Historical export analysis

The saved list contains:

- 23,253 parsed rows;
- 23,253 unique addresses;
- 23,250 unique player IDs;
- three duplicated IDs: `52992`, `52999`, and `56299`;
- minimum address `0x7FF4D9890010`;
- maximum address `0x7FF4DA1376F0`.

The file documentation estimates 24,112 total players. Relative to that estimate, 23,253 is about 96.4%, but this percentage is not an authoritative coverage result because the denominator has not been validated for the active database/mod combination.

The duplicated IDs prove that `playerId` alone must not silently select a write target. A session record identity should include at least process identity, record address, player ID, and a stable fingerprint such as name/commentary ID. Queries by player ID must return all matches or an ambiguity error.

The large JSON export must not be used as canonical semantic evidence. It was produced by the v4 reader and contains impossible decoded values such as form `83` and playing-style code `96`. It is useful as address/record-family evidence only.

A later live comparison against the EDIT arena found every one of the 23,250 historical unique IDs and no historical-only ID. However, the live arena contains 1,755 additional raw IDs. The historical file therefore covers 97.02% of the 23,963 live IDs below 500,000, but only 92.98% of all 25,005 populated records when IDs are preserved as opaque `u32`. The old scanner's numeric ID limits omit 50 unflagged extended IDs and 992 records with high ID bits. See `edit-live-evidence-2026-08-31.md` for the complete evidence and denominators.

## Read-only live evidence

The active process during this study was PID `27040`, started on 2026-08-30. PID and addresses are session-specific evidence, not profile constants.

The operator confirmed that this process had no Master League loaded. Consequently, all records in this subsection are evidence for the always-loaded EDIT player arena. The session context status is `USER_CONFIRMED_SESSION_CONTEXT`; the structural read status is `CONFIRMED_READ_ONLY`.

Overmem performed an exact `Int32` search for player ID `58120`. A structurally valid hit at `0x7FF4D908F240` normalized to record base `0x7FF4D908F210` by subtracting the ID offset `0x30`.

Reading five records around that base produced:

| Relative slot | Address | Player ID | Name | Height | Weight | Raw market value |
|---:|---:|---:|---|---:|---:|---:|
| -2 | `0x7FF4D908EF18` | 58118 | Luis Segovia | 182 | 74 | 0 |
| -1 | `0x7FF4D908F094` | 58119 | Anthony Landazuri | 179 | 73 | 0 |
| 0 | `0x7FF4D908F210` | 58120 | Piero Hincapie | 184 | 74 | 500000 |
| +1 | `0x7FF4D908F38C` | 58121 | Jhon Sanchez | 175 | 74 | 0 |
| +2 | `0x7FF4D908F508` | 58122 | Jonathan Bauman | 178 | 73 | 0 |

Every address differs by `0x17C`. A 16-byte pattern from the normalized Piero record had one match in the process, which supports this as the EDIT/base-record copy for the current session.

At this EDIT copy, the raw values at `+0x12C/+0x12E` were both `0xFFFF` and `+0x15C` was zero. These values are consistent with ML context not being active. The 16-bit fields must remain `unknown_12c` and `unknown_12e`, and `+0x15C` must not be advertised as an authoritative salary in EDIT. They must not be published as team/league/salary merely because the Lua or CT used those labels in another context.

The same read-only session delimited the EDIT arena from `0x7FF4D8EC0010` through exclusive end `0x7FF4D999F4CC`. It contains 30,001 stride-aligned slots with zero unclassified slots inside the accepted boundary: 25,005 populated and 4,996 reserved/empty. All empty records were byte-identical. The next theoretical slot contains unrelated data, providing a structural end boundary. These addresses are evidence only and must be rediscovered after restart.

The player ID must be decoded as opaque `u32`, not rejected merely for being greater than 500,000. Of the 25,005 populated records, 24,013 have no high marker bits, 989 carry `0x40000000`, and three carry `0x80000000`. The meaning of those bits remains unknown. Using the Lua's ID bounds would silently discard structurally valid named players.

The raw market value `500000` is compatible with the Lua calculator's stated `stored * 100 EUR` convention (EUR 50,000,000), but this scale still needs a UI-correlated read test before it is marked confirmed.

## Canonical candidate record

Record size/stride: `0x17C` (380 bytes).

### High-confidence structural fields

These are supported by agreement between the CT and v5 schema, and several also participated in the live record validation:

| Field | Offset | Raw type | Initial status |
|---|---:|---|---|
| height | `0x00` | `u8` | strong structural candidate |
| weight | `0x01` | `u8` | strong structural candidate |
| player ID | `0x30` | `u32 little-endian` | live structural confirmation |
| commentary ID | `0x34` | `u32 little-endian` | CT/v5 candidate |
| player name | `0x38` | fixed string area | live structural confirmation |
| club shirt name | `0x75` | fixed string area | CT/v5 candidate |
| national shirt name | `0xB2` | fixed string area | CT/v5 candidate |
| nationality | `0x144` | `u16 little-endian` | CT/v5 candidate |
| market value | `0x174` | `i32 little-endian` | address/function user-validated; display scale pending |

### Fields physically present in EDIT but requiring an ML context for semantic promotion

| Field | Offset/bit range | Raw type | Required promotion evidence |
|---|---:|---|---|
| contract end | `0x138`, `0x13A`, `0x13B` | `u16,u8,u8` | UI correlation on multiple players and restart |
| affection | `0x13E` | `u8` | controlled value variation |
| max affection | `0x13F bit 0` | bit | paired before/after evidence |
| listed player | `0x13F bit 1` | bit | transfer-screen correlation |
| team-role level | `0x143 bits 6..7` | bits | UI correlation |
| stamina bar | `0x146 bits 0..6` | bits | runtime change correlation |
| blinking form arrow | `0x146 bit 7` | bit | runtime/UI correlation |
| current form arrow | `0x147 bits 0..2` | bits | runtime/UI correlation |
| unavailable days | `0x148` | `u8` | injury/suspension experiment |
| transfer listed | `0x14A bit 1` | bit | transfer-screen correlation |
| loan listed | `0x14A bit 2` | bit | transfer-screen correlation |
| team role | `0x150 bits 0..4` | bits | UI correlation |
| personality axes | `0x151..0x154` | `i8` | UI or controlled-delta evidence |
| impact | `0x155` | `u8` | controlled-delta evidence |
| annual salary | `0x15C` | `i32` | authoritative ML-copy discovery plus UI correlation |
| unknown | `0x178`, `0x179` | `i8` | remain unknown |

Base attributes, positions, playing style, COM styles, and skills in `0x03..0x29` are described comprehensively by the CT and v5 schema. They can be implemented as candidate readers after pure parser tests, but each write capability must be promoted separately because these are densely packed bitfields.

## Two-map decision and the meaning of 100%

The first implementation target is `EDIT_BASE`. It must reach two independent forms of complete coverage:

1. **Arena coverage:** every theoretical slot inside every accepted EDIT arena segment is classified as valid player, invalid/empty, hole, unreadable, partial, or ambiguous. Silent skips are forbidden.
2. **Territorial record coverage:** every byte from `0x000` through `0x17B` is represented in the profile as confirmed field, candidate field, unknown, padding/reserved, or shared bit container. Zero uncovered bytes and zero unjustified overlaps are allowed.

This does not require 100% semantic knowledge. `UNKNOWN` is a valid and necessary result. Player-count coverage, unique-ID coverage, and semantic-byte coverage must be reported as separate metrics with explicit denominators.

The complete decision, required artifacts, discovery algorithm, and later EDIT-versus-ML protocol are recorded in `edit-first-decision.md`.

## Required architecture

### 1. Versioned profile

Create a self-contained JSON profile under `files/pes2021/player-memory/`. It must contain:

- profile ID and schema version;
- supported executable identity/version evidence;
- record stride and field definitions;
- raw type, byte offset, bit start/length, signedness, endianness, and display transform;
- read validation bounds;
- field evidence status (`CONFIRMED`, `CANDIDATE`, `UNKNOWN`, `REFUTED`);
- independent read and write status;
- source hashes.

The first profile is `pes2021-player-edit-v1.json`. A separate `pes2021-player-ml-v1.json` is deferred until an accepted comparison between a no-ML capture and a loaded-ML capture exists.

Do not derive the live API dynamically from the external CT. The profile must be reviewed and shipped with Overmem.

### 2. Pure parser and validator

A pure `Pes2021PlayerRecordParser` should decode one 380-byte span. Bitfield writes need a separately tested read-modify-write codec that preserves all non-target bits.

Validation should be staged:

1. cheap bounds: height, weight, ID;
2. string plausibility and termination;
3. field-range checks;
4. neighbor-run score at `recordBase +/- n * 0x17C`;
5. cross-field/context diagnostics, without converting those diagnostics into unproved semantics.

### 3. Injection-free discovery

Preferred seed flow:

1. search a requested player ID as little-endian `Int32` in readable, non-executable private regions;
2. normalize each hit to `hit - 0x30`;
3. validate the record and score neighboring `0x17C` records;
4. choose only an unambiguous high-confidence anchor;
5. scan the containing region in blocks and cluster valid records by stride/residue;
6. report holes, duplicates, rejected candidates, partial reads, and coverage denominator.

An automatic catalog-seed mode can try several known control IDs, but a hit must never be accepted from ID alone.

Reuse the existing PES 2021 patterns where appropriate:

- `Pes2021PrivateRegionFilter`;
- `Pes2021RegionBlockReader`;
- fixture profile loading and validation;
- session cache keyed by attachment/process start/profile hash;
- atomic JSON output;
- explicit diagnostic/status payloads.

### 4. Player-specific read surface

Proposed operations:

- CLI `pes2021-find-player-anchor` / MCP `pes2021_find_player_anchor`;
- CLI `pes2021-scan-players` / MCP `pes2021_scan_players`;
- CLI `pes2021-get-player` / MCP `pes2021_get_player`;
- CLI `pes2021-query-players` / MCP `pes2021_query_players`;
- CLI `pes2021-export-players` / MCP `pes2021_export_players`.

Every result must include raw and display values where a transform exists, field evidence status, record context classification, session identity, and profile identity.

### 5. Guarded write surface

Generic `write_value` is insufficient for safe agent-driven player edits. Add a domain transaction:

1. `plan`: resolve targets, reject ambiguity, capture original bytes, calculate exact patch bytes, and return a hash-bound plan;
2. `apply`: require the plan hash, same process start/profile, expected current bytes, explicit write authorization, and limits;
3. `verify`: reread every byte and semantic field;
4. `rollback`: restore original bytes and verify restoration.

Safety defaults:

- dry run by default;
- no implicit writes from read commands;
- no batch write until single-record validation is accepted;
- no field write unless its independent write status is allowlisted;
- no `playerId`-only target when duplicates exist;
- reject stale process/session/profile tokens;
- compare-and-swap using expected raw bytes;
- preserve neighboring bitfields;
- bounded target count and explicit `--allow-batch`;
- audit manifest with original/new bytes, timestamps, addresses, profile hash, and outcome;
- atomic rollback artifact persisted before the first write.

Market value at `0x174` is the best first write pilot because it is an aligned 32-bit field and the user has already validated the zeroing behavior. Salary, form, and all packed fields remain blocked until their own evidence gates pass.

## Context classification is mandatory

The same player can appear in multiple process structures. The extension must classify, not assume:

- `EDIT_BASE_CANDIDATE`: structurally plausible base record without confirmed session context;
- `EDIT_BASE_CONFIRMED`: EDIT record found while the session is confirmed to have no ML loaded, then revalidated after restart;
- `MASTER_LEAGUE_CANDIDATE`: record with ML-like fields but not yet authoritative;
- `MASTER_LEAGUE_CONFIRMED`: correlated with visible ML values and restart/save behavior;
- `UI_OR_RUNTIME_CACHE`: transient or screen-specific copy;
- `UNKNOWN_CONTEXT`.

A structurally valid base record may be sufficient for height, name, and base attributes while being wrong for salary, form arrow, or contract. Write planning must require a context compatible with the field.

## Explicit non-conclusions

- `+0x12C` is not confirmed as team ID.
- `+0x12E` is not confirmed as league ID or `teamLiga`.
- salary scaling and persistence are not confirmed.
- current-form authority is not confirmed.
- a one-session address or constant relocation delta is not a stable pointer.
- the old JSON's v4-decoded semantic fields are not trustworthy.
- user-validated market-value zeroing does not automatically validate writes to any other field.
- no write was performed during this study.
- no Master League record family was discovered or validated in this session.

## Reproducible read-only commands used

Build/runtime commands were executed from `D:\git-lab-pes\overmem`:

```powershell
dotnet run --project src\Overmem.Cli -- scan-value --pid 27040 --value-kind Int32 --value 58120 --alignment 1 --max-results 100

dotnet run --no-build --project src\Overmem.Cli -- read --pid 27040 --address 0x7FF4D908F210 --value-kind Bytes --size 380

dotnet run --no-build --project src\Overmem.Cli -- scan-pattern --pid 27040 --pattern "B8 4A 00 D1 51 D4 F2 19 4A DE 33 0A 54 E9 74 09" --max-results 500
```

The PID, addresses, and example bytes are historical evidence for this run. Future work must rediscover them.

## Final feasibility classification

| Capability | Classification |
|---|---|
| Discover the EDIT player-record family without CE | `CONFIRMED_READ_ONLY` |
| Enumerate and territorially classify the full EDIT arena | `CONFIRMED_READ_ONLY_SINGLE_SESSION`; native implementation/restart validation pending |
| Create a separate ML mapping | `FEASIBLE`; no ML evidence captured in this session |
| Decode CT/v5 base fields | `FEASIBLE_WITH_FIELD_GATES` |
| Read market value | `HIGH_CONFIDENCE_CANDIDATE`; scale needs UI correlation |
| Write market value | `USER_VALIDATED_EXTERNALLY`; Overmem transaction not implemented |
| Read/write salary, contract, current form, role | `CANDIDATE`; authoritative ML copy and semantics pending |
| Safe bulk writes | `FEASIBLE_AFTER_TRANSACTION_AND_LIVE_GATES` |

Implementation should proceed through the gated packages in `implementation-packages.md`.
