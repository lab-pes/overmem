# Decisao de arquitetura: mapear EDIT antes de Master League

Data: 2026-08-31  
Status: decisao aceita para delegacao  
Prioridade: `EDIT_BASE` primeiro; `MASTER_LEAGUE` somente depois do gate EDIT  
Implementacao: ainda nao iniciada

## Correcao factual da sessao observada

Durante a validacao somente leitura, `PES2021.exe` estava aberto sem qualquer Master League carregada. Portanto, a familia de registros encontrada pelo Overmem nao era uma copia ML: era a base de jogadores do modo EDIT, carregada independentemente de competicao ou carreira.

O hit do player ID `58120` em `0x7FF4D908F240`, normalizado para a base `0x7FF4D908F210`, e os cinco registros consecutivos separados por `0x17C` passam a ser classificados como:

```text
contexto: EDIT_BASE
status do contexto: USER_CONFIRMED_SESSION_CONTEXT
status estrutural: CONFIRMED_READ_ONLY
```

PID e enderecos continuam sendo evidencias historicas daquela sessao e nunca podem ser reutilizados como constantes.

## Dois mapeamentos independentes

O Overmem devera modelar duas familias, sem misturar autoridade semantica:

| Mapeamento | Quando existe | Autoridade esperada | Primeira prioridade |
|---|---|---|---|
| `EDIT_BASE` | PES aberto, sem depender de ML | identidade, nomes, aparencia/base, atributos, posicoes, estilos, habilidades e valores-base presentes no registro | sim |
| `MASTER_LEAGUE` | somente com uma ML carregada | dados contextuais da carreira: salario, contrato, funcao, afeicao, listas, indisponibilidade, forma atual e outros overlays/caches | nao; trilha posterior |

A existencia de um campo fisico no registro EDIT nao prova que seu significado ML esteja ativo ali. Exemplo observado: na copia EDIT, `+0x12C/+0x12E = 0xFFFF` e `+0x15C = 0`. Esses valores sao dados validamente lidos, mas nao autorizam os nomes time, liga ou salario naquela copia.

## Objetivo da primeira implementacao

Entregar um mapeamento nativo, somente leitura e reproduzivel da arena `EDIT_BASE`, sem Cheat Engine, Lua, hook ou injecao.

O primeiro release deve conseguir:

1. localizar a arena EDIT em cada sessao;
2. determinar seus limites e segmentos;
3. classificar todos os slots no territorio encontrado;
4. extrair todos os jogadores estruturalmente validos;
5. registrar buracos, slots invalidos, duplicidades e leituras parciais;
6. cobrir territorialmente todos os bytes `0x000` a `0x17B` do registro;
7. exportar valores crus, valores de exibicao quando confirmados e status epistemico por campo;
8. repetir a descoberta apos reiniciar o jogo sem reutilizar enderecos;
9. provar zero escritas em toda a trilha EDIT de leitura.

Escrita, inclusive valor de mercado, nao faz parte do primeiro release de leitura EDIT.

## Baseline live ja observado

A evidencia detalhada esta em `edit-live-evidence-2026-08-31.md`. Nesta sessao sem ML, a arena EDIT foi delimitada em leitura:

```text
start                 = 0x7FF4D8EC0010
endExclusive          = 0x7FF4D999F4CC
stride                = 0x17C
theoreticalSlots      = 30001
populatedSlots        = 25005
emptyReservedSlots    = 4996
unaccountedSlots      = 0
uniqueRawPlayerIds    = 25005
duplicateRawPlayerIds = 0
```

Os 4.996 slots vazios sao byte-identicos. O endereco seguinte quebra o formato. Isso e baseline de uma sessao, nao constante de perfil. O agente deve redescobrir tudo apos restart.

O ID e `u32` opaco. O scanner nao pode impor os limites `< 300000` ou `< 500000` do Lua: 50 registros sem marcador estao acima de 500.000, 989 usam o bit `0x40000000` e tres usam o bit `0x80000000`. O significado dos marcadores permanece `UNKNOWN`.

