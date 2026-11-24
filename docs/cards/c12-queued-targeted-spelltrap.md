# C12 — Destruição seletiva de S/T

1079 Mystical Space Typhoon e 1004 Remove Trap, ambos já Done no
sandbox. Reutiliza SpellTrapTargetRules.Accepts (0057B530 / 0057B270)
e o executor de destruição S/T/FIELD. MST aceita qualquer S/T ocupada;
Remove Trap somente Trap face-up. Ambos aceitam alvo dos dois lados,
excluindo a própria fonte. MST Set não ativa no mesmo turno; Remove
Trap é Normal Spell. Sem janelas de resposta fora da Main Phase.

QueuedSpellTrapTargetCandidates(source) é consulta sem mutação:
projeta identidade CardTarget, slot visual e carta do catálogo, com
face state. Apenas candidatos rastreáveis. O consumidor pode cancelar
sem alterar o duelo. TryQueueTargetedSpellTrap(source,target) recebe
ocorrências capturadas, não slots de escolha, valida e admite o comando.
Não cria um segundo prompt: A06 deve integrar a seleção bloqueante,
UI/CPU e console. Snapshot bruto é para consumidor autorizado; UI
deve ocultar identidade de cartas setadas do adversário.

ActivateTargetedSpellTrapCommand revalida antes do gasto, reserva
DestroyCommand primário e (se houver Destiny Board ativo e alvo for
peça face-up) os demais membros face-up do lado atingido, em ordem de
slot. Gasto separado. Se gasto falhar, lote cancelado; se o alvo
primário desaparecer após gasto, não destruir substituto nem disparar
cascata. Revalida face state para Remove Trap na destruição. Cada
movimentação aguarda o gate. A cascata só executa se o alvo primário
foi destruído. Sem simulação de correntes ou imunidade.
Sem alterar LastMonsterWipe nem habilitar destruição avulsa genérica.

Testes: ambos lados, seleção sem mutação, equipamento, campo próprio,
Quick-Play Set turn, Remove Trap Set, Destiny Board e mensagens,
alvos obsoletos. CI pendente. Sem promoção de cobertura.
