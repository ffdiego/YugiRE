# Checkpoint — engine de combate independente

## Objetivo
Produzir scores/eventos em C# em vez de depender dos valores intermediários
nativos. EtapaA: confronto puroB0F40; depois B0D90/B0B90/B0A50/baseline e
candidato→ranking. Nenhum Joey noFedora,nenhumasave/patch/DB persistido.
Branchmain, commitsincrementais, preservar dirtyanteriores.

## Baseline desta sessão
HEADinicial22bbd06. Buildsemwarnings/erros;454checksC#;16testesPythonai* e
13decode_ai*,todosOK. Rankingobservado15/15 e7/7 não é scoreindependente.
Documentosdeentradalidos:ai-decision-engine/checkpoint, validaçõesgates/detail,
relatóriosGhidraprincipais,NativeAiRanking/AiRankingChecks ehelpersPython.

## Incremento em andamento
- NativeAiCombat.cs: tipos12bytes/4bytes, contexto desconhecidodefault,
  projeçãooculta1000/1900, aritméticawrap32/int16 e efeitoslocais deB0F40.
  EtapaA passou531checks (77novos); não está integradaaoDemoDuel.
- Agenteattributes_subset: NativeAiAttributes.cs/AiAttributeChecks.cs,
  deltasconfirmados,halves/aliases,pipelinefechadoevitandoefeitosdesconhecidos.
- Agentecorpus: somenteJSONLdd2029... existe noFedora;22F40,14comdoisstats
  filhos,8semstatsrival(possíveloculto). Nãoinferirposição/flags/effects
  ausentespelosoutputs. NovosJSONLdetail/gatesausentes; usarMD/fixtures.
- Agenteheadless: três consultasdelimitadas prontas,bancoLIBERADO;
  selftestsemcompiladorMinGW/gcc/clang,nãoinstalar. Relatórioemprodução.

## Regras de precisão
IsSupported=false para contexto nãoresolvido; desconhecido nãoé zero.
Statscalculados explícitos sãoentrada permitida, mas origemcapturada/declarada
precisa ficarclara. Score/utility é avaliação,não efeito real emLP.
Nãoalegar22replaysdecombatecomcontextofullsefaltaminputs.

## Próximos passos
1. Testarconfrontos ecorpus nativo, corrigir discrepâncias comassembly.
2. CommitEtapaAcomlimitesclaros.
3. Sequênciaimutável/isolada,sortnãostável,alvosemáscara,soma/contadores.
4. Baseline/candidatos/ranking comgatesexplícitos e atributosgradualmenteC#.
5. Consolidarai-combat-engine.md eheadless-feasibility.md;continuartarefasoffline
   promissoras seminstrumentaçãoWindows desnecessária.

## Preservação
Dirtyanteriores:docs/15-rea-integration-todo.md,scripts/README.md,
scripts/run-archive-query.sh,duel-crash-sites.txt,toolchain*. Não commitar.
EXE SHA esperado db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.

## Último marco
EtapaA validada com vetores debranches e22buffers nativos deformat/utilidade.
Contextos reais incompletos explicitados; nenhum scorecapturado usado como
entrada para geraroutroscore. Próximaetapa sequência eattrs.

## Marcos adicionais
- `e8646c7`: confrontoA puro,531checks.
- Atributos:FromPlainCard jáderivaATK/DEF/type/attributo doDATpara2recursos;
  overloadcomatributoexplícito preservado para entradas resolvidas.
  21checksattrs(70deltas/28half+DAT2buffers)passaram;componenteattrs elevou531→552 checks
  Headless003relatórios delimitadossemvantagem demostrada sobreC#,
  selftestMinGWnãoexecutável. Relatóriocompletoheadless-feasibility.md.
- SequênciaB compilaepassou20checks novos(total572)antesúltimapropriedadederivada;
  próximaqueryvalidoupolíticaADE40 eoverloadatributo. Fecharcommitseparado.

## Headless delimitado concluído
Verheadless-feasibility.md:8raízes118nósdepth5;dispatchers409nósdepth3;
fronteiraB0F40com7serviçosstop=9nós,rootOPEN. PURE não excluiglobais.
Importsheap/criticalsection viaCARD_IDlazy;semharnessnativo e semWin32stubs.

## Próximo commit B
Sequência eCLI passaram comcontextoslocais;CandidateSearch jápassou13checks
mas entraemcommitposterior. Reviewindependente fechouRuleFlagsnullable e
contradiçãoB5A0, comregressões. Doisvanillas apenas,nãoestenderwhitelist
poraparência; novasCartas semgatescompletos devemUnsupported.

## Marco B concluído
Sequência local, baseline e CLI independente: build e 583 checks C# passaram.
Inclui regressões de flags desconhecidas, política de ameaças, sort não estável,
empates, remoções e overflow DWORD/int16. Contrato fechado em dois monstros
comuns; não é replay integral de uma decisão observada. Próximo incremento:
publicar o fluxo candidato → score calculado → ranking e o corpus diferencial.
