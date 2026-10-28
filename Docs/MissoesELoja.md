# Missões de entrega, reputação e barraca

## Referências

- *Stardew Valley* usa um quadro junto à loja de Pierre para pedidos com prazo e pagamento: https://stardewvalleywiki.com/Quests
- *My Time at Sandrock* usa encomendas com prazo que rendem dinheiro e reputação: https://mytimeatsandrock.fandom.com/wiki/Commissions

## Implementação atual

- A barraca tenta aparecer nos dias 1, 4, 7 e 10. Com reputação negativa, as visitas intermediárias têm chance inicial de 60%; os dias 1 e 10 são garantidos. O quadro de pedidos permanece acessível nos demais dias para entregas.
- Cada visita gera **2 ou 3 ofertas aleatórias**, com tipos, títulos, quantidades e recompensas variáveis. Ofertas não aceitas desaparecem no dia seguinte; as aceitas continuam até o prazo.
- O jogador pode aceitar várias ofertas. O botão **Próximo pedido** percorre as ofertas e os pedidos ativos. A entrega consome os itens do estoque e dá reputação e moedas.
- Reputação negativa gera pedidos menores e prazo inicial de quatro dias, para permitir recuperação. Reputação comum gera prazo de três dias; acima de 300 gera pedidos maiores. O prazo é limitado pelo último dia da campanha.
- O catálogo pode solicitar madeira, comida, sementes de milho ou sementes de abóbora. Os valores iniciais de quantidade, recompensa e frequência são parâmetros de protótipo.
- Reputação negativa aumenta em uma moeda o preço dos produtos e reduz o estoque da visita para duas compras. Reputação comum permite três compras; reputação alta reduz em uma moeda o preço, com mínimo de uma moeda, e permite cinco compras.
- Quando a reputação fica em −200 ou menos, há chance inicial de 25% por dia de um trabalhador se ausentar. A duração inicial é um dia. Esses números ficam no componente **DayCycle** para ajuste no Inspector. O ausente não trabalha nem consome comida. Não há duas ausências simultâneas.

## Direção para eventos

Os eventos descritos a seguir são **especificação para a próxima etapa**, sem efeitos implementados ainda:

1. Sortear no máximo um evento ao iniciar um dia, com chance ajustável. O card deve interromper as ações até ser fechado.
2. Exemplos de eventos gerais: seca reduz pesca e crescimento das culturas; pragas afetam uma ou mais plantações; chuva acelera crescimento e recuperação da floresta. O efeito precisa exibir duração e alcance.
3. A Comadre Fulozinha deve usar um indicador separado de exploração da floresta, como árvores derrubadas menos árvores reflorestadas. Ao cruzar o limite ajustável, passa a ter chance de manifestação. Ela desorienta um trabalhador por um número configurável de dias. Remédio e oferenda são decisões futuras; a opção de seguir com menos trabalhadores já se encaixa na regra de ausência.
4. Todos os elementos visuais do evento ficarão na Hierarquia da cena: fundo escuro semitransparente, frente e verso do card, painel de descrição, textos e botão X. O código apenas atualizará conteúdo, estados e animações.
5. Sequência visual proposta: card sobe de fora da tela com **Ease.OutCubic**, inclina alguns graus para cada lado e estabiliza; clique gira o card e abre a caixa à direita; texto revela letras progressivamente, com pequeno salto individual; X fecha e devolve o controle. Para o texto, usar **TextMeshProUGUI**: `maxVisibleCharacters` controla a revelação sem mexer no layout e `textInfo.characterInfo` permite animar os vértices de cada letra. A duração e amplitude do salto ficam serializadas no Inspector.

Referências técnicas: https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_Text.maxVisibleCharacters.html e https://docs.unity3d.com/Packages/com.unity.textmeshpro@3.0/api/TMPro.TMP_Text.textInfo.html
