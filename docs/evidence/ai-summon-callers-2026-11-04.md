# Três chamadores da colocação automatizada de monstros (2026-11-04)

Fonte primária: `ai-summon-callers.txt`, extração Ghidra
fornecida pelo operador com 246 linhas de instruções, para
o EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Fonte cruzada: `normal-summon-level-consumers.txt` e
`summon-builder-callsites.txt` já versionados.
Nesta etapa, **não foi executada nenhuma função de IA**.

## Contrato comum

`FUN_005B6FF0(side, handIndex, modeSelector)` lê uma
carta selecionada da mão e, conforme classe e restrições,
prepara summon/set usando `FUN_005AD740` e marca
a flag 17 via `FUN_00579930`.

- `modeSelector == 0` normalmente é convertido em
  `mode=1` (summon face-up) para o construtor.
- `modeSelector != 0` resulta em `mode=0` (set).
- `FUN_00570A70(side) == 0` força o seletor para
  um caminho de set independentemente do valor recebido.
- O retorno de `FUN_005B6FF0` **não é a escolha da carta**
  e não deve ser confundido com sucesso definitivo da
  resolução no campo.

Os três chamadores passam o lado extraído de
`DAT_00A577FA & 1`. Isso corresponde ao lado ativo;
não é prova isolada de que o caminho só rode quando side=0
(CPU) ou também esteja ativo em demo/replay.

## Callsite A: 0x005B73D2

Instruções relevantes:

```asm
005B7397 PUSH 0
005B739C PUSH side
005B739D CALL 005B1360
005B73A5 CMP EAX, -1
005B73A8 JNZ 005B73BD
005B73AF PUSH -1
005B73B4 PUSH side
005B73B5 CALL 005B1360
005B73BD TEST EAX, EAX
005B73BF JL 005B755B
005B73CB PUSH 0         ; modeSelector
005B73D0 PUSH EAX       ; handIndex
005B73D1 PUSH side
005B73D2 CALL 005B6FF0
```

`FUN_005B1360(side, 0)` é o primeiro seletor
de índice. Se retorna `-1`, a rotina tenta
`FUN_005B1360(side, -1)`. Se o resultado
final for negativo, avança o estado sem chamar o
construtor. Se encontra índice não negativo,
chama `FUN_005B6FF0(side,index,0)`.

**Inferência cautelosa:** os modos de seleção `0` e
`-1` indicam uma preferência e um fallback, mas
não sabemos ainda quais critérios diferenciam esses
modos. Uma função não é aleatória só por escolher
entre múltiplas cartas.

## Callsite B: 0x005B7534

Antes de chamar, o trecho:

- `FUN_005AE390(side)` precisa retornar não zero.
- `TEST playerFlags[side], 0x20000` rejeita ramo
  quando a invocação normal já foi consumida.
- Um índice anteriormente salvo em `ESI` é usado
  para ler `hand[side][ESI] & 0xFFF`.
- `FUN_005AE270(side, cardIndex, 0, 0)` é
  chamada; o fluxo que alcança a colocação exige
  que seu retorno seja `0`.

A chamada direta é então
`FUN_005B6FF0(side, ESI, 0)`, pois o código faz
`PUSH EAX` após um `TEST EAX; JNZ` que
descarta retornos não zero. **Ainda não sabemos de
onde `ESI` foi originalmente obtido**, porque o
relatório começa no meio desse bloco.

`FUN_005AE270` é uma validação/filtro candidato,
mas não temos pseudocódigo suficiente para nomear
a regra exata. Não afirmar que se trata de teste de
tributo, sorteio ou valor estratégico.

## Callsite C: 0x005B766D

A sequência confirma:

1. `FUN_005AE390(side) != 0`.
2. `(playerFlags[side] & 0x20000) == 0`.
3. A combinação de `FUN_00570A70(side)` e
   `FUN_00570AC0(side)` determina se o ramo
   pode avançar; pelo menos uma condição é exigida.
4. `FUN_005B16C0(side)` escolhe um índice, cujo
   retorno é armazenado em `ESI`.
5. A carta em `hand[side][ESI]` é passada a
   `FUN_005AE270(side, cardIndex, 0, 0)`.
6. **O retorno bruto de `FUN_005AE270` é usado
   como terceiro argumento** de
   `FUN_005B6FF0(side, ESI, result)`.

Diferentemente do B, aqui o retorno é passado sem
uma guarda explícita `result==0` no recorte.
Pelo contrato do executor, o valor pode afetar
summon vs set, sujeito à substituição por
`FUN_00570A70`. A função `FUN_005AE270`
deve ser estudada antes de afirmar o significado
estratégico dessa escolha.

O recorte não mostra uma checagem explícita de
`ESI == -1` entre `FUN_005B16C0` e o
acesso à mão. Isso pode estar garantido pelos
gates/contratos do seletor, mas precisa de
verificação, não de um acesso externo replicado.

## Cruzamento de chamadas: seletores consultam regras

`normal-summon-level-consumers.txt` confirma
que `FUN_005AAB90` é chamada:
- de `FUN_005B1360` em `0x005B13E0`;
- de `FUN_005B16C0` em `0x005B19D4`
  e em `0x005B1AE9`.

A mesma evidência identifica `FUN_005AEE70`
como chamada por `FUN_005B16C0` em
`0x005B1A39`. O corpo de `FUN_005AEE70`
classifica níveis `0..4` como sem tributos,
`5..6` como um tributo e classes maiores como
dois, usando `FUN_005AECE0` para verificar
referências disponíveis. Isso é **forte evidência**
de que o seletor mais elaborado filtra candidatas
por regras do duelo, inclusive tributos.

Não temos, porém, o critério de pontuação ou desempate.
Sem o corpo de `FUN_005B1360`/`FUN_005B16C0`,
ainda não dá para distinguir uma heurística fixa,
prioridade por ordem na mão ou sorteio.

## Consequências para o laboratório

Antes de automatizar duelos com as mesmas decisões
do adversário, devemos inspecionar os dois seletores,
não o construtor, e em seguida o filtro de modo.
Eles fornecem pontos naturais de instrumentação
passiva para capturar candidatos, retornos e
escolhas de ação da IA em cada turno.

**Próxima consulta Ghidra no PowerShell:**

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-hand-selectors.txt `
  -Addresses @(
    '005b1360',
    '005b16c0',
    '005ae270',
    'ins:005b74ab:45',
    'ins:005b7615:65'
  )
```

Os corpos das funções podem ser longos; se o relatório
ficar grande, podemos separar `FUN_005B1360` e
`FUN_005B16C0` em dois relatórios menores.

Este estudo **não altera o executor summon/set**;
não temos prova de que o modo de demonstração do menu
seja IA contra IA nem de como o deck da CPU é escolhido.
