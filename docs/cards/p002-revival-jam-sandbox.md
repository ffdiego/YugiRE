# P002 — Revival Jam: decisões opcionais e efeito diferido (sandbox)

**Status:** Implementação C# experimental. **Não é paridade nativa verificada.**

## Motivação

O controle `mass96-c11-no-gravity` mostrou custos de 1.000 LP associados ao
resource ID **1405** e transferências posteriores de **1060** do cemitério
para o campo; porém **não capturou a escolha na UI**. As regras históricas
e o texto moderno apontam uma opção do **dono** após destruição em batalha,
com custo pago no cemitério e invocação em defesa na próxima Standby.
Referências externas não substituem confirmação no Joey original.

**Atualização 20/11/2026:** uma captura **com jogador humano**
comprovou a existência da pergunta *Yes/No*, três episódios de pagamento
de exatamente 1.000 LP e três retornos na partida com respostas **Sim**,
além de uma partida com resposta **Não** seguida de ausência de retorno.
O operador também observou que o prompt não foi oferecido com 500 LP.
A leitura de RAM não registra cliques e não determina a fronteira em
1.000 LP exatos. A política CPU/demo e o timing exato seguem pendentes.
Ver [evidência humana P002](../research/p002-revival-jam-human-yes-no-2026-11-20.md).

- Evidência local: [C11 sem Gravity Bind](../research/mass96-c11-no-gravity-control-2026-11-20.md)
- Catálogo: `P002`, availability `0x00582990`, preparation `0x0057DC40`.
- Fonte histórica: https://ms.yugipedia.com/a/a1/2003_tournament_rulings.pdf
- Texto atual: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5063&ope=2&request_locale=en

## Contrato implementado

O Core mantém um `PendingEffectDecision` tipado por ID, jogador dono,
ocorrência da carta (`Card` por referência), custo e turno. A função
`ResolveEffectDecision(id, Accept|Decline)` é igual para raylib, headless
ou um futuro player controlado por IA. Resposta inválida não altera
LP/estado. Decisões simultâneas entram numa fila sem descartar o segundo
gatilho.

Ao aceitar, o Core cobra o custo **imediatamente** e agenda
`PendingDelayedRevival` para a próxima entrada no turno do dono.
O `DemoDuel` ainda não possui uma fase Standby pública; o scheduler
executa um **hook provisório de começo de turno**, antes da Draw.
Revalida a **mesma ocorrência** no GY e a existência de uma zona livre.
Se não encontrar a carta ou a vaga, o custo não é reembolsado.
Coloca-a em defesa face-up; não tenta simular o `positionWord=515`
nativo como formato já decodificado.

### Exibição de prompt versus resposta

`EffectPromptMode` não é uma regra da carta:
- **Ask:** a UI apresenta Sim/Não.
- **Off:** o programa recusa custos opcionais sem perguntar (não paga
  silenciosamente).
- **Automatic:** exige política injetável; sem política configurada,
  a resposta conservadora é Decline. Não é IA nativa.

No `Joey.App`, o botão `Prompts: ON/OFF` expõe Ask/Off. No
`SandboxDuelRunner`, decisões dessa categoria são explicitamente
respondidas com `DeclineOptionalCosts` e registradas em eventos
`sandbox_effect_decision`. Isso impede travas do modo headless;
não se deve interpretar os resultados como política nativa.

### Escopo e limitações

- Destruição **por batalha** é o único gatilho; outras remoções não
  devem solicitar custo.
- Não há modelagem completa de **owner diferente de controller**: o
  adapter `DemoDuel` atualmente atribui o GY ao lado do campo.
- LP estritamente maiores que 1000 são exigidos no sandbox;
  faltam a confirmação de limites e negações no Joey PC.
- O hook de Standby ainda não emite chains, prioridade, negadores ou
  ativação de efeitos na invocação; não substituir por status Verified.
- A política das outras decisões (`PendingCardSelection`,
  `PendingSummonResponse`, `PendingAttackResponse`) continua independente.
  Unificação sob um scheduler genérico é etapa futura.
- Políticas mais avançadas de On/Auto/Off inspiradas em jogos posteriores
  podem alterar **oportunidades de resposta**, não apenas o layout.
  Não confundir supressão do prompt com aceitar automaticamente um custo.

## Testes

`src/tests/Joey.Tests/RevivalJamDecisionChecks.cs` exercita:
solicitação do dono, aceite e recusa, pagamento único, ID obsoleto,
LP insuficiente, campo lotado, retorno exato e dois gatilhos na mesma
batalha. Continuam pendentes captura com humano (Sim/Não), perfil de
CPU/demo e comparação de janelas temporais.
