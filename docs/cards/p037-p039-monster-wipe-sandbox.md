# P037/P039 — Dark Hole e Raigeki (sandbox)

Status: **implementados no Joey.Core com testes C# sintéticos**, não
validados dinamicamente contra `joey_pc.exe`. Esta branch é empilhada
no PR #16 e não modifica `main`.

## Callback / semântica

`docs/evidence/card-callback-catalog.json` registra:

| Magia | Resource ID | Índice DAT | Callback | Escopo |
| --- | ---: | ---: | --- | --- |
| Dark Hole | 335 | 802 | P037 | Todos os monstros de ambos os lados |
| Raigeki | 336 | 345 | P039 | Todos os monstros controlados pelo adversário |

Ambos são Spells do DAT (`classCode=22`), efeitos **sem alvo**
que atingem face-up/face-down independentemente de posição. Não são
destruição por batalha; monstros face-down **não são virados** por
esse efeito. Textos das cartas são consistentes com
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4342&ope=2&request_locale=en
e
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4343&ope=2&request_locale=en .

## Core e interface

- `MonsterWipeRules.Scope` identifica o efeito pelo **resource ID**, não
  pelo índice DAT. `Targets` apenas cria snapshot de ocorrências no
  campo, sem implementar target selection ou condições de imunidade.
- `DemoDuel.Play` e `ActivateSetSpell` validam se há monstro afetável.
  Sem candidato ou sem espaço para ativar magia da mão, não consomem carta.
  Carta Set fica na zona até ativar; depois vai ao GY do dono.
- `ResolveMonsterWipe` percorre todos os ocupantes coletados, chama o
  caminho comum `RemoveMonster` para limpar campo e equipamentos
  associados e envia os monstros para seus cemitérios.
- `resolvingBoardWipe` impede abrir escolhas P006/P169 no meio de uma
  destruição em massa: enfileira eventos e somente após concluir todos
  os removals inicia as decisões. A ordem interna do sandbox é lado 0
  depois lado 1 e zonas 0..4; ainda não é paridade nativa de chain.
- Sangan e Witch continuam buscando monstros; Spear Cretin previamente
  virado continua oferecendo ressureição aos jogadores. **P164/P168 não
  disparam**, por exigirem destruição em batalha. Um FLIP ainda face-down
  não executa seu efeito simplesmente porque foi destruído.
- `LastMonsterWipe` registra ID, lado do conjurador, totais destruídos
  por lado e quantos estavam face-down. É um audit interno, não JSONL
  nativo.

## Casos de teste

`src/tests/Joey.Tests/MonsterWipeChecks.cs` cobre identificação dos IDs,
escopos e no-target, ativação da mão e Set, face-down sem FLIP, preservação
do lado próprio com Raigeki, destruição de ambos com Dark Hole, ordem
pós-remoção de Sangan e Witch, continuidade de Spear Cretin, rejeição
sem monstros, classificação DAT incorreta e falta de zona livre.

## Pendências

Não há chain, timing de resposta, negação, imunidade/efeitos contínuos,
banimento substituindo cemitério, troca de controle ou matching com
rotinas P037/P039. Mais adiante, Torrential Tribute (P038) e Mirror
Force (P201) poderão reutilizar a seleção de campo, **mas dependem de
janelas de ativação próprias** — não tratá-las como magias de Main Phase.
`Done (sandbox)` continua distinto de `Verified`.
