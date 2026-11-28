# V09/V10 — seleção de ações e pedidos de cursor

Fonte privada SQLite v2, SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check=ok; fingerprint EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta decompilations, refs, pointers, strings, visual_evidence_memory e
visual_evidence_instructions. Os alvos do roteiro 26 não têm assembly
indexado de entrada; fórmulas abaixo são leitura de decompilação, não
assembly confirmado.

## Produtor de opções

0044DBC0 zera 005F2FDC/005F2FE0, obtém coordenadas pelo objeto
de input e usa hit-test 0041E3D0. 005BEEE0 lê a palavra contextual
via 00418B50, ou usa 0045C3F0 quando não há carta sob o cursor.
Publica lado/zona/índice em 00A55044/48/4C e reúne bits de entrada.
005A0CD0 cancela ao falhar 005BEED0 ou entrada bit 2, reinicia
ao mudar o trio (005B88D0), recalcula por 005A0090→005A00C0→
005A0140→005A0150. Zona 0 (monstros) chama 005A0530.
005A00C0 escreve máscara em 00A57806 (metade alta de 00A57804).

005A0530 diferencia lados, fase e flags do ocupante. Caminhos
de fase 2/4 consultam 0056B440/0056B5A0 e flags 00A55D7A
(com stride 0x90/0xD44). Se flag 2 não está setada, o ramo
pode ofertar 0x20 e 2; se está, retorna 0x20 ou 0x40
dependendo do bit 1, antes da opção adicional 8.
Fase 3 consulta máscara 00A55D70 e 0056B730: opção 4.
0059FF00 executa 00575210(...,0,0,1) para action 5 ou 6.
Isto liga as duas opções de mudança de posição à lógica
de ações do campo, mas não prova qual imagem corresponde
a cada code. Não duplicar estes checks no Core.

## Ciclo e pedido visual

005B8910: se máscara=0 cancela e chama 005BF4B0; na entrada
procura o primeiro bit permitido seguindo tabela de 13 dwords
005DD980..005DD9B3. Seleção fica nos bits 3..6 de 00A57804.
Enquanto ativa, entrada 0x400 (inclui remapeamento de 0x100
quando 005BEEB0 retorna não zero) percorre a tabela circular,
pulando opções indisponíveis. 005AA480 é consultado para
confirmar a ação. Não identificar 0x400 exclusivamente
como clique direito sem rastrear input.
Se action=12 usa reset; outras chamam 005BF490(action).
005BF490 escreve action+1 em [005F33E4]+108h e byte 1 em
+10Ch; 005BF4B0 escreve zero e byte 1. Xref 0040F777:
0040F670 inicializa 005F33E4 com objeto +27Ch e vtable
005DA594. Não confundir campo de pedido com índice de textura.
A string 005EB6C0 (data/cursor/cursor_?.txt) existe em strings
mas não tem xref direto no índice de refs. Ainda falta localizar
o consumidor de +108h, hot spot e a associação ao manifesto.

NativeCursorActionSelector modela somente o ciclo e pedido
raw, com tabela injetada; testes usam ordem sintética.
A exigência de permutação e recuperação de seleção inválida
são salvaguardas do adaptador, não uma confirmação da tabela
real. Não ligar ao dispatcher de App enquanto faltarem a
tabela, a ABI, o vínculo code→asset e os offsets. O cursor
atual segue inalterado e usa a autorização do Core.

## Próxima evidência

Roteiro 26 (Ghidra): assembly dos cinco métodos, 52 bytes da
tabela 005DD980 e 20 bytes da vtable 005DA594. Verificar
alvos da vtable primeiro no SQLite após importar. Manifesto
cursor_e.txt local, ordem, campos e dimensões sem pixels.
Nenhuma nova rotina Draw contabilizada. V09/V10 Partial:
investigação/modelo, não integração ou Verified.

## V09/V10.1 — manifesto DAT e nuvens legais de posição (25/11)

