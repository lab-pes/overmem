# Checkpoint e revezamento: Codex <-> Antigravity

> **ATUALIZACAO OPERACIONAL 2026-09-02:** depois da refutacao do repasse M3, o Codex corrigiu P3/P5. Anchor, scan completo e query funcionaram ao vivo no PES sem ML: stride `0x17C`, 25.005 jogadores, 4.996 slots reservados, 30.001 totais e zero IDs duplicados. Uma segunda execucao em novo PID e novos enderecos repetiu exatamente a estrutura e os hashes de cinco registros de controle. Os comandos experimentais defeituosos foram removidos. Estado: `OPERATIONAL_READ_RESTART_VALIDATED`; falta apenas o subgate semantico de correlacao com a UI. Autoridade atual: `deliveries/P11/summary.md`, `deliveries/P11/restart-validation-2026-09-02.md` e `edit-operational-read-runbook.md`.

> **LIMITE DE COBERTURA:** 100% dos bytes crus foram capturados, mas o perfil nomeia 226/380 bytes; 154 continuam sem modelagem. Nao afirmar conhecimento semantico total do jogador. Contratos/mercado aparecem preenchidos apenas numa fracao da base EDIT e vinculos de emprestimo ainda nao foram resolvidos. Ver `deliveries/P11/coverage-contract-analysis-2026-09-02.md`.

Data do checkpoint: 2026-08-31  
Repositorio: `D:\git-lab-pes\overmem`  
Objetivo ativo: implementar primeiro o mapeamento de jogadores `EDIT_BASE`; Master League permanece separada e adiada  
Estado do worktree: alterado e sem commit; preservar todos os arquivos existentes

## Regra de autoridade

Este documento e o ponto de retomada quando os creditos de um agente estiverem indisponiveis.

- Willian define prioridade, autoriza leitura live e autoriza qualquer futura escrita.
- Antigravity implementa somente o pacote explicitamente autorizado, produz evidencias e para.
- Codex audita o pacote, reproduz testes, aceita ou rejeita e libera o proximo pacote.
- Um pacote produzido nao e automaticamente aceito.
- Nenhum agente inicia o pacote seguinte sem gate registrado.

## Estado confirmado

### Estudo e evidencia EDIT

Concluido e documentado:

- estrutura de jogador com stride `0x17C`;
- `playerId` em `+0x30` como `u32` opaco;
- nome na area iniciada em `+0x38`;
- valor de mercado cru em `+0x174`;
- sessao observada sem Master League carregada;
- arena EDIT delimitada em leitura numa unica sessao;
- nenhuma escrita, freeze, hook, injecao ou execucao de Lua/CT.

Baseline live da sessao:

```text
arenaStart             = 0x7FF4D8EC0010
arenaEndExclusive      = 0x7FF4D999F4CC
stride                 = 0x17C
theoreticalSlots       = 30001
populatedSlots         = 25005
emptyReservedSlots     = 4996
unaccountedSlots       = 0
uniqueRawPlayerIds     = 25005
duplicateRawPlayerIds  = 0
```

PID e enderecos sao historicos e nao podem virar constantes. A descoberta precisa ser repetida apos restart.

Distribuicao observada dos IDs:

```text
below 300000           = 22334
300000..499999         = 1629
500000..0x3FFFFFFF     = 50
0x40000000 marked      = 989
0x80000000 marked      = 3
total                  = 25005
```

O significado dos bits altos permanece `UNKNOWN`. Eles nao sao motivo de rejeicao estrutural.

### Comparacao com o Lua historico

```text
historical rows                  = 23253
historical unique IDs            = 23250
historical IDs present live      = 23250
historical IDs absent live       = 0
live raw IDs absent historically = 1755
```

Cobertura do arquivo historico:

- 97,02% dos 23.963 IDs atuais abaixo de 500.000;
- 92,98% de todos os 25.005 registros preenchidos.

## Estado dos pacotes

| Pacote | Estado | Evidencia | Gate |
|---|---|---|---|
| Estudo/ADR | concluido | documentos e evidencia live | aceito como base de trabalho |
| P0 contratos/proveniencia | produzido; revisao pendente | docs, exemplos JSON, manifest e testes | nao aceito ainda |
| P1 perfil territorial | produzido parcialmente; bloqueado | perfil, loader, defaults e testes | rejeitar ate corrigir validadores |
| P2 parser puro | nao iniciado | nenhum parser encontrado | bloqueado por P0/P1 |
| P3 descoberta EDIT | nao iniciado | nenhuma implementacao nativa | bloqueado por P2 |
| P4 catalogo/export | nao iniciado | - | bloqueado por P3 |
| P5 CLI/MCP leitura | nao iniciado | - | bloqueado por P4 |
| P6 validacao live/restart | nao iniciado | baseline de uma sessao apenas | bloqueado por P5 |
| P7 transacao TestTarget | nao iniciado | - | bloqueado por P2 e autorizacao do gate |
| P8-P10 escritas/hardening | nao iniciado | - | sem autorizacao |
| M0-M4 Master League | nao iniciado | - | proibido antes da aceitacao de P6 |

