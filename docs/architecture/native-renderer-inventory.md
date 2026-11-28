# Inventário do renderizador nativo do duelo

Alvo: `joey_pc.exe`, SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços em VA. O inventário cobre as famílias encontradas no cadastro de
objetos do duelo e na busca de manifests. Uma célula aberta indica que a
rotina ainda não foi atribuída, e não ausência de Update ou Draw.

## Método e composição

- Os objetos são registrados por `0045CF80 → 0045D4F0` e removidos
  por `0045CFE0`. `0045D040` e `0045D170` filtram por
  `obj+04h` ativo e interseção de `obj+08h` com a máscara da cena.
  Ordenam por `obj+0Ch` crescente via `0045D150`, depois chamam
  vtable `+08h` e `+0Ch`. O desempate não é comprovado.
- Os manifests preservam ordem. `005498B0` lê nome e inteiro auxiliar,
  `00481D00` resolve idioma e `00481AD0` carrega sob demanda.
  `00496CA0` seleciona o loader e `00497360` lê YGA, passando pelo
  LZSS `0054A000` quando necessário. O parâmetro inteiro do manifesto
  só ganha semântica no consumidor.
- Para rerodar os pontos centrais use `scripts/run-archive-query.sh`
  com `0045d170 004511d0 00456730 ins:004514b0:122 ref:005db324`.
  As associações das famílias vieram dos Xrefs aos manifests e da
  leitura dos slots `+8/+C` das tabelas instaladas pelos construtores.
  Para as regras de retomada, consulte o [scheduler](../research/native-action-scheduler.md).
- **Fiel e validado** exige comparação do elemento com o original em
  estado equivalente. Comparar uma textura com o DAT valida a seleção,
  não valida a composição inteira. Nenhuma família inteira recebe
  esse estado nesta rodada. Para geometria já medida, ver
  [contrato visual](native-visual-contract.md) e [cartas](native-card-rendering.md).
  Resultados de base também constam de *rendering-architecture*,
  *animation-formats* e *activation-effect* em `analise-visual`.

## Objetos estruturais e HUD

| Elemento | Construtor | Update | Draw | Recursos DAT | Estado |
| --- | --- | --- | --- | --- | --- |
| Campo e troca de cenário | `00449330` / VT `005DB060` | `00449830` | `00449850` | `field/field.txt`, `fie_*.bmp`, variantes YGA | Implementado com divergências. `DrawDuel` usa o campo estável em (230,0); troca temporizada de cenário em aberto. |
| Painel de detalhes | `0041BE80` / `005DA9F8` | `0041C6D0` | `0046F7A0` | `detail/detail.txt`, `detail.bmp`, `line.bmp`, `scroll_bar.bmp` | Parcial: removeu overlays raylib sobre o fundo nativo; nome, ATK/DEF e descrição usam fonte GDI inglesa compatível (Courier New do SO), cores COLORREF e viewport x8..184, y365..526. Raster, marcação rica e geometria do scrollbar pendentes. Ver `docs/research/native-detail-sidebar-2026-11-21.md`. |
| Cartas e mão | `004182E0` / `005DA868` | `00418B60`; hover `0045C8B0` | `00418EF0` | `card/card.txt`, molduras, arte e versos | Implementado com divergências. `DrawDuelField`, `DrawDuelCard`, `DrawFieldCard`, `DrawDuelHand`, `DrawCpuHand`, `DrawArtwork`, `DrawCardBack`. Centros, ângulos e face separados; a combinação da arte, clipping e escala em trânsito ainda difere. |
| Deck, cemitério e pilhas | `0044C730` / `005DAC94` | `0043C870` | `0043C980` | `card/deck.txt`, BMPs de deck/sombra/grave | Implementado com divergências. `DrawSidePiles` e `DrawPile` usam o verso e texto do cliente. A vtable também aparece no caminho `0043D090`. |
| Atributos ATK/DEF | criado por `004182E0`, recurso em `005F33EC` | Suavização no Draw | `00418EF0 → 004194D0 → 00480500 / 00480300` | `font/number.txt` e tiras de números | Parcial: atlases, tons por posição, tween 7/8 e gate nativo de zona/face-up/identidade/estado estável; números seguem carta/seleção e precedem status `00419B70/0041A3B0/0041A070`. F12 não expõe inimigo antes do impacto. Ainda pendem blend, âncoras e overlays de status. |
| LP, dano e placar | `0044C3E0` carrega LP; `0047C310` cria objetos de delta (`005DBAA4`) | `0047C560` | `0047C660` | `life/lifepoint.txt`, `lp_jibu1/2.bmp`, `lp_aite1/2.bmp`, `font/number.txt`, `damage00/01.bmp` | Parcial: contador 100/10/1, barras texturizadas, dígitos do DAT quando manifest compreendido e animação flutuante de dano/cura via `004806F0`. Fontes, posição, máscara, mistura, som e frames pareados não confirmados. |
| Fases, botões e troca de cor | `00481040` / `005DBB2C` | `00481370` | `00481420` | `phase/phase.txt`, `pha_s/b/r_*.bmp` | Implementado com divergências. `DrawPhasePanel`: recortes centrais do giro foram comparados com o DAT nos dois sentidos. Hitbox e detalhes do estado ativo ainda não foram validados contra o Joey. |
| Avisos de fase e novo jogador | `00480B40 / 00480BC0` inicializam estados | `00480C20` | `00480C60` | `phase_name/phase_name.txt`, banners e `nex_*.yga` | Implementado com divergências. `DrawPhaseNotice` seleciona o banner azul/vermelho por lado. Os sete quadros intermediários de batalha também vêm do DAT. Entrada e saída seguem os deslocamentos e a opacidade do Draw. Mistura do meio e aviso de jogador continuam abertos. |
| Seleção nativa de cartas | `0043A060` / `005DABD8` | `0043B0F0` | `0043B280` | `list/selectlist*.txt`, `gravelist*.txt`, `fusionlist*.txt`, `excludelist*.txt`, `list_icon/list_icon.txt` | **Pending:** lista de cinco cartões, navegação e renderização de transição; a UI provisória do cliente usa dez cartões em duas filas. Vtable confirmada no lote 22/11; a decompilação tem warning de tipagem. Ver `docs/research/visual-virtual-methods-2026-11-22.md`. |
| Diálogos e seleções | `00487E70` (manifesto) | Aberto | Aberto | `dialog/dialog.txt`, `button_?.txt`, `ritualdlg.txt` | Placeholder/procedural inventado. `DrawCardSelectionOverlay`, `DrawSummonResponseOverlay`, `DrawAttackResponseOverlay` e `DrawEffectDecisionOverlay` usam painéis de gameplay do cliente. |
| Ajuda | `0045E250` / `005DB630` | `0045E3D0` | `0045E4A0` | `help/help.txt`, YGAs por idioma | Não implementado. |
| Efeito de estado persistente | `0040DAC0` / `005DA44C` | `0040E010` | `0040E260` | `effect/affect.txt` | Parcial no duelo: vínculos de equipamentos, campo, aura e restrições modelados; scratch DAT com máscara aproximada, somente extremos face-up. Demais relações e compositor nativo pendentes. |

