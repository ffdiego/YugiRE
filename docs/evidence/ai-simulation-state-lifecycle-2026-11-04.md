# Ciclo de simulação da IA: backup, avaliação e restauração do campo

Data: 2026-11-04. Fonte: `ai-simulation-state-lifecycle.txt`, arquivo
Ghidra fornecido pelo operador (249 linhas). EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Estudo **estático**; nenhum código nativo adicional foi executado.

## Save/restore real, mas limitado

`FUN_005B08E0(side)` salva dois valores auxiliares do lado
selecionado e copia **ambos** os campos de monstro:

```cpp
savedAuxA = read16(0xA55D6E + (side&1)*0xD44);
savedAuxB = read16(0xA55D70 + (side&1)*0xD44);
memcpy(0xA579B4, 0xA55D74, 0x2D0); // lado 0
memcpy(0xA57C84, 0xA56AB8, 0x2D0); // lado 1
```

`0x2D0 = 720 = 5 * 0x90`: cada buffer cobre os cinco
registros das zonas de monstro do lado correspondente.
A distância entre as duas origens é `0xD44`, stride por lado.

`FUN_005B0950(side)` restaura os buffers:

```cpp
// Antes de restaurar, guarda um indicador calculado:
write16(0xA579AC, read16(0xA55D70 + (side&1)*0xD44));
write16(0xA55D6E + (side&1)*0xD44, savedAuxA);
write16(0xA55D70 + (side&1)*0xD44, savedAuxB);
memcpy(0xA55D74, 0xA579B4, 0x2D0);
memcpy(0xA56AB8, 0xA57C84, 0x2D0);
```

Os valores `savedAuxA` e `savedAuxB` são armazenados em
`DAT_00A579AC` (metade alta) e `DAT_00A579B0`,
respectivamente. O valor capturado *antes* de restaurar
`DAT_00A55D70` fica na metade baixa de `DAT_00A579AC`,
possivelmente como saída do cálculo.

**Não é um snapshot completo de toda a memória:** flags,
estruturas de efeitos, globais de seleção e pontuações fora desses
dois arrays podem persistir ou ser reconstruídos por outras rotinas.
Os buffers são **globais compartilhados**; reentrância não comprovada.

## Preparação, candidato e resultado

`FUN_005B09C0(side)` percorre as cinco zonas do lado e pode
alterar os bits de posição com `(flags & ~1) | 2`,
sob condições de `FUN_00570A70`, `FUN_00567620` e
`FUN_0056B5A0`.

`FUN_005B1610(side,internalCard,tributeCount,targets)`:

1. Para cada par de bytes `(targetSide,targetSlot)` de tributo,
   calcula o registro e chama `FUN_005BDDF0(registro,0x90)`.
2. O corpo recém-decompilado de `FUN_005BDDF0` é um
   **zerador de memória** equivalente a `memset(ptr,0,size)`.
3. Chama `FUN_0056A030(side)` para determinar um slot.
4. Grava `(old & ~0xFFF) | (internalCard & 0xFFF)` no
   DWORD de identidade do slot e aplica
   `positionFlags=(positionFlags & ~1)|2`.

### Retorno de FUN_005B1610: o Ghidra inferiu void incorretamente

O assembly mostra `CALL 0056A030` em `0x005B1662`.
O slot retorna em **EAX** e nenhuma instrução subsequente
altera EAX até `RET` em `0x005B16BC` (a instrução
`MOV DX,...` altera **EDX**, não EAX).
O chamador faz `MOV EDI,EAX` em `0x005B14DF`.
Portanto, **neste callsite a função retorna o slot em EAX**,
apesar do protótipo `void` inferido pelo Ghidra.

Esse conhecimento **não autoriza chamá-la isoladamente**.
A função modifica os monstros dos dois lados conforme os
tributos e depende do contexto de save/restore.

## FUN_005B0A50: pontuação com remoções hipotéticas

