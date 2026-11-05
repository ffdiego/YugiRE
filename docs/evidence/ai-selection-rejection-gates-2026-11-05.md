# IA: gates de seleção da mão e limiar 900 (2026-11-05)

Fonte primária: arquivo de Ghidra fornecido pelo operador
`ai-selection-rejection-gates.txt` (530 linhas), EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Decompilação das funções `FUN_005B1360`,
`FUN_005AAB90`, `FUN_005AE3E0` e
`FUN_005AE220`, além de assembly em
`0x005B1510..`. Análise estática e
**nenhum novo teste dinâmico** nesta rodada.

## Contrato real do seletor

`FUN_005B1360(side, pass)` inicia
`bestHandIndex = -1` e obtém o baseline de
`FUN_005B08B0(side)`. Depois percorre a mão,
com os seguintes gates (ordem de chamada
confirmada pela decompilação):

| Etapa | Condição para avançar |
| --- | --- |
| 1 | `FUN_005AAB90(side, internalIndex) != 0` |
| 2 | `FUN_005AE3E0(side, 2, resourceId) == false` |
| 3 | `FUN_005AE220(internalIndex) == 0` |
| 4 | Classe 1–4: `pass != 1` (vale 0 ou -1); classe 5–6 ou superior: `pass != 0` mais tributos |
| 5 | Snapshot, colocação hipotética, atributos calculados, score e rollback |
| 6 | Valor local maior que 899, bit do slot no bitmap e score melhor/desempate válido |

Em linguagem de pseudocódigo parcial:

```cpp
int baseline = FUN_005B08B0(side);
int best = -1;
for (int i = 0; i < handCount[side]; ++i) {
    CardIndex card = hand[side][i] & 0xfff;
    if (!FUN_005AAB90(side, card)) continue;
    if (FUN_005AE3E0(side, 2, ResourceId(card))) continue;
    if (FUN_005AE220(card)) continue;
    if (!PassAllowsLevelClass(card, pass)) continue;

    FUN_005B08E0(side);    // snapshot
    FUN_005B09C0(side);    // flags auxiliares
    slot = FUN_005B1610(...);  // estado hipotético, não pure
    FUN_0056DD30(side, slot, stats);
    candidateScore = FUN_005B0A50(side);
    // atualizar campos auxiliares, restaurar monstros
    FUN_005B0950(side);

    if (valueFromStats >= 900 && SlotBitAllowed(slot) &&
        (candidateScore > baseline ||
         (candidateScore == baseline && SpecialTieBreaker()))) {
        best = i;
        baseline = candidateScore;
    }
}
return best;
```

**Atenção:** esse esboço omite a atualização detalhada
de `DAT_00A579AA/AC`, a lógica de desempate,
máscaras e os alvos de tributos. Não é um
algoritmo executável nem uma reimplementação
completa do nativo.

## O teste de 900: ATK computado confirmado

Em `0x005B153F`, após
`FUN_005B0950`, o assembly tem:

```asm
005B1544 MOV EAX,dword ptr [ESP + 0x60]
005B1548 ADD ESP,0x34
005B154B CMP EAX,0x384
005B1550 JL  0x005B15B3
```

A extração posterior confirmou a relação exata dos ponteiros: `out=S+0x28` em `0x005B14E1`; a leitura de `0x005B1544` é `S+0x2C = out+4`, o DWORD de **ATK calculado**. O valor mínimo de **900 ATK** é, portanto, um gate comprovado deste seletor. Ver [prova de pilha](ai-selection-score-offsets-2026-11-05.md).

## Bitmap de zona e pontuação

O assembly imediatamente seguinte lê
`DAT_00A579AC`, cria uma máscara
`1 << slot` e executa `TEST`. Se o bit
estiver apagado, salta para
`0x005B15B3` sem aceitar o candidato.

A avaliação `FUN_005B0A50` é comparada
ao `bestScore` (inicialmente baseline de
`FUN_005B08B0`):
- se `candidateScore > bestScore`, pode
  atualizar a escolha;
- se `candidateScore == bestScore`, exige
  desempate adicional em flags/contadores;
- se `candidateScore < bestScore`, não
  atualiza a escolha.

Esse filtro é distinto das regras que
permitem ao jogador invocar legalmente.

## FUN_005AAB90: retorno é máscara, não apenas bool

