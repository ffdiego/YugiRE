# V04/V06 — preparação de dano, estado de combate e Draws ausentes

Fonte read-only: joey-knowledge-visual-decoded.sqlite,
SHA256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta direta às decompilações persistentes 00413BC0, 004140B0,
00414110, 00408470, 0047C310, 0047C4F0, 0047C540, 0047C550,
0047C560 e 004806F0; as duas tabelas de decompilação não têm
004142C0 nem 0047C660. Nenhuma nova evidência assembly nesta fatia.
As notas históricas sobre o Draw não substituem instruções reais.

## V04.5 — despacho da espada observado em Proton

Na demonstração do EXE de referência em Proton 9.0, Xvfb a 800×600
e llvmpipe, o roteiro 54 capturou um ataque do slot adversário 0
ao slot próprio 1 e um ataque direto do slot próprio 1. O centro de
impacto foi (447,352) no primeiro caso e (525,64) no segundo.
Cada ataque teve uma amostra de mira no contador 128, sete pedidos
da espada inteira de 144 a 240 e 16 pares a partir de 256.
Os ponteiros de origem conferiram com o banco ativo do manifesto.

| Chave | Recurso | Origem | Amostra de chave RGB565 | Destino |
| --- | --- | --- | --- | --- |
| 0 | `atk_icon.bmp` | 3 (RGB565) | (0,0) → `0x07E0` | 3 |
| 1 | `atk_icon2.bmp` | 3 (RGB565) | (0,0) → `0x07E0` | 3 |
| 2 | `atk_eff.yga` | 10 (RGB565 com alpha) | (0,0) → `0x0000` | 3 |

No contador 256, o Draw pediu o impacto com escala zero e a lâmina
parcial com escala 65536, em 16.16. O impacto ficou sem chamada ao
compositor nesse passo. O primeiro pedido de impacto que chegou ao
compositor foi o de 272, com escala 8192. A lâmina parcial chegou
ao compositor de base `0051EBD0` já em 256. Daí até o contador 384,
as chamadas com escala positiva usaram esse compositor. Em 400,
quando a intensidade passou a 224, os dois passes passaram por
`00422E30` e seguiram nele até o último par, no contador 496.

