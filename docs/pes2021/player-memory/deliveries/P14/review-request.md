# P14 — Gate de revisão

## Aceitar quando

1. Cada registro é validado por comprimento e SHA-256 antes do diff.
2. IDs são tratados como `u32` opacos, inclusive com bits altos.
3. IDs duplicados são preservados, relatados e excluídos da associação automática.
4. Fingerprint incompatível impede comparação automática dos bytes.
5. Todos os 380 offsets são contabilizados.
6. Os rótulos semânticos mantêm seu status de evidência de origem.
7. O pacote não contém APIs de processo ou escrita de memória.

## Estado do gate

- Implementação: `PASS`.
- Testes sintéticos: `PASS`.
- Integração EDIT × EDIT após reinício: `PASS`.
- Integração EDIT × ML real: `PENDING_USER_CAPTURE`.

## Próximo passo quando houver ML

1. Redescobrir a arena no processo corrente.
2. Gerar um dump somente leitura com ML carregada.
3. Executar o comando documentado em `commands.md`.
4. Auditar primeiro os offsets com maior frequência de mudança.
5. Correlacionar controles conhecidos de clube, salário, contrato e empréstimo.
6. Não promover nem escrever qualquer campo apenas com base no diff.
