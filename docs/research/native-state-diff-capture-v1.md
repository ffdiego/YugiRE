# State-diff capture v1 — observação passiva no Joey

Status: piloto opt-in, sem prova de causalidade de efeitos. Empilhado na
branch `feature/native-state-diff-capture-v1` sobre a pesquisa de efeitos
`feature/native-effect-discovery-queue-v1` (PR #6), sem merge em main.

## Primeiro duelo validado — evidência recebida (16/11/2026 horário local)

O operador enviou somente `state-report.json` e `state-summary.md`.
Captura original reportada: `duels-20261117-020520-485960-pid20416.jsonl`,
SHA256 `e76d67678a3843d14541fed5ec5d94aabbca603a129554ab2f46f69e27919994`.
O JSONL original permanece no Windows; **não revalidamos o hash do bruto**
nesta sessão. Auditoria do relatório: 1 terminal íntegro, 382 amostras,
1 baseline, 108 deltas, 0 readErrors, 18 comandos sem amostra e
178 transições individuais.

Rain (índice 969) tem CF seq335; Skull Invitation (índice 414)
já aparece no slot 5 do lado 0. Rain entra no slot 7 na seq336,
LP de ambos aumentam +1000 na seq340, Rain sai do campo
na seq344 simultaneamente a +1 no contador do Cemitério do
lado 0, e há comando de 300 e delta -300 no lado 0 em seq346–348.

De maior valor para investigação por lotes: **13 incrementos unitários**
do contador de Cemitério tiveram perda de 300 LP do mesmo lado
em até 3–5 seq, mas houve **14 perdas de -300** no total,
incluindo uma sem pareamento direto na seq455. É evidência temporal
forte, não mecanismo/causalidade confirmado. Skull permaneceu
observada no slot 5 após sua colocação na seq67.
Ver `docs/evidence/native-skull-graveyard-lp300-correlation-2026-11-17.md`.

Nova análise genérica (implementada **depois** do primeiro relatório)
quantifica essas correlações sem duplicar perdas e gera campos
`graveyardLp300TemporalCorrelations`. Precisa dos testes e de
reprocessar **o mesmo JSONL**, sem executar novo duelo:

```powershell
git pull --ff-only
python -m unittest discover -s yugilab/tests -p test_temporal_correlations.py -v
python -m unittest discover -s yugilab/tests -p test_state_interactions.py -v
python -m yugilab.state_interactions `
  --input .\scratch\native-state-round01-one-v1 `
  --output-dir .\scratch\native-state-report-one-v2
```

**Esperado, a confirmar:** 13 pares temporais, 14 quedas de
300 LP e uma queda sem pareamento (seq455, lado1).
Analisar o caso extra antes de atribuir tudo a Skull.

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
