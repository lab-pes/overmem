# P16 — Proveniência das fontes

Data da coleta: 2026-09-03

As fontes abaixo foram copiadas byte a byte para `reference/`. A comparação SHA-256 entre origem e cópia retornou igualdade para todos os arquivos. Nenhum original foi alterado.

## Cheat Table

Origem:

`C:\Users\Willian\Documents\My Cheat Tables\work\cheat-engine\tables\PES 2021 - wILL- v0.0.1 - Copia.CT`

- bytes: 99.809;
- SHA-256: `F28D14079169457A93080DD4FE2287F1A2F91CFFA9A9F747D1062152FD0D6AD3`;
- rótulo interno: `CT VER: 21.1.0`;
- versão de formato Cheat Engine: 27.

A CT não contém o hook de jogador diretamente. Ela tenta carregar um bootstrap por um caminho antigo do OneDrive, que não existe nesta máquina, e então solicita `players\cheat-table\edit-player.lua`.

Foram localizados e copiados, somente para auditoria estática:

| Origem atual | Bytes | SHA-256 |
|---|---:|---|
| `C:\Users\Willian\Documents\My Cheat Tables\scripts\calendar\cheat-table\ml-calendar-bootstrap.lua` | 3.133 | `00C2EE30885F88A7E5D220B501DA3A082147E571E8186AC497D8E99ACD807087` |
| `C:\Users\Willian\Documents\My Cheat Tables\scripts\players\cheat-table\edit-player.lua` | 5.037 | `036A88ED8DF2F925FCBEF8B78427B681F8C4B6F79E2A1DDE711DCBECDE658D89` |

Esses arquivos não foram executados.

## Referência WORLD/GOGOSZ

Origem:

`D:\git-lab-pes\pes-sider-player-info\patches\world-gogosz\reference\recent`

| Arquivo | Bytes | SHA-256 |
|---|---:|---|
| `MANIFEST-SHA256.csv` | 918 | `FCA6AD3009A355665007563787AC22DBCDF49C283853697A0EEA3FC62BFEE79C` |
| `ml_leagues.cfg` | 3.085 | `92D03C9CA04ED0D74C71B258AC956C29A44EA38B06AA31F68E9536AA543384FB` |
| `ml_player_info.lua` | 41.331 | `2CAA910E61B7CEA961F9FF6593B4D9F0D99F908F16A5B0E8EEA0C204F6846D4A` |
| `ml_team_league.cfg` | 11.344 | `AC394D4CF06C4A0A621DAC24193AE8F1A3597380139B60DD945BBD844045BD0E` |
| `ml_teams.cfg` | 11.718 | `35C9A7A692F225B73DE19BA60BD6769692C3BF296929CD46969AD7AAFA694317` |
| `README.md` | 893 | `36BF8A00B0CED7E54484285C80A4D92EE62CA58560D2FF7E1200512653C5B353` |

As quatro linhas de dados do manifesto coincidem atualmente em tamanho e hash com as quatro fontes declaradas. Isso confirma consistência do snapshot atual; não prova a história anterior à coleta. A própria referência informa versão exata do patch não confirmada e mantém equipes/ligas como candidatos.
