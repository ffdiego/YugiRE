# Recebimento dos quatro contratos de Ghidra — 23/11/2026

Todos os bundles privados enviados foram abertos e examinados. Cada
manifest declara o mesmo SHA-256 do executável original:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`,
HEAD `b32d982b3ec3ec28e3ec4f1e7b624e09372b8f02` e `missing=[]`.
Os arquivos privados não são versionados.

| Bundle | Funções recuperadas | Memória/janelas |
| --- | ---: | --- |
| selection-fusion | 6/6 | 3/3 leituras de memória |
| chain-disabled | 5/5 | assembly e decompilações |
| field-attributes | 8/8 | assembly e decompilações |
| card-status | 17/17 | 12/12 janelas exatas |

## Status e atributos — resultado implementável

O assembly dos indicadores resolve os operandos ambíguos de
`0041A070`, `0041A3B0` e `00419B70`, incluindo X/Y, recortes e
posição relativa dos quadros. A geometria dos ícones 4..9 usa
as dimensões da miniatura e o divisor de colunas da textura.
O contrato puro `NativeStatusDrawLayout` tem testes com medidas
sintéticas. Ainda falta a extração de metadados reais do
`card.txt`, a ligação semântica das flags e a composição visual.
Ver [status](native-card-status-digits-2026-11-22.md).

O bundle ATK/DEF confirma Y±46, intensidades e duas divisões
inteiras independentes para o afastamento lateral. A fórmula
corrigida é usada no renderer C# existente. Os flashes têm
sinal dependente do delta anterior ao smoothing e oscilação
pelo contador global, não da diferença com os atributos base.
Ver [ATK/DEF](native-field-attributes-2026-11-22.md).

## Corrente desativada — correção de flag e geometria

`004531C0` seleciona diretamente a textura de índice
`(counter/2)%6`. O teste 0x40 ocorre no byte alto do descritor,
portanto o bit da palavra completa é **0x4000**. Com esse bit,
`00422540` recebe rotação de 90 graus e escala 0x10000;
as dimensões trocam de eixo para manter o centro. Sem o bit,
desenha a textura inteira sem rotação. O contrato e testes
foram corrigidos, mas falta integrar recursos e gatilho.
Ver [corrente](native-chain-disabled-2026-11-22.md).

## Seleção/fusão — primeira leitura

As três leituras de memória solicitadas vieram completas:

| Endereço | Interpretação little-endian |
| --- | --- |
| `005DB570` | int32 = 525 |
| `005DCCF8` | double = pi |
| `005DCCB0` | double = 1/256 |

`0055DCF0` (instruções `0055DD0B..0055DD3C`) gera 512
entradas de cosseno: FILD índice, multiplica pelas duas
constantes, FCOS, multiplica pelo double em `005DCD00`
e converte em inteiro via `005C3E40`. A última constante
e a conversão não vieram nas leituras de memória solicitadas;
não afirmar equivalência binária da tabela gerada em C# ainda.

`0043B280` confirma as operações de escala de posição/dimensões
com o valor da tabela deslocado em 8 bits. O assembly usa
correção de sinal antes de SAR para truncar em direção a zero.
As duas famílias de fusão têm seus Updates e Draws recuperados;
a revisão final documenta as fases, relógios, fórmulas de escala,
recursos 0/1/2 e acumuladores de ângulo em
[native-selection-window-fusion](native-selection-window-fusion-2026-11-22.md).
O assembly longo é limitado a 512 instruções; o epílogo da fusão geral
permanece sustentado pelo pseudocódigo, não por assembly integral.

## Estado

Não há novos Draws integrados por este conjunto de contratos.
A melhoria real na composição ATK/DEF mantém o estado Partial;
19/34 e 0 Verified. O próximo trabalho de maior retorno é
analisar seleção/fusão com o assembly já entregue ou finalizar
a integração da corrente desativada quando os metadados DAT
e o gatilho estiverem estabelecidos. Não solicitar novamente
os quatro bundles já recebidos.


## Handoff — encerrar este lote de quatro arquivos

**Escopo fechado:** consolidar os quatro ZIPs entregues,
não iniciar novas implementações. O commit anterior
`d03038e` já contém a correção de geometria numérica
e os contratos de status/corrente; todos os jobs .NET,
Python/Node e badges passaram nessa revisão.

| Frente | Conhecimento estático absorvido | Próxima dependência, fora deste lote |
| --- | --- | --- |
| selection-fusion | Âncora 525, lookup correto, fases de duas fusões, relógios 4/8, espera e mutações no Draw | Cosseno runtime, manifest fusion/list, constantes de centro e tabela de seletores, flag de espera |
| chain-disabled | Opcode 0x72, gates, descritor (0/5/10), centro por 0045C140, seis recursos e rotação | disable.txt original, semântica do comando, integração |
| field-attributes | Gates, suavização, seleção de atlas, posição e intensidades, delta do flash | Comparação original/client e blend/cadência |
| card-status | Gates, campos, opcodes, tempo, recortes, X/Y e ordem de passes | card.txt com colunas reais, flags semânticas, compositor/integração |

**Retomar pelo repositório, não pelos ZIPs desde o início.**
Para o próximo chat, ler este documento e os quatro links
de pesquisa; usar `PLANS.md` como fila de implementação.
Manter `Joey.LookAndFeel` separado de `Joey.Core`, usar
assembly como autoridade para geometria, distinguir
pseudocódigo de assembly e validação em jogo. Não versionar
os bundles privados, EXE, DAT ou dumps. Os quatro roteiros
ficam arquivados em scripts/pending com estado de coleta
concluída; não é necessário executá-los novamente.

**Cobertura visual na entrega:** 19/34 Partial, 0 Verified.
Nenhuma mudança na classificação por esta consolidação
documental. O avanço de conhecimento não deve inflar o badge.

### Links de retomada

- [Seleção e fusão](native-selection-window-fusion-2026-11-22.md)
- [Corrente desativada](native-chain-disabled-2026-11-22.md)
- [ATK/DEF](native-field-attributes-2026-11-22.md)
- [Geometria do status](native-card-status-digits-2026-11-22.md)
- [Estados e flags de status](native-card-status-overlays-2026-11-22.md)
