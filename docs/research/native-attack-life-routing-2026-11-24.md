# V04/V06 — preparação de dano, estado de combate e Draws ausentes

Fonte read-only: joey-knowledge-visual-decoded.sqlite,
SHA256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta direta às decompilações persistentes 00413BC0, 004140B0,
00414110, 00408470, 0047C310, 0047C4F0, 0047C540, 0047C550,
0047C560 e 004806F0; as duas tabelas de decompilação não têm
004142C0 nem 0047C660. Nenhuma nova evidência assembly nesta fatia.
As notas históricas sobre o Draw não substituem instruções reais.

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
