# P7 - review request

Reviewer: Codex
Scope: plan / apply / verify / rollback restricted to the test harness.

## Acceptance gates (from implementation-packages.md P7)

- [x] Apply performs compare-and-swap before writing; mismatch returns
      `PES2021_PLAYER_EXPECTED_BYTES_MISMATCH` without touching memory.
- [x] Verify confirms the reread bytes after the write; mismatch returns
      `PES2021_PLAYER_VERIFY_FAILED`.
- [x] Rollback verifies the post-apply state before restoring; failure returns
      `PES2021_PLAYER_ROLLBACK_FAILED`.
- [x] Default allowlist excludes PES2021; an override is required to write
      against it.
- [x] Rollback artifact is written atomically through `Pes2021AtomicFileWriter`.
- [x] Library-only; no CLI/MCP surface in this delivery.

## Review questions

1. Could an operator accidentally write PES2021 with the default core? No:
   `EnsureProcessAllowed` throws when the process name is empty or absent
   from the allowlist. PES2021 is not in the default list.
2. Is the rollback path resilient to mid-session crashes? Yes: the artifact is
   flushed to disk before the plan is returned. A subsequent process restart
   can read it back and restore the bytes.
3. Does the apply path ever write without verifying first? No: the
   compare-and-swap runs before the write, and a verify runs after.
4. Is the rollback artifact format stable? Yes: it is a fixed-shape record with
   `OriginalHex`, `OriginalSha256`, `RawOld`, `RawNew`, and timing fields. New
   fields can be added in future packages without breaking older readers
   because `System.Text.Json` ignores unknown JSON properties.
5. Could a future CLI/MCP integration accidentally target PES2021? Not without
   an explicit authorization step that re-extends the allowlist; the core
   refuses unknown process names by design.

## Reproduce

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerTransactionTests"
```