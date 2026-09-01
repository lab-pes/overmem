# PES 2021 player-memory error codes (P0)

Date: 2026-08-31
Status: stable codes for P0+; new codes require a review of this document.

Each code is the canonical string returned by CLI/MCP/JSON payloads. The full exception message has the shape `[CODE] human-readable detail`.

| Code | Where raised | Meaning |
|---|---|---|
| `PES2021_PLAYER_PROFILE_INVALID` | `Pes2021PlayerProfileLoader` | The profile JSON is missing required fields, contains out-of-range offsets, overlapping fields that are not declared `sharedBitfield`, unknown types, or any other static validation failure. |
| `PES2021_PLAYER_ANCHOR_NOT_FOUND` | `Pes2021PlayerAnchorFinder` | No candidate record matched the requested player ID after neighbor scoring. |
| `PES2021_PLAYER_ANCHOR_AMBIGUOUS` | `Pes2021PlayerAnchorFinder` | Two or more candidates tied at the top score and normalized to different competition/record blocks. Caller must supply more information. |
| `PES2021_PLAYER_RECORD_INVALID` | `Pes2021PlayerRecordParser` / `Pes2021PlayerRecordValidator` | The 380-byte span failed cheap or expensive validation. The exception message must list the reason(s). |
| `PES2021_PLAYER_ID_AMBIGUOUS` | `Pes2021PlayerQuery` | A `playerId` query returned more than one match. The caller must narrow by `(recordAddress, fingerprint)`. |
| `PES2021_PLAYER_CONTEXT_INCOMPATIBLE` | `Pes2021PlayerPatchPlanner` | A write was requested against a record whose classified context does not allow the target field (for example, salary in `EDIT_BASE`). |
| `PES2021_PLAYER_STALE_SESSION` | `Pes2021PlayerPatchExecutor` | The current `processId`/`processStartedAtUtc`/`profileSha256` no longer match the snapshot held by the plan. |
| `PES2021_PLAYER_WRITE_NOT_AUTHORIZED` | `Pes2021PlayerPatchExecutor` | The call did not pass a non-expired authorization token, or the field is not in the policy allowlist. |
| `PES2021_PLAYER_EXPECTED_BYTES_MISMATCH` | `Pes2021PlayerPatchExecutor` | Compare-and-swap failed: the bytes at the patch address do not match `oldBytes` from the plan. |
| `PES2021_PLAYER_VERIFY_FAILED` | `Pes2021PlayerPatchExecutor` | After applying the patch, the reread bytes do not match `newBytes` or neighbor bits changed. |
| `PES2021_PLAYER_ROLLBACK_FAILED` | `Pes2021PlayerPatchRollback` | The rollback artifact cannot be restored; the record is left in an inconsistent state. |

## Conventions

- Codes are stable strings. They never include whitespace or punctuation other than `_`.
- A new code is added only when no existing code fits. Refactoring an error class to use a new code requires updating this document, the wire-contract references, and the corresponding exception class in the same change.
- CLI/MCP surfaces must surface the `code` field unchanged. The human message may be localized; the code is the contract.
