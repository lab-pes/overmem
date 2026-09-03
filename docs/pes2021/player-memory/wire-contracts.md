# PES 2021 player-memory wire contracts (P0 draft)

Date: 2026-08-31
Status: draft contracts; implementation deferred to P2-P5
Schema family: `pes2021.player-memory.v1`
Naming: `camelCase` properties, `SCREAMING_SNAKE_CASE` enums (matches the existing fixture wire schema `pes2021.competition-fixtures.v1`).

## Global rules

1. Every payload that references a process address must carry `attachmentId`, `processId`, `processStartedAtUtc`, `profileId`, `profileVersion`, and `profileSha256`. Session-local addresses are never reused across restarts.
2. Every field decoded from a player record is returned as both `raw` (the bytes as stored) and `display` (the user-facing value, when a transform exists). Missing transform means `display` is `null` and `raw` is the only authoritative value.
3. Every decoded field carries `evidenceStatus`: `CONFIRMED`, `CANDIDATE`, `UNKNOWN`, or `REFUTED`.
4. Every player record carries `context`: `EDIT_BASE_CANDIDATE`, `EDIT_BASE_CONFIRMED`, `MASTER_LEAGUE_CANDIDATE`, `MASTER_LEAGUE_CONFIRMED`, `UI_OR_RUNTIME_CACHE`, or `UNKNOWN_CONTEXT`.
5. Querying by `playerId` when duplicates exist returns all matches plus an `ambiguous: true` flag. The API must never silently select one record.
6. No payload contains an absolute live process address as a default. `recordAddress` is always required and always session-local.

## Player anchor

Used by `pes2021-find-player-anchor` / `pes2021_find_player_anchor`.

```json
{
  "schemaVersion": "pes2021.player-memory.v1",
  "kind": "player_anchor",
  "session": {
    "attachmentId": "00000000-0000-0000-0000-000000000000",
    "processId": 27040,
    "processStartedAtUtc": "2026-08-30T19:12:33.000Z",
    "profileId": "pes2021-player-edit-v1",
    "profileVersion": "1.0.0",
    "profileSha256": "<sha256-of-profile-json>"
  },
  "anchor": {
    "recordAddress": "0x7FF4D908F210",
    "playerId": 58120,
    "fingerprint": "Piero Hincapie",
    "context": "EDIT_BASE_CONFIRMED",
    "evidenceStatus": "CONFIRMED",
    "score": 17,
    "reasons": ["id_match", "neighbor_run_forward_4", "neighbor_run_backward_2"]
  },
  "candidates": [
    {
      "recordAddress": "0x7FF4D908F210",
      "playerId": 58120,
      "score": 17,
      "reasons": ["id_match", "neighbor_run_forward_4"]
    }
  ],
  "diagnostics": {
    "regionsEnumerated": 412,
    "regionsAccepted": 38,
    "regionsRejected": 374,
    "bytesRequested": 33554432,
    "bytesRead": 33554432,
    "readCalls": 33,
    "elapsedMs": 184.2,
    "rejectionReasons": {"partial_read": 0, "stride_mismatch": 2, "id_outside_record": 1}
  },
  "warnings": []
}
```

## Player scan

Used by `pes2021-scan-players` / `pes2021_scan_players`.

