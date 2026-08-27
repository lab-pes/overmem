# Pacotes de implementação

Os pacotes são pequenos, revisáveis e bloqueados pelos gates de [verification.md](verification.md). Cada entrega deve listar arquivos alterados, testes, evidência, riscos, pendências e pedido de revisão. Não iniciar um pacote dependente antes do gate anterior.

## P0 — Congelar contratos e fixtures de teste

**Objetivo:** materializar tipos, schemas e infraestrutura de fixtures sem mudar comportamento público.

**Arquivos:** namespace novo `Fixtures/`, testes de contrato, gerador sintético de memória, carregador de perfil e exemplos.

**Trabalho:**

- criar value objects/enums/records de [contracts.md](contracts.md);
- implementar validação e hash do perfil;
- implementar `SyntheticCalendarMemoryGenerator` para produzir buffers em memória de registros de 596 bytes (datas válidas, IDs normais, IDs > 5000, sentinelas `0xFFFF` e múltiplas competições), permitindo testar P0–P4 de forma automatizada sem depender de dump live prévio;
- preparar estrutura de dumps offline e manifest para posterior captura em P7;
- garantir serialização `camelCase` e enum wire em `SCREAMING_SNAKE_CASE` onde especificado.

**Aceite:** testes de contrato e fixtures sintéticas passam; nenhuma ferramenta nova exposta; nenhum termo chama `teamLiga` de competição.

## P1 — Parser puro e correção de IDs

**Dependência:** P0.

**Trabalho:**

- extrair `Pes2021CalendarRecordParser` de `TryReadRecordAsync`;
- validar `DateOnly` real;
- aceitar todo `u16` de equipe exceto `0xFFFF`;
- adaptar `Pes2021CalendarRecordSnapshot` ao resultado novo;
- cobrir offsets, endianness, scores e IDs altos via fixtures sintéticas.

**Aceite:** G1; comportamento legado permanece, exceto correção intencional de IDs altos.

## P2 — Leitor e enumerador em blocos

**Dependência:** P1.

**Trabalho:**

- implementar block reader com fronteiras, overflow e métricas;
- migrar `DumpDateAsync` e `CalendarSummaryAsync`;
- transformar `CompareDatesAsync` em passagem única/snapshot;
- manter opção interna temporária de caminho legado para benchmark A/B.

**Aceite:** G2; igualdade de resultados; chamadas reduzidas de O(registros) para O(blocos).

## P3 — Descoberta de âncora e normalização

**Dependência:** P2.

**Trabalho:**

- scanner por regiões privadas/graváveis;
- busca por competição/equipe em chunks com overlap;
- score, razões, sequência no stride e detecção de ambiguidade;
- distinguir base do bloco e base completa;
- estratégias de normalização do perfil;
- remover default oculto 29 do fluxo novo e ajustar legado;
- expor service method, MCP, CLI e ajuda.

**Aceite:** G3, incluindo reinício do PES sem endereço anterior.

## P4 — Extrator nativo

**Dependência:** P3.

**Trabalho:**

- implementar orquestração endereço/cache/descoberta;
- filtrar competição, produzir `Fixture`, ordenar e calcular contagens;
- expor MCP e CLI;
- adicionar opção `--output-file <caminho>` na CLI com escrita atômica (gravação em `.tmp` seguida de substituição/rename atômico) para consumo seguro por processos externos;
- códigos de erro estáveis;
- testes de parsing de argumentos, smoke MCP e exemplo de saída.

**Aceite:** resultado sem nomes já substitui a parte de leitura/parsing do script auxiliar.

## P5 — Catálogos e resolução de nomes

**Dependência:** P1; integra após P4.

**Trabalho:**

- loaders CSV/configuração e hashes;
- índices compostos, fallback único e conflitos;
- mapa de competição;
- anexar diagnóstico e não ocultar unresolved;
- remover dependência implícita de caminhos WORLD/GOGOSZ no extrator novo.

**Aceite:** G4; `32768/482` e `32784/313` resolvem; `32768` isolado é ambíguo.