## Objetos de efeito e eventos

Nos recursos abaixo, caminhos sem prefixo são relativos a `j/duel/`.
Em famílias recém-identificadas, o par Update/Draw é o slot virtual,
sem inferir todos os argumentos ou o comportamento de cada fase.

| Elemento | Construtor | Update | Draw | Recursos DAT | Estado |
| --- | --- | --- | --- | --- | --- |
| Combate declarado e espada | `00413BC0` / `005DA710` | `00414110` | `004142C0` | `effect/attack.txt`, `atk_icon.bmp`, `atk_icon2.bmp` | Implementado com divergências. Ícones de elegibilidade e mira usam o centro da zona e escala unitária. `DrawAttackCue` usa o deslocamento, lâmina parcial e impacto do estado 4. Ramos alternativos e mistura de raster ficam em aberto. Comando 31 via `00407A20 → 00413BA0`. |
| Explosão de destruição | `004511D0` / `005DB324` | `00456730` | `004514B0` | `effect/explosion.txt`, sete `expl*.yga` | Implementado com divergências. `DrawDestruction` usa os recursos e passes nativos. Composição raylib e retenção da carta ainda requerem comparação com o original. Comando 7C via `00405190`; outra chamada em `00405620`. |
| Quebra / remoção alternativa | `00450C30` / `005DB314` | `00450FB0` | `00450FD0` | `effect/break.txt` | Não implementado como família separada. Distinção entre quebra e explosão ainda precisa de mapa de comandos. |
| Revelação e deslocamento | `004182E0` / `005DA868` | `00418B60` | `00418EF0` | `card/card.txt` e arte/verso | Implementado com divergências. `DrawFlight` e `DrawHandTransfer` usam a interpolação de oito passos onde integrada. O FLIP e a troca animada de face ainda não reproduzem todos os passes. |
| Set | `0044EFD0` / `005DB2C4` | `0044F370` | `0044F390` | `effect/set.txt`, `eff_set_b/y.bmp`, `eff_star0/1_b/y.bmp` | Implementado parcialmente: sprite central, oito partículas radiais do DAT, contador, escala e fade nativos. Seleção de variantes inferida; RNG, scratch, blending e frames pareados ainda pendentes. |
| Invocação normal | `0044F630` / `005DB2D4` | `0044F9F0` | `0044FA10` | `effect/summon.txt`, `mini/list_card.txt`, `mini/*.bmp` | Implementado parcialmente: miniatura DAT, duas janelas nativas, 33 updates e fila após o voo da carta (jogador e CPU); composição, recorte, lado e alpha ainda exigem capturas pareadas. |
| Invocação especial | `0044FCC0` / `005DB2E4` | `004500E0` | `00450100` | `effect/sp_summon.txt`, `sp_sum_a1..4.yga`, `sp_sum_d1..4.yga` | Implementado parcialmente: 41 updates, quatro YGAs alternados no início, imagem da carta transformada na segunda janela e eventos para buscas especiais/Morphing Jar #2. Orientação, textura interna e transformador `0054E890` provisórios. |
| Ativação no campo | `00450260` / `005DB2F4` | `00450590` | `004505D0` | `effect/activate.txt`, pares `kouka_ue/sita_*` | Implementado com divergências. `DrawSpellActivation` e `DrawActivationSprite` usam dois YGAs e scratch dinâmico, que também existe no original. Mistura e seleção por categoria ainda divergem. Comando 72, handler `00406B20`. |
| Ativação alternativa | `00450730` / `005DB304` | `00450A90` | `00450AD0` | seleção aberta | Não implementado como ramo separado. |
| Abertura | `004516A0` / `005DB334` | `00451950` | `00451980` | `effect/duelstart.txt`, `duel_start_e.bmp` | Parcial: imagem original DAT centralizada em (400,300), 40 updates, escala inicial + cosseno e fade-in/hold/fade-out. Ordenação antes da mão inicial é política da Alpha. |
| Resultado do duelo | `00451B80` / `005DB344` | `00451E40` | `00451E60` | `effect/duelend.txt`, `duel_win/lose/draw_e.yga` | Parcial: substituiu painel do cliente pelos YGAs de resultado originais, centralizados em (400,300), com fade-in da função nativa; após t=512 mantém banner (não presume fade-out inatingível no Update). |
| Saída do banner de resultado | `00483B10` / `005DBC48` | `004481F0` (retorno imediato) | `00444490` | `effect/duelend.txt`, inclusive `aborted_?.bmp` | Pending. O contador avança no Draw quando `obj+14h` está ativo. |
| Fusão | `004520E0` / `005DB364` e `004874C0` / `005DBD54` | `004524E0 / 004877B0` | `004524F0 / 004877F0` | `effect/fusion.txt` | Fusão geral parcial no duelo; fusão vinculada em revisão isolada. |
| Embaralhamento | `004532C0` / `005DB384` | `00453540` | `00453560` | `effect/shuffle.txt`, `deck_shuf1..5.bmp`, `deck_shuf_waku_e.yga` | Parcial: 41 Updates com 5 quadros originais e movimento deck→centro→deck; eventos em buscas no deck e Morphing Jar #2. Centro `DAT_005DB570`, escala/composição e ordem do manifesto ainda não comprovados. |
| Corrente | `0041F5C0` / `005DABA8` | `0041F830` | `0041F870` | `chain/chain.txt` | Não implementado. |
| Indicador de corrente desativada | `00452F20` / `005DB374` | `004531A0` | `004531C0` | `chain/disable.txt` | **Draw Pending:** modelo temporal isolado em `NativeDisabledChainFrame.cs` (24 Updates, 6 índices repetidos, bit `0x40`) e testes C#; faltam recursos/posições/composição/disparo e integração raylib. |
| Remoção / exclusão | `0043D810` / `005DACB4` | `004481F0` | `0043DBE0` | `effect/exclusion.txt` | Parcial: BanishCommand aplicado, zona de destino 0F, 24 Draws independentes e identidade mascarada. Associação positiva no original e outras rotas pendentes. |
| Sacrifício | `00486D80` / `005DBD24` | `00487060` | `004870B0` | `effect/sacrifice.txt`, estrelas do Set provisórias | Implementação parcial: evento para cada monstro usado como tributo; aguarda counter 128 em modo 0, modo 1 +8 até 384; oito partículas radiais; animações de tributos ocorrem antes do voo da nova invocação. Ordem real dos sprites, rotação/scratch, som e temporização pareada pendentes. |
| Moeda | `004571D0` / `005DB404` | `00457500` | `00457520` | `effect/coin.txt`, 14 padrões na tira principal | Não implementado. Variante e relógio têm análise parcial na branch histórica. |
| Dado | `004568C0` / `005DB3F4` | `00456B70` | `00456B90` | `effect/dice.txt` | Não implementado. |
| Exodia | `00453B60` / `005DB3B4` | `00454000` | `00454010` | `exodia/exodia.txt`, miniaturas DAT, `exo_face/arm/arml`, `card_hikari`, `exo_ball` | **Partial:** Core reconhece as cinco peças na mão; Alpha porta apenas o primeiro ramo orbital de cinco miniaturas (79 updates, posição/alpha nativas). Montagem completa de Exodia, efeitos de luz, sons e recursos finais continuam pendentes. Ver `docs/research/exodia-observable-victory.md`. |
| Vija / Destiny Board F-I-N-A-L | `004580F0` / `005DB414` | `004481F0` (vazio) | `004584C0` (contador +4 no Draw) | `effect/vija.txt`, `vija_skel.bmp`, `vija_smoke.yga`, `vija_moji_*.bmp/yga` | **Pending:** cenas criadas por `00409940/00409AB0` em modos diferentes para o conjunto de 5 cartas no campo; a ordem da máscara, o fluxo completo de letras e a lógica de vitória ainda não foram portados. Ver `docs/research/bust-up-cinematics-and-destiny-board.md`. |
| Layerfont A | `00455D90` / `005DB3C4` | `004481F0` (vazio) | `00455FF0` (atualiza contador no Draw) | `effect/layerfont.txt` | **Pending:** contador +4 módulo 256, tabela cosseno e âncora próxima ao canto inferior direito. |
| Layerfont B | `00456100` / `005DB3D4` | `004481F0` (vazio) | `00456360` (atualiza estado no Draw) | `effect/layerfont.txt` | **Pending:** intensidade +4/-4 e desenho condicional; estado acionador ainda não nomeado. |
| Ícones/indicadores de cartas, prompts e variantes de ajuda | `004182E0`, `0041A800` | `00418B60` (contador regressivo) | `004194D0 → 00419B70 / 0041A3B0 / 0041A070` (subpasses internos) | `duel/card/card.txt` (ícones), `duel/font/duel_fontm_y.bmp` (números); `duel/card/icon.txt` separado | **Draw dos ícones Pending**: assembly recebido em 23/11 resolve posições de sprites 4..9, recortes, numeração e argumentos de flash em `NativeStatusDrawLayout`. Falta DAT, semântica Core, integração e validação visual. [Evidência](docs/research/native-card-status-digits-2026-11-22.md). |

