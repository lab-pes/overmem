# P1 - Versioned player-memory profile

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: profile JSON + loader + record types + tests. No process memory access.

## Goal

Implement a validated, immutable profile for the `0x17C` EDIT-base player
record so that downstream packages (parser, scanner, CLI/MCP) can rely on
machine-enforceable offsets, types, contexts, and epistemic statuses.

## Changed files

### New production code

- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerProfile.cs`
  - Record types: `Pes2021PlayerProfile`, `Pes2021PlayerRecordLayout`,
    `Pes2021PlayerFieldDefinition`, `Pes2021PlayerBitField`,
    `Pes2021PlayerRegionFilter`, `Pes2021PlayerAnchorValidation`,
    `Pes2021PlayerLimits`, `Pes2021PlayerProfileSources`,
    `Pes2021PlayerRecordValidation`.
  - Enums: `Pes2021PlayerFieldType` (U8, I8, U16Le, U32Le, I32Le, FixedAscii,
    I8X4), `Pes2021PlayerTransform` (None, RawMul100Eur, TrimAsciiZ, Bitfield),
    `Pes2021PlayerEvidenceStatus` (Confirmed, Candidate, Unknown, Refuted),
    `Pes2021PlayerContext` (EditBaseCandidate, EditBaseConfirmed,
    MasterLeagueCandidate, MasterLeagueConfirmed, UiOrRuntimeCache,
    UnknownContext).
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerProfileException.cs`
  - `Pes2021PlayerProfileException` mirrors the fixture exception type. Carries
    `Code` (default `PES2021_PLAYER_PROFILE_INVALID`).
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerProfileLoader.cs`
  - Static `LoadFromFile(path)` and `LoadFromBytes(bytes, sourcePath)`. SHA-256
    is computed over the original JSON bytes. Validates:
    - stride must be exactly 380 (`0x17C`);
    - startOffset in `[0, stride)`;
    - every field offset+width stays inside the stride;
    - type/width/signedness/endianness/transform combinations match;
    - no overlapping fields unless both declare `sharedBitfield: true`;
    - bit range fits inside byte container (bitStart+bitLength ≤ width*8);
    - evidence status is one of the four closed labels;
    - context labels are one of the six closed values;
    - duplicate field names rejected.
  - Every rejection throws `Pes2021PlayerProfileException` with code
    `PES2021_PLAYER_PROFILE_INVALID`.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerProfileDefaults.cs`
  - `BuildBuiltIn()` mirrors the shipped JSON so callers can run without any
    file on disk.
  - `GetOrLoad()` tries `AppContext.BaseDirectory/profiles/pes2021-player-record.json`
    then `Environment.CurrentDirectory/profiles/pes2021-player-record.json`
    before falling back to the built-in.
  - `Override(profile)` lets tests pin a different profile.

### New profile artifact

- `files/pes2021/player-memory/pes2021-player-record-v1.json`
  - 27 fields, each with offset, width, type, signedness, endianness,
    transform, read/write status, valid contexts, sharedBitfield, and notes.
  - All candidate fields default to `CANDIDATE` (no implicit `CONFIRMED`).
  - `unknown_12c`, `unknown_12e`, `unknown_178`, `unknown_179` carry
    neutral names and `UNKNOWN` read status.
  - `marketValue` is the only field whose `validContexts` allow
    `MASTER_LEAGUE_CONFIRMED` writes (still `CANDIDATE`).
  - `sources` block carries the CT path/SHA-256 and the schema_v5 Lua SHA-256
    so the profile is traceable to the studies.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerProfileTests.cs`
  - Built-in profile loads with expected stride and contains the four neutral
    unknown fields.
  - Built-in profile exposes no unjustified overlap.
  - Loader rejects: non-matching stride, offset outside stride, bit overflow,
    duplicate field key, unjustified overlap, unsupported type, bad evidence
    status, invalid transform.
  - Round-trip serialize/reload preserves semantic profile.
  - Shipped JSON loads with the documented profile ID and SHA-256.
  - Shipped JSON contains all 27 mandatory fields from the feasibility study.
  - Field-table renderer produces a Markdown table covering every field.

## Decisions

- **Stride is hard-coded to 380.** The loader refuses any other value. This
  enforces the EDIT-base record size from the study.
- **`writeStatus` is an evidence status, not a "blocked" sentinel.** Every
  field that is not yet permitted writes uses `CANDIDATE`. Promotion to
  `CONFIRMED` happens per-field per P9.
- **`sharedBitfield: true` is the only escape hatch for overlapping bytes.**
  Each shared container must declare its sub-bits with `bitStart` and
  `bitLength`. Without a declaration, overlapping fields are rejected.
- **Type/width/signedness/endianness are locked together.** A `u32le` field
  must have width 4, signedness `unsigned`, and endianness `little`. This
  catches transcription errors in the profile JSON.
- **The shipped JSON path is the source of truth; the built-in is a fallback.**
  The JSON lives at `files/pes2021/player-memory/pes2021-player-record-v1.json`
  and is not embedded into the assembly.

## Limitations

- The profile does not yet include `0x03..0x29` packed base attributes. They
  remain future work because each requires per-field promotion evidence (P9).
- The schema-version label uses dot separators (`pes2021.player-record.v1`)
  while the fixture profile uses dash separators (`pes2021.fixture-profile.v1`).
  Both are valid; aligning them is documentation-only future work.

## Rollback

Reverting the four production files, the JSON profile, and the test file
restores the repository to its pre-P1 state. No other file is touched.
