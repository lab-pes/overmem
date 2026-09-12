# Glossário do Family Discovery System (FDS)

## Termos Fundamentais

### Region (Região)
Bloco contíguo de memória virtual retornado por `VirtualQueryEx`. Mapeado pelo tipo `MemoryRegionInfo` no Overmem. Cada região possui base, tamanho, estado (`MEM_COMMIT`, `MEM_RESERVE`, `MEM_FREE`), tipo (`Private`, `Mapped`, `Image`) e proteção (`PAGE_READWRITE`, `PAGE_READONLY`, etc.).

### Arena
Sequência de uma ou mais **regions** contíguas (ou próximas) que passam o filtro do perfil e contêm registros de jogadores. A arena EDIT-base é a arena principal; a arena ML é uma arena separada que pode coexistir. Duas janelas da mesma região VirtualQuery **não** formam duas arenas.

### Family (Família)
Grupo de **hits** que compartilham:
- a mesma **region** VirtualQuery (ou regiões adjacentes);
- o mesmo **stride** (tamanho do registro);
- o mesmo **resíduo** (`endereço % stride`).

Uma família corresponde a uma representação estrutural dos dados de jogadores (ex: registros completos de 380 bytes, tabela densa de IDs, escalação).

### Segment (Segmento)
Porção contígua de **slots** dentro de uma família. Uma família pode conter múltiplos segmentos separados por buracos, páginas ilegíveis ou dados não relacionados.

### Slot
Posição de tamanho `stride` bytes dentro de um segmento. Cada slot é classificado como: jogador válido, reservado vazio, registro inválido, ilegível, leitura parcial, buraco, registro ambíguo, ou limite não-jogador.

### Hit
Endereço específico na memória onde uma **âncora** (ID, nome ou fingerprint) de um jogador de controle foi encontrada. Um hit pode ser aceito (promovido a candidato) ou rejeitado (com justificativa registrada).

### Candidate (Candidato)
Um **hit** que passou nas validações iniciais (cheap validation) e foi promovido para participar da inferência de stride e agrupamento em famílias.

### Fingerprint
Representação binária de um jogador de controle usada para busca na memória. Tipos:
- **Fingerprint exato**: 380 bytes completos do registro.
- **Fingerprint mascarado**: 380 bytes com campos dinâmicos (stamina, form, condition) zerados.
- **Fingerprint de ID**: 4 bytes LE do `playerId`.
- **Fingerprint de nome**: bytes UTF-8 do `playerName`.

### Stride
Tamanho em bytes de um registro completo dentro de uma família. O stride padrão do PES 2021 EDIT é 380 bytes, mas famílias alternativas podem ter strides diferentes.

### Resíduo (Residue)
Resultado de `endereço % stride`. Todos os registros de uma mesma família devem ter o mesmo resíduo. Resíduos diferentes indicam famílias distintas mesmo com o mesmo stride.

---

## Classes de Resultado

### ExactRecordCopy
380 bytes idênticos ao registro do jogador de controle.

### MaskedRecordCopy
380 bytes que correspondem ao controle quando campos dinâmicos são mascarados.

### SameLayoutFamily
Registros com o mesmo stride e mesmos offsets de campo (playerId, playerName, height, weight), validados por múltiplos controles.

### AlternateStrideFamily
Layout diferente do padrão (stride ≠ 380), mas com IDs de jogadores reconhecidos. Inclui stride duplo (760), metade (190) ou outro valor.

### IdNameColocated
ID e nome do jogador encontrados próximos um do outro, mas em offsets diferentes do layout padrão. Não implica que o layout completo seja conhecido.

### DenseIdTable
Array contíguo de `uint32` (4 bytes LE cada) contendo IDs de jogadores conhecidos, sem payload entre eles. Validado por centenas de IDs, não apenas um.

### PointerTableCandidate
Ponteiros x64 (8 bytes, endereços canônicos) apontando para registros de jogadores conhecidos. Requer validação de alinhamento e múltiplos controles.

### IsolatedHit
Hit válido sem vizinhos reconhecíveis no mesmo stride/resíduo. Não promovido a família.

### AmbiguousFamily
Empate entre dois ou mais strides/resíduos candidatos que não pôde ser resolvido pela validação de vizinhos. Reportado explicitamente; nunca resolvido arbitrariamente.

### RefutedFalsePositive
Candidato que inicialmente pareceu válido mas foi rejeitado durante validação mais rigorosa (ex: falso candidato deslocado em +3 bytes).

---

## Políticas de Região

### DefaultPlayerArena
`MEM_COMMIT` + `Private` + Read-Write. Comportamento atual do scanner.

### IncludeMapped
Adiciona regiões `Mapped` à política default.

### IncludeReadOnly
Adiciona regiões `ReadOnly` (podem conter cópias de dados de jogadores em cache).

### IncludeExecutable
Adiciona regiões executáveis + legíveis (opt-in, alto risco de falsos positivos).

### All
Qualquer região legível, independente de tipo ou proteção. Uso exclusivo para diagnóstico.

---

## Regras Invariantes

1. **Zero escrita**: o FDS nunca chama `WriteAsync`. Qualquer chamada é bug.
2. **Todos os hits preservados**: hits rejeitados permanecem no resultado com justificativa.
3. **Empates explícitos**: nunca resolver ambiguidade pelo menor endereço.
4. **Stride mínimo**: nenhum stride é promovido com apenas 2 hits.
5. **Diagnóstico completo**: toda região, todo hit, toda rejeição é registrada.
6. **Páginas não ocultas**: uma página não lida nunca é silenciosamente ignorada.
