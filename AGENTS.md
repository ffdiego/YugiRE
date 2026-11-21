# AGENTS.md — orientações para agentes do YugiRE

Projeto: reimplementar **Yu-Gi-Oh! Power of Chaos: Joey the Passion** em C# / .NET 10, com **Joey.Core** para regras e **Joey.App (raylib-cs)** para a experiência visual clássica.

## Prioridade de trabalho

**Funcionalidade jogável e manutenção simples têm prioridade sobre paridade interna de assembly.** Modele as regras do duelo no `Joey.Core` em C#. Use a engenharia reversa como evidência para decisões de domínio.

- Para o look and feel e o loop principal, é válido modelar opcodes, callbacks, buffers, máquinas de estado e trechos da arquitetura nativa quando a evidência explicar a ordem de apresentação, as esperas ou a cadência. Preserve o menor recorte que reproduza esse comportamento, com endereços ou observações e testes de sequência.
- Distinga o laço da aplicação, o avanço lógico do duelo e o agendamento de Update/Draw. O `Joey.App` e o `Joey.LookAndFeel` podem usar eventos, contadores e filas nativas como referência. O `Joey.Core` continua responsável pelas regras e independe dos relógios de apresentação.

- Implemente **verticalmente**: regra → integração em `DemoDuel`/motor → teste → UI se aplicável → atualização de `CARDS.md`.
- Para efeitos claros, use `Card.Description` do DAT **local**, regras da época de Joey, perfis de callback (`docs/evidence/card-callback-catalog.json`) e evidências já documentadas. Não exija captura Windows para começar uma implementação simples.
- Agrupe cartas por mecânica; prefira tabelas declarativas e primitivos reutilizáveis a um `if` espalhado por cada ID.
- Se houver ambiguidade material (timing, custos, alvo, cadeia, comportamento exclusivo de Joey), escolha uma implementação conservadora e testável, registre a hipótese no teste/`CARDS.md` e use o executável original como oráculo **sob demanda**.
- Não transforme uma hipótese em resultado verificado. Diferencie explicitamente *implementado no sandbox*, *testado em C#*, *observado no Joey* e *inferido*. Índice interno, resource ID e slot são distintos.

## Meta permanente de fidelidade visual

**O alvo do `Joey.App`/`Joey.LookAndFeel` é reproduzir a apresentação do Joey original em nível de pixel e update observável; não reproduzir o código que a implementa.** É aceitável usar raylib, uma timeline de eventos e algoritmos C# diferentes se o resultado for equivalente. A liberdade arquitetural do Core **não** autoriza inventar coordenadas, tamanhos, pivôs, ângulos, ordem de desenho, duração ou sequência de animações.

- Tratar **todo elemento visual** como parte do objetivo: cartas próprias/adversárias, posições de ataque/defesa, cartas face-up/face-down, atributos ATK/DEF, mão, zonas, sprites, textos/HUD, LP, cursor/espada, avisos de fase, prompts, transições e efeitos. Priorizar os elementos jogáveis na alpha, sem reduzir a meta final.
- **Assets originais são obrigatórios no modo de fidelidade:** carregar sprites, fundos, miniaturas, versos, ícones e quadros/variantes de animação diretamente do `data.dat` local via leitor do projeto. É permitido decodificar/cachear texturas em memória/GPU por eficiência; não exigir I/O no DAT a cada frame. Não usar placeholders, desenhos geométricos substitutos ou sprites inventados no fluxo visual padrão quando o recurso nativo existir. Temas alternativos e fallback de diagnóstico devem ser explícitos e separados do modo fiel.
- Preservar **todas as variantes e trocas de sprite por estado/quadros**, inclusive efeitos sutis e breves. Exemplo observado: durante o ataque, a espada completa parece ser substituída por uma variante de lâmina parcialmente oculta ao atingir a carta; investigar `j\\duel\\effect\\atk_icon.bmp` e `atk_icon2.bmp` e o seletor Update/Draw no Ghidra, sem assumir que o nome dos arquivos comprova a transição.
- **Debug nunca é parte do visual original:** isolar overlays de diagnóstico (FPS, contadores, estados, IDs, coordenadas, logs, botões provisórios) da UI de jogo. Durante o desenvolvimento podem se sobrepor ao canvas, mas devem ter controle explícito **"Ocultar debug"** acessível por clique quando ativos, atalho **F3** para alternar e opção de reativar no menu/configuração. No modo fiel, o debug começa **desligado** e a captura de comparação deve sair limpa. Ocultar debug não esconde informações ou controles **essenciais** do jogo nem altera estado, input, layout ou temporização do duelo. Ver [política de overlays](docs/architecture/visual-fidelity-goal.md#overlays-de-diagnóstico-e-modo-visual-limpo).
- Não usar a classificação "debug" para dispensar a investigação do UI real: se um botão ou texto da alpha é necessário para jogar, deve receber posicionamento/aparência nativos ou uma solução transitória explicitamente identificada e não pode encobrir regiões de jogo no modo fiel. Inspecionar alinhamento, sobreposição, clipping e z-order a 800×600.
- Para cada elemento, obter **posição e tamanho em pixels lógicos**, origem/pivô, orientação, escala, camada e recursos do DAT; para animações, obter contadores, curva/frames, ordem, gates/esperas e cadência. A tela-alvo de referência é **800×600**; separar coordenadas lógicas de escala da janela.
- Priorizar prova por Ghidra + assets DAT + comparação do original em estado/frame equivalente. Registrar se cada valor é **estático confirmado**, **observado no original**, **derivado** ou **provisório**. Não chamar de nativo algo apenas copiado do vídeo ou escolhido no cliente.
- Quando o executável fornecer a fórmula/tabela, portá-la ou reproduzir **seus resultados**; quando não, usar fallback explícito com pendência localizada. Nunca fazer números mágicos de UI passarem por valores verificados. Não exigir matching binário/bit a bit no rasterizador se os pixels observáveis coincidirem.
- Para aceitar uma fatia visual, exigir ao menos casos dos dois lados, ataque/defesa, revelado/oculto quando cabível, e comparação de coordenadas/dimensões por teste; para efeitos, comparar quadros-chave e número de updates. Dados nativos e decisões provisórias devem ficar separados no código e nas notas.
- Este contrato deve ser seguido mesmo durante ciclos rápidos de alpha. Detalhes e critérios: [meta de fidelidade visual](docs/architecture/visual-fidelity-goal.md). A apresentação não deve alterar nem bloquear permanentemente as regras do `Joey.Core`.

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
- Para mudanças na apresentação, consulte `docs/research/native-action-scheduler.md` para a ordem de execução e `docs/architecture/activation-preview.md` para o estado do cliente. Registre quais contadores e interpolações vêm do executável e quais parâmetros vieram de decisões do cliente.
- Fórmulas e modelos visuais da branch `analise-visual` foram consolidados em `Joey.LookAndFeel` (`HandPresentation`, `NativeDuelLayout`, `NativeActivationTimeline`, `NativeExplosionTimeline`, `NativeVisualDispatch`); consultar `docs/architecture/native-visual-contract.md` antes de inferir FPS ou duração. A tabela x16.16 deve ser fornecida/validada, não substituída silenciosamente por `Math.Cos` como se houvesse paridade.
- `Joey.LookAndFeel` é independente de `Joey.Core`/raylib e contém políticas de duração, perfis e preferências de voz. Prefira eventos tipados para sequenciar a apresentação. As mensagens de texto servem como feedback. `--mute-joey` ainda não silencia áudio real (não há player).

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
