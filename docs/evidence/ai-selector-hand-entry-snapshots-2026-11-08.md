# Snapshot da mão na entrada dos seletores da IA (experimento)

**Status:** implementação disponível em `experiment/hybrid-native-probe`;
JavaScript verificado sintaticamente e testes offline adicionados.
**Ainda precisa de validação com o Joey original no Windows.**

## Motivação

Na captura de 100 duelos com `--trace-ai`, já sabemos
qual carta chegou ao executor para 1.200 chamadas durante
duelos, correlacionando `ai_probe/select_*.leave` com
`demo_executor/normal_placement.enter`.
Isso mostra *o que foi selecionado*, mas não as demais
cartas presentes na mão no momento da decisão.

## Novo comportamento

Os probes já existentes em `FUN_005B1360` (`select_normal`)
e `FUN_005B16C0` (`select_priority`) capturam
**somente na entrada** das funções:

- `sideRaw`: argumento bruto para conferência da ABI;
- `duelRunning`: leitura de `0x00A577F8`, sem tentar
  ler cartas quando a engine não está em duelo;
- `handCount`: número de entradas na mão do lado;
- `handWords`: array de palavras de 32 bits originais,
  na ordem da memória;
- `handIndices`: `handWords[i] & 0xFFF`.

A leitura limita a mão a **80 cartas**. Contador
inválido ou lado fora de 0/1 são registrados em
`handReadError` sem fazer acesso fora das estruturas.

Na **saída** do seletor, se `selectedHandIndex`
estiver dentro do snapshot:

- `selectedCardWordAtEntry`
- `selectedCardIndexAtEntry`
- `handCountAtEntry`

Essa associação usa os valores já guardados **na entrada**,
não relê a memória depois da avaliação. O hook é passivo:
não chama funções do jogo, não altera cartas ou o retorno.

**A mão observada não é uma lista de jogadas legais ou
de alternativas efetivamente pontuadas pela IA.** Para
descobrir critérios, ainda precisamos compreender filtragem,
estado de campo e o que cada seletor calcula.

## Teste no Windows

A partir de `native_hooks`:

```powershell
.\.venv-frida\Scripts\python.exe .\native_rules.py --spawn --exe "..\..\passion2\joey_pc.exe" --observe-only --speed-value 2400 --deck-0 ".\decks\demo-A.json" --deck-1 ".\decks\demo-B.json" --trace-ai --duels 3
```

Enviar o JSONL gerado. Verificar que `complete=true`,
`overwritten=0`, que os eventos de entrada dos
seletores durante um duelo tragam `handIndices`, e que
o campo `selectedCardIndexAtEntry` no retorno
corresponda à carta encaminhada ao executor quando
houver `demo_executor/normal_placement.enter`.

Análise offline, sem Frida:

```powershell
python ..\scripts\analyze_ai_selector_choices.py ..\scratch\traces\duels-ARQUIVO.jsonl
```

Gera dois arquivos no mesmo diretório:
`*-selector-choices.csv` e
`*-selector-summary.json`. O resumo inclui
`handSnapshots`, `identifiedChoices`,
`forwardedToExecutor`, `matchedToExecutor`,
`mismatchedToExecutor`, `entryReadErrors` e
`unterminatedSelectors`. Não generalizar a correspondência
sem validação no jogo real.

## Limites de interpretação

- O snapshot é uma sequência de leituras da memória
  na thread interceptada; não é snapshot atômico
  de todo o estado do duelo.
- `sideRaw` deve ser inspecionado. Se a ABI da função
  não usar 0/1 em `args[0]`, o hook não coleta a mão
  e emite `handReadError`; jamais deduzir o lado de
  um ponteiro sem evidência adicional.
- Mesmo quando a carta é identificada, o retorno do
  seletor não prova que ocorreu uma invocação ou colocação.
- Não tentar comparar a nova quantidade de dados com
  capturas antigas como se tivessem a mesma instrumentação.
