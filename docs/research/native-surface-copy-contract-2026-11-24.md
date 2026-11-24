# V17 / V30 — infraestrutura de superfície e blit opaco

Fonte privada: joey-knowledge-visual-cumulative-2026-11-24-v2.zip,
SQLite SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4.
Consultadas decompilations, pointers, refs e visual_evidence_memory.
As fórmulas abaixo são **decompilação e cruzamento de rotinas**,
não assembly novo: nenhum byte de código novo do EXE foi extraído.
Não afirmar equivalência de pixels sem comparação.

## Vtable da superfície scratch de Vija

004580F0 chama 004A9CC0; este construtor grava vtable 005DC6EC
(ref 004A9D07, também 004A9DB0 em 004A9DD1). O índice esparso
pointers tem 005DC6EC -> 004A9D90, 005DC734 (+48) -> 0041AB90,
005DC740 (+54) -> 004AA4B0, 005DC748 (+5C) -> 004A9E80,
005DC754 (+68) -> 004ABDD0. O último chama virtual +E0
com dimensões e flag, seguido de 004AC110 / 004AC1B0.
Vija cria a scratch com flag 1. Ainda não deduzir formato
de pixels ativo a partir dessa flag.

O alvo de +98 (005DC784) NÃO está em pointers/refs nem
coberto pelos 24 chunks de visual_evidence_memory. Não
é seguro escolher um dos muitos blits por similaridade
do pseudocódigo. 004584C0 já foi coletada; não repetir.
Roteiro 24 lê 005DC6EC:228 (até +E0 inclusive) e confirma
00547180 / 004ABDD0 e entrada de 004AD9C0. Depois resolver
os VAs de +98, +4C, +48, +E0 pelo resultado; se a função
já constar no banco, não exportar novamente.
A composição da máscara e as opções de cor/canal de
004589C8 continuam sem implementação. O roteiro 24 não
promete recuperar automaticamente código de alvos desconhecidos.

## Cópia opaca — 004AD9C0 / 00547180

004584C0 usa 004AD9C0 diretamente para as duas cópias
do fundo (key4) na scratch, não +98. Essa rotina já é
decompilada. Despacha por formato em surface+74h, usando
dados de pixels +10h, pitch +14h e clipping 00547180.

| Código | Layout lógico recuperado |
| --- | --- |
| 3 | RGB565 little-endian |
| 4 | RGB555 little-endian |
| 5 | B,G,R (3 bytes) |
| 7 | B,G,R,X (4 bytes) |

004C7600/004C7740 convertem 565/555; 004C7640/004C7780
expandem canais com shifts/máscaras, **sem replicação de
bits** (branco 565 -> 248,252,248; branco 555 ->
248,248,248). 004B50C0/004B5500 confirmam quantização
BGR24 para 565/555. 004B5FE0 coloca X=0 no destino de
32 bits; 004B89F0 copia apenas BGR de 32 para 24.
004BA020 copia dword inteiro quando formatos compatíveis;
o byte X não deve ser interpretado automaticamente
como alfa. 004BFD00/004C0130 confirmam 32 -> 565/555.
Os demais formatos (6,8,10..13 e variantes) não foram
implementados. Não confundir o tipo 7 com YGA source alpha.

00547180 tem caminho direto se largura E altura coincidem
e outro se alguma dimensão difere. Para cada eixo escalado:
S = extensão de origem, D = extensão destino, k = clipping
no início do destino, A=-D+k*S. Se A>0, deslocar cursor
por q=A/D+1 e A-=q*D; depois A-=1. Passo base S/D,
resto S%D. Antes de copiar cada pixel, somar resto;
se A>=0, avançar cursor e subtrair D. Copiar pixel,
avançar cursor por passo base. Este detalhe difere de
floor(i*S/D) e do filtro de ponto padrão da GPU.
Exemplos 5->3: [0,2,3], 3->5: [0,1,1,2,2].
No caminho misto, ambos eixos seguem recorrência,
inclusive o eixo de dimensão igual e início cortado;
preservar a expressão recuperada, confirmar assembly
do caso de fronteira antes de rotular pixel-parity.

NativeSurfaceCopy é implementação de CPU independente
de Core/raylib, com pitch positivo, retângulos fonte
válidos, clipping destino e conversões de quatro formatos.
Usa intermediários de 64 bits e valida memória, sem
reproduzir overflow/endereços inválidos do x86. Não
implementa flip, sobreposição in-place, alpha, color key,
+98 ou alocação nativa. Os testes exercitam conversões,
clipping, padding, caminho direto/misto e recorrência.
A enumeração independente em Python conferiu limites de
amostras para S,D=1..64 e todos os skips. Não é captura
nativa. Aplicação de Vija permanece plano de composição
até resolver máscara/remap/format; não ligar novo gate.

## Reuso

O mesmo primitivo pode servir às cópias de bancos
de outros efeitos e à composição de Vija quando o
formato estiver resolvido. O pipeline do jogo permanece
inalterado. A03 continua responsável por scheduler,
snapshots e tempo de vida. Cobertura 20/34.
