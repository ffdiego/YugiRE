# P006 — buscadores do deck por envio do campo ao cemitério

Escopo: **sandbox C#**, não paridade nativa verificada. Branch de desenvolvimento
empilhada sobre P018 para reutilizar a escolha genérica e a UI da raylib.

## Dados do executável já identificados

No catálogo (`docs/evidence/card-callback-catalog.json`), callback primário
`P006 = 0x00583D20` é compartilhado exatamente por:

| Nome | Resource ID | Filtro histórico | Destino |
| --- | ---: | --- | --- |
| Sangan | 47 | Monstros ATK ≤1500 | Deck → mão |
| Witch of the Black Forest | 573 | Monstros DEF ≤1500 | Deck → mão |

Trata-se de **gatilho de envio do campo ao cemitério**, não FLIP.
A função nativa catalogada apenas demonstra compartilhamento de callback;
não certifica o fluxo/campos da implementação C#.

## Desenho de implementação

- `DemoDuel.RemoveMonster` é a transição para o graveyard, distinguindo
  retorno à mão (`ReturnMonsterToHand`) e retorno ao deck por efeito.
- Cada retirada de Sangan/Witch gera gatilho enfileirado, pois tributos
  simultâneos ou duas destruições de batalha podem ativar efeitos em sequência.
- `CardSelectionRules.OnFieldToGraveyardFilter` define o predicado;
  `PendingCardSelection` expõe as cartas elegíveis e suas posições no
  Main Deck, mas não revela o deck inteiro.
- `ResolveCardSelection` revalida ocorrência e filtro, transfere do
  Main Deck para a mão do dono no sandbox e embaralha o restante
  com `effectRandom` (seed determinística para teste, não RNG nativo).
- Sem cartas válidas, não há pedido de seleção, mas o gatilho não deve
  bloquear o duelo.
- No modelo atual **o controlador coincide com o dono**. Ainda não há
  chains, negação, banimento/substituição de destino, restrições históricas
  confirmadas pelo EXE, side deck ou políticas da banlist.
- Banlist limita montagem do deck e não deve filtrar a busca durante o
  duelo se a carta já está no Main Deck.
- Fluxos testados: destruição em batalha de defensor, tributo, efeito
  Man-Eater Bug, retorno Penguin Soldier (sem gatilho), seleção inválida,
  zero candidatos, duas cartas tributadas e shuffle determinístico.

Próxima família: `P164` (callback primário `0x0058AC10`): Mystic Tomato,
Giant Rat, Shining Angel, UFO Turtle, Mother Grizzly, Flying Kamakiri #1.
Requer contexto de destruição **em batalha** e destino `Deck -> Field`
com atributo, posição e zonas de campo válidas — não atribuir um filtro
completo a cartas só por compartilhar o callback.
