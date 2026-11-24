# C12 — Recrutadores elementais P164

1123 Mystic Tomato (DARK=2), 1108 Giant Rat (EARTH=5),
1117 Shining Angel (LIGHT=1), 1110 UFO Turtle (FIRE=4),
1119 Mother Grizzly (WATER=3), 1120 Flying Kamakiri #1 (WIND=6).
Compartilham callback catalogado 0058AC10. Reusar filtro existente
CardSelectionRules.BattleRecruitAttribute/IsEligible, ATK <=1500.

A batalha legada gera PendingCardSelection após destruir o recrutador
por batalha. Não criar um segundo produtor nem alterar battle pipeline.
A resposta admite apenas P164, Deck→Field, 0..1 seleção, sem batalha
diferida ou outros prompts. Candidatos rastreados por ocorrência,
somente no deck do dono e da solicitação original; ocultar lista
do adversário. Escolha aceita target e slot livre, ou ambos null
para recusar. Executor revalida antes de consumir request.

SpecialSummonCommand é primitiva inicialmente Deck→Field face-up
attack, não valida procedimento específico de cada carta nem cria
janela de resposta indevida. Move a ocorrência e emite o evento
MonsterSpecialSummoned. Reutiliza a continuação de P006 para
ShuffleDeckCommand após transferência aplicada e finalização após
gate; recusa ou falha não consome RNG. Estado/ordem final do deck
equivalentes ao legado com mesma seed; ordem visual não comprovada.
Não há aumento de cobertura nem implementação da batalha na fila.

Testes: seis IDs, ambos os donos, filtros, cópias, slot ocupado,
request obsoleto, recusa, gate, equivalência com seed e dois
recrutadores destruídos juntos. Última carta não provoca deck-out.
UI/CPU, janela de batalha, especiais de GY e múltiplos alvos pendentes.
CI pendente.