## Explosão: parâmetros recuperados e aplicação

O construtor usa o descritor de lado/região/slot para obter o centro
em `0045C140`, grava `obj+14h/+18h` e põe a chave de camada
`0x110` em `obj+0Ch`. O manifesto tem exatamente `expl1.yga` a
`expl7.yga`, que medem 164, 90, 114, 140, 160, 162 e 196 px em cada eixo.

| Ordem | Recurso | Janela | Transformação |
| --- | --- | --- | --- |
| 1 | índice 1, `expl2` | `0x20 ≤ t < 0x90` | centro, giro 0, escala x16.16 `(t+0x60)<<8`, opacidade `min(255,t+0x60)`; `004514B9–00451533` |
| 2 | índices 2–6, `expl3…7` | `0x80 ≤ t < 0xD0` e índice <7 | índice `2+trunc(5×(t−0x80)/64)`; blit integral em `centro−dimensão/2` via virtual `+28h`; `0045153C–004515E5` |
| 3 | índice 0, `expl1` | `t < 0x80` | centro, ângulo `t/512` voltas, escala `(t+0x80)<<8`, opacidade `min(255,C[(t−0x40)&511]>>8)`; `00451605–00451681` |

`00456730` soma 8 ao contador. Em `t=192,200` o índice de
fragmentos já excede 6 e o Draw retorna sem desenho. Em `t=208` a
flag ativa é limpa. Os passes 1 e 3 usam `00422C00`, que dimensiona
a extensão `(dimensão−1)` via MulDiv e usa a tabela x16.16
`A52EE8` para transformar o retângulo. O passe 2 usa a imagem inteira.

