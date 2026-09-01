# Evidencia live da arena EDIT - 2026-08-31

Status: `CONFIRMED_READ_ONLY_SINGLE_SESSION`  
Contexto informado pelo operador: PES 2021 aberto, sem Master League carregada  
Processo observado: `PES2021.exe`, PID `27040`, iniciado em 2026-08-30  
Regra de seguranca: nenhuma escrita, freeze, hook, injecao ou execucao de Lua/CT

## Resultado principal

O Overmem delimitou, somente por leitura, uma arena EDIT contigua com stride `0x17C`:

| Propriedade | Resultado desta sessao |
|---|---:|
| Regiao virtual que contem a arena | `0x7FF4D8EC0000..0x7FF4DA740000` |
| Tamanho da regiao virtual | `0x1880000` (25.690.112 bytes) |
| Protecao/tipo | `ReadWrite`, `Private`, legivel |
| Inicio do primeiro registro | `0x7FF4D8EC0010` |
| Fim exclusivo da arena | `0x7FF4D999F4CC` |
| Tamanho territorial da arena | `0xADF4BC` (11.400.380 bytes) |
| Stride | `0x17C` (380 bytes) |
| Slots territoriais | 30.001 |
| Slots preenchidos | 25.005 |
| Slots vazios/reservados | 4.996 |
| Slots nao classificados dentro da arena | 0 |
| IDs crus unicos entre preenchidos | 25.005 |
| IDs crus duplicados | 0 |

Os slots `0..25004` sao 25.005 registros preenchidos consecutivos. Os slots `25005..30000` sao 4.996 registros vazios consecutivos e byte-identicos. O slot teorico `30001`, em `0x7FF4D999F4CC`, ja contem outra estrutura e encerra a arena aceita.

O SHA-256 de cada um dos 4.996 registros vazios e:

```text
C80EA0B4665A3928B96D4C7972B37DE66128B57CA0979ACCFDC1485857C16A0E
```

Isso estabelece 100% de cobertura territorial dos **slots da arena nesta sessao**, mas ainda nao estabelece:

- estabilidade dos enderecos apos restart;
- descoberta automatica implementada no Overmem;
- 100% de semantica dos 380 bytes;
- equivalencia com uma estrutura de Master League;
- autoridade de escrita de qualquer campo.

## Como a arena foi delimitada

1. Uma busca exata por ID `58120` encontrou o campo em `0x7FF4D908F240`.
2. Subtrair `playerId.offset = 0x30` produziu a base `0x7FF4D908F210`.
3. Registros vizinhos validos confirmaram o stride `0x17C`.
4. A regiao virtual contendo a ancora foi lida em bloco.
5. A ancora definiu o residuo: o primeiro registro alinhado nessa regiao fica em `regionBase + 0x10`.
6. Todos os slots desse residuo foram classificados, sem saltos.
7. A sequencia observada foi:

```text
16 bytes anteriores ao primeiro registro
25.005 x POPULATED_RECORD
4.996 x EMPTY_RESERVED_RECORD, todos byte-identicos
estrutura de outro tipo; fim exclusivo da arena
```

Para os registros preenchidos, os invariantes usados foram:

- `playerId` em `+0x30` interpretado como `u32` opaco e diferente de zero;
- altura em `+0x00` entre 120 e 220;
- peso em `+0x01` entre 30 e 160;
- nome UTF-8 plausivel na area iniciada em `+0x38`;
- mercado em `+0x174` como `i32` nao negativo;
- continuidade exata pelo stride `0x17C`.

Os limites de altura e ID nao devem ser copiados do Lua antigo. Dois jogadores reais possuem altura crua 130, e existem IDs validos com bits altos marcados.

## Classes de ID encontradas

O ID precisa ser preservado como `u32` cru. Nao se deve aplicar um limite global `< 500000` durante a cobertura territorial.

| Classe crua | Quantidade | Observacao |
|---|---:|---|
| abaixo de 300.000 | 22.334 | faixa aceita pelos dois leitores Lua |
| `300000..499999` | 1.629 | aceita apenas por parte da descoberta Lua |
| `500000..0x3FFFFFFF` | 50 | excluida pelos limites do Lua |
| bit `0x40000000` marcado | 989 | registros reais com nomes e estrutura valida |
| bit `0x80000000` marcado | 3 | registros reais no final da area preenchida |

Total sem bits altos: 24.013. Total com bits altos: 992. Total preenchido: 25.005.

Exemplos que provam que o corte por inteiro pequeno e incorreto:

