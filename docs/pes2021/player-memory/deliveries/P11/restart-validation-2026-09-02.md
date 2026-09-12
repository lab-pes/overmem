# P11 - Validacao apos restart do PES

Data: 2026-09-02
Contexto declarado: EDIT, sem Master League carregada
Seguranca: somente leitura; zero escrita, Lua, Cheat Engine, hook ou injecao

## Veredito

`PASS_STRUCTURAL_RESTART`

O PES foi reiniciado pelo usuario. O processo mudou de PID 33136 para 25604 e toda a arena mudou de endereco. O Overmem redescobriu a ancora e a arena sem reutilizar PID ou endereco historico.

## Comparacao

| Medida | Sessao 1 | Sessao 2 | Resultado |
|---|---:|---:|---|
| PID historico | 33136 | 25604 | mudou |
| Regiao historica | `0x7FF4D9E60000` | `0x7FF4D9950000` | mudou |
| Primeiro registro | `0x7FF4D9E60010` | `0x7FF4D9950010` | mudou; residuo `+0x10` preservado |
| Ancora 58120 | `0x7FF4DA02F210` | `0x7FF4D9B1F210` | mudou; redescoberta |
| Final exclusivo | `0x7FF4DA93F4CC` | `0x7FF4DA42F4CC` | mudou |
| Stride | 380 | 380 | estavel |
| Preenchidos | 25.005 | 25.005 | estavel |
| Reservados vazios | 4.996 | 4.996 | estavel |
| Slots totais | 30.001 | 30.001 | estavel |
| Nao contabilizados | 0 | 0 | estavel |
| IDs duplicados | 0 | 0 | estavel |
| Hash do slot vazio | `c80ea0...16a0e` | `c80ea0...16a0e` | identico |

## Ancora

```text
playerId       = 58120
fingerprint    = Piero Hincapie
candidateCount = 1
ambiguous      = false
confidence     = high
score          = 16/16
recordIndex    = 4992
```

## Estabilidade dos cinco controles

| ID | Nome | SHA-256 do registro nas duas sessoes |
|---:|---|---|
| 58118 | Luis Segovia | `2a685496304e9eba5747648605a89d6ceb3c650e8aa0d421cfb6449a089dacca` |
| 58119 | Anthony Landazuri | `613f0f33f16f86460a1a6bd3f20391cd38d983f57e00d0117239bc13c4d328eb` |
| 58120 | Piero Hincapie | `a088bb710f316d7d3bedfc48f228763cc6613285624a22bc512a41354d52af38` |
| 58121 | Jhon Sanchez | `5a42d829f2245b20882b3186393dc85c611378cf4a9d84348efe4ef122ef3901` |
| 58122 | Jonathan Bauman | `65a350c04e8fc30b2e709cf94ba2163a95e0b109346ae7a44a74783faf668df6` |

Isso confirma estabilidade estrutural e byte a byte desses controles. Nao promove automaticamente a semantica de campos ainda marcados como `CANDIDATE` ou `UNKNOWN`.

## Artefatos da segunda sessao

```text
files/pes2021/player-memory/codex-live-anchor-restart-2026-09-02.json
SHA-256 C50E6BC4F4CCF09AAB69BC51EAD38087851761A75FCF9310FE3698BB1C6ED316

files/pes2021/player-memory/codex-live-edit-restart-2026-09-02.json
SHA-256 6D7E922C28D3120BCAB2B819783A716596E5C71D10B18826CF9CE85D6A5FBEA4
```

O dump completo possui aproximadamente 190 MB e nao deve ser adicionado ao Git sem decisao explicita.

## Gate remanescente

O restart estrutural esta concluido. Para promover o P6 inteiro a `PASS`, resta correlacionar visualmente cinco jogadores/campos com a UI do EDIT e registrar o executavel/mod set. Valor de mercado, salario, forma atual, contrato e demais semanticas mantem seus status atuais ate experimento especifico.
