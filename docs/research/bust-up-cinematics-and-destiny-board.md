# Joey: bust-up, signature summons, Exodia e Destiny Board

Investigação estática de 21/11/2026. Fontes inspecionadas: joey-knowledge.sqlite (6.518 funções + referências + callbacks), ZIP Ghidra visual para 004584C0 e docs/evidence/data.dat-index.json. Nenhuma nova decompilação requisitada. Este documento não afirma paridade pixel-perfect.

## Achado principal: família de bustos separada do campo

O DAT indexado possui 177 itens em j\bust_up\: **17 manifests TXT** e **160 imagens BMP/YGA**, fora de j\duel\ (57 manifests). Reações faciais não são a mesma rotina Draw da invocação comum; o jogo tem um sistema distinto para o busto do personagem.

| Cena | Evidência no joey_pc.exe | Recursos originais |
| --- | --- | --- |
| Reações/expressões curtas | 00402E00, 00403310, 004166F0, 00416A00 | bust_up/reaction01..13.txt; reaction/*_eyes*.bmp, *_kuti*.bmp, *_te*.bmp |
| Invocações signature | 00403310 -> 00414A20 | bust_up/summon.txt, summon2.txt; summon/redeyes/*, summon/kurohyo/* |
| Busto de vitória/derrota/empate | 00415AF0 -> 00415C90 | bust_up/win.txt, lose.txt, draw.txt; win/fukidashi/* |
| Vitória por Exodia | 00409D00 -> 00453B60; Update 00454000, Draw 00454010 | duel/exodia/exodia.txt, miniaturas e exo_* |
| Vitória por Destiny Board (Vija/Ouija) | 00409940 e 00409AB0 -> 004580F0; Draw 004584C0 | duel/effect/vija.txt, vija_skel.bmp, vija_smoke.yga, vija_moji_iro.bmp, vija_moji_mask.bmp, vija_moji_smo.yga |

O dispatcher 00403310 cria o busto signature para IDs de carta 0x51 ou 0x2E8. O SQLite confirma resource ID 81 (0x51) como Red-Eyes B. Dragon. O segundo ID ainda não tem identidade na tabela consultada; não associá-lo a uma carta apenas pelo nome do arquivo. A escolha de summon2.txt ocorre quando o ID é 0x2E8.

## Infraestrutura compartilhada e bloqueio de decisões

Os construtores 00414A20, 00415C90, 00453B60, 004580F0 e 004166F0 chamam 0045CF80 (base de objetos de apresentação) e instalam **vtables diferentes**. Eles compartilham infraestrutura de vida útil/agendamento, mas não o mesmo Draw nem a mesma composição de sprites.

Há evidência concreta de estados de espera:
- 00414A20 ativa uma flag em DAT_005F3400+5, escreve modo 6 em +8, contador 0x20 em +0x10 e vincula um objeto de apresentação em +0x28 quando o global está disponível.
- 00415C90 e 004166F0 usam o mesmo global com modo 3 em alguns caminhos. 00453B60 (Exodia) também acessa DAT_005F3400.
- O estado 6 de 00409D00 percorre a lista de objetos do duel manager (DAT_005F3280+0x132c/0x1330), aguardando sua conclusão. 00409940 e 00409AB0 realizam esperas semelhantes.

Isso é **compatível com a observação de que uma cena impede novas decisões**. Ainda não prova que o global seja especificamente um input lock: faltam o contrato de mensagens Windows e o rastreio completo da fila.

Proposta para Joey.App/Joey.LookAndFeel (ainda não implementada): evento do Core -> cue tipada (Reaction, SignatureSummon, Exodia, DestinyBoard, DuelResult) -> fila visual com contador e prioridade -> aguardar conclusão sem avançar ações novas do Core -> liberar input e próxima decisão. Não copiar o scheduler nativo nem bloquear cálculos de regras depois de já confirmados.

## Destiny Board: cinco cartas F-I-N-A-L, diferentes de Exodia

Identidades **confirmadas no SQLite** (resource ID / índice interno / classe / perfil):

| Carta | Resource ID | Índice | Tipo | Perfil |
| --- | ---: | ---: | --- | --- |
| Destiny Board | 1528 | 68 | Trap (21) | P259 |
| Spirit Message "I" | 1541 | 371 | Spell (22) | P009 |
| Spirit Message "N" | 1542 | 373 | Spell (22) | P009 |
| Spirit Message "A" | 1543 | 374 | Spell (22) | P009 |
| Spirit Message "L" | 1544 | 372 | Spell (22) | P009 |

Callback primário Destiny Board: 00590D80. Primário das quatro Spirit Messages: 00583F30 (disponibilidade 00547CE0). Os entrypoints primários não possuem decompilação independente na tabela functions; não inferir regras internas a partir dos nomes.

00409940 instancia 004580F0 em um modo. 00409AB0 percorre até cinco posições de carta nas zonas do campo e instancia 004580F0 em outro modo conforme correspondências da tabela de cartas. Ambos passam por etapa de espera pelos objetos. O construtor 004580F0 carrega duel/effect/vija.txt; seu Draw transitório 004584C0 incrementa o contador em **4 por chamada**, aplica deslocamentos e composição/mascaras do manifesto. O nome Vija corresponde ao sistema Ouija/Destiny Board na evidência; **não** à rotina Exodia 00454010.

A condição geral do TCG é ter Destiny Board mais as quatro Spirit Messages no **campo**. Exodia requer cinco peças na **mão**. Fonte pública da regra, não prova da edição de 2004: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5224&ope=2&request_locale=en

## Evidência faltante e roteiro de teste posterior

1. Invocar um monstro comum versus Red-Eyes B. Dragon, do mesmo lado/posição, observando que manifestação do busto e quando a janela bloqueia inputs. Buscar outro exemplo para o caminho summon2.txt.
2. Capturar mudanças de expressão após ataque, perda de LP, compra e efeitos, para mapear reactionXX.txt aos tipos de decisão/resultado; não atribuir nomes com base no número do arquivo.
3. Comparar o duelo encerrado por LP, Exodia e Destiny Board F-I-N-A-L separadamente. Registrar ordem de bust_up/win, banner do campo, animação especial, som e liberação de input.
4. Na máquina com o DAT original, rodar o inspetor e consultar o conteúdo de bust_up/summon.txt, summon2.txt, reaction*.txt, win.txt e duel/effect/vija.txt; só então escolher atlas, coordenadas e quadros para porta-los.

## Mudanças desta pesquisa

--inspect-visual-dat agora examina **j\duel\ e j\bust_up\**, totalizando **74 manifests** no índice original e incluindo metadados das 160 imagens de busto. O JSON é gerado localmente em scratch/visual-review/dat-manifests.json. Não exporta pixels, EXE ou DAT nem faz upload automático. NativeVisualArchiveScope é um predicado de descoberta com testes, **não é um renderer**.

Cobertura Draw permanece **19/31 (61,29%)**, 0 Verified.

## Implementação posterior: cue tipada e gate sem pausa invisível

O contrato `NativeCinematicCue` e a política `NativeCinematicRouting`
estão documentados em [gates de cenas](../architecture/native-cinematic-gates.md).
A Exodia usa a mesma fila `PresentationSequence` e preserva os 79 updates
já conhecidos. Invocações normais de Red-Eyes (resource ID 81) e do
recurso 744 passam a emitir um **candidato de cut-in** observável no
JSON do F12. Como as respectivas durações e renderizadores não foram
reconstruídos, **nenhum dos dois interrompe ou atrasa o duelo**. O bust-up não integra o denominador atual das 31 funções visualmente inventariadas e Vija continua Pending.

## Implementação subsequente: vitória F-I-N-A-L no sandbox

`DestinyBoardRules` reutiliza os resource IDs 1528 e 1541–1544,
sem converter os índices internos do SQLite em IDs. `DemoDuel`
pode ativar Destiny Board após seu turno de Set, colocar
uma Spirit Message da mão ou deck em cada End Phase
adversária e encerrar o duelo quando as cinco cartas
estão simultaneamente face-up no campo. A condição é
independente dos LP e da vitória por Exodia na mão.

Esta etapa é **sandbox jogável, não paridade Joey original**:
resposta de Trap no turno adversário, remoção conjunta,
composição visual `004584C0`, áudio e IA continuam abertos.
O `NativeCinematicRouting` identifica uma intenção Vija,
sem reservá-la na fila até que seu Draw tenha duração e
sprites validados. Ver [limites/testes da implementação]
(../cards/destiny-board-sandbox.md). A cobertura Draw
permanece **19/31**, zero `Verified`.


## Complemento V31/V18 (24/11)

Roteamento de dano 00403900 e som-only/bust-up 00402E00, duração 00416B80 e índice 005C0F00 isolados em NativeJoeyReaction. Ver [contrato e roteiro 27](native-joey-reaction-audio-2026-11-24.md). Sem renderer nem áudio real; não bloquear fila com parâmetro de duração isolado.
