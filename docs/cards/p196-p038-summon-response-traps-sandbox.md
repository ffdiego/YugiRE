# P196/P038 — Trap Hole e Torrential Tribute (sandbox)

Status: **implementação C# com testes de regressão sintéticos**, sem paridade
dinâmica do callback nativo confirmada. Branch empilhada no PR #17
(Dark Hole / Raigeki), sem alterar main.

## IDs, gatilhos e limites

O catálogo `docs/evidence/card-callback-catalog.json` identifica:
| Armadilha | Resource ID | Índice DAT | Perfil | Condição |
| --- | ---: | ---: | --- | --- |
| Trap Hole | 1002 | 120 | P196 | Normal/Flip Summon do adversário com ATK ≥1000 |
| Torrential Tribute | 1425 | 272 | P038 | Invocação de qualquer lado; destruição de todos os monstros |

Card text: https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4836&ope=2
e https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5114&ope=2 .
O código limita a abertura real da janela aos caminhos Normal/Flip
Summon. **Special Summon não publica ainda janelas**, inclusive as
invocações durante resolução no Damage Step, e por isso Torrential
tem cobertura parcial do gatilho histórico.

## Estrutura da reação

- `SummonTrapRules` é pura: `DemoSummonEvent` especifica jogador,
  ocorrência, zona e tipo da invocação. `CanActivate` verifica ID,
  classe DAT Trap=21, controle, tipo de invocação e ATK visível
  calculado com os bônus de campo/equipamentos.
- `DemoDuel.PendingSummonResponse` apresenta as opções válidas
  (com dono, zona e carta), somente para armadilhas Set já disponíveis.
  `setCardTurn` impede que uma armadilha colocada no turno corrente
  seja usada. Fixtures que injetam uma carta Set diretamente nas zonas
  a consideram anterior ao turno observado (política de teste).
- `ActivateSummonTrap(owner, zone)` revalida a identidade e o estado.
  Trap Hole destrói exclusivamente a ocorrência invocada; Torrential
  reutiliza o `ResolveMonsterWipe` atômico, incluindo monstros
  face-down dos dois jogadores. Não executa FLIP por destruição.
- `PassSummonResponse` encerra a oportunidade sem consumir cartas.
  Enquanto a resposta está pendente, o Core bloqueia as ações normais
  e a UI raylib mostra Activate/Pass (hot-seat). **Somente uma armadilha
  ou Pass por janela**: não existem encadeamentos/negações.
- O envio ao cemitério por Trap Hole/Torrential passa por
  `RemoveMonster`. Sangan/Witch e Spear Cretin podem gerar seleções
  depois da destruição, mas P164/P168 não ativam pois não houve
  destruição em batalha. `resolvingNormalSummon` impede que tributos
  iniciem efeitos de cemitério no meio de uma invocação; a janela
  antecede as buscas P165.
- Se a Normal Summon do Sonic Bird for respondida com Trap Hole,
  a busca P165 continua enfileirada no sandbox por ter ocorrido a
  invocação. A ordem exata no Joey depende de chain/timing nativos
  e **não** está sendo reivindicada.

## Escopo de teste

`src/tests/Joey.Tests/SummonTrapChecks.cs` testa:
- Trap Hole no limiar inclusivo 1000 ATK, e ATK 990; buffs ativos;
- Normal Summon adversária, Flip Summon voluntária e revelação externa
  sem invocação (não aciona trap);
- separação dono/controlador, carta Trap com classe 21;
- cartas Set de turno anterior vs carta recém-colocada;
- Torrential na própria invocação, destruição de ambos os lados,
  limpeza de face-down, fila Sangan/Witch e ausência de recrutamento
  por batalha;
- múltiplas opções com decisão Pass; ativação inválida atômica;
- busca P165 Sonic Bird aguarda resposta, mesmo se destruído;
- bloqueio de ativar Trap a partir da mão no fluxo de magia.

## Limitações importantes

**Não é implementação de chains.** Não reproduz hierarquia de prioridade,
respostas múltiplas, Magic Jammer/Seven Tools, armadilhas contínuas,
imunidades, ativação em Damage Step, Special Summon gerando janelas
nem integra todas as FLIPs com efeitos de seleção pendentes. Não há
captura diferencial nem tempos do `joey_pc.exe`. Os limites acima
devem bloquear promoção de `Done (sandbox)` para `Verified`.
