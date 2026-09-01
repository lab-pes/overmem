# P0 - review request

Reviewer: Codex
Scope: docs + contract tests only.

## Acceptance gates (from implementation-packages.md P0)

- [x] zero production memory code changed;
- [x] contracts distinguish candidate semantics from confirmed semantics;
- [x] no absolute live address appears as a default or constant;
- [x] source manifest enumerates sources with hashes and no runtime dependency.

## Review questions

1. Can a consumer distinguish raw, display, and evidence status from the wire
   examples alone (without reading the JSON Schema prose)? Yes: every example
   uses `evidenceStatus` in `SCREAMING_SNAKE_CASE` and pairs `raw` with
   `display` whenever a transform exists.
2. Can an ambiguous ID be represented without silently choosing one record?
   Yes: the `query` example carries `ambiguous: true` plus a `results` array
   with two distinct fingerprints. The contract never selects first-match.
3. Is every source/provenance statement reproducible? Yes: the SHA-256 of the
   CT copy at `files\PES 2021 - v21.1.0.CT` is recomputed at test time and
   asserted equal to the expected hash in `source-manifest.json`.
4. Are the 12 error codes sufficient for the downstream P2-P10 packages, or
   should any be split/added now? Current list covers all the codes
   `implementation-packages.md` requires.
5. Do the wire examples leak any session-local addresses that should be
   sanitized further? They use `0x0` and `processId: 1` placeholders; the
   test `WireContracts_DoNotEmbedAbsoluteLiveAddresses` enforces this.

## Reproduce

```powershell
dotnet build tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerContractsTests|FullyQualifiedName~Pes2021SourceManifestTests"
```
