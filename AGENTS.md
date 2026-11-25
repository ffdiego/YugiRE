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

## Trilhas de trabalho (ACTIONS, CARDS, VISUALS)

O trabalho está organizado em três trilhas em [`PLANS.md`](PLANS.md), com itens numerados (`A##`, `C##`, `V##`). Quando o mantenedor disser **"continue o trabalho em CARDS"** (ou VISUALS, ACTIONS), ou **"faça V07"**:

1. Leia a seção "Trilhas de trabalho" e "Espinha dorsal: a fila de ações" do `PLANS.md`, depois a trilha pedida.
2. Pegue o primeiro item aberto com Status `Pronto` (ou o ID citado); `Parcial` pode ser retomado se a próxima fatia for executável. Respeite dependências e confira PRs/branches ativos antes de reservar o ID.
3. Entregue uma fatia vertical pequena, marque `[x]`, atualize o Status e registre evidência e limitações. Achados novos viram **novos IDs no fim da trilha**; nunca reutilize nem renumere IDs.
4. Relate de forma curta: comportamento entregue, testes executados, limitações reais, próximo ID sugerido.
5. **Integração por marco:** mantenha as fatias intermediárias (ex. A02.1–A02.5) no PR do marco. Ao satisfazer seus critérios de aceitação, com CI obrigatório verde e documentação/badge atualizadas, confira a main e PRs paralelos, resolva conflitos sem sobrescrever trabalho alheio e faça merge autonomamente, respeitando dependências. Não aguarde autorização a cada merge; consulte o mantenedor apenas para mudanças destrutivas, decisões arquiteturais relevantes ou conflitos sem resolução segura. Inicie o próximo marco sobre main atualizada. Se um PR for integrado externamente antes do fechamento documental, confirme o merge e finalize o marco em PR de consolidação; nunca repita o merge.

**Fidelidade e esforço:** regras triviais de cartas se implementam rápido a partir do texto do DAT, sem validação nativa exaustiva. O rigor fica em VISUALS (assembly-first) e na fila de ações: toda mudança de estado observável deve ser um comando tipado executado um de cada vez, e **nenhuma regra nova deve resolver ações fora da fila** depois que `A02` existir. Enquanto `A02` não existir, registre cada carta implementada fora da fila em `C12` para migração posterior.

**Badges das três frentes:** ACTIONS mede marcos A numerados com `[x]` e status `Concluído` no PLANS.md; ao editar a trilha, executar `python scripts/publish_actions_progress.py`. CARDS mede Done+Verified no catálogo de 771 cartas e exibe Partial separadamente; VISUALS mede Draw implementadas inclusive Partial, não fidelidade. Manter os três geradores e seus testes consistentes com as fontes.

## Autonomia operacional e execução contínua

O mantenedor autoriza explicitamente os agentes a realizar as operações ordinárias abaixo, **sem solicitar confirmação adicional na conversa**:

- Criar e reutilizar branches/worktrees isoladas; selecionar tarefas e implementar/refatorar dentro do escopo e dos contratos do projeto.
- Executar builds, testes e investigações autorizadas, atualizar documentação, manifests e badges, criar commits e fazer push de branches.
- Abrir, revisar e atualizar PRs; reconciliar conflitos preservando o trabalho concorrente; integrar marcos com os critérios de aceitação satisfeitos e CI obrigatório aprovado.
- Quando houver um coordenador, somente ele (ou o integrador explicitamente designado) integra na main; os demais agentes trabalham em branches/worktrees próprias. Para um único escritor, permanece válido o fluxo preferencial da seção Git.

Em uma sessão com Goal ou missão autônoma de múltiplos marcos, manter ACTIONS, CARDS e VISUALS produtivas sempre que houver trabalho independente. A conclusão de uma fatia, commit, PR ou relatório intermediário não encerra a missão: selecionar ou delegar imediatamente a próxima tarefa executável, manter checkpoints e atualizar a base de integração. Se faltar evidência, permissão ou dependência em uma frente, registrar a lacuna e avançar em outra tarefa; não ficar ocioso esperando autorização já concedida. Encerrar ao atingir o objetivo, esgotar as alternativas produtivas ou alcançar limites reais de execução.

