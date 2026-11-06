# YugiRE — análise da captura real do modo 7 (06/11/2026)

**Referências:** branch [`experiment/hybrid-native-probe`](https://github.com/ffdiego/YugiRE/tree/experiment/hybrid-native-probe), HEAD `8a0421f430e0e22cf99b55c7c8e6778679db5b38`, [`demo-mode7-deck-resource-steps-2026-11-06.md`](https://github.com/ffdiego/YugiRE/blob/experiment/hybrid-native-probe/docs/evidence/demo-mode7-deck-resource-steps-2026-11-06.md). EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

**Arquivos analisados:** `commands-20261106-044114-605159-pid12180.jsonl` (230 linhas) e `demo-two-deck-loads.txt` (1.447 linhas). O primeiro é uma captura dinâmica; o segundo é uma extração estática. As conclusões abaixo distinguem observação e hipótese.

## Resultado resumido

- O hook passivo capturou `FUN_0044BEE0` recebendo **modo 7** pela chamada em `0x0048A550` (return address `0x0048A555`). Esta é a referência concreta para rastrear o gatilho de inatividade do menu.
- A **primeira** `FUN_0044A920(1)` (`caller=0x0044BF7A`) preencheu o **source slot 1** com um deck `41/6/3`, sem mudar o source slot 0 existente (`40/3/6`).
- Entre as duas chamadas, os `0xE2` bytes do slot 1 foram copiados para o slot 0, como previsto pelo código de `FUN_0044BEE0` (`REP MOVSD` 0x38 DWORDs + `MOVSW`).
- A **segunda** `FUN_0044A920(1)` (`caller=0x0044BFDD`) **não modificou as listas observadas**. Não está demonstrado se a rotina recarregou as mesmas cartas, usou cache ou fez um caminho sem escrita.
- **Ambos os source slots tinham exatamente os mesmos índices de carta** (`41/6/3`) quando `FUN_005BFEB0` iniciou o duelo.
- `FUN_00569380` preparou **41 cartas idênticas em composição e ordem de índices para cada lado**; `FUN_00568D80` embaralhou independentemente as ordens mantendo os multiconjuntos.
- **A hipótese anterior de que a segunda carga substitui o slot 1 por um adversário diferente não se confirmou nesta captura.** Pode ocorrer em outras execuções, mas não deve constar como fato.

## Identidade do conjunto observado

SHA256 dos `mainIndices` **ordenados**, separados por vírgula, truncado em 16 caracteres: `65d77013fdeb9210` (41 cartas). Essa assinatura já está documentada em uma demo anterior, com a mesma composição. Não confundir `mainIndices` com os `words` completos do deck: os últimos contêm outros bits e podem diferir por lado.

## Linha do tempo — IDs de evento do arquivo JSONL

| Seq | Evento | Observação |
|---|---|---|
| 2 | `scene_config/enter` | `requestedSceneMode=7`, `sceneMode` anterior 0; `caller=0x48a555`; source 0 = 40/3/6, source 1 vazio |
| 3 | `deck_resource_step/enter` | 1ª carga, `caller=0x44bf7a`, parâmetro bruto 1 |
| 4 | `deck_resource_step/leave` | source 0 = 40/3/6; source 1 = 41/6/3 |
| 5 | `deck_resource_step/enter` | 2ª carga, `caller=0x44bfdd`; **ambos** os sources = 41/6/3, com arrays iguais |
| 6 | `deck_resource_step/leave` | arrays continuam iguais; nenhum índice ou contagem mudou |
| 7 | `scene_config/leave` | `sceneMode=7`; `launchGlobals=[0,1,1793940016]` |
| 8–9 | `scene_factory/enter`, `duel_entry/enter` | factory caller `0x55c797`, duelo caller `0x44c633`; dois sources iguais |
| 10 | `state_reset/enter` | `duelModeWord=14`; decks efetivos ainda vazios |
| 11–14 | `deck_prepare` lados 0 e 1 | cada lado recebe 41 cartas; mesmos `mainIndices` na mesma ordem |
| 15–18 | `deck_shuffle` lados 1 e 0 | permutações independentes; as composições continuam iguais |
| 19 | `state_reset/leave` | dois decks de 41 cartas preparados e embaralhados |

## Estatísticas da captura

- 230 registros no total: `metadata=1`, `marker=1`, `demo_init=20`, `demo_executor=12` (6 pares de entrada/saída), `command=196`.
- Metadata: `capacity=20000`, `seen=229`, `retained=229`, `overwritten=0`. Nenhuma perda declarada.
- Janela observada: 2026-11-06 04:40:16.277 a 04:41:11.866 UTC (55,589 segundos).
- Comando `0x0001` em seq `26,52,90,128,158,206` com lados `1,0,1,0,1,0`. Nenhum `0x0004` de encerramento nesse trecho.
- O primeiro evento `normal_placement` registra `duelRunning=false`, enquanto os cinco seguintes são `true`. Tratar o primeiro como transição/inicialização, **não** como evidência de duelo inativo durante toda a jogada.

## O que o material estático sustenta

1. `FUN_0044BEE0` chama `FUN_0044BBE0` para compor/obter informação associada à seleção, chama `FUN_0044A920(1)`, copia `0xE2` bytes da origem 1 para a 0, prepara outro argumento e chama `FUN_0044A920(1)` novamente.
2. `FUN_0044BBE0` constrói referências a recursos `data/J/file/CPU_` ou `data/J/file/FUL_` e utiliza tabelas e seleção pseudoaleatória. Isso **não prova** qual arquivo foi selecionado neste episódio.
3. `FUN_0044A920` usa `FUN_0055DA10` e possivelmente uma rotina de carregamento `FUN_0044A690` conforme condições. A decompilação exige conferência da ABI e dos argumentos: não se pode concluir só por ela que ambos os caminhos carregam arquivos diferentes, nem que um é pulado.
4. Há preparação de argumento entre as duas chamadas no código de `FUN_0044BEE0`; capturar a identidade do recurso lido em cada etapa é a próxima prova necessária.

## Hipóteses em aberto

- Por que o segundo passo mantém o mesmo `41/6/3`: mesmo recurso selecionado, cache, retorno antecipado, erro mascarado, ou uma transformação que não altera os índices?
- O `40/3/6` visto **antes** do modo 7 é estado preexistente válido ou resíduo de outra cena? Não atribuir a deck do usuário.
- Qual o parâmetro e a lógica do chamador `0x0048A550`? Onde está o contador de inatividade do menu?
- Como o modo de jogo e os bits de lado determinam os buffers usados no bootstrap? Nesta captura `deckSideBit=0`, mas outros modos podem ser diferentes.

## Próximo trabalho proposto ao Codex (sem escrita na memória do jogo)

1. **Prioridade A — chamar da demo:** usar `0x0048A550` para localizar a função que invoca `FUN_0044BEE0(7,...)`; decompilar sua condição de menu/idle e origem dos argumentos. Não assumir que `FUN_005BC700` é o timer de menu.
2. **Prioridade B — origem dos recursos:** cruzar `FUN_0044BBE0`, `FUN_0044E050`, `FUN_0040E960`, `FUN_0044A920`, `FUN_0055DA10`, `FUN_0044A690` e os dois callsites `0x0044BF75`/`0x0044BFD8`. Descobrir a identidade do recurso e o valor de retorno de cada chamada. Preferir hooks passivos opt-in com strings e ponteiros validados.
3. **Prioridade C — repetibilidade:** registrar pelo menos três novas transições `scene_config -> duel_entry` com o processo reiniciado entre testes; comparar hashes e os arrays nos mesmos pontos. Verificar se existe modo 7 com decks distintos.
4. **Só então** decidir um ponto seguro de substituição dos buffers entre carregamento e `FUN_00569380`, sem sobrescrever dados nativos antes de conhecer pré-condições e invariantes.

### Consulta estática sugerida

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report demo-mode7-caller-and-loader.txt `
  -Addresses @(
    '0048a550',
    'ins:0048a500:120',
    '0044bbe0',
    '0044a920',
    '0044e050',
    '0040e960',
    '0055da10',
    '0044a690'
  )
```

A consulta pode precisar de ajustes caso alguma função não esteja identificada no programa carregado no Ghidra. Não realizar patch, mudança dos decks ou disparo artificial do modo 7 nesta fase.