O cliente substituiu os círculos e os fragmentos procedurais pelos sete sprites
do DAT. A fila ainda conserva a carta até o limiar `0x80`, uma
decisão anterior do cliente cuja visibilidade precisa de comparação.
O decoder usa RGB preto como chave e conserva o alpha dos demais
pixels. A chave por superfície, o kernel exato do blit `+28h`,
o clipping e a mistura inteira do PE ainda precisam de comparação
para esta classe. A GPU usa alpha padrão, limites do canvas e textura
pontual. A tabela do cosseno é calculada separadamente no C#.
Assim, a seleção e os parâmetros foram portados, e equivalência de
rasterização permanece aberta.

Em um teste isolado da função `DrawDestruction` do aplicativo, a
raylib decodificou e enviou à GPU os sete YGAs reais. A leitura do
render target a 800 × 600 confirmou desenho nos contadores
0, 32, 128, 144, 160, 168 e 184, e ausência de sprite em 192.
As capturas ficaram em `scratch/`. Esta validação é do cliente e
não inclui um frame pareado do executável original.

## Auditoria dos caminhos restantes do aplicativo

`DrawDuelDebug`, as grades opcionais e os botões de diagnóstico são
do cliente. Os avisos de instrução, balão da mão, alvos e contornos de
tributo em `DrawDuel`, `DrawDuelHand` e `DrawDuelField` são UI
funcional provisória. `DrawCards` e `DrawDeck` são telas fora do
dispatcher do duelo. `DrawPile` e `DrawCardBack` têm fallback e
textos do cliente, mesmo quando usam verso do DAT. `DrawAttackAim` ainda usa o giro por cursor e o tamanho do cliente.

A próxima investigação pode seguir a ordem de revelação, quebra e
explosão, e então os três efeitos de invocação e as variantes de
ativação. Os contadores de LP e as transições de fase já têm pontos de
entrada.

## Ataque normal: origem, troca de sprite e impacto

`attack.txt` lista, nesta ordem, `atk_icon.bmp`, `atk_icon2.bmp`,
`atk_eff.yga` e `dmg_ref.yga`. A vtable `005DA710` liga
Update `00414110` e Draw `004142C0`. No estado 4, o Update
`0041413F` soma 16 ao contador, com avanço de estado ao atingir
`0x200`. As coordenadas vêm de `0045C140` para ataque a monstro;
o alvo direto está em X=525 e Y=64/536 conforme o lado
(`004142F0–0041430E`).

| Contador do estado 4 | Sprite e transformação | Evidência |
| --- | --- | --- |
| `0…0x80` | Espada inteira, na origem, gira de 0 ou 256 unidades angulares até a direção calculada por atan2. A fração do giro é `t/0x80`. | `00414812–004148F5` |
| `0x80 < t < 0x100` | Espada inteira em interpolação entre origem e alvo. Peso da origem = `C[(t−0x80)&511] >> 8`, peso do alvo = `256 − pesoOrigem`. Coordenadas divididas por 256, com truncamento. | `00414601–004146E5` |
| `0x100 ≤ t < 0x200` | No centro do alvo, primeiro desenha o impacto, depois a espada parcial. Impacto com escala `(t−0x100)<<9`, sem giro. Espada parcial com escala unitária e giro do vetor de ataque. Intensidade global `2×(0x200−t)` para ambos. | `004146F7–00414800` |

