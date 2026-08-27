# Testes, benchmark e evidência

## Pirâmide de testes

### 1. Unitários

Obrigatórios:

- little-endian para todo `u16`;
- layout e stride configurável;
- data válida, incluindo mês/dia realmente possível;
- IDs `0`, `5000`, `5001`, `32768`, `49169`, `65534` aceitos como equipe;
- `65535` rejeitado como sentinela;
- home/away, placares e índice/endereço;
- `TeamKey` e igualdade;
- resolução exata, fallback único, ausência, ambiguidade e conflito;
- aliases `secondary_id`/`league_id` apenas na entrada;
- overflow e limites de bloco/região;
- score/razões de candidato e empate ambíguo;
- chave e invalidação de cache;
- ordenação determinística;
- saída sempre `FIXTURES_ONLY`.

O fake gateway deve contar chamadas e bytes, permitir leitura parcial e simular duas regiões contíguas/não contíguas.

### 2. Offline e Fixtures

A validação offline é dividida em dois níveis complementares:

#### A. Fixtures Sintéticas em Memória (P0–P4)
Para destravar os pacotes P0 a P4 sem dependência de dumps binários reais do jogo, a suite unitária utiliza `SyntheticCalendarMemoryGenerator`. Esse gerador cria em memória buffers `byte[]` e fakes de leitura simulando o stride canônico de 596 bytes:
- registro individual válido com datas e pontuações controladas;
- sentinelas `0xFFFF` e datas impossíveis;
- IDs de participantes acima de 5000 (`32768`, `32784`, `49169`);
- cenários de colisão (`32768` com múltiplos `teamLiga`);
- múltiplos blocos contíguos e não contíguos simulando fronteiras de regiões de memória;
- calendários intercalando duas ou mais competições.

#### B. Dumps Binários Sanitizados (P7)
No pacote P7, capturas reais do processo `PES2021.exe` são sanitizadas e armazenadas sob:

```text
tests/Overmem.Extensions.Pes2021.Tests/Fixtures/CompetitionFixtures/<fixture-id>/
  memory.bin
  manifest.json
  expected.json
  SHA256SUMS
```

`manifest.json` contém: schema, tamanho, SHA-256, stride, base lógica sanitizada, perfil, versão do executável, patch/mods, data UTC, procedimento de captura, intervalos removidos/zerados e status epistemológico.

Sanitização:

- copiar apenas o intervalo necessário do array;
- substituir endereços reais por base lógica `0x10000000` no esperado;
- não incluir save, nome de usuário, caminhos pessoais ou regiões adjacentes;
- preservar integralmente os bytes dos registros usados;
- nunca editar o dump depois de gerar hashes; uma correção cria novo fixture id.

Casos mínimos da captura real:

- bloco de competição real completo;
- referência 17 com os 380 jogos quando o dump for capturado.

### 3. Live read-only

Pré-condições registradas:

- versão/hash de `PES2021.exe`;
- patch/mods ativos;
- save/carreira e data visível;
- perfil e mapas com SHA-256;
- PID e início do processo;
- build/commit do Overmem.

Procedimento de restart:

1. iniciar PES e anexar;
2. descobrir, extrair e salvar evidência A;
3. registrar PID, início, âncora, bases e hashes de amostra;
4. encerrar completamente o PES;
5. confirmar que o PID anterior não existe;
6. iniciar novamente o PES e carregar a mesma carreira;
7. anexar com nova sessão;
8. executar sem fornecer endereços anteriores;
9. salvar evidência B;
10. comprovar redescoberta, resultado semântico igual e ausência de reutilização do cache A.

Executar também uma competição fora da referência brasileira. Os valores esperados dessa competição devem ser registrados no momento do teste; não inventar contagens antes da captura.

## Baseline de aceite 17

A baseline autocontida está em [`examples/acceptance-competition-17.json`](examples/acceptance-competition-17.json). Gates:

