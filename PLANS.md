# PLANS — YugiRE: investigação nativa + Joey.Lab

> **Estado:** plano de trabalho, não execução automática. Atualizar após revisões.  
> **Início deste ciclo:** 2026-11-11. **Prioridade:** obter o contexto **completo e observado** que permita comparar o avaliador nativo de combate com `Joey.Core`.  
> **Nome oficial:** `Joey.Lab` (aplicação .NET 10); `native_hooks/` (agentes Frida/DLL C++); `yugilab/` (Python legado temporário). Evitar criar o nome YugiProbe.

## 1. Referências e ponto de partida

- [Arquitetura e inventário da migração do Joey.Lab](docs/research/joey-lab-migration.md): o CLI `summarize` já lê JSONL de capturas reais, referencia `Joey.Core` e preserva desconhecidos; a execução Windows e o streaming seguem no Python.
- [Engine de combate independente](docs/research/ai-combat-engine.md) e [checkpoint](docs/research/ai-combat-checkpoint.md): avaliação C# do subconjunto fechado (Axe Raider e Gemini Elf); não confundir resultado sob contexto declarado com replay nativo de contexto completo.
- [Investigação dos filtros da IA](docs/evidence/ai-gates-full-decision-live-validation-2026-11-11.md): 15 candidatos e sete índices reconstruídos **com métricas nativas**, não com scores calculados integralmente no C#.
- [Corpus de divergências](src/tests/Joey.Tests/Fixtures/ai-combat-differential.json): 22 eventos F40, 14 pares de stats, **zero contextos completos**. No cenário neutro uniforme, 12/14 batem; 304/319 batem *condicionalmente* ao declarar Goblin Attack Force em defesa. A postura nativa não foi capturada.
- [YugiLab Python legado](yugilab/README.md) e [backend](native_hooks/README.md): seeds controladas, decks, captura loss-detecting, workers e experimentos reais existentes. Preservar até equivalência comprovada.
- SHA256 do EXE fixado: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

**Critério global de progresso:** aumentar o número de confrontos cuja *entrada necessária* foi efetivamente observada e cuja saída de quatro bytes é calculada independentemente pelo C#, sem preencher lacunas olhando para o resultado.

## 2. Duas trilhas em paralelo

### Trilha S — investigação nativa (sugestão: Sol / High)

**Escopo próprio:** Ghidra read-only, `scripts/` ligados à análise, `native_hooks/` para hooks passivos, `docs/research/` e `docs/evidence/` de investigação. Não reimplementar o CLI .NET e não alterar `Joey.Core` enquanto a outra trilha trabalha.

| ID | Etapa e entrega mensurável | Dependência | Pare quando |
| --- | --- | --- | --- |
| **S1** | Definir contrato de observação de `FUN_005B0F40`: argumentos/ABI, lado/thread/callId, slots, posição, face-up, flags, LP, stats de `005ADD60/005ADDC0`, seed do descritor, estados hipotético × real, efeitos/vínculos `0056D790/0056D8D0/005699A0/0056B220`. Para cada campo, indicar VA/offset, ponto de leitura, risco de transitoriedade e se está realmente provado. Publicar `docs/research/ai-combat-observation-contract.md`. | Nenhuma | Um campo essencial exigir validação no Windows: marcar desconhecido e separar piloto, não inventar. |
| **S2** | Propor/implementar um modo **opt-in, passivo e limitado** `combat-context` em `native_hooks/`, usando hooks nas chamadas naturais e registros correlacionados por thread/callId. Incluir mocks e testes de integridade, nenhuma chamada artificial à engine, nenhum patch do estado. | S1 com ABI/pontos seguros | Hooks preparados offline e plano de piloto de **1–3 duelos no Windows**. Não declarar validado no Windows. |
| **S3** | Após captura Windows real, auditar entradas/saídas da F40, comparar 304/319 e casos comuns, identificar primeiro campo ausente ou primeira divergência causal; produzir relatório com event IDs/hashes, sem modificar dados originais. | Captura enviada/produzida pelo operador | Capture ausente, incompleta ou com possível alteração de estado: **aguardar operador**. |
| **S4** | Cruzar uma carta por vez: descrição do DAT *local* → função/callback → regra observada → teste C#, começando com Goblin Attack Force. Publicar apenas metadados e referências, sem textos integrais. | Evidência suficiente para uma hipótese testável | Se não houver observação que discrimine as hipóteses, documentar experimento e parar. |

