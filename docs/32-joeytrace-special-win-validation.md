# Validação dinâmica de Exodia e falta de cartas (JoeyTrace)

Três capturas reais adicionais de 2026-11-03: 1.043 lotes de diferenças RAM, 2.845 comandos YDR, três marcadores, zero erros de leitura. Nenhum replay, bloco de RAM nem identificador de save foi publicado. Esta nota contém apenas observações reduzidas e hipóteses.

## Três condições terminais confirmadas

| Sessão | Replay | LP finais lado 0 / lado 1 | Condição terminal | Byte bruto |
|---|---|---|---|---|
| 032345 | exodia2.ydr | 4850 / 4100 | lado 1: 0x000 -> 0x200 em t=576,889s | offset 0x0D4D, 00 -> 02 |
| 034526 | esgota-cartas-2.ydr | 8000 / 5700 | lado 0: 0x000 -> 0x100 em t=358,296s | offset 0x0009, 00 -> 01 |
| 035958 | perdi-por-cartas.ydr | 2800 / 1650 | lado 1: 0x000 -> 0x100 em t=733,703s | offset 0x0D4D, 00 -> 01 |

Cada evento de condição terminal mudou exatamente um byte na amostra, corroborando o campo de estado em 0x00A55D6C + side*0xD44 documentado em docs/23-terminal-conditions.md. Os duelos terminaram sem zerar os LP. Deck-out não exige mão vazia: lado 0 tinha uma carta na mão ao perder em 034526; lado 1 tinha duas em 035958.

## Exodia: padrão repetido com confirmação do bit 0x200

Na sessão 032345, a mão do lado 1 passou de seis para sete cartas em t=576,703s, seq=379, e a marca de Exodia (0x200) apareceu em t=576,889s, seq=380: 186 ms entre observações amostradas. Os cinco últimos comandos YDR são 0x8001, 0x8050, 0x8061, 0x8005, 0x8004. O trio final 0x8061 -> 0x8005 -> 0x8004 repete exatamente o replay de Exodia anteriormente analisado em docs/31-joeytrace-duel-evidence.md.

Os dois novos replays de deck-out não incluem opcode base 0x0005. Hipótese forte, a confirmar no Ghidra: 0x05 participa da apresentação ou do tratamento da vitória por Exodia; não necessariamente é a própria verificação de condição terminal. Esta última é sustentada pelo bit 0x200 e pela função 0x005BBF70 já identificada estaticamente.

## Opcode final 0x8004

Os sete replays com final conhecidos (quatro anteriores e três desta rodada) mostram o primeiro argumento de 0x8004 igual a 0 nas quatro vitórias locais e 1 nas três derrotas locais. As duas novas perdas de deck têm o argumento oposto: lado 0 perde -> arg1=0 (vitória local); lado 1 perde -> arg1=1 (derrota local). O bit alto de 0x8004 não diferencia o perdedor nas três novas amostras. Hipótese: arg1 codifica resultado relativo ao jogador local. Testar empates e condições simultâneas; confirmar handler nativo.

## Marcadores e limitações

- Sessão 032345, t=567,108s: troca de carta de campo, com descarte da anterior relatado pelo operador. Não são lidas zonas de mágicas/armadilhas nem cemitério.
- Sessão 035958, t=602,296s: Shield & Sword troca ATK/DEF. O parser não coleta ATK/DEF efetivos.
- Sessão 035958, t=662,093s: Harpie's Feather Duster destrói monstro anteriormente equipado, segundo operador. O parser não observa equipamentos/associações, portanto o efeito não está confirmado pelos campos disponíveis.

As três capturas tiveram hand_cards_enabled=false, raw_blocks_enabled=false, unknown_only_enabled=false. Logo ainda não há evidência da identidade da última carta de Exodia nem reconstrução de campos ocultos. boundary_candidates=0 é compatível com capturas iniciadas antes da distribuição inicial e não valida a heurística de reinício.

## Próxima rodada

1. Usar o comando python scripts/joeytrace.py capture --hand-cards --verbose para capturar índices internos das cartas na mão e a marca 0x200 durante Exodia.
2. Fazer uma sessão curta com --hand-cards --track-unknown --raw-blocks para investigar carta de campo, Shield & Sword ou equipamentos; guardar os dados em ambiente privado.
3. No Ghidra, confirmar produtor/consumidor de 0x05, handler de 0x04, relação com 0x005BBF70 e 0x005BC050, e o armazenamento de ATK/DEF efetivos e zonas spell/trap.

Observações de RAM, comandos YDR e testemunhos do operador são fontes diferentes; não estabelecer causalidade por alinhamento direto de tempo com o YDR, pois seus comandos não possuem timestamps individuais.
