# Requisitos e decisões

## Vocabulário obrigatório

| Termo | Definição | Não significa |
|---|---|---|
| `CompetitionId` | valor `u16` armazenado em `record+0x00`; identifica a competição do registro | `teamLiga` |
| `TeamKey` | par imutável `(teamId, teamLiga)` de um participante | `teamId` isolado |
| `teamLiga` | segundo `u16` do participante no registro; compõe a identidade runtime da equipe | ID da competição ou liga exibida garantida |
| `secondary_id` | nome legado usado por catálogos externos para o mesmo segundo valor runtime | prova semântica de liga |
| `Fixture` | registro plausível da agenda pertencente à competição solicitada | resultado final validado |
| `AnchorAddress` | endereço de um registro confirmado usado para localizar a estrutura na instância atual | base durável |
| `CompetitionBlockBaseAddress` | primeiro registro do bloco contíguo confirmado da competição | início de todo o array |
| `CalendarArrayBaseAddress` | início do array completo, quando a normalização do perfil o confirmar | âncora ou bloco da competição |
| `CalendarSession` | identidade do processo, attachment, perfil, bases e amostra de validação | cache persistente |
| `FIXTURES_ONLY` | a saída contém agenda e placares crus | classificação |

No código novo, `CompetitionCode` deve ser migrado para `CompetitionId`. Compatibilidade de serialização pode ser mantida temporariamente, mas documentação e novas APIs não usarão os termos como sinônimos.

## Requisitos funcionais

### RF-01 — Tipos explícitos

Criar `CompetitionId`, `TeamKey`, `Fixture`, `CalendarSession`, `FixtureExtractionStatus`, `NameResolution` e `ExtractionDiagnostics`. O parser retorna dados crus; resolução de nomes acontece depois.

### RF-02 — Parser único

Um único `Pes2021CalendarRecordParser` decodifica todos os consumidores do calendário. Ele recebe `ReadOnlySpan<byte>`, endereço e índice; não lê memória, não consulta arquivos e não resolve nomes.

### RF-03 — Leitura em blocos

`ReadCalendarRecordsBlockAsync` lê por padrão 1024 registros, aceita configuração entre 1 e o máximo seguro do perfil e preserva o índice absoluto. Leituras que cruzariam o fim de uma região devem ser divididas ou reduzidas; leitura parcial nunca pode ser tratada como bloco completo.

### RF-04 — Migração de consumidores

`DumpDateAsync`, `CompareDatesAsync` e `CalendarSummaryAsync` devem consumir o mesmo enumerador em blocos. `CompareDatesAsync` deve fazer uma única passagem ou reutilizar um snapshot da mesma sessão, e não reler toda a agenda duas vezes.

### RF-05 — Descoberta de âncora

Adicionar `FindFixtureAnchorAsync` e `pes2021_find_fixture_anchor`. `competitionId` e `teamId` são obrigatórios; `teamLiga` é opcional. Não haverá default oculto `29`.

### RF-06 — Escopo da busca

A descoberta considera apenas regiões com `State=Commit`, `Type=Private`, `IsReadable=true`, `IsWritable=true` e `IsExecutable=false`, salvo alteração explícita no perfil. A resposta lista regiões aceitas, recusadas e motivo.

### RF-07 — Validação e normalização

Cada hit precisa:

- decodificar um registro válido;
- pertencer à competição solicitada;
- conter a equipe em casa ou fora;
- respeitar `teamLiga` quando informado;
- participar de uma sequência plausível no stride do perfil;
- produzir base normalizada conforme a estratégia do perfil.

Empate entre candidatos sem evidência suficiente resulta em `AMBIGUOUS_ANCHOR`; não se escolhe o menor endereço silenciosamente.

### RF-08 — Extração nativa

Adicionar `ExtractCompetitionFixturesAsync`, MCP `pes2021_extract_competition_fixtures` e CLI `pes2021-extract-competition-fixtures`. A saída contém metadados de sessão, partidas, equipes não resolvidas, conflitos e diagnóstico.

### RF-09 — Catálogos configuráveis

Mapas de competição e equipe são selecionáveis por argumento, perfil ou configuração. A ordem de precedência é definida em [Perfis e mapas](configuration-and-maps.md). Nenhum caminho do GOGOSZ/WORLD é obrigatório ou embutido na extração.

### RF-10 — Resolução segura