A autorização de Git não substitui o sandbox, as permissões do ambiente nem as proteções da branch. Respeitar bloqueios e não contorná-los; não fazer force push, descartar trabalho alheio, publicar artefatos privados ou executar mudanças destrutivas fora do escopo. Consultar o mantenedor para operações destrutivas, decisões arquiteturais relevantes fora dos contratos acordados ou conflitos sem resolução segura.

## Motor orientado a comandos e console (ACTIONS)

O console (A04) é um produtor da mesma API de comandos tipados usada por jogador, CPU, efeitos, scripts e testes. A origem determina autorização, não execução: uma vez admitido, um comando equivalente passa pela mesma fila, executor, gatilhos, eventos e gate visual. O console de laboratório pode solicitar operações privilegiadas explicitamente, sem transformar isso em jogadas legais nem modificar o estado diretamente. `state`/`diff` são leituras. Parser e runner reutilizáveis em headless, com UI opcional no Joey.App. Validar identidade/condições na execução; distinguir destruição, descarte e envio ao cemitério. Perfis de apresentação podem mudar duração, não o resultado ou a sequência lógica. A04 ainda é plano, não funcionalidade entregue.

## Meta permanente de fidelidade visual

**O alvo do `Joey.App`/`Joey.LookAndFeel` é reproduzir a apresentação do Joey original em nível de pixel e update observável; não reproduzir o código que a implementa.** É aceitável usar raylib, uma timeline de eventos e algoritmos C# diferentes se o resultado for equivalente. A liberdade arquitetural do Core **não** autoriza inventar coordenadas, tamanhos, pivôs, ângulos, ordem de desenho, duração ou sequência de animações.

