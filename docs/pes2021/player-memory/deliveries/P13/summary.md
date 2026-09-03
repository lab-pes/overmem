# P13 — Auditoria do scanner de relações de clubes

Data: 2026-09-02

Estado: **PARCIALMENTE CORRIGIDO; uso como atlas estrutural continua BLOQUEADO**

Escopo executado: revisão estática, correção de defeitos determinísticos e testes sintéticos. Nenhuma varredura ao vivo foi realizada.

## Correções implementadas

### F-001 — sobreposição dos blocos corrompia offsets

Estado: `CONFIRMED_FIXED`

O leitor usa sobreposição de 16 bytes entre blocos. O serviço concatenava todos os payloads integralmente, duplicando a sobreposição. A partir do segundo bloco, o índice do buffer já não correspondia ao offset real da região. Endereços reportados podiam ficar progressivamente deslocados e sequências artificiais podiam surgir nas fronteiras.

Foi criado `Pes2021RegionBlockAssembler`, que:

- posiciona cada bloco pelo seu `BlockOffset` real;
- funde bytes sobrepostos sem duplicação;
- rejeita lacunas;
- rejeita sobreposições cujos bytes discordem;
- preserva `buffer[index] == regionBase + index`.

### F-002 — `team_id` descartava a identidade composta

Estado: `CONFIRMED_FIXED`

O finder convertia o catálogo em `Dictionary<int, Row>` e guardava apenas o melhor resultado por `team_id`. Duas linhas com o mesmo `team_id` e diferentes `secondary_id` eram sobrescritas silenciosamente.

O catálogo agora mantém todas as linhas por `team_id`, e a seleção usa a chave `(team_id, secondary_id)`. O resultado é ordenado de forma determinística. Isso preserva colisões; não prova qual identidade é a correta.

## Bloqueios ainda abertos

### F-003 — endereço do nome é tratado como início do registro

Estado: `CONFIRMED_OPEN`, severidade crítica

O baseline grava `NameMatchAddress` na coluna `club_record_address`. O modo layout lê janelas a partir desse endereço. Portanto, a posição zero da janela é o começo do texto do nome, não um início de estrutura demonstrado.

Consequência: classificações de offsets produzidas pelo modo layout não podem ser promovidas como layout de clube. Antes disso é necessário inferir uma base repetível do registro e demonstrar a relação entre base, ID e nome.

### F-004 — a cobertura não é uma varredura completa da memória

Estado: `CONFIRMED_OPEN`, severidade alta

O baseline:

- aceita somente regiões `Private`, legíveis e não executáveis;
- normalmente elimina regiões acima de 32 MiB;
- aplica um teto adicional de 64 MiB;
- lê no máximo os primeiros 8 MiB de cada região aceita.

Isso é incompatível com a meta de explorar todos os “armários” relevantes. O scanner genérico do Antigravity deve resolver cobertura e orçamento; esta implementação não deve duplicar esse trabalho.

### F-005 — proximidade de ID e nome não demonstra estrutura

Estado: `CONFIRMED_OPEN`, severidade alta

O finder procura qualquer `u16` igual ao `team_id` e qualquer ocorrência UTF-8 exata do nome em uma janela de ±`0x1000`. A pontuação depende apenas da distância. Não há stride, alinhamento, terminador, cabeçalho, repetição entre registros ou validação do `secondary_id` nos bytes.

Consequência: resultados são `CANDIDATE` de proximidade, com risco de textos de interface, tabelas de recursos e números incidentais.

### F-006 — ambiguidades de endereço ainda são ocultadas

Estado: `CONFIRMED_OPEN`, severidade média

Para cada identidade composta ainda é mantido apenas o candidato de maior pontuação. Empates preservam silenciosamente o primeiro. O contrato de saída não informa quantidade de candidatos equivalentes nem margem entre primeiro e segundo lugar.

### F-007 — leitura de CSV do modo layout não respeita aspas

Estado: `CONFIRMED_OPEN`, severidade média

O writer escapa vírgulas e aspas, mas `LoadObservations` usa `Split(',')`. Uma nota ou nome com vírgula desloca colunas na releitura.

### F-008 — anchors verificam apenas `team_id`

Estado: `CONFIRMED_OPEN`, severidade baixa

`BuildControlCaseMap` testa se algum registro possui o `team_id` do anchor, mas não exige o `secondary_id` correspondente antes de adicionar a chave composta.

## Gate

O scanner pode continuar existindo como sonda exploratória `CANDIDATE`, mas:

- seus relatórios antigos de endereço após o primeiro bloco devem ser considerados inválidos;
- o modo layout não pode sustentar offsets de clube enquanto F-003 estiver aberto;
- “anchor encontrado” não equivale a registro de clube confirmado;
- nenhuma escrita pode usar seus endereços.

## Próximo passo independente recomendado

Implementar P14, um comparador offline EDIT × ML testável com fixtures sintéticas. Ele não depende de encontrar clubes nem de uma ML real agora. Quando houver um dump ML, produzirá imediatamente deltas por jogador e por offset.
