# Destiny Board / F-I-N-A-L — fatia jogável do sandbox

Data da implementação: 21/11/2026. A regra é uma **aproximação
limitada e testável** da mecânica de Destiny Board, não paridade
nativa confirmada do Joey de 2004. Não utilizar a porcentagem de
Draw como prova de jogabilidade de todos os efeitos associados.

## Identidades confirmadas

O SQLite global do `joey_pc.exe` contém:

| Carta | Resource ID | Índice interno | Tipo |
| --- | ---: | ---: | --- |
| Destiny Board | 1528 | 68 | Trap |
| Spirit Message "I" | 1541 | 371 | Spell |
| Spirit Message "N" | 1542 | 373 | Spell |
| Spirit Message "A" | 1543 | 374 | Spell |
| Spirit Message "L" | 1544 | 372 | Spell |

Não confundir índices internos com os IDs usados pelo Core.

## Comportamento implementado no Core

`DestinyBoardRules` verifica a existência de **cinco cartas
distintas face-up nas cinco zonas de magia/armadilha do
mesmo jogador**. Cartas na mão, deck, cemitério ou
face-down não cumprem o requisito. Diferente de Exodia:
as peças estão no **campo**, não na mão.

1. Destiny Board pode ser **Set** como Trap. O sandbox
   só permite ativá-la durante a **Main Phase própria
   de um turno posterior ao Set** (uma limitação da
   implementação de traps, não o timing completo nativo).
2. No encerramento do **turno do adversário**, se Board
   estiver face-up, o motor tenta colocar **uma** próxima
   Spirit Message, seguindo I → N → A → L, na primeira
   zona livre. Procura primeiro na mão do dono e depois
   no deck; usa resource ID, não nome.
3. Se não houver zona, carta correspondente na mão/deck
   ou a sequência atual estiver inconsistente, **não
   consome** outra carta e não gera vitória.
4. Ao colocar L com as quatro mensagens e Board face-up,
   `DestinyBoardWinner` identifica o vencedor,
   `DestinyBoardCompleted` emite os cinco snapshots,
   `Winner` encerra imediatamente o duelo e o
   turno **não avança**. LP permanecem inalterados.
5. `DestinyBoardMessagePlaced` comunica cada
   colocação (jogador, zona, carta, origem mão/deck)
   para futuras animações. Ele não representa uma
   transferência para o cemitério nem toca o RNG.
6. Os quatro `Spirit Message` não podem ser
   baixados manualmente da mão fora do efeito.

O efeito é calculado **depois** de descartes obrigatórios
do limite de mão, no próprio ponto em que o sandbox
encerraria o turno. Não reorganiza as fases ou a
estrutura de filas do `Joey.Core`.

## Cobertura e limites

`DestinyBoardChecks` verifica identificação de cinco
IDs em ambas as perspectivas, set-turn, quatro End Phases
adversárias, contagens e ordem do deck, prioridade da
mão, face-down, sem vaga, sem carta e bloqueio após
vitória. O renderer do YugiRE ainda mostra a vitória
através do banner genérico já implementado, não por
uma cena especial inventada.

**Ainda falta:**

- Ativação de Trap no turno adversário, *chain windows*
  e prioridades de resposta. A interface atual só oferece
  ativação durante a Main Phase do controlador.
- Regras de destruição/remoção conjunta de Destiny Board
  e suas mensagens quando qualquer parte sai do campo;
  não há sistema genérico para todos os tipos de remoção.
- Confirmar procura por carta da mão/deck, instante do
  End Phase, condições de interrupção e efeitos de
  múltiplas cópias contra o executável original.
- Composição visual `004584C0` e `vija.txt`, sons,
  reações do Joey e sincronização da cena final.
- A IA atualmente não seleciona deliberadamente
  essa sequência; ela permanece como caso jogável
  do jogador/sandbox até melhorar a política de traps.

`NativeCinematicRouting.DestinyBoardVictory` registra
a intenção de cena `j/duel/effect/vija.txt`, mas
`ImplementedUpdates` retorna `null`: o cliente não
reserva uma pausa invisível. O JSON de F12 contém
`destinyBoardWinner`, `destinyBoardManifest`
e os IDs de magia/armadilha face-up dos dois lados.

A família Draw **Vija `004584C0`** permanece
`Pending`; não elevar a cobertura visual (19/31,
61,29%; zero Verified) por completar as regras.
