# P11 - Comandos reproduziveis

```powershell
Set-Location -LiteralPath "D:\git-lab-pes\overmem"

dotnet test Overmem.slnx --no-restore

$pesProcess = Get-Process -Name PES2021 -ErrorAction Stop | Select-Object -First 1

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj --no-build -- pes2021-find-player-anchor --pid $pesProcess.Id --control-player-id 58120 --output-file files/pes2021/player-memory/codex-live-anchor-operational.json

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj --no-build -- pes2021-scan-players --pid $pesProcess.Id --control-player-id 58120 --output-file files/pes2021/player-memory/codex-live-edit-operational.json

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj --no-build -- pes2021-query-player --pid $pesProcess.Id --player-id 58120

git diff --check
git status --short
```

Pre-condicao live: PES aberto e nenhuma Master League carregada. Nao copie PID nem enderecos de uma execucao anterior.