A seleção de sprites, as janelas e os parâmetros acima são **estáticos
confirmados**. O código C# usa uma tabela de cosseno calculada, atan2
da plataforma, recorte e alpha da raylib. Os valores de intensidade
acima de 255 entram no ramo nativo sem atenuação de `00422C00`;
o cliente os limita a alpha 255. O estado 6 usa `dmg_ref.yga` para
outra apresentação e não foi portado nesta fatia.

A Alpha e o duelo local agendam este efeito quando o ataque continua
após a janela de respostas. Um ataque negado não mostra impacto no
alvo. Essa é a integração de apresentação do cliente, que aplica a
sequência do estado 4 aos ataques resolvidos no sandbox.
Antes da declaração, os ramos `00414317–00414401` e `00414907–004149D8`
usam o centro de `0045C140` e escala unitária. O ícone elegível usa
intensidade `0xA0`; para a mira, o original interpola o ângulo até
`min(t, 0x80)/0x80`. No cliente, a mira aponta imediatamente e a
confirmação humana de um alvo conserva o ângulo durante a espera
inicial. O encaixe no centro do alvo e essa continuidade são políticas
do cliente. A relação exata com revelação e LP e os ramos especiais
seguem em aberto.

O teste de render target a 800 × 600 chamou o Draw real do cliente para
um ataque vertical. Nos updates 0, 8 e 15, a espada ocupou 71 px de
altura. No update 16, já centralizada no alvo, a lâmina parcial
ocupou 41 px de pixels visíveis. Nos updates 20 e 31 a área aumentou
com o impacto. A comparação com o original em estado equivalente
continua pendente.


### Distribuição e recomposição das mãos

`0045C8B0` percorre as posições correntes das duas mãos em
`005F32AC`, consulta o alvo por carta em `0045C590` e grava de volta
`alvo + trunc((posição − alvo) × 3/4)`. O trecho `0045C964–0045C976`
aplica a fórmula em X e `0045C978–0045C98F` em Y. O cliente já
aplicava o cálculo ao hover. Agora também aplica em X ao mudar a
quantidade de cartas e mantém a posição corrente das demais quando
uma delas sai. A carta em trânsito usa a posição visível na saída e
o slot calculado na entrada. Isso vale nos dois lados e o preset
Instant aplica o destino de imediato.

A Alpha apresenta as dez cartas iniciais pela fila visual, uma por
vez, com as mãos inicialmente vazias na apresentação. A ordem
alternada entre jogadores e a revelação da carta própria ao pousar
são decisões do cliente. A cadência e a ordem de distribuição nativas
seguem em aberto. O Core já entrega as mãos iniciais prontas e a
animação não realiza compras adicionais. Compras do turno, descartes
de Morphing Jar e o descarte de fim de turno usam a mesma fila.

### Quadros do aviso de batalha

O manifesto `phase_name.txt` reserva os índices 12–18 e 19–25 para
as sete imagens de batalha de cada lado. `00480DA7–00480DD9`
usa `(t−0x40)/8` quando o índice de fase é 3 e a coluna é menor
que 7. Como `00481370` avança o contador de quatro em quatro nesse
intervalo, cada imagem fica dois updates. O cliente aplica essa
seleção e mantém a composição central anterior nos demais updates.


### Entrada e saída dos avisos

Em `00480D0D–00480D8E`, durante `t<0x40`, as duas cópias usam
X do centro ± `(C[2t] >> 10)` e opacidade `3t`. Após `0x80`,
`00480EBE–00480F04` desenha uma cópia centrada, com opacidade
`C[t−0x80] >> 8`. A Alpha usa essas fórmulas com a tabela de cosseno
calculada pelo cliente e mistura raylib. O segundo passe do trecho
central e o aviso de troca de jogador ainda não foram portados.

## Set — integração parcial (21/11/2026)

Em `0044F370`, o contador soma `0x0C` por Update até ultrapassar
`0x100`: 22 updates (`0,12,…,252`), desativando em `264`.
Em `0044F390`, a escala principal é `0x10000` para `t<0x80`
e `t<<9` depois; a intensidade principal usa a tabela de cossenos
`(C[(t−0x80)&511] × 0xB0)>>16`. O laço de partículas seleciona
duas variantes alternadamente e usa oito lançamentos espaçados de
16 unidades de contador, com deslocamento vertical quadrático.

O índice DAT contém `eff_set_b/y.bmp` e
`eff_star0/1_b/y.bmp`. O cliente desenha o sprite central **e as
oito partículas** depois do voo de Set humano/CPU de ambos os lados,
para monstros e magias/armadilhas. Em 21/11/2026, o SQLite global
recuperou o construtor `0044EFD0`, ausente do ZIP inicial: a cada
partícula um gerador MT compartilhado escolhe raio de 16 a 47 px e a
posição `obj+104h + 8*i` é calculada com `cos[i*64]` e
`cos[(i*64−128)&511]` x16.16 em torno do centro. O Draw desloca cada
partícula verticalmente por `floor(age²/16)` e alterna a variante
`age & 1`. `00402690` revela seleção pela categoria monstro vs.
magia/armadilha, não pela perspectiva do jogador.