## O que significa 100%

Existem quatro metricas diferentes. Nenhuma pode ser substituida por uma porcentagem vaga.

### 1. Cobertura territorial do registro

O intervalo `0x000..0x17B` deve ser particionado integralmente. Cada byte ou bit recebe uma classe:

- `CONFIRMED_FIELD`;
- `CANDIDATE_FIELD`;
- `UNKNOWN`;
- `PADDING_OR_RESERVED`;
- `SHARED_BIT_CONTAINER`.

Regras:

- primeiro byte coberto: `0x000`;
- ultimo byte coberto: `0x17B`;
- bytes nao cobertos: zero;
- overlaps injustificados: zero;
- `UNKNOWN` e um resultado valido;
- 100% territorial nao significa 100% semantico.

### 2. Cobertura da arena EDIT

Depois que a familia/arena for delimitada, todo slot pertencente ao residuo/stride identificado deve ser classificado como:

- `VALID_PLAYER`;
- `INVALID_OR_EMPTY_SLOT`;
- `HOLE`;
- `UNREADABLE`;
- `PARTIAL_READ`;
- `AMBIGUOUS_RECORD`.

Nao pode existir salto silencioso. O relatorio precisa declarar:

- endereco inicial e final de cada segmento;
- stride e residuo;
- total teorico de slots;
- total por classificacao;
- bytes/segmentos nao lidos;
- criterio usado para incluir o segmento na arena.

### 3. Cobertura de jogadores

Relatar separadamente:

- jogadores validos encontrados;
- IDs unicos;
- IDs duplicados;
- fingerprints duplicados;
- jogadores de um catalogo de referencia presentes/ausentes, se um catalogo versionado for fornecido;
- denominador exato de cada porcentagem.

O historico de 23.253 registros e referencia comparativa, nao denominador automatico.

### 4. Cobertura semantica

Percentual de bytes/bits com significado confirmado. Essa metrica pode ser baixa mesmo quando as tres coberturas anteriores sao 100%. Campos candidatos e desconhecidos nunca devem ser promovidos para melhorar artificialmente o percentual.

## Artefatos obrigatorios do mapeamento EDIT

```text
artifacts/pes2021/player-memory/edit/<run-id>/
  process-identity.json
  region-manifest.json
  edit-arena-manifest.json
  edit-slot-classification.jsonl
  edit-players.jsonl
  edit-collisions.json
  edit-rejections.jsonl
  edit-record-layout.json
  edit-coverage-report.json
  zero-write-proof.json
  manifest-sha256.csv
```

Todos os arquivos finais devem ser produzidos atomicamente. JSONL pode ser usado para os conjuntos grandes, mas o arquivo temporario so pode substituir o destino depois de flush/close bem-sucedido.

`edit-coverage-report.json` deve conter pelo menos:

- schema e versao;
- perfil ID/versao/SHA-256;
- identidade do executavel e do processo;
- metodo de descoberta;
- ancora e score;
- segmentos e stride;
- contagens de slots e jogadores;
- colisao de IDs/fingerprints;
- cobertura territorial `0x000..0x17B`;
- leituras parciais/erros;
- duracao e bytes lidos;
- contagem de escritas observadas, obrigatoriamente zero.

## Estrategia de descoberta EDIT

1. Enumerar regioes privadas, legiveis e nao executaveis.
2. Buscar um ou mais IDs de controle como `Int32` little-endian.
3. Para cada hit, subtrair `playerId.offset = 0x30` com aritmetica verificada.
4. Validar o registro por ID, altura, peso, nome, limites e hash/fingerprint.
5. Pontuar vizinhos em `base +/- n * 0x17C`.
6. Rejeitar ambiguidade quando nao houver separacao clara entre candidatos.
7. Ler regioes em blocos; nunca fazer uma chamada Win32 por endereco candidato.
8. Encontrar candidatos alinhados a 4 bytes.
9. Agrupar por residuo modulo `0x17C`, continuidade e multiplos do stride.
10. Delimitar um ou mais segmentos da arena EDIT.
11. Repercorrer cada slot teorico dos segmentos e classifica-lo explicitamente.
12. Deduplicar hits gerados por overlap entre blocos.
13. Emitir cobertura, rejeicoes e zero-write proof.

