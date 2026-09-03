# P12 — Atlas offline dos registros EDIT

Data: 2026-09-02

Contexto: `EDIT_NO_ML`

Estado: **CONCLUÍDO — análise offline; nenhuma leitura ou escrita adicional no PES**

## Resultado principal

O registro EDIT de jogador tem 380 bytes e agora possui um censo reproduzível de cada byte sobre os 25.005 jogadores. Os dois dumps obtidos em processos diferentes contêm os mesmos 25.005 IDs e os mesmos 25.005 hashes de registros, sem uma única divergência.

Isso confirma a estabilidade estrutural do conjunto EDIT após reinício. Não confirma automaticamente o significado de cada campo.

## As 380 “gavetas”

| Estado | Bytes | Significado |
|---|---:|---|
| `PROFILE_CONFIRMED` | 67 | Campo já confirmado no perfil de leitura |
| `PROFILE_CANDIDATE` | 153 | Campo modelado, mas sem confirmação semântica suficiente |
| `LABELLED_UNKNOWN` | 44 | A CT dá um rótulo estrutural, mas o perfil ainda não o modela ou confirma |
| `UNLABELLED` | 116 | Nenhum rótulo no perfil nem na CT |
| **Total** | **380** | Todos os offsets foram medidos estatisticamente |

Dos 380 bytes, 142 variam entre jogadores e 238 são constantes nesta captura EDIT. Entre os constantes, 188 são `0x00`, 48 são `0xFF`, um é `0x64` e um é `0x12`. Constância no EDIT não significa que o byte seja inútil: alguns podem ser campos reservados ou preenchidos somente quando uma Master Liga é carregada.

## Inventário da CT

A CT contém 128 entradas com `Address=ptrPlayer` e offset. Uma é o marcador de limite `start+0x17C`, portanto existem 127 campos ou bitfields alegados pela CT em 66 offsets únicos.

- 123 possuem rótulo semântico e foram preservados como alegações `CANDIDATE` da CT.
- 4 permanecem `UNKNOWN`: `Team (?)`, `League (?)` e dois campos chamados apenas `?`.
- 106 das 127 entradas variam no conjunto EDIT; 21 são constantes.
- O perfil operacional atual corresponde diretamente a 26 entradas: 4 `CONFIRMED` e 22 `CANDIDATE`.
- As 101 entradas restantes não foram promovidas ao perfil de runtime. Elas estão catalogadas para validação futura.

Os atributos básicos fornecem evidência estrutural forte. Exemplos: `Age` apresenta 37 valores, `Form` 8 valores, habilidades binárias apresentam 2 valores e atributos numéricos se concentram em faixas compatíveis com PES. Ainda assim, os nomes da CT são referência histórica, não prova semântica ao vivo.

## EDIT versus campos de contrato/ML

No contexto sem ML:

- `+0x12C Team (?)` é `0xFFFF` em 25.005/25.005 jogadores;
- `+0x12E League (?)` é `0xFFFF` em 25.005/25.005 jogadores;
- salário anual, afeto, lista de transferência, lista de empréstimo, função no time e indisponibilidade são constantes em valores neutros;
- barra de stamina é sempre 100 e seta de forma é sempre 2;
- valor de mercado e data de término possuem subconjuntos preenchidos, já detalhados em P11.

Interpretação: a arena EDIT contém o cadastro-base completo, mas vários campos de estado contratual/dinâmico parecem reservados ou neutralizados fora de uma ML. O resultado não prova ainda onde ficam clube atual, salário efetivo, vínculos e empréstimos de uma ML.

## Corpus dourado

`golden-player-corpus.csv` contém 30 controles determinísticos, incluindo:

- âncora operacional Piero Hincapié;
- amostras com e sem valor/data de contrato;
- extremos de altura, peso e nacionalidade observados;
- nomes não ASCII;
- IDs com bits altos;
- assinaturas estruturais diversas.

Uma correção importante foi registrada: Firas Al-buraikan é `0x4001FABF` (1073871551); `0x4001FAFF` (1073871615) pertence a Lee Si-heon. O corpus mantém ambos para impedir a repetição dessa associação incorreta.

## Artefatos

- `analyze_edit_corpus.py`: gerador offline reproduzível;
- `record-byte-census.csv`: uma linha para cada offset de `0x000` a `0x17B`;
- `ct-dump-matrix.csv`: CT × perfil × estatística observada;
- `ct-candidate-field-catalog.json`: catálogo estático de todas as entradas `ptrPlayer`;
- `golden-player-corpus.csv`: controles para comparações EDIT/ML futuras;
- `evidence.json`: proveniência, contagens e verificação entre reinícios.

## Limites

- Nenhum rótulo novo foi promovido a `CONFIRMED`.
- Nenhum offset foi habilitado para escrita.
- O corpus não substitui correlação controlada com a interface do jogo.
- O endereço absoluto dos registros não deve ser reutilizado após reinício.
