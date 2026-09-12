# Comandos de Verificacao: R0-P0P1-LIVE-ALIGNMENT

Execute em PowerShell para reproduzir a verificacao completa do pacote `R0-P0P1-LIVE-ALIGNMENT`:

```powershell
Set-Location -LiteralPath "D:\git-lab-pes\overmem"

# 1. Verificacao de integridade das fontes externas
Get-FileHash -Algorithm SHA256 -LiteralPath `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\ZerarValorMercado.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\player_tool\operations.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\player_tool\reader_v5.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\player_tool\schema_v5.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\work\cheat-engine\tables\PES 2021 - v21.1.0.CT", `
  "C:\Users\Willian\Documents\My Cheat Tables\jogadores_pes2021.txt"

# 2. Verificacao de integridade dos arquivos locais do perfil e fixtures
Get-FileHash -Algorithm SHA256 -LiteralPath `
  "files\pes2021\player-memory\pes2021-player-edit-v1.json", `
  "files\pes2021\player-memory\pes2021-player-record-v1.json", `
  "docs\pes2021\player-memory\wire-examples\player-high-bit-id.json", `
  "docs\pes2021\player-memory\wire-examples\scan.json"

# 3. Build completo da solucao
dotnet build Overmem.slnx

# 4. Execucao de todos os testes da solucao
dotnet test Overmem.slnx --no-build

# 5. Execucao focada dos testes da extensao PES 2021
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build

# 6. Verificacao de diff e estado do worktree
git diff --check
git status --short
```