```json
{
  "schemaVersion": "pes2021.player-memory.v1",
  "kind": "player_scan",
  "session": {
    "attachmentId": "00000000-0000-0000-0000-000000000000",
    "processId": 27040,
    "processStartedAtUtc": "2026-08-30T19:12:33.000Z",
    "profileId": "pes2021-player-edit-v1",
    "profileVersion": "1.0.0",
    "profileSha256": "<sha256-of-profile-json>"
  },
  "arena": {
    "context": "EDIT_BASE_CANDIDATE",
    "segments": [
      {
        "startAddress": "0x7FF4D9000000",
        "stopAddress": "0x7FF4DA140000",
        "stride": 380,
        "theoreticalSlots": 30001,
        "populatedSlots": 25005,
        "emptyReservedSlots": 4996,
        "unaccountedSlots": 0
      }
    ]
  },
  "summary": {
    "theoreticalSlots": 30001,
    "populatedSlots": 25005,
    "emptyReservedSlots": 4996,
    "unaccountedSlots": 0,
    "uniqueRawPlayerIds": 25005,
    "duplicatePlayerIds": 0,
    "recordsDecoded": 25005,
    "recordsAccepted": 25005,
    "recordsRejected": 0,
    "ambiguousResolutions": 0,
    "holes": 0,
    "partialReads": 0,
    "historicalComparison": {
      "historicalExportRows": 23253,
      "historicalUniqueIds": 23250,
      "historicalIdsPresentLive": 23250,
      "historicalIdsAbsentLive": 0,
      "liveRawIdsAbsentHistorically": 1755
    }
  },
  "players": [
    {
      "recordAddress": "0x7FF4D908F210",
      "playerId": 58120,
      "fingerprint": "Piero Hincapie",
      "context": "EDIT_BASE_CANDIDATE",
      "rawRecordSha256": "<sha256-of-380-bytes>",
      "fields": [
        {
          "name": "height",
          "raw": 184,
          "display": null,
          "evidenceStatus": "CONFIRMED"
        },
        {
          "name": "marketValue",
          "raw": 500000,
          "display": 50000000,
          "evidenceStatus": "CANDIDATE",
          "transform": "rawMul100Eur"
        },
        {
          "name": "unknown_12c",
          "raw": 65535,
          "display": null,
          "evidenceStatus": "UNKNOWN"
        }
      ]
    }
  ],
  "diagnostics": {
    "regionsEnumerated": 412,
    "regionsAccepted": 38,
    "regionsRejected": 374,
    "bytesRequested": 33554432,
    "bytesRead": 33554432,
    "readCalls": 33,
    "elapsedMs": 412.7,
    "rejectionReasons": {"height_out_of_range": 0, "weight_out_of_range": 0, "name_unterminated": 0}
  },
  "warnings": []
}
```

## Player snapshot (single record)

Used by `pes2021-get-player` / `pes2021_get_player`.

```json
{
  "schemaVersion": "pes2021.player-memory.v1",
  "kind": "player_snapshot",
  "session": {
    "attachmentId": "00000000-0000-0000-0000-000000000000",
    "processId": 27040,
    "processStartedAtUtc": "2026-08-30T19:12:33.000Z",
    "profileId": "pes2021-player-edit-v1",
    "profileVersion": "1.0.0",
    "profileSha256": "<sha256-of-profile-json>"
  },
  "player": {
    "recordAddress": "0x7FF4D908F210",
    "playerId": 58120,
    "fingerprint": "Piero Hincapine",
    "context": "EDIT_BASE_CONFIRMED",
    "rawRecordSha256": "<sha256-of-380-bytes>",
    "fields": [
      {"name": "height", "raw": 184, "display": null, "evidenceStatus": "CONFIRMED"},
      {"name": "weight", "raw": 74, "display": null, "evidenceStatus": "CONFIRMED"},
      {"name": "playerName", "raw": "Piero Hincapie", "display": null, "evidenceStatus": "CONFIRMED"},
      {"name": "marketValue", "raw": 500000, "display": 50000000, "evidenceStatus": "CANDIDATE"}
    ]
  },
  "warnings": []
}
```

## Player query (by ID, possibly multiple)

Used by `pes2021-query-players` / `pes2021_query_players`.

```json
{
  "schemaVersion": "pes2021.player-memory.v1",
  "kind": "player_query",
  "session": {
    "attachmentId": "00000000-0000-0000-0000-000000000000",
    "processId": 27040,
    "processStartedAtUtc": "2026-08-30T19:12:33.000Z",
    "profileId": "pes2021-player-edit-v1",
    "profileVersion": "1.0.0",
    "profileSha256": "<sha256-of-profile-json>"
  },
  "query": {"playerId": 58120},
  "ambiguous": false,
  "results": [
    {
      "recordAddress": "0x7FF4D908F210",
      "playerId": 58120,
      "fingerprint": "Piero Hincapie",
      "context": "EDIT_BASE_CONFIRMED"
    }
  ]
}
```

