# C12 — FLIPs de campo
Greenkappa (585/P085), Tornado Bird (1521/P255) e Four-Starred
Ladybug of Doom (1338) já implementados no sandbox. Sem aumento de cobertura.
Par obrigatório usa solicitação exata, duas ocorrências distintas, revalidação
da face state e vínculo antes da admissão e no executor. Não misturar seleção
parcial legada. Royal Command pode suprimir o pedido antes da resolução.
Greenkappa destrói somente Set, Tornado retorna qualquer S/T/FIELD elegível,
limpando vínculo sem enviar equipamento ao GY. O retorno de Destiny Board
ativo reserva destruição das demais peças face-up não selecionadas depois
dos dois retornos, somente se Board realmente retornou. A primitiva S/T
de ReturnToHandCommand permanece restrita a esse lote, não habilita
retorno genérico sem tratamento de vínculo. Alterações externas entre
comandos podem invalidar uma ocorrência sem desfazer a anterior.
Ladybug usa LevelFourOpponentTargets e reserva DestroyCommand e vínculos,
sem gasto de carta, reutiliza FinishRemoval e FinishFlipBatch. Gatilhos
após o lote, não entre destruições. Usa Level DAT, não level dinâmico.
Testes de ambos lados, FIELD, filtros, identidade, gate, equipamento e
Sangan. A batalha diferida permanece legada. CI pendente; nenhuma
afirmação de timing nativo. Coleta 05 continua pendente.
