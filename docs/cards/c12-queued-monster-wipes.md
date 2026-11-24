# C12 — Destruição em massa de monstros na fila

Generalização do piloto A02, não um segundo executor. Nove IDs já Done:
335 Dark Hole, 336 Raigeki, 652 Warrior Elimination, 659 Stain Storm,
661 Eradicating Aerosol, 662 Breath of Light, 663 Eternal Draught,
1036 Last Day of Witch, 1037 Exile of the Wicked. Reusar o filtro e
ordem sandbox de MonsterWipeRules.Targets (lado 0, lado 1, slots 0..4),
não afirmar que esta ordem seja a apresentação nativa.

TryQueueMonsterWipe aceita mão ou Set, exige Main Phase, ocorrência
rastreável, zona livre para mão, pelo menos um monstro elegível, e
valida vínculos. ActivateMonsterWipeCommand repete as guardas, reserva
todos os alvos/links e enfileira o gasto antes das destruições.
O lote é cancelado se o gasto não for aplicado. Cada monstro, equipamento
vinculado e consequência já modelada recebe seu comando; FinishFieldRemoval
abre buscas depois do último gate. LastMonsterWipe usa o resource ID real.
TryQueueRaigekiFromHand continua compatível com A04; comando interno
legado conservado. Não migrar seleção de Sangan/Witch nem regras de
imunidade/negação/correntes nesta fatia. UI e CPU seguem legadas.

Testes: nove IDs, dois lados, mão/Set, filtros, gates, origem obsoleta,
Sangan/Witch e vínculo de equipamento. CI pendente. Sem promoção de
cobertura ou nova coleta; evidência P037/P039/P092 já documentada.
