# Handoff S1 — contrato de observação F40

Data: 2026-11-11. Branch `work/native-analysis`.
Commit da entrega: `eeb75ac` (`docs(native): define F40 observation contract and pilot limits`).

## Objetivo e entrega

Definir como observar entradas efetivas de `FUN_005B0F40`, distinguindo memória
transitória, contexto hipotético, retorno nativo e inferência.
Contrato: [ai-combat-observation-contract.md](../ai-combat-observation-contract.md).
Nenhum arquivo da trilha T, AGENTS.md ou PLANS.md foi alterado.

## Fatos e evidência

- SHA256 local do EXE conferido:
  `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- ABI F40 compatível com cdecl: quatro argumentos stack; `RET` em 005B1308;
  caller limpa 0x10 em 005B0E20. Evidência: assembly versionado e objdump local.
- A postura do alvo é lida em 005B0F74 antes dos wrappers. ADD60 pode modificar
  temporariamente bit 2; ADDC0 pode produzir stats estimados sem chamar DD30.
- Capturar DD30 isoladamente não fornece todas as entradas de F40.
- Contagens/predicados precisam de caller, thread e combatCallId; chamadas dentro
  dos produtores de stats não são as chamadas diretas da fórmula F40.
- O REA estava conectado, sem alvo aberto, mas Ghidra informou `not_configured`.
  O projeto Ghidra não existe neste worktree; foram reutilizadas evidências
  versionadas e objdump somente leitura. Não houve gravação em projeto persistente.

## Verificação efetivamente realizada

`sha256sum ../passion/joey_pc.exe`, consultas `objdump -d -Mintel` listadas no
contrato, leitura dos relatórios primários e `git diff --check` passaram.
O teste estático adicionado em S2 (`824b444`) valida posteriormente os quinze
CALLs diretos e o prologue/epilogue de F40 contra o PE original.
Não houve teste Windows, execução do jogo, build .NET ou alteração de dados.
A primeira tentativa de commit encontrou índice Git fora da raiz gravável;
a autorização para registrar o commit foi concedida e a gravação terminou.

## Lacunas, hipótese e próximo passo

A equivalência entre snapshot de entrada e instante efetivo de uso de flags
é hipótese a validar no piloto; reentrância, atomicidade e overhead continuam
sem observação dinâmica. Desconhecidos ficam null; zero contextos completos
novos observados. O contrato permite preparar S2 offline, entregue em `824b444`.
A próxima evidência necessária é o piloto Windows descrito no
[handoff S2](native-combat-context-pilot.md), após revisão. Não há autorização
implícita de ativação por este documento.