## P6 — Cache de sessão e diagnóstico completo

**Dependência:** P3 e P4.

**Trabalho:**

- enriquecer `AttachmentInfo` em `Overmem.Abstractions` com `DateTimeOffset? ProcessStartedAtUtc` (preenchido no gateway Windows no attach) com fallback defensivo via `Process.GetProcessById`;
- chave de cache por instância/perfil `(attachmentId, processId, processStartedAtUtc?, profileId, profileVersion, profileSha256)`;
- hashes de amostra e revalidação;
- invalidação em detach/restart/falha;
- métricas por etapa, regiões e motivos;
- testes concorrentes e de cache stale.

**Aceite:** G5; usuário distingue endereço fornecido, reuse, descoberta, redescoberta e recusa.

## P7 — Offline, live, benchmark e segurança

**Dependência:** P2–P6.

**Trabalho:**

- capturar dumps sanitizados reais do processo `PES2021.exe`;
- executar baseline 17 e segunda competição;
- executar restart A/B;
- benchmark legado/512/1024;
- provar zero escrita;
- armazenar hashes e relatório epistemológico.

**Aceite:** G6 e G7 completos. Falha de qualquer contagem ou write gate devolve o pacote para correção.

## P8 — Documentação operacional e handoff Sider

**Dependência:** P7.

**Trabalho:**

- atualizar README principal, status e tool surface;
- documentar comandos reais, opções, `--output-file` atômico e exemplos gerados;
- documentar criação/seleção de perfil e mapas;
- documentar restart, cache e troubleshooting;
- descrever integração segura com módulo Lua/Sider via leitura do arquivo JSON atômico;
- remover menções de plano que contradigam o código entregue.

**Aceite:** um usuário novo executa a extração usando somente este repositório e arquivos do próprio patch.

## Procedimento operacional para ambiente de desenvolvimento (Windows)

- **Bloqueio de DLLs pelo MCP Server (`MSB3026`):** Quando o `Overmem.McpServer` estiver em execução (ex.: como servidor de ferramentas ativo na IDE ou terminal), compilações globais da solution via `dotnet build` podem falhar por bloqueio das DLLs compartilhadas.
- **Comandos recomendados para o ciclo rápido de testes:**
  ```powershell
  # Executar testes da extensão sem recompilar projetos bloqueados:
  dotnet test tests/Overmem.Extensions.Pes2021.Tests/Overmem.Extensions.Pes2021.Tests.csproj --no-build

  # Ou compilar apenas a extensão PES 2021 sem rebuild do servidor MCP:
  dotnet build tests/Overmem.Extensions.Pes2021.Tests/Overmem.Extensions.Pes2021.Tests.csproj
  ```
- Para rebuild completo da solution (`dotnet build Overmem.slnx`), deve-se encerrar temporariamente as instâncias ativas do MCP Server.

## Ordem e paralelismo permitido

```text
P0 -> P1 -> P2 -> P3 -> P4 -> P6 -> P7 -> P8
            \             /
             ----> P5 ----
```

P5 pode começar após P1, mas sua integração pública depende de P4. P6 não deve ser implementado antes de a semântica de base de P3 estar estável.

## Checklist de revisão por pacote

- escopo respeitado e nenhum arquivo alheio alterado;
- implementação, teste e documentação concordam;
- nenhuma evidência histórica apresentada como live atual;
- `dotnet build`, `dotnet test` e `git diff --check`;
- novos caminhos sem endereço absoluto pessoal;
- métricas e hashes anexados quando aplicável;
- riscos/pendências explícitos;
- rollback possível por pacote;
- gate seguinte autorizado apenas após aceite.

## Rastreabilidade com as oito melhorias originais

| Melhoria | Pacotes |
|---|---|
| contratos/modelo | P0–P1 |
| leitura em blocos | P2 |
| âncora/base após reinício | P3 |
| extrator nativo | P4 |
| nomes/mapas do patch | P5 |
| cache/diagnóstico | P6 |
| testes/evidência | P7 |
| documentação/Lua futuro | P8 |

