# Próximas mecânicas propostas

Estas regras são propostas para a próxima rodada de implementação. Eventos e melhorias ainda não têm efeitos no protótipo.

## Eventos aleatórios

- A partir do dia 2, sortear no máximo um evento no início do dia, com chance inicial sugerida de 25%.
- Evitar repetir o mesmo evento em dias consecutivos. Mostrar o evento em um painel antes de permitir ações.
- **Chuva forte:** pesca produz 1 comida a menos naquele dia.
- **Boa colheita:** a primeira colheita do dia produz 1 comida a mais caso seja convertida em comida.
- **Árvores caídas:** a primeira coleta de madeira do dia produz 1 madeira a mais.
- Guardar os efeitos em uma estrutura de dados separada das ações, para adicionar escolhas e novos eventos depois.

## Melhorias compradas com madeira

| Melhoria | Custo inicial sugerido | Efeito proposto |
| --- | ---: | --- |
| Ampliar floresta | 4 madeiras | +1 madeira por coleta, uma compra por partida |
| Ampliar lago | 4 madeiras | +1 comida por pesca, uma compra por partida |
| Melhorar casa | 6 madeiras | Libera um terceiro trabalhador, que também exige 2 comidas por dia |

As melhorias devem ter modelos de blocagem permanentes na Hierarquia da cena. A compra apenas alternará seus estados e modificará regras; custo, frequência e limite ficam sujeitos ao teste de equilíbrio.
