# P15 — Gate de revisão

## Aceitar

- duas arenas estruturalmente válidas coexistem com a ML carregada;
- a arena A é classificada como cadastro-base por população, identidade e estabilidade;
- a arena B é classificada como cópia ML estrutural candidata;
- salário, datas, flags e links são observações reais, mas mantêm semântica `CANDIDATE` ou `UNKNOWN`;
- os três IDs duplicados permanecem ambíguos;
- `+0x164/+0x166` pode avançar como candidato de relação com elenco atual.

## Não aceitar ainda

- unidade ou periodicidade do salário sem correlação com a UI;
- `+0x12C/+0x12E` como team/league apenas pelo rótulo da CT;
- bit `loan listed` como prova de empréstimo vigente;
- `+0x160/+0x162` como clube proprietário ou destino sem experimento controlado;
- qualquer endereço histórico como endereço estável;
- qualquer escrita.

## Experimentos mínimos seguintes

1. Conferir na UI os seis salários e términos listados no resumo.
2. Identificar um jogador comprovadamente emprestado na ML e comparar os três pares de vínculo.
3. Identificar um jogador transferido depois do início da ML e repetir a comparação.
4. Salvar/recarregar a mesma ML, redescobrir ambas as arenas e exigir equivalência semântica.
5. Carregar outra ML ou temporada para separar campos globais de campos do save.
