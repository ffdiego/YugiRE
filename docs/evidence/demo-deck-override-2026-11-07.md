# Decks controlados no modo 7 — primeiro experimento de escrita

Data: 2026-11-07  
Branch: `experiment/hybrid-native-probe`  
Alvo: mesmo `joey_pc.exe` x86, com SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## O que é observado, não hipótese

Nas capturas anteriores, para demos modo 7:

- `FUN_0044BEE0` orquestra `FUN_0044A920`, incluindo uma cópia
  de 0xE2 bytes do source 1 para o source 0.
- Duas estruturas de origem em `0x00A54E60` e
  `0x00A54E60+0xE2=0x00A54F42`, cada uma com 80/15/15
  posições `uint16` e contadores nos offsets
  `0xDC`, `0xDE` e `0xE0`.
- `FUN_00569380(side)` inicia a preparação do deck efetivo
  *depois* de as estruturas de origem estarem presentes e
  *antes* de `FUN_00568D80(side,passes)` embaralhar.
- O probe passivo correlaciona o lado ativo ao source slot
  por `sourceSlot = side ^ (duelModeWord & 1)`; em demos
  anteriores o conteúdo do source na entrada em
  `deck_prepare` coincidiu com o main efetivo observado
  na saída.
- Três composições completas distintas foram registradas,
  preservadas sem modificações em
  `native_hooks/decks/demo-{A,B,C}.json`.

**Validação dinâmica parcial recebida:** o operador executou o launcher
no Windows com os dois presets (deck A no lado 0, deck B no lado 1),
velocidade configurada em 240, e registrou o estado abaixo. O log
demonstra a aplicação e correspondência do deck principal em
`deck_prepare/leave`, mas não contém um `trace dump` completo
nem o término de um duelo.

## Desenho implementado (opt-in)

O Python recebe `--deck-0` e/ou `--deck-1`,
lê JSON antes de iniciar qualquer processo e valida os seguintes
campos:

- `main`: obrigatório, 40..80 inteiros de 1..4095.
- `second` e `third`: opcionais, 0..15 inteiros cada.
- Não admite chaves desconhecidas, strings, `true`,
  índices negativos, zero ou acima de 4095.

Esses valores são **índices internos**, não resource IDs
nem passcodes de carta. O validador de formato não valida
legalidade de deck ou compatibilidade de efeitos.

Após instalar `trace demo on` e **antes de `resume`**,
o launcher chama `setdemodecks({"0":deckA,"1":deckB})`
no agente Frida. O hook compartilhado com o probe
`deck_prepare/enter` altera somente o source slot
correspondente ao lado naquele duelo, quando:

1. Cena = 7 e mapeamento de source slot reconhecido.
2. A estrutura original contém um deck preenchido e válido.
3. O bloco de memória completo está em região gravável.

A escrita preenche 80/15/15 `uint16` (com zeros nas posições
não usadas) e as três contagens. O bloco de 0xE2 bytes é
copiado antes da alteração; erro na escrita ou falha na
releitura faz o agente tentar restaurar os bytes antigos.
A alteração acontece dentro do callback Frida na thread
que chamou a preparação do deck, não via RPC da thread de console.

`deck_prepare/leave` lê o main do deck efetivo e verifica
a **ordem inteira** dos índices contra a entrada do usuário.
Assim, `deck status` diferencia:

- `applied`: fonte gravada/releitura OK;
- `verified`: deck efetivo de main igual ao esperado;
- `failed`: erro ou divergência;
- `configured`: quais lados têm plano ativo.

O JSONL registra eventos `kind=deck_override`:
`applied`, `verified`, `mismatch` e `failed`.
Essa checagem detecta um ponto de intervenção incorreto,
em vez de atribuir resultados de IA a decks que não foram
realmente substituídos.

## Resultado da primeira validação real (Windows)

Comando do operador:

```powershell
.\.venv-frida\Scripts\python.exe .\native_rules.py --spawn --exe "..\..\passion2\joey_pc.exe" --observe-only --speed-value 240 --deck-0 ".\decks\demo-A.json" --deck-1 ".\decks\demo-B.json"
```

O processo foi criado suspenso (PID 19536), os decks foram configurados
antes do `resume`, e o console mostrou:

```text
{'configured': [True, True],
 'applied': [1, 1],
 'verified': [1, 1],
 'failed': [0, 0]}
```

Interpretação restrita:
- As duas origens foram gravadas e relidas corretamente.
- O `main` de ambos os decks efetivos correspondeu, em tamanho
  **e ordem**, aos decks solicitados na saída de `deck_prepare`.
- Não houve falha detectada nesse primeiro ciclo de preparação.
- Isso **não comprova ainda** a preservação de `second`/`third`,
  um duelo completo com esses decks, nem repetição em várias demos.

O aviso `Unable to start idund` apareceu e não impediu a aplicação
nesta execução.

## Teste de continuidade recomendado no Windows

Em `native_hooks`:

```powershell
.\.venv-frida\Scripts\python.exe .\native_rules.py --spawn --exe "..\..\passion2\joey_pc.exe" --observe-only --speed-value 240 --deck-0 ".\decks\demo-A.json" --deck-1 ".\decks\demo-B.json"
```

Permitir que a demo comece (modo 7) e executar:

```text
deck status
trace dump
```

Checar `applied` e `verified` nos dois lados, e que
`failed` permanece zero. Conferir as listas em
`deck_prepare/leave`, `deck_shuffle/enter` e
`deck_shuffle/leave` dos dois lados.

Para repetição controlada com dois workers, adicionar
`--allow-multi-instance` e iniciar em dois consoles separados.
Para rotação sequencial de capturas, adicionar
`--rotate-events 15000 --cycles 2`.

## Limitações

- **A substituição do main já foi validada em uma preparação
  real no Windows**; o término de duelos e as seções auxiliares
  ainda requerem validação.
- O jogo pode usar outros caches/variáveis para cartas extras;
  a verificação pós-`deck_prepare` comprova o main efetivo
  apenas. É necessário comparar outras listas em futuros traces.
- Se o jogo começar modo 7 sem os hooks, nada será alterado.
- No modo interativo, a captura deve ser exportada
  antes do jogo ser finalizado; o launcher exporta ao encerrar
  quando ainda existirem eventos não salvos.
- O hook pode encontrar um source inesperado e pular a alteração.
  O evento `failed` registra esse estado.
- **A mutação não é revertida quando Frida desanexa**, porque
  esses buffers são parte da memória do próprio processo, que
  continua usando as cartas. Ela não altera arquivos no disco.
  Para parar o experimento e voltar à seleção original, reiniciar o jogo.
