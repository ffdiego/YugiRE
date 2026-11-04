# Helpers de decisão da IA: preparação hipotética e postura (2026-11-04)

Fonte primária: extração Ghidra `ai-decision-helpers.txt`,
252 linhas, recebida em 2026-11-04. Alvo EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Cruzamentos com `docs/03-functions.md`,
`card-callback-catalog.json` e relatório
[`ai-hand-selection-2026-11-04.md`](ai-hand-selection-2026-11-04.md).

## 1. FUN_0056D340(side, resourceId): primeiro índice da mão

A função itera `DAT_00A56434 + (side&1)*0xD44`
em elementos de 4 bytes, durante `handCount`,
e compara
`FUN_004022E0(internalIndex & 0xFFF)`
com o ID de recurso procurado.

```cpp
int FirstHandIndexByResourceId(uint32_t side, int resourceId) {
    for (int i = 0; i < handCount[side & 1]; ++i) {
        int internal = hand[side & 1][i] & 0xFFF;
        if (ResourceId(internal) == resourceId) return i;
    }
    return -1;
}
```

Isso **confirma** que as prioridades por nome da
`FUN_005B16C0` procuram a primeira cópia disponível
na mão e param na primeira correspondência; o helper
em si não pontua nem sorteia cartas. Ele também é
chamado por outras rotinas, não apenas pela IA.

## 2. FUN_005B1610(side, card, tributeCount, targets): modifica campo

O pseudocódigo Ghidra é declarado como `void`, tem
quatro argumentos e executa:

```cpp
// Esquema sem todas as estruturas e flags originais:
for (int i = 0; i < tributeCount; ++i) {
    auto slotPointer = FieldSlot(targets[i].side, targets[i].slot);
    FUN_005BDDF0(slotPointer, 0x90);  // confirmado como memset zero
}
int selected = FUN_0056A030(side);
FieldSlot(side, selected).cardIdentityBits = card;
FieldSlot(side, selected).positionFlags =
    (FieldSlot(side, selected).positionFlags & ~1) | 2;
```

O Ghidra acessa os targets como **pares de bytes**;
o primeiro codifica side no bit 0, e o segundo
fornece o slot. `FUN_005BDDF0` recebe um endereço
de registro e tamanho `0x90`; foi decompilada no relatório posterior: é
um zerador de memória equivalente a `memset`. A escrita posterior da identidade
da carta usa uma expressão de bits com máscara
`0xFFF` que deve ser reconfirmada em assembly,
pois o decompilador inferiu tipo de ponteiro pequeno
para `DAT_00A55D74`.

**Conclusão sólida:** o helper **escreve na estrutura
de campo** antes da decisão final. Por estar ligado
ao loop de avaliação do seletor `FUN_005B1360`,
isso sugere *avaliação sobre um estado candidato*.
Uma análise posterior confirmou o snapshot
`FUN_005B08E0` e o restore `FUN_005B0950`
dos cinco registros de monstros de ambos os
jogadores; os buffers são globais compartilhados. **Nunca chamar
esse helper de maneira avulsa em um duelo real.**

### Sequência do caller `0x005B14C4`

```text
FUN_005B08E0(side)
FUN_005B09C0(side)
FUN_005B1610(side,card,tributeCount,targets)
FUN_0056DD30(side,slot?,outStats)
FUN_005B0A50(side)
FUN_00569D50(side), FUN_00569D50(opponent)
FUN_005B0950(side)
CMP dword ptr [ESP+0x60], 0x384  ; comparação com 900
... compara métricas e bitmask DAT_00A579AC
```

`0x005B14D2` e `0x005B14E1` usam
`LEA ..., [ESP+0x40]` e repassam o ponteiro
à sequência, sugerindo uma estrutura local de
saída. Em `0x005B1544` há `MOV EAX,[ESP+0x60]`,
seguido de `CMP EAX,0x384`. A associação exata
entre essa variável, ATK/DEF e a estrutura de saída
ainda depende do layout real da pilha.

**ABI corrigida:** o assembly mostra
`FUN_005B1610` chamando `FUN_0056A030`
para escolher o slot em `EAX` e preservando
`EAX` até o retorno. O caller usa essa informação
em `MOV EDI,EAX`. O `void` inferido pelo
Ghidra não reflete o contrato observado.
Não invocar a rotina isoladamente.

`FUN_005B08E0` e `FUN_005B0950` foram
confirmadas como backup/restore dos registros
de monstro de ambos os lados. A restauração
não garante transação completa de todas as
estruturas de estado. Ver [relatório detalhado
do ciclo](ai-simulation-state-lifecycle-2026-11-04.md).

