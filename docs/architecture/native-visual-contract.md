# Contrato visual estático de Joey — consolidação em Joey.LookAndFeel

**Alvo de engenharia reversa:** `joey_pc.exe`, SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`,
image base `0x00400000`. Endereços a seguir são VAs **dessa versão**.

**Fonte desta consolidação:** a branch
[`analise-visual`](https://github.com/ffdiego/YugiRE/tree/analise-visual),
com relatórios de análise estática
[`rendering-architecture.md`](https://github.com/ffdiego/YugiRE/blob/analise-visual/docs/research/visual/rendering-architecture.md),
[`animation-formats.md`](https://github.com/ffdiego/YugiRE/blob/analise-visual/docs/research/visual/animation-formats.md)
e
[`activation-effect.md`](https://github.com/ffdiego/YugiRE/blob/analise-visual/docs/research/visual/activation-effect.md).
A investigação anterior da mão está em
[`duel-hand-pause.md`](../visual/duel-hand-pause.md).
**Este commit extrai somente os contratos sustentados pela evidência**, não
importa automaticamente toda a branch de pesquisa nem dados do jogo.

## Separação de responsabilidades

```text
Joey.Core
  | alterações de estado, regras, dano, efeitos FLIP
  v
Joey.LookAndFeel
  | coordenadas, movimento, parâmetros dos passes, contadores,
  | máscaras de despacho, timing selecionado pelo usuário
  v
Joey.App (raylib)
    entrada, renderização, recursos DAT locais, relógio de quadros
