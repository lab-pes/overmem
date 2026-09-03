# P12 — Gate de revisão

## Decisão solicitada ao Codex

Validar se o pacote pode ser aceito como atlas offline do registro EDIT, sem promover novos significados nem autorizar escrita.

## Critérios de aceitação

1. O analisador não contém APIs de processo ou de escrita de memória.
2. Os 25.005 registros decodificam para exatamente 380 bytes e passam pelo SHA-256 embutido.
3. Os dois processos históricos possuem o mesmo conjunto de IDs e hashes.
4. O censo contém exatamente uma linha por byte do registro.
5. O inventário da CT preserva offsets, larguras e bitfields sem tratá-los como confirmação.
6. O corpus possui exatamente 30 controles e inclui IDs comuns, nomes não ASCII e IDs com bits altos.
7. Os dumps brutos permanecem fora do commit.

## Gate semântico

- `CONFIRMED`: somente o que já estava confirmado pelo perfil e pela validação estrutural anterior.
- `CANDIDATE`: rótulos da CT ou campos já candidatos no perfil.
- `UNKNOWN`: rótulos interrogativos, bytes sem semântica ou associações ainda não validadas.

## Próximos pacotes independentes

### P13 — Auditoria do scanner de relações clube/jogador

Escopo: revisão estática e testes sintéticos do scanner existente. Verificar colisões de identidade composta, falsa proximidade entre ID e nome, limite de região, escolha prematura do “melhor” candidato e ausência de inferência estrutural. Não executar varredura ao vivo e não sobrepor o trabalho exploratório M3-X1.

### P14 — Gerador de comparação EDIT × ML

Escopo: ferramenta offline que recebe dois dumps operacionais, associa por ID e fingerprint, produz diffs por byte/campo e separa mudança global, mudança por jogador e provável ruído de sessão. Pode ser implementada e testada com fixtures sintéticas agora; captura ML real continua dependendo do usuário.

### P15 — Perfil candidato ampliado da CT

Escopo: gerar um perfil de estudo separado, somente leitura, contendo os 123 rótulos candidatos da CT. Não substituir o perfil operacional nem expor escrita. A promoção individual exige controles de UI e repetição após reinício.

## Stop conditions

- Não anexar ao PES.
- Não escrever em memória ou save.
- Não promover alegação da CT diretamente a `CONFIRMED`.
- Não reutilizar endereços absolutos históricos.
