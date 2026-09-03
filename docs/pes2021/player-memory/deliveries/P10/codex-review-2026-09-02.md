# Revisao Codex do repasse M3 - 2026-09-02

Veredito: `FAIL`
Escopo: afirmacoes live, descoberta de stride, duas arenas e cobertura de jogadores
Seguranca: auditoria e revalidacao somente leitura; zero escritas no PES

## Conclusao

As principais conclusoes live do repasse M3 estao refutadas.

- O stride real continua `0x17C` (380 bytes).
- Existem 25.005 registros preenchidos no EDIT sem ML carregada, nao 61.
- As chamadas arenas A e B sao duas janelas da mesma regiao de memoria, nao duas arenas.
- O suposto stride `0x46C94` e um artefato matematico de subamostragem.
- Dos 61 resultados publicados, 60 pertencem a grade `0x17C` e um e falso positivo iniciado tres bytes dentro de um registro verdadeiro.
- A alegacao de padding de Season Update nao possui evidencia e contradiz os bytes observados.

O estudo EDIT anterior foi revalidado na sessao atual do PES, PID 33136, iniciado em 2026-09-02 12:57:10 local.

## Achados

### [P0] `0x46C94` nao e stride de registro

O comando experimental recebe `--stride 763` e executa `addr += stride`. Ele le 380 bytes em cada endereco, mas avanca 763 **bytes**, conforme `Pes2021CliExtension.cs` linhas 648-651 e 783-789. Nao existe no codigo um avanco de `380 * 763` nem uma verificacao de 762 bytes de padding.

Como `gcd(763, 380) = 1`, a varredura percorre todos os residuos modulo 380. Ela so volta a coincidir com a grade de jogadores depois de 380 passos:

```text
380 * 763 bytes = 289940 bytes = 763 * 380 bytes
```

Cada acerto alinhado seguinte fica 763 **registros reais** adiante. `0x46C94` e a distancia entre amostras, nao o tamanho de um registro.

Isso tambem explica a quantidade reportada:

```text
25005 / 763 = 32,77
```

A janela A encontrou 33 registros: exatamente o esperado ao selecionar aproximadamente um em cada 763 jogadores.

### [P0] As arenas A e B sao a mesma regiao

O manifesto de regioes atual mostra:

```text
VirtualQuery region start = 0x7FF4D9E60000
VirtualQuery region end   = 0x7FF4DB6E0000
region size               = 0x1880000
type/protection           = Private / ReadWrite
```

Os tres pontos abaixo pertencem a essa mesma regiao:

```text
arena A alegada = 0x7FF4D9E60000
arena B alegada = 0x7FF4D9F50000
ancora Piero    = 0x7FF4DA02F210
```

B e apenas um segundo ponto de partida dentro da arena A. A alternancia A/B no CSV e a intercalacao de duas subamostras da mesma matriz.

### [P0] Um dos 61 resultados e falso positivo deslocado

O resultado publicado:

```text
address = 0x7FF4DA751EE7
id      = 727872
name    = as Al-buraikan
```

esta tres bytes depois da base correta:

```text
address = 0x7FF4DA751EE4
id      = 0x4001FAFF
name    = Firas Al-buraikan
height  = 181
weight  = 75
```

A heuristica aceitou bytes internos do registro. Dos 61 resultados, 60 estao no residuo correto e um tem residuo `+3`.

### [P0] O filtro exclui registros reais conhecidos

Os comandos experimentais rejeitam `height < 140 || height > 220` e `playerId < 1 || playerId > 2_000_000` em `Pes2021CliExtension.cs` linhas 670/673 e 804/807. Esses limites contradizem evidencia aceita:

- existem jogadores reais com altura crua 130;
- 50 IDs sem marcador estao acima de 500.000, alguns acima de 2.000.000;
- 989 IDs usam `0x40000000`;
- tres IDs usam `0x80000000`.

O ID deve permanecer `u32` opaco nao zero. Nome plausivel sozinho nao corrige residuo errado, como prova o falso positivo.

### [P0] Os 382 testes nao cobrem os comandos live

`dotnet test Overmem.slnx --no-build` foi reproduzido:

```text
Overmem.Extensions.Pes2021.Tests: 320 passed
Overmem.Tests:                     62 passed
Total:                            382 passed
```

Nao ha teste que mencione `Pes2021StrideScanPlayersCliCommand`, `Pes2021ScanAllArenasCliCommand`, `pes2021-stride-scan-players`, `pes2021-scan-all-arenas` ou 763. Os testes verdes nao validam a logica do relatorio live.

### [P0] A descoberta nativa P3 tambem falhou nesta sessao