O helper inicia `uVar4=1`,
`uVar5=0x10` e pode retornar sua
combinação `0x11`, como no preflight real
de Mystic Tomato. O valor pode ser
alterado por regras específicas.

Gates observados:
- identidade nula/invalidada por
  `FUN_00402690`;
- filtro `FUN_0056B440`, que tem
  vínculo observado com **Prohibition**;
- `FUN_00570A10` (motivo ainda pendente);
- `FUN_00567A00` (motivo ainda pendente);
- `FUN_00570A70` em conjunto com
  **Light of Intervention** (`0x47F`);
- disponibilidade de zona,
  `FUN_0056A000`, e tributos para classes
  superiores.

Alguns ramos chamam `FUN_0045E240`
para emitir feedback ao lado que está
jogando. Isso é mais uma razão para não
chamar o helper repetidamente de forma
avulsa fora da thread do duelo.

**Diferença relevante entre nossos testes:**
`preflightNormalPlacement` chama
`FUN_005AAB90(side, handWordCompleto)`,
enquanto o próprio `FUN_005B1360` envia
`FUN_005AAB90(side, handWord & 0xFFF)`.
A extração não prova se os bits altos
mudam a resposta dos helpers internos.
Um `0x11` obtido com o word completo
não é, sozinho, evidência de que todos os
gates internos do seletor tenham passado.

## FUN_005AE3E0: filtro condicional por recurso

O seletor chama com modo numérico **2**.
Esse helper retorna **true para bloquear**
certos recursos segundo o modo/estado do
duelo; false permite passar ao próximo gate.

O código inclui listas especiais (ex. IDs
`0x3F2`, `0x403`, `0x405`, `0x406`,
`0x420`, `0x425`, `0x42C`),
e regras que dependem do estado de campo,
cemitério e efeitos.

**Mystic Tomato** tem índice interno
`221` e recurso `0x463` no catálogo;
**Wall of Illusion** é `273` / `0x419`.
Para ambos, o `switch` do modo 2
retorna `false` pelo ramo padrão
no código apresentado. Portanto, **esse
filtro não parece ser a causa** da recusa
dessas cartas no caso observado.

Isso é conclusão do **corpo decompilado**,
não de telemetria específica por candidato.

## FUN_005AE220: filtro de vínculo e exceções

O helper retorna 0 se
`FUN_00567620(internalIndex,-1)==0` e
o recurso **não** estiver entre:

- `0x4DB`: Ancient Lamp
- `0x2DF`: Blast Sphere
- `0x492`: Kiseitai
- `0x585`: nome não confirmado no catálogo

Em outros casos retorna 1. A função `FUN_00567620` foi extraída posteriormente e é uma classificação de recursos: Mystic Tomato (`0x463`) e Wall of Illusion (`0x419`) **não pertencem à whitelist**, logo `FUN_005AE220` retorna 0 para ambas. **Este gate não explica a recusa dessas cartas.** Ver [classificador e pilha](ai-selection-score-offsets-2026-11-05.md).

## O que mudou sobre o caso real

O trace [com duas cópias de Mystic Tomato
e Wall of Illusion](ai-native-selection-rejected-legal-mystic-tomato-2026-11-05.md)
registrou `-1` nos dois passes, embora
`summon check 1 2` permitisse a colocação.

O novo código indica causas possíveis
**mais delimitadas**:
1. primeiro helper de elegibilidade
   usa o índice interno e pode depender
   de flags/contexto;
2. vínculo especial ou restrição
   específica `FUN_005AE220`;
3. **ATK calculado** menor que 900;
4. bit do slot selecionado zerado em
   `DAT_00A579AC`;
5. score da hipótese não superou o
   baseline (e desempate não favoreceu).

A segunda hipótese foi reduzida à
dependência de `FUN_00567620`; o filtro
`FUN_005AE3E0` exclui explicitamente
outros recursos, não Mystic Tomato.
**Nenhum motivo individual está comprovado
dinamicamente.**

## Próxima consulta Ghidra: confirmar offset e vínculo

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-selection-score-offsets.txt `
  -Addresses @(
    'ins:005b1360:125',
    'ins:005b14b0:145',
    '00567620',
    'ins:005b1540:90'
  )
```

Objetivo: localizar o `out` exato de
`FUN_0056DD30` em relação à leitura
`[ESP+0x60]` e estudar `FUN_00567620`.
Somente após isso vale escolher pontos
mínimos de observação de gates do seletor.
