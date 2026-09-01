# Delegacao pronta: P0 - contratos do mapeamento EDIT

## Destinatario

Agente de implementacao Minimax M3 executando via OpenCode.

## Repositorio e branch

```text
cwd: D:\git-lab-pes\overmem
branch sugerida: codex/pes2021-player-edit-p0
```

Nao modifique nem execute arquivos em:

```text
C:\Users\Willian\Documents\My Cheat Tables
```

## Objetivo unico desta entrega

Implementar somente o pacote P0 documental/contratual do mapeamento de jogadores `EDIT_BASE` descrito em:

```text
docs/pes2021/player-memory/feasibility-study.md
docs/pes2021/player-memory/edit-first-decision.md
docs/pes2021/player-memory/implementation-packages.md
```

Nao implementar scanner, parser de memoria, CLI/MCP de jogadores ou qualquer escrita nesta entrega.

## Contexto obrigatorio

A leitura realizada em 2026-08-31 ocorreu com `PES2021.exe` aberto e nenhuma Master League carregada. A familia `0x17C` observada e a arena base do EDIT, carregada independentemente de ML.

Existirao dois mapeamentos independentes:

- `EDIT_BASE`: alvo atual;
- `MASTER_LEAGUE`: trilha futura M0-M4, fora do escopo de P0.

Cobertura total nao significa semantica total. `UNKNOWN` e um resultado valido. O contrato deve distinguir:

1. cobertura territorial `0x000..0x17B`;
2. cobertura de todos os slots da arena EDIT;
3. cobertura de jogadores/IDs/fingerprints;
4. cobertura semantica dos campos.

Leia tambem obrigatoriamente:

```text
docs/pes2021/player-memory/edit-live-evidence-2026-08-31.md
```

O baseline live dessa pagina deve aparecer apenas como evidencia de teste e exemplo de contrato. PID e enderecos nao podem virar constantes. O contrato de ID deve usar `u32` opaco e aceitar bits altos sem lhes atribuir significado.

## Escopo autorizado

1. Criar `docs/pes2021/player-memory/source-manifest.json` com caminhos, SHA-256, data de captura e papel de cada fonte.
2. Criar contratos JSON de exemplo para:
   - identidade do processo/perfil;
   - resultado de ancora EDIT;
   - manifest de segmentos da arena EDIT;
   - classificacao de slot;
   - snapshot de jogador;
   - colisoes e rejeicoes;
   - cobertura territorial do registro;
   - relatorio de cobertura EDIT;
   - prova de zero escritas;
   - plano/apply/rollback futuros, apenas como contrato documental.
3. Documentar enums/status:
   - `PlayerRecordContext`;
   - `EvidenceStatus`;
   - `EditSlotClassification`;
   - codigos de erro estaveis.
4. Adicionar testes pequenos de round-trip `System.Text.Json` para os contratos que forem representados em C# nesta fase.
5. Produzir os artefatos de entrega P0.

## Fora do escopo

- nenhuma chamada a `IProcessMemoryGateway`;
- nenhum attach a `PES2021.exe`;
- nenhuma leitura live;
- nenhuma escrita, freeze, hook ou injecao;
- nenhum parser de registro;
- nenhum scanner de regioes;
- nenhum perfil `pes2021-player-ml-v1.json`;
- nenhuma promocao de `0x12C`, `0x12E`, `0x15C`, `0x178` ou `0x179` para semantica nao comprovada;
- nenhuma dependencia runtime do diretorio externo de Cheat Tables.

## Fontes e hashes que devem ser verificados

Recalcule; nao apenas copie os valores abaixo:

| Fonte | SHA-256 esperado |
|---|---|
| `C:\Users\Willian\Documents\My Cheat Tables\scripts\players\ZerarValorMercado.lua` | `27A8486E0145725EB8D9C370566038C50B98BC5E5AEB8009927E0C076DF1D809` |
| `...\player_tool\operations.lua` | `A3F540FCE2E698914BF2DB4669CE1082F2FB931DD62F1CF6D80F4E407C7B1CC7` |
| `...\player_tool\reader_v5.lua` | `D9F9B7919BF4CD8EAAF07983E23397FE1580EE2820D088E814A97A73563AD115` |
| `...\player_tool\schema_v5.lua` | `6BD22B451085FE4D4209D7DB5FA93152CE78683439D760CA88D33BFC7144050E` |
| `...\work\cheat-engine\tables\PES 2021 - v21.1.0.CT` | `DA67EB5C8F7B13243AD5BE654D618EA5E4BAEB52449FECBC453144AF6C89AF7C` |
| `...\jogadores_pes2021.txt` | `0C771B409267009D28C6CC21C093113FB23749A97532676F07CB22EEA7047408` |

