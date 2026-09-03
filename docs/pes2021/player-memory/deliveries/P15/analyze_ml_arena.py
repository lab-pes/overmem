#!/usr/bin/env python3
"""Produce compact, reproducible evidence from a PES 2021 ML player arena dump."""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


CONTROL_IDS = [
    58120, 111207, 101520, 126689, 127544, 136778, 103404, 130202,
    120439, 41135, 126390, 108200, 132155, 132518, 103613, 114044,
    47242, 393914, 114906, 114506, 108959, 126918, 133299, 60854,
    131057, 110794, 100400, 112070, 142445, 32578,
]


def args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dump", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def fields(player: dict[str, Any]) -> dict[str, Any]:
    return {
        field["name"]: field["rawString"] if field["rawString"] is not None else field["rawLong"]
        for field in player["fields"]
    }


def link(raw: bytes, offset: int) -> tuple[int, int]:
    return (
        int.from_bytes(raw[offset : offset + 2], "little"),
        int.from_bytes(raw[offset + 2 : offset + 4], "little"),
    )


def valid_link(value: tuple[int, int]) -> bool:
    return value != (0xFFFF, 0xFFFF)


def write_csv(path: Path, headers: list[str], rows: list[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    parsed = args()
    parsed.output.mkdir(parents=True, exist_ok=True)
    with parsed.dump.open("r", encoding="utf-8") as handle:
        dump = json.load(handle)
    players = dump["players"]
    groups: dict[int, list[dict[str, Any]]] = defaultdict(list)
    decoded: list[tuple[dict[str, Any], dict[str, Any], bytes]] = []
    for player in players:
        raw = base64.b64decode(player["rawRecord"], validate=True)
        if len(raw) != 380 or hashlib.sha256(raw).hexdigest() != player["rawRecordSha256"]:
            raise ValueError(f"invalid raw record at index {player['recordIndex']}")
        groups[int(player["playerId"])].append(player)
        decoded.append((player, fields(player), raw))

    plausible_date = lambda value: (
        2000 <= value.get("contractEndYear", 0) <= 2200
        and 1 <= value.get("contractEndMonth", 0) <= 12
        and 1 <= value.get("contractEndDay", 0) <= 31
    )
    patterns: Counter[tuple[bool, bool, bool, bool, bool, bool, bool]] = Counter()
    mismatch_samples: list[dict[str, Any]] = []
    control_rows: list[dict[str, Any]] = []
    controls = set(CONTROL_IDS)
    for player, value, raw in decoded:
        base_link = link(raw, 0x12C)
        auxiliary_link = link(raw, 0x160)
        roster_link = link(raw, 0x164)
        flags = int(value.get("transferFlags", 0))
        patterns[(
            valid_link(base_link), valid_link(roster_link), valid_link(auxiliary_link),
            base_link == roster_link, auxiliary_link == roster_link,
            bool(flags & 0x02), bool(flags & 0x04),
        )] += 1
        common = {
            "record_index": player["recordIndex"],
            "player_id": player["playerId"],
            "player_name": player["playerName"],
            "base_link_12c_a": base_link[0],
            "base_link_12e_b": base_link[1],
            "aux_link_160_a": auxiliary_link[0],
            "aux_link_162_b": auxiliary_link[1],
            "roster_link_164_a": roster_link[0],
            "roster_link_166_b": roster_link[1],
            "salary_raw_candidate": value.get("annualSalary"),
            "contract_end_candidate": (
                f"{value['contractEndYear']:04d}-{value['contractEndMonth']:02d}-{value['contractEndDay']:02d}"
                if plausible_date(value) else ""
            ),
            "market_value_raw": value.get("marketValue"),
            "transfer_flags_raw": flags,
            "transfer_listed_candidate": bool(flags & 0x02),
            "loan_listed_candidate": bool(flags & 0x04),
            "form_arrow_candidate": int(value.get("currentFormArrow", 0)) & 0x07,
            "stamina_candidate": int(value.get("staminaBar", 0)) & 0x7F,
            "raw_record_sha256": player["rawRecordSha256"],
        }
        if int(player["playerId"]) in controls:
            control_rows.append(common)
        if valid_link(base_link) and valid_link(roster_link) and base_link != roster_link and len(mismatch_samples) < 50:
            mismatch_samples.append(common)

    control_rows.sort(key=lambda row: CONTROL_IDS.index(int(row["player_id"])))
    headers = list(control_rows[0].keys())
    write_csv(parsed.output / "ml-control-samples.csv", headers, control_rows)
    write_csv(parsed.output / "ml-link-mismatch-samples.csv", headers, mismatch_samples)

    duplicate_rows = []
    for player_id, instances in sorted(groups.items()):
        if len(instances) > 1:
            for player in instances:
                duplicate_rows.append({
                    "player_id": player_id,
                    "player_name": player["playerName"],
                    "record_index": player["recordIndex"],
                    "raw_record_sha256": player["rawRecordSha256"],
                })
    write_csv(
        parsed.output / "ml-duplicate-ids.csv",
        ["player_id", "player_name", "record_index", "raw_record_sha256"],
        duplicate_rows,
    )

    pattern_rows = [
        {
            "count": count,
            "base_link_valid": key[0],
            "roster_link_valid": key[1],
            "aux_link_valid": key[2],
            "base_equals_roster": key[3],
            "aux_equals_roster": key[4],
            "transfer_listed_candidate": key[5],
            "loan_listed_candidate": key[6],
        }
        for key, count in patterns.most_common()
    ]
    write_csv(parsed.output / "ml-link-patterns.csv", list(pattern_rows[0].keys()), pattern_rows)

    count = len(players)
    evidence = {
        "schemaVersion": "pes2021.player-memory.ml-arena-evidence.v1",
        "capturedContext": "MASTER_LEAGUE_LOADED",
        "safety": {"readOnly": True, "memoryWrites": 0},
        "input": {"path": parsed.dump.as_posix(), "sha256": sha256(parsed.dump)},
        "session": dump["session"],
        "arenaCoverage": dump.get("arenaCoverage"),
        "population": {
            "records": count,
            "uniqueIds": len(groups),
            "duplicateIds": sum(1 for instances in groups.values() if len(instances) > 1),
            "duplicateRecordsBeyondFirst": sum(len(instances) - 1 for instances in groups.values()),
        },
        "occupancy": {
            "positiveMarketValue": sum(value.get("marketValue", 0) > 0 for _, value, _ in decoded),
            "positiveAnnualSalary": sum(value.get("annualSalary", 0) > 0 for _, value, _ in decoded),
            "plausibleContractDate": sum(plausible_date(value) for _, value, _ in decoded),
            "baseLink12cValid": sum(valid_link(link(raw, 0x12C)) for _, _, raw in decoded),
            "rosterLink164Valid": sum(valid_link(link(raw, 0x164)) for _, _, raw in decoded),
            "auxLink160Valid": sum(valid_link(link(raw, 0x160)) for _, _, raw in decoded),
            "baseRosterMismatch": sum(
                valid_link(link(raw, 0x12C)) and valid_link(link(raw, 0x164))
                and link(raw, 0x12C) != link(raw, 0x164)
                for _, _, raw in decoded
            ),
            "transferListedBit": sum(bool(int(value.get("transferFlags", 0)) & 0x02) for _, value, _ in decoded),
            "loanListedBit": sum(bool(int(value.get("transferFlags", 0)) & 0x04) for _, value, _ in decoded),
            "bothListingBits": sum((int(value.get("transferFlags", 0)) & 0x06) == 0x06 for _, value, _ in decoded),
        },
        "evidenceLabels": {
            "arenaClassification": "CONFIRMED_STRUCTURAL_ML_CANDIDATE",
            "salaryDateMarket": "CANDIDATE_CT_SEMANTICS",
            "baseLink12c": "UNKNOWN",
            "auxLink160": "UNKNOWN",
            "rosterLink164": "CANDIDATE_CURRENT_ML_ROSTER_RELATION",
            "actualLoanRelationship": "UNKNOWN",
        },
    }
    with (parsed.output / "evidence.json").open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(evidence, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    print(json.dumps(evidence, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