A função zera bits de `0xA579AA`, zera um bitmask em
`0xA55D70 + side*0xD44`, chama
`FUN_005A0DD0(side)` e itera `FUN_005B0B90(side)`.
A cada elemento, extrai a zona de `DAT_00A579A6`,
registra-a no bitmask e:

- Se `DAT_00A579A6 & 0x400`, limpa os 12 bits de
  identidade de um monstro no lado próprio;
- Se `DAT_00A579A6 & 0x800`, limpa os 12 bits de
  identidade de um monstro no lado adversário;
- Acumula `DAT_00A579A8` no valor inteiro devolvido.

Isso é **compatível com simulação de confrontos/trocas**,
mas os significados dos eventos e de `DAT_00A579A8`
ainda não estão estabelecidos. Não afirmar minimax,
probabilidades ou profundidade de árvore de jogadas.

## Sequência confirmada do seletor FUN_005B1360

```text
FUN_005B08E0(side)              backup de monstros dos dois lados
FUN_005B09C0(side)              ajuste temporário de posições
FUN_005B1610(side,card,...)     aplica tributos e carta hipotética
FUN_0056DD30(side,slot,buffer)  recalcula atributos do candidato
FUN_005B0A50(side)              métricas/remoções simuladas
FUN_00569D50(side, opponent)    indicadores de campo
FUN_005B0950(side)              restaura os dois campos de monstro
...                            compara resultado e mantém índice
```

O restore é anterior à comparação final com as métricas
conservadas em globais/stack. Snapshots tomados durante
a avaliação **podem ver a jogada hipotética**, não o
estado final do duelo.

Para um observador passivo, preferir entrada/saída do
seletor e diferenciar claramente a janela de simulação
da execução real pelo builder `FUN_005AD740`.
Não instalar hooks que invoquem esses helpers no
processo sem mapear seus efeitos e sincronização.

## Pontuação de baseline e confrontos documentada

O relatório posterior `ai-simulated-combat-scoring.txt`
confirma que `FUN_005B08B0(side)` é um wrapper
de snapshot → preparação → `FUN_005B0A50`
→ restore. Ele é usado por `FUN_005B1360`
como score de referência antes de simular
invocações hipotéticas.

`FUN_005B0B90(side)` ordena candidatos de
confronto por classes de prioridade de carta
e `FUN_0056F690(side,slot) & 0xFFFF`, depois
chama `FUN_005B0D90` para gerar eventos.
Um gate anterior `FUN_005A0DD0` verifica
o duelo e o bit `0x40000` (diferente do
bit `0x20000` de summon/set).
O assembly de `FUN_005B1360` contém ainda
threshold `900`, bitmask de slot e desempates.
Ver [detalhes do score](ai-simulated-combat-scoring-2026-11-04.md).

## Confirmação da produção de alvos e eventos

`FUN_005B0D90` foi decompilada e percorre
os cinco possíveis alvos, utilizando
`FUN_0056BD80`, `FUN_005B05A0` e
`FUN_005B0F40`. A segunda palavra do
evento de quatro bytes é comparada como
`int16` e acumula-se em `DAT_00A579A8`
durante `FUN_005B0A50`. O desempate
considera DEF calculada do alvo.
Uma rota com destino `5` permanece sem
semântica completamente confirmada.
`FUN_0056B730` filtra os monstros e
`FUN_005A0D60` constrói seu bitmap.

Ver [análise de alvos e eventos](ai-combat-event-producer-2026-11-04.md).

## Próxima consulta (PowerShell)

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-combat-outcome-scoring.txt `
  -Addresses @(
    '005b0f40',
    '005b05a0',
    '0056bd80',
    '0056bf20',
    '0056bfd0',
    'ins:005b0d90:85'
  )
```

Investigar `FUN_005B0B90` e `FUN_005A0DD0` para
entender o mecanismo de eventos e a métrica usada
ao selecionar o melhor candidato.