## 3. FUN_005AFFD0: maior atributo entre slots

`FUN_005AFFD0(observerSide, fieldSide, excludedSlot,
includeFirstStat, includeSecondStat)` percorre as
cinco zonas do `fieldSide`; ignora `excludedSlot`,
se fornecido. Para cada monstro ocupado, chama
`FUN_005ADD60` quando `observerSide==fieldSide`
e `FUN_005ADDC0` quando não.

Os helpers preenchem uma estrutura temporária
com atributos; `includeFirstStat` e
`includeSecondStat` escolhem quais atributos
podem entrar no máximo. Em ambos falsos, cada
monstro contribui com zero. Se nenhuma zona
contribui, a função devolve `-1` (ou um máximo
não negativo se houver ao menos um monstro).

O Ghidra chama as variáveis de saída `local_8` e
`local_4` sem atribuição explícita; na verdade
o ponteiro `local_c` é passado aos helpers e a
semântica de seus offsets precisa ser verificada
no assembly. O projeto já relaciona os atributos
a ATK/DEF; **não cravar qual local é ATK** sem
inspecionar `FUN_005ADD60` e o layout de saída.

Esse helper explica os dois usos no seletor de postura
`FUN_005AE0A0` e a comparação com valores de campo
em `FUN_005B16C0`.

## 4. FUN_005AE0A0: três resultados, limiares numéricos

Recebe `(side, candidateStatA, candidateStatB)`.
Consulta:

- `FUN_005ADE40(opponent,-1)`: valor comparado
  ao candidato, interpretação exata ainda aberta;
- `FUN_005AFFD0(side,side,-1,1,0)` e
  `FUN_005AFFD0(side,side,-1,0,1)`: máximos
  de atributos selecionados do campo próprio;
- `FUN_00569D50(opponent)` e
  `FUN_005B0330(side,side)`: métricas de presença/
  estado dos monstros;
- `FUN_005699A0(side,0x590)`, com o recurso
  **Fairy Box** (`0x590`, 1424) confirmado
  no catálogo do YugiRE.

O pseudocódigo contém limiares significativos:
`opponentMetric - candidateStatA > 999`;
`candidateStatA > 0x6A3` (1700);
`candidateStatB > 0x5DC` (1500);
diferença menor que `0x12D` (301),
e um desfecho com `candidateStatA + 300`.

O retorno é um de **`-1`, `0` ou `1`**.
A expressão final

```c
return ((opponentMetric <= candidateStatA + 300) - 1 & 2) - 1;
```

se reduz a:

```cpp
return opponentMetric > candidateStatA + 300 ? 1 : -1;
```

**Importante:** `FUN_005AE270` usa o retorno
de `FUN_005AE0A0` e, quando obtém `-1`,
substitui pelo quarto argumento `param_4`
(`0` nos dois callers observados). Logo,
`-1` parece um *sentinel de fallback*,
não necessariamente uma terceira postura.
`0` e `1` passam a alimentar os ramos
que escolhem summon face-up ou set face-down.

Sem decompilar os helpers e analisar efeitos
especiais, o uso desses três valores não é
uma enumeração completa das posições do jogo.

## Impacto para o projeto

**Achado novo:** existe um caminho que pode testar
vários candidatos alterando temporariamente estado
de campo, depois recalculando atributos e aplicando
limiares/bitmasks. Isso é mais do que uma escolha
uniformemente aleatória, mas **não prova uma busca
com múltiplos turnos futuros (minimax)**.

Para instrumentar um futuro laboratório:
- observar entradas/saídas de `FUN_005B1360`
  e `FUN_005B16C0`;
- capturar snapshots antes/depois de cada rodada de
  candidato *somente após localizar a restauração*;
- distinguir estágio de simulação de submissão real
  `FUN_005AD740`, evitando reportar o campo
  transitório como jogada resolvida;
- usar hooks passivos e nunca chamar
  `FUN_005B1610` diretamente sem saber seu
  isolamento/transação.

### Próxima extração recomendada — PowerShell

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-simulation-state-lifecycle.txt `
  -Addresses @(
    '005b08e0',
    '005b09c0',
    '005b0950',
    '005bddf0',
    '005b0a50',
    'ins:005b1610:75'
  )
```

A prioridade é determinar como o estado hipotético
é preparado/restaurado e se `FUN_005B1610`
de fato retorna um slot no registrador EAX.
Nenhum código injetado foi alterado nesta etapa.
