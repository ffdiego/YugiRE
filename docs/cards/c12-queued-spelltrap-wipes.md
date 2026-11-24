# C12 — Destruição de S/T na fila

IDs existentes 671 Harpie's Feather Duster e 1061 Heavy Storm.
Reutiliza SpellTrapWipeRules e DestroySpellTrapCard. Sem nova coleta.
Ordem sandbox legada: adversário, depois controlador (apenas Storm);
slots S/T 0..4 seguidos do FIELD de cada lado. Não confundir com
ordem de apresentação do executável.

TryQueueSpellTrapWipe valida fonte mão/Set, classe, fase e ocorrências
de todos os alvos. Revalida no executor, exclui a fonte Set e reserva
uma sequência de gasto + DestroyCommand por ocorrência. Reaproveita
a proteção de lote da destruição de monstros: gasto inválido cancela
todas as remoções. FIELD usa DuelZone.FieldSpell, slot 0. O helper
de destruição existente movimenta uma carta e desvincula equipamento;
não resolve uma segunda remoção de outra ocorrência. Se o alvo saiu
do campo, não destrói substituto de mesmo ID. LastMonsterWipe não é
alterado por S/T wipe.

Destiny Board e suas mensagens estão no mesmo conjunto de S/T quando
presentes no lado atingido; não é necessária cascata separada nesse
lote. Destruição avulsa fora de lote mantém limite anterior de
equipamentos vinculados. A cascata para efeitos direcionados, resposta
e seleção, UI/CPU e paridade nativa permanecem pendentes.

Testes ambos os lados, mão/Set, ordem, FIELD, remoção de equipamento,
gate, exclusão da fonte e ocorrências obsoletas. Sem promoção de
cobertura. CI pendente.
