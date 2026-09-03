# P11 - Recuperacao operacional da leitura EDIT

Data: 2026-09-02
Executor/revisor: Codex
Seguranca: somente leitura no PES; nenhuma escrita live

## Resultado

O caminho nativo do Overmem foi corrigido e validado em duas execucoes do PES sem Master League. A descoberta, o scan completo e a consulta pontual funcionam pela CLI publica, inclusive depois de restart e mudanca de PID/enderecos.

O resultado live foi 25.005 jogadores consecutivos e 4.996 slots vazios/reservados, totalizando 30.001 slots em stride 380. Isso refuta os 61 jogadores e o stride `0x46C94` reportados no P10 do M3.

## Correcoes

- anchor finder pesquisa o ID byte a byte, calcula `hit - playerIdOffset` e valida vizinhos;
- empates de melhor score passam a ser ambiguos;
- scanner seleciona a unica regiao que contem a ancora;
- scanner deriva o residuo da grade a partir da ancora, inclusive `regionBase + 0x10`;
- scanner retorna apenas o run preenchido que contem a ancora e contabiliza a cauda vazia byte-identica;
- resultado ganhou `arenaCoverage`;
- consulta CLI agora faz descoberta/scan antes de consultar o catalogo;
- comandos experimentais `pes2021-stride-scan-players` e `pes2021-scan-all-arenas` foram removidos da superficie;
- a opcao inoperante `--max-records` foi removida do comando de scan;
- regressoes provam residuo `+0x10`, cauda reservada, rejeicao do falso positivo deslocado, IDs com bits altos e zero escrita.

## Validacao

```text
Overmem.Tests:                     62 passed
Overmem.Extensions.Pes2021.Tests: 326 passed
Total:                            388 passed
Warnings:                         0
```

Live:

```text
anchorAddress      = 0x7FF4DA02F210 (historico desta sessao)
anchorFingerprint  = Piero Hincapie
anchorScore        = 16/16, high, nao ambiguo
regionBase         = 0x7FF4D9E60000 (historico)
firstRecord        = 0x7FF4D9E60010 (historico)
arenaEndExclusive  = 0x7FF4DA93F4CC (historico)
recordStride       = 380
populatedSlots     = 25005
emptyReservedSlots = 4996
theoreticalSlots   = 30001
duplicatePlayerIds = 0
```

Os enderecos acima sao evidencia historica e nao podem ser reutilizados.

## Artefatos

- `docs/pes2021/player-memory/edit-operational-read-runbook.md`: operacao e criterios;
- `docs/pes2021/player-memory/deliveries/P10/codex-review-2026-09-02.md`: refutacao detalhada;
- `docs/pes2021/player-memory/deliveries/P11/restart-validation-2026-09-02.md`: comparacao das duas sessoes;
- `docs/pes2021/player-memory/deliveries/P11/coverage-contract-analysis-2026-09-02.md`: limites de cobertura e estatisticas contratuais;
- `docs/pes2021/player-memory/deliveries/P11/contract-validation-samples-2026-09-02.csv`: amostras para conferencia na UI;
- `files/pes2021/player-memory/codex-live-anchor-operational.json`: evidencia live de ancora;
- `files/pes2021/player-memory/codex-live-edit-operational.json`: dump live completo local, cerca de 190 MB;
- `evidence.json`: resumo pequeno e delegavel.

## Estado dos gates

- P3 descoberta EDIT: `OPERATIONAL_RESTART_VALIDATED`;
- P4 catalogo/query: `OPERATIONAL_RESTART_VALIDATED`;
- P5 CLI read: `OPERATIONAL_RESTART_VALIDATED`;
- P6 estrutural/restart: `PASS`;
- P6 semantico: `PARTIAL_PASS`; falta correlacionar cinco jogadores com a UI conforme contrato;
- P7: existente apenas para TestTarget; nenhuma ampliacao;
- P8/P9: continuam sem autorizacao live;
- M0-M4: continuam adiados ate P6 aceito.

## Limitacoes e rollback

O dump completo e grande e nao deve ser adicionado ao Git sem decisao explicita. Para rollback de codigo, reverta apenas os arquivos listados no diff desta entrega; nao use reset global porque o worktree contem alteracoes anteriores de outros agentes.
