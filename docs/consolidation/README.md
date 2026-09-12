# Consolidação Overmem — lab-pes

Repositório canônico: https://github.com/lab-pes/overmem. Marca: **lab-pes**; produto e namespaces: **Overmem**. A licença MIT existente já atribui o projeto a lab-pes e foi preservada.

Data do inventário: 2026-09-12. Branch de integração: `codex/unify-lab-pes-overmem`. Base remota: `master`, commit `5f00ca3a1ebc185ef998be6db338f5bcb428134d`.

## O que foi reunido

| Origem | Contribuição | Tratamento |
|---|---|---|
| `overmem`, branch `codex/player-edit-operational` | Descoberta EDIT, catálogo, comparador EDIT/ML, atlas e evidências P11–P16 | Integrada, com histórico Git |
| `overmem-melhorias`, branch `feature/2026-09-02-melhoria` | Family Discovery System, scanners, catálogos e transporte HTTP | Integrada, com histórico Git e correções da interface |
| Worktree `opencode/sunny-mountain` | Busca Int32 delimitada e ponte por named pipes | Integrada, com histórico Git e teste de snapshot/refinamento |
| `m3-overmem`, branch `feature/exploratorio` | Base compartilhada, documentos e arquivos locais | Conteúdo inventariado; base já ancestral da integração |
| Worktree Codex `b25e/overmem` | Estudo de viabilidade e documentos sem commit | Snapshot integral de código/documentação, sem substituir as decisões posteriores |
| `D:\git\overmem` | Calendário legado, scripts de port, estudos e testes | Código original preservado; 13 testes de CLI recuperados para a suíte ativa |
| `gearlabs/overmem` e `gearlabs-history-cleanup/overmem` | Refatoração histórica de calendário em handlers e vocabulário `daily` | Variantes preservadas; operações equivalentes mantidas no módulo atual; aliases CLI/MCP adicionados |
| Quatro snapshots em `history-docs/_sources` | Versões intermediárias e documentos | Preservação exata, com identificação separada de cada origem |

Os hashes completos das 12 origens, população, caminhos e exclusões estão no [manifesto](preservation-manifest.json). O worktree registrado como `D:\git-lab-pes\m3-overmem-m3-x1` estava ausente/prunable antes desta intervenção, com a branch apontando para a mesma base `5f00ca3`; não foi inventariado como diretório existente nem removido.

## Preservação verificável

- **2.535 arquivos inventariados**; identidade composta `(origem, caminho)` e SHA-256 dos bytes originais.
- **2.436 arquivos** em 12 snapshots ZIP versionados em `archive/lineage/`, total de 4.506.617 bytes comprimidos. Incluem código, testes, documentação, protótipos e scripts históricos.
- **99 arquivos** adicionais preservados em objetos locais, incluindo dumps, evidências e configurações. Eles não fazem parte do conteúdo público do GitHub.
- Cofre local: `D:\git-lab-pes\overmem\artifacts\consolidation-2026-09-12`. O diretório contém `objects/`, inventários completos, metadados Git, bundles e logs desta validação. Fica dentro da pasta canônica escolhida, mas é ignorado pelo Git.
- Bundles `overmem-before.bundle` e `legacy-dgit.bundle` preservam os históricos independentes disponíveis. As contribuições atuais foram integradas com merges, mantendo seus commits e autoria.
- `.git`, `bin`, `obj`, `.vs`, `node_modules` e `__pycache__` foram excluídos da cópia de arquivos. Metadados Git têm preservação própria; caches e saídas de compilação não são classificados como código-fonte. As exclusões por origem estão registradas.
- Nenhum diretório de origem foi apagado. A conferência abrange as 12 origens encontradas; não afirma uma busca exaustiva em todos os discos do computador.

Verificação completa, a partir da raiz do repositório:

```powershell
python scripts/consolidation/verify_preservation.py --vault D:/git-lab-pes/overmem/artifacts/consolidation-2026-09-12
```

O verificador exige população completa, sem entradas ausentes, inesperadas, duplicadas ou divergentes. Sem `--vault`, verifica os arquivos versionados e informa explicitamente quantos arquivos locais não foram conferidos.

Para recuperar uma origem em uma **nova** pasta, sem sobrescrever arquivos:

```powershell
python scripts/consolidation/restore_source.py --source overmem --output artifacts/restored-overmem --vault D:/git-lab-pes/overmem/artifacts/consolidation-2026-09-12
```

