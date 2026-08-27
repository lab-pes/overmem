# Contratos CLI e MCP

## Convenções

- MCP usa `snake_case` no nome da ferramenta e `camelCase` nos argumentos.
- CLI usa kebab-case.
- endereços aceitam decimal ou hexadecimal com `0x` na CLI; respostas usam string hexadecimal.
- `competitionId` e `teamId` são obrigatórios na descoberta; não existe default `29`.
- valores omitidos vêm do perfil, e a resposta informa o valor efetivo.
- o schema de saída é o mesmo em CLI e MCP.

## Descoberta de âncora

MCP: `pes2021_find_fixture_anchor`

```text
attachmentId       Guid obrigatório
competitionId      u16 obrigatório
teamId             u16 obrigatório
teamLiga           u16 opcional
profilePath        string opcional
scanStartAddress   ulong opcional
scanStopAddress    ulong opcional
blockRecords       int opcional
maxScanBytes       ulong opcional
```

CLI:

```powershell
dotnet run --project src/Overmem.Cli -- `
  pes2021-find-fixture-anchor `
  --name PES2021 `
  --competition-id 17 `
  --team-id 32784 `
  --team-liga 313 `
  --profile-file .\profiles\pes2021-fixtures.json
```

A CLI anexa, executa e desanexa como os comandos atuais. O MCP usa attachment existente. Saída: `FixtureAnchorResult` de [contracts.md](contracts.md).

## Extração de partidas

MCP: `pes2021_extract_competition_fixtures`

```text
attachmentId                Guid obrigatório
competitionId               u16 obrigatório
teamId                      u16 opcional se um endereço válido for fornecido
teamLiga                    u16 opcional
calendarBaseAddress         ulong opcional
competitionBlockBaseAddress ulong opcional
anchorAddress               ulong opcional
profilePath                 string opcional
competitionMapPath          string opcional
teamMapPath                 string opcional
blockRecords                int opcional
recordLimit                 int opcional
```

Regras de entrada:

- sem qualquer endereço, `teamId` é obrigatório para descoberta;
- apenas uma das duas bases pode ser fornecida;
- `anchorAddress` pode coexistir com uma base somente para validação cruzada;
- endereço fornecido é validado para competição/stride antes da leitura ampla;
- `recordLimit` não pode exceder o máximo do perfil;
- `blockRecords` não pode exceder o máximo do perfil;
- `--output-file` (CLI opcional): quando especificado, salva o JSON resultante atomicamente (gravação em `.tmp` no mesmo diretório seguida de renomeação/substituição atômica via SO), prevenindo leituras parciais por módulos externos ou Lua/Sider.

CLI:

```powershell
dotnet run --project src/Overmem.Cli -- `
  pes2021-extract-competition-fixtures `
  --name PES2021 `
  --competition-id 17 `
  --team-id 32784 `
  --team-liga 313 `
  --profile-file .\profiles\pes2021-fixtures.json `
  --competition-map-file .\maps\competitions.csv `
  --team-map-file .\maps\teams.csv `
  --block-records 1024 `
  --output-file .\artifacts\fixtures-competition-17.json
```

Exemplo lógico abreviado de resposta:

```json
{
  "schemaVersion": "pes2021.competition-fixtures.v1",
  "status": "FIXTURES_ONLY",
  "warning": "Raw scores do not prove that a fixture was completed. Do not derive standings from this payload.",
  "competitionId": 17,
  "fixtureCount": 380,
  "distinctTeamCount": 20,
  "session": {
    "process": { "processId": 1234, "processStartedAtUtc": "2026-08-27T19:00:00Z" },
    "recordStride": 596,
    "calendarArrayBaseAddress": "0x0000000000000000",
    "competitionBlockBaseAddress": "0x0000000000000000",
    "anchorAddress": "0x0000000000000000",
    "cacheDisposition": "DISCOVERED"
  },
  "unresolvedTeamKeys": [],
  "catalogConflicts": [],
  "fixtures": [],
  "diagnostics": {
    "readCalls": 0,
    "bytesRead": 0,
    "stageDurationMs": {}
  }
}
```

Zeros no exemplo são placeholders documentais, nunca endereços de fallback.

## Compatibilidade das ferramentas atuais

- `pes2021_find_calendar_base`: remover o default silencioso 29. Durante uma versão de transição, omissão deve usar âncora declarada no perfil ou falhar com instrução clara.
- `pes2021_dump_calendar_date`, `pes2021_compare_calendar_dates` e `pes2021_calendar_summary`: manter nomes e formas de resposta, mas delegar ao leitor/parser em blocos.
- adicionar métricas pode ser aditivo; não renomear campos legados na mesma versão.
- documentar `competitionCode` legado como alias de `competitionId` apenas nas ferramentas antigas.

## Erros estáveis

Enquanto o host não tiver envelope JSON de erro, exceptions devem começar com um código:

| Código | Condição |
|---|---|
| `PES2021_PROFILE_INVALID` | schema, offset ou limite inválido |
| `PES2021_INPUT_INVALID` | combinação de argumentos inválida |
| `PES2021_NO_SCAN_REGION` | nenhum intervalo atende ao filtro |
| `PES2021_ANCHOR_NOT_FOUND` | zero candidato validado |
| `PES2021_ANCHOR_AMBIGUOUS` | candidatos empatados sem prova |
| `PES2021_BASE_INVALID` | endereço fornecido/cache não revalida |
| `PES2021_PARTIAL_READ` | leitura incompleta sem recuperação segura |
| `PES2021_CATALOG_INVALID` | arquivo ilegível/schema inválido |
| `PES2021_EXTRACTION_EMPTY` | base válida, zero fixture da competição |

A CLI retorna `1` e escreve o erro em stderr. Cancelamento continua sendo cancelamento, não deve ser convertido em erro de domínio.

## Saída para Sider no futuro

Fluxo permitido:

```text
Overmem -> resultado v1 validado -> escritor externo com rename atômico -> Lua somente lê/exibe
```

O arquivo publicado deve conter `schemaVersion`, `status`, `generatedAtUtc`, identidade não sensível da sessão, fixtures e checksum. O Lua rejeita schema desconhecido ou JSON parcial. Classificação permanece fora do contrato v1.

