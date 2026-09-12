# P15 — Primeira arena de jogadores da Master Liga

Data: 2026-09-03

Estado: **ARENA ML ENCONTRADA E EXTRAÍDA; semântica contratual ainda CANDIDATE**

## Descoberta das duas arenas

Com a Master Liga carregada, a busca por Piero Hincapié encontrou dois candidatos independentes com score estrutural 16/16. O Overmem recusou escolher automaticamente, como deveria.

As duas arenas foram selecionadas explicitamente e revalidadas dentro de suas próprias regiões:

| Arena | Registros | IDs únicos | Duplicatas | Classificação |
|---|---:|---:|---:|---|
| A | 25.005 | 25.005 | 0 | cadastro-base EDIT realocado |
| B | 24.325 | 24.322 | 3 | forte candidata à cópia ML |

Endereços são históricos desta sessão e não podem ser reutilizados após reinício.

## Evidência EDIT × arena A

A arena A preserva o cadastro-base completo:

- 24.998 registros exatamente iguais ao dump EDIT anterior;
- 7 jogadores com alterações em 21 offsets de atributos;
- nenhum campo já modelado foi alterado;
- mesmos 25.005 IDs e nenhuma duplicata.

Os sete deltas são compatíveis com atualização de atributos no cadastro-base, mas sua causa não foi determinada.

## Evidência EDIT × arena B

O comparador associou com segurança 24.319 jogadores:

- 24.319/24.319 registros associados possuem deltas;
- 158 dos 380 offsets mudam em pelo menos um jogador;
- 18 campos atualmente modelados apresentam diferenças;
- 683 IDs existentes no EDIT não aparecem na arena ML;
- nenhum ID aparece somente na ML;
- três IDs duplicados foram isolados, sem escolha automática.

Isso confirma estruturalmente que a arena B não é outra cópia simples do EDIT.

## Cobertura dos campos de contrato

Sobre 24.325 registros ML:

| Campo/condição | Preenchidos | Cobertura |
|---|---:|---:|
| salário anual candidato `+0x15C` positivo | 24.325 | 100,00% |
| data candidata completa | 19.517 | 80,23% |
| valor de mercado `+0x174` positivo | 1.699 | 6,98% |
| vínculo-base `+0x12C/+0x12E` não sentinela | 18.223 | 74,91% |
| vínculo de elenco `+0x164/+0x166` não sentinela | 17.795 | 73,16% |
| vínculo auxiliar `+0x160/+0x162` não sentinela | 436 | 1,79% |
| bit CT “transfer listed” | 4.072 | 16,74% |
| bit CT “loan listed” | 2.802 | 11,52% |
| ambos os bits | 2.406 | 9,89% |

O salário candidato varia de 140 a 272.032, com mediana 4.409. A unidade e periodicidade ainda precisam ser comparadas com a interface; o rótulo “Annual Salary (Euro)” vem da CT.

As datas preenchidas concentram-se em 2027–2031. Os 4.808 registros sem data usam `0xFFFF/0/0`.

## Amostras para validação visual

| Jogador | Salário raw | Término | Mercado raw | Par de elenco |
|---|---:|---|---:|---|
| Piero Hincapié | 34.526 | 2029-08-31 | 500.000 | `(16384,25)` |
| Gabriel Magalhães | 85.222 | 2027-08-31 | 750.000 | `(16384,25)` |
| William Saliba | 89.069 | 2028-08-31 | 900.000 | `(16384,25)` |
| Bukayo Saka | 112.323 | 2028-08-31 | 1.200.000 | `(16384,25)` |
| Declan Rice | 117.168 | 2028-08-31 | 1.200.000 | `(16384,25)` |
| Kai Havertz | 92.917 | 2027-08-31 | 500.000 | `(16384,25)` |

Os 30 controles completos estão em `ml-control-samples.csv`.

## Vínculos e possíveis empréstimos

Três pares de 16 bits apareceram:

- `+0x12C/+0x12E`: vínculo-base, semântica `UNKNOWN`;
- `+0x160/+0x162`: vínculo auxiliar raro, semântica `UNKNOWN`;
- `+0x164/+0x166`: forte candidato ao elenco atual da ML.

O par `+0x164/+0x166` é constante dentro de blocos contíguos que correspondem a elencos. Nos primeiros jogadores da ML, Piero, Gabriel, Raya, Saliba, Saka, Ødegaard, Rice e outros compartilham `(16384,25)`.

Em 428 registros, o vínculo-base e o vínculo de elenco divergem. Em quase todos esses casos, o vínculo auxiliar existe e repete o par de elenco. Exemplos estão em `ml-link-mismatch-samples.csv`.

Isso é uma pista forte para transferências, promoções, registros especiais ou empréstimos. Ainda não é possível declarar qual dessas relações significa “clube proprietário”, “clube atual” ou “emprestado para”. O bit chamado `loan listed` pela CT descreve listagem e não prova um contrato de empréstimo existente.

## Duplicatas preservadas

- 52992 — Elkan Baggott, índices 323 e 19.193;
- 52999 — Ebrahim Kameel, índices 18.791 e 19.200;
- 56299 — Bandar Bouresli, índices 18.792 e 19.108.

Cada instância possui hash diferente. Elas permanecem `AMBIGUOUS_DUPLICATE`.

## Segurança

- leituras de memória: somente as necessárias aos dois dumps;
- escritas de memória: zero;
- Cheat Engine/Lua: não executados;
- nenhum endereço foi promovido a constante;
- dumps brutos continuam fora do Git.