Primeiro tentar `TeamKey`. Fallback por `teamId` só ocorre quando esse ID possui exatamente uma chave e um nome não conflitante no catálogo carregado. Toda ausência, duplicidade e colisão é reportada.

### RF-11 — Cache de sessão

O cache é somente em memória e inclui attachment, PID, início do processo quando disponível, perfil e hash do perfil, stride, base e amostra de revalidação. Troca de qualquer identidade ou falha de amostra invalida a entrada.

### RF-12 — Diagnóstico

Toda descoberta e extração informa no mínimo: disposição do cache, regiões consideradas, bytes solicitados/lidos, chamadas de leitura, registros decodificados/descartados, candidatos e motivos, duração por etapa e confiança.

### RF-13 — Somente leitura

O fluxo novo depende de uma interface estreita de leitura e não chama escrita ou freeze. A verificação live deve registrar o journal antes/depois e falhar se houver operação de escrita atribuível ao fluxo.

### RF-14 — Publicação futura

A saída JSON pode ser gravada por um consumidor em arquivo atômico para o Sider. A CLI deve expor a opção `--output-file <caminho>` realizando gravação atômica (`.tmp` no mesmo diretório seguida de substituição/rename atômico). O Overmem não passa a depender do Lua e o Lua futuro apenas exibe o JSON validado.

## Requisitos não funcionais

- **RNF-01 Desempenho:** 13.014 registros com bloco 1024 exigem aproximadamente 13 leituras de payload, mais operações pequenas de descoberta/revalidação. O gate é medido em hardware real; “segundos” não será substituído por um número artificial sem baseline.
- **RNF-02 Determinismo:** o mesmo dump, perfil e catálogo geram o mesmo conjunto e ordenação de fixtures.
- **RNF-03 Limites:** validar overflow de endereço, `blockRecords * stride`, limites da região e cancelamento entre blocos.
- **RNF-04 Compatibilidade:** APIs antigas continuam funcionando durante a migração; a remoção do default `29` pode ser breaking e precisa constar no changelog.
- **RNF-05 Observabilidade:** métricas não incluem conteúdo bruto integral de memória nos logs.
- **RNF-06 Portabilidade de patch:** layout, filtros, limites e estratégia de normalização vêm do perfil; dados de equipe/competição vêm de catálogos.
- **RNF-07 Segurança de evidência:** dumps offline são mínimos, sanitizados, acompanhados de SHA-256 e metadados de origem/build/mods.

## Decisões fechadas para implementação

1. A extensão continua em `Overmem.Extensions.Pes2021`; não mover regras para o core.
2. O stride canônico desta família é 596 (`0x254`), mas o código lê o valor do perfil.
3. O parser não resolve nomes.
4. A chave composta é a regra; fallback simples é excepcional e auditável.
5. A extração produz `FIXTURES_ONLY`.
6. Placar é preservado como byte cru e recebe estado não validado.
7. Cache não sobrevive ao processo host nem a um restart do PES.
8. Endereços são serializados como hexadecimal, além do valor numérico interno `ulong`.
9. Confiança é derivada de razões e pontuação documentadas; não é uma opinião livre.
10. O primeiro gate live usa a baseline em [`examples/acceptance-competition-17.json`](examples/acceptance-competition-17.json).
11. `AttachmentInfo` em `Overmem.Abstractions` recebe `ProcessStartedAtUtc` no pacote P6 para ancorar com precisão a identidade da sessão no cache, mantendo fallback defensivo na extensão via `Process.GetProcessById` e amostra hash.
12. A validação de P0 a P4 apoia-se em gerador de fixtures sintéticas em memória (`SyntheticCalendarMemoryGenerator`), desacoplando o desenvolvimento inicial da captura de dumps reais (executada em P7).

## Questões que a implementação deve manter explícitas

- A hora de início do processo será adicionada em `AttachmentInfo` durante P6; caso a leitura falhe por permissão do SO, o cache usa PID + amostra hash com confiança reduzida.
- A base completa do array e o início do bloco da competição são endereços diferentes. Se o perfil não conseguir provar a base completa, a resposta deve deixar `calendarArrayBaseAddress=null` e ainda pode extrair pelo bloco confirmado.
- `competitionId=17` é baseline, não default universal.
- O significado de `teamLiga` além de compor `TeamKey` permanece não confirmado.
- O estado de jogo encerrado, especialmente `0–0`, fica para investigação separada.

