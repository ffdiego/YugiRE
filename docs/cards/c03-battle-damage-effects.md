# C03 — Gatilhos de dano de batalha (P019/P023/P078/P239/P120)

## Fonte e escopo

SQLite privado joey-knowledge-visual-decoded.sqlite (integridade quick_check = ok),
fingerprint do executável db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta read-only a card_identities, card_callbacks, card_callback_slots e
decompilations. Pack confirmado pelo docs/cards/pack-index.json. Não há descrição
DAT no banco; os efeitos abaixo são contratos de domínio a revisar contra o DAT
local, não provas de resolução nativa.

| ID | Carta | Pack | Perfil | Callback primário | Situação |
| --- | --- | --- | --- | --- | --- |
| 113 | White Magical Hat | Kaiba | P019 | 005842E0 | Pending |
| 219 | Masked Sorcerer | Kaiba | P023 | 005844A0 | Done legado |
| 522 | The Bistro Butcher | Kaiba | P078 | 00585D30 | Done legado |
| 1328 | Drill Bug | Joey | P239 | 0058F640 | Pending |
| 1511 | Kycoo the Ghost Destroyer | Joey | P120 | 00588830 | Pending |

A decompilação de 00578540 contém os cinco IDs (0x71, 0xDB, 0x20A, 0x530,
0x5E7), enfileira dano 0x43 antes de solicitar resolução via 005BA490 e retorna
sem efeito com dano zero. Existe comparação dos campos de lado/contexto antes
do despacho; não transformar o layout desses bits em uma ABI C# sem confirmação.
Os callbacks primários específicos podem faltar no índice de decompilação;
o produtor comum não prova a resolução completa. Ver especialmente a
disponibilidade P120 005807F0 e preparação de alvo 00594F00.

## Fatia entregue: intenção sem mutação

BattleDamageEffectRules.AfterBattleDamage recebe um evento já resolvido
de dano de batalha infligido por monstro ao outro lado. Não é um hook de
ChangeLife. A política sandbox existente não abre efeitos após derrota por LP.
As intenções retornadas descrevem uma compra própria (Masked), duas compras
do oponente (Bistro), descarte aleatório do oponente (Hat), busca de Parasite
Paracide (Drill) ou escolha opcional de até dois monstros no cemitério inimigo
(Kycoo). As duas últimas são apenas tipos de intenção: faltam seletor, destino
e gates; não expõem uma resolução incompleta como jogável.

A02 tem PR #67 em andamento e ainda não está integrado na main. Não alterar
DemoDuel nem executar intenções fora da fila. A classe pura é uma entrada
independente para A06/A08; não substituir ainda a resolução legada das duas
compras. Testes C# registrados no runner; execução exige .NET 10.

## Integração a seguir após estabilização de A02

1. Após DamageCommand de batalha aplicado e gate de apresentação, produzir
   intenção uma vez com identidade/controlador do evento original. Não
   disparar em Sparks, cura, ataque cancelado, dano zero ou LP letal.
2. Hat: no momento da resolução, obter mão atual; se vazia, sem descarte.
   Escolher índice com RNG do duelo, imediatamente capturar CardTarget
   (ocorrência, lado, zona Hand) e enfileirar DiscardCommand. Nunca descartar
   pela posição que o índice tinha antes do gate; se ocorrência saiu, deixar
   executor rejeitar/ignorar, sem escolher substituta. Sem novo RNG de sistema.
   Não assumir algoritmo nativo de RNG confirmado.
3. Masked/Bistro: emitir um DrawCommand por carta. Não migrar o legado até
   garantir que a fila seja o único executor e que deck-out encerre o restante.
4. Drill Bug: a regra de domínio indica Parasite Paracide do próprio Deck
   para o topo. Requer seleção e comando de reordenação de Deck, não compra.
5. Kycoo: até dois monstros do GY inimigo (não magia/armadilha); confirmar
   janela opcional, ocorrência e comando de banimento. Efeito contínuo que
   impede banimento dos cemitérios pelo oponente fica como recorte separado.
   Não promover a Done após apenas o gatilho.

## Continuação relacionada: descarte

00575AF0 enfileira 0x8E e trata 0x1CE (462 Electric Snake),
0x215 (533 Minar) e 0x1DE (478 Cockroach Knight). Os dois primeiros
compartilham testes de param_3 bit 0 e lado da ocorrência; Cockroach
passa por outro ramo. 00584E60 contém o caso de Minar com dano 1000.
Não concluir semântica integral de causa/oponente a partir dos bits; mapear
o evento de descarte separadamente de envio ao GY, custo, limite de mão e
redirecionamento de destino. Preservar causalidade no futuro DiscardCommand.
Nenhum novo roteiro Ghidra necessário para a fatia pura; se a integração
exigir paridade do RNG ou causa, pedir somente os callbacks e chamadores
faltantes.

## Aceite da fatia

Testes unitários de intenções dos cinco IDs nos dois lados, gates, ausência de
efeito, escolha aleatória vazia/limites. Não há novo Done/Verified, alteração
no manifest ou mudança na badge. C03 permanece Parcial. Próximo recorte
integrável: P019 quando houver o evento de dano de batalha na fila, seguido
dos dois monstros de Joey. Manter C12 para as duas compras legadas.