- 380 fixtures;
- 20 `TeamKey` distintos;
- 38 fixtures contendo `32784/313`;
- 38 rodadas lógicas com 10 partidas quando a captura reproduzir essa estrutura;
- `32784/313 -> SANTOS`;
- `32768/482 -> ATHLETICO PARANAENSE`;
- zero chaves não resolvidas com o mapa de exemplo;
- competição de cada registro igual a 17;
- nenhum `teamId=65535`.

Essa baseline é `BASELINE_IMPORTADO` até o primeiro teste live deste repositório. Depois, a entrega adiciona a evidência reproduzida sem sobrescrever o arquivo histórico.

## Benchmark

Comparar no mesmo processo, base, record limit e máquina:

- **legado:** uma leitura por registro através do fluxo atual;
- **blocos-512**;
- **blocos-1024**.

Executar uma rodada de aquecimento e no mínimo cinco medições por variante, alternando a ordem. Registrar mediana e p95, não apenas a melhor execução.

Campos obrigatórios:

```text
variant,run,record_limit,block_records,read_calls,bytes_requested,bytes_read,duration_ms,fixture_count,process_id,profile_sha256,overmem_commit
```

Gate funcional: resultados semanticamente idênticos. Gate de eficiência: chamadas próximas de `ceil(recordLimit/blockRecords)` no caminho em blocos. O objetivo de “segundos” é aceito apenas com os números reais anexados.

## Prova de nenhuma escrita

1. teste arquitetural garante que os novos serviços dependem apenas do reader estreito;
2. fake gateway falha imediatamente se `WriteAsync` for chamado;
3. teste MCP/CLI captura operações antes/depois e exige zero `write_value` e zero freeze originados pelo comando;
4. revisão estática procura chamadas de escrita no namespace `Fixtures`;
5. relatório live declara explicitamente `writeOperations=0`.

O fato de o processo ter sido aberto com direitos amplos não é prova de escrita. Um modo de attachment read-only é hardening desejável, mas o gate deste incremento é ausência verificável de operação de escrita no fluxo.

## Comandos de validação do repositório

```powershell
dotnet build Overmem.slnx
dotnet test Overmem.slnx
git diff --check
```

> [!NOTE]
> **Ambiente Windows com MCP Server ativo:** Caso o `Overmem.McpServer` esteja em execução na IDE/terminal durante o desenvolvimento, o `dotnet build` da solution completa pode acusar arquivo bloqueado (`MSB3026`). Nesses casos, utilize o ciclo focado na extensão:
> ```powershell
> dotnet test tests/Overmem.Extensions.Pes2021.Tests/Overmem.Extensions.Pes2021.Tests.csproj --no-build
> ```
> ou encerre os processos MCP antes de um build global.

Adicionar testes de parsing de CLI, ajuda, serialização MCP e smoke da ferramenta nova.

## Pacote de evidência por gate

```text
artifacts/pes2021-fixtures/<timestamp-utc>/
  environment.json
  discovery.json
  extraction.json
  benchmark.csv
  operation-log-before.json
  operation-log-after.json
  test-summary.txt
  SHA256SUMS
```

O diretório de artifacts pode ficar fora do Git quando contiver capturas live; a entrega versiona somente dumps sanitizados e relatórios aprovados. O relatório final marca cada conclusão como `observado`, `hipótese`, `confirmado` ou `refutado`.

## Matriz de aceite

| Gate | Evidência | Bloqueia |
|---|---|---|
| G1 contratos | testes de tipos/parser e busca textual de terminologia | leitura em blocos |
| G2 blocos | igualdade legado/novo + contadores | descoberta |
| G3 descoberta | candidatos/razões + restart | extrator público |
| G4 extração/mapas | baseline 17 e colisões | cache |
| G5 cache/diagnóstico | reuse/rediscovery/refusal testados | live final |
| G6 segurança | zero write/freeze | integração Sider |
| G7 live | referência 17 + segunda competição + benchmark | conclusão |