| Indice | ID cru | Nome |
|---:|---:|---|
| 24013 | `0x400006E1` | Lee Dong-gook |
| 25002 | `0x80000000` | Humberto Suazo |
| 25003 | `0x80000025` | Humberto Osorio |
| 25004 | `0x8000003E` | Franz Gonzales |

Os bits altos sao estruturalmente observados; seu significado funcional continua `UNKNOWN`.

## Comparacao com o arquivo historico do Lua

Fonte: `C:\Users\Willian\Documents\My Cheat Tables\jogadores_pes2021.txt`, SHA-256 `0C771B409267009D28C6CC21C093113FB23749A97532676F07CB22EEA7047408`.

| Metrica | Quantidade |
|---|---:|
| Linhas historicas | 23.253 |
| IDs historicos unicos | 23.250 |
| IDs historicos encontrados na arena EDIT atual | 23.250 |
| IDs historicos ausentes da arena EDIT atual | 0 |
| IDs crus atuais ausentes do arquivo historico | 1.755 |

Portanto:

- o arquivo historico cobre `23.250 / 23.963 = 97,02%` dos IDs atuais abaixo de 500.000;
- cobre `23.250 / 25.005 = 92,98%` de todos os registros preenchidos quando o ID e preservado como `u32` opaco;
- as tres linhas duplicadas do historico sao duplicacao do export, pois os respectivos IDs aparecem uma vez cada na arena atual;
- os 23.250 IDs historicos formam um subconjunto exato da arena EDIT atual.

Essas porcentagens sao comparacoes desta sessao, nao garantias universais para outra versao de executavel, database ou mod.

## Fronteiras observadas

```text
first populated:  index=0     address=0x7FF4D8EC0010 id=296        name=Javier Marton
last populated:   index=25004 address=0x7FF4D97CFB60 id=0x8000003E name=Franz Gonzales
first empty:      index=25005 address=0x7FF4D97CFCDC
last empty:       index=30000 address=0x7FF4D999F350
end exclusive:    index=30001 address=0x7FF4D999F4CC classification=NOT_PLAYER_ARENA
```

## Reproducao no Overmem atual

Os comandos basicos de observacao sao:

```powershell
Set-Location -LiteralPath "D:\git-lab-pes\overmem"

dotnet run --no-build --project src\Overmem.Cli -- regions --pid 27040

dotnet run --no-build --project src\Overmem.Cli -- `
  read --pid 27040 `
  --address 0x7FF4D8EC0000 `
  --value-kind Bytes `
  --size 25690112
```

O segundo comando produz um payload hexadecimal grande. A classificacao usada no estudo foi um analisador PowerShell de evidencia, nao uma funcionalidade implementada do Overmem. O agente deve implementar leitura em blocos, classificacao e artefatos nativos; nao deve embutir PID ou enderecos acima.

Para uma reproducao aceita, o agente precisa redescobrir a ancora e os limites. Usar diretamente os enderecos desta pagina invalida o teste.

## Gates ainda pendentes

1. Repetir a descoberta apos reiniciar o jogo e provar que nenhum endereco antigo foi reutilizado.
2. Confirmar novamente a capacidade, a contagem preenchida e o bloco vazio para o mesmo database/mod.
3. Produzir manifesto de regioes, slots, rejeicoes e hashes atomicamente.
4. Completar o mapa territorial `0x000..0x17B`, preservando `UNKNOWN`.
5. Somente depois do gate EDIT, capturar uma sessao separada com ML carregada.

## Classificacao epistemica final

| Afirmacao | Status |
|---|---|
| Existe uma arena EDIT com stride `0x17C` | `CONFIRMED_READ_ONLY_SINGLE_SESSION` |
| A arena desta sessao possui 30.001 slots | `CONFIRMED_READ_ONLY_SINGLE_SESSION` |
| 25.005 slots estao preenchidos | `CONFIRMED_READ_ONLY_SINGLE_SESSION` |
| 4.996 slots vazios sao byte-identicos | `CONFIRMED_READ_ONLY_SINGLE_SESSION` |
| Todos os 23.250 IDs historicos estao presentes | `CONFIRMED_COMPARATIVE_SINGLE_SESSION` |
| IDs com bits altos sao registros estruturalmente validos | `CONFIRMED_STRUCTURAL_SINGLE_SESSION` |
| Significado dos bits `0x40000000/0x80000000` | `UNKNOWN` |
| Enderecos permanecem validos apos restart | `UNCONFIRMED_AND_MUST_NOT_BE_ASSUMED` |
| A mesma arena e a copia autoritativa da ML | `REFUTED_FOR_THIS_SESSION` |
