# Overmem source lineage

These 12 ZIP files preserve exact original bytes from the source directories inventoried on 2026-09-12. They are historical snapshots, outside the active solution. Each entry is checked against `docs/consolidation/preservation-manifest.json`.

The maintained implementation is in `src/` and `tests/`. A snapshot may contain superseded code, unfinished prototypes, old paths, runtime patches, or contradictory research claims. Archiving preserves that work; it does not establish runtime validity. Original attribution and reference documents are retained.

Use `python scripts/consolidation/verify_preservation.py` to verify the repository snapshots. Use `--vault <local-vault>` to verify the additional local evidence. Restore a snapshot without overwriting a working directory:

```powershell
python scripts/consolidation/restore_source.py --source gearlabs --output artifacts/restored-gearlabs --source-only
```

The full source inventory and active-versus-historical decisions are documented in [the consolidation report](../../docs/consolidation/README.md).
