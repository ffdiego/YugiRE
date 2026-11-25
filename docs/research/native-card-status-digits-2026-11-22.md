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


## Fechamento de integração (23/11)

A revisão cruzada confirma a ordem dos passes dos indicadores
A/B: sprite principal, numeração amarela, sprite de
transição se estado apropriado e t>=256. A somente
prossegue se valor não zero **ou** estado 3
(`0041A0FB..0041A103`); B tem teste equivalente para
estado 4 (`0041A443..0041A44F`), além da região <0xA.
A ordem global é tipo/atributo, B, A após os números.

O bundle não traz `data.dat` nem as entradas do `card.txt`.
O extrator atual `summarize_native_card_status_dat.py`
lista nomes e dimensões mas ainda não registra o inteiro
auxiliar de cada entrada; **preservar/validar as colunas
originais** (consumidor usa divisor) antes de conectar
`NativeStatusAtlas`. O relatório integral de
`--inspect-visual-dat` preserva texto do manifesto.
Não usar as dimensões sintéticas dos testes como fatos DAT.
Não inventar significado das flags A/B; o bundle prova
as operações 0x5F/0x60/0x69, não os efeitos de carta
que as acionam.

A coleta de 17 funções e 12 janelas foi consumida;
não há nova extração de assembly requerida para a
geometria ordinária. O ramo especial 0x48A, blend/pivô
dos flashes e mapeamento semântico ficam como lacunas
delimitadas. Nenhum Draw adicional está integrado.


## V08 — compositor e revisão isolada

NativeCardStatusComposition encadeia as passagens ordinárias tipo,
atributo, B e A. Dentro de B/A preserva sprite principal, unidades
primeiro e transição. Gate de face-up/região e gate específico de B
são separados; objetos inativos ou em movimento não desenham.
O ramo especial 0x48A e os flashes 10..13 ficam fora até resolver
o compositor virtual +0x50 e a ligação semântica.

NativeCardStatusResources usa o manifesto do DAT em runtime,
verifica os quatro índices de foco, existência dos recursos e
divisores explícitos dos índices 4..9. Não deduz divisores por
largura nem aceita múltiplos inteiros sem saber qual usar.
O extrator Python agora preserva os campos auxiliares textuais.

Revisão isolada: escolher ID real de um monstro do catálogo, descritor
hexadecimal, flags hexadecimais, tipo efetivo, atributo efetivo e
estado 0/3/4 (não é uma declaração sobre efeitos do monstro).
Exemplo com um ID do catálogo local,
slot 2, face-up, A=12, B=3, estado 3:

    dotnet run --project src/Joey.App -- ../passion/data.dat --review-card-status=ID,8080,00F0,2,3,3

Substituir ID pelo resource ID numérico. F7 avança um update e
contador global sintético, F8 reinicia, F12 exporta PNG e JSON
com a ordem, recurso e coordenadas de cada pass, Esc encerra.
Perspectiva 1 permite testar o outro lado; descritor 0080 testa
face-down. Nenhum comando de duelo é produzido. Dimensões da
miniatura são lidas da arte real. Política de blend/colorkey
existente é apenas a adaptação de revisão; não é paridade.

Sem DAT original aqui, sintaxe e dimensões reais ainda precisam ser
exercitadas. Sem dados do Core de flags e identidade efetiva, não
disparar no fluxo normal. São subpasses de 004194D0 e não três
novos objetos no inventário. Cobertura segue 19/34, 0 Verified.

## V08.1 — banco DAT real e gates ordinários (25/11/2026)

Fonte privada v4-visuals, SHA256 SQLite
2085bc65dbc79fb2ec64645d2536bf0b56dde7bfedf4d173e32acb060b43722c;
quick_check ok. visual_dat_manifests, visual_dat_images, source_id
visual-dat-20261124 (dat-manifests.json SHA256
208a458484306d9249be1ed04a126b213aae8a4aaf79ee7e8b4c7bed3fe3a34b).
O manifesto original de 424 bytes está no SQLite em CP932; ler o texto
sem comentários para nomes/divisores. Não repetir --inspect-visual-dat.

| Índice | Recurso | Atlas | Colunas | Quadro |
| --- | --- | --- | --- | --- |
| 4 | icon_zokusei.bmp | 160x16 | 10 | 16x16 |
| 5 | icon_syuzoku.bmp | 320x16 | 20 | 16x16 |
| 6 | pas_eff_suna.bmp | 78x13 | 6 | 13x13 |
| 7 | pas_eff.bmp | 192x24 | 8 | 24x24 |
| 8 | pas_eff_skul.bmp | 44x12 | 4 | 11x12 |
| 9 | pas_eff2.bmp | 192x24 | 8 | 24x24 |
| 10/11 | yaji_ue1/2.bmp | 12x11 cada | sem divisor | 12x11 |
| 12/13 | yaji_sita1/2.bmp | 12x11 cada | sem divisor | 12x11 |

Os nomes e comentários identificam ampulheta (A) e caveira (B);
não provam a correspondência com toda mecânica de carta. A faixa
de atributo efetivo 1..6 não implica um atlas de seis colunas:
o divisor real é 10. As setas seguem não desenhadas enquanto o
contrato virtual +50 não for resolvido.

Assembly: 004194F6..00419525 retorna antes de qualquer subpass
se região+slot>10. 00419B7D..00419B85 e 00419B87..00419BCD
confirmam que a identidade efetiva ordinária só é lida para região
<5; em outras regiões existem ramos excepcionais de 0x479/0x47A,
0x48A e 0x5A8 (não portados). A mantém seu gate próprio e B
continua restrito a região<5. NativeCardStatusComposition corrige
os dois gates ausentes; não usa o gate numérico ATK/DEF para
suprimir status em outros casos. Testes exercitam limite inclusivo
10 e região S/T. Referências assembly: batch-000.txt, bundles
502c6177c5e694e384d03ead56f7e9b435758b22abbd4de253e3e78fcb8bd2db
e beeb78df561a42a33177d4c098368bd33d07b2901f2923b77eb391858c2e6fcd.

NativeCardStatusResources.ValidateEvidenceBank/Atlas verificam
a sequência e dimensões exatas de 4..13 contra os metadados.
CardStatusReviewScreen passa a validar texturas reais em runtime,
inclusive as setas, mantendo carregamento via DAT local. Testes
usam fixtures de nomes/dimensões atestados e miniatura 50x72
do cliente, rejeitam banco reordenado, divisor incorreto,
variante ausente e dimensões erradas; exercitam geometria de A/B
e tipo/atributo. Não há captura real neste ambiente. Não
alterar o duelo normal até mapear fatos do Core; não inferir
flags pelos nomes dos sprites. Nenhuma nova Draw contabilizada,
CI pendente, comparação gráfica e composição ainda pendentes.
