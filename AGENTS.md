# AGENTS.md — orientações para agentes do YugiRE

Projeto: reimplementar **Yu-Gi-Oh! Power of Chaos: Joey the Passion** em C# / .NET 10, com **Joey.Core** para regras e **Joey.App (raylib-cs)** para a experiência visual clássica.

## Prioridade de trabalho

**Funcionalidade jogável e manutenção simples têm prioridade sobre paridade interna de assembly.** Não tente reproduzir opcodes, callbacks, buffers e arquitetura da engine original quando uma regra de domínio C# for suficiente. Preserve a engenharia reversa existente como referência, não como bloqueio geral.

- Implemente **verticalmente**: regra → integração em `DemoDuel`/motor → teste → UI se aplicável → atualização de `CARDS.md`.
- Para efeitos claros, use `Card.Description` do DAT **local**, regras da época de Joey, perfis de callback (`docs/evidence/card-callback-catalog.json`) e evidências já documentadas. Não exija captura Windows para começar uma implementação simples.
- Agrupe cartas por mecânica; prefira tabelas declarativas e primitivos reutilizáveis a um `if` espalhado por cada ID.
- Se houver ambiguidade material (timing, custos, alvo, cadeia, comportamento exclusivo de Joey), escolha uma implementação conservadora e testável, registre a hipótese no teste/`CARDS.md` e use o executável original como oráculo **sob demanda**.
- Não transforme uma hipótese em resultado verificado. Diferencie explicitamente *implementado no sandbox*, *testado em C#*, *observado no Joey* e *inferido*. Índice interno, resource ID e slot são distintos.

## Arquitetura

- `src/Joey.Core/`: catálogo, estado de duelo, cartas, atributos, efeitos e IA; **sem dependência de Frida/Windows/Python**.
- `src/Joey.App/`: raylib, UI, assets locais, sons e timings; animações **não** determinam as regras.
- `src/tests/Joey.Tests/`: testes rápidos de cenários jogáveis e regressões, com fixtures mínimos.
- `src/Joey.Lab/`: análise e simulação offline; comandos existentes não devem ser anunciados como implementados sem conferir o CLI.
- `yugilab/` + `native_hooks/`: captura do executável original, com preflight e gerenciamento do PID próprio. Preservar o ferramental; usá-lo para esclarecer dúvidas, não para atrasar o C#.
- `PLANS.md`: prioridades vivas. `CARDS.md`: estado funcional. `docs/cards/playability-manifest.json`: fonte versionada de status por resource ID. `scripts/card_playability.py`: métricas pelo DAT local sem dados proprietários no Git.

## Definição prática de pronto

