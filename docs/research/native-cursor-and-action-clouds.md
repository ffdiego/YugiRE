# Cursor do duelo e balões de ação — primeira integração visual

Data: 21/11/2026. **Status: Partial / sem captura pareada**.
O cursor e os balões de ação são uma família **distinta** dos
efeitos de invocação/Set desenhados no campo.

## Evidência no data.dat e no joey_pc.exe

O índice original `docs/evidence/data.dat-index.json` lista
**68 entradas em `cursor/`**. Entre elas:

- `cursor/cursor00.yga` e `cursor/cursor01.yga`;
- `cursor/cursor_e.txt` (manifest de idioma inglês);
- `cursor/cursor_e_summon.yga`;
- `cursor/cursor_e_set.yga`, `cursor_e_setatk.yga`,
  `cursor_e_setdef.yga`;
- `cursor/cursor_e_act.yga`, `cursor_e_atk.yga`,
  `cursor_e_draw.yga`, `cursor_e_flip.yga`,
  `cursor_e_fusion.yga`, `cursor_e_rev.yga`,
  `cursor_e_surrender.yga` e `cursor_e_over.yga`.
- Há variantes `f/g/i/s` para outros idiomas.

Essas texturas são **os rótulos/balões de cursor que
aparecem ao escolher ações em cartas**, e não os
efeitos que acontecem após a ação ser confirmada.

O SQLite das 6.518 funções contém a string
`data/cursor/cursor_?.txt` em `005EB6C0`; os callsites
dessa string não foram reconstruídos com segurança.
`00566F00` controla `ShowCursor` por flags internas do
contexto Win32, com chamadores `004A8BE0`/`00566D00`.
`0054AE20` obtém `GetCursorPos` e transforma coordenadas
da janela quando necessário. Isso confirma que o
original gerencia o cursor do Windows e suas coordenadas,
mas **não** revela ainda o hotspot exato, a escolha
entre `cursor00/01`, sua taxa de troca, nem o Draw dos
balões de ação.

Não confundir com:

- **Set de campo:** `0044EFD0/0044F370/0044F390`,
  usando `duel/effect/set.txt`, `eff_set_b/y.bmp`
  e as estrelas.
- **Invocação normal:** `0044F630/0044F9F0/0044FA10`,
  usando `duel/effect/summon.txt` e `mini/*.bmp`.
- **Invocação especial:** `0044FCC0/004500E0/00450100`
  e `sp_summon.txt`.
- **Busto signature:** `00414A20` e
  `bust_up/summon.txt`, `summon2.txt`.

O usuário descreveu os balões do cursor como uma espécie
de **nuvem** sobre a carta. A inspeção estática identifica
os sprites pelo nome, não pelo formato visual (ainda
não houve captura do atlas original neste ambiente).

## Implementado no Joey.App

`NativeDuelCursor` mapeia o idioma inglês por ação
e possui um contrato puro de posição/limites testável.
O balão procedural da mão foi substituído pelas texturas
originais do DAT:
- monstro + ação normal → `cursor_e_summon.yga`;
- monstro ou magia/armadilha + Set → `cursor_e_set.yga`;
- magia/armadilha + ativação → `cursor_e_act.yga`.

O botão de descarte de fim de turno continua usando o
painel local, pois não foi identificada ação equivalente
sem ambiguidade entre os YGAs. As demais ações
(`atk`, `flip`, `fusion`, `rev`, etc.) estão
catalogadas para integrações posteriores.

A interface calcula a área clicável a partir da **dimensão
real da textura** (não mais do retângulo genérico
de 50 × 27). O posicionamento atual é
`x = centroDaCarta − larguraSprite/2`,
`y = topoDaCarta − alturaSprite − 5`.
É **ancoragem provisória do cliente**, não coordenada
nativa verificada.

O ponteiro do sistema é escondido apenas durante o
duelo quando o `cursor00.yga` original tem tamanho
plausível (cada dimensão entre 1 e 128 pixels).
O `cursor00.yga` é desenhado no canvas
**800 × 600**, após os outros overlays, na posição
do mouse lógico (origin/hotspot superior esquerdo
**provisório**). Ao sair do duelo ou mover para a
área de letterboxing, o ponteiro do Windows reaparece.
`cursor01.yga` fica catalogado, mas não alternamos
entre as imagens sem comprovar a condição nativa.

`DatGraphics.Validate` passa a decodificar os YGAs de
cursor para detectar formatos inesperados sem abrir a
janela. `--inspect-visual-dat` também examina
`cursor/`, inclusive o texto original `cursor_e.txt`,
e registra as dimensões sem exportar os pixels.
Os recursos de jogo não são versionados no Git.

## Roteiro para validação com o original

1. Ficar sobre carta de monstro, observar balão de
   Summon. Alternar botão direito para Set; repetir
   em magia/armadilha e comparar com Activate.
2. Conferir posição da ponta/nuvem, tamanho nativo
   das texturas, transparência, efeito de hover,
   ordem de render e deslocamento com a carta.
3. Capturar o cursor em repouso e pressionado;
   determinar hotspot e a função real de
   `cursor00/01.yga`, inclusive se o executável
   original esconde o cursor Win32 somente em
   tela cheia ou também em janela.
4. Usar F6/F7 para inspecionar quadros estáveis e
   F12 para PNG/JSON de 800×600. Comparar
   ao lado de uma captura do jogo original.

**Esta PR não altera regras no `Joey.Core` nem a badge
visual de 19/31**, que inventaria outras funções Draw.
O cursor é uma família adicional ainda fora das
31 funções do denominador. Só promover fidelidade
após capturas pareadas.
