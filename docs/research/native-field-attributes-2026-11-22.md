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
`obj+0x30`; DEF fica em `X+3*larguraGlifoNormal`, ATK em
`X-3*larguraGlifoNormal`, separador na coluna 10 em
`X-larguraGlifoNormal/2`. `00480500` desenha os algarismos da direita
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
de visibilidade (incluindo bit `0x8000`), a origem absoluta das
coordenadas e a composição exata dos bitmaps. A cadência do tween
no cliente precisa de comparação pareada com o nativo. Não afirmar fidelidade pixel-perfeita.

## Próxima coleta (pendente)

`bash scripts/pending/03-field-attributes-ghidra.sh` gera
`scratch/ghidra-field-attributes-contract.zip` no Fedora/Ghidra.
Para dimensões dos BMPs originais, executar `--inspect-visual-dat`
no Windows com `data.dat` e consultar o manifesto `number.txt`
no relatório `scratch/visual-review/dat-manifests.json`.
Compartilhar resultados privadamente e não commitar os assets.
