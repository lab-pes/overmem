# P2 - Pure record parser, validator, and bitfield codec

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: pure logic, no process gateway.

## Goal

Decode and encode player records deterministically without a process gateway so
P3 (discovery), P4 (catalog), and P7 (transactions) can build on a tested core.

## Changed files

### New production code

- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerRecordRejectionReasons.cs`
  - Stable rejection reasons: `BUFFER_TOO_SMALL`, `HEIGHT_OUT_OF_RANGE`,
    `WEIGHT_OUT_OF_RANGE`, `PLAYER_ID_OUT_OF_RANGE`, `NAME_UNTERMINATED`,
    `NAME_EMPTY`, `NAME_CONTAINS_CONTROL_BYTES`,
    `CLUB_SHIRT_NAME_UNTERMINATED`, `NATIONAL_SHIRT_NAME_UNTERMINATED`,
    `MARKET_VALUE_IMPLAUSIBLE`, `NEIGHBOR_STRIDE_MISMATCH`, `PARTIAL_READ`.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerModels.cs`
  - `DecodedFieldValue`, `DecodedPlayerRecord`, `PlayerRecordParseResult`,
    `PlayerRecordValidationResult`. All carry raw/display + evidence status and
    preserve the original 380 bytes plus SHA-256.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerBitfieldCodec.cs`
  - `Read`, `Write`, `WriteMany`, `FieldBytesUnchanged`. Pure functions over
    `ReadOnlySpan<byte>`. Writes always return a fresh copy; never mutate the
    caller buffer. Validates bit ranges and rejects overflow.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerRecordParser.cs`
  - `TryParse(buffer, recordIndex, address, profile)` decodes one 380-byte span.
    Cheap validation first (height, weight, playerId), then string checks for
    `playerName`, then secondary name fields. `clubShirtName` and
    `nationalShirtName` produce non-fatal warnings when they lack an embedded
    terminator. Raw record and SHA-256 are preserved on the snapshot.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerRecordValidator.cs`
  - `Validate(record, profile)` and `ValidateWithNeighbors(...)`. Score-based:
    returns an `Accept` boolean plus the full reasons list. Never returns only a
    boolean. Neighbor scoring adds `forward_neighbors_present` and
    `backward_neighbors_present`.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerParserTests.cs` (15 tests)
  - Decodes the five control records from `feasibility-study.md` (IDs 58118-58122).
  - Market value `rawMul100Eur` produces display = raw * 100.
  - Rejects: buffer too small, height/weight/playerId out of range, unterminated
    or empty player name, name with control bytes, implausible market value.
  - SHA-256 of the raw record is stable across repeated parses.
  - Signed `i8` and `i32` boundary values decode without overflow.
  - Default evidence status is `CANDIDATE`/`UNKNOWN`, never `CONFIRMED`.
  - Validator happy path: `maxScore == score` and every contributing reason is
    present. Validator with neighbors adds the two neighbor reasons.
- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerBitfieldCodecTests.cs` (10 tests)
  - Reads single-bit and multi-bit fields from a packed byte container.
  - Writes preserve every other bit in the container and every other byte in
    the record.
  - Writes never mutate the input buffer.
  - Writes refuse values larger than the bit capacity.
  - `WriteMany` applies patches left-to-right and produces the final state.
  - Round-trip read-after-write for all 8 values of a 3-bit field.
  - `FieldBytesUnchanged` distinguishes identical from differing byte runs.

## Decisions

- **Cheap checks before expensive checks.** Cheap validation (height, weight,
  playerId, marketValue range) runs before any string scan. If the cheap checks
  fail, the parser returns immediately.
- **Non-fatal warnings stay warnings.** `clubShirtName` and `nationalShirtName`
  produce non-fatal warnings when they lack an embedded NUL terminator. The
  primary `playerName` field is the only one that can reject the record for
  missing terminator.
- **Bitfield writes return fresh copies.** The codec never mutates the caller
  buffer. This keeps the read path (parser) and the write path (executor)
  immutable from the caller's perspective.
- **Score-based validator.** The validator returns a `PlayerRecordValidationResult`
  with `Accept`, `Score`, `MaxScore`, and `Reasons`. Callers can audit every
  reason and never see a silent rejection.
- **Evidence status is carried but not enforced.** The parser surfaces each
  field's `evidenceStatus` from the profile so wire payloads can render
  `CONFIRMED`/`CANDIDATE`/`UNKNOWN` honestly. The parser does not silently
  promote `CANDIDATE` to `CONFIRMED`.

## Limitations

- Bitfield writes are not yet wired into a transaction layer (P7).
- The parser does not decode the `0x03..0x29` packed base attributes — those
  fields are not yet present in the profile. Each requires per-field promotion
  evidence (P9).
- I8X4 (`personalityAxes`) is parsed as a single primary field whose raw value
  is the first byte; the remaining three bytes are preserved inside the raw
  record and accessible via `RawRecord`. Future code can decode the full 4-byte
  array without changing the wire shape.

## Rollback

Reverting the five production files and the two test files restores the
repository to its pre-P2 state. No process memory code was added.