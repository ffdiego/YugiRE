# Geometria dos algarismos e sprites de status — evidência de assembly (23/11/2026)

## Evidência recebida

O bundle privado `ghidra-card-status-contract.zip`, entregue em
23/11/2026, contém as 17 funções solicitadas e as 12 janelas de
assembly exatas, todas recuperadas. Fingerprint do executável:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
A análise abaixo substitui a antiga hipótese de X baseada nos
registradores `unaff_EBX` e `iStack_6c`: o pseudocódigo não recuperava
corretamente os operandos.

O banco de miniaturas inicializado por `00418560` fornece largura
`DAT_005F3004` e altura `DAT_005F3008`. A largura de um quadro
de cada sprite de status é a largura de sua textura dividida
pelo inteiro de colunas associado no manifesto `duel/card/card.txt`.

## Indicador A — 0041A070

Com centro da carta X/Y, largura W e altura H da miniatura original,
S=+1 para perspectiva própria e -1 para a outra, a âncora vertical
é `Y + S*(H/2-5)`.

- `0041A14A..0041A16A`: consulta dimensões do recurso 6 pelo
  virtual +0x38 e divide largura por número de colunas (campo +0x34).
- `0041A1A5..0041A1DD`: X principal = `X - W/2`.
- `0041A20F..0041A23A`: Y principal = `statusY - (spriteHeight+1)/2`.
  O recorte horizontal é o quadro calculado anteriormente.
- `0041A23D..0041A288`: ESI recebe a largura de **um quadro**,
  e a numeração centralizada usa X principal + larguraQuadro + 6.
  Não é uma escrita arbitrária do temporário pelo compositor.
- `0041A2F5..0041A39E`: recurso 7 é a transição adicional;
  quadro `(-contador >> 4) & 7`, divisor do manifesto +0x3C.
  Destino = (centroNuméricoX - larguraQuadro7/2,
  statusY - altura7/2).

## Indicador B — 0041A3B0

Há gate adicional no início (`0041A3BD..0041A3C3`):
`(descriptor & 0x3E) < 0x0A`. Esse gate não é o mesmo
gate geral do chamador `004194D0`.

- `0041A496..0041A4B6`: dimensões do recurso 8 e divisão da
  largura pelo campo de colunas +0x44.
- `0041A4FC..0041A510`: limite direito da numeração =
  `X + W/2 - 3`.
- `0041A548..0041A57D`: sprite principal em
  `right - larguraQuadro8 - 8`, `statusY - altura8/2`.
- `0041A5BC..0041A5D3`: Y do número é centralizado pela
  altura do atlas; X é o limite direito, não o centro.
- `0041A63F..0041A6E6`: recurso 9 usa quadro
  `(-contador >> 4) & 7`, colunas em +0x4C, destino
  `right - larguraQuadro9/2 - 4`, `statusY - altura9/2`.

## Numeração confirmada

Ambos selecionam recurso índice 8 de `duel/font/number.txt`,
`duel_fontm_y.bmp`. O metadado DAT privado anterior registra
70×10, dez colunas (células 7×10). Em `00480690`, A centraliza
a sequência por sua largura total e delega a `004803C0`; B
chama `004803C0` diretamente. O callee decrementa X pela
largura do glifo antes do Draw, extraindo unidades primeiro.
`NativeCardStatusDigits` mantém esse contrato.

## Tipo e atributo alterados e flashes de ATK/DEF

`00419E7A..00419F28`: tipo alterado usa índice 5 e quadro
`effectiveType-1`, centro `X - (W*S)/2`. `00419FBE..0041A060`:
atributo alterado usa índice 4, quadro `effectiveAttribute-1`,
centro `X + (W*S)/2`. Ambos compartilham `Y - (H*S)/2`
como centro vertical e subtraem metade das dimensões do quadro.
A ramificação especial do ID 0x48A desenha arte de outra carta
e não faz parte desse modelo.

O bundle `ghidra-field-attributes-contract.zip` confirma que os
flashes 10..13 dependem de `target-displayedAnterior` **antes**
do smoothing, e não do buff/debuff relativo à carta base.
`00419734..00419832` (DEF) e `00419A16..00419B02` (ATK)
selecionam variante `(globalCounter >> 2) & 1`; a chamada
virtual +0x50 recebe X do centro numérico, intensidade 0x100,
e Y `numberTop-7-((globalCounter >> 1)&3)` para aumento
ou `numberTop-9+((globalCounter >> 1)&3)` para redução.
Não confundir os argumentos dessa chamada com pivô/blend
comprovados no raylib.

## Contrato C# e estado

`NativeStatusDrawLayout` e `NativeStatusAtlas` modelam os
recortes e destinos dos sprites 4..9, número, transição e
argumentos do flash. Os testes usam dimensões sintéticas
explicitamente, não valores inventados para o DAT. O antigo
helper com operandos decompilados foi removido.

**Geometria A/B e tipo/atributo fechada por assembly; renderização
dos ícones continua Pending.** Falta o DAT real (nomes, colunas e
dimensões dos sprites), semântica das flags de campo e composição
nativa comparada. Não alterar o denominador do badge: os subpasses
pertencem a 004194D0, não a novos objetos Draw independentes.
