from __future__ import annotations

import base64
import hashlib
import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("compare_edit_ml.py")
SPEC = importlib.util.spec_from_file_location("compare_edit_ml", MODULE_PATH)
assert SPEC and SPEC.loader
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def player(player_id: int, name: str, changes: dict[int, int] | None = None, market: int = 0):
    raw = bytearray(380)
    raw[48:52] = player_id.to_bytes(4, "little")
    encoded = name.encode("utf-8")
    raw[56 : 56 + len(encoded)] = encoded
    raw[372:376] = market.to_bytes(4, "little")
    for offset, value in (changes or {}).items():
        raw[offset] = value
    digest = hashlib.sha256(raw).hexdigest()
    return {
        "recordIndex": player_id & 0xFFFF,
        "playerId": player_id,
        "playerName": name,
        "fields": [
            {
                "name": "marketValue",
                "rawLong": market,
                "rawString": None,
                "evidenceStatus": "CANDIDATE",
            }
        ],
        "rawRecord": base64.b64encode(raw).decode("ascii"),
        "rawRecordSha256": digest,
    }


class CompareEditMlTests(unittest.TestCase):
    def test_comparison_separates_exact_changed_unmatched_and_fingerprint_mismatch(self):
        edit = {
            "players": [
                player(1, "Exact"),
                player(2, "Changed", market=100),
                player(3, "Only Edit"),
                player(0x80000004, "Guard Name"),
            ]
        }
        ml = {
            "players": [
                player(1, "Exact"),
                player(2, "Changed", changes={312: 0xEA}, market=250),
                player(4, "Only ML"),
                player(0x80000004, "Different Person", changes={10: 99}),
            ]
        }
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            edit_path = root / "edit.json"
            ml_path = root / "ml.json"
            output = root / "out"
            edit_path.write_text(json.dumps(edit), encoding="utf-8")
            ml_path.write_text(json.dumps(ml), encoding="utf-8")

            evidence = MODULE.compare(edit_path, ml_path, output)

            self.assertEqual(4, evidence["counts"]["editPlayers"])
            self.assertEqual(4, evidence["counts"]["mlPlayers"])
            self.assertEqual(1, evidence["counts"]["exactMatches"])
            self.assertEqual(1, evidence["counts"]["changedMatches"])
            self.assertEqual(1, evidence["counts"]["fingerprintMismatches"])
            self.assertEqual(1, evidence["counts"]["onlyEdit"])
            self.assertEqual(1, evidence["counts"]["onlyMl"])
            self.assertEqual(2, evidence["counts"]["offsetsChangedInAnyPlayer"])

            player_csv = (output / "player-diff-summary.csv").read_text(encoding="utf-8")
            self.assertIn("MATCHED_EXACT", player_csv)
            self.assertIn("MATCHED_CHANGED", player_csv)
            self.assertIn("FINGERPRINT_MISMATCH", player_csv)
            self.assertIn("ONLY_EDIT", player_csv)
            self.assertIn("ONLY_ML", player_csv)

            field_csv = (output / "player-field-diffs.csv").read_text(encoding="utf-8")
            self.assertIn("marketValue", field_csv)

    def test_duplicate_ids_are_rejected(self):
        duplicate = player(7, "Duplicate")
        with self.assertRaisesRegex(ValueError, "duplicate player IDs"):
            MODULE.index_players([duplicate, duplicate], "fixture")


if __name__ == "__main__":
    unittest.main()