O `skipSize=10000` do Lua nao deve ser copiado: ele altera o residuo em relacao ao stride e impede uma prova territorial rigorosa.

## Identidade do jogador

O historico possui tres IDs duplicados (`52992`, `52999`, `56299`). Por isso:

- `playerId` e chave de consulta, nao identidade unica;
- a identidade de sessao inclui processo/start time, perfil, endereco, player ID e fingerprint;
- fingerprint deve combinar campos estaveis como ID, commentary ID, nome e, se necessario, bytes-base selecionados;
- uma consulta ambigua retorna todos os resultados ou erro explicito;
- nenhuma escrita futura pode escolher o primeiro resultado silenciosamente.

## Perfil EDIT e perfil ML

Nao criar um unico perfil que atribua a mesma autoridade a todos os contextos.

Arquivos planejados:

```text
files/pes2021/player-memory/pes2021-player-edit-v1.json
files/pes2021/player-memory/pes2021-player-ml-v1.json
```

`pes2021-player-edit-v1.json` e o unico perfil autorizado na primeira trilha. O perfil ML so nasce depois da captura comparativa com e sem ML.

Campos comuns podem compartilhar definicoes estruturais por composicao interna de codigo, mas os perfis devem manter independentes:

- status de evidencia;
- contextos validos;
- transformacao de exibicao;
- autoridade de leitura;
- permissao de escrita;
- versao/hash de evidencia.

## Protocolo futuro EDIT versus ML

Somente depois de aceitar o mapeamento EDIT:

1. executar captura A sem ML carregada;
2. carregar uma ML conhecida;
3. executar captura B sem reutilizar enderecos da captura A;
4. redescobrir a arena EDIT e verificar se continua presente;
5. procurar familias adicionais de registros/caches com IDs/fingerprints correspondentes;
6. unir A/B por fingerprint, preservando colisoes;
7. comparar territorialmente os 380 bytes e outras estruturas relacionadas;
8. classificar campos como comuns, EDIT-only, ML-only, overlay ou cache;
9. correlacionar salario, contrato, forma atual e demais campos com a UI da ML;
10. repetir apos restart/load da ML antes de criar `pes2021-player-ml-v1.json`.

Nao inferir que a estrutura ML tera necessariamente o mesmo stride, os mesmos limites ou um delta constante em relacao ao EDIT.

## Ordem de delegacao

O agente de implementacao deve seguir `implementation-packages.md` com esta leitura obrigatoria:

- P0-P6: exclusivamente `EDIT_BASE`, somente leitura;
- P7: transacao testada apenas no `Overmem.TestTarget`;
- P8-P9: opcionais, bloqueados por autorizacao futura e limitados ao contexto EDIT confirmado;
- M0-M4: trilha ML posterior, proibida antes da aceitacao de P6.

Delegar primeiro apenas P0. Depois da auditoria de P0, delegar P1. Nao pedir ao agente que implemente simultaneamente scanner, API e escrita.

O pedido pronto para copiar ao agente esta em `delegation-p0-edit-contracts.md`.

## Estado final desta decisao

- Contexto EDIT da leitura de 2026-08-31: confirmado pelo operador da sessao.
- Viabilidade da descoberta EDIT sem CE: confirmada em leitura.
- Baseline territorial live da arena EDIT: confirmado em uma sessao (30.001/30.001 slots classificados); automatizacao nativa ainda nao implementada.
- Mapeamento territorial completo `0x000..0x17B`: ainda nao implementado.
- Perfil ML: ainda nao existe.
- Escrita Overmem em jogadores: ainda nao implementada nem autorizada.
