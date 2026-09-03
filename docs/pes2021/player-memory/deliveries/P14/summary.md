# P14 — Comparador offline EDIT × ML

Data: 2026-09-03

Estado: **IMPLEMENTADO E VALIDADO; aguarda apenas um dump ML real**

## O que ficou pronto

`compare_edit_ml.py` compara dois dumps operacionais sem abrir nem anexar ao PES. A associação usa:

1. `playerId` como `u32` opaco;
2. fingerprint normalizado do nome como trava de identidade;
3. SHA-256 e comprimento de 380 bytes para validar cada registro antes da comparação.

Se o mesmo ID aparecer com outro fingerprint, o registro recebe `FINGERPRINT_MISMATCH` e seus bytes não são comparados automaticamente.

## Saídas

- `player-diff-summary.csv`: estado e quantidade de bytes/campos alterados por jogador;
- `player-field-diffs.csv`: valores EDIT e ML dos campos modelados que mudaram;
- `offset-diff-summary.csv`: para cada um dos 380 offsets, frequência e transições de bytes;
- `field-diff-summary.csv`: frequência e transições por campo modelado;
- `comparison-evidence.json`: proveniência, política de identidade e contagens.

O comparador não chama um delta de “contrato”, “clube” ou “empréstimo” por conta própria. Ele preserva o status semântico que veio do dump e relata a mudança observada.

## Validação sintética

A fixture cobre:

- jogador idêntico;
- jogador com bytes e valor de mercado alterados;
- jogador existente apenas no EDIT;
- jogador existente apenas na ML;
- ID com bit alto;
- mesmo ID com fingerprint incompatível;
- rejeição de IDs duplicados.

## Validação em escala real

Os dois dumps EDIT obtidos antes e depois do reinício foram usados como entrada do comparador:

- EDIT: 25.005 jogadores;
- segunda captura: 25.005 jogadores;
- associações seguras: 25.005;
- registros exatamente iguais: 25.005;
- registros alterados: 0;
- fingerprints incompatíveis: 0;
- offsets alterados: 0.

Isso valida o pipeline em escala real e reafirma a estabilidade da arena EDIT. Não é uma validação ML, pois ambas as entradas são EDIT.

## O que dependerá do usuário

Somente a captura de um dump com uma Master Liga carregada. Depois disso, o comparador já está operacional e não requer comparação manual.

## Limites

- IDs duplicados em qualquer entrada são rejeitados, não escolhidos arbitrariamente.
- Mudança de nome com o mesmo ID exige revisão humana.
- Campos não modelados aparecem no relatório por offset, mas permanecem semanticamente desconhecidos.
- O comparador é somente leitura de arquivos e não contém escrita em memória.