O cliente usa `Random.Shared` para os oito raios, sem replicar o estado
MT do jogo; a tabela de cosseno ainda é calculada em C# e a ordem dos
BMPs/colorkey, scratch e blending precisa ser confirmada com o
`set.txt` do DAT e capturas pareadas. Há detalhes e SQL em
`docs/research/native-set-particles.md` e
`docs/research/joey-knowledge-sqlite.md`. Sem comparação visual
pareada, o estado continua **Partial**, não `Verified`.


## Invocação normal — primeira fatia visual (21/11/2026)

O construtor `0044F630` está no SQLite global e instala a vtable
`005DB2D4`, carrega `duel/effect/summon.txt`, posiciona o objeto pelo
mesmo `0045C140` usado por outros efeitos e inicializa referências
a `data/mini/list_card.txt`. Os entrypoints transitórios
`0044F9F0` (Update) e `0044FA10` (Draw) estão no ZIP Ghidra
de 21/11, mas não são linhas independentes em `functions` no banco.

**Fatos estáticos:** Update soma `8`, conserva atividade enquanto
`counter < 257`, produz 33 amostras `0,8,…,256`. Draw tem dois
ramos, delimitados por `(counter+0x40)*0x100 <= 0x8000`.
No primeiro (`counter <= 64`), escala aparente
`(counter+64)*256` em x16.16 e intensidade
`C[(counter+56)&511] >> 8`; o blit chama `00422C00`.
No segundo (`counter >= 72`), intensidade aparente
`min(255, 3*C[(counter−160)&511] >> 10)` e varredura da
imagem mini com origem `trunc(counter*5/8)`, módulo a dimensão
do sprite. A decompilação ainda perde protótipos e registradores,
e a seleção do quadrante e os passes de clipping não estão comprovados.

O índice do DAT mostra uma contraparte `mini/<arquivo>.bmp`
para todas as 777 imagens `card/<arquivo>.bmp` inventariadas.
O cliente reutiliza a imagem mini original, sem placeholder,
e agenda a animação depois do voo de invocação **face-up**,
tanto humano quanto CPU. A segunda janela é representada por
dois segmentos de recorte para a varredura circular; orientação,
transformação e composição exatas são **provisórias**.
A tabela x16.16 segue aproximada em C# e não há comparação
pixel a pixel com a versão Windows original.

O Set face-down conserva sua animação separada; a mecânica,
a resposta à invocação e a IA em `Joey.Core` não mudaram.
Esta fatia altera a cobertura de Draw para **13/31 (41,94%)**,
mas continua `Partial`, não `Verified`.


## Invocação especial — etapa inicial (21/11/2026)

`0044FCC0` está decompilada no SQLite global e prepara objeto
`005DB2E4`, centro via `0045C140`, `effect/sp_summon.txt` e
representação da carta em `obj+0x108`. No lote Ghidra inicial,
`004500E0` avança `obj+0x10 += 8` até desativar em 328,
produzindo 41 amostras `0,8,…,320`.
O Draw `00450100` tem dois passes:

- `t < 192`: `(t >> 4)&3` seleciona um dos quatro YGAs do
  grupo indicado pelo bit 14 do descritor. A intensidade segue
  `min(255, C[(t−96)&511] >> 8)`. O DAT contém
  `sp_sum_a1..4.yga` e `sp_sum_d1..4.yga`.
- `64 < t < 320`: a imagem da carta em `obj+0x108` é
  transformada por `0054E890`, recebendo um parâmetro
  `256 − (C[(t−192)&511] >> 13)`. Interpretado como
  escala em 8.8 no cliente. Textura, blend e posicionamento
  exatos não foram validados contra o original.

O cliente agora enfileira o efeito quando o Core confirma uma
invocação especial pelo seletor de cartas, busca em lote ou
escavação de Morphing Jar #2, inclusive face-down quando
cabível. O evento `MonsterSpecialSummoned` é **somente
observacional** e não altera regras, custo ou estado de turno.
As oito imagens YGA originais são carregadas do DAT local.
O fallback visual da segunda janela usa miniatura/back nativo
do DAT, mas a equivalência com `0054E890` ainda está aberta.

A classe `NativeSpecialSummonFrame` guarda contador, sprite
0..3, intensidade e janela de transformação; testes cobrem
limites e a notificação de ambos os lados em Morphing Jar #2.
A invocação normal existente também teve sua derivação de
`mini\\` corrigida: as strings verbatim C# continham um
separador duplicado, fazendo o cliente não encontrar a
miniatura no carregamento. Esta correção usa prefixos
de um único separador real (nome de entrada DAT).

Status **Partial**. A cobertura Draw passa a **14/31 (45.16%)**,
sem qualquer `Verified` até comparação de quadros.


## Dano e cura flutuantes — primeira integração (21/11/2026)

O Draw `0047C660` chama `004806F0` sempre que o objeto de
LP tem delta pendente (`obj+0x28 != 0`). O contador passado é
`(obj+0x34) * 2`; `0047C560` já havia confirmado que
`obj+0x34` cresce em 8 por Update até 256.

`004806F0`, presente no SQLite global, confirma uma rotina
de dígitos alinhados à direita, processados **da unidade para a
esquerda**, com 10–12 glifos possíveis no atlas. Quando o
contador visual está entre 1 e 255, cada glifo tem fase
`phase = 2*ramp − floor(64/(n−1))*index` (um dígito usa
delay zero), e só aparece quando sua fase é positiva:

