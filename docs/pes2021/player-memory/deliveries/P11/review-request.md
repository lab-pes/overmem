# P11 - Gate e proximas tarefas

## Gate atual

Aceitar como `OPERATIONAL_READ_CURRENT_SESSION` para EDIT sem ML. Nao promover ainda P6 a aceito: falta uma repeticao apos restart e a correlacao visual prevista no contrato.

## Tarefas simples que ja podem ser delegadas

### P11-A - Saida compacta do scan

Objetivo: adicionar um modo de resumo/catalogo compacto sem alterar a descoberta.

Escopo:

- opcao CLI explicita `--output-mode full|compact|summary`;
- `full` preserva o contrato atual;
- `compact` inclui por jogador endereco hexadecimal, indice, ID cru, nome, hash e campos selecionados sem `rawRecord`;
- `summary` nao inclui a lista de jogadores;
- testes de serializacao e equivalencia de contagens;
- nenhuma chamada adicional ao gateway e nenhuma escrita de memoria.

Aceite: os tres modos reportam as mesmas contagens e o artefato summary fica abaixo de 100 KiB no fixture sintetico; `git diff --check` e 388+ testes verdes.

### P11-B - Otimizacao conservadora da ancora

Objetivo: reduzir os aproximadamente 3,2 GB lidos sem perder a descoberta apos restart.

Escopo:

- medir por tipo/tamanho de regiao;
- criar filtro somente se duas sessoes demonstrarem que ele nao exclui a arena;
- manter fallback full scan;
- benchmark sintetico e diagnostico de bytes/regioes;
- nao usar endereco absoluto, PID, tamanho exato da arena ou residue `+0x10` como constante.

Aceite: mesmos candidatos/score do fixture e da repeticao live, com fallback testado.

### P11-C - Fechamento das regressões P3

Objetivo: completar os casos de falha exigidos no plano original.

Escopo:

- ampliar os testes ja adicionados para hit falso deslocado e IDs `0x40000000`/`0x80000000`;
- record split em block boundary;
- empate entre duas familias explicito;
- hole, partial read e cancelamento;
- prova spy de zero `WriteAsync` em anchor, scan, query e export.

Aceite: cada caso possui teste que falha na implementacao defeituosa correspondente e passa na atual/corrigida.

### P11-D - Cobertura territorial real do registro

Objetivo: reduzir a zero os 154 bytes ainda nao modelados sem inventar semantica.

Escopo:

- importar para o perfil os atributos, posicoes, estilos e habilidades ja descritos na CT;
- classificar cada byte/bit restante como `CONFIRMED`, `CANDIDATE`, `UNKNOWN` ou reservado;
- gerar relatorio de cobertura automaticamente;
- rejeitar gaps e overlaps nao justificados;
- manter leitura separada de autorizacao de escrita.

Aceite: uniao territorial `0x000..0x17B` igual a 380/380 bytes e zero gaps, com testes por byte e bit.

### P11-E - Relacao jogador-time no contexto EDIT

Objetivo: localizar a tabela de inscricao/vinculo fora do registro de 380 bytes.

Escopo:

- somente leitura e sem ML;
- usar pelo menos cinco pares jogador-time conhecidos fornecidos/validados pelo usuario;
- procurar estrutura repetitiva e vizinhos, nao apenas ocorrencias isoladas de ID;
- distinguir clube atual, selecao, livre, sem clube e possivel clube proprietario;
- nao reutilizar endereco apos restart;
- produzir denominadores de cobertura e ambiguidades.

Aceite: a mesma relacao e redescoberta apos restart para cinco controles e nenhum ID isolado e promovido a vinculo sem evidencia estrutural.

## Tarefas que exigem Willian/Codex e nao devem ser delegadas de forma autonoma

### P6-UI - Fechamento semantico EDIT

O restart estrutural ja passou em novo PID e novos enderecos. Para concluir todo o P6, comparar cinco jogadores com a UI do EDIT, registrar o hash do executavel/mod set e promover somente os campos efetivamente confirmados.

### FIELD-READ - Promocao por campo

Executar um campo por pacote: valor de mercado, contrato, salario, forma atual etc. Cada pacote exige antes/depois controlado na UI, diff minimo de bytes, repeticao e classificacao `CONFIRMED`, `CANDIDATE`, `UNKNOWN` ou `REFUTED`. Nenhuma escrita e necessaria para a primeira promocao de leitura se a UI permitir alterar o valor por meios normais do jogo.

### M0 - Captura comparativa com ML

Somente depois de P6 aceito: capturar A sem ML e B com ML, redescobrir ambas, comparar familias/arenas e manter perfis EDIT e ML separados.

### P8/P9 - Escritas no PES

Continuam bloqueadas. Exigem autorizacao explicita, identidade composta do alvo, expected bytes, journal/rollback anterior a escrita, releitura/verificacao e um unico campo/jogador por piloto.
