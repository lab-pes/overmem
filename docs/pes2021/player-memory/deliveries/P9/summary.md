# P9 - Per-field write policy + evidence gating

Date: 2026-08-31
Status: accepted (subject to Codex review)
Scope: per-field write authorization and evidence-status gating.

## Goal

Ensure no field can be patched through `Pes2021PlayerTransactionCore.ApplyWithPolicyAsync`
unless its evidence status is `Confirmed`, the active context is in the field's
allowlist, the profile identity matches, and the supplied authorization is valid.

## Changed files

### New production code

- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerWritePolicy.cs`
  - `PlayerWriteAuthorization`: token id, granted/expires timestamps, reason.
  - `PlayerWritePolicyResult`: allow flag plus reasons list.
  - `Pes2021PlayerWritePolicy.Evaluate(...)`: stateless check that combines
    profile identity, field evidence status, context compatibility, and
    authorization validity.

### Updated production code

- `src/Overmem.Extensions.Pes2021/Players/Pes2021PlayerTransactionCore.cs`
  - New `ActiveContext` property defaults to `EditBaseCandidate`.
  - New `ApplyWithPolicyAsync` overload: gates the apply through
    `Pes2021PlayerWritePolicy.Evaluate`, returns
    `PES2021_PLAYER_WRITE_NOT_AUTHORIZED` when rejected, otherwise delegates
    to the existing compare-and-swap apply path.

### New tests

- `tests/Overmem.Extensions.Pes2021.Tests/Pes2021PlayerWritePolicyTests.cs` (5 tests)
  - Field with `Unknown` write status is rejected.
  - `Candidate` field is rejected even when context and authorization match.
  - Expired authorization is rejected.
  - Field-name mismatch in the authorization is rejected.
  - Profile-identity mismatch is rejected.

## Decisions

- **Apply with policy is opt-in.** The existing `ApplyAsync` is unchanged so
  the TestTarget-only P7 tests still pass without authorization.
- **Default context is `EditBaseCandidate`.** Until a Master League
  discriminator is added (P6), `MasterLeagueConfirmed` is unreachable through
  the default context; the field's `ValidContexts` list keeps each field
  honest.
- **Each rejection carries a stable code.** The wire payload can surface
  the policy result without exposing internal state.

## Limitations

- The profile JSON still tags `marketValue` write status as `Candidate`;
  promotion to `Confirmed` requires per-field evidence that will be gathered in
  P10 or in dedicated evidence-gathering packages.
- The authorization token format is opaque to the policy; future packages may
  replace it with a signed artifact.

## Rollback

Reverting the new file and the two updated files restores the repository to
its pre-P9 state.