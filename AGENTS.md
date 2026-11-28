# AGENTS.md — YugiRE

Reimplementar **Yu-Gi-Oh! Power of Chaos: Joey the Passion** em C# / .NET 10. `Joey.Core` = regras; `Joey.Cards` = definições e planos de efeitos; `Joey.App` (raylib-cs) + `Joey.LookAndFeel` = apresentação; `Joey.AI` = decisões CPU; `Joey.Lab` = análise offline; `yugilab/` + `native_hooks/` = captura do original.

Este arquivo contém instruções permanentes. Prioridades, estado e histórico ficam nos planos; políticas especializadas são carregadas sob demanda.

## Princípios

- Funcionalidade jogável e manutenção simples têm prioridade sobre paridade interna de assembly. Engenharia reversa é evidência, não obrigação de copiar arquitetura.
- Implementar verticalmente: regra → fila única → integração jogável → manifesto.
- Core é dono de estado, comandos, scheduler e execução, independente de relógios de apresentação, Frida, Windows e Python. Cards fornece definições/planos sem mutação direta. AI escolhe pela API legal comum, com estado somente leitura.
- Jogador, CPU, efeitos, console e scripts usam a mesma API de comandos tipados. Perfis de apresentação mudam duração, nunca resultado nem sequência lógica.
- CARDS: efeitos claros pelo Card.Description do DAT local e regras da época, agrupados por mecânica (records/tabelas e primitivos reutilizáveis, sem if por ID); sem callback, teste individual, documento por carta ou captura nativa obrigatórios. Se houver ambiguidade material, usar hipótese conservadora quando possível, registrar a limitação e investigar divergências concretas; sem DSL desnecessária. ACTIONS/VISUALS mantêm rigor nativo.
- Distinguir implementado no sandbox, testado em C#, observado no Joey e inferido. Índice interno, resource ID e slot são distintos. Teste sintético não gera Verified.
- VISUALS: fidelidade em pixel e update observável a 800×600, assets originais, sem placeholders no modo fiel; debug isolado, desligado por padrão, F3 alterna. Investigar o assembly de `joey_pc.exe` antes de alterar apresentação (inclusive áudio, geometria, sprites, composição, timing e sequências). Não inventar valores nem chamar fallback provisório de nativo.

## Fluxo de trabalho

1. Ler [PLANS.md](PLANS.md) (Foco atual, Como despachar, Contrato da fila) e a trilha pertinente em `docs/plans/<TRILHA>.md`. Foco atual tem precedência; respeitar objetivo/ID solicitado, dependências e PRs/branches ativos.
2. Escolher o item executável de maior ganho provável por esforço; não pedir confirmação só para desempatar. Uma fatia vertical e um agente executor por vez.
3. Atualizar checkbox, Status e Próximo passo no PLANS; registrar relatos no bloco do ID da trilha. Novos achados viram IDs no fim; nunca renumerar. Atualizar manifestos e regenerar badges afetadas.
4. Preferir commits pequenos e validados na main (pull --ff-only); PR por marco quando risco ou concorrência justificar. Um escritor por vez; sem force-push. Operações ordinárias de Git, testes e integração validados são autorizadas, sem confirmação adicional; respeitar permissões e proteções.
5. Em continuidade genérica, retomar Goal compatível ou definir/criar Goal conforme capacidade do ambiente. Em missão autônoma, continuar após checkpoints, commits e PRs até concluir o objetivo ou atingir limite real; registrar bloqueio e seguir tarefa independente quando possível. Sem delegação por padrão. Não criar Goal para consulta ou mudança pontual.

## Leitura sob demanda

Carregar apenas os documentos pertinentes, antes de decidir ou implementar a tarefa; não ler todos preventivamente. Consultar as referências técnicas relevantes a partir deles.

| Tarefa | Documento |
| --- | --- |
| Contratos do motor, planejamento, testes históricos, badges | [Trilhas e processo](docs/agents/tracks-and-process.md) |
| Cartas, manifesto e classificação de cobertura | [Definição de pronto](docs/agents/definition-of-done.md) |
| Apresentação, áudio, animação, Update/Draw | [Fidelidade visual](docs/agents/visual-fidelity.md) |
| Investigação nativa, SQLite, Ghidra, scripts | [Evidência nativa e roteiros](docs/agents/native-evidence-and-scripts.md) |
| Goal, execução autônoma, Git e integração | [Autonomia e Git](docs/agents/autonomy-and-git.md) |

## Validação e restrições

- Validação principal: [duelos diferenciais](docs/architecture/duel-differential-policy.md), build/CI, infraestrutura, scheduler e visual. Não executar testes históricos de cartas por obrigação nem criar novos testes individuais por carta.
- Consultar [guia do SQLite](docs/research/joey-knowledge-sqlite.md) e evidência existente antes de pedir coleta; usar `scripts/pending/` para dependências de outra máquina, sem rodá-las automaticamente no CI.
- Não alegar teste executado sem executá-lo. Declarar validações pendentes. Não versionar EXE, DAT, saves, SQLite, ZIPs privados, dumps integrais ou traces brutos; usar `scratch/`. Não alterar o executável original nem encerrar PIDs alheios; preservar integridade e proveniência.
- Consultar mantenedor para mudança destrutiva, decisão arquitetural fora dos contratos ou conflito sem resolução segura.

Ao concluir, relatar comportamento entregue, testes, limitações reais, commit e próximo passo útil.
