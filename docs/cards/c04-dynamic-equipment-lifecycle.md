# C04.5 — Ciclo de vida dos equipamentos dinâmicos

Revisão em 24/11 do SQLite cumulativo v2, quick_check ok, aberto
somente leitura. 0056DD30 ramo 0x290 usa 004027B0 (ATK original)
e 005BE050 para metade, em função do LP do lado do Equip; na
igualdade não substitui. 0x5A9 chama 00569D90(lado,1,0) ×800;
0x5AA chama 0056A320(lado,0,0,1) ×500. Evidência anterior C04.2
permanece válida. Sem nova alteração do cálculo de produção:
o caminho de ocorrência e ciclo de vida foi completado em C04.4.

Testes adicionais exercitam os três dinâmicos e Legendary Sword
simultaneamente, ambos donos, alvos de ambos lados, mão/Set e
ordem inversa. LP muda por Damage/Heal; monstro oculto por
Flip Summon e Destroy; FIELD entra por Set e sai por MST;
retorno de alvo ou destruição de fontes encerra vínculos.
Conferir ATK e DEF separadamente, cada fonte no GY uma vez,
contagem completa 0..5 / 0..6, duas Megamorph do mesmo dono
não duplicam exponencialmente ATK. Não modificar Card.
Não afirmar fidelidade da ordem de links de donos opostos,
supressão ou animação. UI/CPU da fila pertencem ACTIONS.

Promoção de três IDs para Done apenas no escopo headless
do sandbox, condicionada ao CI, sem Verified. C04 continua
Parcial como marco (outros equipamentos ainda pendentes).
