# ATK/DEF no campo — atlas numérico nativo (22/11/2026)

## Procedência

SQLite privado do usuário `joey-knowledge-visual-decoded.sqlite` (Joey EXE SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`),
decompilações de `00418A80`, `004194D0`, `00417F40`, `00480500`,
`00480300`, `00480200`, `00480230`, `004027B0` e `00402800`.
Manifest `j\duel\font\number.txt` recuperado anteriormente do DAT original
do usuário; não versionar DAT, imagens ou decompilações.

## Contrato visual observado na análise estática

| Manifest (índice) | Colunas | Seleção indicada por `004194D0` |
| --- | ---: | --- |
| `duel_fontm.bmp` (1) | 11 | Atributo inalterado; coluna 10 é separador |
| `duel_fontm_b.bmp` (2) | 10 | Atributo acima do valor base |
| `duel_fontm_r.bmp` (3) | 10 | Atributo abaixo do valor base |

`004194D0` chama `00417F40(1/2/3)` para escolher o bitmap. A comparação
de DEF vem do extrator `00402800`; a de ATK, de `004027B0`. Os nomes
sugerem azul/vermelho para buff/debuff, mas a cor pixel a pixel ainda
requer captura do original.

A ordem do Draw é **DEF, separador, ATK**. A base horizontal é
`obj+0x30`; DEF fica em `X+D`, ATK em `X-D`, onde `D = larguraGlifoNormal/2 + (5*larguraGlifoNormal)/2` (duas divisões inteiras). O separador na coluna 10 tem início em `X+larguraGlifoNormal/2-larguraGlifoNormal`. `00480500` desenha os algarismos da direita
para a esquerda, centralizados pela largura real do atlas escolhido.
A origem vertical usa `obj+0x34` e um deslocamento dependente de lado,
com altura da fonte. A UI preserva seu centro atual até verificarmos
a ancoragem absoluta via assembly de `0045C140` e screenshots 800×600.

O bit `0x4000`, também usado por `00418A80` na rotação de defesa,
inverte os parâmetros de opacidade: posição de ataque
ATK/DEF=`256/160` e defesa `160/256`; separador recebe `160`.
Esses são argumentos de composição nativos, não valores de alpha de
8 bits. Raylib converte 256 em alpha 255 e usa 160 no outro ramo;
o blending exato ainda está pendente.

`004194D0` também atualiza os valores exibidos de ATK e DEF com a
fórmula `target + trunc((displayed-target)*7/8)`. A investigação adicional
localizou a origem e o reset: **`00418B10` grava o ATK e DEF básicos do
`CARD_Prop.bin` nos campos +0x4C e +0x50 quando atribui nova carta**.
Em seguida `004194D0` usa os valores efetivos do duelo e avança esses
acumuladores **durante Draw**. Os dois campos se movem independentemente.

O cliente agora executa a mesma recorrência em `NativeFieldAttributeTimeline`,
mas com uma adaptação intencional de frequência: a janela raylib usa 60 FPS
mesmo com o perfil Classic de 30 updates/s; o avanço usa a contagem de
`PresentationTickClock`, não o número bruto de frames raylib, evitando
velocidade visual dupla e respeitando F6/F7. Sem updates a amostra fica
estável; em `Instant` muda imediatamente. Se o slot fica vazio/oculto,
ou a carta troca de instância, a timeline reinicia. Essa adaptação
continua **Partial** até comparar a cadência real do Draw nativo.
Não atualizar o `Joey.Core` para animar números.

## Gate de visibilidade e ordem de camadas (investigação posterior)

A cadeia de Draw foi conferida novamente no SQLite (versão privada,
sem alterar a base de funções do projeto):

1. `00418EF0` verifica `obj+0x20 != 0` (objeto ativo). No ramo de
   animação `obj+0x18 == 5`, desenha a carta/transformação e retorna
   sem chamar `004194D0`.
2. Se o contador de transição `obj+0x14 != 0x100`, o Draw trata a
   transformação/movimento e retorna, novamente sem números. Esse
   contador vem de `00418B60`, que interpola posição, rotação e
   estado face-up/down.
3. No ramo estabilizado, `00418EF0` desenha primeiro a carta,
   compõe o foco/seleção quando aplicável, e só então chama
   `004194D0`.
4. `004194D0` lê o descriptor de posição em `obj+0x24`:
   bit 0 = lado, bits 1..5 = região, bits 6..13 = slot.
   Se `região+slot > 10` ou `região != 0`, não desenha ATK/DEF.
   Se o lado não corresponde à perspectiva, só desenha quando
   `0x8000` está marcado (face-up/revelado). Isso sustenta a
   observação: monstros próprios Set mostram atributos, ocultos
   adversários não.
5. Antes do desenho, `004194D0` chama `0056DD30(lado,slot)` e
   compara a identidade de carta retornada com `obj+0x10`.
   Caso não coincida, suprime os números, mesmo que o objeto
   renderizado ainda tenha a carta antiga.
6. **Não confundir números com outros indicadores.** Quando
   `descriptor & 0x8000` e a região é elegível, `004194D0`
   executa separadamente `00419B70`, `0041A3B0` e
   `0041A070` **após o caminho dos números**, inclusive em
   alguns casos em que os números foram omitidos. Esses Draws
   consultam flags de efeitos e usam outros recursos; ainda não
   foram portados como se fossem parte do atlas ATK/DEF.

A classe pura `NativeFieldAttributeVisibility` codifica os gates de
número acima e possui testes de estados de movimento, saída, região,
identidade e ocultação. No cliente, a função `ShouldDrawClient`
adapta o gate à cena resolvida pelo Core e é compartilhada pelo
`DuelScreen` e pela captura `F12`.

**Regressão corrigida:** se o Core revelar uma carta inimiga
face-down **antes** do quadro de contato da espada, o YugiRE preserva
o verso através de `DrawHiddenAttackTargetBeforeImpact`, mas a captura
JSON do F12 anteriormente consultava o flag lógico já atualizado e
podia registrar números efetivos ocultos no visual. Agora F12
consulta o mesmo gate visual da cena e não registra esses atributos
antes do contato. Nossos Set continuam exibindo ATK/DEF.

A correspondência exata em pixels do overlay de status e suas
prioridades com explosões e invocações requer comparação pareada.
Não tratar essa análise como nova Draw implementada.

## Implementação e limitações

`NativeFieldAttributeFrame` encapsula posições relativas, variantes,
opacidades e smoothing; `NativeFieldAttributeTimeline` mantém um estado
por objeto e slot e avança por updates de apresentação. `DatGraphics` carrega os três BMPs do
`data.dat` local e aplica colorkey verde. `DuelScreen` substitui a
fonte de texto provisória pelo desenho de glifos nos três passes.
Mantém os atributos dos nossos face-down visíveis, e os do adversário
face-down ocultos, conforme observado pelo mantenedor. Testes cobrem
valor normal/aumentado/reduzido, centro, alpha e aritmética assinada.

Esta é uma melhoria da Draw `004194D0` **já Partial**: badge continua
**19/34 (55,88%), 0 Verified**. Ainda não recuperamos todas as flags
de visibilidade e de objetos transientes além dos gates já mapeados, a origem absoluta das
coordenadas e a composição exata dos bitmaps. A cadência do tween
no cliente precisa de comparação pareada com o nativo. Não afirmar fidelidade pixel-perfeita.

## Próxima coleta (pendente)

`bash scripts/completed/03-field-attributes-ghidra.sh` gera
`scratch/ghidra-field-attributes-contract.zip` no Fedora/Ghidra.
Para dimensões dos BMPs originais, executar `--inspect-visual-dat`
no Windows com `data.dat` e consultar o manifesto `number.txt`
no relatório `scratch/visual-review/dat-manifests.json`.
Compartilhar resultados privadamente e não commitar os assets.


## 23/11 — conferência pelo assembly recebido

`ghidra-field-attributes-contract.zip`: 8/8 funções, SHA-256 original confirmado. `004196D6..004196F6` e `004199D0..004199DE` demonstram duas divisões inteiras separadas para o afastamento dos números. `00419864..0041987B` e `0048036E..00480374` confirmam o separador com âncora direita `X+glifo/2`, da qual o helper subtrai uma largura. `00419612..00419630` confirma Y ±46 menos metade da altura da fonte; intensidades 160/256 também confirmadas. `NativeFieldAttributeFrame.Compose` agora usa a aritmética exata no renderer existente (diferença de 1 px para glifos ímpares). Flashes de mudança de atributo seguem o delta anterior ao smoothing; ver [status](native-card-status-digits-2026-11-22.md). Sem validação pareada de pixels, mantém Partial.


## Fechamento da revisão de assembly (23/11)

`00419549..0041956A` confirma a chamada de ocupante
e o gate de identidade. `00419570..004195CB` calcula
os dois deltas antes da suavização, salva-os e aplica
`target+trunc((displayed-target)*7/8)` nos acumuladores.
`00419632..004196CB` confirma que a variante de DEF é
escolhida comparando o **valor suavizado** ao base, e
não a direção do delta. A distinção é importante quando
o efeito expira: o número ainda pode estar acima do base
enquanto o flash indica redução.

O teste de lado (`00419612..00419630`) confirma:
mesma perspectiva → Y+46, adversário → Y-46; top =
esse centro menos altura da fonte/2. O blend ainda não
foi reconstruído: 160/256 são intensidades nativas
passadas ao helper, não prova de alpha raylib idêntico.
A comparação de pixels e cadência permanece pendente.
Não solicitar novamente o bundle 03: o contrato já foi
absorvido em código e testes no commit d03038e.

## V05.1 — ciclo de vida dos acumuladores e âncoras (SQLite v3)

Consulta read-only de joey-knowledge-visual-cumulative-2026-11-24-v3.zip,
SHA256 SQLite 9953a4eb3af861b034f91863aa99adfcaa83d7d34c671eb8f20315cfa37948ed;
quick_check=ok, fingerprint EXE confirmado. Assembly de 00418B10,
004194D0, 0045C280 disponível. Não repetir bundle 03.

Há três resultados distintos, não apenas visível/oculto:
- 00418EF0 não chama 004194D0 quando inativo, em movimento ou
  estado 5: os acumuladores +4C/+50 são preservados.
- 004194F6..00419525 (região+slot>10) e 0041952A..00419543
  -> 00419B07..00419B29 (região != 0 ou adversário oculto)
  regravam ATK/DEF básicos via 004027B0/00402800.
- 00419568..0041956A (identidade divergente) pula para 00419B2C,
  sem passar pelo reset. 00419570..004195CB avança somente no
  caminho elegível. 00418B10 semeia a base na atribuição de carta.

NativeFieldAttributeDisposition codifica Advance/Preserve/ResetToBase
e mantém os predicados booleanos para consumidores existentes.
No DuelScreen, slot ocupado em movimento deixa de descartar o cache;
vacância e adversário oculto estabilizado continuam a resetar. A próxima
amostra valida identidade por referência como antes. Não introduz
avanço durante movimento nem altera o gate da captura F12. A adaptação
do cliente por slot não é um modelo completo da identidade do objeto
nativo, especialmente transferências entre slots. Dez checks sintéticos
cobrem ambos os lados, privacidade, posição, interrupção, substituição
e geometria. CI novo a confirmar; não declarar paridade de cadência.

DAT v3: normal 77x10/11, azul e vermelho 70x10/10, portanto
todos os glifos têm 7x10. 00419612..00419630 confirma Y +/-46,
topo Y +/-46 - 5. Para o centro X=525, afastamento 3+17=20;
ATK centrado em 505, DEF em 545, separador começa em 521.
O histórico native-visual-contract.md registra centros Y=352/248,
dando top 393/197; esses Y ainda não foram reatestados pelos
bytes do snapshot v3. 00418B60 obtém posição de 0045C140,
e 0045C280 (0045C2C3..0045C2D8 / 0045C2D9..0045C2EE) usa
índice region+slot + 5*ladoRelativo para as regiões 0..4/5..9.
As duas tabelas de dez pares são 005DB560 e 005DB4E8. O segundo
banco usa índices 5..14: seus dez pares efetivamente acessados
começam em 005DB510 (005DB4E8+5*8), não no endereço-base.
A coleta 33 deve ler 005DB560:80 e 005DB510:80; 005DB570:4
(X central) já existe. Preservar layout atual até reatestar
a tabela; composição e cadência continuam Partial.

## V05.2 — coleta 33 recebida e consolidada (25/11/2026)

Bundle privado ghidra-native-field-centers-contract.zip, SHA256
0f1b6ad94406438aa5f52f9c7347895a4f7bdb3e2d82c8072b8fee09861c3c3d.
Manifest e ambos os relatórios têm o fingerprint EXE esperado.
1/1 decompilação, 2/2 reads, sem missing. 0045C280 confirma
side relativo, soma região+slot, e índice +5 para adversário.
Dados little-endian int32:

| Banco | Slots 0..4 próprios | Slots 0..4 adversários |
| --- | --- | --- |
| 005DB560, monstros | (369,352), (447,352), (525,352), (603,352), (681,352) | (681,248), (603,248), (525,248), (447,248), (369,248) |
| 005DB510, S/T | (369,460), (447,460), (525,460), (603,460), (681,460) | (681,140), (603,140), (525,140), (447,140), (369,140) |

O valor prévio em 005DB570 coincide byte a byte. Os centros históricos
do cliente estão confirmados, sem deslocamento novo. O Y dos atributos
é 398/202 e o topo do glifo normal de 10px é 393/197. V06 slot -1
usa a S/T central, (525,460)/(525,140); não confundir a origem do
blit com centro. V02 dimensões/rotação e V06 composição não estão
validadas pela tabela de centros.

Acrescentadas 50 verificações ao teste V05.1: vinte centros em dez
combinações de lado/slot e quarenta gates/âncoras com posição e
face-down. CI da fatia anterior aprovado (45544506962), novo CI
pendente. Nenhuma alteração adicional à geometria do renderer.

Snapshot privado joey-knowledge-visual-cumulative-2026-11-25-v4-visuals.zip:
SQLite SHA256 2085bc65dbc79fb2ec64645d2536bf0b56dde7bfedf4d173e32acb060b43722c;
ZIP SHA256 a9cfc0b516486d4e82a8f062227b8431bb1226f3b4ff7ea1f5f056b9e5612e87.
Base v3 preservada: +1 bundle (22), +2 reports (177), +1 registro
de função (282, 215 VAs distintas), +2 chunks (26), instruções
167494 inalteradas. Integrity_check ok, foreign_key_check vazio;
auditoria registra contagens antes/depois de todas as tabelas.
Importação focada compatível com schema do importador; CLI e suíte
Python do repo não foram executados neste ambiente. O v4-visuals
não inclui v4-actions (coleta 31); conciliar os dois ramos cumulativos,
não sobrescrever o snapshot global. Roteiro 33 movido para completed.
Não repetir coleta; falta comparação gráfica e mistura/cadência.