- `phase < 128`: deslocamento em Y por
  `−(C[(2*phase−128)&511] * alturaGlifo >> 16)`;
- `128 ≤ phase < 192`: segundo deslocamento por
  `−(C[(4*phase−128)&511] * (alturaGlifo/4) >> 16)`;
- `phase ≥ 192`: glifo em repouso.
- Em atlases de 12 glifos, índices 10/11 representam
  sinais positivo/negativo na análise estática.

O DAT contém `j\\duel\\font\\damage00.bmp` e
`damage01.bmp`; o Draw seleciona os descritores
`(delta>0)+6` para a fonte. A associação desses dois nomes
com os descritores 6/7 **ainda deve ser confirmada em
`number.txt`**; no cliente, cada bitmap só é utilizado
quando a quantidade de colunas pode ser lida do manifesto.
O cliente desenha os glifos originais e amostra a função
`NativeFloatingLifeDigits.Sample`, enquanto LP lógico
continua a mudar imediatamente no Core.

A origem `0045C140` na chamada original depende de parâmetros
não reconstruídos com segurança pelo descompilador. A posição
relativa ao HUD `right=209, top=536/45` e a escala máxima de
32 pixels são aproximações explícitas do cliente. Ainda não há
captura pareada ou validação de cor, colorkey, ordens dos passes
ou FPS efetivo.

Por já estar a família de LP contabilizada como `Partial`,
a porcentagem visual permanece **14/31 (45.16%)**.


## Avisos de início e resultado do duelo — recursos e janela (21/11/2026)

O SQLite global contém os construtores `004516A0` e
`00451B80`; os updates/draws `00451950/00451980` e
`00451E40/00451E60` estão no lote visual do Ghidra, sem
necessidade de outra extração.

**DUEL START:** `00451950` incrementa o contador em 8;
`00451980` libera o objeto quando ultrapassa `0x13F`.
Foram modeladas 40 amostras `0,8,…,312`:
`t<64`: escala `256 + (C[2t] >> 9)` (8.8)
e alfa `C[(2t−128)&511] >> 8`;
`64≤t≤256`: opacidade 255 e escala unitária;
`256<t<320`: alfa `C[(2t)&511] >> 8`.
O Draw nativo usa o sprite `duel_start_e.bmp` no centro
`(400,300)`. O cliente enfileira esse aviso antes da
distribuição inicial na Alpha e no duelo local; a ordem entre
efeitos e cartas **não** foi capturada no original.

**Resultado:** `00451E40` aumenta o contador em 4 até
`0x200`, e nesse limite altera uma flag interna. O Draw
`00451E60` exibe uma única imagem no centro `(400,300)`:
`t<128` usa `C[(t−128)&511]>>8` para fade-in;
`128≤t≤512` exibe opacidade 255. O Draw também contém
ramo para fade-out após 512, mas sua alcançabilidade
pelo scheduler não está demonstrada. A Alpha, portanto,
mantém o banner durante o estado terminal, em vez de
inventar a transição subsequente.

`DatGraphics` carrega do DAT os originais:
`j\duel\effect\duel_start_e.bmp` e
`j\duel\effect\duel_win_e.yga`,
`duel_lose_e.yga`, `duel_draw_e.yga`.
O mapa de win/lose/draw depende da perspectiva de
`ViewPlayer`, sem mudar a resolução do duelo.

Status **Partial** para ambas as famílias; a cobertura
visual passa para **16/31 (51.61%)**. Ainda faltam
composição de superfície, diferenças de kernel BMP/YGA,
eventual mascaramento do fundo, som do aviso, troca de cena,
calibração do scheduler e capturas pareadas 800×600.


## Embaralhamento — integração parcial (21/11/2026)

O construtor `004532C0` está no SQLite global; os
entrypoints `00453540` e `00453560` no ZIP Ghidra
anterior. Update soma 8 a `obj+0x14` até desativar
em 328: **41 quadros** `0,8,…,320`.

O Draw seleciona cinco variantes com índice
`(counter / 8) % 5 + 1` (1..5) carregadas pelo manifesto
`effect/shuffle.txt`: o DAT lista `deck_shuf1..5.bmp`.
Há também `deck_shuf_waku_e.yga`, usado como moldura
na janela central. Três janelas:
- `t < 64`: deck para centro, peso de origem
  `256 − 4t` em ponto fixo x8.8.
- `64 ≤ t ≤ 256`: embaralhamento central em cinco quadros;
  música/sons repetidos a cada 64 unidades do contador.
- `256 < t ≤ 320`: retorno ao deck, peso
  `256 − 4(320−t)`.

O ponto do deck usa `0045C140` com descritor de deck.
O centro da operação lê `DAT_005DB570`, valor ainda
não recuperado. No cliente, a hipótese explícita é
`(525,300)` e a moldura em `(525,326)`; escala de
sprite, clipping, seleção de buffers e blend continuam
provisórios. As animações são apresentadas exclusivamente
com os BMP/YGA originais do DAT.

