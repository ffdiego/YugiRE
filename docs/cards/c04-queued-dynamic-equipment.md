# C04.2 — EquipCommand e bônus dependentes do estado

SQLite joey-knowledge-visual-decoded.sqlite, PRAGMA quick_check = ok, consulta
somente leitura. IDs 656, 1449, 1450, 1046 e 1060 conferidos no catálogo.
VA 00579CE0 retorna verdadeiro para os três primeiros sem filtro específico
além de monstro ocupado face-up. 0056DD30 case 0x290 substitui ATK base
quando LP do lado do equipamento diverge, com 005BE050 no ramo de metade.
Em igualdade não substitui. 0x5A9 usa 00569D90(ladoEquip,1,0): cinco
slots de monstro ocupados, bit 2 presente (face-up), sem filtro de posição.
0x5AA usa 0056A320(ladoEquip,0,0,1): seis slots 5..10 ocupados,
incluindo set e FIELD, sem teste de face. Bônus respectivos 800 e 500
para ATK e DEF. Ambos contam o controlador do equipamento.
Stim-Pack e Germ Infection permanecem fora: contador flags & 0x3C >> 2,
ordem de atualização ainda não fechada.

Implementação: EquipCommand conserva ocorrência da fonte e do alvo,
lado controlador e slot de S/T. Admissão sem mutação, execução revalida
fase, fonte, destino, elegibilidade e identidade. Suporta mão e Set para
21 fixos + 3 dinâmicos. DestroyCommand já remove vínculo e equipamento.
Atributos recalculados sem cache e sem alterar Card. Megamorph altera
working base ATK, não a soma de deltas; múltiplas cópias seguem ordem
de vínculos sandbox, sem alegar paridade da ordem nativa. LP não limitados
ao ushort no sandbox; sem interações globais de supressão/controle.
Campo/mão/LP em testes de headless. Nenhum novo efeito resolve fora da fila.

UI/CPU legados permanecem nos 21 fixos; os 3 novos só são admitidos
por TryQueueEquipment/EquipCommand até migração de entrada/presentation
gate da aplicação. Não marcar Done. Nenhum hook ou coleta externa
necessário. Testes registrados, CI pendente nesta sessão.