Consulta efetiva ao SQLite v4-visuals, SHA256
2085bc65dbc79fb2ec64645d2536bf0b56dde7bfedf4d173e32acb060b43722c,
quick_check ok. A coleta 26 é citada como recebida em VISUALS.md,
mas as tabelas visual_evidence_memory/instructions deste snapshot
não incluem 005DD980, 005DA594 ou os cinco alvos do roteiro.
Não afirmar prioridade nativa verificada; localizar a coleta 26
já executada antes de solicitar uma nova. Releitura das
decompilações 005A0530, 0059FF00, 005B8910, 005BF490/4B0,
0040F670 e 0040FED0. Pointers confirma 005DA5A0 -> 0040FED0
(destrutor), não o consumidor de +108.

visual_dat_manifests contém cursor_e.txt (611 bytes, CP932).
A ordem real (índice zero-based) é:

| Índice | Sprite (inglês) | Campo textual | Dimensão |
| --- | --- | --- | --- |
| 0 | cursor_pow.yga | ausente | 63x63 |
| 1/2 | cursor00/01.yga | 007014 | 64x64 |
| 3 | cursor_e_summon.yga | 007014 | 64x64 |
| 4 | cursor_e_flip.yga | 007014 | 64x64 |
| 5 | cursor_e_atk.yga | 007014 | 64x64 |
| 6 | cursor_e_act.yga | 007014 | 64x64 |
| 7 | cursor_e_set.yga | 007014 | 64x64 |
| 8/9 | cursor_e_setdef/setatk.yga | 007014 | 64x64 |
| 10 | cursor_e_draw.yga | 007014 | 64x64 |
| 11 | cursor_e_surrender.yga | 043015 | 64x64 |
| 12 | cursor_e_fusion.yga | 007014 | 64x64 |
| 13/14 | cursor_ssummon_a/d.yga | 007030 | 80x80 |
| 15 | cursor_e_over.yga | 007014 | 64x64 |

Comentários japoneses distinguem ataque, flip e posição
de defesa/ataque. O campo textual deve preservar zeros à
esquerda; não provar hotspot ou fórmula por sua aparência.
O sprite revive existe no DAT, mas não consta neste manifesto.
NativeCursorManifest fornece parser/validador isolado para
a ordem, parâmetros e dimensões atestadas; não é o dispatcher.
A ligação ao preflight de DatGraphics.Validate não foi aplicada
nesta fatia (operação bloqueada pelo ambiente).

005A0530 oferece bits de posição nas fases 2/4 e bit de
ataque na fase 3; 0059FF00 encaminha códigos 5/6 à mesma
operação de posição. NativeDuelCursor.FieldMonsterPositionAction
é uma adaptação explícita: usa CanChangePosition e estado do
monstro do Core, Flip para oculto, SetAttack para defesa
revelada, SetDefense para ataque revelado. GlobalCursorScreen
usa o mesmo hit-test e gates existentes, suprime seleção de
alvo e conserva Attack na Battle Phase. Não altera execução
de comandos nem declara a prioridade de múltiplas ações
nativas; hotspot continua provisório. Testes cobrem 32
combinações de elegibilidade, inversão para diagnóstico,
16 dimensões e rejeições do manifesto. Smoke desktop e CI
pendentes; nenhuma nova Draw no inventário.

## V09/V10.2 — estado pressionado e onda de choque (28/11)

Observação do mantenedor no original: MouseDown troca o ponteiro e
cria uma bolha. Reconsulta do manifesto no SQLite v14: comentários
originais `cursor00.yga // OFF`, `cursor01.yga // ON`,
`cursor_pow.yga // 衝撃波` (onda de choque); 64x64, 64x64, 63x63.
A tabela de prioridade e vtable do roteiro 26 estão presentes no v14:
005DD980 = [2,10,11,3,0,1,4,5,6,7,12,8,9]; 005DA594+10 = 0047FE90.
A branch histórica visuals/V10-cursor-hotspots-followup documenta
assembly 0047FEF3..0047FF15: pedido não zero escolhe pedido+2;
zero escolhe 1 + variante do objeto de input. 0047FF15..0047FF35
separa packed hotspot por 1000, 004800D4..004800F5 subtrai antes
do blit. O snapshot v14 não contém a janela de assembly do consumidor;
a atribuição MouseDown ao bit nativo específico continua aberta.

