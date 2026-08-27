# Extração de partidas por competição no PES 2021

Este diretório é a fonte de verdade autocontida para implementar a extração rápida e reproduzível da agenda da Master League por competição. Nenhum documento, script ou dado de outro repositório é necessário para executar o plano.

## Estado

- **IMPLEMENTADO:** o Overmem já anexa ao processo, lista regiões, lê memória, faz pattern scan e expõe a extensão PES 2021 por CLI e MCP. `Pes2021AgendaService` já decodifica registros de `0x254` bytes.
- **IMPLEMENTADO COM LIMITAÇÕES:** `DumpDateAsync`, `CompareDatesAsync` e `CalendarSummaryAsync` leem um registro por chamada; `FindBaseAsync` usa `competitionCode=29` como padrão; o cache é indexado apenas por `AttachmentId`; `IsStrongRecord` limita IDs de equipe a `5000`.
- **BASELINE IMPORTADO:** uma captura externa de 27/08/2026 produziu 380 partidas, 20 chaves compostas de equipe e 38 partidas do Santos para `competitionId=17`. Essa baseline orienta o aceite, mas precisa ser reproduzida no Overmem antes de virar evidência live deste repositório.
- **PLANEJADO:** os contratos e pacotes descritos neste diretório. Nada aqui deve ser anunciado como implementado antes dos respectivos gates.

## Resultado esperado

O Overmem deverá:

1. localizar um registro-âncora usando competição e equipe, sem endereço absoluto persistente;
2. restringir a busca a regiões privadas, committed, legíveis e graváveis;
3. descobrir e revalidar a base do calendário após reinício do PES;
4. ler centenas de registros por chamada e decodificá-los com um parser único;
5. extrair todas as partidas de uma competição por CLI e MCP;
6. resolver nomes por `(teamId, teamLiga)`, sem esconder colisões;
7. informar cache, regiões, bytes, chamadas, tempos, descartes e confiança;
8. operar sem chamada de escrita na memória do processo;
9. publicar somente `FIXTURES_ONLY`, sem derivar classificação.

## Escopo e não escopo

Incluído:

- calendário principal da Master League;
- registros de 596 bytes no stride `0x254`;
- descoberta, leitura em blocos, parsing, filtro por competição e resolução de nomes;
- CLI, MCP, configuração por perfil, cache em memória e diagnóstico;
- testes unitários, offline e live read-only;
- saída JSON apropriada para consumo futuro por um módulo Sider.

Fora deste incremento:

- escrita, freeze, patch ou injeção no PES;
- classificação, pontos, saldo, campeão ou rebaixamento;
- inferência de partida encerrada a partir de placar cru;
- leitura de memória pelo Lua;
- persistência de endereços absolutos entre processos;
- catálogo universal embutido para qualquer patch.

## Invariantes

- `competitionId`, `teamLiga` e o antigo rótulo `secondary_id` são conceitos diferentes.
- A identidade de uma equipe é `TeamKey(teamId, teamLiga)`.
- Todo ID de equipe `u16` é admissível, exceto `0xFFFF`; não existe teto `5000`.
- Endereço absoluto é dado de uma instância do processo, nunca configuração durável.
- Um cache só é reutilizado depois de revalidação.
- Nome ausente ou ambíguo permanece visível na saída.
- `0–0` cru não prova jogo futuro nem jogo encerrado.
- O caminho de extração não chama `WriteAsync` nem qualquer ferramenta de escrita.

## Documentos normativos

Leia nesta ordem:

1. [Requisitos e decisões](requirements-and-decisions.md)
2. [Contratos de domínio e saída](contracts.md)
3. [Arquitetura, leitura e descoberta](architecture-and-memory.md)
4. [Perfis e mapas](configuration-and-maps.md)
5. [Contratos CLI e MCP](api.md)
6. [Testes, benchmark e evidência](verification.md)
7. [Pacotes de implementação](implementation-plan.md)

Os exemplos versionados ficam em [`examples/`](examples/). Em caso de conflito, prevalece: decisão explícita do usuário, estes requisitos, contratos, testes aceitos e, por último, implementação existente.

## Definição global de pronto

O incremento só está pronto quando todos estes pontos forem verdadeiros:

- o comando nativo substitui funcionalmente o script auxiliar externo;
- a baseline `competitionId=17` é reproduzida com 380 partidas, 20 equipes e 38 partidas do Santos;
- `32784/313` resolve Santos e `32768/482` resolve Athletico Paranaense;
- nenhuma colisão é resolvida silenciosamente;
- uma segunda competição, fora da referência brasileira, passa no teste live;
- a descoberta funciona após encerramento e novo início do `PES2021.exe`;
- a evidência mostra zero operações de escrita;
- o benchmark apresenta chamadas, bytes e duração do método antigo e do novo;
- testes automatizados, build e `git diff --check` passam;
- README e ajuda da CLI/MCP refletem a superfície efetivamente entregue.