- **Pending:** sem regra implementada. **Partial:** primitivo ou integração parcial. **Done:** comportamento completo **no escopo anunciado** em `Joey.Core`, exercitável no duelo e coberto por testes C#. **Verified:** adicionalmente comparado com observação do Joey original. Um teste sintético não vira `Verified`.
- Marque as limitações de escopo (por exemplo, ausência de correntes, efeitos globais ou interação com outras cartas); não declare todo o catálogo de monstros pronto apenas porque a invocação e o combate básicos funcionam.
- Prefira fatias menores que deixem o jogo melhor no fim da sessão. Um número grande de cartas listadas sem ativação jogável não é progresso funcional.
- A cada carta nova, atualizar `CARDS.md` e `docs/cards/playability-manifest.json` com resource ID exato, família, status e evidência de teste. As badges do README e o relatório `docs/cards/PLAYABILITY.md` são recalculados pelo workflow `playability-badges.yml` em pushes na `main`. Nunca editar manualmente os percentuais.
- Os 264 recursos classificados como `Normal` são `Done` apenas para a mecânica básica de invocação/set/tributos/batalha testada em C#. Isso **não** torna efeito, fusão ou ritual completos nem confere `Verified`. Se o DAT real divergir da classificação de referência, revisar o inventário antes de confiar na badge. Não declarar Done por catálogo/callback ou invocação genérica de monstros.
- Para medir porcentagem: `python scripts/card_playability.py --dat ../passion/data.dat`. Denominador é a união de packs (771 registros) ou Joey (350), não os 1.114 registros nem o número de callbacks. Sem DAT local, não inventar percentuais.
- Efeitos FLIP no sandbox passam por `FlipEffectRules` e pelo gatilho em `DemoDuel` (Flip Summon/revelação no combate/efeito). Dano e marcação de destruição de combate precedem FLIP; cards marcados não podem voltar à mão com Penguin Soldier (601), ainda que permaneçam na zona até o fim da etapa. Pending targets bloqueiam ações; `FinishFlipTargets` fecha 0–1 alvos opcionais. Hane-Hane (540) exige exatamente um alvo legal e compartilha apenas a infraestrutura de bounce com Penguin Soldier (601), que é opcional com 0–2 alvos. Morphing Jar (590) descarta mãos e compra cinco; Morphing Jar #2 (1163) é um FLIP **distinto**, que embaralha monstros para Main Decks e escava até quotas por dono. O código do sandbox está em `DemoDuel.ResolveMorphingJar2`; não inferir Extra Deck, tokens nem posse fora do controlador. Próxima meta é UI de seleção reutilizável para P018 (427/101), P006 (47/573) e P164 (1123/1108/1117), mas somente P018 é ativado por FLIP.
- Trilha de apresentação **pausada após a prévia de ativação**. Não expandir animações/vozes automaticamente. `src/Joey.App/DuelActivationScreen.cs` + `src/Joey.LookAndFeel/ActivationEffectPlayback.cs` implementam três passes via YGAs locais e timing escolhido pelo cliente (não nativo); detalhes em `docs/architecture/activation-preview.md`. Priorizar a Iteração B: efeitos das cartas.
- Fórmulas e modelos visuais da branch `analise-visual` foram consolidados em `Joey.LookAndFeel` (`HandPresentation`, `NativeDuelLayout`, `NativeActivationTimeline`, `NativeExplosionTimeline`, `NativeVisualDispatch`); consultar `docs/architecture/native-visual-contract.md` antes de inferir FPS ou duração. A tabela x16.16 deve ser fornecida/validada, não substituída silenciosamente por `Math.Cos` como se houvesse paridade.
- `Joey.LookAndFeel` é independente de `Joey.Core`/raylib e contém políticas de duração, perfis e preferências de voz. O Core jamais consulta tempos ou animações. No `Joey.App`, os cues provisórios vêm de mensagens de texto; substituir futuramente por eventos tipados. `--mute-joey` ainda não silencia áudio real (não há player).

## Git e trabalho entre máquinas

**Fluxo preferencial para um agente escritor:** atualizar `main` com `git pull --ff-only`, implementar uma unidade pequena, executar testes, commitar e fazer push para `origin/main`. Assim, outra máquina pode continuar com um pull simples. Não fazer force-push nem publicar código sabidamente quebrado.

**Concorrência:** apenas **um escritor por vez na `main`**. Agentes em paralelo podem pesquisar ou trabalhar em branches/worktrees isoladas; integrar sequencialmente após revisar conflitos. Não permitir que duas sessões independentes façam push concorrente para a mesma `main` sem coordenação. PRs continuam úteis para mudanças arriscadas, grandes ou não validadas.

## Validação proporcional

- Regras C#: `dotnet build YugiRE.sln` e `dotnet run --project src/tests/Joey.Tests`.
- Backend alterado: `python -m unittest discover -s native_hooks/tests`, `python -m unittest discover -s yugilab/tests` e testes Node pertinentes.
- Não afirme que um comando passou sem executá-lo. Se faltarem .NET, DAT original ou Windows, explique qual validação ficou pendente; não invente resultados.
- Para capturas simples no Windows, prefira `python scripts/duelos.py` (ou `--battle-attributes`). O modo `--trace-state` atual limita a 3 duelos/1 worker; para lotes maiores use `--no-state`, sem fingir equivalência de cobertura.

## Restrições

Não versionar EXE/DAT/saves, dumps proprietários integrais ou traces brutos; use `scratch/` ignorado pelo Git. Não alterar o executável original nem encerrar PIDs não criados pelo laboratório. Continue a proteger a integridade de capturas (eventos perdidos, footer, hashes quando necessários). A segurança e procedência do laboratório permanecem mesmo com o desenvolvimento mais ágil.

Ao concluir, relatar de forma curta: comportamento entregue, testes executados, limitações reais, commit e próximo passo útil.
