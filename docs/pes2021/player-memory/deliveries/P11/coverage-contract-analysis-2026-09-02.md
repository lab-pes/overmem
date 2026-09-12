# Cobertura do registro EDIT e analise contratual

Data: 2026-09-02
Fonte: dump completo da segunda sessao, sem ML
Registros preenchidos: 25.005
Seguranca: analise offline; nenhuma escrita no PES

## Resposta curta

Ainda nao sabemos tudo que existe sobre jogadores no processo.

Temos duas coberturas diferentes:

1. `CONFIRMED` fisicamente: os 380 bytes crus de cada um dos 25.005 jogadores foram lidos e preservados, junto com os 4.996 slots vazios da arena EDIT.
2. Incompleta semanticamente: o perfil atual nomeia explicitamente 226 dos 380 bytes. Apenas 67 bytes possuem status de leitura `CONFIRMED`; 153 sao `CANDIDATE`, seis sao `UNKNOWN` e 154 ainda nao estao modelados no perfil.

| Classificacao no perfil | Bytes unicos | Percentual do registro |
|---|---:|---:|
| `CONFIRMED` | 67 | 17,63% |
| `CANDIDATE` | 153 | 40,26% |
| `UNKNOWN` explicitamente representado | 6 | 1,58% |
| ainda nao modelado | 154 | 40,53% |
| bytes crus capturados | 380 | 100% |

Percentual por bytes nao equivale a percentual de funcionalidades: as tres strings fixas ocupam 183 bytes, enquanto muitos atributos usam poucos bits.

O anchor finder pesquisou aproximadamente 3,2 GB de regioes aceitas para localizar o ID de controle. Isso nao prova que toda estrutura relacionada a jogadores no processo foi descoberta. O scanner final delimitou uma arena EDIT autoritativa; caches, tabelas de inscricao, relacoes jogador-time e estruturas exclusivas de ML ainda podem existir em outras areas.

## Estabilidade apos restart

Entre os PIDs historicos 33136 e 25604:

- todos os enderecos mudaram;
- a ordem dos 25.005 IDs permaneceu identica;
- 25.005/25.005 hashes de registros completos permaneceram identicos;
- 25.005/25.005 assinaturas contratuais analisadas permaneceram identicas.

Isso confirma que os dados abaixo pertencem a base EDIT estavel desta instalacao/mod set. Nao confirma sozinho a interpretacao apresentada pela CT.

## Ocupacao dos campos contratuais

`Preenchido` abaixo significa apenas que o valor cru difere do sentinela/default e passa uma regra estrutural simples. Nao significa promocao semantica.

| Campo/padrao | Quantidade | Percentual dos 25.005 |
|---|---:|---:|
| valor de mercado cru maior que zero | 1.700 | 6,80% |
| data plausivel, ano 1900..2200 e mes/dia validos | 1.527 | 6,11% |
| valor e data simultaneamente preenchidos | 1.500 | 6,00% |
| data preenchida, valor zero | 27 | 0,11% |
| valor preenchido, data sentinela | 200 | 0,80% |
| salario anual maior que zero | 0 | 0,00% |
| ano sentinela `65535`, mes/dia zero | 23.478 | 93,89% |

Entre os 1.527 jogadores com data plausivel, 98,23% tambem possuem valor de mercado. Entre os 1.700 com valor, 88,24% possuem data plausivel.

Distribuicao por ano entre as 1.527 datas plausiveis:

| Ano | Jogadores |
|---:|---:|
| 2026 | 777 |
| 2027 | 320 |
| 2028 | 229 |
| 2029 | 132 |
| 2030 | 66 |
| 2031 | 3 |

O padrao e forte e coerente com datas contratuais, mas permanece `CANDIDATE` ate comparacao com a UI.

## Amostras para validacao no EDIT

O display de mercado abaixo aplica a transformacao candidata `raw * 100 EUR`.

| ID | Jogador | Mercado cru | Display candidato | Termino candidato |
|---:|---|---:|---:|---|
| 108959 | Declan Rice | 1.200.000 | EUR 120.000.000 | 2028-06-30 |
| 127544 | Bukayo Saka | 1.200.000 | EUR 120.000.000 | 2030-06-30 |
| 126689 | William Saliba | 900.000 | EUR 90.000.000 | 2030-06-30 |
| 33739 | Morgan Rogers | 800.000 | EUR 80.000.000 | 2031-06-30 |
| 132155 | Jurrien Timber | 700.000 | EUR 70.000.000 | 2028-06-30 |
| 114506 | Kai Havertz | 500.000 | EUR 50.000.000 | 2028-06-30 |
| 58120 | Piero Hincapie | 500.000 | EUR 50.000.000 | 2026-06-30 |
| 120246 | Vitor Roque | 380.000 | EUR 38.000.000 | 2029-12-31 |
| 59801 | Youri Tielemans | 350.000 | EUR 35.000.000 | 2028-06-30 |
| 40352 | Neymar | 100.000 | EUR 10.000.000 | 2026-12-31 |

Controle negativo importante:

| ID | Jogador | Mercado cru | Data crua |
|---:|---|---:|---|
| 111207 | Gabriel Magalhaes | 750.000 | `65535-00-00` |

Se a UI concordar com as amostras positivas, poderemos promover endereco/tipo e transformacao dos campos. Se a UI mostrar contrato para o controle negativo, a ausencia indica que a base EDIT e parcial para dados contratuais ou que a semantica do offset muda por contexto.

## Emprestimos e vinculos

A CT oferece somente estes candidatos em `+0x14A`:

- bit 1: `Is Transfer Listed`;
- bit 2: `Is Loan Listed`.

Na base EDIT atual:

- bit 1 ativo: 0 jogadores;
- bit 2 ativo: 0 jogadores;
- os valores nao zero de `+0x14A` usam principalmente bits altos ainda nao explicados.

`Is Loan Listed` significa listado para emprestimo, nao prova que o atleta esta emprestado nem identifica clube de origem, clube de destino, inicio ou termino do emprestimo.

O perfil atual nao contem `ownerClubId`, `registeredClubId`, `loanFromTeamId`, `loanToTeamId` ou equivalente. Portanto, ainda nao conseguimos reconstruir vinculos contratuais ou emprestimos apenas desta arena EDIT.

Hipotese mais provavel: vinculos jogador-time vivem numa tabela relacional separada ou na copia especifica da Master League. Isso deve ser investigado por comparacao A/B, primeiro sem ML e depois com uma ML conhecida, correlacionando jogadores emprestados conhecidos. Nao se deve reinterpretar bits altos de `+0x14A` sem essa evidencia.

## Proximos experimentos seguros

1. Willian valida na UI as amostras de mercado e termino listadas no CSV anexo.
2. Registrar pelo menos um acerto e um controle negativo.
3. Mapear tabelas jogador-time fora do registro `0x17C`, ainda sem ML.
4. Depois do gate EDIT, carregar uma ML conhecida e capturar separadamente a copia ML.
5. Escolher jogadores sabidamente emprestados e comparar todas as tabelas/bytes relacionados.
6. Promover um conceito por vez: valor, termino, salario, listado para emprestimo e vinculo efetivo sao campos distintos.

Nenhum desses passos exige escrita em memoria na fase de descoberta.
