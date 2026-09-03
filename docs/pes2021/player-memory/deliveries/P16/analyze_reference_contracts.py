#!/usr/bin/env python3
"""Audit CT/Lua player-contract claims against read-only EDIT and ML dumps."""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any


SENTINEL_LINK = (0xFFFF, 0xFFFF)
CORE_OFFSETS = {
    "contract_year": 0x138,
    "contract_month": 0x13A,
    "contract_day": 0x13B,
    "salary": 0x15C,
    "link_12c": 0x12C,
    "link_160": 0x160,
    "link_164": 0x164,
    "candidate_loan_year": 0x16C,
    "candidate_loan_month": 0x16E,
    "candidate_loan_day": 0x16F,
    "market_value": 0x174,
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--edit-dump", required=True, type=Path)
    parser.add_argument("--ml-dump", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_csv(path: Path, headers: list[str], rows: list[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=headers, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def direct_text(node: ET.Element, name: str) -> str | None:
    child = node.find(name)
    return child.text if child is not None else None


def parse_ct_fields(path: Path) -> list[dict[str, Any]]:
    root = ET.parse(path).getroot()
    rows: list[dict[str, Any]] = []
    for entry in root.iter("CheatEntry"):
        if direct_text(entry, "Address") != "ptrPlayer":
            continue
        offset_node = entry.find("./Offsets/Offset")
        if offset_node is None or offset_node.text is None:
            continue
        rows.append({
            "ct_entry_id": direct_text(entry, "ID") or "",
            "description": (direct_text(entry, "Description") or "").strip('"'),
            "offset_hex": f"0x{int(offset_node.text, 16):03X}",
            "offset_decimal": int(offset_node.text, 16),
            "variable_type": direct_text(entry, "VariableType") or "",
            "bit_start": direct_text(entry, "BitStart") or "",
            "bit_length": direct_text(entry, "BitLength") or "",
            "signed": direct_text(entry, "ShowAsSigned") or "",
        })
    return sorted(rows, key=lambda row: (int(row["offset_decimal"]), str(row["description"])))


def parse_lua_constants(path: Path) -> list[dict[str, Any]]:
    pattern = re.compile(r"^local\s+(OFF_[A-Z0-9_]+)\s*=\s*(0x[0-9A-Fa-f]+)", re.MULTILINE)
    text = path.read_text(encoding="utf-8")
    return [
        {"constant": name, "offset_hex": f"0x{int(value, 16):03X}", "offset_decimal": int(value, 16)}
        for name, value in pattern.findall(text)
    ]


def load_cfg(path: Path) -> dict[int, str]:
    values: dict[int, str] = {}
    for line in path.read_text(encoding="utf-8-sig").splitlines():
        if "=" not in line or line.lstrip().startswith(("#", ";")):
            continue
        key, value = line.split("=", 1)
        try:
            values[int(key.strip())] = value.strip()
        except ValueError:
            continue
    return values


def u16(raw: bytes, offset: int) -> int:
    return int.from_bytes(raw[offset:offset + 2], "little")


def i32(raw: bytes, offset: int) -> int:
    return int.from_bytes(raw[offset:offset + 4], "little", signed=True)


def link(raw: bytes, offset: int) -> tuple[int, int]:
    return u16(raw, offset), u16(raw, offset + 2)


def valid_link(value: tuple[int, int]) -> bool:
    return value != SENTINEL_LINK


def plausible_date(year: int, month: int, day: int) -> bool:
    return 2000 <= year <= 2200 and 1 <= month <= 12 and 1 <= day <= 31


def decode_dump(path: Path) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    dump = json.loads(path.read_text(encoding="utf-8"))
    decoded: list[dict[str, Any]] = []
    for player in dump["players"]:
        raw = base64.b64decode(player["rawRecord"], validate=True)
        if len(raw) != 0x17C:
            raise ValueError(f"record {player['recordIndex']} has {len(raw)} bytes, expected 0x17C")
        if hashlib.sha256(raw).hexdigest() != player["rawRecordSha256"]:
            raise ValueError(f"record hash mismatch at index {player['recordIndex']}")
        decoded.append({**player, "raw": raw})
    return dump, decoded


def unique_by_id(players: list[dict[str, Any]]) -> dict[int, dict[str, Any]]:
    groups: dict[int, list[dict[str, Any]]] = defaultdict(list)
    for player in players:
        groups[int(player["playerId"])].append(player)
    return {player_id: values[0] for player_id, values in groups.items() if len(values) == 1}


def changed_span_count(edit: dict[int, dict[str, Any]], ml: dict[int, dict[str, Any]], offset: int, size: int) -> int:
    return sum(
        edit[player_id]["raw"][offset:offset + size] != ml[player_id]["raw"][offset:offset + size]
        for player_id in edit.keys() & ml.keys()
    )


def main() -> None:
    args = parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    package = Path(__file__).resolve().parent
    reference = package / "reference"
    ct = reference / "cheat-table" / "PES 2021 - wILL- v0.0.1 - Copia.CT"
    lua = reference / "world-gogosz-recent" / "ml_player_info.lua"
    team_cfg = reference / "world-gogosz-recent" / "ml_teams.cfg"

    ct_fields = parse_ct_fields(ct)
    write_csv(args.output / "ct-player-fields.csv", list(ct_fields[0]), ct_fields)
    lua_constants = parse_lua_constants(lua)
    write_csv(args.output / "lua-offset-constants.csv", list(lua_constants[0]), lua_constants)

    source_rows = []
    for path in sorted((reference / "cheat-table").rglob("*")) + sorted((reference / "world-gogosz-recent").glob("*")):
        if path.is_file():
            source_rows.append({
                "relative_path": path.relative_to(package).as_posix(),
                "bytes": path.stat().st_size,
                "sha256": sha256(path),
            })
    write_csv(args.output / "source-snapshot-sha256.csv", list(source_rows[0]), source_rows)

    edit_dump, edit_players = decode_dump(args.edit_dump)
    ml_dump, ml_players = decode_dump(args.ml_dump)
    edit_unique = unique_by_id(edit_players)
    ml_unique = unique_by_id(ml_players)
    safe_ids = edit_unique.keys() & ml_unique.keys()
    teams = load_cfg(team_cfg)

    relation_patterns: Counter[tuple[bool, bool, bool, bool, bool, bool]] = Counter()
    relation_rows: list[dict[str, Any]] = []
    candidate_loan_rows: list[dict[str, Any]] = []
    contract_dates = 0
    salaries = 0
    market_values = 0
    lua_inferred_relations = 0
    lua_current_equals_roster = 0
    lua_origin_equals_roster = 0
    candidate_loan_dates = 0
    candidate_loan_lua_current_equals_roster = 0
    candidate_loan_lua_origin_equals_roster = 0

    for player in ml_players:
        raw = player["raw"]
        base = link(raw, 0x12C)
        auxiliary = link(raw, 0x160)
        roster = link(raw, 0x164)
        year, month, day = u16(raw, 0x16C), raw[0x16E], raw[0x16F]
        has_candidate_loan_date = plausible_date(year, month, day)
        has_lua_relation = auxiliary[0] not in (0, 0xFFFF) and auxiliary != base
        relation_patterns[(
            valid_link(base), valid_link(auxiliary), valid_link(roster),
            base == roster, auxiliary == roster, has_candidate_loan_date,
        )] += 1

        contract_dates += plausible_date(u16(raw, 0x138), raw[0x13A], raw[0x13B])
        salaries += i32(raw, 0x15C) > 0
        market_values += i32(raw, 0x174) > 0
        candidate_loan_dates += has_candidate_loan_date

        if has_lua_relation:
            lua_inferred_relations += 1
            lua_current_equals_roster += base == roster
            lua_origin_equals_roster += auxiliary == roster
        if has_candidate_loan_date:
            candidate_loan_lua_current_equals_roster += base == roster
            candidate_loan_lua_origin_equals_roster += auxiliary == roster

        if valid_link(auxiliary):
            direction = "OTHER"
            if auxiliary == roster and base != roster:
                direction = "LUA_ORIGIN_EQUALS_ROSTER"
            elif base == roster and auxiliary != roster:
                direction = "LUA_CURRENT_EQUALS_ROSTER"
            row = {
                "record_index": player["recordIndex"],
                "player_id": player["playerId"],
                "player_name": player["playerName"],
                "link_12c_team": base[0],
                "link_12c_team_name_candidate": teams.get(base[0], ""),
                "link_12c_second": base[1],
                "link_160_team": auxiliary[0],
                "link_160_team_name_candidate": teams.get(auxiliary[0], ""),
                "link_160_second": auxiliary[1],
                "link_164_team": roster[0],
                "link_164_team_name_candidate": teams.get(roster[0], ""),
                "link_164_second": roster[1],
                "candidate_date": f"{year:04d}-{month:02d}-{day:02d}" if has_candidate_loan_date else "",
                "direction_vs_roster_candidate": direction,
            }
            relation_rows.append(row)
            if has_candidate_loan_date:
                candidate_loan_rows.append(row)

    relation_rows.sort(key=lambda row: int(row["record_index"]))
    candidate_loan_rows.sort(key=lambda row: int(row["record_index"]))
    headers = list(relation_rows[0])
    write_csv(args.output / "all-aux-link-records.csv", headers, relation_rows)
    write_csv(args.output / "candidate-loan-date-records.csv", headers, candidate_loan_rows)

    pattern_rows = [
        {
            "count": count,
            "link_12c_valid": key[0],
            "link_160_valid": key[1],
            "link_164_valid": key[2],
            "link_12c_equals_164": key[3],
            "link_160_equals_164": key[4],
            "candidate_loan_date": key[5],
        }
        for key, count in relation_patterns.most_common()
    ]
    write_csv(args.output / "relation-patterns.csv", list(pattern_rows[0]), pattern_rows)

    span_specs = {
        "contract_date_138_13b": (0x138, 4),
        "salary_15c": (0x15C, 4),
        "link_12c": (0x12C, 4),
        "link_160": (0x160, 4),
        "link_164": (0x164, 4),
        "candidate_loan_date_16c_16f": (0x16C, 4),
        "market_value_174": (0x174, 4),
    }
    changes = {
        name: changed_span_count(edit_unique, ml_unique, offset, size)
        for name, (offset, size) in span_specs.items()
    }

    ct_offsets = {int(row["offset_decimal"]) for row in ct_fields}
    lua_offsets = {int(row["offset_decimal"]) for row in lua_constants}
    current_manifest = reference / "world-gogosz-recent" / "MANIFEST-SHA256.csv"
    manifest_matches = 0
    manifest_rows = list(csv.DictReader(current_manifest.open(encoding="utf-8-sig", newline="")))
    for row in manifest_rows:
        target = reference / "world-gogosz-recent" / row["relative_path"]
        manifest_matches += int(
            target.is_file()
            and target.stat().st_size == int(row["bytes"])
            and sha256(target).lower() == row["sha256"].lower()
        )

    evidence = {
        "schemaVersion": "pes2021.player-memory.reference-contract-audit.v1",
        "safety": {"readOnly": True, "memoryWrites": 0, "referenceOriginalsModified": False},
        "inputs": {
            "editDump": {"path": args.edit_dump.as_posix(), "sha256": sha256(args.edit_dump)},
            "mlDump": {"path": args.ml_dump.as_posix(), "sha256": sha256(args.ml_dump)},
            "referenceSnapshotFiles": len(source_rows),
            "worldManifestRowsMatchingSnapshot": manifest_matches,
            "worldManifestRows": len(manifest_rows),
        },
        "staticInventory": {
            "ctPtrPlayerFields": len(ct_fields),
            "ctDeclaredEndOffset": max(ct_offsets),
            "ctMaximumDataOffset": max(offset for offset in ct_offsets if offset < 0x17C),
            "luaOffsetConstants": len(lua_constants),
            "ctContainsLink12c": 0x12C in ct_offsets,
            "ctContainsLink160": 0x160 in ct_offsets,
            "ctContainsLink164": 0x164 in ct_offsets,
            "ctContainsCandidateLoanDate16c": 0x16C in ct_offsets,
            "luaContainsLink160": 0x160 in lua_offsets,
            "luaContainsCandidateLoanDate16c": 0x16C in lua_offsets,
        },
        "population": {
            "editRecords": len(edit_players),
            "mlRecords": len(ml_players),
            "safeUniqueEditMlAssociations": len(safe_ids),
        },
        "mlOccupancy": {
            "positiveSalary15c": salaries,
            "plausibleContractDate138": contract_dates,
            "positiveMarketValue174": market_values,
            "candidateLoanDate16c": candidate_loan_dates,
            "auxLink160Valid": len(relation_rows),
        },
        "editMlChangedSafeAssociations": changes,
        "luaDirectionAuditAgainstRoster164Candidate": {
            "luaInferredRelations": lua_inferred_relations,
            "luaCurrent12cEqualsRoster164": lua_current_equals_roster,
            "luaOrigin160EqualsRoster164": lua_origin_equals_roster,
            "candidateLoanDates": candidate_loan_dates,
            "candidateLoanLuaCurrent12cEqualsRoster164": candidate_loan_lua_current_equals_roster,
            "candidateLoanLuaOrigin160EqualsRoster164": candidate_loan_lua_origin_equals_roster,
        },
        "evidenceLabels": {
            "recordSize17c": "CONFIRMED_BY_CT_AND_DUMPS",
            "salaryDateOffsets": "CANDIDATE_CT_SEMANTICS_STRONGLY_ML_POPULATED",
            "marketOffset": "CONFIRMED_STABLE_BASE_VALUE_CANDIDATE_MARKET_SEMANTICS_SCALE_UNCONFIRMED",
            "link164": "CANDIDATE_CURRENT_ML_ROSTER_RELATION",
            "luaFixedCurrentOriginDirection": "REFUTED_AS_GENERAL_RULE",
            "loanDate16c": "CANDIDATE_REQUIRES_UI_OR_KNOWN_LOAN_CONTROLS",
            "teamAndLeagueCfgNames": "CANDIDATE_REFERENCE_ONLY",
        },
        "sessions": {"edit": edit_dump.get("session"), "ml": ml_dump.get("session")},
    }
    with (args.output / "evidence.json").open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(evidence, handle, ensure_ascii=False, indent=2)
        handle.write("\n")
    print(json.dumps(evidence, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
