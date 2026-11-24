# C12 — Ressurreição GY → Field

P169: Spear Cretin (1116). Reusar RemoveMonster e BeginNextQueuedDeckSearch:
após ocorrência previamente revelada chegar ao GY, gerar duas
solicitações obrigatórias, primeiro dono, depois oponente, pulando
lado sem candidatos ou espaço. A solicitação original exclui a
ocorrência disparadora, não todas as cartas do mesmo resource ID.
Não criar gatilho no ato de revelar nem inferir novo comportamento.

QueuedGraveyardRevivalCandidates projeta apenas ocorrências atuais do
GY do dono presentes no request original. TryQueueGraveyardRevival
valida request exato, ocorrência, zona livre, escolha de posição:
ataque face-up ou defesa face-down. Sem recusa para esse efeito.
ResolveGraveyardRevivalCommand revalida antes de consumir request.
Reutiliza a finalização de recuperação de GY; aguarda o gate de
SpecialSummonCommand antes de abrir próxima escolha. Não embaralha.

SpecialSummonCommand admite origem Deck ou Graveyard, do próprio
lado. Preserva ocorrência, limpa histórico de FLIP na colocação e
usa evento MonsterSpecialSummoned. Não altera o produtor de batalha
nem as janelas de resposta, nem implementa ressurreição do GY adversário.
Primitiva de movimentação não substitui regras de admissão específicas.

Testes: ambos donos e posições, exclusão da fonte, outra cópia, filtro,
slot, request obsoleto, gate, segunda escolha, batalha concluída e
primitiva inválida. Sem paridade nativa, sem aumento de cobertura.
CI pendente; UI/CPU ainda legados.
