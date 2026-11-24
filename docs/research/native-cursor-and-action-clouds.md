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

## Integração revisada após duas capturas do Joey original

As capturas fornecidas pelo usuário mostram a mão/cursor sob o
balão de **Summon**, e que o balão acompanha o posicionamento do
mouse sobre cartas diferentes. Não é um botão estático centralizado
na carta: é uma variante visual do **mesmo cursor**.

O cliente agora usa o cursor original durante **toda a execução
gráfica, em toda a janela** (menu, cartas, deck e duelo, inclusive
área de letterboxing quando redimensionada). O ponteiro do SO é
ocultado uma única vez depois da inicialização raylib, restaurado
ao fechar o jogo. Desenhamos **exatamente um sprite por quadro**
depois da composição final da janela, usando o mouse em coordenadas
físicas da janela e o fator de escala visual:

- cursor normal: `cursor/cursor00.yga`;
- hover de monstro invocável: `cursor/cursor_e_summon.yga`;
- hover com Set válido: `cursor/cursor_e_set.yga`;
- hover de magia ativável: `cursor/cursor_e_act.yga`.

**Nunca desenhar simultaneamente** `cursor00` e
`cursor_e_summon`: isso duplicaria a mão/ponteiro.
Também não desenhar mais as nuvens de ação fixas no topo
das cartas. O arquivo `cursor01.yga` permanece catalogado,
mas sua ativação nativa ainda é desconhecida.

A nuvem só aparece quando a carta sob o mouse permite
uma ação de acordo com o `Joey.Core`. Não basta
que uma carta seja monstro ou magia: é preciso respeitar
fase, espaço de campo, invocação já usada e restrições
de ativação. Ao trocar de carta, o modo padrão é:
- monstro normal: **Summon**; botão direito alterna para
  Set se também permitido;
- magia com ativação e Set possíveis: **Activate**;
  botão direito alterna para Set;
- armadilha na mão: **Set** imediatamente; não oferece
  Activate nem permite alternar com botão direito,
  pois a ativação direta da mão é ilegal;
- nenhuma opção disponível: cursor normal, sem nuvem.

O fallback procedural **Discard** do sandbox continua
independente até existir uma variante nativa correspondente.
A detecção de hover usa a união da posição de repouso
e da posição levantada da carta para não deixar a nuvem
presa fora da carta ou oscilar durante o movimento.

O hotspot real (origem relativa à ponta do dedo), as
dimensões e a ordem de variação de `cursor00/01.yga`
continuam para calibração. O atual `PointerOrigin`
usa a posição do mouse como origem da textura; é
**provisório**, embora os YGAs e sua seleção sejam
originais. A textura é desenhada na janela inteira,
fora do alvo de captura 800 × 600; portanto F12
registra os nomes dos sprites ativos e o mouse lógico,
mas o PNG do canvas não contém o cursor.

`DatGraphics.Validate` continua verificando os YGAs;
`--inspect-visual-dat` inclui todos os 68 itens da árvore
`cursor/` sem exportar pixels. O jogo original não
é copiado nem incluído no repositório.

## Roteiro para validação com o original

1. Percorrer menu, lista, deck e duelo; o cursor original
   deve ficar sempre visível dentro da janela e nunca
   coexistir com a seta do SO.
2. Hover sobre uma carta de monstro e mover o mouse pela
   sua superfície: a nuvem deve acompanhar o ponteiro,
   não permanecer no centro da carta.
3. Repetir com armadilha na mão: deve mostrar **Set**
   por padrão e ignorar o botão direito. Com uma
   magia ativável, conferir alternância Activate/Set
   somente quando ambas forem legais.
4. Conferir cursor normal quando a carta não tem ação
   válida, e comparar ponto do dedo, transparência,
   origem e escala do sprite com a captura original.
5. F12 produz PNG/JSON do canvas (sem o cursor, que
   é desenhado na janela); registrar comparações do
   ponteiro com captura da janela inteira.

**Esta PR não altera regras no `Joey.Core` nem a badge
visual de 19/31**, que inventaria outras funções Draw.
O cursor é uma família adicional ainda fora das
31 funções do denominador. Só promover fidelidade
após capturas pareadas.

## Correção do texto transparente do balão (22/11/2026)

Captura do usuário: o balão Summon era branco e legível, mas as letras pretas
viravam buracos mostrando a carta sob o cursor. A causa era a transformação
incondicional de **RGB(0,0,0) em alpha=0** no decodificador YGA, criada para
certos efeitos que usam preto como color-key.

`NativeYgaSprite.Decode` agora recebe uma política explícita:

- `YgaTransparencyMode.SourceAlpha` para recursos `cursor/*.yga`:
  conserva o alpha BGRA do original, inclusive para pixels pretos opacos.
- `YgaTransparencyMode.BlackColorKey` para sprites de efeitos já existentes:
  mantém o comportamento legado de preto transparente, sem regressão global.

O teste com YGA sintético verifica preto opaco, preto de alpha 0 e a conversão
BGRA para RGBA. Os sprites proprietários não foram exportados nem versionados.
A validação visual ainda exige executar o jogo localmente e verificar Summon,
Set, Activate e outros idiomas caso esses recursos sejam habilitados.

## Cursor Attack sobre monstros do campo (22/11/2026)

**Observação fornecida pelo mantenedor:** durante a Battle Phase, o cursor
apresenta o balão **Attack** quando o monstro é elegível a atacar.
O DAT já contém `cursor/cursor_e_atk.yga`; a função
`NativeDuelCursor.ActionSprite(NativeCursorAction.Attack)` já apontava
para esse recurso. A implementação do ponteiro global, porém, só escolhia
balões para a mão.

`GlobalCursorScreen.ActiveWindowCursorSprite` agora detecta hover
nas zonas de monstros do **jogador visível/ativo** e solicita
`NativeDuelCursor.FieldMonsterAttackAction`. O gate usa
`DemoDuel.CanAttack(zone)`, que testa estado lógico de Battle,
monstro existente, não usado no turno, posição de ataque, face-up e
janelas de ações bloqueadas. A nuvem não aparece enquanto já existe
um alvo de ataque sendo escolhido (`attacker >= 0`), nem durante
animações, troca de fases, reação/carta pendente ou turno da CPU.
Continuamos desenhando **um único cursor original** após o canvas,
sem segundo ponteiro, sprite fake ou ajuste novo de pixels.

**Limite importante:** isso reproduz a elegibilidade da Alpha pelo
Core e a observação do usuário, **não** prova o dispatcher real do EXE,
a sua precedência na escolha de ações nem o hotspot. O cursor de
mudança de posição continua pendente; não o emulamos neste commit.
Testes determinísticos verificam os casos válidos e inválidos.
