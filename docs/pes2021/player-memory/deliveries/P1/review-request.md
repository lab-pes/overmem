# P1 - review request

Reviewer: Codex
Scope: profile JSON + loader + record types + tests.

## Acceptance gates (from implementation-packages.md P1)

- [x] Record stride exactly 380 (`0x17C`).
- [x] Field definitions include offset, width/type, endianness, signedness,
      bit start/length, display offset/scale, read status, write status,
      valid contexts, and evidence notes.
- [x] Reject overlaps unless explicitly marked as a shared bitfield container.
- [x] Reject fields outside the record.
- [x] Reject invalid transforms and bit ranges.
- [x] Source hash metadata present and matches P0.
- [x] Default status for unproved fields is `CANDIDATE` or `UNKNOWN`, never
      implicit `CONFIRMED`.
- [x] `unknown_12c`, `unknown_12e`, `unknown_178`, `unknown_179` stored under
      neutral names.

## Review questions

1. Does the profile encode independent read and write promotion? Yes:
   `Pes2021PlayerFieldDefinition` carries both `ReadStatus` and `WriteStatus`
   plus the per-context allowlist.
2. Are context requirements machine-enforceable? Yes: `validContexts` is a
   required array on every field; empty arrays are rejected.
3. Are byte/bit overlaps intentional and tested? Yes: every overlapping byte
   block has `sharedBitfield: true` and a `bits` array with explicit
   `bitStart` and `bitLength`. The loader rejects overlap when both fields
   are not marked shared.
4. Does the loader correctly handle `fixedAscii` with `trimAsciiZ`? Yes: the
   parser rejects any other transform for `fixedAscii`, and `fixedAscii`
   requires `signedness: n/a` and `endianness: n/a`.
5. Is the shipped JSON shipped alongside the binary? It lives under
   `files/pes2021/player-memory/pes2021-player-record-v1.json`. The runtime
   looks for `profiles/pes2021-player-record.json` under the working
   directory or `AppContext.BaseDirectory`; a future package (P5) will copy
   the file into the publish directory if needed.

## Reproduce

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerProfileTests"
```
