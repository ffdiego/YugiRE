# Handoff Codex Fedora — lote P018/P002 sem intervenção (21/11/2026)

## Objetivo

Investigar de forma autônoma e **baseada em evidência** o sistema de recuperação de cartas do GY do Joey original, com foco em P018 (Magician of Faith / Mask of Darkness), e depois explorar a máquina compartilhada de decisões P002 **somente se sobrar escopo**. O resultado desejado é um conjunto de notas e verificações rastreáveis, não um número artificial de cartas 'Verified'. O usuário não estará disponível durante a execução; não pedir confirmação.

## Preparação e restrições

1. `cd ~/prj/joeyre/yugire`; ler `AGENTS.md`, `PLANS.md`, `docs/research/p018-native-flip-recovery-2026-11-21.md`, `docs/research/p002-native-decision-dispatch-2026-11-21.md`, [issue #28](https://github.com/ffdiego/YugiRE/issues/28), [issue #24](https://github.com/ffdiego/YugiRE/issues/24).
2. Verificar `git status --short`, branch, `git fetch origin`; não perder modificações nem substituir evidências locais. Se estiver em `main` e não houver mudanças tracked conflitantes, usar `git pull --ff-only`. Se houver divergência, trabalhar em worktree própria baseada em `origin/main` e deixar isso explícito no relatório; não forçar sincronização.
3. Ferramentas disponíveis: Ghidra 12.2 em `~/prj/joeyre/tools/ghidra_12.2_PUBLIC`, projeto `ghidra-projects/Joey.gpr`, `scripts/run-archive-query.sh` e `scripts/QueryArchive.java`, Java, Python, .NET 10 se disponível. `analyzeHeadless` opera **-readOnly -noanalysis**. `tempfunc:` é necessário quando a função não foi definida; `ins:` sem `tempfunc:` pode pular até a próxima função conhecida — conferir o endereço inicial nas saídas.
4. **Não rodar joey_pc.exe no Fedora** nem simular observação Windows, não modificar EXE/DAT/saves, não publicar traces brutos ou dumps decompilados enormes. Guardar saídas completas em `scratch/`; `run-archive-query.sh` escreve em `docs/evidence/`, então mover para `scratch/` quando forem brutas e deixar somente extratos curtos e auditáveis se forem úteis no Git. Manter SHA256 do EXE nas notas.
5. Sem força-push, sem commits de código sabidamente quebrado. Um agente escritor por vez em `main`; análises paralelas apenas em árvores isoladas. Sem mudanças em regras ou progresso/badges sem divergência demonstrada e testes. Ser conservador com prototypes/ABI do decompiler.
6. Usar **consultas pequenas** de 2–4 funções e contagens de instruções limitadas, preferindo recortes de casos a decompilar repetidamente um switch de centenas de linhas; o miniPC N150/16GB não deve receber processos headless Ghidra simultâneos desnecessários. Evitar loops infinitos: ao atingir uma hipótese sem progresso após duas abordagens independentes, registrar blocker e passar à próxima frente.

## Investigações, em ordem

### A. Política de seleção automática — prioridade máxima

- Decompilar `FUN_005AF600`, especialmente ramos por resource IDs 101 (`0x65`) e 427 (`0x1AB`); confirmar inputs `side/cardIndex/options`, uso de RNG, prioridade, primeiro/último e desempate.
- Correlacionar `FUN_005AF5A0` com `FUN_0040CAA0` e com buffer `00A582A4`; confirmar se CPU devolve índice de candidato ou índice bruto de zona.
- Exercitar raciocínio com 0, 1, 2+ candidatos e duas ocorrências idênticas. **Sem executar o jogo, classificar como inferência** onde a escolha depender de runtime.
- Entregar uma nota curta `docs/research/p018-cpu-selection-2026-11-21.md` com endereços, pseudocódigo reduzido e pendências.

### B. Filtro compartilhado e pós-processamento de candidatos

- Inspecionar os dois cases em `FUN_00599E30` e as chamadas finais `FUN_005675F0`, `FUN_00569CB0`, `FUN_00570BE0`; verificar possíveis remoções adicionais em lugar.
- Confirmar (por assembly) offsets e stride de GY/candidatos, limite 127, marcador de origem 4, formato dos registros e se ordem original é mantida.
- Comparar resultados ao `CardSelectionRules.Candidates` e `CardSelectionChecks.cs`; corrigir notas se houver divergência, jamais ocultar incerteza.
- Produzir notas auditáveis de critérios (e testes sintéticos somente se uma regra concreta for evidenciada).

### C. Fluxo humano da seleção, ocorrência e transferência

- Seguir `FUN_0040C940`, `FUN_0040CAB0`, `FUN_0040CAA0`, `FUN_00592A50`, `FUN_005B9170`, `FUN_00575D50`; descobrir contrato do 'modo 6', a forma de confirmar escolha e o destino GY→Hand.
- Reconstituir máquina de estados de `0x00593530` via `DAT_00A55C8E`, separando chamada, espera, resposta e comandos; não inventar frame/milissegundos.
- Comparar com `PendingCardSelection` e casos em `src/tests/Joey.Tests/CardSelectionChecks.cs`.

### D. Gatilho FLIP e contexto de combate

- Examinar `FUN_00584270`, `FUN_00568AD0`, `FUN_00575D50`; identificar o teste `(ctx+4 & 0xE000)==0x4000`, bit `0x4`, side e índices dos eventos.
- Diferenciar Flip Summon e revelação por ataque se o binário permitir; separar evidência de suposições. Registrar boundary quando Ghidra tiver ABI suspeita.

### E. Decisões opcionais P002 — somente depois de A–D

- Investigar `FUN_0043FB60` (vtable +0x1C), `FUN_0043FAF0`, `FUN_0043FAA0` e writer de `dialog+0x10` para converter clique em Yes/No; `FUN_005BF230` para continuação condicionada.
- Confirmar (por decompilação) `FUN_00578460` e `FUN_00579790` como custo/comando, sem afirmar fase/owner ainda não observados.
- Preservar regra de elegibilidade estrita `LP > 1000` e bit automático por lado da cena demo=7. Não tornar `EffectPromptMode.Automatic` sinônimo de aceitar custos sem contrato arquitetural e testes.

### F. Se tudo acima ficar suficientemente delimitado

- Conferir reuso do mesmo sistema em **P006 Sangan/Witch** (não são FLIPs) ou **P164 recrutadores**. Escolher só uma família, que compartilhe helpers realmente observados, e registrar três diferenças relevantes em vez de expandir indiscriminadamente.
- Não entrar em animações, DirectPlay, seleção de decks da CPU ou PRs grandes nesta sessão.

## Produção e controle de qualidade

- Em cada investigação: comando executado, SHA256, endereços e offsets, fatos vs inferências vs inconclusões, mapa de funções chamadas e questões abertas. Manter os dados brutos em `scratch/`; **versionar só relatórios curtos**.
- Preferir evoluir os docs atuais a criar dezenas de arquivos soltos. Vincular notas a issues #28 e #24.
- Modificar `Joey.Core` apenas para diferença concreta demonstrada; caso modifique, manter uma unidade vertical pequena com teste em `src/tests/Joey.Tests`, verificar `dotnet build YugiRE.sln` e `dotnet run --project src/tests/Joey.Tests`; declarar testes não executados se SDK não disponível.
- Para docs/scripts: `git diff --check`, testes de Python alterados e checagem de links/caminhos quando aplicável. Evitar criar porcentagens ou alterar manifest sem carta nova plenamente implementada.
- **Commitar unidades concluídas pequenas** em `main` quando o estado permitir e fazer `git push origin main` por fast-forward; se houver conflito remoto, preservar os commits e parar o push, sem `--force`. Jamais descartar mudanças existentes.
- Escrever ao final `docs/research/handoffs/p018-p002-night-results-2026-11-21.md` com resumo executivo, evidências (funções e relatórios), o que não foi concluído, divergências relevantes com o C#, testes reais, commits e **3 próximos passos ordenados**.
- Se terminar os itens cedo, fazer revisão de coerência das notas e cobertura de testes em vez de iniciar projetos novos.

**Critério de sucesso:** ao menos A e B bem delimitados e C/D avançados onde houver evidência; não criar código especulativo só para dar aparência de progresso.