## Checkpoint tecnico reproduzido

Em 2026-08-31, com os arquivos presentes no worktree:

```text
dotnet build tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj
PASS - 0 warnings, 0 errors

dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build
PASS - 198 passed, 0 failed, 0 skipped

git diff --check
PASS - apenas aviso esperado de conversao LF/CRLF no README
```

Passar testes nao supera um erro de contrato. O P1 atual compila, mas seus limites contradizem a evidencia live.

## Bloqueios concretos encontrados

### P0 precisa de revisao/correcao

O exemplo `wire-examples/scan.json` ainda usa o historico de 23.253 como se fosse a arena de exemplo e relata tres IDs duplicados. Depois da delimitacao live, o contrato precisa demonstrar separadamente:

- 30.001 slots territoriais;
- 25.005 preenchidos;
- 4.996 vazios/reservados;
- zero slots nao contabilizados;
- 25.005 IDs crus unicos nesta sessao;
- historico de 23.253 linhas somente como comparacao externa.

Tambem falta o fixture obrigatorio capaz de serializar sem truncamento:

```text
rawPlayerId = 0x8000003E
playerName  = Franz Gonzales
idFlags     = UNKNOWN
```

O contrato nao deve representar `idFlags` como semantica conhecida.

### P1 esta bloqueado por validadores incorretos

O perfil atual em `files/pes2021/player-memory/pes2021-player-record-v1.json` declara:

```text
minimumHeight   = 140
maximumPlayerId = 200000
```

Isso rejeitaria dados reais observados:

- Davor Zdravkovski e Victor Stina possuem altura crua 130;
- 1.629 jogadores possuem IDs entre 300.000 e 499.999;
- 50 IDs sem marcador estao acima de 500.000;
- 989 IDs possuem `0x40000000`;
- tres IDs possuem `0x80000000`.

Correcao obrigatoria:

- preservar `playerId` como `u32` nao zero;
- nao usar um teto numerico pequeno como criterio de rejeicao;
- se o schema exigir limite superior, usar `uint.MaxValue` e documentar que os bits sao opacos;
- usar altura estrutural conservadora que aceite pelo menos 120..220;
- pontuar registros por multiplos invariantes e vizinhos, nao por ID isolado;
- adicionar testes para todas as classes de ID acima;
- alinhar ou justificar o nome do arquivo: o plano pede `pes2021-player-edit-v1.json`, mas o arquivo atual se chama `pes2021-player-record-v1.json`.

## Proximo pacote autorizado ao Antigravity

Identificador sugerido: `R0-P0P1-LIVE-ALIGNMENT`.

Escopo unico:

1. corrigir os contratos P0 para refletirem a diferenca entre arena live e export historico;
2. adicionar fixture/teste do ID `0x8000003E` sem truncamento;
3. corrigir os validadores P1 para `u32` opaco e altura observada;
4. resolver ou documentar o nome canonico do perfil EDIT;
5. atualizar testes e produzir uma entrega de correcao;
6. parar e pedir auditoria do Codex.

Fora do escopo de `R0`:

- parser P2;
- acesso a `IProcessMemoryGateway`;
- attach ou nova leitura do PES;
- scanner P3;
- CLI/MCP;
- escrita, freeze, hook ou injecao;
- qualquer perfil ou semantica Master League.

Artefatos obrigatorios:

```text
docs/pes2021/player-memory/deliveries/R0-P0P1-LIVE-ALIGNMENT/
  summary.md
  commands.md
  test-results.txt
  evidence.json
  review-request.md
```

Testes obrigatorios:

```powershell
Set-Location -LiteralPath "D:\git-lab-pes\overmem"

dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build
git diff --check
git status --short
```

O agente nao deve apagar, resetar ou sobrescrever trabalho preexistente. O repositorio esta sujo e sem commit.

## Prompt pronto para o Antigravity

