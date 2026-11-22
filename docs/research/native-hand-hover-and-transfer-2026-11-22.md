# Mãos do jogador e CPU: hover, reflow e compra (22/11/2026)

Requisitos observados pelo usuário: sprite sobre carta selecionada; hover da CPU mostra verso sem informações; reflow simultâneo à transferência; virada da compra própria; inspeção contínua salvo fala do Joey.

## Mudanças de implementação

- A mão não reverte a transferência ativa ao calcular a quantidade visível. A chegada fica separada em primeiro plano até aterrissar, e a mão restante reacomoda durante o voo.
- Na saída, a posição da carta é capturada antes de removê-la do layout, preservando sua origem enquanto as demais se movem.
- Hover e atualização do painel lateral independem do turno e das ações legais. Só cliques que mudam o duelo continuam sujeitos às restrições do Core.
- Hover adversário retorna apenas um marcador de verso, sem Card ou texto; o painel renderiza o verso original e deixa descrição e atributos vazios.
- A compra perto do jogador inicia de verso, comprime horizontalmente até a aresta no quarto dos oito updates e termina de frente. A CPU mantém a carta oculta.

## Evidência e limites

- Ghidra 00418B60: incremento de 0x20 até 0x100, campos de orientação e transição de face. 00418EF0 aplica tabela cosseno em parâmetros da carta.
- A prévia usa Math.Cos, não o conteúdo exato da tabela do binário, e não afirma composição pixel-perfect.
- A exceção de fala está ligada ao DuelPresentationCue.JoeySpeech. Cut-ins e fala reais ainda não compõem o cliente de modo completo.
- Ainda pendente: trocar a borda raylib do card selecionado pelo recurso original. O DAT tem card_waku#l.bmp, #l2.bmp, #s.bmp e #s2.bmp em j/duel/card; a associação exata de variante e enquadramento depende do manifesto card.txt.
- Ainda pendente: capturas pareadas da virada e do hover em 800x600, com os mesmos estados do original.

Nenhuma regra em Joey.Core foi alterada e a cobertura de Draws ainda não pode ser elevada. Verificar manualmente com compras/descartes de duas e cinco cartas e com a mão da CPU durante animações.