When `ambiguous: true` (duplicate IDs exist), `results` contains every match and the caller must choose by `(recordAddress, fingerprint)`. The API never picks one for the caller.

## Patch plan

Used by `pes2021-plan-player-patch` / `pes2021_plan_player_patch`. Read-only.

```json
{
  "schemaVersion": "pes2021.player-memory.v1",
  "kind": "patch_plan",
  "planId": "<sha256-of-plan-bytes>",
  "session": {
    "attachmentId": "00000000-0000-0000-0000-000000000000",
    "processId": 27040,
    "processStartedAtUtc": "2026-08-30T19:12:33.000Z",
    "profileId": "pes2021-player-edit-v1",
    "profileVersion": "1.0.0",
    "profileSha256": "<sha256-of-profile-json>"
  },
  "target": {
    "recordAddress": "0x7FF4D908F210",
    "playerId": 58120,
    "expectedFingerprint": "Piero Hincapie",
    "context": "EDIT_BASE_CONFIRMED"
  },
  "patches": [
    {
      "field": "marketValue",
      "offset": 372,
      "oldBytes": "20 A1 07 00",
      "newBytes": "00 00 00 00",
      "rawOld": 500000,
      "rawNew": 0,
      "displayOld": 50000000,
      "displayNew": 0
    }
  ],
  "rollback": {
    "rollbackId": "<sha256-of-rollback-bytes>",
    "artifactPath": "<absolute-path-to-rollback-artifact>"
  },
  "warnings": []
}
```

## Apply result

Used by `pes2021-apply-player-patch` / `pes2021_apply_player_patch`. Carries the verification outcome.

```json
{
  "schemaVersion": "pes2021.player-memory.v1",
  "kind": "apply_result",
  "planId": "<sha256-of-plan-bytes>",
  "session": {
    "attachmentId": "00000000-0000-0000-0000-000000000000",
    "processId": 27040,
    "processStartedAtUtc": "2026-08-30T19:12:33.000Z",
    "profileId": "pes2021-player-edit-v1",
    "profileVersion": "1.0.0",
    "profileSha256": "<sha256-of-profile-json>"
  },
  "outcome": "applied",
  "verification": {
    "rawNew": 0,
    "expectedRawNew": 0,
    "rawBeforeSha256": "<sha256>",
    "rawAfterSha256": "<sha256>",
    "neighborBitsUnchanged": true
  },
  "warnings": []
}
```

Possible `outcome` values: `applied`, `dry_run`, `expected_bytes_mismatch`, `verify_failed`, `rollback_invoked`, `rejected`. Every non-`applied` outcome must carry a `code` from `error-codes.md`.

## Rollback result

Used by `pes2021-rollback-player-patch` / `pes2021_rollback_player_patch`.

```json
{
  "schemaVersion": "pes2021.player-memory.v1",
  "kind": "rollback_result",
  "planId": "<sha256-of-plan-bytes>",
  "rollbackId": "<sha256-of-rollback-bytes>",
  "session": {
    "attachmentId": "00000000-0000-0000-0000-000000000000",
    "processId": 27040,
    "processStartedAtUtc": "2026-08-30T19:12:33.000Z",
    "profileId": "pes2021-player-edit-v1",
    "profileVersion": "1.0.0",
    "profileSha256": "<sha256-of-profile-json>"
  },
  "outcome": "rolled_back",
  "verification": {
    "rawBeforeSha256": "<sha256>",
    "rawAfterSha256": "<sha256>",
    "rawRestored": 500000,
    "bytesRestored": 4
  },
  "warnings": []
}
```

## Forward compatibility

- New optional fields may be added by a later `pes2021.player-memory.vN` schema. Consumers must ignore unknown fields.
- Renaming a field is a breaking change: it requires a new profile version and a new `schemaVersion`.
- Adding a new `context` value requires updating both this document and `error-codes.md` in the same review.
