# Estado das correções de integração

Gate aplicado à `master` consolidada em 2026-09-12. Os estados abaixo se referem ao código integrado e aos testes controlados; não promovem hipóteses sobre a memória do PES a fatos de runtime.

| Correção | Estado | Evidência |
|---|---|---|
| MCP stdio, HTTP e pipes coexistem; stdio é o padrão | `ACCEPTED` | Build, handshake real por stdio, distribuição HTTP e integração de pipes contra `Overmem.TestTarget` |
| Ferramentas de jogadores/famílias registradas no MCP | `ACCEPTED` | `tools/list` retorna a superfície esperada, sem nomes duplicados |
| Quatro comandos Family Discovery ligados ao parser/help | `ACCEPTED` | Testes de parsing e help da distribuição |
| Caminhos explícitos na exportação/comparação de catálogos | `ACCEPTED` | Round-trip em diretórios temporários e falha explícita para arquivo ausente |
| Refinamento Int32 da ponte de pipes | `ACCEPTED` | Snapshot e refinamento `Unchanged` contra valor conhecido do processo de teste |
| Aliases históricos `daily-calendar` | `ACCEPTED_WITH_LIMIT` | CLI e MCP expõem aliases; retorno usa o schema `secondary-calendar` atual |
| Distribuição única CLI/MCP e comando `serve` | `ACCEPTED` | Publicação conjunta, help e inicialização HTTP com 56 ferramentas e zero anexos a processos |
| Family Discovery como prova de identidade/semântica de jogadores | `CANDIDATE` | Implementação e contratos estão preservados, mas as heurísticas ainda exigem validação controlada no jogo |

Resultado da verificação repetida nesta etapa: compilação com zero erros/avisos, 83/83 testes do núcleo e 408/408 testes PES aprovados, total 491/491. Nenhum jogo ou Sider foi iniciado.
