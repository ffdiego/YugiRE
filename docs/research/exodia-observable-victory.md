# Exodia — condição de vitória observável (sandbox Core)

**Estado:** regra lógica implementada; **primeira etapa** do Draw
`00454010` parcialmente portada. A animação posterior da figura
montada de Exodia continua pendente, e nenhuma paridade visual foi
demonstrada.

## Evidência e contrato

O arquivo `joey-knowledge.sqlite` foi consultado para
`00453B60` (construtor do objeto visual Exodia) e
`00454010` (Draw decompilado; mais de 7 mil caracteres,
conversões de ponteiros/registradores ambíguas).
O ZIP visual já fornecido inclui `00454000`:
o contador de apresentação nativo em `obj+0x10`
avança **4** por Update. Nenhuma nova exportação Ghidra foi
solicitada.

O índice do DAT confirma `j\duel\exodia\exodia.txt`,
`card_hikari.yga`, `effe_hikari.bmp`,
`exo_face1..3.yga`, `exo_arm1..3.yga`,
`exo_arml1..3.yga` e `exo_ball.yga`.
A decompilação do Draw permite recuperar o **primeiro ramo**,
que usa as miniaturas das cinco cartas de `data/mini/list_card.txt`.
A correspondência entre os BMP/YGA de Exodia e os ramos
posteriores a esse primeiro trecho **não está confirmada**.
Apenas o primeiro ramo foi integrado como `Partial`.

O contrato observável do jogo de cartas é possuir **cinco peças
distintas simultaneamente na própria mão**: cabeça, braço direito,
braço esquerdo, perna direita e perna esquerda. Nenhum outro
monstro com `Exodia` no nome substitui uma peça. A regra
não exige que o LP do adversário chegue a zero.

O `ExodiaRules.CompleteHand` reconhece **nomes ingleses exatos**
do catálogo do DAT (comparação case-insensitive, ignora espaços
exteriores) e exige `Card.IsMonster`. Esta é uma
**estratégia de sandbox**: os identificadores internos da
família ainda precisam ser confirmados pelo catálogo original
antes de declarar paridade de nomes/IDs ou compatibilidade com
outros idiomas. O SQLite de callbacks contém 443 perfis,
mas não os cinco IDs de Exodia, que não têm callbacks separados
nessa tabela.

## Integração com o Core

`DemoDuel.ExodiaWinner` distingue esse motivo dos duelos
vencidos por dano ou deck-out. Ao completar a mão,
`Winner` é definido imediatamente e
`ExodiaCompleted` é emitido **uma única vez**, levando
cinco snapshots de `Card` na ordem cabeça/braços/pernas.

Locais em que o estado da mão pode ficar completo:
- mão inicial de `FromDecks` e snapshots já completos
  de `FromPartialObservation`;
- compra normal, cartas de compra e a substituição de mãos
  por Morphing Jar;
- busca de cartas no Deck/cemitério para a mão;
- retorno de monstros à mão por efeitos FLIP;
- compra especial que descarte as magias recém-compradas,
  avaliada ao final do efeito.

O evento de transferência de carta ocorre **antes**
de `ExodiaCompleted` quando a quinta peça é comprada,
para permitir uma futura animação após o voo da carta.
A máquina lógica encerra o duelo independentemente do renderer.
A Alpha enfileira a primeira fase orbital **depois** do voo da
quinta carta, quando existente. O banner de vitória genérico
só começa após essa primeira fase; as etapas posteriores do
efeito Exodia permanecem sem reimplementação.

## Cobertura de regressão

`src/tests/Joey.Tests/ExodiaChecks.cs` valida:
- cinco nomes distintos, ordem independente, maiúsculas,
  extras na mão; sem falsos positivos por peças duplicadas,
  monstros não relacionados e magias falsificadas;
- vitória imediata nos dois lados após comprar a quinta
  peça (LP intactos, exatamente um evento e ações bloqueadas);
- estado de observação já completo;
- busca de Sangan que transfere a última peça do Deck;
- Hane-Hane devolvendo a última peça à **mão adversária**,
  cuja vitória deve ocorrer mesmo no turno do controlador
  do Hane-Hane;
- nenhuma vitória quando uma das cinco peças está somente
  no campo.

## Limites ainda não comprovados

- Prioridade em mãos iniciais **simultaneamente** completas:
  `FromDecks` verifica o lado 0 antes do lado 1 por política
  do sandbox, não por evidência de ordem nativa.
- A momento de encerrar efeitos multi-compra/Morphing Jar:
  o Core deixa as operações já comprometidas terminarem;
  a ordem de apresentação com o encerramento nativo
  exige captura pareada.
- A primeira janela do Draw (`0 ≤ t < 316`) foi portada,
  mas o restante da animação, as imagens finais e os sons
  permanecem sem equivalência; o HUD usa o aviso de vitória
  geral após essa janela.

## Primeira janela visual de 00454010 (21/11/2026)

`00454000` soma `4` ao contador do objeto por Update.
O primeiro ramo do Draw, `t < 0x13C`, tem **79 amostras**
`0, 4, …, 312` e itera cinco vezes, desenhando uma
miniatura por peça de Exodia.

Para a miniatura `i` de 0 a 4:

```text
angle = floor(i * 512 / 5)
alpha = clamp(t * 16 - 256 * i, 0, 255)
X = 400 + trunc_to_zero(C[(angle + 128) & 511] * 200 / 65536)
Y = 300 - trunc_to_zero(C[angle] * 200 / 65536)
    + floor((256 - alpha) / 4)
```

A coordenada é o **centro** do BMP (o Draw original
subtrai metade das dimensões), num círculo de raio
200 pixels em torno de `(400,300)`. Cada carta entra
com atraso de 16 unidades no contador nativo, desaparecida
64 pixels abaixo do ponto final, e chega ao ponto de
destino com opacidade 255. A Alpha usa a tabela de
cosseno aproximada do `Joey.LookAndFeel`, miniaturas
originais do DAT e uma sequência de 79 Updates.

A ordem dos arquivos de miniatura é a das cinco cartas
retornadas por `ExodiaRules` (cabeça, braços, pernas).
**A ordem de colocação nativa dos cinco recursos e sua
orientação exata ainda precisam de captura pareada.**

O Draw decompilado entra em outro ramo a partir de
`t=316`, com composição, texturas, brilhos e
movimentos da figura de Exodia mais complexos. Em vez
de inventar essa etapa, a Alpha termina a animação
orbital e então apresenta o banner comum de vitória.

A `NativeExodiaOpeningFrame` valida contadores, os
cinco centros, opacidades e a transição após o voo.
A função `00454010` passa a constar como **Partial**,
não `Verified`.

**Métrica visual:** **19/31 (61,29%) Partial**, com
**0 Verified**; a contagem considera apenas esta primeira
fatia do Draw de Exodia.
