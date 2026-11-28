# Autonomia operacional, Goal e Git

> Permissões, execução contínua, Goal e integração. Carregue sob demanda.

## Autonomia operacional e execução contínua

O mantenedor autoriza explicitamente os agentes a realizar as operações ordinárias abaixo, **sem solicitar confirmação adicional na conversa**:

- Criar e reutilizar branches/worktrees isoladas; selecionar tarefas e implementar/refatorar dentro do escopo e dos contratos do projeto.
- Executar builds, testes e investigações autorizadas, atualizar documentação, manifests e badges, criar commits e fazer push de branches.
- Abrir, revisar e atualizar PRs; reconciliar conflitos preservando o trabalho concorrente; integrar marcos com os critérios de aceitação satisfeitos e CI obrigatório aprovado.
- Quando houver um coordenador, somente ele (ou o integrador explicitamente designado) integra na main; os demais agentes trabalham em branches/worktrees próprias. Para um único escritor, permanece válido o fluxo preferencial da seção Git.

Em uma sessão com `/goal` ou missão autônoma de múltiplos marcos, manter **um agente principal e uma fatia ativa por vez**, sem criar agentes coordenadores ou delegar por padrão. A conclusão de uma fatia, commit, PR ou relatório intermediário não encerra a missão: selecionar e executar a próxima tarefa de maior ganho provável dentro do objetivo, mantendo checkpoints. Se faltar evidência, permissão ou dependência em uma frente, registrar a lacuna e avançar em outra tarefa útil compatível com o objetivo; se o objetivo for geral, escolher a frente de maior retorno provável. Não alternar frentes apenas para manter todas ocupadas. Encerrar ao atingir o objetivo, esgotar as alternativas produtivas ou alcançar limites reais de execução. Paralelismo é exceção: só quando houver benefício claro sobre o custo de coordenação, preferencialmente em investigações independentes somente leitura ou tarefas isoladas sem contratos/arquivos compartilhados.

A autorização de Git não substitui o sandbox, as permissões do ambiente nem as proteções da branch. Respeitar bloqueios e não contorná-los; não fazer force push, descartar trabalho alheio, publicar artefatos privados ou executar mudanças destrutivas fora do escopo. Consultar o mantenedor para operações destrutivas, decisões arquiteturais relevantes fora dos contratos acordados ou conflitos sem resolução segura.

### Goal automático

Quando o mantenedor solicitar a continuidade do projeto sem especificar uma tarefa pontual (por exemplo, `continue o trabalho` ou `continue o trabalho em CARDS`):

1. Se já houver um Goal ativo compatível com a solicitação, continuar sua execução; não reiniciar nem criar Goals duplicados. Respeitar qualquer mudança explícita de objetivo pelo mantenedor.
2. Se não houver Goal ativo, selecionar autonomamente um objetivo a partir de `PLANS.md`, respeitando o Foco atual, a trilha/escopo solicitado, os gates e as dependências. Na dúvida, escolher a frente com maior ganho funcional provável por esforço e franquia.
3. Criar o Goal pelo mecanismo nativo do Codex, quando disponível, com escopo claro e critérios mensuráveis de conclusão e validação. Se o ambiente não oferecer essa capacidade, explicitar a limitação e trabalhar autonomamente com o objetivo definido na sessão, sem afirmar que um Goal nativo foi criado.
4. Executar sequencialmente como agente único, sem delegação por padrão, seguindo a política de execução contínua acima. Não encerrar a missão a cada checkpoint.
5. Não pedir confirmação apenas para escolher o próximo objetivo. Não criar Goals automaticamente para perguntas, consultas ou alterações pontuais; respeitar pedidos explícitos de pausa ou encerramento.

## Git e trabalho entre máquinas

**Fluxo preferencial para um agente escritor:** atualizar `main` com `git pull --ff-only`, implementar uma unidade pequena, executar testes, commitar e fazer push para `origin/main`. Assim, outra máquina pode continuar com um pull simples. Não fazer force-push nem publicar código sabidamente quebrado.

**Concorrência excepcional:** preferir um agente único; não iniciar outras sessões por trilha por padrão. Apenas **um escritor por vez na `main`**. Quando o paralelismo for justificado, usar branch/worktree por trilha e ID e sinalizar o item em PR draft ou por coordenação explícita. ACTIONS tem precedência nos contratos compartilhados e mudanças estruturais em `DemoDuel`; CARDS e VISUALS avançam fatias independentes. Coordenar alterações nos contratos compartilhados: adaptadores temporários explícitos, sem duplicar mutação. Agentes em paralelo podem pesquisar ou trabalhar em branches/worktrees isoladas; integrar sequencialmente após revisar conflitos. Não permitir que duas sessões independentes façam push concorrente para a mesma `main` sem coordenação. PRs continuam úteis para mudanças arriscadas, grandes ou não validadas.

**Integração por marco:** ao satisfazer os critérios de aceitação, com CI obrigatório verde e documentação/badges atualizadas, conferir main e PRs ativos, resolver conflitos preservando trabalho alheio e integrar autonomamente, respeitando dependências. Iniciar o próximo marco sobre a main atualizada. Se outro agente integrar antes do fechamento documental, confirmar o merge e finalizar a consolidação sem repetir o merge.
