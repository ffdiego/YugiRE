# Orientações para agentes no YugiRE

Projeto: engenharia reversa de **Yu-Gi-Oh! Power of Chaos: Joey the Passion** e reimplementação de regras/IA em C#/.NET 10, com raylib-cs na interface. **Joey.Lab** é o laboratório .NET; `native_hooks/` contém o backend Frida/DLL; `yugilab/` é o laboratório Python legado, preservado durante a migração.

## Como escolher trabalho

- O plano operacional, a fila de tarefas, os proprietários e os pontos de parada estão em [PLANS.md](PLANS.md).
- Quando o usuário pedir para continuar **a trilha nativa** ou **a trilha laboratório**, escolha a primeira tarefa desbloqueada da sua trilha. Não assuma controle da outra.
- Em trabalhos pontuais explicitamente solicitados, cumpra o pedido, sem iniciar o roadmap automaticamente.
- Releia apenas os documentos relevantes à tarefa. Não reanalise todo o repositório a cada turno.
- **Sol / investigação nativa**: `docs/research/` (contratos e evidências), scripts Ghidra e, quando necessário, instrumentação passiva em `native_hooks/`. **Terra / implementação do laboratório**: `src/Joey.Lab/`, testes/fixtures correspondentes e código C# apoiado em contratos já estabelecidos. Um agente não altera arquivos sob responsabilidade do outro sem coordenação.
- `PLANS.md` e este arquivo pertencem ao coordenador/usuário: os dois agentes propõem ajustes via handoffs, sem editar ambos os arquivos enquanto trabalham em paralelo.

## Princípios de engenharia

- Diferencie **assembly/decompilação**, **captura nativa observada**, **inferência**, **contexto declarado** e **teste sintético**. Nunca afirme equivalência dinâmica com base somente em oráculos gerados do mesmo pseudocódigo.
- Em observações, desconhecido permanece `null`/`Unsupported`, nunca vira 0, false, ataque ou ausência de efeitos. Preserve índice interno, ID de recurso e slot como campos distintos.
- Valide o SHA256 do executável quando o artefato local estiver disponível: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Ausência de hash no JSONL não é atestado implícito.
- Não execute Joey no Fedora, não altere EXE/DAT/saves/replays nem grave alterações no projeto Ghidra persistente. Consultas Ghidra com `-readOnly`/`-noanalysis` quando aplicável. Não invoque helpers nativos para obter respostas sem investigação de efeitos colaterais.
- Não versionar dados proprietários extraídos em massa, descrições integrais de cartas, saves ou traces brutos sensíveis; prefira metadados, hashes, resumos, fixtures mínimos e proveniência.
- Mantenha `Joey.Core` independente de Frida/Windows/Python; não use o modelo da engine para representar estado parcialmente observado sem um adaptador que valide a completude.
- Não remova `yugilab/`, `native_hooks/` ou código legado até passar os critérios de equivalência do plano e obter aprovação explícita. Não instale dependências grandes sem necessidade comprovada.

## Concorrência, validação e entrega

- Para trabalhos paralelos com escrita, use **worktrees e branches diferentes**. Nunca dois agentes escrevendo no mesmo checkout. Não faça force-push ou push automático em `main`.
- Commits pequenos com testes e evidências; prefira enviar a branch própria para revisão quando autorizado. Não integre branches concorrentes sem verificar conflitos e rodar regressões.
- Testes típicos: `dotnet build YugiRE.sln --no-restore`; `dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc`; `python3 -m unittest discover -s native_hooks/tests`; `python3 -m unittest discover -s yugilab/tests`; e os testes JS relevantes em `native_hooks/tests/`. Explique dependências locais ausentes em vez de afirmar PASS.
- Ao concluir uma unidade de trabalho, registre commit, testes realmente executados, evidência, incerteza e próximo passo em um handoff próprio da trilha, conforme `PLANS.md`.
- **Pare com justificativa** quando precisar de uma captura Windows, decisão do usuário, novo contrato da outra trilha, permissão, artefato ausente ou quando não houver tarefa segura e útil desbloqueada. Não invente resultados para continuar consumindo franquia. Não prometa execução automática depois que o turno terminar.
