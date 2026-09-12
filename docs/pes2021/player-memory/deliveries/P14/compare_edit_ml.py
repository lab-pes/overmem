#!/usr/bin/env python3
"""Compare two Overmem player dumps without attaching to a process."""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import json
import re
import unicodedata
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable


RECORD_SIZE = 380


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--edit", required=True, type=Path)
    parser.add_argument("--ml", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fingerprint(name: str) -> str:
    decomposed = unicodedata.normalize("NFKD", name)
    ascii_text = "".join(character for character in decomposed if not unicodedata.combining(character))
    return re.sub(r"[^a-z0-9]", "", ascii_text.casefold())


def decode_record(player: dict[str, Any]) -> bytes:
    raw = base64.b64decode(player["rawRecord"], validate=True)
    if len(raw) != RECORD_SIZE:
        raise ValueError(f"player {player['playerId']} record length is {len(raw)}, expected 380")
    digest = hashlib.sha256(raw).hexdigest()
    if digest != player["rawRecordSha256"].lower():
        raise ValueError(f"player {player['playerId']} rawRecordSha256 mismatch")
    return raw


def group_players(players: list[dict[str, Any]], label: str) -> dict[int, list[dict[str, Any]]]:
    result: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for player in players:
        player_id = int(player["playerId"])
        if not 0 <= player_id <= 0xFFFFFFFF:
            raise ValueError(f"{label} player ID outside opaque u32 range: {player_id}")
        result[player_id].append(player)
    return result


def fields_by_name(player: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {field["name"]: field for field in player.get("fields", [])}


def raw_field_value(field: dict[str, Any] | None) -> Any:
    if field is None:
        return None
    return field.get("rawString") if field.get("rawString") is not None else field.get("rawLong")


def json_cell(value: Any) -> str:
    return json.dumps(value, ensure_ascii=False, separators=(",", ":"))


def write_csv(path: Path, fieldnames: list[str], rows: Iterable[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def compare(edit_path: Path, ml_path: Path, output: Path) -> dict[str, Any]:
    output.mkdir(parents=True, exist_ok=True)
    edit_dump = load_json(edit_path)
    ml_dump = load_json(ml_path)
    edit_records_all = edit_dump["players"]
    ml_records_all = ml_dump["players"]
    edit_players = group_players(edit_records_all, "EDIT")
    ml_players = group_players(ml_records_all, "ML")
    for player in edit_records_all + ml_records_all:
        decode_record(player)

    common_ids = sorted(edit_players.keys() & ml_players.keys())
    ambiguous_ids = sorted(
        player_id
        for player_id in common_ids
        if len(edit_players[player_id]) != 1 or len(ml_players[player_id]) != 1
    )
    safe_common_ids = sorted(set(common_ids) - set(ambiguous_ids))
    only_edit = sorted(edit_players.keys() - ml_players.keys())
    only_ml = sorted(ml_players.keys() - edit_players.keys())

    player_rows: list[dict[str, Any]] = []
    field_detail_rows: list[dict[str, Any]] = []
    offset_changed_players: Counter[int] = Counter()
    offset_transitions: dict[int, Counter[tuple[int, int]]] = defaultdict(Counter)
    field_changed_players: Counter[str] = Counter()
    field_transitions: dict[str, Counter[tuple[str, str]]] = defaultdict(Counter)
    fingerprint_mismatches = 0
    exact_matches = 0
    changed_matches = 0

    for player_id in safe_common_ids:
        edit_player = edit_players[player_id][0]
        ml_player = ml_players[player_id][0]
        edit_fingerprint = fingerprint(str(edit_player["playerName"]))
        ml_fingerprint = fingerprint(str(ml_player["playerName"]))
        if edit_fingerprint != ml_fingerprint:
            fingerprint_mismatches += 1
            player_rows.append(
                {
                    "player_id_decimal": player_id,
                    "player_id_hex": f"0x{player_id:08X}",
                    "edit_name": edit_player["playerName"],
                    "ml_name": ml_player["playerName"],
                    "match_status": "FINGERPRINT_MISMATCH",
                    "edit_instances": 1,
                    "ml_instances": 1,
                    "changed_bytes": "",
                    "changed_fields": "",
                    "edit_record_sha256": edit_player["rawRecordSha256"],
                    "ml_record_sha256": ml_player["rawRecordSha256"],
                }
            )
            continue

        edit_raw = decode_record(edit_player)
        ml_raw = decode_record(ml_player)
        changed_offsets = [offset for offset in range(RECORD_SIZE) if edit_raw[offset] != ml_raw[offset]]
        for offset in changed_offsets:
            offset_changed_players[offset] += 1
            offset_transitions[offset][(edit_raw[offset], ml_raw[offset])] += 1

        edit_fields = fields_by_name(edit_player)
        ml_fields = fields_by_name(ml_player)
        changed_fields: list[str] = []
        for name in sorted(edit_fields.keys() | ml_fields.keys()):
            edit_field = edit_fields.get(name)
            ml_field = ml_fields.get(name)
            edit_value = raw_field_value(edit_field)
            ml_value = raw_field_value(ml_field)
            if edit_value == ml_value:
                continue
            changed_fields.append(name)
            field_changed_players[name] += 1
            field_transitions[name][(json_cell(edit_value), json_cell(ml_value))] += 1
            field_detail_rows.append(
                {
                    "player_id_decimal": player_id,
                    "player_id_hex": f"0x{player_id:08X}",
                    "player_name": edit_player["playerName"],
                    "field_name": name,
                    "edit_value": json_cell(edit_value),
                    "ml_value": json_cell(ml_value),
                    "edit_evidence_status": (edit_field or {}).get("evidenceStatus", ""),
                    "ml_evidence_status": (ml_field or {}).get("evidenceStatus", ""),
                }
            )

        status = "MATCHED_CHANGED" if changed_offsets else "MATCHED_EXACT"
        if changed_offsets:
            changed_matches += 1
        else:
            exact_matches += 1
        player_rows.append(
            {
                "player_id_decimal": player_id,
                "player_id_hex": f"0x{player_id:08X}",
                "edit_name": edit_player["playerName"],
                "ml_name": ml_player["playerName"],
                "match_status": status,
                "edit_instances": 1,
                "ml_instances": 1,
                "changed_bytes": len(changed_offsets),
                "changed_fields": ";".join(changed_fields),
                "edit_record_sha256": edit_player["rawRecordSha256"],
                "ml_record_sha256": ml_player["rawRecordSha256"],
            }
        )

    for player_id in ambiguous_ids:
        edit_group = edit_players[player_id]
        ml_group = ml_players[player_id]
        player_rows.append(
            {
                "player_id_decimal": player_id,
                "player_id_hex": f"0x{player_id:08X}",
                "edit_name": ";".join(sorted({str(player["playerName"]) for player in edit_group})),
                "ml_name": ";".join(sorted({str(player["playerName"]) for player in ml_group})),
                "match_status": "AMBIGUOUS_DUPLICATE",
                "edit_instances": len(edit_group),
                "ml_instances": len(ml_group),
                "changed_bytes": "",
                "changed_fields": "",
                "edit_record_sha256": ";".join(player["rawRecordSha256"] for player in edit_group),
                "ml_record_sha256": ";".join(player["rawRecordSha256"] for player in ml_group),
            }
        )

    for player_id, status in [(value, "ONLY_EDIT") for value in only_edit] + [
        (value, "ONLY_ML") for value in only_ml
    ]:
        edit_group = edit_players.get(player_id, [])
        ml_group = ml_players.get(player_id, [])
        player_rows.append(
            {
                "player_id_decimal": player_id,
                "player_id_hex": f"0x{player_id:08X}",
                "edit_name": ";".join(sorted({str(player["playerName"]) for player in edit_group})),
                "ml_name": ";".join(sorted({str(player["playerName"]) for player in ml_group})),
                "match_status": status,
                "edit_instances": len(edit_group),
                "ml_instances": len(ml_group),
                "changed_bytes": "",
                "changed_fields": "",
                "edit_record_sha256": ";".join(player["rawRecordSha256"] for player in edit_group),
                "ml_record_sha256": ";".join(player["rawRecordSha256"] for player in ml_group),
            }
        )

    player_rows.sort(key=lambda row: (int(row["player_id_decimal"]), row["match_status"]))
    write_csv(output / "player-diff-summary.csv", list(player_rows[0].keys()), player_rows)
    field_headers = [
        "player_id_decimal", "player_id_hex", "player_name", "field_name",
        "edit_value", "ml_value", "edit_evidence_status", "ml_evidence_status",
    ]
    write_csv(output / "player-field-diffs.csv", field_headers, field_detail_rows)

    offset_rows = []
    for offset in range(RECORD_SIZE):
        transitions = offset_transitions[offset]
        offset_rows.append(
            {
                "offset_decimal": offset,
                "offset_hex": f"0x{offset:03X}",
                "changed_players": offset_changed_players[offset],
                "changed_percent_of_safe_matches": round(
                    100 * offset_changed_players[offset] / max(1, exact_matches + changed_matches), 6
                ),
                "distinct_transitions": len(transitions),
                "top_transitions": json_cell(
                    [[old, new, count] for (old, new), count in transitions.most_common(10)]
                ),
            }
        )
    write_csv(output / "offset-diff-summary.csv", list(offset_rows[0].keys()), offset_rows)

    field_rows = [
        {
            "field_name": name,
            "changed_players": count,
            "top_transitions": json_cell(
                [[old, new, transition_count] for (old, new), transition_count in field_transitions[name].most_common(10)]
            ),
        }
        for name, count in sorted(field_changed_players.items(), key=lambda item: (-item[1], item[0]))
    ]
    write_csv(output / "field-diff-summary.csv", ["field_name", "changed_players", "top_transitions"], field_rows)

    evidence = {
        "schemaVersion": "pes2021.player-memory.edit-ml-comparison.v1",
        "safety": {"offlineOnly": True, "processAttachment": False, "memoryWrites": 0},
        "inputs": {
            "edit": {"path": edit_path.as_posix(), "sha256": sha256_file(edit_path)},
            "ml": {"path": ml_path.as_posix(), "sha256": sha256_file(ml_path)},
        },
        "identityPolicy": {
            "primary": "opaque u32 playerId",
            "guard": "normalized playerName fingerprint",
            "fingerprintMismatchDisposition": "not byte-compared",
        },
        "counts": {
            "editRecords": len(edit_records_all),
            "mlRecords": len(ml_records_all),
            "editUniqueIds": len(edit_players),
            "mlUniqueIds": len(ml_players),
            "commonIds": len(common_ids),
            "ambiguousDuplicateIds": len(ambiguous_ids),
            "safeMatches": exact_matches + changed_matches,
            "exactMatches": exact_matches,
            "changedMatches": changed_matches,
            "fingerprintMismatches": fingerprint_mismatches,
            "onlyEdit": len(only_edit),
            "onlyMl": len(only_ml),
            "offsetsChangedInAnyPlayer": len(offset_changed_players),
            "modeledFieldsChanged": len(field_changed_players),
        },
        "interpretation": "Deltas are observations. Field semantics retain their source evidence status.",
    }
    with (output / "comparison-evidence.json").open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(evidence, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    return evidence


def main() -> None:
    args = parse_args()
    print(json.dumps(compare(args.edit, args.ml, args.output), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
