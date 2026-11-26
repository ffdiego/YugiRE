# P200/P192 — Magic Cylinder e Negate Attack (sandbox)

**Status:** implementadas no motor C# e cobertas por testes sintéticos,
**sem paridade nativa verificada** para `joey_pc.exe`. PR #21
é empilhado sobre #20 (Mirror Force), sem merge em `main`.

## Identidades

| Carta | Recurso | Índice interno | Pack | Callback | Regra modelada |
| --- | ---: | ---: | --- | --- | --- |
| Magic Cylinder | 1214 | 894 | Joey | P200 | Cancela o ataque e inflige ao atacante dano igual ao ATK atual do monstro que atacou |
| Negate Attack | 960 | 282 | Kaiba | P192 | Cancela o ataque, evita dano e encerra a Battle Phase |

IDs e callbacks estão em `docs/evidence/card-callback-catalog.json`;
disponibilidade por pack está em `docs/cards/pack-index.json`.
Os textos de referência são o card list do Power of Chaos: Joey the Passion
e a base Konami TCG, que não garantem o mesmo timing do EXE histórico:
- https://gamefaqs.gamespot.com/pc/920746-yu-gi-oh-power-of-chaos-joey-the-passion/faqs/32648
- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5124&ope=2
- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4804&ope=2

## Decisão / resolução

- Reusa `DemoAttackDeclaration`, `PendingAttackResponse`,
  `ActivateAttackTrap` e `PassAttackResponse` de Mirror Force.
  A janela nasce na declaração de ataque, **antes** de dano e FLIP.
  Somente Trap previamente Set por turno anterior e pertencente ao
  adversário de quem atacou é opção.
- O jogador pode escolher Mirror Force, Magic Cylinder, Negate Attack
  ou `Pass`. **Apenas uma resposta** é possível no sandbox; não há
  construção de chains nem counter-response. A ativação consome uma
  Trap e cancela o ataque declarado.
- Magic Cylinder consulta `AttackValue` na resolução e aplica dano
  ao dono do monstro atacante, inclusive contra ataques diretos ou
  defensores face-down. O atacante permanece no campo, o defensor
  não é revelado e outra criatura pode declarar um ataque durante
  a mesma Battle Phase. Dano letal aplica `Winner`.
- Negate Attack mantém monstros, LP e decks intactos. Para distinguir
  final de Battle Phase de término do turno, o sandbox volta à Main
  pós-batalha, porém `battlePhaseEndedThisTurn` impede nova entrada
  em Battle até o próximo `AdvanceTurn`.
- `LastAttackTrap` registra `ReflectedDamage` e
  `BattlePhaseEnded`, além da identidade do efeito e número de
  monstros destruídos (zero nas duas cartas). É um audit interno,
  não um evento capturado no Joey original.

## Validação e piloto nativo

`src/tests/Joey.Tests/AttackNegationTrapChecks.cs`: resposta adversária
por tipo DAT, combate vs ataque direto, defensor FLIP não revelado,
ATK de equipamento/campo (Sogen), dano refletido, morte por LP,
continuação por outro monstro, fim da Battle Phase, reset no próximo
turno, escolha entre três Trap Set e Pass sem gasto.

Para confirmação pelo executável, quatro decks preparados em
`native_hooks/decks/attack-responses/`, com validação offline em
`tests/test_attack_response_decks.py`. O roteiro Windows em
[Native attack response pilot](../../docs/research/native-attack-response-pilot.md)
explica três duelos por cenário, JSONL, estado e gates de integridade.

**Limitações:** sem chains LIFO, Counter Trap cancelando a ativação
de outra Trap, custos/prioridade alternada, imunidade, target invalidation
por efeitos intermediários, repeat attacks, replay de Battle Step,
atribuição de controle alterado, timings nativos ou observação dinâmica
P200/P192. A opção de Negate Attack pertence originalmente ao pack
Kaiba e aparece aqui por ser uma carta da trilogia, não do subconjunto
Joey-only de 350 cards. Até compararmos ações observadas, o status
continua `Done (sandbox)`, não `Verified`.


No piloto de ataque direto da fila, Magic Cylinder e Negate Attack abrem a
resposta após a declaração. O passe retoma o mesmo ataque. A ativação gasta a ocorrência
Set. Magic Cylinder emite um comando separado de dano contra o atacante.
Negate Attack emite um comando de saída da Battle Phase, sem aplicar dano. O cenário exige um
único atacante, zona FIELD vazia e somente cópias elegíveis dessas duas armadilhas nos demais slots. Console: `respond-attack pass` ou
`respond-attack SIDE SLOT`, com posições base zero e `choices` para
consultar a janela. Outros cenários preservam o resolvedor legado.
