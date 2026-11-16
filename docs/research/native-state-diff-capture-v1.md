# State-diff capture v1 — observação passiva no Joey

Status: piloto opt-in, sem prova de causalidade de efeitos. Empilhado na
branch `feature/native-state-diff-capture-v1` sobre a pesquisa de efeitos
`feature/native-effect-discovery-queue-v1` (PR #6), sem merge em main.

## Objetivo

Substituir o ciclo 'um hook novo por carta' por um observador genérico
de estado que sirva a diversas famílias de efeitos: retirada/inserção
em campo, mudanças de mão, contagem de Cemitério, LP e associações.

**A coleta não instala hook novo**: opt-in sobre o Interceptor existente
na entrada de `FUN_005B9170` (emissor comum de comandos), executado
na thread do próprio jogo. A leitura é somente de memória conhecida
(`0x00A55D64 + lado*0xD44`, 11 registros físicos de `0x90`).
Não carregamos a DLL de regras neste modo. O observador não muda EXE,
DAT, regras ou saves. Os overrides de deck/seed/speed que já existem
no YugiLab continuam sendo modificações experimentais separadas.

## Contrato de eventos

Opt-in com `--trace-state` (1 worker, 1 a 3 duelos; sem `--trace-ai`).
Sem a flag, nenhuma amostra nova é realizada e JSONL permanece
compatível com as capturas anteriores.

- `metadata.stateTrace`: schema `joey-command-entry-state-delta-v1`,
  `samplingPoint=command_producer_onEnter`, `nonAtomic=true`.
- `command.stateSampled=true`: houve leitura válida no instante da
  entrada do produtor; ausência de `stateObservation` indica que
  **não houve diferenças entre duas leituras válidas consecutivas**.
- `stateObservation.kind=baseline`: primeira amostra após início/reset;
  `sides` contém LP, mão, deck, contagem de Cemitério, byte status
  bruto, flags terminais, 11 slots com índice interno, ocorrência,
  posição, flags brutas, contador e associações tipadas somente
  por palavra bruta; `handIndices` capados em 80.
- `stateObservation.kind=delta`: diferenças em um ou ambos os lados
  e `previousSampleSeq`, com before/after de contadores/LP/posições.
- `stateReadError`: leitura inválida; o analisador descarta a
  continuidade e requer nova baseline. `associationCount >32`
  recebe `associationCountValid=false` e `associations=null`,
  **nunca array vazio** que pudesse fingir ausência de vínculos.
- `capture_summary.stateTrace`: amostras, baselines, deltas, erros;
  `LiveDuelCapture` exige pelo menos uma baseline por duelo completo.

**Semântica:** cada estado é observado **antes** de enfileirar o
comando que carrega a observação. Se uma diferença aparecer no comando
N, ela aconteceu em algum momento desde a amostra anterior, e **não**
foi necessariamente causada pelo comando N. Todas as leituras
sequenciais dentro de uma amostra são **não atômicas**. Operações
transitórias entre dois produtores podem passar despercebidas.
Não deduzir que uma posição desaparecida foi enviada ao Cemitério
somente porque `graveyardCount` aumentou.

## Validação local prévia (sem executar Joey)

Trabalhe no Windows em um worktree da branch acima. Execute:

```powershell
git pull --ff-only
node .\native_hooks\tests\test_native_state_sampler.cjs
node .\native_hooks\tests\test_loader.cjs
python -m unittest discover -s native_hooks/tests -p test_duel_capture.py -v
python -m unittest discover -s yugilab/tests -p test_state_interactions.py -v
```

Se qualquer teste falhar, não execute um novo duelo.

## Primeiro piloto Windows: apenas UM duelo nativo

Use backup dos saves e o executável original previamente fingerprintado.
Os decks já gerados do primeiro par apresentam Rain/Skull nas
capturas anteriores, mas **um único duelo não garante que aparecerão**.

```powershell
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab `
  --exe ..\passion\joey_pc.exe `
  --duels 1 --workers 1 --trace-state `
  --deck-0 .\scratch\native-discovery-decks-v1\round-01-A.json `
  --deck-1 .\scratch\native-discovery-decks-v1\round-01-B.json `
  --seed 12345 --speed-value 600 `
  --output-dir .\scratch\native-state-round01-one-v1
```

Piloto deliberadamente menor/lento para conferir overhead de leitura.
Sem `--trace-ai`; não rodar múltiplas instâncias no mesmo save.

Verifique: 1 terminal, `complete=true`, `overwritten=0`,
`capture_summary.stateTrace.baselines>=1`,
`stateTrace.samples>0`, `stateTrace.readErrors=0` se possível,
PID encerrado e deck override verificado por lado. Em caso de
crash, timeout, buffer perdido, erro de memória ou falha de
desligamento do PID, interromper; não tentar 3/100 duelos.

## Análise offline dos movimentos de estado

```powershell
python -m yugilab.state_interactions `
  --input .\scratch\native-state-round01-one-v1 `
  --output-dir .\scratch\native-state-report-one-v1
```

Saídas `state-report.json` e `state-summary.md`. Cada evento de campo
representa uma **mudança de slot entre amostras**, e cada aumento de
Cemitério é uma **mudança de contador**. O relatório só cria casos
Rain/Skull quando detecta CF da Rain (resource ID 1315), inspeciona
se a carta Skull (índice interno 414) estava numa posição física 5–9
na mesma amostra, e registra janelas até 48 eventos posteriores:
saída de Rain do campo, contagem do Cemitério, comandos de 300 LP
e LP -300. `positionBit1Set` é bit bruto, **não** garantia de
que a armadilha estava ativa. Conclusão sempre `Unverified`.

Se Rain não for usada no único duelo, os eventos genéricos de campo,
mão, Cemitério e LP continuam sendo o objetivo do piloto; não fazer
uma bateria grande para forçar a interação.

### Critérios para avançar

1. Revisar um JSONL íntegro com exemplos de baseline/delta e overhead aceitável.
2. Confirmar o significado de flags de face/atividade e origem/destino
   na versão exata do executável antes de qualquer atribuição causal.
3. Só depois fazer campanha de 3 duelos ou separar agentes por
   família: triggers de Cemitério, cartas contínuas, custos/alvos,
   cura/dano. Usar branches/worktrees separados e evidência versionada.

Arquivos brutos, saves e DAT continuam em `scratch/`; não enviar ao Git.
