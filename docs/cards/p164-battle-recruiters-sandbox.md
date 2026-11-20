# P164 — seis recrutadores por atributo (sandbox)

Branch empilhada em P006 (e, indiretamente, P018), sem alterar `main`.
O catálogo nativo em `docs/evidence/card-callback-catalog.json` associa os seis
recruiters ao callback primário `0x0058AC10` (P164).
Os filtros abaixo são a interpretação histórica da regra, **não**
uma equivalência nativa verificada para essa build.

| Carta | ID | Atributo DAT | Seleção |
|---|---:|---:|---|
| Mystic Tomato | 1123 | DARK=2 | Monstro do Main Deck com ATK ≤1500 |
| Giant Rat | 1108 | EARTH=5 | Idem, atributo EARTH |
| Shining Angel | 1117 | LIGHT=1 | Idem, atributo LIGHT |
| UFO Turtle | 1110 | FIRE=4 | Idem, atributo FIRE |
| Mother Grizzly | 1119 | WATER=3 | Idem, atributo WATER |
| Flying Kamakiri #1 | 1120 | WIND=6 | Idem, atributo WIND |

Gatilho: monstro enviado ao cemitério **por destruição em batalha**.
Não ativa em tributo, destruição por efeito, retorno à mão ou descarte.
`FinishDeferredBattle` preserva a causa da destruição ao chamar
`RemoveMonster(..., destroyedByBattle:true)`; esse evento é registrado na
mesma fila de efeitos de P006. As filas são drenadas uma decisão por vez.

`CardSelectionRules` calcula candidatos por `Card.Properties >> 29` e
ATK base (não inclui bonus de campo/equipamento). `PendingCardSelection`
marca destino `Field` e `MinSelections=0`, por ser opcional.
A UI provisória agora oferece **Skip** só nesses casos.
O Core aceita uma posição livre explícita ou usa a primeira livre por
política do sandbox; todos os recrutados entram em ataque face-up.
Escolher consome somente a ocorrência selecionada, embaralha o restante do
Main Deck por seed de teste e não gasta a invocação normal. Skip não embaralha.

Cobertura: as seis combinações de atributo, limite ATK inclusivo, invalidez
de filtro, posição inválida, ausência de candidato, espaço livre após
destruição, invocação, skip, destruição não-batalha, tributo e dois
recrutadores destruídos simultaneamente. Arquivo:
`src/tests/Joey.Tests/BattleRecruiterChecks.cs`.

**Limites:** sem chain/negação, troca de controlador/proprietário,
banimento substituindo destino, restrições intrínsecas de invocação especial,
interação completa com game over, paridade de RNG ou interface e tempos
nativos. P164 não recebeu `Verified` até capturas diferenciais.
