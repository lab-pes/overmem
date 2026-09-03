#!/usr/bin/env python3
"""Build a reproducible EDIT control corpus and CT-to-dump field matrix.

This is an offline-only analyzer. It reads an Overmem JSON dump containing the
raw 380-byte player records, a player profile, and the Cheat Engine table. It
does not attach to PES and has no memory-write capability.
"""

from __future__ import annotations

import argparse
import base64
import csv
import hashlib
import json
import math
import re
import xml.etree.ElementTree as ET
from collections import Counter
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Iterable


RECORD_SIZE = 380
SENTINEL_U16 = 0xFFFF
SENTINEL_U32 = 0xFFFFFFFF
MANDATORY_CONTROL_IDS = [
    58120,   # Piero Hincapie, operational anchor
    108959,  # Declan Rice
    127544,  # Bukayo Saka
    126689,  # William Saliba
    33739,   # Morgan Rogers
    132155,  # Jurrien Timber
    114506,  # Kai Havertz
    120246,  # Vitor Roque
    59801,   # Youri Tielemans
    40352,   # Neymar
    111207,  # Gabriel Magalhaes, negative contract control
    0x4001FABF,  # Firas Al-buraikan
    0x4001FAFF,  # Lee Si-heon; catches a previously reported ID/name mismatch
    0x8000003E,
]


