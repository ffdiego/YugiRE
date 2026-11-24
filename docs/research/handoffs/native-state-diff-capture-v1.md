# Handoff: observador passivo de estado por comando — v1

## Branch e dependências

Branch `feature/native-state-diff-capture-v1`, empilhada sobre
`feature/native-effect-discovery-queue-v1` (PR #6, Draft).
Não fazer merge isolado nem modificar `Joey.Core`.

## Entregas

- `native_hooks/native_state_sampler.js`: leitura de dois blocos
  jogador (stride 0xD44), onze posições de 0x90, índices internos
  de mão e flags brutas. Captura baselines e diffs com limites.
- `native_hooks/yugire_dual_loader.js`: liga/desliga observação
  no mesmo hook de produtor de comandos existente, sem chamar
  funções nativas. Cada comando amostrado carrega flag própria;
  cada baseline/delta é relativo a seq da captura.
- `native_hooks/native_rules.py` e `yugilab/__init__.py`: opt-in
  `--trace-state`, condicionado a laboratório de 1 worker e no
  máximo 3 duelos durante o primeiro piloto.
- `native_hooks/duel_capture.py`: metadados e contadores no footer;
  exige baseline por duelo completo quando opt-in.
- `yugilab/state_interactions.py`: analisa JSONL íntegro offline;
  valida before/after de diffs e contagens, produz transições
  genéricas de estado e janelas CF Rain + indícios de Skull,
  sempre com conclusão `Unverified`.
- Testes sintéticos novos/estendidos em `native_hooks/tests/`
  e `yugilab/tests/`; protocolo completo em
  `docs/research/native-state-diff-capture-v1.md`.

## Validação e limites

- O código fonte JavaScript `native_state_sampler.js`,
  `yugire_dual_loader.js` e os dois arquivos de teste JS
  passaram por **checagem de sintaxe ECMAScript V8** na
  orquestração desta sessão.
- O sampler puro foi efetivamente **executado contra memória
  sintética em V8**: detectou saída do slot 5, aumento do
  contador de Cemitério e queda de LP; rejeitou mão >80.
- **Testes completos em Node/Python e jogos Windows não executados
  nesta sessão.** É obrigatório validar na máquina Windows
  antes de iniciar o piloto.
- Snapshot por comando é **não atômico** e feito em
  `command_producer_onEnter`: diffs são associados à observação
  e não prova causal. Saída do campo + aumento de grave-count
  não prova identidade da carta movida ao cemitério.
- Flag de face/permanência ativa ainda precisa de pesquisa;
  `positionBit1Set` é rótulo bruto, não confirmação de armadilha.
- Cap de 3 duelos/1 worker e uma campanha inicial de apenas 1 duelo
  sem trace AI. Interromper em readError, evento perdido, crash
  ou cleanup inesperado.

## Próximos passos

1. Windows: executar JS sampler, loader, testes Python de
   streaming e state_interactions.
2. Executar um único duelo com flag --trace-state e sem trace AI.
3. Checar contadores footer e gerar state-report.json,
   state-summary.md. Revisar custos temporais e estado observado.
4. Caso apareça Rain, verificar persistência de Skull em campo e
   mudanças de grave-count/LP, **sem ainda atribuir gatilho**.

## Primeiro piloto nativo e investigação temporal (16/11/2026)

O operador informou que todos os testes offline anteriores passaram e enviou os relatórios gerados por 1 duelo de `--trace-state`. Relatório aponta captura original `duels-20261117-020520-485960-pid20416.jsonl`, SHA256 `e76d67678a3843d14541fed5ec5d94aabbca603a129554ab2f46f69e27919994`, com 1 terminal, integridade true, 382 amostras, 1 baseline, 108 deltas, 0 erros, 18 comandos não amostrados e 178 transições. Os bytes do JSONL original **não foram compartilhados nesta etapa**.

Rain CF seq335, Skull Invitation índice414 no slot5 do lado0, Rain índice969 no slot7 seq336, LP +1000/+1000 na seq340, Rain slot7→vazio junto com incremento de Cemitério do lado0 na seq344, comando de dano 300 seq346 e perda real de 300 seq348. Observação **consistente** com o texto da armadilha, sem prova causal ou destino individual da carta.

Há **13 incrementos unitários de graveyardCount** em todo o duelo e **14 quedas de 300 LP**. Os 13 incrementos têm perda correspondente do mesmo lado após 3–5 seq, sem reutilizar perdas; queda extra seq455 do lado1 (antes de incremento seq457) está sem pareamento causal. Evidência curada em `docs/evidence/native-skull-graveyard-lp300-correlation-2026-11-17.md`.

Adicionado `yugilab/temporal_correlations.py` com correlação genérica temporal não causal e `yugilab/tests/test_temporal_correlations.py`; incluído resultado `graveyardLp300TemporalCorrelations` no relatório de `yugilab.state_interactions` e teste de integração. **Essas mudanças adicionais de pós-análise ainda não foram executadas no Windows**. Testar os dois arquivos e reprocessar o mesmo JSONL em output v2; não é necessário novo duelo. Depois, planejar controle negativo Skull ausente e controle positivo Skull confirmada face-up, mais prova de movimento real ao Cemitério.

## Validação do relatório v2 e controle sem Skull (16/11/2026)

Operador confirmou no PowerShell o resultado do reprocessamento v2: `unitGraveyardIncreases=13`, `lpLoss300Count=14`, `pairedCount=13` e único `unpairedLpLoss300={duelIndex:0,side:1,lossSeq:455,lossDelta:-300}`. Testes Windows: `test_temporal_correlations.py` 4/4 PASS, `test_state_interactions.py` 6/6 PASS, segundo log do operador.

Incremento posterior nesta branch: `yugilab/prepare_skull_negative.py` produz dois arquivos de deck de controle sem índice 414 a partir dos decks originais, selecionando apenas normais validados do catálogo já presentes no deck, preservando outros índices/ordem/comprimento e gravando hash/manifest; falha se original não tiver Skull ou faltar substituto verificado. Incluído `yugilab/tests/test_prepare_skull_negative.py`. `yugilab.state_interactions` passa a expor `captures[].skullFieldPresence` com amostras de presença/ausência e entradas não observadas (não diz se armadilha está ativa). Atualizado teste sintético. Essas alterações **não foram executadas no Windows**, portanto o próximo gate é apenas preparação offline + testes, antes do segundo duelo. Sequência completa em `docs/research/native-state-diff-capture-v1.md`.

Contrastar com controle negativo de 1 duelo, mesmo seed e speed, mas não presumir trajetória idêntica nem causalidade provada. Validar `skullFieldPresence.presentSamples=0`, `graveyardCount` aumentando e distribuição de dano -300. Se ausência de variação no Cemitério, controle é inconclusivo. PR #7 permanece Draft e sem merge.
