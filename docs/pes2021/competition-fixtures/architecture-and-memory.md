# Arquitetura, leitura e descoberta

## Diagnóstico do código atual

| Área | Estado atual | Mudança necessária |
|---|---|---|
| parser | embutido em `TryReadRecordAsync` | extrair parser puro e testável |
| leitura | uma chamada de 596 bytes por registro | blocos de 512/1024 registros |
| resumo | percorre 13.014 registros | usar enumerador em blocos |
| comparação | chama dois dumps completos | uma passagem/snapshot |
| descoberta | pattern scan em toda memória legível e default 29 | scanner regional privado/gravável e entrada explícita |
| normalização | retrocede no máximo 512 registros plausíveis | estratégia configurável e distinção array/bloco |
| validação forte | rejeita equipe acima de 5000 | `u16`, exceto `0xFFFF` |
| cache | `ConcurrentDictionary<AttachmentId,...>` | chave por instância/perfil + amostra |
| nomes | lazy map implícito de caminhos instalados | catálogos explícitos e diagnóstico |
| diagnóstico | resultado parcial de candidatos | métricas completas por etapa |

## Componentes-alvo

Tudo específico permanece em `src/Overmem.Extensions.Pes2021`.

```text
CLI / MCP
   -> Pes2021CompetitionFixtureService
      -> Pes2021FixtureAnchorFinder
      -> Pes2021CalendarBlockReader
         -> ProcessMemoryApplicationService (somente ListRegions/Read)
      -> Pes2021CalendarRecordParser
      -> Pes2021FixtureCatalogLoader / Resolver
      -> Pes2021CalendarSessionCache
      -> Pes2021ExtractionDiagnosticsCollector
```

Responsabilidades:

- **Service:** valida entrada, resolve perfil/catálogos, decide endereço fornecido/cache/descoberta, extrai e monta saída.
- **AnchorFinder:** enumera regiões, localiza hits, valida sequências e normaliza bases.
- **BlockReader:** faz aritmética segura, respeita regiões e contabiliza I/O.
- **Parser:** bytes para `RawCalendarRecord`; zero I/O.
- **Resolver:** enriquece participantes e expõe ausência/conflito.
- **Cache:** guarda sessão somente em memória e revalida antes de servir.
- **Diagnostics:** não lê memória; agrega contadores, tempos, regiões e descartes.

O serviço legado pode delegar ao leitor/parser novo. Não duplicar decoder.

## Layout confirmado para o perfil de referência

Registro de 596 bytes, little-endian:

| Offset | Tamanho | Campo | Tratamento |
|---:|---:|---|---|
| `0x00` | 2 | `competitionId` | `u16` |
| `0x02` | 1 | rodada crua | `byte` |
| `0x03` | 1 | desconhecido | ignorar/preservar apenas em dump bruto |
| `0x04` | 2 | ano | `u16` |
| `0x06` | 1 | mês | `byte` |
| `0x07` | 1 | dia | `byte` |
| `0x10` | 2 | mandante `teamId` | `u16`, `0xFFFF` inválido |
| `0x12` | 2 | mandante `teamLiga` | `u16` |
| `0x14` | 2 | visitante `teamId` | `u16`, `0xFFFF` inválido |
| `0x16` | 2 | visitante `teamLiga` | `u16` |
| `0x18` | 1 | placar mandante cru | não inferir finalização |
| `0x1B` | 1 | placar visitante cru | não inferir finalização |

O restante do registro é `UNKNOWN` para este incremento. O parser não deve atribuir significado a bytes não investigados.

## Leitura em blocos

Assinatura recomendada:

```csharp
Task<CalendarRecordBlock> ReadCalendarRecordsBlockAsync(
    AttachmentId attachmentId,
    ulong baseAddress,
    int startRecordIndex,
    int recordCount,
    Pes2021FixtureProfile profile,
    CancellationToken cancellationToken);
```

Algoritmo:

1. validar `startRecordIndex >= 0`, `recordCount > 0` e limites do perfil;
2. calcular com `checked` `offset = startRecordIndex * stride` e `size = recordCount * stride`;
3. localizar a região que contém o início;
4. limitar a leitura ao fim dessa região e a `int.MaxValue`;
5. chamar `ReadAsync(..., MemoryValueKind.Bytes, size)` uma vez por segmento;
6. exigir múltiplo completo do stride; registrar cauda parcial como `partial_read`;
7. iterar `ReadOnlySpan<byte>` em fatias sem cópia desnecessária;
8. retornar registros, intervalo efetivo e métricas.

O enumerador superior repete blocos até `recordLimit`, fim da região ou política de parada. Default: 1024; fallback operacional: 512. O tamanho do bloco é otimização, não parte do conteúdo lógico.

`CompareDatesAsync` deve solicitar um único snapshot/enumerador e agrupar as duas datas. Ler duas vezes só é permitido quando explicitamente pedido para comparação temporal e deve aparecer no diagnóstico.

## Descoberta regional da âncora

### 1. Seleção de regiões

Aplicar comparação case-insensitive nos textos retornados por `MemoryRegionInfo`:

- committed;
- private;
- legível;
- gravável;
- não executável;
- tamanho mínimo para a sequência exigida;
- interseção com limites opcionais do pedido/perfil.

Não chamar o `scan_pattern` genérico para esta busca, pois ele percorre toda memória legível. O finder lê somente as regiões aprovadas em chunks, com sobreposição de `stride - 1` bytes para não perder registro na fronteira.

### 2. Detecção de hits

- procurar os dois bytes little-endian de `competitionId` como possível início de registro;
- antes de promover, decodificar o registro completo;
- conferir a equipe em `home` ou `away`;
- quando `teamLiga` foi fornecido, exigir o par exato;
- deduplicar endereços encontrados na sobreposição de chunks.

### 3. Validação por stride

Para cada hit, ler uma janela para trás e para frente e pontuar:

- +3: registro âncora casa exatamente com competição/equipe;
- +2: `teamLiga` explícito casa;
- +1 por registro plausível na sequência, limitado pelo perfil;
- +2: ao menos `minimumCompetitionRun` registros da competição no stride;
- +2: datas reais e não regressão impossível dentro do bloco;
- +2: normalização do início do bloco não ambígua;
- +2: base completa confirmada pela estratégia do perfil;
- -3 por leitura parcial ou cruzamento de região;
- -5 por candidato concorrente com a mesma pontuação sem desempate estrutural.

O perfil define limiares `medium` e `high`. A resposta sempre inclui razões e pontuação máxima possível.

### 4. Normalização sem ambiguidade

Primeiro, recuar pelo stride enquanto os registros pertencem à competição e permanecem plausíveis. O primeiro registro desse run é `CompetitionBlockBaseAddress`.

Depois aplicar a estratégia do perfil para a base completa:

- `competition-block-only`: não afirma a base completa; retorna `null`.
- `known-season-start-index`: subtrai o índice configurado do início do bloco e valida amostras do array.
- `scan-array-boundary`: procura a fronteira por janelas e regras do perfil; só aceita uma candidata.

Para a baseline atual, `known-season-start-index=12288` é uma observação importada e configurável, nunca uma constante no serviço. Se a validação falhar, a extração ainda pode operar a partir do bloco da competição, dentro do limite confirmado.

## Extração

Ordem de resolução da origem:

1. `calendarBaseAddress`/`competitionBlockBaseAddress` fornecido: validar antes de usar;
2. sessão em cache: validar identidade e amostra;
3. `anchorAddress` fornecido: validar e normalizar;
4. descoberta por competição/equipe.

A extração lê o intervalo confirmado, filtra exatamente o `competitionId`, rejeita sentinelas e datas inválidas, enriquece nomes e ordena deterministicamente.

Políticas de parada do bloco de competição:

- depois de `minimumCompetitionRun`, encerrar após `maxConsecutiveNonCompetitionRecords` configurado;
- nunca ultrapassar região ou `recordLimit`;
- se usar base completa, percorrer o limite do perfil e filtrar, para compatibilidade com calendários não contíguos;
- informar a política usada na sessão.

## Cache e revalidação

Chave recomendada:

```text
(attachmentId, processId, processStartedAtUtc?, profileId, profileVersion, profileSha256)
```

A entrada guarda duas ou mais amostras de bytes: início do bloco confirmado e uma posição posterior. Antes de reutilizar:

1. confirmar que attachment e PID ainda estão ativos;
2. confirmar início do processo, quando disponível;
3. confirmar perfil/hash;
4. reler amostras e comparar hash;
5. decodificar novamente âncora e sequência mínima.

Falha remove a entrada e aciona redescoberta. Endereço fornecido inválido é recusado, não cai silenciosamente em cache. O resultado diferencia `provided`, `reused`, `discovered`, `rediscovered` e `refused`.

## Alterações de arquivos previstas

Novos arquivos sugeridos:

```text
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureModels.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarRecordParser.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarBlockReader.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureAnchorFinder.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureCatalogLoader.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CalendarSessionCache.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021CompetitionFixtureService.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureProfile.cs
src/Overmem.Extensions.Pes2021/Fixtures/Pes2021FixtureProfileLoader.cs
```

Arquivos existentes a alterar:

- `Pes2021AgendaService.cs`: delegar leitura/parsing e corrigir validação de IDs;
- `Pes2021AgendaModels.cs`: adaptadores/deprecações, sem novo decoder;
- `Tools/Pes2021AgendaTools.cs`: duas ferramentas novas;
- `Cli/Pes2021CliCommands.cs` e `Pes2021CliExtension.cs`: comandos/opções/ajuda e opção `--output-file` atômica;
- `Pes2021Extension.cs`: registrar serviços singleton/scoped adequados;
- `AttachmentInfo.cs` e gateway Windows: adicionar `DateTimeOffset? ProcessStartedAtUtc` para reforçar a chave imutável de cache;
- testes da extensão: `SyntheticCalendarMemoryGenerator` para testes unitários em memória de P0 a P4 e testes de CLI/MCP.