O comando `pes2021-find-player-anchor --pid 33136 --control-player-id 58120` leu 3.219.954.254 bytes, mas retornou:

```text
recordsDecoded  = 0
recordsAccepted = 0
anchorAddress   = null
confidence      = low / no_winner
```

Na mesma sessao, a leitura direta confirmou Piero em `0x7FF4DA02F210`. Logo P3/P5 nao podem ser considerados live-validados apesar de compilarem e passarem testes sinteticos.

## Revalidacao territorial independente

Processo: `PES2021.exe`, PID 33136, sem Master League carregada.

```text
regionStart       = 0x7FF4D9E60000
regionSize        = 0x1880000
firstRecord       = 0x7FF4D9E60010
recordStride      = 0x17C
arenaEndExclusive = 0x7FF4DA93F4CC
```

| Classe | Quantidade |
|---|---:|
| Registros preenchidos consecutivos | 25.005 |
| IDs crus unicos | 25.005 |
| Slots vazios/reservados consecutivos | 4.996 |
| Hashes distintos entre slots vazios | 1 |
| Slots territoriais aceitos | 30.001 |
| IDs historicos unicos encontrados | 23.250/23.250 |

SHA-256 de cada slot vazio:

```text
C80EA0B4665A3928B96D4C7972B37DE66128B57CA0979ACCFDC1485857C16A0E
```

| Classe de ID | Quantidade |
|---|---:|
| abaixo de 300.000 | 22.334 |
| 300.000..499.999 | 1.629 |
| sem marcador, acima de 500.000 | 50 |
| bit `0x40000000` | 989 |
| bit `0x80000000` | 3 |

```text
first populated: index 0     address 0x7FF4D9E60010 id 296        Javier Marton
last populated:  index 25004 address 0x7FF4DA76FB60 id 0x8000003E Franz Gonzales
first empty:     index 25005 address 0x7FF4DA76FCDC
last empty:      index 30000 address 0x7FF4DA93F350
end exclusive:   index 30001 address 0x7FF4DA93F4CC
```

## Prova local do stride por vizinhanca

| Address | ID | Nome |
|---|---:|---|
| `0x7FF4DA02EF18` | 58118 | Luis Segovia |
| `0x7FF4DA02F094` | 58119 | Anthony Landazuri |
| `0x7FF4DA02F210` | 58120 | Piero Hincapie |
| `0x7FF4DA02F38C` | 58121 | Jhon Sanchez |
| `0x7FF4DA02F508` | 58122 | Jonathan Bauman |

Cada diferenca e exatamente `0x17C`. Nao existem 762 bytes de padding entre eles.

## Status corrigido dos gates

| Componente | Status depois da auditoria |
|---|---|
| Layout/parser de 380 bytes | estruturalmente corroborado live |
| P3 anchor finder | `FAIL_LIVE` |
| comandos experimentais stride/all-arenas | `FAIL` |
| alegacao de stride `0x46C94` | `REFUTED` |
| alegacao de duas arenas | `REFUTED` |
| alegacao de somente 61 jogadores fora da ML | `REFUTED` |
| baseline EDIT 25.005 + 4.996 | `CONFIRMED_READ_ONLY_SECOND_SESSION` |
| P6 | nao aceito; precisa ser refeito com scanner corrigido |
| P8 | continua nao autorizado |

Os pacotes P0-P10 autoidentificados como “accepted” pelo executor nao recebem aceite retroativo do Codex. Esta revisao avaliou a trilha live; uma auditoria completa dos demais pacotes continua separada.

## Proximas correcoes obrigatorias

1. Remover `763` como default de `pes2021-scan-all-arenas`.
2. Tratar `stride` inequivocamente como bytes e usar o stride do perfil: 380.
3. Corrigir P3 para localizar o ID, normalizar `hit - 0x30` e validar vizinhos `+/- n * 0x17C`.
4. Descobrir o residuo da arena a partir da ancora; nesta sessao e `regionBase + 0x10`.
5. Ler regioes em blocos e classificar todos os slots, sem uma chamada por endereco.
6. Preservar IDs como `u32` opacos, inclusive bits altos.
7. Exigir alinhamento/residuo e vizinhanca para impedir candidatos iniciados dentro de um registro.
8. Tratar regioes VirtualQuery como regioes; nao chamar pontos de partida arbitrarios de novas arenas.
9. Adicionar testes para a subamostragem 763, o falso positivo `+3`, IDs marcados e as 30.001 classificacoes.
10. Reexecutar P6 sem ML apos restart, sem reutilizar estes enderecos.

Nenhuma variante de perfil `live-v1` com stride `0x46C94` deve ser criada.
