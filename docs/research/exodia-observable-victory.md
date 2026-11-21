# Exodia — condição de vitória observável (sandbox Core)

**Estado:** regra lógica implementada; animação `00454010` ainda
`Pending` no inventário Draw. Esta distinção é deliberada:
um duelo terminar corretamente não equivale a reproduzir a cena.

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
**Não está confirmado** como os recursos se distribuem
pelos estados temporais de `00454010`, nem como o sprite
de cada carta se movimenta. Não inventar associação de
sprites ou promover esta rotina a `Partial` antes de
implementar uma etapa vertical sustentada pelas evidências.

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
A máquina lógica encerra o duelo independentemente do
renderer; o banner de resultado genérico existente na Alpha
continua em uso até portar o efeito Exodia nativo.

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
- Sem equivalência da animação `00454010`; o HUD usa
  o aviso de vitória geral já implementado.

**Métrica visual:** permanece **18/31 (58,06%) Partial**,
com **0 Verified**; adicionar a regra de vitória não conta
como reimplementar o Draw de Exodia.
