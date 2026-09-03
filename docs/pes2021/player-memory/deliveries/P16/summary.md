# P16 — Auditoria independente da CT e do Lua WORLD contra EDIT e ML

Data: 2026-09-03

Decisão: **ACEITO COMO FONTE DE CANDIDATOS; REGRA GERAL DE VÍNCULO DO LUA REFUTADA**

## Resultado principal

A CT é útil e confirma o layout de `0x17C` bytes e muitos campos do jogador selecionado. Os dumps EDIT/ML também possuem exatamente `0x17C` bytes por registro. Os offsets contratuais da CT são fortemente compatíveis com dados exclusivos da ML:

| Campo | Offset | Resultado no dump ML | Estado |
|---|---:|---:|---|
| término de contrato | `+0x138/+0x13A/+0x13B` | 19.517 datas plausíveis (80,23%) | `CANDIDATE`, forte |
| salário | `+0x15C` | 24.325 valores positivos (100%) | `CANDIDATE`, forte |
| valor de mercado/base | `+0x174` | 1.699 positivos (6,98%) | offset estrutural confirmado; significado/escala `CANDIDATE` |
| relação A | `+0x12C/+0x12E` | 18.223 pares válidos | semântica `UNKNOWN` |
| relação auxiliar | `+0x160/+0x162` | 436 pares válidos | semântica variável; `UNKNOWN` |
| relação de elenco | `+0x164/+0x166` | 17.795 pares válidos | atual elenco ML `CANDIDATE`, forte |
| possível fim de empréstimo | `+0x16C/+0x16E/+0x16F` | 179 datas plausíveis | `CANDIDATE`; requer controles conhecidos/UI |

Não houve escrita na memória. A CT e o Lua não foram executados.

## Descoberta crítica sobre vínculos

O Lua WORLD fixa esta interpretação:

- `+0x12C` = time atual;
- `+0x160` = clube de origem;
- data plausível em `+0x16C` = empréstimo.

Essa regra não é geral. Entre os 436 registros nos quais o próprio Lua inferiria uma relação:

- somente **3** têm `+0x12C` igual à relação de elenco `+0x164`;
- **433** têm `+0x160` igual à relação de elenco `+0x164`.

Nos 179 registros com possível data de empréstimo, a mesma inversão aparece:

- 3 seguem a direção declarada pelo Lua;
- 176 colocam no suposto “clube de origem” exatamente o clube do elenco atual.

Portanto, usar o Lua sem correção faria o overlay/log provavelmente inverter atual/origem em 433 de 436 relações (99,31%) sob a hipótese estrutural de que `+0x164` representa o elenco atual.

Uma regra candidata melhor é:

1. usar `+0x164/+0x166` como relação do elenco atual quando válida;
2. entre `+0x12C/+0x12E` e `+0x160/+0x162`, tratar como possível contraparte a relação válida que difere de `+0x164/+0x166`;
3. promover “empréstimo” somente se a data em `+0x16C..+0x16F` for plausível e um controle conhecido/UI confirmar a direção.

Essa regra explica estruturalmente todos os 179 candidatos com data, mas ainda permanece `CANDIDATE`.

## EDIT versus ML

Em 24.319 associações únicas e seguras:

- salário mudou em 24.319/24.319;
- bloco da data contratual mudou em 19.554/24.319;
- relação `+0x12C` mudou em 18.222/24.319;
- relação `+0x164` mudou em 17.794/24.319;
- `+0x174` não mudou em nenhum jogador.

Logo, salário, contrato e relações são claramente materializados pela ML. Já `+0x174` pertence à base do jogador e é copiado sem alteração para a ML; não é evidência de um valor de mercado recalculado dinamicamente pela carreira.

O Lua multiplica salário e mercado por 100, enquanto a CT apenas os rotula como euros e lê os inteiros diretamente. A escala `×100` não é confirmada pela CT. Os valores resultantes parecem plausíveis, mas isso não substitui comparação com a interface.

## CT e padrões de descoberta

- 128 entradas da CT apontam para `ptrPlayer` com offset;
- a CT declara o fim da estrutura em `+0x17C` e o último dado em `+0x179`;
- salário, data contratual, forma atual, stamina, listagens e mercado estão presentes;
- `+0x160`, `+0x164` e `+0x16C` **não** aparecem na CT: esses significados foram adicionados pelo Lua WORLD;
- os dois AOBs da CT e o prefixo do Lua foram encontrados uma única vez no `PES2021.exe` atual;
- os endereços/RVAs escritos nos comentários da CT estão obsoletos e não devem ser reutilizados.

## Limitações restantes

- O dump contém o registro persistente de `0x17C`, não os `0x500` bytes que o Lua tenta ler do objeto selecionado.
- Nomes de equipes/ligas dos CFGs WORLD são apenas referência candidata; há proveniência incompleta e conflitos conhecidos.
- As 179 datas em 2026 são uma assinatura forte, mas não provam sozinhas um empréstimo.
- São necessários controles visualmente conhecidos para confirmar proprietário, clube atual, direção e escala monetária.

## Artefatos

- `evidence.json`: números e estados epistemológicos;
- `ct-player-fields.csv`: inventário completo das entradas `ptrPlayer` da CT;
- `lua-offset-constants.csv`: offsets declarados pelo Lua;
- `relation-patterns.csv`: padrões agregados das três relações;
- `all-aux-link-records.csv`: todos os 436 casos auxiliares;
- `candidate-loan-date-records.csv`: os 179 candidatos com data;
- `source-snapshot-sha256.csv`: hashes das nove fontes copiadas;
- `live-aob-validation-2026-09-03.json`: revalidação somente leitura dos AOBs;
- `reference/`: cópia imutável para estudo, com hashes iguais aos originais no momento da coleta.
