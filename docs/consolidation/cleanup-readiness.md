# Gate para limpeza das cópias do Overmem

Este gate existe para permitir uma limpeza posterior sem transformar a consolidação em uma suposição. Ele **não apaga arquivos**. A limpeza só pode começar quando o relatório produzido tiver `status: READY` e nenhum check `FAIL`.

## Estado preparado em 2026-09-12

- Repositório canônico e remoto: `D:\git-lab-pes\overmem` → `https://github.com/lab-pes/overmem.git`.
- Integração aceita na `master`: commit de merge `4a0b99b9784ea838dba9e6a7222b144f00283781`.
- Cofre primário: `D:\git-lab-pes\overmem\artifacts\consolidation-2026-09-12`.
- Cofre secundário independente: `D:\Tools\_backups\lab-pes-overmem\consolidation-2026-09-12`.
- Os dois cofres validam 2.535/2.535 arquivos. Ambos contêm os bundles `overmem-before.bundle`, `legacy-dgit.bundle` e `canonical-master.bundle`.
- A configuração operacional `C:\Users\Willian\.codex\config.toml` deixou de autorizar `D:\git\overmem` como projeto. Uma cópia anterior à alteração está no cofre secundário.
- As configurações ativas verificadas do Codex, OpenCode e VS Code não contêm caminhos para as 11 cópias candidatas.
- Históricos de conversa/editor não são configurações executáveis e podem conservar caminhos antigos como evidência.

O Codex ainda pode exibir no sidebar um projeto salvo chamado `overmem`, associado a `D:\git\overmem`. Esse cadastro é metadado da aplicação, não uma configuração usada pelo Overmem. Remova-o da lista de projetos imediatamente antes da limpeza, se ainda aparecer. Não apague tarefas ou históricos associados.

## Executar o gate

Na `master` canônica, depois de fechar tarefas ou terminais que estejam usando as cópias:

```powershell
./scripts/consolidation/Test-OvermemCleanupReadiness.ps1 `
  -ConfirmSavedProjectRemoved `
  -ConfirmTargetsClosed
```

O relatório completo é gravado em `artifacts/consolidation-2026-09-12/cleanup-readiness-latest.json`. O comando retorna código diferente de zero quando a limpeza deve ser bloqueada.

Os dois switches são declarações feitas no momento da limpeza: o projeto antigo já não aparece no sidebar e todas as tarefas, terminais e editores que usam os alvos foram fechados. Sem as duas declarações, o resultado correto é `BLOCKED`.

O gate confirma:

1. pasta, branch, remoto e commit canônicos;
2. sincronização exata entre `HEAD` e `origin/master`;
3. integridade do repositório e ausência de alterações rastreadas;
4. correspondência dos subdiretórios validados `src`, `tests` e `scripts`;
5. população e SHA-256 dos 2.535 arquivos nos dois cofres;
6. integridade dos seis exemplares dos três bundles Git;
7. ausência de arquivos novos, ausentes ou divergentes nas 11 origens desde o inventário;
8. preservação por hash dos arquivos locais não rastreados da pasta canônica;
9. ausência de caminhos antigos nas configurações operacionais listadas no plano;
10. existência e identidade exata dos 11 alvos de limpeza.

## Unidades de limpeza

O arquivo [cleanup-plan.json](cleanup-plan.json) é a fonte de verdade dos alvos. Há três classes:

- `repository`: repositório independente; remover somente a pasta exata depois do gate.
- `nested-directory`: subdiretório dentro de outro repositório; remover apenas o subdiretório `overmem`, nunca o repositório pai.
- `git-worktree`: retirar primeiro com `git worktree remove <caminho>` a partir do repositório canônico. Não usar exclusão recursiva direta enquanto o worktree estiver registrado.

O checkout `artifacts/consolidation-2026-09-12/candidate` é um worktree interno do processo de integração. Ele não aparece como origem no manifesto e não deve ser confundido com o cofre. Antes da limpeza, pode ser retirado com `git worktree remove` somente depois de confirmar que nenhuma tarefa o utiliza. O restante de `artifacts/consolidation-2026-09-12` deve permanecer.

## Condições de parada

Interromper a limpeza quando:

- o gate retornar `BLOCKED`;
- uma tarefa, terminal ou editor estiver usando um alvo;
- um caminho real não coincidir exatamente com o plano;
- aparecer qualquer arquivo novo ou divergente em uma origem;
- qualquer cofre ou bundle falhar;
- `master` local e remota divergirem;
- o alvo for pai ou ancestral da pasta canônica ou do cofre secundário.

Na etapa de limpeza, prefira mover diretórios independentes para uma quarentena fora dos locais de trabalho. Verifique novamente o repositório canônico e a restauração antes de excluir a quarentena. A remoção definitiva deve ser uma autorização separada.
