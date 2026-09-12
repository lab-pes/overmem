# P13 — Gate de revisão

## Aceitar

- F-001 e F-002 como defeitos corrigidos e cobertos por regressão.
- Os relatórios históricos do scanner que dependam de blocos posteriores ao primeiro devem ser regenerados no futuro.
- O finder continua produzindo apenas candidatos de proximidade.

## Não aceitar

- `NameMatchAddress` como início confirmado de um registro de clube.
- O relatório layout atual como prova de offsets.
- A cobertura atual como varredura total ou quase total da memória.
- Endereços do scanner como alvo de escrita.

## Dependências para reabrir o gate de layout

1. Preservar todos os candidatos e medir ambiguidade.
2. Inferir estrutura repetida, alinhamento ou stride em torno de múltiplos clubes.
3. Demonstrar onde fica a base do registro em relação ao ID e ao nome.
4. Repetir em outro processo, com endereços redescobertos.
5. Correlacionar ao menos três identidades compostas com controles independentes.

## Coordenação

Não ampliar agora a enumeração de regiões neste scanner. Essa responsabilidade pertence ao scanner genérico em desenvolvimento pelo Antigravity. Não consumir nem reinterpretar resultados do M3-X1 até que sejam entregues e auditados.