Cliente: adaptador explícito Raylib IsMouseButtonDown(Left) seleciona
cursor01 somente quando não existe ação específica; preserva os gates
Core e ação prioritária. Menu/duelo/letterbox seguem o renderer global,
sem duplicar a mão. Não modifica o hotspot provisório nesta fatia.
Sem bolha fake, sem fórmula de fade/escala inventada. Roteiro 77 exporta
as janelas do consumidor e input para localizar produtor e lifecycle de
cursor_pow. Validação desktop e CI pendentes, badge inalterada.

## V10.3 — ciclo da onda de choque

Coleta 77 e disassembly do PE de fingerprint
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
A confirmação do mantenedor cobre o OFF/ON do cliente após #281.

- **Entrada, assembly.** `0054B030` mapeia `0x201/0x202` (esquerdo)
  no byte +5 e `0x204/0x205` (direito) no +4. `0054B100` atualiza
  o par atual/anterior. O cursor chama `0054B240` em `0047FF62`:
  anterior esquerdo zero, esquerdo atual diferente de zero e +1C zero.
  `0047FF75` exige o flag do cursor +10D. No mesmo objeto, +10
  governa toda a passagem. Segurar por várias amostras e soltar
  não criam outra onda. Um segundo clique que atravesse outra borda
  amostrada cria outro objeto, inclusive enquanto o primeiro existe.
  O botão direito usa outro byte.
- **Origem e hotspot, assembly/DAT.** `0040F670` instala o cursor no
  objeto de aplicação e arma +10D. `0047FFA7` passa as coordenadas
  lógicas correntes do input para o construtor `00451F70`, que as
  guarda no objeto. A onda é ancorada no clique e fica na mesma
  posição quando o ponteiro se move. A subtração do hotspot em
  `004800D4–004800F5` governa o blit do ponteiro. A branch histórica
  `03ae747` documenta os hotspots (7,14), (43,15) e (7,30) e não
  foi mesclada nesta investigação.
- **Ciclo, assembly.** Vtable `005DB354`, Update `00450FB0`,
  Draw `00451FB0`. Contador nasce em 0 e soma `0x10` por Update.
  Ao alcançar `0x100` no 16º Update, +4 é limpo. O Draw seleciona
  a entrada 0 (`cursor_pow.yga`, 63×63), trunca o tamanho
  `(dimensão * contador) >> 8`, centra com metade inteira do
  tamanho escalado e passa intensidade
  `min(255, cos16_16[(contador / 2) & 0x1ff] >> 8)`.
  No primeiro Update que pode desenhar a onda, o tamanho é 3×3.
  No 15º, é 59×59. A intensidade é um argumento do compositor.
- **Ordem, assembly.** `0047FEB0/0047FEBC` executam Update e Draw da
  máscara 4 antes do bloco de criação e do blit do ponteiro
  (`0048010C`). A primeira oportunidade de Draw para uma nova onda
  é a passagem seguinte, após o Update. O fluxo está no cursor da
  aplicação. A amostra pareada em menu, deck e duelo deve confirmar
  as condições de visibilidade de cada tela.

O perfil `cursor` captura mensagens de botão, estado de amostragem e
IDs de passagem/objeto nos três cenários. A concentração de cliques na
mesma amostra, o lote de ondas simultâneas e os pixels do compositor
ainda dependem de captura. [Roteiro Windows](../../scripts/pending/README.md#v09v10-v18v31-e-v32--validação-em-execução).
