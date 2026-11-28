# Trilhas, fidelidade/esforço e motor de comandos

> Políticas de processo, contratos e validação. Carregue sob demanda.

## Trilhas de trabalho (ACTIONS, CARDS, VISUALS, AI)

O trabalho está organizado em quatro trilhas em [`PLANS.md`](../../PLANS.md), com itens numerados (`A##`, `C##`, `V##`, `I##`). **Preferir um único agente executor, com um `/goal` definido, trabalhando sequencialmente.** As trilhas organizam o backlog e a evidência, não representam agentes que precisam trabalhar simultaneamente. Quando o mantenedor indicar uma trilha ou ID, respeitar esse escopo e executar suas dependências mínimas nas demais trilhas. Quando o objetivo for genérico, aplicar o Foco atual e, na dúvida, escolher a frente com maior ganho funcional provável por esforço, considerando desbloqueios, evidência disponível, risco e custo de validação. Não perguntar ao mantenedor apenas para desempatar tarefas executáveis.

1. Leia "Foco atual", "Como despachar um agente" e "Contrato da fila de ações" de `PLANS.md`, depois a trilha e os detalhes em `docs/plans/<TRILHA>.md`. O foco atual tem precedência sobre a ordem genérica.
2. Escolha o ID citado ou o item `Pronto`/`Parcial` executável com maior ganho provável, respeitando a ordem obrigatória do foco e as dependências. Confira PRs/branches ativos para não repetir trabalho.
3. Entregue uma fatia vertical pequena, marque `[x]` ao concluir, atualize apenas Status e Próximo passo do item em `PLANS.md`. Relato da fatia (PR, CI, evidência, limites) no bloco correspondente de `docs/plans/<TRILHA>.md`, nunca em `PLANS.md`. Achados novos viram **novos IDs no fim da trilha**; nunca reutilize nem renumere IDs.
4. Relate de forma curta: comportamento entregue, testes executados, limitações reais, próximo ID sugerido.
5. Integrar marcos conforme [autonomia e Git](autonomy-and-git.md).

**Fidelidade e esforço:** CARDS implementa pelo DAT em Joey.Cards, sem testes, documentação individual ou callbacks obrigatórios. ACTIONS/VISUALS mantêm rigor nativo. Remoções arquiteturais de legado seguem o escopo e os critérios definidos no PLANS.md: não manter resolutores paralelos nem mutar em operações não suportadas; reclassificar a cobertura pelo suporte real. Core é dono de estado, comandos, scheduler e execução; Cards fornece definições/planos sem mutação direta. AI escolhe pela API legal comum e recebe estado somente leitura.

**Testes históricos de cartas:** consultar somente por divergência concreta; não ler, executar, atualizar ou manter compatibilidade por obrigação. Retirar do runner/CI e, por fatias, da compilação; preservar baseline. Não criar novos testes individuais. Preservar infraestrutura, scheduler e visual. Validação principal por duelos diferenciais: mesmas decisões, condições iniciais e RNG, ações normalizadas e checkpoints, primeira divergência e gaps. AI usa snapshots pré-decisão nativos; não exigir duelo inteiro para comparar escolha.

**Badges das quatro frentes:** ACTIONS mede marcos A numerados com `[x]` e status `Concluído` no PLANS.md; ao editar a trilha, executar `python scripts/publish_actions_progress.py`. CARDS mede Done+Verified no catálogo de 771 cartas e exibe Partial separadamente; VISUALS mede Draw implementadas inclusive Partial, não fidelidade. AI mede decisões equivalentes / comparáveis com corpus e lacunas; publicar quando houver comparador. Manter geradores existentes consistentes.

## Motor orientado a comandos e console (ACTIONS)

O console é um produtor da mesma API de comandos tipados usada por jogador, CPU, efeitos, scripts e testes. A origem determina autorização, não execução: uma vez admitido, um comando equivalente passa pela mesma fila, executor, gatilhos, eventos e gate visual. O console de laboratório pode solicitar operações privilegiadas explicitamente, sem transformar isso em jogadas legais nem modificar o estado diretamente. `state`/`diff` são leituras. Parser e runner reutilizáveis em headless, com UI opcional no Joey.App. Validar identidade/condições na execução; distinguir destruição, descarte e envio ao cemitério. Perfis de apresentação podem mudar duração, não o resultado ou a sequência lógica. Consultar [contrato do console](../../docs/architecture/duel-console.md) e o estado do App antes de anunciar atalhos ou cobertura.

## Validação proporcional

- C#: build da solution, CI ativo e cenários diferenciais disponíveis. Não executar suíte histórica de cartas por padrão.
- Backend alterado: `python -m unittest discover -s native_hooks/tests`, `python -m unittest discover -s yugilab/tests` e testes Node pertinentes.
- Não afirme que um comando passou sem executá-lo. Se faltarem .NET, DAT original ou Windows, explique qual validação ficou pendente; não invente resultados.
- Política diferencial: [contrato](../../docs/architecture/duel-differential-policy.md). Lacunas ficam inconclusivas; AI compara snapshots pré-decisão, não exige duelo inteiro.