**Handoff S:** `docs/research/handoffs/native-<tema>.md` (novo arquivo por marco; fatos, hipóteses, endereços, SHA, comandos, testes, commit, lacunas). O contrato `ai-combat-observation-contract.md` pertence à trilha S.

### Trilha T — Joey.Lab / C# (sugestão: Terra / Medium)

**Escopo próprio:** `src/Joey.Lab/`, testes .NET específicos, `docs/research/` sobre migração e handoffs da trilha T. Não modificar hooks, ABI, scripts do Ghidra ou o contrato de observação da trilha S.

| ID | Etapa e entrega mensurável | Dependência | Pare quando |
| --- | --- | --- | --- |
| **T1** | Adicionar `Joey.Lab compare` para capturas/execuções, portando inicialmente o comportamento observável de `yugilab/command_words.py` e `yugilab/compare.py`. Comparar seeds, decks, opcodes, outputs de IA, diferenças raw/low/high e integridade. Usar fixtures originais/portáteis como oráculo; não rotular vencedores sem contrato. | Nenhuma | Sem fixtures reais suficientes: entregar o subconjunto verificável e listar o restante. |
| **T2** | Modelos de `ObservedCombatContext`/snapshots que suportem `null`/missing, provenance, timestamp/thread/callId, fase real × hipotética, propriedades brutas; adaptação validada para `Joey.Core` somente se todos os requisitos estiverem disponíveis. JSONL versionado e compatível com campos desconhecidos. | **S1**, ou iniciar somente infraestrutura neutra sem fixar offsets | Sem contrato de posição/flags: manter `Incomplete`, não usar defaults. |
| **T3** | `Joey.Lab differential`: analisar pares completos, rodar `NativeAiCombat.Evaluate` e classificar `Match`/`Mismatch`/`Incomplete`/`Unsupported`; fixtures reproduzíveis e relatório de campos ausentes por carta/ID. Não escolher inputs para fazer outputs baterem. | T2 + captura com contexto observado | Se zero casos completos, registrar `0` e aguardar piloto. |
| **T4** | Migrar mais recursos offline (export de fixtures, auditoria de RNG), um módulo por vez, com equivalência contra Python; depois especificar camada de execução/streaming .NET. **Não remover** o backend Python nesta fase. | T1 e testes existentes | Quando a próxima tarefa exigir PID/Frida/Windows sem piloto seguro: pausar. |

**Handoff T:** `docs/research/handoffs/lab-<tema>.md` (mudanças, comandos, testes efetivamente executados, diferenças preservadas, commit, lacunas). Se precisar mudar `Joey.Core`, preparar proposta com teste e contrato; combinar a posse do arquivo antes de editar.

### Dependências / sincronização

```text
S1: contrato nativo ────────┐
                            ├── T2: snapshots tipados ─── T3: diferencial completo
T1: compare offline ───────┘                        ▲
S2: hook passivo ─── piloto Windows ─── S3: evidência┘

S4: efeitos por carta (somente quando surgir evidência discriminante)
T4: migração restante (somente após T1/T2; sem pressa em deletar legado)
```

**S1 e T1 podem começar imediatamente em paralelo.** O contrato de S1 é um artefato versionado, não uma mensagem transitória da sessão. Terra pode usar versões commitadas do contrato; não precisa esperar S2 para desenvolver validações offline.

## 3. Marcos e critérios de aceite

- **M0 — concluído anteriormente:** `Joey.Lab summarize` em .NET, `native_hooks/` renomeado e Python preservado, conforme `joey-lab-migration.md`.
- **M1 — comparações offline:** T1 passa testes de paridade e preserva diferenças não explicadas (inclusive high words). Nenhum resultado é declarado vencedor por mera associação ao opcode terminal.
- **M2 — contrato de captura:** S1 explicita para cada campo *quando* e *de onde* ler, identifica os campos não provados e não confunde estado real com scratch hipotético.
- **M3 — piloto Windows:** S2 é ativado **só após revisão**; 1–3 duelos naturais, retorno normal do jogo, trace com sequência íntegra, ausência de readError/perda, saída das funções correlacionável e nenhum efeito colateral observado. Um problema → interromper e preservar a captura.
- **M4 — primeira prova diferencial real:** ao menos um confronto com contexto completo observado gera o mesmo buffer de quatro bytes no Joey e em `Joey.Core`; publicar contagem `complete/incomplete/unsupported/mismatch`. Não falsificar cobertura.
- **M5 — execução em escala:** somente após M3/M4, 10 duelos com decks/seeds fixados, depois ampliar. Relatório por carta; não medir fidelidade por número bruto de hooks.
- **M6 — substituição do legado:** equivalência de captura lossless, integridade e encerramento de PID, comparação reproduzível, decks/seeds, múltiplos workers e piloto Windows, incluindo 100 duelos; **exigir aprovação do usuário antes de apagar `yugilab/` ou qualquer módulo Python**. `native_hooks/` pode permanecer com Frida/DLL mesmo quando o host for C#.