```text
Trabalhe em D:\git-lab-pes\overmem.

Leia integralmente:
- docs/pes2021/player-memory/handoff-codex-antigravity.md
- docs/pes2021/player-memory/edit-live-evidence-2026-08-31.md
- docs/pes2021/player-memory/edit-first-decision.md
- docs/pes2021/player-memory/implementation-packages.md
- docs/pes2021/player-memory/delegation-p0-edit-contracts.md

Execute somente o pacote R0-P0P1-LIVE-ALIGNMENT descrito no handoff.
Preserve o worktree existente. Nao use git reset/checkout para descartar arquivos.
Nao leia nem escreva memoria do PES. Nao implemente P2 ou P3. Nao implemente ML.
Trate playerId como u32 opaco nao zero; aceite os marcadores 0x40000000 e 0x80000000 sem inventar seu significado.
Ao terminar, produza todos os artefatos da entrega R0, execute os testes obrigatorios, pare e solicite auditoria do Codex.
```

## Prompt pronto para o Codex apos R0

```text
Audite somente a entrega R0-P0P1-LIVE-ALIGNMENT em D:\git-lab-pes\overmem.

Leia docs/pes2021/player-memory/handoff-codex-antigravity.md e a entrega R0.
Nao implemente o pacote seguinte durante a auditoria.
Verifique o diff real, validadores u32, limites de altura, fixture 0x8000003E, separacao entre baseline live e historico, nome do perfil, ausencia de acesso a memoria e testes completos.
Registre PASS, PASS_WITH_NOTES ou FAIL com evidencias e somente libere P2 se todos os bloqueios forem resolvidos.
```

## Sequencia depois da aceitacao de R0

### P2 - parser puro

- parser de exatamente 380 bytes;
- `u32` opaco para ID;
- strings limitadas;
- valores crus e status epistemico;
- codec de bitfields sobre copia;
- zero acesso ao processo;
- fixtures sinteticos e registros live recortados/documentados.

### P3 - descoberta EDIT somente leitura

- encontrar ancora por ID mais vizinhos;
- ler regioes privadas em blocos;
- agrupar por residuo `mod 0x17C`;
- delimitar preenchidos, vazios/reservados e fim da arena;
- contabilizar todos os slots;
- cache ligado a process start/profile hash;
- zero chamadas de escrita.

### P4-P5 - catalogo e superficies de leitura

- identidade de sessao, colisoes e fingerprints;
- export atomico;
- CLI e MCP somente leitura;
- nenhum `playerId` resolve ambiguidades silenciosamente.

### P6 - gate live EDIT

- Run A sem ML;
- restart completo do jogo;
- Run B sem ML, sem reutilizar enderecos;
- comparar arena, slots, IDs/fingerprints e campos;
- registrar divergencias por database/mod;
- somente P6 aceito libera discussao ML ou escrita real.

### P7 e posteriores

- P7 testa transacao exclusivamente no `Overmem.TestTarget`;
- P8/P9 exigem autorizacao explicita futura de Willian;
- valor de mercado e o primeiro candidato de escrita;
- salario, contrato e forma atual dependem do mapeamento ML;
- M0-M4 somente depois do gate P6.

## Protocolo de troca entre agentes

Ao encerrar cada turno de implementacao, o agente executor deve deixar:

1. pacote executado e escopo;
2. arquivos alterados;
3. comandos e saidas completas;
4. afirmacoes ligadas a evidencias;
5. limitacoes e pendencias;
6. rollback;
7. pergunta objetiva de revisao;
8. condicao de parada respeitada.

Ao encerrar cada auditoria, o Codex deve deixar:

1. veredito `PASS`, `PASS_WITH_NOTES` ou `FAIL`;
2. achados por severidade e arquivo/linha;
3. testes reproduzidos;
4. gate seguinte autorizado ou bloqueado;
5. texto pronto para o proximo agente.

## Arquivos de referencia obrigatoria

- `docs/pes2021/player-memory/edit-live-evidence-2026-08-31.md`
- `docs/pes2021/player-memory/feasibility-study.md`
- `docs/pes2021/player-memory/edit-first-decision.md`
- `docs/pes2021/player-memory/implementation-packages.md`
- `docs/pes2021/player-memory/delegation-p0-edit-contracts.md`
- `docs/pes2021/player-memory/wire-contracts.md`
- `docs/pes2021/player-memory/error-codes.md`

## Proibicoes que permanecem

- nao reutilizar PID/endereco entre sessoes;
- nao tratar o historico de 23.253 linhas como capacidade da arena;
- nao rejeitar IDs por terem bits altos;
- nao promover `+0x12C`, `+0x12E` ou `+0x15C` a time/liga/salario no EDIT;
- nao iniciar ML antes de P6;
- nao escrever memoria real sem autorizacao futura expressa;
- nao apagar o worktree sujo;
- nao confundir teste verde com aceite de contrato.
