# Consolidação de regras — lotes 6–14

Integra os PRs #53–#61 sem descartar a linha visual da main (66f66a0).
Os PRs #55/#56 já são ancestrais de #57; #58–#61 formam a pilha
posterior. #53 (Dream Clown) e #54 (Bubonic Vermin) são independentes
e foram reconciliados com o seletor e a supressão atuais.

## Resultado do inventário
- Trilogia: 383/771 (49.68%) Done+Verified; 9 Partial; 0 Verified.
- Joey: 119/350 (34.00%) Done+Verified.
- Dream Clown, Bubonic Vermin, Greenkappa, Tornado Bird, Lady Assailant
  of Flames, Fire Sorcerer, The Immortal of Thunder e Dimensional Warrior:
  Done no escopo sandbox. Royal Command e Dark Necrofear: Partial.
  Prohibition: Pending; não implementar por aproximação.

## Integração
- Trigger de posição separado de FLIP: Royal Command não cancela Dream Clown.
- Bubonic Vermin passa pelo gate inicial e tardio de Royal Command.
- Seleção FLIP e continuação de batalha/banimento preservam identidade
  dos combatentes. Regressões em CardConsolidationChecks.
- A main já consolidou as quatro coletas visuais 01–04. O README de
  pendências preserva esse estado e agrega somente 05–10 ainda não executados.
- O plano histórico mass96, decks e seeds da main são preservados:
  doneSandbox ali é observação histórica, não status corrente.
  O manifesto é a fonte do progresso atual.

## Limites
Sem nova comparação dinâmica no original. Não promover a Verified.
Necrofear ainda sem UI/CPU e sem controle; falta separar owner/controller,
causa e destino de saída, End Phase e vínculos. Royal Command ainda sem
cadeia completa. A próxima investigação pode ser retomada a partir
dos contratos e scripts em scripts/pending.
