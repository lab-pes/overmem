# Perfis e mapas

## Resolução de configuração

Precedência, da maior para a menor:

1. argumento explícito da chamada (`profilePath`, `competitionMapPath`, `teamMapPath`);
2. caminhos declarados no perfil selecionado;
3. configuração do host (`Pes2021:Fixtures:*`);
4. defaults neutros do Overmem.

Não procurar automaticamente em instalações GOGOSZ, WORLD, BMPES ou em outro repositório no novo extrator. Compatibilidade com o carregador legado pode permanecer nas ferramentas antigas, isolada e documentada.

Todo arquivo carregado informa caminho resolvido e SHA-256 na saída. Caminho relativo no perfil é resolvido em relação ao diretório do próprio perfil.

## Perfil de memória

Schema: `pes2021.fixture-profile.v1`. Exemplo completo em [`examples/pes2021-fixture-profile.example.json`](examples/pes2021-fixture-profile.example.json).

Campos obrigatórios:

- `schemaVersion`, `profileId`, `profileVersion`;
- `recordLayout.stride` e offsets/tipos;
- `calendar.defaultBlockRecords`, `maxBlockRecords`, `recordLimit`;
- `recordValidation` para ano, rodada e sentinelas;
- `regionFilter`;
- `anchorValidation`;
- `normalization.strategy`.

Regras:

- `profileId + profileVersion` identifica semanticamente; SHA-256 detecta alteração de conteúdo.
- offsets precisam estar dentro do stride;
- tipos v1 permitidos: `u8`, `u16le`;
- todos os cálculos usam overflow checked;
- configuração inválida falha antes de anexar/ler memória;
- valores específicos de patch vivem no perfil, não em constantes do serviço.

## Mapa de competições

CSV UTF-8 com cabeçalho:

```csv
competition_id,name
17,COMPETICAO 17 - REFERENCIA DE ACEITE
```

Regras:

- `competition_id` decimal entre `0` e `65535`, sujeito ao perfil;
- nome não vazio;
- chave duplicada com nomes normalizados iguais gera warning de duplicidade;
- chave duplicada com nomes diferentes gera conflito e não resolve nome;
- ausência não impede extração.

## Mapa de equipes

CSV UTF-8 com cabeçalho mínimo:

```csv
team_id,team_liga,name
32784,313,SANTOS
32768,482,ATHLETICO PARANAENSE
```

Colunas opcionais: `short_name`, `source`, `evidence_status`. Exemplo autocontido com as 20 equipes da baseline em [`examples/competition-17-team-map.csv`](examples/competition-17-team-map.csv).

Compatibilidade de entrada:

- `league_id` e `secondary_id` podem ser aceitos como alias de coluna para importar catálogos legados;
- ao usar alias, o diagnóstico deve registrar que o valor foi mapeado para `teamLiga` e não validado como liga semântica;
- a saída sempre usa `teamLiga`.

## Algoritmo de resolução

1. construir índice exato `TeamKey -> entradas`;
2. se a chave possui um único nome normalizado, resolver `ExactComposite`;
3. se a chave possui nomes conflitantes, retornar `Conflict`;
4. se não há chave exata, consultar todas as chaves com o mesmo `teamId`;
5. fallback só se existir exatamente uma chave e um nome não conflitante;
6. múltiplas chaves, mesmo que uma pareça popular, retornam `Ambiguous`;
7. nenhuma entrada retorna `Unresolved`.

Exemplo obrigatório de colisão: `teamId=32768` existe com diferentes `teamLiga`; portanto `32768` isolado não pode ser resolvido. `32768/482` resolve Athletico Paranaense.

## Diagnóstico dos catálogos

A resposta inclui:

- arquivos e hashes usados;
- total de linhas, chaves válidas e linhas rejeitadas;
- duplicidades equivalentes;
- conflitos por chave composta;
- IDs com colisão;
- chaves usadas nas fixtures e não resolvidas;
- contagem por `NameResolutionStatus`.

Não escrever `?` como se fosse nome. No wire contract, use `name=null` e status explícito. CSV exportado pode usar campo vazio, acompanhado por uma coluna `*_name_status`.

## Configuração do host

Forma recomendada:

```json
{
  "Pes2021": {
    "Fixtures": {
      "DefaultProfilePath": "profiles/pes2021-fixture-profile.json",
      "CompetitionMapPath": "maps/competitions.csv",
      "TeamMapPath": "maps/teams.csv"
    }
  }
}
```

Não incluir caminhos absolutos pessoais em arquivos versionados. Argumentos absolutos continuam permitidos em execução local e aparecem apenas nos metadados da sessão.