## Correções necessárias à integração

1. Os modos MCP stdio, HTTP (`sse`, endpoint `/sse`) e pipes coexistem. Stdio continua sendo o padrão e não inicia um servidor HTTP.
2. As ferramentas de jogadores e famílias foram registradas no MCP. A descoberta real por `tools/list` é testada, incluindo nomes sem duplicação.
3. Os quatro comandos de famílias foram conectados ao parser e ao help da CLI. A comparação de catálogos é offline e dispensa PID.
4. Exportação/comparação de catálogos agora aceita os caminhos de arquivo informados pelo usuário; não acrescenta um nome derivado de outra sessão. Catálogo inexistente gera falha explícita.
5. O refinamento da ponte de pipes agora lê bytes antes da decodificação Int32; antes solicitava texto decimal e o interpretava como hexadecimal.
6. Os nomes antigos `pes2021-*-daily-calendar-*` têm aliases para as operações `secondary-calendar`. Os aliases MCP usam o **schema atual**; não garantem compatibilidade binária ou de nomes de propriedades com os DTOs antigos. Os arquivos originais continuam disponíveis nos snapshots.
7. CLI e MCP podem ser empacotados juntos; `serve` valida transporte/porta, mantém mensagens no stderr e encerra seu próprio subprocesso quando cancelado.

## Uso da distribuição única

Pré-requisitos: Windows, SDK .NET 10 para compilar a solução e executar a suíte PES; hosts e biblioteca continuam em `net8.0`. A distribuição exige os runtimes .NET 8 e ASP.NET Core 8. Python 3.11+ é necessário apenas para os verificadores e scripts Python.

```powershell
dotnet build Overmem.slnx
./scripts/Publish-Overmem.ps1
./artifacts/distribution/Overmem.Cli.exe --help
./artifacts/distribution/Overmem.Cli.exe serve --transport sse --port 5000
```

Também é possível iniciar `Overmem.McpServer.exe` diretamente, sem argumentos para stdio ou com `--pipe <nome>` para a ponte delimitada. A opção `sse` preserva o nome adotado na branch de melhorias e usa o transporte HTTP do SDK MCP; não é uma promessa de compatibilidade com qualquer protocolo SSE antigo.

## Validação e limites

As verificações finais e contagens estão em [validation.json](validation.json). Abrangem compilação, testes das duas suítes, comandos de calendário legados, descoberta MCP via stdio, pipes contra `Overmem.TestTarget`, distribuição CLI/HTTP e hashes de preservação. Nenhum jogo ou Sider foi iniciado; nenhum teste se conectou ao PES.

Family Discovery continua sendo uma funcionalidade experimental: classificação por heurísticas, limites de cobertura e comparação de famílias ainda exigem evidência controlada. Em particular, a comparação herdada usa stride/classe para associar famílias e pode rejeitar catálogos com IDs repetidos no mesmo grupo; não foi promovida a prova de identidade de jogadores. A consolidação preserva o código e corrige sua integração, sem declarar concluída a pesquisa sobre a memória do jogo.

Os protótipos `test*.cs`, `test3.csproj` e `TestProject/` vieram da branch de melhorias. Estão preservados, fora de `Overmem.slnx`; não são aplicativos suportados nem entram na validação da solução. Scripts históricos de port/injeção permanecem nos snapshots e não foram executados.

## Antes de uma futura limpeza

A integração está na branch principal, a pasta canônica usa a revisão aprovada, o verificador retorna `issues=[]`, e uma segunda cópia do cofre e dos três bundles foi criada fora dos diretórios candidatos em `D:\Tools\_backups\lab-pes-overmem\consolidation-2026-09-12`. A referência operacional antiga foi removida de `C:\Users\Willian\.codex\config.toml`; históricos de conversa/editor foram mantidos como evidência.

O [gate de limpeza](cleanup-readiness.md) e o [plano de alvos](cleanup-plan.json) tornam esses requisitos executáveis. O gate também bloqueia a operação se uma origem mudar depois do inventário. A aprovação para mover ou apagar diretórios continua sendo uma etapa posterior. O checkout isolado `artifacts/consolidation-2026-09-12/candidate` mantém os resultados de build desta sessão e pode ser retirado depois com o procedimento normal de worktrees, preservando o cofre que o contém.

**Não apagar `artifacts/consolidation-2026-09-12` pensando que contém apenas arquivos regeneráveis. O GitHub sozinho não contém os 99 arquivos locais de evidência.**
