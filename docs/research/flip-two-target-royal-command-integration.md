# Integração dos PRs #55 e #56 — seletor duplo e supressão tardia

Este documento descreve uma integração **sandbox**, não uma nova evidência
de paridade nativa. A branch tem como pais os HEADs dos PRs #55 e #56;
nenhum deles foi mesclado na `main` por esta investigação.

## Problema
Greenkappa (585) e Tornado Bird (1521) abrem
`PendingFlipSpellTrapTargets`. O PR #56 já negava na criação do FLIP
e revalidava três famílias de pedidos, mas não conhecia o seletor
especializado criado no PR #55.

## Implementação
- O gate `FlipEffectsNegated` impede a criação inicial dos dois efeitos.
- `TryNegatePendingFlip(PendingFlipChoice.SpellTrapTargets)` também
  verifica Royal Command antes de concluir uma escolha iniciada.
- Se houve supressão, limpa a solicitação e os alvos, sem mover nenhuma
  S/T, e retoma a continuação de batalha.
- O seletor ainda revalida presença e identidade das ocorrências.
- Royal Decree e Jinzo continuam interferindo no gate conservador
  do PR #56. A engine de chains e a ordem nativa são pendências.

## Regressões cruzadas
`FlipSuppressionIntegrationChecks.cs` testa:
1. Greenkappa negada desde o Flip Summon;
2. Tornado Bird negada em revelação por efeito;
3. Greenkappa negada após abrir pedido, sem destruir S/T;
4. Tornado Bird negada após escolher o primeiro alvo, sem retorno à mão;
5. Royal Decree impedindo a supressão de Tornado Bird;
6. Combate completo sob Royal Command ativa.

O cenário de supressão tardia usa mutação direta da fixture do campo
para simular mudança de estado entre pedido e resolução.
**Não afirma que esse timing específico é permitido no Joey original.**

## Pendência Prohibition
`0056B440` não é uma busca simples por ID de nome proibido:
consulta vínculos de ocorrência; `005674E0` tem equivalências de IDs.
Uma integração futura de Prohibition exige rastrear declaração e
momento de aplicação, além de capturas nativas.