A validação conferiu a ordem, o manifesto, as escalas, as intensidades,
os formatos e as entradas dos compositores. O valor no campo de
chave do YGA registra a amostra da superfície. A contribuição do
alpha do YGA, os vértices efetivos e os pixels de saída ficam para
capturas específicas. [Roteiro 54](../../scripts/regression/README.md#54--v04-despacho-da-espada-em-proton).

## V04 — espada

00413BC0 carrega duel/effect/attack.txt, três recursos, camada
0x108. O DAT histórico (native-visual-contract.md) confirma
atk_icon.bmp e atk_icon2.bmp, ambos 35x71, fundo verde; segundo
contém só parte da lâmina. O cliente já troca em t>=256, mas
não recuperamos a instrução do Draw que seleciona cada recurso.
00414110 Update estado 3 soma 16 até 128; estado 4 soma 16
até alcançar 512 e muda para 5; estado 6 soma 16 até 512,
zera contador e muda para 7; estado 7 soma 8 até 256, zera e
muda para 8. 004140B0 prepara deltas e flags dos dois lados,
zera contador e seleciona 7 se alvo -1, bit 2 do adversário
ou delta do adversário não zero; caso contrário seleciona 6.
Não atribuir nomes definitivos às flags. O estado 4 sozinho
não representa todo o combate. Nenhuma nova escolha de sprite,
pivô, opacidade ou duração foi introduzida no renderer.

## V06 — seleção da âncora de LP e hold de consumo

00414110 state 7, counter 0: para cada lado (0,1) cujo delta
de dano em +28/+2C não é zero, chama 0047C4F0(-delta,slot,1).
O slot é +1C para o lado atacante (+18), +24 para o outro
lado. Há override para -1 quando +24 == -1 ou quando as
flags do lado (+30/+34) contêm bit 3 ou bit 4. Logo não se
trata de uma escolha universal do defensor ou de ponto médio.
O slot -1 não prova as coordenadas: a Draw ausente é quem
interpreta +2C. 00408470 passa -1 para o dano/cura geral
no stage 0 (hold 1) e stage 3 (hold 0).

0047C4F0: se +30 !=0, chama 0047C540 (limpa só +30) e
retorna; ignora novo delta e slot, não reinicia rampa/som.
Caso contrário escreve +2C=slot, +34=0, +28=delta, +30=flag;
som 10000009 para negativo, 1000001F para não negativo.
0047C560 avança +8 a rampa até 256 mesmo quando +30 está
setado. Depois, enquanto bloqueado, não consome delta;
quando livre, consome magnitude 100/10/1 por Update.
0047C550 cancela delta e hold sem resetar LP visível, rampa
ou flash. Não confundir esse flag com IPresentationGate:
é um estado interno da própria apresentação.

NativeLifeDeltaRequest porta o seletor de slot e o input
nativo, recebendo fatos do chamador, não calculando dano.
NativeLifePointPresentation agora possui entrada opt-in
RequestNativeChange e CancelNativeChange, além do adaptador
legado SetLogicalLife (inalterado para os chamadores atuais).
ACTIONS/A03 decide quando preparar/liberar/cancelar e como
capturar o slot e os dados de combate antes da mutação; não
usar simultaneamente ambos os caminhos para uma mesma mudança.
Sem alterar App, Core, filas ou gate. Não mudar a âncora
provisória no fluxo real antes de recuperar 0047C660; flags
brutas não são automaticamente flags equivalentes do Core.

Teste sintético cobre ambos os lados, bits 3/4, ataque
direto, dano zero, hold após rampa, segundo pedido que
apenas libera, cancelamento, som e compatibilidade legada.
Cobertura permanece 20/34; V04/V06 já contados Partial.

### V06.2 — slot de combate pela fila

Consulta ao snapshot cumulativo v7 confirma o seletor de `00414110` e o armazenamento dos argumentos em `004140B0`. A decompilação de `00413BA0` mostra o repasse dos quatro argumentos a `004140B0`. O Draw `0047C660` lê `+2C` em `0047C69A` e chama `0045C140` em `0047C6AB`. Os centros lógicos usados pelo App são os da coleta 33.

No C#, o produtor de dano de combate comum guarda o slot da reserva do combatente do lado que perdeu LP. O App usa esse slot ao observar o comando aplicado e inicia a rampa já existente. O vínculo entre dano comum e o caminho nativo sem override de slot é uma inferência. Dano perfurante mantém o centro da fileira de magias/armadilhas enquanto os produtores das flags `0x08/0x10` permanecem em aberto. O hold nativo continua fora desse recorte.

## Próxima evidência

21-attack-life-draw-ghidra.sh solicita os dois Draws,
janelas dos produtores de argumentos e vtables 005DA710/
005DBAA4. Não repetir os bundles 01–04. Importar o ZIP no
SQLite cumulativo com import_visual_evidence.py. Depois
confirmar os recortes/ordem e pivô da espada, o significado
do slot -1, posição da âncora de slot e argumentos de
004806F0. O cliente deve preservar fallback atual até
então, sem novo deslocamento estimado.


## Coleta 21 recebida — implementação incremental

Fonte: joey-knowledge-visual-cumulative-2026-11-24-v2.zip,
SQLite SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
21 bundles e quick_check ok. Evidência de 004142C0 e 0047C660 em
visual_evidence_functions e visual_evidence_instructions,
ghidra-attack-life-draw-contract.zip. Não repetir roteiro 21.

**Espada:** 004145BF..004145F5: state 4, t<=128 vira na origem;
t=144 pede som 10000006 no Draw. 00414601..00414689 interpola
centro inteiro com C(t-128)>>8, recalcula atan2 usando o centro
interpolado (truncamento altera ângulo), não o centro inicial.
00414694 usa chave 0, escala 65536, caminho opaco 00422840.
0041470C..00414772, t>=256 e <512: chave 2 (impacto), escala
(t-256)*512, intensidade bruta 2*(512-t), centro do alvo.
004147B0..00414800: depois chave 1 (espada parcial), escala
65536, mesma intensidade, ângulo do vetor original até alvo.
00422C00 encaminha intensidades >255 ao compositor opaco.
Sampler existente agora recalcula ângulo de voo e expõe chaves,
intensidade bruta e pedido de áudio, preservando clamp de Raylib.
Mantém o bypass legado alreadyAimed e aproximação atan2 do cliente;
constantes x87 005DA708/728/730 ainda não lidas, não afirmar paridade
de arredondamento. Nenhum novo relógio, disparo de áudio ou gate.

**LP:** 0047C69A..0047C6AB chama 0045C140(side,5,2) se slot<0,
ou 0045C140(side,slot,0) se slot>=0. A região 5 + slot 2 resulta
em zona S/T central. 0047C6C8..0047C6FA escolhe a chave 6 para
dano e 7 para cura, quantidade de colunas do manifesto (zero
vira 10). 0047C6FD..0047C748 mede largura total por
00480230(delta,1,0), direita = centroX + largura/2, topo =
centroY - altura/2, ambas divisões truncadas. Passa 2*rampa
e flag de sinal 1 para 004806F0. Não alinhar o final do número
ao centro. NativeLifeHud e DrawLifeFloatingDigits agora usam
esse seletor/centralização no duelo real, com a geometria de
campo do cliente existente. O seletor está confirmado em assembly;
os bytes da tabela de zonas S/T 005DB4E8 ainda não foram coletados,
logo a geometria absoluta do cliente segue sujeita a comparação.
A03 fornece slot de combate e mantém responsabilidade exclusiva
sobre preparação/liberação de LP; SetLogicalLife legado usa -1.
Amostras de DAT e capturas pareadas continuam necessárias.
Cobertura permanece 20/34, nenhuma promoção para Verified.

## V04.2 — geometria do blit rotacionado (assembly confirmado)

SQLite v3 extraído e consultado localmente: SHA256
9953a4eb3af861b034f91863aa99adfcaa83d7d34c671eb8f20315cfa37948ed,
integrity_check=ok, fingerprint EXE igual ao acima. O bundle
ghidra-fusion-remaining-contract.zip, batch-000.txt, contém as
janelas 00422840 e 00422C00. Não repetir essa coleta.

0042284E..00422896 e 00422C47..00422C96: virtual +38 devolve
dimensões; cada eixo usa MulDiv(dimensão-1, escala, 65536).
Escala zero ou resultado zero retorna sem desenho. A textura
é descrita separadamente pelo retângulo +18/+1C/+20/+24,
com endpoints direito/inferior decrementados.
004228CD..00422A02: C=cos[a&511], S=cos[(a-128)&511];
offsets X=-trunc(w/2), trunc((w+1)/2), Y análogo.
Cada vértice (x,y) relativo ao centro fornecido pelo chamador
torna-se (cx+((x*C-y*S)>>16), cy+((y*C+x*S)>>16)).
O SAR é aritmético, inclusive para valores negativos. Os
quatro pontos fonte/destino são cíclicos TL,TR,BR,BL.
00422C00..00422C46: intensidade >=256 encaminha ao caminho
base; abaixo disso repete a geometria e chama 00422E30.
Isso não significa que a composição base ignore color key.

DAT v3 confirma attack.txt (chaves 0/1: atk_icon.bmp e
atk_icon2.bmp, ambas 35x71; 2: atk_eff.yga; 3: dmg_ref.yga).
Para 35x71, escala 65536 e ângulo 0, os offsets inclusivos
são (-17,-35),(17,-35),(17,35),(-17,35). O primeiro quadro
do impacto (t=256) tem escala zero, logo chave 2 não desenha.
A âncora do ataque direto também foi confirmada: 004142FC
lê 005DB570=525 (memória v3); 00414302..0041430E usa Y=64
se atacante == perspectiva local e Y=536 caso contrário.
Isso não altera o destino de ataques a monstros.

NativeRotatedQuad porta a geometria no domínio de escalas
não negativas e dimensões limitadas, recebe a tabela de cosseno,
sinaliza degeneração e não substitui o raster. Testes sintéticos
cobrem 0/90/180/45 graus, wrap, escala, crop e ausência de
blit. Não há integração com o Draw normal nesta fatia:
ângulo x87 (005DA708/728/730), formato/key de superfície e
comparação gráfica continuam em aberto. Nenhuma mudança ao
Core, gate, scheduler ou inventário. Sem crédito Verified.

## V04.3 — operandos x87 e perspectiva

Reconsulta local somente leitura do SQLite v3 (mesmo SHA256,
quick_check ok, fingerprint EXE confirmado). Assembly do Draw
004142C0 confirma, com dx=targetX-originX, dy=targetY-originY:

- 00414812..0041485C, quando targetSide == (~localPerspective & 1):
  atan2(dx,-dy) * D[005DA730] / D[005DA708], convertido por
  005C3E40, depois trunc(raw * counter / 128).
- 0041485E..0041489B, caso contrário:
  atan2(dx,dy) * D[005DA728] / D[005DA708], convertido pelo
  mesmo helper, depois 256 + trunc(raw * counter / 128).
- 0041465E..00414689: voo, dx=targetX-currentX,
  dy=targetY-currentY, atan2(dx,-dy) com D[005DA730].
  O centro corrente já foi truncado na interpolação.
- 00414777..004147AE: impacto usa vetor origin→target e
  D[005DA730]; não reaproveitar o último índice do voo.

FPATAN recebe ST1=dx e ST0=±dy. CDQ/AND 0x7F/ADD/SAR 7
trunca o produto signed em direção a zero; não confundir com
SAR da transformação geométrica. A preparação é definida em
0..128 e a origem do branch é o lado do alvo, não o sinal de dy.

NativeSwordAngle encapsula apenas o roteamento de operandos e
interpolação; delegate explícito fornece a conversão, sem
presumir pi, fator, double versus x87 estendido, ou arredondamento.
Sete checks com retorno sentinela validam os dois lados e a
distinção voo/impacto, não a conversão em si. Nenhuma alteração
ao sampler/renderer normal, que conserva a aproximação existente.

Lacuna efetivamente consultada: nenhum intervalo de
visual_evidence_memory contém 005DA708, 005DA728 ou 005DA730
(o bloco 005DA710:16 não cobre nenhum dos três). Relatórios
brutos têm referências por instrução, sem bytes dessas constantes.
005C3E40 tem decompilação identificada como CRT __ftol e
ROUND(in_ST0), mas nenhuma instrução indexada. Não inferir
truncamento/nearest pelo nome nem pela representação do Ghidra.
Roteiro 32 coleta somente 24 bytes e helper; sem repetir 21.
CI anterior V04.2 passou (run 45543687589); novos checks aguardam CI.

### V04.4 — constantes e conversão x87 (27/11)

A coleta 32 foi executada no projeto Ghidra local sobre o fingerprint
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Os blocos `005DA708:8` e `005DA728:16` conferem com a seção
`.rdata` do PE lida independentemente com `objdump`. Os doubles
IEEE 754 little-endian são `0x400921FB54442D18` (π em binário64)
em `005DA708`, `-256` em `005DA728` e `+256` em `005DA730`.

A rotina `005C3E40..005C3E66` salva a palavra de controle x87,
força RC para `11b` com `OR AH,0x0C`, converte e retira ST0
com `FISTP qword` em `005C3E59`, restaura a palavra de controle
em `005C3E5C` e retorna o inteiro de 64 bits em EDX:EAX.
O resultado finito representável é truncado em direção a zero.
A leitura é estática. A precisão de FPATAN em fronteiras de índice
e a composição do quadrilátero ainda precisam de validação dinâmica.

O bundle privado foi importado com os relatórios `batch-000.txt`
e `memory-read.txt`. [Snapshot v8](joey-knowledge-sqlite.md#complemento-cumulativo-v8--conversão-x87-27112026).

## V06.1 — trajetória e fronteiras por glifo (25/11)

Fonte privada v4-visuals, SQLite SHA256
2085bc65dbc79fb2ec64645d2536bf0b56dde7bfedf4d173e32acb060b43722c,
quick_check ok, fingerprint esperado. Coleta 33 confirma centros
005DB560/005DB510. Âncora geral (525,460)/(525,140); combate usa
o centro do slot afetado, não o centro entre fileiras. A03 ainda
precisa fornecer slot no fluxo real, que continua com -1 legado.

004806F0 não tinha janela própria, mas suas 216 instruções
(004806F0..0048096C) estão integralmente nas janelas anteriores
00480200/00480230/004803C0/00480690 do bundle 502c6177c5e694e384d03ead56f7e9b435758b22abbd4de253e3e78fcb8bd2db,
batch-001.txt, e também no bundle beeb78df561a42a33177d4c098368bd33d07b2901f2923b77eb391858c2e6fcd.
Consulta por address, não apenas window_address. Sem nova coleta.

- 004806F8..00480708: counter global <=0 retorna sem desenhar.
- 0048070B..00480736: counter >=256 encaminha para 004803C0,
  caminho estático que não calcula atraso nem deslocamento.
- 0048073D..004807A3: módulos de magnitude em ordem inversa;
  sinal 11 negativo, 10 positivo quando flag ativa, só com >=12 colunas.
- 004807F2..00480803: atraso inteiro 64/(n-1), ou zero se n<2.
- 00480858..00480873: decrementa X pela largura; JL pula apenas
  phase <0. Phase ==0 de um glifo posterior JÁ desenha em Y base.
- 00480879..004808A9: phase<128, y=top-(C[(2*phase-128)&511]*h >>16).
- 004808AB..004808FB: 128<=phase<192, amplitude h/4,
  índice (4*phase-128)&511; >=192 usa top.
- 00480914: virtual +8, sem alpha interpolado; color key/raster
  ainda precisam comparação. 00480917..0048094D reduz phase por atraso.

DAT: number.txt entradas 6/7, damage00/01.bmp, 192x40, 12
colunas, 16x40 por glifo. 0047C748 passa 2*rampa (0..512).
O sampler anterior já tinha as curvas e recortes; omitira phase
individual zero e não distinguia o caminho estático do animado.
O App limitava artificialmente o contador a 256. Agora preserva
2*rampa; >=256 remove atraso, sem mudar a aparência estacionária.
Não altera relógio, fila, regras, som ou hold. Phase é metadado,
não opacidade. Cinquenta checks adicionais incluindo todos os
32 updates da rampa e os 12 centros (dois gerais, dez monstros).
Referências numéricas geradas independentemente em Python com
a tabela privada de 512 valores: zero divergências da fórmula
Math.Cos truncada (comparação Python equivalente, não execução .NET).
Exemplo -10: t32 glifos 0 e 1 em y=-28 e 0; t64 0/1/- em
y=-40/-28/0; t160 primeiro glifo em -10. C# CI pendente, sem
SDK local. Não declarar Verified; falta slot de batalha via A03,
comparação gráfica/cadência e composição.
