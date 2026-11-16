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