## 4. Como executar duas sessões independentes

Criar **duas worktrees/branches**, a partir da `main` atualizada (uma vez, fora das sessões):

```bash
git fetch origin
git worktree add -b work/native-analysis ../YugiRE-native origin/main
git worktree add -b work/joey-lab ../YugiRE-lab origin/main
```

- Inicie um Codex em `../YugiRE-native`, escolha **Sol** e peça: `Siga a trilha S de PLANS.md, iniciando por S1. Faça commits na sua branch; pare nos gates de Windows.`
- Inicie outro Codex em `../YugiRE-lab`, escolha **Terra** e peça: `Siga a trilha T de PLANS.md, iniciando por T1. Faça commits na sua branch; pare se depender de S1 ou do Windows.`
- Cada sessão pode trabalhar em paralelo e continuar pelas tarefas **independentes e desbloqueadas** da própria trilha, sempre com testes e commits pequenos. Não transferir arquivos diretamente entre worktrees.
- Para tornar o progresso visível aos dois, fazer push **somente da própria branch** quando autorizado e revisar/mesclar cada etapa em `main` sequencialmente. Antes de consumir um contrato novo: `git fetch` e integrar a `main` revisada na própria branch; lidar com conflitos conscientemente. **Não fazer push direto em `main` a partir das duas sessões.**
- Caso o Codex ofereça subagentes, eles podem ajudar em *leitura/revisão*; evitar múltiplos escritores no mesmo checkout. Dois worktrees são mais simples para estas trilhas longas.
- Nenhuma sessão se reativa sozinha depois de encerrar um turno. O plano reduz os prompts de retomada, mas iniciar/continuar sessões e aprovar a integração ainda são ações do usuário ou de uma automação explicitamente configurada.

## 5. Critérios para parar sem desperdiçar esforço

Em qualquer trilha, **encerre o turno com um handoff útil** quando ocorrer um destes eventos:

1. É necessária execução/captura no Windows pelo operador, acesso a arquivo proprietário local ausente, uma aprovação ou mudança de privilégio.
2. A hipótese só pode avançar alterando o jogo, adivinhando flags/offsets, preenchendo estado desconhecido ou aceitando contexto inferido como observado.
3. A próxima tarefa depende do commit da outra trilha, ainda não integrado e validado.
4. As tarefas da trilha que podem ser verificadas nesse ambiente terminaram; há somente trabalho especulativo/repetitivo.
5. Houve falha de integridade, crash, regressão significativa ou conflito de trabalho concorrente.

Handoff mínimo: **objetivo, fatos novos, arquivos/commit, testes executados e não executados, riscos, dependência exata e próximo comando/experimento**. Não dizer apenas “aguardando”; especificar o que falta e por quê.

## 6. Comandos de referência

```bash
dotnet restore YugiRE.sln
dotnet build YugiRE.sln --no-restore
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
python3 -m unittest discover -s native_hooks/tests
python3 -m unittest discover -s yugilab/tests
node native_hooks/tests/test_loader.cjs
dotnet run --project src/Joey.Lab -- summarize <captura.jsonl> --json
```

Comandos com DAT exigem arquivo local legítimo; não buscar, distribuir ou commitar binários do jogo. Para validação Windows/Frida, seguir `native_hooks/README.md` e `yugilab/README.md`, em piloto curto e apenas no processo próprio/autorizado.

## 7. Coordenação e prioridades quando tudo estiver bloqueado

O Sol entrega **um contrato comprovável para produzir observação nova**; o Terra entrega **uma ferramenta que usa essa observação para encontrar o próximo erro causal no C#**. Os dois se encontram na fronteira de snapshots versionados.

Não priorizar, por enquanto: Joey headless completo, reescrita prematura de Frida em C#, catálogo massivo de efeitos a partir de descrições, dezenas de novos hooks sem perguntas precisas ou remoção do legado. O objetivo é uma única prova diferencial end-to-end de alta qualidade, e então ampliá-la.