```

Os três arquivos `HandPresentation.cs`, `NativeAnimation.cs` e
`DuelLookAndFeel.cs` ficam em `src/Joey.LookAndFeel`. O Core **não** depende
do assembly de apresentação. O método `MoveCardAxis` aplica um passo nativo. O relógio
de apresentação o agenda a 30 passos/s em Classic e 60 em Fast. Instant
salta à posição final. Isso altera **só a apresentação**, não o número de turnos,
compras ou respostas lógicas. O FPS do programa original ainda não foi medido.

## Geometria e mão

| Função/variável original | Fato sustentado | Tipo |
| --- | --- | --- |
| `0x0045C140` / `0x0045C280` | Localiza carta por lado, região e slot | Estático |
| `0x0045C590` / `0x0045C600` | Posições e largura da mão; limita largura a 512 | Estático |
| `0x0045C8B0` | Suaviza movimento do hover e eleva alvo em 32 | Estático |
| `0x005DB560` | Centro dos monstros, X 369/447/525/603/681, Y próprio 352, adversário 248 | Estático |
| `0x00418B60` | Interpola posição, rotação e escala do objeto visual | Estático |

A função de aproximação implementada é
`target + trunc((current - target) * 3 / 4)` **por update**.
`HandPresentation.CenterX` mantém as fórmulas anteriores, agora fora do
`Joey.Core`. As dimensões atuais da mão estão descritas na seção de mãos.
Os `66×88` são retângulos de clique do cliente. Os centros e a ordem
espelhada das zonas são recuperados das tabelas nativas.

O `Joey.App` usa `NativeDuelLayout.MonsterBounds` para zonas de monstros,
`HandPresentation.CenterX` para a mão e
`presentation.Style.MoveCardAxis` para o movimento de hover.

## Objeto visual e despacho

A investigação de `0x0045D040` (update),
`0x0045D170` (draw) e `0x0045D150` (comparador) sustenta:

- byte `obj+0x04`: objeto ativo;
- DWORD `obj+0x08`: máscara que precisa cruzar a máscara solicitada;
- int `obj+0x0C`: chave de ordenação **crescente**;
- vtable `+8`: update; `+0x0C`: desenho, para os objetos investigados.

`NativeVisualDispatch.Participates` e `CompareLayer` modelam o filtro e a
ordenação. Empates de chave, máscaras exatas de cada tela e ordem de desenho
sob múltiplos passes ainda precisam de verificação.

## Animação da ativação

Funções `0x00450260` (construtor), `0x00450590` (update),
`0x004505D0` (draw) e `0x00422C00` (transformação).

- O contador `t` cresce em **16 por atualização**; após a 17ª atualização
  sai de 256 para 272 e desativa a animação (`t < 257`).
- `NativeActivationTimeline` expõe o contador em **updates**, sem converter
  para frames exibidos ou milissegundos.
- `NativeActivationFrame.FromCounter` produz três passes, nesta ordem:
  **sprite inferior, scratch preto gerado, sprite superior**.
- A escala vem de uma tabela nativa `cos` em ponto fixo
  `C[(t - 128) & 511]`; a função exige as **512 entradas fornecidas pelo
  chamador**, pois `Math.Cos` não comprova paridade bit a bit com a tabela x87
  do processo. Não há tabela inventada no repositório.
- Opacidade dos sprites `min(s >> 8, 255)`; do scratch `s >> 9`.
  Rotações `t/2` (sprite inferior) e `t` (superior).
- `ResourceIndex` usa o par `2*categoria+1` / `2*categoria`
  para categorias azul/amarela do `activate.txt`.

**Isso é um contrato de sequência e de parâmetros, não uma reprodução completa
do rasterizador.** A mistura de canais, a color key e o tamanho real do scratch
dependem de recursos, superfícies e captura ainda não integrados à raylib.
A branch `analise-visual` já contém um visualizador isolado para investigar
esses passes; não foi copiado para o `Joey.App` como se fosse validado.

## Explosão e relógios

`NativeExplosionTimeline` representa `0x00456730` (contador +8 por update)
e as janelas de desenho em `0x004514B0`:

| Pass | Janela de contador |
| --- | --- |
| Initial | `t < 0x80` |
| Flare | `0x1F < t < 0x90` |
| Fragments | `0x7F < t < 0xD0` |
| Desativação | no desenho quando `t > 0xCF` |

A função `AfterNativeDraw` é separada de `AdvanceNativeUpdate` porque a
evidência mostra que **draw também altera estado** para essa classe.
Não se afirmou duração de animação em segundos, FPS de 60 do nativo nem
contagem fixa de imagens exibidas.

## Contadores do executável e efeito de `-speedy`

Análise estática em Ghidra com `-noanalysis -readOnly`, conferida com
`objdump` no EXE do SHA acima. O DWORD em `005EB618` começa em **30** na
seção `.data`. O parser em `00411CA2` grava **60** com `-speedy`.
A leitura em `00411101` passa o valor a `005456E0`, que calcula
`(1000 << 16) / frequência`. O laço chama `00545780` em `004112D4` e
essa rotina usa o intervalo acumulado e timestamps em milissegundos.
Isso estabelece a cadência **nominal do laço** de 30 ou 60 iterações/s.
O relatório anterior de xrefs não mostrou a leitura em `00411101`.

| Efeito | Contador estático | Tempo nominal a 30 / 60 updates/s |
| --- | --- | --- |
| Movimento da carta (`00418B60`) | De `0` até `0x100` em passos de `0x20`: 8 updates de movimento | 267 / 133 ms |
| Brilho de ativação (`00450590`) | `+0x10`, desativa no 17.º update | 567 / 283 ms |
| Aviso padrão de fase (`00480B40`, `00480C20`, `00480C60`) | Começa em `0`, soma `8`, encerra no draw em `0x100`: 32 updates. O índice de fase `3` soma `4` de `0x40` a `0x80` e leva 40 updates. | 1.067 / 533 ms. Índice `3`: 1.333 / 667 ms. |
| Aviso de troca de jogador (`00480BC0`, `00480C20`, `00480C60`) | Índice `6`, `+8` até `0x180`: 48 updates | 1.600 / 800 ms |
| Valor flutuante de LP (`0047C4F0`, `0047C560`) | Contador `0 → 0x100` em 32 updates. Depois, enquanto a flag em `+0x30` estiver livre, consome um passo por update de `100`, `10` ou `1`, conforme o valor restante. Um delta de `1.000` requer mais 10 updates. | Espera: 1.067 / 533 ms. Consumo de 1.000: mais 333 / 167 ms. |

As conversões assumem um update do objeto por iteração. Os valores não
incluem esperas por resposta, sobreposições, pausas impostas por flags,
nem a cadência efetiva de draw. A gravação feita com `-speedy`, a 30 fps,
mostra a ordem dos efeitos, mas amostra menos quadros que a cadência nominal
de update. Ela não mede o preset sem a flag. Ainda falta fechar a lógica de
seleção e movimento da espada, que não deve herdar os tempos de uma carta.

## Primeira prévia em raylib (posterior à análise estática)

A aplicação agora carrega os sprites de ativação `kouka_ue/sita_b/y.yga`
do DAT original por `NativeYgaSprite` e desenha os três passes via
`DuelActivationScreen.cs` quando uma magia suportada é realmente ativada.
A política temporal `ActivationEffectPlayback` segue os 17 updates do
contador com 30 atualizações/s em Classic e 60 em Fast. Essas são as
cadências nominais do laço recuperadas estaticamente. As transformações usam tabela gerada com `Math.Cos`
e GPU blending da raylib; isto é uma **prévia**, não replay pixel-perfect.
[Instruções, limitações e pausa da trilha](activation-preview.md).

## Validação, limites e próximos trabalhos

`NativePresentationChecks` valida fórmulas e contadores sintéticos,
posicionamento espelhado, perfis e fases de explosão. A build e testes C# são
executados pela CI; a UI ainda requer comparação manual com Joey no Windows.

1. Confirmar cadência e ordem `update → draw` com instrumentação observacional
   em `0x0045D040`/`0x0045D170`; coletar valores `t` e timestamps.
2. Medir posições da mão ao longo do tempo e conferir interpolação e dimensões.
3. Validar o replay de ativação com a tabela de cosseno **extraída do
   processo**, comparando frames/alfa/transformações contra capturas nativas.
4. Só depois criar timeline em tempo real, sprites raylib e áudio/voz original
   e concluir a ligação por eventos tipados de domínio. `--fast` e
   `--mute-joey` continuam políticas de apresentação, nunca regras.

As durações dos demais cues em `DuelLookAndFeel.cs` continuam sendo
políticas do protótipo. A trilha de ataque ainda usa uma duração provisória.

## Origem do fundo, do verso e do texto

- O índice e os cabeçalhos BMP do `data.dat` local confirmam
  `j\duel\field\fie_normal.bmp` (570 × 600), `card\card_ura.bmp`
  (200 × 290) e `mini\card_ura.bmp` (50 × 72).
- O DAT contém imagens de dígitos e dano em `j\duel\font\`. O executável
  referencia `duel/font/number.txt` via `00413DB0` durante a preparação
  da apresentação das cartas. [Decompilação](../evidence/hand-hover-animation-references.txt).
- Para texto, o executável chama `GDI32.CreateFontA` em `0054524B` e
  `005453C1`, e a rotina `00545570` usa `TextOutW`. Esse caminho pede ao
  Windows uma fonte por nome ou passa uma string vazia para a seleção do GDI.
  O seletor `00544B80` tem `Courier New` no índice 0 e `Arial` no
  índice 1 (tabela em `00544DE0`). A associação de cada família a uma
  tela específica ainda precisa de rastreamento dos chamadores.
- A árvore PE `.rsrc` contém ícones, grupo de ícones e strings. O índice do
  DAT não lista TTF, OTF, FON ou FNT. A inspeção cobre esses contêineres
  e os caminhos de texto acima.

## Espadas e fases no DAT

| Recurso | Entrada do DAT | Evidência |
| --- | --- | --- |
| Espada de ataque | `j\duel\effect\atk_icon.bmp` e `atk_icon2.bmp` na mesma pasta | Dois BMPs de 35 × 71. O primeiro tem a espada inteira e o segundo contém o punho e parte da lâmina. O fundo dos dois é verde. |
| Botões de fase | `j\duel\phase\pha_{b,r,s}_{dr,st,m1,ba,m2,en}.bmp` | 18 imagens para seis fases e três variantes. A pasta também tem `phase.txt`. |
| Avisos de fase | `j\duel\phase_name\e_dr_b.bmp`, `e_m1_b.bmp` e demais variantes da pasta | O aviso de compra em inglês mede 239 × 44. Há variantes azuis/vermelhas e quadros adicionais para batalha e troca de turno. |

O inventário identifica as imagens. A seleção exata das variantes de
espada e botões durante Update/Draw continua aberta.

## Mira e dimensões do cliente

A consulta Ghidra confirmou que a tabela `005DB560` alimenta os
conversores de posição `0045C280` e `0045C3F0`. A animação de ataque
já declarado entra em `00407A20 → 00413BA0 → 004140B0`. O último
inicializa os estados 6 ou 7. A rotina que define a origem e a rotação
da mira antes da declaração ainda precisa ser isolada.
O construtor `00413BC0` referencia `duel/effect/attack.txt` em
`00413CD8` e instala a tabela virtual em `005DA710`.

No vídeo local, em torno de 00:43, as cartas do campo ocupam
aproximadamente 50 × 72 pixels em pé. Isso coincide com o tamanho do
verso em `mini\card_ura.bmp`. O cliente usa esse tamanho para as
cartas assentadas no campo, invertendo os eixos em defesa e usando
as zonas anteriores para os cliques. O tamanho da mão e a interpolação
das cartas em trânsito ainda precisam de comparação.

A posição da espada junto ao atacante e a mira pelo centro do alvo
seguem a observação informada pelo jogador. O deslocamento da âncora
dentro da zona e a rotação por atan2 são decisões do cliente. O vídeo
também confirma a função da coluna de detalhes. O YugiRE apresenta LP,
arte e descrição na coluna e mantém comandos em uma sobreposição no
campo, com composição própria.

## Coluna de detalhes, troca de turno e orientação

- A coluna tem fundo próprio `j\duel\detail\detail.bmp` (230 × 600).
  O manifesto `detail.txt` também lista `line.bmp` e `scroll_bar.bmp`.
  O Ghidra mostra o construtor `0041BE80` referenciando o manifesto em
  `0041BF18` e instalando a vtable `005DA9F8`.
- O construtor `00481040` carrega `duel/phase/phase.txt` e instala
  `005DBB2C`. Update `00481370` avança `obj+150h` de 8 em 8 até
  `0x200`. Draw `00481420` tem um ramo que lê esse contador enquanto
  abaixo de `0x200`. O predicado ocupado `004812B0` também testa se
  `obj+14Ch < 0x80`. São contadores separados para a troca e a fase.
  A atribuição dos frames e das variantes de cor ainda está em aberto.
- A vtable de carta `005DA868` aponta o Draw para `00418EF0`. Essa
  rotina lê escala e rotação, usa caminho direto em casos sem
  transformação e consulta a tabela x16.16 no caminho transformado.
  A orientação vista no vídeo e relatada pelo jogador motivou no
  YugiRE mais 180° para a fileira oposta, inclusive em defesa. Ainda
  falta ligar estaticamente o lado do dono à atribuição do ângulo
  no executável. A coluna de detalhes exibe a arte em pé.

## Auditoria de composição em 800 × 600

| Superfície | Geometria | Evidência |
| --- | --- | --- |
| Campo padrão | (230,0), 570 × 600 | `00449330` carrega `duel/field/field.txt`. A vtable `005DB060` chama Draw `00449850`. Em `004498AA–004498C0` o caminho estável passa Y=0 e X=`0xE6`. Dimensões do BMP. |
| Fundo lateral | (0,0), 230 × 600 | `detail.txt`, dimensões do DAT e alinhamento observado no vídeo. O fundo do cliente ocupa o canvas à esquerda do campo sem encolher. |
| Botões | X=221, Y=82/157/232/307/382/457, célula 40 × 75 | `phase.txt` codifica `1000 × X + Y`. Draw separa quociente e resto em `0048169C–004816BD`. `pha_s_*.bmp` mede 80 × 150, dividido em duas colunas e duas linhas em `0048165A–0048168C`. |

O campo e o fundo lateral são desenhados antes da faixa de fases. Essa
faixa cobre X=221…260, cruzando a junção X=230 conforme as posições do
manifesto. Antes, a faixa era reduzida a 28 × 50 e a lateral a 214 px,
deixando um vão artificial. A malha de diagnóstico foi retirada do
quadro limpo, assim como a moldura opaca dos avisos e a barra de log sobre
o campo. Menu, reset, opções de prompts e log ficam no overlay opcional.
Esc abre o menu, Backspace cancela a seleção, e a paginação da mão e
a confirmação do FLIP aparecem apenas quando cabíveis, nas bordas.
Esses controles e os painéis de LP/descrição ainda usam composição do
cliente. A mão adversária, o pivô dos avisos, os prompts e a distribuição
dos quadros de rotação são pendências de fidelidade.

As pilhas usam os centros recuperados em `005DB5B0` (deck) e
`005DB5C0` (cemitério), acessados por `0045C280` nas regiões 0D e 0E.
O deck adversário fica em (290,140). Com o verso de 50 px, sua borda
esquerda fica em 265, deixando quatro pixels após a faixa de fases.
O foco desta correção é a composição do campo padrão, sem aplicar
os ramos nativos de transição entre fundos.

## Mãos e saída da batalha

`0045C600` calcula `span = min((largura/2 + 8) × N, 512) − largura/2`
e grava a contagem da mão inteira. `0045C590` começa em `525 − span/2`
para o lado próprio ou `525 + span/2` para o adversário e soma ou
subtrai `trunc(span × índice / (N−1))` quando há mais de uma carta.
As âncoras Y=600 e Y=0 vêm de `005DB608`. Em `00418771–0041878B`, a
largura e a altura globais são inicializadas pela consulta virtual
`+38h` do recurso. O valor carregado nesse caminho não foi observado em execução.
Em 00:02.5 do vídeo, os cinco versos adversários medem cerca de
100 pixels no eixo horizontal e seus centros distam 103 px. Com cinco
cartas, a fórmula estática está no limite de 512 e o intervalo total
observado de 412 implica largura de cálculo de 200. Assim, a largura
de cálculo é derivada da fórmula e do vídeo. O desenho usa 100 × 145,
compatível com a escala de metade do verso de 200 × 290 do DAT e com
o recorte visível. A seleção final de subimagem e a escala exata em
Draw ainda não estão ligadas de ponta a ponta pelo código nativo.

As duas mãos da Alpha agora usam a distribuição inteira, sem
paginação. O canvas recorta as pontas das cartas junto às bordas.
A mão distante desenha só versos de cartas, girados para aquele
lado, e as origens de compra e descarte seguem as mesmas coordenadas.
A mão própria conserva a elevação de foco de 32 px e o balão
provisório de ações. A ordem exata de oclusão quando uma carta se
move entre a mão e o campo ainda requer captura equivalente.

O botão EP chama o comando de fim de turno também durante BP, como
observado pelo jogador. O caminho mantém as escolhas pendentes e
o descarte obrigatório do Core.

## Rotação dos botões na troca de jogador

`00481230` zera `obj+150h` quando muda o jogador. `00481370`
soma 8 até `0x200`, totalizando 64 updates. O Draw
`00481420` usa, para o botão de índice `i=0…5`,
`local = contador − 0x40 − 0x18 × i`. Se negativo, desenha
o quadro estático do lado anterior. Caso contrário usa
`8 − trunc(min(local, 0x40) / 8)` como coluna de uma tira
de 9 quadros de 40 × 75. O botão seguinte começa três updates
depois. O quadro 0 permanece até o contador global chegar a
`0x200`; então volta a usar o atlas estático.

Em `004815D0–004815D8`, a seleção de recurso soma `6 × (1 +
ladoRelativoNovo)` ao índice. Conforme a ordem de `phase.txt`,
isso seleciona `pha_b_*.bmp` para destino azul e
`pha_r_*.bmp` para destino vermelho. A leitura das tiras é
da direita para a esquerda em ambas as direções.

No cliente, Classic e Fast usam os 64 updates, a 30 ou 60 Hz.
Instant mostra diretamente o lado novo. A animação começa depois do
aviso de End e antes do aviso de Draw do novo turno. Essa integração
usa o relógio visual do cliente. O aviso nativo de troca de jogador,
solicitado separadamente por `00480BC0`, ainda não está integrado.

Uma captura da Alpha Fast a 800 × 600 e 60 FPS, com debug desligado,
encontrou o quadro intermediário 4 de todos os seis botões, nas duas
direções, com igualdade RGB em relação aos recortes do DAT. Em ambas
as viradas, as amostras apareceram a três frames de distância na captura.
Esse teste compara a saída raylib com as tiras, e não com uma gravação
quadro a quadro do executável.