- Tratar **todo elemento visual** como parte do objetivo: cartas próprias/adversárias, posições de ataque/defesa, cartas face-up/face-down, atributos ATK/DEF, mão, zonas, sprites, textos/HUD, LP, cursor/espada, avisos de fase, prompts, transições e efeitos. Priorizar os elementos jogáveis na alpha, sem reduzir a meta final.
- **Assets originais são obrigatórios no modo de fidelidade:** carregar sprites, fundos, miniaturas, versos, ícones e quadros/variantes de animação diretamente do `data.dat` local via leitor do projeto. É permitido decodificar/cachear texturas em memória/GPU por eficiência; não exigir I/O no DAT a cada frame. Não usar placeholders, desenhos geométricos substitutos ou sprites inventados no fluxo visual padrão quando o recurso nativo existir. Temas alternativos e fallback de diagnóstico devem ser explícitos e separados do modo fiel.
- Preservar **todas as variantes e trocas de sprite por estado/quadros**, inclusive efeitos sutis e breves. Exemplo observado: durante o ataque, a espada completa parece ser substituída por uma variante de lâmina parcialmente oculta ao atingir a carta; investigar `j\\duel\\effect\\atk_icon.bmp` e `atk_icon2.bmp` e o seletor Update/Draw no Ghidra, sem assumir que o nome dos arquivos comprova a transição.
- **Debug nunca é parte do visual original:** isolar overlays de diagnóstico (FPS, contadores, estados, IDs, coordenadas, logs, botões provisórios) da UI de jogo. Durante o desenvolvimento podem se sobrepor ao canvas, mas devem ter controle explícito **"Ocultar debug"** acessível por clique quando ativos, atalho **F3** para alternar e opção de reativar no menu/configuração. No modo fiel, o debug começa **desligado** e a captura de comparação deve sair limpa. Ocultar debug não esconde informações ou controles **essenciais** do jogo nem altera estado, input, layout ou temporização do duelo. Ver [política de overlays](docs/architecture/visual-fidelity-goal.md#overlays-de-diagnóstico-e-modo-visual-limpo).
- Não usar a classificação "debug" para dispensar a investigação do UI real: se um botão ou texto da alpha é necessário para jogar, deve receber posicionamento/aparência nativos ou uma solução transitória explicitamente identificada e não pode encobrir regiões de jogo no modo fiel. Inspecionar alinhamento, sobreposição, clipping e z-order a 800×600.
- A badge de cobertura visual usa `docs/architecture/visual-function-coverage.json` e rotinas `Draw` únicas do inventário. `Partial` conta como implementação de apresentação, mas **não** como fidelidade validada. Atualize status com evidências no C# e execute `python scripts/publish_visual_coverage.py` e os testes ao mudar o inventário.
- Para cada elemento, obter **posição e tamanho em pixels lógicos**, origem/pivô, orientação, escala, camada e recursos do DAT; para animações, obter contadores, curva/frames, ordem, gates/esperas e cadência. A tela-alvo de referência é **800×600**; separar coordenadas lógicas de escala da janela.
- Priorizar prova por Ghidra + assets DAT + comparação do original em estado/frame equivalente. Registrar se cada valor é **estático confirmado**, **observado no original**, **derivado** ou **provisório**. Não chamar de nativo algo apenas copiado do vídeo ou escolhido no cliente.
- Quando o executável fornecer a fórmula/tabela, portá-la ou reproduzir **seus resultados**; quando não, usar fallback explícito com pendência localizada. Nunca fazer números mágicos de UI passarem por valores verificados. Não exigir matching binário/bit a bit no rasterizador se os pixels observáveis coincidirem.
- Para aceitar uma fatia visual, exigir ao menos casos dos dois lados, ataque/defesa, revelado/oculto quando cabível, e comparação de coordenadas/dimensões por teste; para efeitos, comparar quadros-chave e número de updates. Dados nativos e decisões provisórias devem ficar separados no código e nas notas.
- Este contrato deve ser seguido mesmo durante ciclos rápidos de alpha. Detalhes e critérios: [meta de fidelidade visual](docs/architecture/visual-fidelity-goal.md). A apresentação não deve alterar nem bloquear permanentemente as regras do `Joey.Core`.

## Política obrigatória: engenharia reversa primeiro para apresentação

**Solicitação do mantenedor (22/11/2026): para qualquer mudança de sprite, posição, tamanho, cor, fonte, orientação, pivô, alpha, textura, z-order, duração, frame, transição ou sequência do `Joey.App` / `Joey.LookAndFeel`, investigar primeiro a implementação correspondente em `joey_pc.exe`, preferencialmente no assembly/disassembly do Ghidra.** Esta regra não exige matching binário nem obriga a copiar arquitetura nativa. Ela determina a origem dos valores e das condições de desenho; `Joey.Core` continua independente da apresentação. A flexibilidade para inferir **regras simples de cartas** a partir do DAT não se estende automaticamente à aparência do cliente.

Fluxo obrigatório para tarefas visuais:

1. **Registrar a observação, não inventar uma correção.** O usuário relata uma diferença visível (incluindo capturas, quando disponíveis); o agente é responsável por localizar a origem, investigar e propor o ajuste. Não transferir ao usuário a obrigação de identificar VAs, filtros, coordenadas ou o código responsável.
2. **Rastrear o produtor e o consumidor nativos.** Partir do recurso no `data.dat`, referência ao manifesto, construtor/objeto/vtable, chamadores e operações Update/Draw. Examinar disassembly e decompilação, incluindo a origem dos argumentos; identificar flags/gates, lado/jogador, modo de carta, ordem de camadas e a condição que escolhe cada variante. **A decompilação sozinha pode estar errada:** quando há casts estranhos, funções temporárias, registradores ambíguos ou chamadas virtuais, usar o assembly e os Xrefs para confirmar.
3. **Distinguir dado de hipótese.** Registrar RVA/VA, fonte, expressão recuperada, hipótese e grau de confiança: `assembly confirmado`, `DAT confirmado`, `captura nativa observada`, `inferência` ou `parâmetro provisório do cliente`. Um nome sugestivo de sprite, um screenshot, uma posição copiada da tela ou uma constante já presente no C# **não comprovam** a regra original.
4. **Só então implementar o contrato visual em C#.** Separar evidência nativa de código de adaptação e de fallback; preferir modelos/testes do `Joey.LookAndFeel` a números mágicos no `Joey.App`. Testar atributos (dois lados, posições de ataque/defesa, revelado/oculto, atividade do turno) e timeline (início, impacto, duração, pausa, fim) conforme o elemento.
5. **Validar contra o original.** Executar testes C# e capturas pareadas nos mesmos estados/updates quando possível. O teste sintético aprova a implementação, mas **não** eleva a classificação para `Verified` sem comparação nativa. Se o assembly, os assets ou a execução necessária não estiverem acessíveis, documentar exatamente a lacuna em `PLANS.md` / pesquisa e preservar o comportamento existente ou um fallback **explicitamente provisório**; não afirmar paridade nem ajustar pixels por tentativa e erro como solução definitiva.

Ferramentas existentes: SQLite nativo enriquecido, `scripts/run-archive-query.sh`, `scripts/export_ghidra_visual_bundle.py`, `QueryArchive.java`, `docs/architecture/native-renderer-inventory.md` e `docs/architecture/native-visual-contract.md`. Antes de solicitar novas exportações, procurar se as funções/assembly já foram extraídos e estão registrados. Para novos resultados, documentar endereços e proveniência sem versionar o EXE, o DAT nem dumps proprietários integrais.

**Exemplos pendentes:** números de LP flutuantes são posicionados pelo contexto/argumentos do Draw (dano de batalha não é idêntico a dano direto); anúncio de troca de jogador usa recursos próprios; atributos ATK/DEF têm visibilidade e cores dependentes do estado; cursor de ataque e cursores de posição só aparecem para ações permitidas. Ver [fila de investigação visual](docs/research/visual-assembly-first-backlog-2026-11-22.md) e `PLANS.md`.

## Fonte privada compartilhada de engenharia reversa

**ACTIONS, CARDS e VISUALS devem consultar primeiro o snapshot cumulativo
`joey-knowledge-visual-cumulative-2026-11-24-v2.zip` (24/11/2026), que
contém `joey-knowledge-visual-cumulative.sqlite` e a auditoria.**
Em conversas ChatGPT, procurar o arquivo nas fontes do projeto YugiRE
ou na Biblioteca privada do mantenedor pelo nome exato; em máquinas locais,
usar a cópia em `scratch/`. Não pedir reenvio sem tentar localizar a fonte.
Validar o hash do SQLite e o fingerprint do EXE conforme o
[guia de consultas](docs/research/joey-knowledge-sqlite.md).
O ZIP sem o sufixo `2026-11-24-v2` contém apenas 19 dos 21 bundles,
apesar da auditoria antiga; não usá-lo como snapshot completo.
Antes de considerar uma VA ausente, consultar `decompilations`,
`visual_transient_decompilations`, `visual_evidence_functions`,
`visual_evidence_instructions` e, quando pertinente,
`visual_evidence_memory` e relatórios brutos. Não repetir coletas
já presentes, inclusive quando o endereço não existe em `functions`.
Ao publicar uma nova versão privada, atualizar o guia com nome, hash,
auditoria, contagens e lacunas; manter as versões anteriores somente como histórico.
Nunca versionar o SQLite ou os ZIPs no GitHub.

## Extrações nativas que dependem de outra máquina

A pasta [`scripts/pending/`](scripts/pending/README.md) contém **tarefas de extração ainda não encerradas**, para Ghidra/Windows e outras evidências indisponíveis neste ambiente; resultados já validados devem sair da fila conforme o ciclo de vida abaixo. Ler seu `README.md` antes de executar, atualizar o estado após obter um ZIP e nunca marcar um item como concluído só porque o script foi escrito. Não executar automaticamente as tarefas pendentes durante builds, CI ou testes. Esta política vale para **ACTIONS, CARDS e VISUALS**. Se qualquer frente depender do Windows (Frida/executável/DAT) ou Fedora (Ghidra/projeto local) indisponível, adicionar um script direcionado, indicar ID da trilha, ambiente, pré-requisitos, evidência esperada e saída em `scratch/`, registrar no README e avançar em uma tarefa independente.

**Ciclo de vida obrigatório dos roteiros (todas as trilhas):**

- `scripts/pending/`: fila ativa de coletas ainda não executadas, incompletas, falhas ou aguardando validação/importação. Manter o README como lista acionável, com estado, próxima ação e ambiente. Uma execução com falha não encerra a pendência: registrar a causa e não pedir nova execução idêntica sem necessidade.
- `scripts/completed/`: após receber e validar o resultado, registrar as conclusões e incorporar a evidência ao SQLite cumulativo quando compatível (ou referenciar o artefato privado quando não for), mover o roteiro de pending para completed no mesmo marco de consolidação. Manter `scripts/completed/README.md` com ID, nome, data, resultado, referência à evidência e snapshot/importação, quando aplicável. Não confundir coleta concluída com implementação ou comportamento Verified.
- `scripts/regression/`: mover testes/smokes reutilizáveis que já cumpriram o objetivo de coleta e devem continuar disponíveis para regressões opcionais. Não misturá-los com a fila de coletas inéditas.
- Preservar nomes, numeração e histórico; não excluir roteiros reprodutíveis. Atualizar referências ativas, comandos e READMEs, revisar caminhos relativos e testes afetados. Se uma nova lacuna exigir coleta, criar roteiro direcionado com novo número; não recolocar uma coleta concluída na fila sem justificativa.
- Antes de solicitar execução ao mantenedor, reconciliar a fila com os bundles recebidos, o SQLite mais recente e PRs ativos. Não repetir coletas já disponíveis. Em trabalho paralelo, o coordenador serializa a reorganização de caminhos para evitar conflitos.

**SQLite primeiro, enriquecimento preferencial:** antes de pedir outra coleta, consultar a versão mais recente do banco, bundles e docs existentes (efeitos triviais de cartas continuam dispensando RE exaustiva). Quando possível, importar novas evidências estáticas em uma cópia cumulativa do SQLite compartilhado, validando fingerprint, origem, integridade e preservação dos dados anteriores; usar/estender importadores testados conforme o formato. Dados dinâmicos/frames que não se ajustem ao banco ficam em artefatos privados com referências. Uma sessão escritora integra o snapshot, demais agentes consultam somente leitura. Registrar a versão e auditoria; não versionar o banco nem dumps. Ver [guia SQLite](docs/research/joey-knowledge-sqlite.md).

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
- A cada carta nova (inclusive procedimento opt-in incompleto, como Partial), atualizar `CARDS.md` e `docs/cards/playability-manifest.json` com resource ID exato, família, status e evidência de teste. A badge CARDS e o relatório `docs/cards/PLAYABILITY.md` são gerados por `python scripts/publish_playability.py`. O workflow `playability-badges.yml` recalcula as três badges em pushes na `main`. Regenerar os arquivos afetados antes do commit para o CI; nunca editar manualmente os percentuais.
- Os 264 recursos classificados como `Normal` são `Done` apenas para a mecânica básica de invocação/set/tributos/batalha testada em C#. Isso **não** torna efeito, fusão ou ritual completos nem confere `Verified`. Se o DAT real divergir da classificação de referência, revisar o inventário antes de confiar na badge. Não declarar Done por catálogo/callback ou invocação genérica de monstros.
- Para medir porcentagem: `python scripts/card_playability.py --dat ../passion/data.dat`. Denominador é a união de packs (771 registros) ou Joey (350), não os 1.114 registros nem o número de callbacks. Sem DAT local, não inventar percentuais.
- Efeitos FLIP no sandbox passam por `FlipEffectRules` e pelo gatilho em `DemoDuel` (Flip Summon/revelação no combate/efeito). Dano e marcação de destruição de combate precedem FLIP; cards marcados não podem voltar à mão com Penguin Soldier (601), ainda que permaneçam na zona até o fim da etapa. Pending targets bloqueiam ações; `FinishFlipTargets` fecha 0–1 alvos opcionais. Hane-Hane (540) exige exatamente um alvo legal e compartilha apenas a infraestrutura de bounce com Penguin Soldier (601), que é opcional com 0–2 alvos. Morphing Jar (590) descarta mãos e compra cinco; Morphing Jar #2 (1163) é um FLIP **distinto**, que embaralha monstros para Main Decks e escava até quotas por dono. O código do sandbox está em `DemoDuel.ResolveMorphingJar2`; não inferir Extra Deck, tokens nem posse fora do controlador. Já existe seletor genérico `CardSelectionRules` com integração raylib para P018 (427/101), P006 (47/573) e P164 (1123/1108/1117); somente P018 é ativado por FLIP. O próximo recorte de custo de invocação especial está em C16; consultar o manifesto e a [consolidação dos lotes 6–14](docs/research/card-rules-consolidation-lotes-6-14.md) antes de reimplementar cartas.
- Para mudanças na apresentação, consulte `docs/research/native-action-scheduler.md` para a ordem de execução e `docs/architecture/activation-preview.md` para o estado do cliente. Registre quais contadores e interpolações vêm do executável e quais parâmetros vieram de decisões do cliente.
- Fórmulas e modelos visuais da branch `analise-visual` foram consolidados em `Joey.LookAndFeel` (`HandPresentation`, `NativeDuelLayout`, `NativeActivationTimeline`, `NativeExplosionTimeline`, `NativeVisualDispatch`); consultar `docs/architecture/native-visual-contract.md` antes de inferir FPS ou duração. A tabela x16.16 deve ser fornecida/validada, não substituída silenciosamente por `Math.Cos` como se houvesse paridade.
- `Joey.LookAndFeel` é independente de `Joey.Core`/raylib e contém políticas de duração, perfis e preferências de voz. Prefira eventos tipados para sequenciar a apresentação. As mensagens de texto servem como feedback. `--mute-joey` ainda não silencia áudio real (não há player).

## Git e trabalho entre máquinas

**Fluxo preferencial para um agente escritor:** atualizar `main` com `git pull --ff-only`, implementar uma unidade pequena, executar testes, commitar e fazer push para `origin/main`. Assim, outra máquina pode continuar com um pull simples. Não fazer force-push nem publicar código sabidamente quebrado.

**Concorrência:** apenas **um escritor por vez na `main`**. Use branch/worktree por trilha e ID; sinalize o item em PR draft ou por coordenação explícita. ACTIONS tem precedência nos contratos compartilhados e mudanças estruturais em `DemoDuel`; CARDS e VISUALS avançam fatias independentes. Coordene A06/A08/C12 para não migrar as mesmas regras duas vezes. Não trate A02 como migração automática de todo o legado: adaptadores temporários devem ser explícitos e nunca duplicar mutação. Agentes em paralelo podem pesquisar ou trabalhar em branches/worktrees isoladas; integrar sequencialmente após revisar conflitos. Não permitir que duas sessões independentes façam push concorrente para a mesma `main` sem coordenação. PRs continuam úteis para mudanças arriscadas, grandes ou não validadas.

## Validação proporcional

- Regras C#: `dotnet build YugiRE.sln` e `dotnet run --project src/tests/Joey.Tests`.
- Backend alterado: `python -m unittest discover -s native_hooks/tests`, `python -m unittest discover -s yugilab/tests` e testes Node pertinentes.
- Não afirme que um comando passou sem executá-lo. Se faltarem .NET, DAT original ou Windows, explique qual validação ficou pendente; não invente resultados.
- Para capturas simples no Windows, prefira `python scripts/duelos.py` (ou `--battle-attributes`). O modo `--trace-state` atual limita a 3 duelos/1 worker; para lotes maiores use `--no-state`, sem fingir equivalência de cobertura.

## Restrições

Não versionar EXE/DAT/saves, dumps proprietários integrais ou traces brutos; use `scratch/` ignorado pelo Git. Não alterar o executável original nem encerrar PIDs não criados pelo laboratório. Continue a proteger a integridade de capturas (eventos perdidos, footer, hashes quando necessários). A segurança e procedência do laboratório permanecem mesmo com o desenvolvimento mais ágil.

Ao concluir, relatar de forma curta: comportamento entregue, testes executados, limitações reais, commit e próximo passo útil.
