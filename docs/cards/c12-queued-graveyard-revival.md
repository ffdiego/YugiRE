# C12 — Ressurreição GY → Field

P169: Spear Cretin (1116). A remoção por batalha ou efeito
usa o mesmo registro de gatilho. Após uma ocorrência previamente
revelada chegar ao cemitério, o produtor gera duas
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
usa evento MonsterSpecialSummoned. O produtor de batalha enfileirado
guarda o histórico de FLIP no snapshot de remoção e aciona a
ressurreição na chegada ao cemitério após os gates. Cada lado revive
uma ocorrência do próprio cemitério.

Testes cobrem os dois donos e posições, exclusão da fonte, outra cópia,
filtro, slot, request obsoleto, gates e segunda escolha. O App e a CPU
usam a seleção compartilhada no modo padrão. O gatilho e a ordem
em batalha foram testados em C# e aguardam comparação nativa.
