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
| Painel de detalhes | `0041BE80` / `005DA9F8` | `0041C6D0` | `0046F7A0` | `detail/detail.txt`, fundo, separador, rolagem | Implementado com divergências. `DrawDuelDetail` usa o fundo e composição de texto/LP do cliente. |
| Cartas e mão | `004182E0` / `005DA868` | `00418B60`; hover `0045C8B0` | `00418EF0` | `card/card.txt`, molduras, arte e versos | Implementado com divergências. `DrawDuelField`, `DrawDuelCard`, `DrawFieldCard`, `DrawDuelHand`, `DrawCpuHand`, `DrawArtwork`, `DrawCardBack`. Centros, ângulos e face separados; a combinação da arte, clipping e escala em trânsito ainda difere. |
| Deck, cemitério e pilhas | `0044C730` / `005DAC94` | `0043C870` | `0043C980` | `card/deck.txt`, BMPs de deck/sombra/grave | Implementado com divergências. `DrawSidePiles` e `DrawPile` usam o verso e texto do cliente. A vtable também aparece no caminho `0043D090`. |
| Atributos ATK/DEF | criado por `004182E0`, recurso em `005F33EC` | Suavização no Draw | `004194D0 → 00480500 / 00480300` | `font/number.txt` e tiras de números | Implementado com divergências. Y centro ±46 está confirmado; o cliente ainda usa fonte de texto e valores imediatos. |
| LP, dano e placar | `0044C3E0` carrega LP; `0047C310` cria objetos de delta (`005DBAA4`) | `0047C560` | `0047C660` | `life/lifepoint.txt`, `lp_jibu1/2.bmp`, `lp_aite1/2.bmp`, `font/number.txt` | Parcial: o contador visual usa rampa +100/10/1; `DrawDuelDetail` já compõe barras texturizadas do DAT por `min(LP,8000)/8000`, e usa atlas de dígitos quando `number.txt` revela quantidade de colunas (texto como fallback). Posicionamento, índice da fonte, máscaras, barra e efeito flutuante ainda não estão pareados com o original. |
| Fases, botões e troca de cor | `00481040` / `005DBB2C` | `00481370` | `00481420` | `phase/phase.txt`, `pha_s/b/r_*.bmp` | Implementado com divergências. `DrawPhasePanel`: recortes centrais do giro foram comparados com o DAT nos dois sentidos. Hitbox e detalhes do estado ativo ainda não foram validados contra o Joey. |
| Avisos de fase e novo jogador | `00480B40 / 00480BC0` inicializam estados | `00480C20` | `00480C60` | `phase_name/phase_name.txt`, banners e `nex_*.yga` | Implementado com divergências. `DrawPhaseNotice` seleciona o banner azul/vermelho por lado. Os sete quadros intermediários de batalha também vêm do DAT. Entrada e saída seguem os deslocamentos e a opacidade do Draw. Mistura do meio e aviso de jogador continuam abertos. |
| Diálogos e seleções | `00487E70` (manifesto) | Aberto | Aberto | `dialog/dialog.txt`, `button_?.txt`, `ritualdlg.txt` | Placeholder/procedural inventado. `DrawCardSelectionOverlay`, `DrawSummonResponseOverlay`, `DrawAttackResponseOverlay` e `DrawEffectDecisionOverlay` usam painéis de gameplay do cliente. |
| Ajuda | `0045E250` / `005DB630` | `0045E3D0` | `0045E4A0` | `help/help.txt`, YGAs por idioma | Não implementado. |
| Efeito de estado persistente | `0040DAC0` / `005DA44C` | `0040E010` | `0040E260` | `effect/affect.txt` | Não implementado. Associação por manifesto e vtable; sem mapa dos ícones e flags. |

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
| Invocação especial | `0044FCC0` / `005DB2E4` | `004500E0` | `00450100` | `effect/sp_summon.txt` | Não implementado como efeito dedicado. |
| Ativação no campo | `00450260` / `005DB2F4` | `00450590` | `004505D0` | `effect/activate.txt`, pares `kouka_ue/sita_*` | Implementado com divergências. `DrawSpellActivation` e `DrawActivationSprite` usam dois YGAs e scratch dinâmico, que também existe no original. Mistura e seleção por categoria ainda divergem. Comando 72, handler `00406B20`. |
| Ativação alternativa | `00450730` / `005DB304` | `00450A90` | `00450AD0` | seleção aberta | Não implementado como ramo separado. |
| Abertura | `004516A0` / `005DB334` | `00451950` | `00451980` | `effect/duelstart.txt` | Não implementado. |
| Resultado do duelo | `00451B80` / `005DB344` | `00451E40` | `00451E60` | `effect/duelend.txt` | Placeholder/procedural inventado. Resultado funcional em `DrawDuel` usa quadro e texto próprios. Há variante de cena em `00483B10`. |
| Fusão | `004520E0` / `005DB364` e `004874C0` / `005DBD54` | `004524E0 / 004877B0` | `004524F0 / 004877F0` | `effect/fusion.txt` | Não implementado. |
| Embaralhamento | `004532C0` / `005DB384` | `00453540` | `00453560` | `effect/shuffle.txt` | Não implementado. As cartas de Morphing Jar usam a fila de transferências já descrita. |
| Corrente | `0041F5C0` / `005DABA8` | `0041F830` | `0041F870` | `chain/chain.txt` | Não implementado. |
| Indicador de corrente desativada | `00452F20` / `005DB374` | `004531A0` | `004531C0` | `chain/disable.txt` | Não implementado. |
| Remoção / exclusão | `0043D810` / `005DACB4` | `004481F0` | `0043DBE0` | `effect/exclusion.txt` | Não implementado como animação. |
| Sacrifício | `00486D80` / `005DBD24` | `00487060` | `004870B0` | `effect/sacrifice.txt` | Não implementado como animação. Seleção funcional em `DrawDuelField` usa contorno do cliente. |
| Moeda | `004571D0` / `005DB404` | `00457500` | `00457520` | `effect/coin.txt`, 14 padrões na tira principal | Não implementado. Variante e relógio têm análise parcial na branch histórica. |
| Dado | `004568C0` / `005DB3F4` | `00456B70` | `00456B90` | `effect/dice.txt` | Não implementado. |
| Exodia | `00453B60` / `005DB3B4` | `00454000` | `00454010` | `exodia/exodia.txt` | Não implementado. |
| Vija | `004580F0` / `005DB414` | `004481F0` (vazio) | `004584C0` | `effect/vija.txt`, inteiros com número de padrões e offset Y | Não implementado. |
| Ícones de carta, outros prompts e variantes de ajuda | Aberto | Aberto | Aberto | `card/icon.txt`, `list_icon/`, `effect/layerfont.txt` e outros manifests | Ainda não investigado como cadeia de objetos. |

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
