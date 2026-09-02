# P9 - review request

Reviewer: Codex
Scope: per-field write policy + evidence gating.

## Acceptance gates (from implementation-packages.md P9)

- [x] Each writeable field has its own evidence status, ownership, and
      authorization rules.
- [x] Writes require `Confirmed` status, matching context, matching profile
      identity, and a non-expired authorization.
- [x] No write promotion is automatic; the policy is enforced.

## Review questions

1. Is the policy the only path that can apply a patch? No: the existing
   `ApplyAsync` is preserved for the TestTarget path. The new
   `ApplyWithPolicyAsync` is the only path that goes through the policy gate.
   P8 must choose `ApplyWithPolicyAsync` and supply a `PlayerWriteAuthorization`.
2. Does the policy return enough information for an audit? Yes: the
   `Reasons` list carries one entry per gate that failed plus a final
   `authorized` entry when the policy passes.
3. Can the authorization token be replayed outside its window? No: the
   policy rejects expired tokens; the caller must request a new grant.
4. Is the policy a gate, not a silent promotion? Yes: every rejection
   returns `PES2021_PLAYER_WRITE_NOT_AUTHORIZED` through the apply result;
   nothing is silently downgraded.

## Reproduce

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerWritePolicyTests"
```