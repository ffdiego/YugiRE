# C04.4 — Ciclo completo dos equipamentos fixos

21 IDs já auditados em 00579CE0 (predicados) e 0056FD40
(deltas). Nenhuma regra derivada só do perfil P025.
C04.1/2 já habilitam ativação de mão ou Set e cálculo de bônus.
Esta fatia fecha o contrato de seleção por ocorrência:
QueuedEquipmentCandidates(source) retorna somente alvos rastreados,
face-up e compatíveis nos dois campos. TryQueueEquipment(source,target)
preserva os tokens entre seleção e resolução; coordenadas são wrapper
de compatibilidade. Rejeitar fonte de classe !=22 também no legado,
e limpar idade de Set quando EquipCommand ativa a carta.

Matriz de regressão: 21 IDs × 2 donos × 2 lados de alvo × mão/Set
× 4 saídas (destruir equip, destruir monstro, retornar monstro à mão,
destruir em batalha). Confere delta ATK/DEF, gate, alvo incompatível/
oculto, uma ocorrência de equip no GY do dono, link removido,
restauração de atributos e seleção obsoleta. A batalha usa o fluxo
legado existente; nenhuma nova mutação fora da fila.

Promoção Partial → Done no escopo sandbox após aprovação do CI.
Não promover os três dinâmicos nesta fatia. Não Verified:
supressão geral, transformação de tipo/atributo, proprietário vs
controlador e integração de UI/CPU com a fila são lacunas compartilhadas.
O catálogo já identifica os 21 IDs jogáveis e os filtros fixos.
CI pendente.
