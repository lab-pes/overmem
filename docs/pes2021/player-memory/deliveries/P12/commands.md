# P12 — Comandos reproduzíveis

Executar a partir da raiz do repositório:

```powershell
python docs/pes2021/player-memory/deliveries/P12/analyze_edit_corpus.py `
  --dump files/pes2021/player-memory/codex-live-edit-restart-2026-09-02.json `
  --baseline files/pes2021/player-memory/codex-live-edit-operational.json `
  --profile files/pes2021/player-memory/pes2021-player-edit-v1.json `
  --ct "files/PES 2021 - v21.1.0.CT" `
  --output docs/pes2021/player-memory/deliveries/P12
```

O comando é exclusivamente offline. Ele deve encerrar com código 0 e informar:

- 25.005 jogadores e IDs únicos;
- 25.005 registros brutos validados;
- zero divergências de hash entre os dois dumps;
- 380 linhas no censo de bytes;
- 128 entradas `ptrPlayer`, sendo 127 campos/bitfields e um marcador de limite;
- 30 jogadores no corpus dourado.

Para conferir o estado do repositório sem incluir os dumps grandes:

```powershell
git status --short
git diff --check
```

Os dois dumps de aproximadamente 190 MB são entradas locais e deliberadamente não fazem parte do commit.