Verifique tambem que a CT externa e `files\PES 2021 - v21.1.0.CT` continuam byte-identicas.

## Codigos de erro minimos

```text
PES2021_PLAYER_PROFILE_INVALID
PES2021_PLAYER_ANCHOR_NOT_FOUND
PES2021_PLAYER_ANCHOR_AMBIGUOUS
PES2021_PLAYER_RECORD_INVALID
PES2021_PLAYER_ID_AMBIGUOUS
PES2021_PLAYER_CONTEXT_INCOMPATIBLE
PES2021_PLAYER_STALE_SESSION
PES2021_EDIT_ARENA_INCOMPLETE
PES2021_EDIT_TERRITORY_INCOMPLETE
PES2021_PLAYER_WRITE_NOT_AUTHORIZED
PES2021_PLAYER_EXPECTED_BYTES_MISMATCH
PES2021_PLAYER_VERIFY_FAILED
PES2021_PLAYER_ROLLBACK_FAILED
```

## Contratos de cobertura obrigatorios

### Territorial

Deve conseguir representar e validar:

```text
recordStart = 0x000
recordEndInclusive = 0x17B
recordSize = 0x17C
uncoveredBytes = 0
unjustifiedOverlaps = 0
```

Classes permitidas: `CONFIRMED_FIELD`, `CANDIDATE_FIELD`, `UNKNOWN`, `PADDING_OR_RESERVED`, `SHARED_BIT_CONTAINER`.

### Arena EDIT

Cada segmento declara inicio, fim, stride, residuo e slots teoricos. Cada slot recebe exatamente uma classificacao:

```text
VALID_PLAYER
INVALID_OR_EMPTY_SLOT
HOLE
UNREADABLE
PARTIAL_READ
AMBIGUOUS_RECORD
```

O contrato deve permitir verificar:

```text
classifiedSlots == theoreticalSlots
unaccountedSlots == 0
```

### Jogadores

O contrato deve separar total de registros, IDs unicos, IDs duplicados, fingerprints unicos/duplicados, ausentes de catalogo e denominadores das porcentagens.

Inclua um fixture contratual que consiga representar sem truncamento:

```text
rawPlayerId = 0x8000003E
playerName  = Franz Gonzales
idFlags     = UNKNOWN
```

## Identidade e ambiguidade

`playerId` sozinho nao e identidade. O contrato de identidade de sessao inclui:

- attachment/process ID;
- process start time;
- profile ID/version/SHA-256;
- record address;
- player ID;
- fingerprint;
- record context.

Consulta ambigua deve poder retornar todos os candidatos ou o erro `PES2021_PLAYER_ID_AMBIGUOUS`. Nunca modele selecao implicita do primeiro resultado.

## Artefatos obrigatorios da entrega

```text
docs/pes2021/player-memory/deliveries/P0/
  summary.md
  commands.md
  test-results.txt
  evidence.json
  review-request.md
```

`summary.md` lista arquivos alterados, decisoes, limitacoes e rollback. `review-request.md` deve pedir explicitamente auditoria de:

1. separacao EDIT versus ML;
2. quatro metricas de cobertura;
3. representacao de `UNKNOWN`;
4. identidade/colisao;
5. ausencia de dependencia runtime externa;
6. ausencia de memoria live e de escrita.

## Comandos obrigatorios

Execute e registre saida completa em `test-results.txt`:

```powershell
Set-Location -LiteralPath "D:\git-lab-pes\overmem"

Get-FileHash -Algorithm SHA256 -LiteralPath `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\ZerarValorMercado.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\player_tool\operations.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\player_tool\reader_v5.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\scripts\players\player_tool\schema_v5.lua", `
  "C:\Users\Willian\Documents\My Cheat Tables\work\cheat-engine\tables\PES 2021 - v21.1.0.CT", `
  "C:\Users\Willian\Documents\My Cheat Tables\jogadores_pes2021.txt"

dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build
git diff --check
git status --short
```

Nao mate processos do usuario para liberar DLL. Se houver lock, documente e use o teste estreito que puder ser executado com seguranca.

## Criterios de aceite

- fontes e hashes reproduzidos;
- CT externa e interna confirmadas como identicas;
- contratos EDIT e ML separados;
- apenas EDIT exposto como trilha ativa;
- quatro metricas de cobertura representadas sem ambiguidade;
- `UNKNOWN` suportado sem ser tratado como falha;
- identidade nao depende apenas de player ID;
- exemplos JSON fazem round-trip quando aplicavel;
- zero codigo de leitura/escrita de processo adicionado;
- build/testes aprovados;
- entrega P0 completa.

## Condicao de parada

Ao concluir P0, pare e solicite revisao do Codex. Nao inicie P1 automaticamente, mesmo que todos os testes passem.
