# P7 - Player-memory transaction core (TestTarget only)

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: write transactions restricted to `Overmem.TestTarget` and `dotnet` via a process-name allowlist.

## Goal

Implement plan / apply / verify / rollback against the in-memory player record
without ever touching a real PES2021.exe. Default allowlist is restricted to the
in-repo test harness so a misconfigured call cannot leak into production.

## Changed files

### New production code

- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerTransactionModels.cs`
  - `PlayerRollbackArtifact`: serializable snapshot of the original field bytes,
    plan id, raw old/new, SHA-256, and creation time.
  - `PlayerPatchPlan`: returned by `PlanAsync`; carries the plan id, the old/new
    hex of the field, the rollback path, and the session id.
  - `PlayerApplyResult`: outcome plus pre/post hashes; non-`applied` outcomes
    include a stable error code.
  - `PlayerRollbackResult`: outcome plus bytes restored and raw restored.
- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerTransactionCore.cs`
  - Process-name allowlist default: `Overmem.TestTarget`, `dotnet`. PES2021 is
    not in the allowlist; an explicit override is required to write against it.
  - `PlanAsync` reads the field, computes old/new hex, writes a rollback
    artifact atomically, and returns the plan.
  - `ApplyAsync` runs compare-and-swap; if the bytes on disk do not match the
    plan's old hex, the apply is rejected with
    `PES2021_PLAYER_EXPECTED_BYTES_MISMATCH`.
  - `RollbackAsync` verifies the post-apply state still matches the plan before
    restoring bytes; failures return `rollback_failed`.
  - All paths that touch memory use the existing `IProcessMemoryGateway`.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerTransactionTests.cs` (5 tests)
  - Plan + apply + verify + rollback round-trip via `FakeProcessMemoryGateway`.
  - Apply rejects when the bytes on disk do not match the plan's old hex.
  - Rollback restores the original bytes after a successful apply.
  - Plan refuses any process name that is not in the allowlist.
  - Rollback artifact is written atomically through
    `Pes2021AtomicFileWriter`.

## Decisions

- **Default allowlist excludes PES2021.** A future package (P8) must
  explicitly opt in to writing PES2021, after a separate authorization flow.
- **Rollback artifact is required.** `PlanAsync` throws when the caller does
  not provide a path; the artifact is the recovery hook and never optional.
- **Compare-and-swap is enforced at apply and rollback time.** Both methods
  verify the bytes match the expected state before any write; a mismatch
  aborts the operation without touching memory.
- **No CI/CD write path is exposed.** This delivery does not register any
  CLI/MCP surface for transactions; the core is library-only and reachable
  through unit tests.

## Limitations

- The transaction core targets single-byte fields with width 1/2/4 and scalar
  bitfields through the codec; multi-byte strings are out of scope.
- The allowlist is in-code; future work should externalize it through the
  profile JSON or a configuration file.
- The transaction core is library-only; CLI/MCP integration is deferred to a
  separate package that requires explicit authorization.

## Rollback

Reverting the two production files and the one test file restores the
repository to its pre-P7 state.