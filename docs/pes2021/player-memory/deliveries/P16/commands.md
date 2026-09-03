# P16 — Comandos reproduzíveis

Os comandos abaixo são somente leitura quanto ao processo do PES. Endereços e PID registrados pertencem à sessão histórica e não devem ser reutilizados.

```powershell
python docs\pes2021\player-memory\deliveries\P16\analyze_reference_contracts.py `
  --edit-dump files\pes2021\player-memory\codex-live-edit-operational.json `
  --ml-dump files\pes2021\player-memory\codex-live-ml-candidate-b-2026-09-03.json `
  --output docs\pes2021\player-memory\deliveries\P16
```

Revalidação dos AOBs no executável correto:

```powershell
$pesProcess = Get-Process -Name PES2021 | Select-Object -First 1

dotnet run --no-build --project src\Overmem.Cli -- scan-pattern `
  --pid $pesProcess.Id --module-name PES2021.exe --max-results 20 `
  --pattern "CB B8 02 00 00 00 0F 1F 40 00 66 0F 1F 84 00 00 00 00 00 0F 10 02 0F 11 01"

dotnet run --no-build --project src\Overmem.Cli -- scan-pattern `
  --pid $pesProcess.Id --module-name PES2021.exe --max-results 20 `
  --pattern "CB B8 02 00 00 00 0F 1F 40 00 66 0F 1F 84 00 00 00 00 00"

dotnet run --no-build --project src\Overmem.Cli -- scan-pattern `
  --pid $pesProcess.Id --module-name PES2021.exe --max-results 20 `
  --pattern "48 8B 40 2C 48 89 02"
```

Validação do analisador:

```powershell
python -m py_compile docs\pes2021\player-memory\deliveries\P16\analyze_reference_contracts.py
git diff --check
```
