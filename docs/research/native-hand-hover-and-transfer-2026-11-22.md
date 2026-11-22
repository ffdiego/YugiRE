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

## Seleção de cartas: recursos originais do DAT (22/11/2026)

O usuário extraiu o manifesto `j\\duel\\card\\card.txt` e metadados de imagem.
A ordem é `card_waku#s.bmp`, `card_waku#l.bmp`, `card_waku#s2.bmp`,
`card_waku#l2.bmp`; as dimensões são **58x80**, **108x153**,
**58x80** e **108x153**. As margens correspondem a quatro pixels
em torno de cartas de 50x72 e 100x145, respectivamente.

Em `00418EF0`, o índice do recurso é
`((descriptor & 0x3E) == 0x16) + 2 * variant`. A região 11
(`0x16`) escolhe a imagem grande; a variante depende de bits de
propriedade/owner e pode ser modificada por flags da carta.
O destaque é desenhado apenas quando o ponteiro global indica a
carta selecionada ou quando um estado visual especial está ativo.

`NativeCardSelectionFrame` preserva todos os nomes e padding de forma
testável e substitui o retângulo branco do cliente nos cartões da mão.
Durante o hover da CPU utiliza a variante alternativa de 108x153 e
gira o destaque junto ao verso. A associação simples near/far para
a variante ainda é uma **hipótese do cliente**, não uma equivalência
comprovada para todas as flags e estados do executável.

`DatGraphics` aplica o mesmo teste de color-key verde aos quatro
sprites. A cor transparente real de cada variante deve ser conferida
com o BMP original e, se necessário, ajustada por análise de pixels.
Falta estender a aplicação para a seleção das cartas de campo e
comparar recortes equivalentes no 800x600 original.

## Ajustes de foco observados no jogo original (22/11/2026)

O jogador reportou:
1. Em hover na mão oposta, o cartão de Joey **desce** enquanto o
   marcador `card_waku#l2.bmp` acompanha o verso. A mão próxima ainda
   sobe. O mesmo passo 3/4 do `HandPresentation.Approach` governa ambas.
2. O painel de detalhe **nunca fica vazio**: inicia com o verso original
   e sem descrição; ao sair de um alvo conserva o último cartão
   conhecido ou o último verso oculto. Apenas outro alvo válido muda a
   seleção. Espaços vazios do campo não apagam o conteúdo.
3. Os sprites **pequenos** `card_waku#s.bmp` e `#s2.bmp` são desenhados
   no campo sobre monstros, magias/armadilhas e cartas de campo
   existentes, acompanhando a orientação da carta (ataque/defesa).
   Um espaço vazio não gera marcador.
4. Uma carta do adversário ainda oculta, seja na mão ou no campo,
   aparece apenas como verso no painel; nome, ATK/DEF e descrição não
   são divulgados.

Os testes sintéticos conferem os deslocamentos espelhados de 32px e
as margens do frame pequeno em defesa. Os valores exatos das variantes
normais/alternativas e os estados do renderizador 00418EF0 ainda
requerem comparação de pixels. Essas mudanças são do cliente de
apresentação; `Joey.Core` permanece intacto.
