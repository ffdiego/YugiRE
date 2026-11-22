# Roteamento de cinemáticas e gates de apresentação — 21/11/2026

## Por que existe

Análise do SQLite privado `joey-knowledge.sqlite`, arquivado em
`joey-knowledge(1).zip`, mostra que as expressões, invocações
marcantes e vitórias usam objetos nativos de apresentação distintos,
mas compartilham a base `0045CF80` e têm esperas de conclusão.
O objetivo da arquitetura do YugiRE **não** é copiar os objetos ou
vtables, e sim reproduzir sua ordenação e impedir decisões apenas
durante cenas que realmente possam ser desenhadas.

Fontes reabertas para esta mudança:

- `00403310` testa os IDs **0x51 (81)** e **0x2E8 (744)** e,
  após checar lado/estado, constrói `00414A20`. O argumento
  `0` escolhe `j/bust_up/summon.txt`; `1` escolhe
  `j/bust_up/summon2.txt`. O SQLite associa resource ID **81**
  a Red-Eyes B. Dragon; **não identifica 744** na tabela de
  identidades. Portanto os dois IDs são *candidatos* a cut-in,
  não prova de que toda invocação desses monstros sempre mostre
  a cena independentemente de lado/estado.
- `00414A20` inicializa objeto com vtable `005DA7F0`,
  carrega o manifesto e altera o global `DAT_005F3400`
  (flag `+5`, modo `+8=6`, contador `+0x10=32`)
  quando as condições do compositor permitem.
- `004166F0` e `00415C90` compartilham a base e têm
  caminhos equivalentes para outros modos. O estado 6 de
  `00409D00` aguarda os objetos listados no gerenciador do
  duelo antes de sair do estado de vitória por Exodia.
  Isso é evidência de *espera visual*, **não é** prova
  completa do protocolo de input do Win32.
- A primeira janela de Exodia `00454000/00454010`
  já foi portada em `NativeExodiaOpeningFrame`:
  **79 updates** de 0 a 312, e é a única cena desta
  família com renderizador parcial utilizável no cliente.

## Implementado agora

`Joey.LookAndFeel.NativeCinematicCue` é o contrato tipado
de `Reaction`, `SignatureSummon`, `ExodiaOpening`,
`DestinyBoard` e `DuelOutcome`. Sua função de roteamento
`NativeCinematicRouting` resolve *candidatos* 81/744
aos manifests corretos. `ImplementedUpdates` retorna
**79 somente para ExodiaOpening com dados válidos** e
`null` para todas as outras cenas.

`Joey.Core.DemoDuel` emite `MonsterNormalSummoned`
somente depois de uma invocação normal válida ou de um
Set feito pela operação `Summon`; preserva jogador,
zona, carta e `FaceDown`. Ele **não** dispara em FLIP
Summon ou invocações especiais: há um evento próprio
`MonsterSpecialSummoned` para estas últimas.

`Joey.App` consome o evento normal no ponto
`ResetCardEffects`, registra o **último candidato**
signature e não o toca ainda. F12 inclui no JSON
`lastSignatureCandidateResourceId`,
`lastSignatureCandidateManifest` e
`signatureCinematicRendered=false`. Se não há
candidato, os dois primeiros campos são `null`.
Isto é **instrumentação para diagnóstico**, não
reação visual ao monstro.

A cena Exodia foi migrada do registro genérico
`ExodiaOpening` para `CinematicScene(Cue, Payload)`,
enfileirada na **mesma `PresentationSequence<CardEffect>`**
que compras, descartes e outros efeitos. `MotionBusy`
já espera por esta fila enquanto o objeto tem quadros;
o resultado genérico permanece escondido até a saída
da cena orbital. Não criamos um relógio paralelo nem
avançamos o Core ao aguardar o fim de um efeito.

Uma cena cujo `ImplementedUpdates` é `null`
**não é enfileirada, não desenha substitutos e não bloqueia
input**. Essa regra é indispensável para que um Red-Eyes
não cause uma pausa invisível enquanto o formato do
`summon.txt` e os loops da sua vtable ainda
não estiverem recuperados.

## Testes de regressão

`LookAndFeelChecks` cobre IDs, manifests, face-down,
classificação, timer 79/sem timer e fluxo na fila após a
última transferência da mão. `NormalSummonCinematicChecks`
cobre pagamento inválido, Red-Eyes com dois tributos,
Set face-down e o lado da CPU.
`MorphingJar2Checks` confirma que as invocações especiais
por escavação **não** são confundidas com invocações normais
e não geram candidato de cut-in.

## O que falta para renderizar o busto do Joey

1. Exportar **apenas metadados/manifests** do DAT pelo
   `--inspect-visual-dat`, já estendido para `j/bust_up`.
   Revisar `summon.txt`, `summon2.txt` e sprites de
   `redeyes`/outros sem supor sua ordem.
2. Recuperar Update/Draw da vtable signature `005DA7F0`
   (entradas além do destrutor `00414BB0`). Os offsets
   exatos não constam como funções independentes no SQLite,
   tal como ocorreu com outros Draws do inventário.
3. Comparar tempo, origem, dimensões, compositing e
   perspectiva no Joey original para invocação normal
   comum versus Red-Eyes, incluindo bloqueio de clique.
4. Só então alterar `ImplementedUpdates` e acrescentar
   o Draw com imagens originais; manter a condição de
   `faceDown` e o gate de perspectiva como hipóteses
   do cliente até a captura nativa.
5. Mapear `reactionXX.txt`, `win.txt` e `vija.txt`
   separadamente: compartilham o scheduler, mas **não**
   necessariamente a mesma timeline ou canvas.

**Métrica visual:** inalterada em **19/31 (61,29%)**,
0 `Verified`; esta PR muda roteamento/eventos/agendamento
de Exodia já parcial, não porta um novo Draw.