O Core emite `DeckShuffled` **depois** de concluir
uma transformação de deck por buscador ou Morphing Jar #2;
o evento não altera a ordem, seed, contagem ou aquisição
das cartas e nunca é gerado pelos embaralhamentos iniciais
de `DemoDuel.FromDecks`. A Alpha usa a mesma fila visual,
inclusive quando ambos os lados embaralham em sequência.
O teste Morphing Jar #2 exige os dois eventos separados,
além da preservação das regras já testadas.

Status **Partial**, cobertura visual **17/31 (54.84%)**,
0 `Verified`. Conferir no executável localização do
shuffle, tamanho real dos cinco BMPs, moldura, direção
da viagem, som, transições, frames e ausência de
animacões redundantes em buscas específicas.


## Sacrifício — primeira fatia vertical (21/11/2026)

As evidências foram **reabertas diretamente** do ZIP deconstruído
`joey-knowledge(1).zip` e de `ghidra-visual-batch.zip`:

- SQLite `functions+decompilations`: `00486D80` inicializa oito
  pares X/Y em `obj+0xB4`, com 8 raios de
  `16 + (NextUiDeckMtUInt32_0055C140() & 31)` sobre a tabela
  de 512 cossenos x16.16, como o efeito Set.
- SQLite callers `004879A0` e `00487B00` instanciam o mesmo
  objeto com mode `0` (espera) e `1` (liberação),
  respectivamente.
- ZIP transient Update `00487060`: com modo 0, ao alcançar
  contador `128` interrompe o avanço; com modo 1, soma
  `8` por Update até desativar após contador `384`
  (49 amostras `0,8,…,384`). Aciona uma chamada de som
  quando alcança `136`.
- ZIP transient Draw `004870B0`: dois passes principais,
  com intensidades calculadas da tabela de cossenos. Depois
  do contador 128, oito partículas são lançadas com atraso
  de 16 unidades, fase `(counter−128−16*i)/8` e subida
  quadrática `floor(age²/16)`. Os índices dos sprites
  dessas partículas são `2 + (age & 1)`.
- `effect/sacrifice.txt` está catalogado no DAT, mas sua
  **ordem interna de recursos não está disponível** nesta
  análise. A Alpha usa as imagens `eff_star0_b/1_b.bmp`
  do Set **como substitutos provisórios**, não como
  comprovação de equivalência.

O Core já distinguia monstros tributados de monstros
destruídos (`isTribute`), mas não emitia evento visual.
Agora `DemoMonsterTribute` preserva jogador, zona, carta,
face-down e defesa depois do pagamento bem-sucedido.
Um pagamento inválido emite zero eventos; dois tributos
válidos geram dois eventos, na ordem selecionada, e
não disparam explosão de destruição.

A Alpha põe os efeitos visuais dos tributos antes do
`CardFlight` da carta recebida da mão e suprime
temporariamente a carta nova no campo. O estado lógico
`Joey.Core` permanece imediato; a fila de apresentação
apenas reconstitui a ordem observável. Completada a
sequência, o voo continua e o efeito de Set/invocação
normal existente roda depois dele. Isto é uma escolha
de composição do cliente, não uma medição pareada da
ordem exata do original.

**Pendências:** `sacrifice.txt` real, imagem central,
rotação acumulativa que o Draw modifica, scratch,
alpha, classe das estrelas, ângulo de cartas em
defesa e sincronização com áudio. A cobertura do
Draw passa para **18/31 (58.06%) Partial**, sem
nova rotina Verified.

## Bustos do Joey, cenas signature e encerramentos

O SQLite contém uma família paralela à composição das cartas do duelo:
`00402E00/00403310/004166F0` para reações, `00414A20` para
signature summons (`bust_up/summon*.txt`) e `00415C90` para as
expressões de vitória/derrota/empate. A base comum `0045CF80` e o
controle de conclusão dos efeitos em `00409D00`, `00409940` e
`00409AB0` são evidências de que essas cenas compartilham a gestão
de apresentações, apesar de possuírem vtables/Draws diferentes.

| Elemento | Construtor | Update | Draw | Recursos DAT | Estado |
| --- | --- | --- | --- | --- | --- |
| Invocação signature | `00414A20` / `005DA7F0` | `00414CF0` | `00414DC0` | `bust_up/summon.txt`, `summon2.txt` | Pending |
| Busto de abertura | `00415130 → 004152D0` / `005DA800` | `00415580` | `00415840` | `bust_up/start.txt` | Pending |
| Busto de resultado | `00415AF0 → 00415C90` / `005DA820` | `00416050` | `00416260` | `bust_up/win.txt`, `lose.txt`, `draw.txt` | Pending |
| Busto de reação | `004166F0` e `00416A00` / `005DA840` | `00456730` | `00416BC0` | `bust_up/reaction*.txt` | Pending |

A coleta 27 preserva as tabelas. O assembly das fábricas confirma a
instalação das vtables finais de abertura e resultado em `0041516B` e
`00415B2B`, após os construtores instalarem as bases `005DA810` e
`005DA830`. A invocação signature instala a sua em `00414AA4`, e as
reações em `004167B6` e `00416ACA`. O slot Draw do objeto de som
(`005DA3FC + 0Ch`) aponta para `0043A5D0`, cujo corpo é `ret 4`.

O índice do DAT inventaria 17 manifests e 160 BMP/YGA em `j\bust_up\`.
O inspetor `--inspect-visual-dat` cobre esses recursos.
[Pesquisa dos bustos](../research/bust-up-cinematics-and-destiny-board.md).
