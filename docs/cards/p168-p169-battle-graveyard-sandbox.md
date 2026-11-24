# P168/P169 — gatilhos de batalha e FLIP diferido (sandbox)

Status: **implementado e testado no sandbox**, sem equivalência dinâmica certificada
com `joey_pc.exe`. PR empilhado sobre P164/P006/P018; `main` inalterada.

## Evidência e identidades

O catálogo `docs/evidence/card-callback-catalog.json` associa o perfil P168
a Nimble Momonga (recurso `1115`, índice interno `453`) e Giant Germ
(recurso `1114`, índice interno `383`). O perfil P169 é Spear Cretin
(recurso `1116`, índice interno `516`).

Os textos históricos do jogo podem ser consultados em
https://gamefaqs.gamespot.com/pc/920746-yu-gi-oh-power-of-chaos-joey-the-passion/faqs/32648
e, para detalhes de Spear, em
https://www.edisonformat.com/rulings/individual-card-rulings-s-t .

| Carta | Gatilho | Ação obrigatória | Escolha |
| --- | --- | --- | --- |
| Nimble Momonga | Destruição em batalha + entrada no cemitério | +1000 LP para o proprietário no sandbox | Invoca 0–2 cópias do deck viradas para baixo em defesa |
| Giant Germ | Mesmo gatilho | -500 LP para o adversário do proprietário | Invoca 0–2 cópias do deck em ataque virado para cima |
| Spear Cretin | Envio campo → cemitério **depois** de ter sido virada | Não ativa no FLIP em si | Cada lado escolhe um monstro do próprio cemitério e o invoca em ataque visível ou defesa virada para baixo |

## Implementação

- `DemoDuel.MonsterWasFlipped[player, zone]` armazena o histórico da carta
  ocupando uma zona. É marcado em Flip Summon, ataque que revela face-down
  e revelação externa. É limpo quando a carta deixa a zona ou uma nova
  carta é colocada. `TriggerFlip` não executa a ressurreição do Spear:
  o envio posterior ao cemitério é que gera a decisão P169.
- `RemoveMonster` registra se a remoção ocorreu por batalha. P168 só
  aplica LP e recruta nesse caso, nunca por tributo ou destruição de efeito.
  P169 recebe a informação de ter sido previamente virado, sem exigir batalha.
- A fila de decisões de P006/P164 é reaproveitada: cada efeito é resolvido
  isoladamente; Spear oferece uma seleção por jogador quando houver opções
  válidas e zona livre. A própria ocorrência de Spear enviada ao cemitério
  é excluída, mas outra cópia é elegível.
- `CardSelectionRules.SameNameMonster` exige o mesmo ID de recurso e
  `AnyMonster` serve para o cemitério. Para P168, `MaxSelections` é
  limitado às cópias encontradas, a 2 e às zonas disponíveis.
- `SelectBatchCard` acumula escolhas sem mutação; `FinishOptionalCardSelection`
  transfere 1–2 cartas e embaralha o deck remanescente **uma única vez**
  por resolução. `SkipOptionalCardSelection` com zero escolhas não
  invoca nem embaralha e não cancela ganho/dano de LP.
- `ResolveCardSelection(index,zone,faceDownDefense,out message)` permite
  escolher postura na ressurreição do Spear; o modo de ataque é o padrão.
  As invocações não gastam a Normal Summon.
- O seletor raylib apresenta escolhas múltiplas e botões Done/Skip/posição.
  Layout, timing e comportamento visual nativos ainda não foram medidos.

## Bateria de regressões

`src/tests/Joey.Tests/BattleGraveyardEffectChecks.cs` contém casos para
ganho/dano obrigatório com e sem cópias disponíveis, 0/1/2 invocações,
transação de lote, posições das cartas, zonas livres, filtros de ID, tributo
sem ativação P168, ausência de efeito Spear antes de flip, revelação por
efeito seguida de destruição, ressurreição para os dois lados, exclusão da
própria ocorrência, defesa oculta e ativação após batalha reveladora.

## Limitações explícitas

Sem chains, negação, mudança de dono/controlador, destinos substitutivos,
restrições especiais de invocação de ritual/fusão, regras de timing e
RNG nativos. A sequência de escolhas de Spear (dono antes do adversário)
é uma política provisória do sandbox; a referência moderna pode ter
regras adicionais e não substitui validação na build Joey original.
Não promover para `Verified` até obter fixtures diferenciais adequadas.