@dataclass(frozen=True)
class CtField:
    entry_id: str
    path: str
    description: str
    offset: int
    variable_type: str
    declared_width: int | None
    bit_start: int | None
    bit_length: int | None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--dump", required=True, type=Path)
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--profile", required=True, type=Path)
    parser.add_argument("--ct", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    return parser.parse_args()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def clean_description(text: str | None) -> str:
    return (text or "").strip().strip('"')


def parse_hex(text: str) -> int:
    value = text.strip()
    if value.startswith("-"):
        return -int(value[1:], 16)
    return int(value, 16)


def declared_width(entry: ET.Element, variable_type: str) -> int | None:
    widths = {
        "Byte": 1,
        "2 Bytes": 2,
        "4 Bytes": 4,
        "8 Bytes": 8,
        "Float": 4,
        "Double": 8,
    }
    if variable_type == "Binary":
        bit_start = int(entry.findtext("BitStart", "0"))
        bit_length = int(entry.findtext("BitLength", "1"))
        return math.ceil((bit_start + bit_length) / 8)
    if variable_type == "String":
        length = entry.findtext("Length")
        return int(length) if length else None
    return widths.get(variable_type)


def collect_ct_fields(root: ET.Element) -> list[CtField]:
    result: list[CtField] = []

    def visit(container: ET.Element, parents: list[str]) -> None:
        cheat_entries = container.find("CheatEntries")
        if cheat_entries is None:
            return
        for entry in cheat_entries.findall("CheatEntry"):
            description = clean_description(entry.findtext("Description"))
            path = parents + ([description] if description else [])
            address = (entry.findtext("Address") or "").strip()
            offset_text = entry.findtext("Offsets/Offset")
            variable_type = (entry.findtext("VariableType") or "").strip()
            if address == "ptrPlayer" and offset_text is not None and variable_type:
                bit_start = entry.findtext("BitStart")
                bit_length = entry.findtext("BitLength")
                result.append(
                    CtField(
                        entry_id=(entry.findtext("ID") or "").strip(),
                        path=" > ".join(path),
                        description=description,
                        offset=parse_hex(offset_text),
                        variable_type=variable_type,
                        declared_width=declared_width(entry, variable_type),
                        bit_start=int(bit_start) if bit_start is not None else None,
                        bit_length=int(bit_length) if bit_length is not None else None,
                    )
                )
            visit(entry, path)

    visit(root, [])
    return sorted(
        result,
        key=lambda field: (
            field.offset,
            field.bit_start if field.bit_start is not None else -1,
            field.path,
        ),
    )


def profile_atoms(profile: dict[str, Any]) -> list[dict[str, Any]]:
    atoms: list[dict[str, Any]] = []
    for field in profile["recordLayout"]["fields"]:
        atoms.append(
            {
                "name": field["name"],
                "parent": field["name"],
                "offset": field["offset"],
                "width": field["width"],
                "bitStart": None,
                "bitLength": None,
                "readStatus": field["readStatus"],
            }
        )
        for bit in field.get("bitFields", []):
            atoms.append(
                {
                    "name": bit["name"],
                    "parent": field["name"],
                    "offset": field["offset"],
                    "width": field["width"],
                    "bitStart": bit["bitStart"],
                    "bitLength": bit["bitLength"],
                    "readStatus": bit["readStatus"],
                }
            )
    return atoms


def normalize_name(value: str) -> str:
    return re.sub(r"[^a-z0-9]", "", value.lower())


def map_profile_field(ct: CtField, atoms: list[dict[str, Any]]) -> tuple[dict[str, Any] | None, str]:
    same_offset = [atom for atom in atoms if atom["offset"] == ct.offset]
    if ct.bit_start is not None:
        exact_bits = [
            atom
            for atom in same_offset
            if atom["bitStart"] == ct.bit_start and atom["bitLength"] == ct.bit_length
        ]
        if len(exact_bits) == 1:
            return exact_bits[0], "OFFSET_AND_BITS"
        name_match = [
            atom
            for atom in exact_bits
            if normalize_name(atom["name"]) == normalize_name(ct.description)
        ]
        if name_match:
            return name_match[0], "OFFSET_BITS_AND_NAME"
    non_bits = [atom for atom in same_offset if atom["bitStart"] is None]
    if len(non_bits) == 1:
        return non_bits[0], "OFFSET"
    compatible = [atom for atom in non_bits if atom["width"] == ct.declared_width]
    if len(compatible) == 1:
        return compatible[0], "OFFSET_AND_WIDTH"
    return None, "NO_PROFILE_MATCH"


def decode_record(player: dict[str, Any]) -> bytes:
    raw = base64.b64decode(player["rawRecord"], validate=True)
    if len(raw) != RECORD_SIZE:
        raise ValueError(
            f"recordIndex={player['recordIndex']} has {len(raw)} bytes, expected {RECORD_SIZE}"
        )
    expected_hash = player["rawRecordSha256"].lower()
    actual_hash = hashlib.sha256(raw).hexdigest()
    if actual_hash != expected_hash:
        raise ValueError(f"record hash mismatch at recordIndex={player['recordIndex']}")
    return raw


def read_ct_value(raw: bytes, field: CtField) -> int | str | None:
    width = field.declared_width
    if width is None or field.offset < 0 or field.offset + width > len(raw):
        return None
    chunk = raw[field.offset : field.offset + width]
    if field.variable_type == "String":
        return chunk.split(b"\0", 1)[0].decode("utf-8", errors="replace")
    value = int.from_bytes(chunk, "little", signed=False)
    if field.bit_start is not None and field.bit_length is not None:
        return (value >> field.bit_start) & ((1 << field.bit_length) - 1)
    return value


def field_map(player: dict[str, Any]) -> dict[str, Any]:
    return {
        field["name"]: field["rawString"] if field["rawString"] is not None else field["rawLong"]
        for field in player["fields"]
    }


def add_category(categories: dict[int, set[str]], player_id: int, category: str) -> None:
    categories.setdefault(player_id, set()).add(category)


def select_corpus(players: list[dict[str, Any]], target: int = 30) -> tuple[list[dict[str, Any]], dict[int, set[str]]]:
    by_id = {int(player["playerId"]): player for player in players}
    selected: list[dict[str, Any]] = []
    selected_ids: set[int] = set()
    categories: dict[int, set[str]] = {}

    def take(player: dict[str, Any] | None, category: str) -> None:
        if player is None:
            return
        player_id = int(player["playerId"])
        add_category(categories, player_id, category)
        if player_id not in selected_ids and len(selected) < target:
            selected.append(player)
            selected_ids.add(player_id)

    for player_id in MANDATORY_CONTROL_IDS:
        take(by_id.get(player_id), "named_control")
    missing_mandatory = sorted(set(MANDATORY_CONTROL_IDS) - by_id.keys())
    if missing_mandatory:
        raise ValueError(f"mandatory control IDs missing from dump: {missing_mandatory}")

    enriched = [(player, field_map(player)) for player in players]
    for player, fields in enriched:
        name = str(player["playerName"])
        if any(ord(character) > 127 for character in name):
            take(player, "non_ascii_name")
            if sum("non_ascii_name" in value for value in categories.values()) >= 3:
                break

    for key, category, reverse in [
        ("height", "height_min", False),
        ("height", "height_max", True),
        ("weight", "weight_min", False),
        ("weight", "weight_max", True),
        ("marketValue", "market_max", True),
        ("nationality", "nationality_max", True),
    ]:
        candidates = [item for item in enriched if isinstance(item[1].get(key), int)]
        candidates.sort(key=lambda item: (item[1][key], int(item[0]["playerId"])), reverse=reverse)
        if candidates:
            take(candidates[0][0], category)

    signature_fields = [
        "marketValue",
        "contractEndYear",
        "contractEndMonth",
        "contractEndDay",
        "currentFormArrow",
        "transferFlags",
        "teamRole",
        "personalityAxes",
        "impact",
    ]
    seen_signatures: set[tuple[Any, ...]] = set()
    diversity = sorted(enriched, key=lambda item: int(item[0]["playerId"]))
    for player, fields in diversity:
        signature = tuple(fields.get(name) for name in signature_fields)
        if signature in seen_signatures:
            continue
        seen_signatures.add(signature)
        take(player, "structural_diversity")
        if len(selected) >= target:
            break

    if len(selected) != target:
        raise ValueError(f"could select only {len(selected)} controls, expected {target}")
    return selected, categories


def stable_hash_check(current: list[dict[str, Any]], baseline_path: Path | None) -> dict[str, Any]:
    if baseline_path is None:
        return {"performed": False}
    baseline = load_json(baseline_path)
    current_hashes = {int(p["playerId"]): p["rawRecordSha256"].lower() for p in current}
    baseline_hashes = {
        int(p["playerId"]): p["rawRecordSha256"].lower() for p in baseline["players"]
    }
    common = current_hashes.keys() & baseline_hashes.keys()
    mismatched = sorted(pid for pid in common if current_hashes[pid] != baseline_hashes[pid])
    return {
        "performed": True,
        "baselinePath": baseline_path.as_posix(),
        "baselineSha256": sha256_file(baseline_path),
        "currentCount": len(current_hashes),
        "baselineCount": len(baseline_hashes),
        "commonPlayerIds": len(common),
        "onlyCurrent": len(current_hashes.keys() - baseline_hashes.keys()),
        "onlyBaseline": len(baseline_hashes.keys() - current_hashes.keys()),
        "hashMismatches": len(mismatched),
        "mismatchedPlayerIds": mismatched[:20],
    }


def write_csv(path: Path, fieldnames: list[str], rows: Iterable[dict[str, Any]]) -> None:
    with path.open("w", encoding="utf-8", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    args = parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    dump = load_json(args.dump)
    profile = load_json(args.profile)
    players = dump["players"]
    if len(players) != 25005:
        raise ValueError(f"expected validated EDIT population 25005, got {len(players)}")
    if int(dump["session"]["recordStride"]) != RECORD_SIZE:
        raise ValueError("dump record stride is not 380")

    records = [(player, decode_record(player)) for player in players]
    ct_fields = collect_ct_fields(ET.parse(args.ct).getroot())
    atoms = profile_atoms(profile)

    matrix_rows: list[dict[str, Any]] = []
    status_counts: Counter[str] = Counter()
    out_of_bounds = 0
    for ct_field in ct_fields:
        entry_kind = (
            "BOUNDARY_MARKER"
            if ct_field.offset == RECORD_SIZE and ct_field.description.lower().startswith("end ")
            else "PLAYER_FIELD"
        )
        profile_field, match_basis = map_profile_field(ct_field, atoms)
        values = [read_ct_value(raw, ct_field) for _, raw in records]
        valid_values = [value for value in values if value is not None]
        if not valid_values:
            out_of_bounds += 1
        counts = Counter(valid_values)
        if profile_field is None:
            evidence_status = "UNKNOWN"
        else:
            evidence_status = profile_field["readStatus"]
        description_is_unknown = (
            ct_field.description.strip() in {"?", ""}
            or ct_field.description in {"Team (?)", "League (?)"}
        )
        ct_claim_status = (
            "NOT_A_FIELD"
            if entry_kind != "PLAYER_FIELD"
            else "UNKNOWN" if description_is_unknown else "CANDIDATE"
        )
        if entry_kind == "PLAYER_FIELD":
            status_counts[evidence_status] += 1
        numeric = [value for value in valid_values if isinstance(value, int)]
        profile_width = profile_field["width"] if profile_field else None
        width_match = (
            "UNKNOWN"
            if profile_width is None or ct_field.declared_width is None
            else str(profile_width == ct_field.declared_width).lower()
        )
        matrix_rows.append(
            {
                "ct_entry_id": ct_field.entry_id,
                "ct_path": ct_field.path,
                "ct_description": ct_field.description,
                "ct_entry_kind": entry_kind,
                "offset_hex": f"0x{ct_field.offset:X}",
                "variable_type": ct_field.variable_type,
                "declared_width": ct_field.declared_width,
                "bit_start": ct_field.bit_start,
                "bit_length": ct_field.bit_length,
                "profile_field": profile_field["name"] if profile_field else "",
                "profile_parent": profile_field["parent"] if profile_field else "",
                "profile_width": profile_width,
                "width_match": width_match,
                "match_basis": match_basis,
                "evidence_status": evidence_status,
                "ct_claim_status": ct_claim_status,
                "records_observed": len(valid_values),
                "distinct_values": len(counts),
                "zero_count": counts.get(0, 0) + counts.get("", 0),
                "all_ones_count": (
                    counts.get((1 << (8 * ct_field.declared_width)) - 1, 0)
                    if ct_field.declared_width and ct_field.bit_start is None
                    else ""
                ),
                "minimum": min(numeric) if numeric else "",
                "maximum": max(numeric) if numeric else "",
                "top_values": json.dumps(counts.most_common(5), ensure_ascii=False, separators=(",", ":")),
            }
        )

    matrix_fields = list(matrix_rows[0].keys())
    write_csv(args.output / "ct-dump-matrix.csv", matrix_fields, matrix_rows)

    catalog = {
        "schemaVersion": "pes2021.player-memory.ct-candidate-catalog.v1",
        "source": {
            "path": args.ct.as_posix(),
            "sha256": sha256_file(args.ct),
            "authority": "STATIC_REFERENCE_ONLY",
        },
        "rules": [
            "A CT label is CANDIDATE evidence, never live semantic confirmation.",
            "Question-mark labels remain UNKNOWN.",
            "Overlapping bitfields are preserved exactly as declared by the CT.",
            "The 0x17C end marker is metadata, not a player field.",
        ],
        "entries": [
            {
                "ctEntryId": row["ct_entry_id"],
                "path": row["ct_path"],
                "description": row["ct_description"],
                "entryKind": row["ct_entry_kind"],
                "offset": int(row["offset_hex"], 16),
                "offsetHex": row["offset_hex"],
                "variableType": row["variable_type"],
                "declaredWidth": row["declared_width"],
                "bitStart": row["bit_start"],
                "bitLength": row["bit_length"],
                "profileField": row["profile_field"] or None,
                "profileReadStatus": row["evidence_status"] if row["profile_field"] else None,
                "ctClaimStatus": row["ct_claim_status"],
                "observedDistinctValues": row["distinct_values"],
            }
            for row in matrix_rows
        ],
    }
    with (args.output / "ct-candidate-field-catalog.json").open(
        "w", encoding="utf-8", newline="\n"
    ) as handle:
        json.dump(catalog, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    corpus, categories = select_corpus(players)
    corpus_rows: list[dict[str, Any]] = []
    corpus_fields = [
        "player_id_decimal",
        "player_id_hex",
        "player_name",
        "record_index",
        "categories",
        "height",
        "weight",
        "nationality",
        "market_value_raw",
        "market_value_eur_candidate",
        "contract_end_candidate",
        "annual_salary_raw",
        "current_form_arrow_raw",
        "transfer_flags_raw",
        "team_role_raw",
        "raw_record_sha256",
    ]
    for player in corpus:
        fields = field_map(player)
        year = fields.get("contractEndYear")
        month = fields.get("contractEndMonth")
        day = fields.get("contractEndDay")
        plausible_date = (
            isinstance(year, int)
            and 2000 <= year <= 2200
            and isinstance(month, int)
            and 1 <= month <= 12
            and isinstance(day, int)
            and 1 <= day <= 31
        )
        market = fields.get("marketValue")
        corpus_rows.append(
            {
                "player_id_decimal": int(player["playerId"]),
                "player_id_hex": f"0x{int(player['playerId']):08X}",
                "player_name": player["playerName"],
                "record_index": int(player["recordIndex"]),
                "categories": ";".join(sorted(categories[int(player["playerId"])])),
                "height": fields.get("height"),
                "weight": fields.get("weight"),
                "nationality": fields.get("nationality"),
                "market_value_raw": market,
                "market_value_eur_candidate": market * 100 if isinstance(market, int) else "",
                "contract_end_candidate": f"{year:04d}-{month:02d}-{day:02d}" if plausible_date else "",
                "annual_salary_raw": fields.get("annualSalary"),
                "current_form_arrow_raw": fields.get("currentFormArrow"),
                "transfer_flags_raw": fields.get("transferFlags"),
                "team_role_raw": fields.get("teamRole"),
                "raw_record_sha256": player["rawRecordSha256"],
            }
        )
    write_csv(args.output / "golden-player-corpus.csv", corpus_fields, corpus_rows)

    covered_bytes: set[int] = set()
    for field in ct_fields:
        if field.declared_width and 0 <= field.offset < RECORD_SIZE:
            covered_bytes.update(range(field.offset, min(RECORD_SIZE, field.offset + field.declared_width)))
    profile_bytes: set[int] = set()
    for field in profile["recordLayout"]["fields"]:
        profile_bytes.update(range(field["offset"], field["offset"] + field["width"]))

    byte_rows: list[dict[str, Any]] = []
    byte_state_counts: Counter[str] = Counter()
    for offset in range(RECORD_SIZE):
        ct_at_byte = [
            field
            for field in ct_fields
            if field.declared_width
            and field.offset <= offset < field.offset + field.declared_width
            and field.offset < RECORD_SIZE
        ]
        profile_at_byte = [
            field
            for field in profile["recordLayout"]["fields"]
            if field["offset"] <= offset < field["offset"] + field["width"]
        ]
        statuses = {field["readStatus"] for field in profile_at_byte}
        if "CONFIRMED" in statuses:
            byte_state = "PROFILE_CONFIRMED"
        elif "CANDIDATE" in statuses:
            byte_state = "PROFILE_CANDIDATE"
        elif "UNKNOWN" in statuses or ct_at_byte:
            byte_state = "LABELLED_UNKNOWN"
        else:
            byte_state = "UNLABELLED"
        byte_state_counts[byte_state] += 1
        values = [raw[offset] for _, raw in records]
        counts = Counter(values)
        byte_rows.append(
            {
                "offset_decimal": offset,
                "offset_hex": f"0x{offset:03X}",
                "byte_state": byte_state,
                "profile_fields": ";".join(field["name"] for field in profile_at_byte),
                "ct_labels": ";".join(sorted({field.description for field in ct_at_byte})),
                "distinct_byte_values": len(counts),
                "zero_count": counts.get(0, 0),
                "ff_count": counts.get(255, 0),
                "minimum": min(counts),
                "maximum": max(counts),
                "top_values": json.dumps(counts.most_common(5), separators=(",", ":")),
            }
        )
    write_csv(args.output / "record-byte-census.csv", list(byte_rows[0].keys()), byte_rows)

    evidence = {
        "schemaVersion": "pes2021.player-memory.edit-offline-atlas.v1",
        "safety": {
            "offlineOnly": True,
            "processAttachment": False,
            "memoryReads": 0,
            "memoryWrites": 0,
        },
        "inputs": {
            "dump": {"path": args.dump.as_posix(), "sha256": sha256_file(args.dump)},
            "profile": {"path": args.profile.as_posix(), "sha256": sha256_file(args.profile)},
            "ct": {"path": args.ct.as_posix(), "sha256": sha256_file(args.ct)},
        },
        "population": {
            "players": len(players),
            "uniquePlayerIds": len({int(player["playerId"]) for player in players}),
            "recordStride": RECORD_SIZE,
            "rawRecordsValidated": len(records),
        },
        "restartIdentity": stable_hash_check(players, args.baseline),
        "ctMatrix": {
            "ptrPlayerEntries": len(ct_fields),
            "playerFieldEntries": sum(
                1
                for field in ct_fields
                if not (field.offset == RECORD_SIZE and field.description.lower().startswith("end "))
            ),
            "boundaryMarkerEntries": sum(
                1
                for field in ct_fields
                if field.offset == RECORD_SIZE and field.description.lower().startswith("end ")
            ),
            "uniqueOffsets": len({field.offset for field in ct_fields}),
            "recordBytesTouchedByCt": len(covered_bytes),
            "recordBytesNotTouchedByCt": RECORD_SIZE - len(covered_bytes),
            "profileBytesModeled": len(profile_bytes),
            "outOfBoundsEntries": out_of_bounds,
            "evidenceStatusCounts": dict(sorted(status_counts.items())),
            "warning": "CT labels are hypotheses; occupancy does not confirm semantics.",
        },
        "recordByteCensus": {
            "totalBytes": RECORD_SIZE,
            "states": dict(sorted(byte_state_counts.items())),
            "warning": "LABELLED_UNKNOWN means a structural label exists, not that its semantics are proven.",
        },
        "goldenCorpus": {
            "count": len(corpus),
            "playerIds": [int(player["playerId"]) for player in corpus],
            "selection": "named controls plus deterministic boundary and structural-diversity cases",
        },
    }
    with (args.output / "evidence.json").open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(evidence, handle, ensure_ascii=False, indent=2)
        handle.write("\n")

    print(json.dumps(evidence, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
