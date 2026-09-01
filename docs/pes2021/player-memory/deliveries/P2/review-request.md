# P2 - review request

Reviewer: Codex
Scope: pure parser + bitfield codec + validator + tests.

## Acceptance gates (from implementation-packages.md P2)

- [x] Parser consumes exactly one `ReadOnlySpan<byte>` of at least 380 bytes.
- [x] Decoder returns per-field raw value, optional display value, evidence
      status, and decode warnings.
- [x] Validator produces a score and reasons; never returns only a boolean.
- [x] Cheap checks precede expensive string/neighbor checks.
- [x] Bitfield read-modify-write preserves all non-target bits.
- [x] Pure tests need no Windows process.
- [x] Parser agrees with CT/v5 offsets, not v4 semantic output.
- [x] Mutation tests demonstrate non-target bytes/bits stay identical.

## Review questions

1. Does invalid input fail without out-of-range reads? Yes: the parser checks
   `buffer.Length < profile.Stride` first and rejects with
   `BUFFER_TOO_SMALL`. Each field decode additionally checks
   `field.Offset + field.Width <= span.Length`.
2. Can warnings coexist with a structurally valid record? Yes:
   `clubShirtName` and `nationalShirtName` produce non-fatal warnings when they
   lack an embedded terminator. The primary `playerName` is the only field that
   can reject the record for missing terminator.
3. Is a packed-field patch byte-for-byte minimal? Yes: every Write call returns
   a fresh copy; the test `Write_PreservesAllOtherBytesInRecord` proves that
   only the target container byte changes and every other byte is byte-identical
   to the input. `WriteMany` applies patches in the supplied order.
4. Are evidence statuses preserved honestly? Yes: the parser surfaces each
   field's `evidenceStatus` exactly as the profile declares it. No field is
   silently promoted to `CONFIRMED`.
5. Does the validator return reasons for every decision? Yes: both `Validate`
   and `ValidateWithNeighbors` return a `Reasons` list with one entry per
   check. `Score` and `MaxScore` are exposed so callers can decide how strict
   they want to be.

## Reproduce

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerParserTests|FullyQualifiedName~Pes2021PlayerBitfieldCodecTests"
```