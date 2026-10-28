# Próximas mecânicas propostas

Estas regras são propostas para a próxima rodada de implementação. Eventos e melhorias ainda não têm efeitos no protótipo.

## Eventos aleatórios

- A cada início de dia, sortear se haverá um evento. A chance, a duração e os efeitos serão configuráveis; não precisa ocorrer todos os dias.
- Exemplos definidos: seca prejudica plantações e pesca; pragas afetam plantações; chuva acelera crescimento e recuperação da floresta.
- A Comadre Fulozinha usa um gatilho de exploração excessiva da floresta, separado do sorteio comum. Sua desorientação torna um trabalhador indisponível por uma duração configurável.
- O card, o fundo escuro e o painel de descrição ficarão na Hierarquia da cena. A sequência de animação e a pesquisa para o texto estão em [MissoesELoja.md](MissoesELoja.md).
- Remédio e oferenda para a Comadre ainda exigem regras e custos definidos.

## Melhorias compradas com madeira

| Melhoria | Custo inicial sugerido | Efeito proposto |
| --- | ---: | --- |
| Ampliar floresta | 4 madeiras | +1 madeira por coleta, uma compra por partida |
| Ampliar lago | 4 madeiras | +1 comida por pesca, uma compra por partida |
| Melhorar casa | 6 madeiras | Libera um terceiro trabalhador, que também exige 2 comidas por dia |

As melhorias devem ter modelos de blocagem permanentes na Hierarquia da cena. A compra apenas alternará seus estados e modificará regras; custo, frequência e limite ficam sujeitos ao teste de equilíbrio.
