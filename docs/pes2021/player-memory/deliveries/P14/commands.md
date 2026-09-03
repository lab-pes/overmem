# P14 — Comandos

## Testes sintéticos

```powershell
python -m unittest docs/pes2021/player-memory/deliveries/P14/test_compare_edit_ml.py -v
```

## Comparação EDIT × ML futura

```powershell
python docs/pes2021/player-memory/deliveries/P14/compare_edit_ml.py `
  --edit files/pes2021/player-memory/EDIT-DUMP.json `
  --ml files/pes2021/player-memory/ML-DUMP.json `
  --output files/pes2021/player-memory/edit-ml-comparison
```

Os nomes acima são placeholders deliberados. Não reutilizar endereços nem PIDs de capturas anteriores.

## Validação completa atual

```powershell
python -m unittest docs/pes2021/player-memory/deliveries/P14/test_compare_edit_ml.py -v
dotnet test Overmem.slnx --no-restore
git diff --check
```
