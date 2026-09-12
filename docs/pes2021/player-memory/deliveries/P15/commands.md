# P15 — Comandos reproduzíveis

Os endereços abaixo pertencem somente à sessão histórica documentada. Em outra sessão, executar primeiro `pes2021-find-player-anchor` e usar somente candidatos retornados naquela execução.

```powershell
$pesProcess = Get-Process -Name PES2021 -ErrorAction Stop | Select-Object -First 1

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj --no-build -- `
  pes2021-find-player-anchor --pid $pesProcess.Id --control-player-id 58120 `
  --output-file files/pes2021/player-memory/live-ml-anchor.json

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj --no-build -- `
  pes2021-scan-players --pid $pesProcess.Id --control-player-id 58120 `
  --anchor-address <validated-candidate-from-current-run> `
  --output-file files/pes2021/player-memory/live-ml-arena.json
```

Análise compacta:

```powershell
python docs/pes2021/player-memory/deliveries/P15/analyze_ml_arena.py `
  --dump files/pes2021/player-memory/live-ml-arena.json `
  --output docs/pes2021/player-memory/deliveries/P15
```

Comparação com o EDIT:

```powershell
python docs/pes2021/player-memory/deliveries/P14/compare_edit_ml.py `
  --edit files/pes2021/player-memory/EDIT-DUMP.json `
  --ml files/pes2021/player-memory/live-ml-arena.json `
  --output files/pes2021/player-memory/edit-ml-comparison
```
