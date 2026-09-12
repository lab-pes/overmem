# Leitura operacional da base EDIT de jogadores

Status em 2026-09-02: `OPERATIONAL_READ_RESTART_VALIDATED`.

Este fluxo localiza e le a base de jogadores carregada no PES 2021 sem exigir uma Master League. Ele nao executa Lua, Cheat Engine, injecao, freeze ou escrita de memoria.

## O que esta operacional

- descoberta de uma ancora pelo `playerId` informado;
- normalizacao do hit para a base do registro por `playerIdOffset`;
- confirmacao por parser, validador e vizinhos em `+/- 0x17C`;
- descoberta do residuo da grade dentro da regiao VirtualQuery;
- leitura em blocos da arena EDIT;
- catalogo completo da sessao, consulta de um jogador e exportacao atomica;
- preservacao de IDs como `u32`, inclusive IDs com bits altos;
- zero chamadas de escrita no caminho de descoberta/leitura.

O stride confirmado e `0x17C` (380 bytes). `0x46C94` esta `REFUTED`: era a distancia entre acertos de uma subamostragem feita com passo incorreto de 763 bytes.

## Pre-condicoes

1. PES 2021 aberto.
2. Para reproduzir o contexto desta entrega, permanecer fora de uma Master League.
3. Escolher um ID conhecido e presente no banco atual. O exemplo usa `58120` somente como ID de controle; nenhum endereco e reutilizavel.
4. Executar a partir da raiz do repositorio.

## Comandos

```powershell
Set-Location -LiteralPath "D:\git-lab-pes\overmem"
$pesProcess = Get-Process -Name PES2021 -ErrorAction Stop | Select-Object -First 1

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj -- pes2021-find-player-anchor --pid $pesProcess.Id --control-player-id 58120 --output-file files/pes2021/player-memory/live-edit-anchor.json

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj -- pes2021-scan-players --pid $pesProcess.Id --control-player-id 58120 --output-file files/pes2021/player-memory/live-edit-scan.json

dotnet run --project src/Overmem.Cli/Overmem.Cli.csproj -- pes2021-query-player --pid $pesProcess.Id --player-id 58120
```

## Criterios de sucesso

### Ancora

- `anchorAddress` nao nulo;
- `ambiguous = false`;
- `confidence.level = high`;
- candidato inclui `neighbor_stride_confirmed`;
- `session.recordStride = 380`.

### Arena

- `players.Count > 0`;
- `arenaCoverage.recordStride = 380`;
- `arenaCoverage.populatedSlots == players.Count`;
- `arenaCoverage.theoreticalSlots == populatedSlots + emptyReservedSlots`;
- `diagnostics.duplicatePlayerIds` deve ser interpretado e nao escondido;
- enderecos pertencem apenas a sessao/processo correntes.

### Consulta

- `ambiguous = false` para um ID unico;
- resultado contem endereco, indice, ID, nome, campos crus, status de evidencia, registro cru e SHA-256;
- campos `CANDIDATE` ou `UNKNOWN` nao podem ser tratados como garantias sem promocao por evidencia.

## Resultado live desta entrega

Sessao sem ML carregada, PID historico 33136:

| Medida | Resultado |
|---|---:|
| Stride | 380 (`0x17C`) |
| Residuo na regiao | `+0x10` |
| Jogadores preenchidos | 25.005 |
| IDs crus unicos | 25.005 |
| Slots vazios/reservados | 4.996 |
| Slots territoriais | 30.001 |
| IDs duplicados | 0 |
| Ancora 58120 | unica, score 16/16 |

Esses numeros confirmam esta sessao e este banco/mod set; o algoritmo deve redescobri-los e nao usa nenhum deles como constante.

## Limitacoes conhecidas

- A busca inicial ainda percorre aproximadamente 3,2 GB de regioes aceitas e levou cerca de 17 segundos nesta maquina.
- O scan JSON completo desta sessao tem aproximadamente 190 MB porque preserva campos, bytes crus e diagnosticos. Use `pes2021-query-player` para consulta humana pontual.
- O subgate estrutural de restart foi concluido em uma segunda sessao. A correlacao visual de cinco jogadores com a UI ainda e necessaria para fechar integralmente o gate semantico P6.
- Somente estrutura, ID, nome, altura e peso tem confirmacao forte no contexto EDIT. Valor de mercado e os demais campos continuam com o status declarado no perfil, em geral `CANDIDATE` ou `UNKNOWN`.
- Nenhuma escrita no PES esta autorizada por este runbook.

## Proibicoes para agentes

- nao fixar PID ou endereco live no codigo;
- nao restaurar o passo 763 nem criar perfil com stride `0x46C94`;
- nao usar apenas ID para selecionar alvo de uma futura escrita;
- nao promover salario, forma, contrato ou valor de mercado sem experimento controlado por campo;
- nao iniciar Master League nem escrita live sem gate e autorizacao explicitos de Willian.
