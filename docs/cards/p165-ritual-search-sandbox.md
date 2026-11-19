# P165 — Sonic Bird / Senju of the Thousand Hands (sandbox)

Status: **implementado no Joey.Core com testes sintéticos; paridade nativa ainda
não demonstrada**. PR empilhado sobre P082/P088 (PR #15); `main` intacta.

## Evidência / identificação

`docs/evidence/card-callback-catalog.json` associa o callback P165
`0x0058ADD0` aos seguintes recursos:

| Carta | Resource ID | Índice DAT | Busca |
| --- | ---: | ---: | --- |
| Sonic Bird | 1122 | 497 | 1 Magia de Ritual do próprio Main Deck à mão |
| Senju of the Thousand Hands | 1109 | 485 | 1 Monstro de Ritual do próprio Main Deck à mão |

A restrição **Normal ou Flip Summoned** vem dos textos históricos de
ambas as cartas, não da identidade compartilhada do callback por si.
A escolha é opcional; sem candidatos, não há prompt nem embaralhamento.
A revelação ao receber um ataque ou por efeito externo *não é* Flip
Summon e não ativa essa família. Special Summon também não é gatilho.

Fontes de card text: base oficial
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4916&ope=2&request_locale=en
e catálogo de decks oficial
https://www.db.yugioh-card.com/yugiohdb/member_deck.action?cgid=cfe82c995dae64700bf1&dno=5&request_locale=en .

## Identificação das cartas Ritual no deck

`docs/evidence/ritual-rules-catalog.json` extraiu estaticamente
**16 pares** da tabela compactada no EXE (VA `0x005ED060`).
`RitualSearchRules.cs` preserva os **IDs exatos** desses 16 monstros
e 16 magias. Para o filtro do C#, exige ainda `Card.IsMonster` ou
`Card.ClassCode == 22`. Não infere o tipo Ritual por nome, classe de
monstro genérica ou semelhança do card text.

`CardSelectionRules` acrescenta filtros `RitualMonster` /
`RitualSpell`, sem alterar filtros pré-existentes. O resolvedor
`PendingCardSelection` é o mesmo usado para P006 Deck→Hand.

## Disparo e resolução

- `DemoDuel.Summon(..., faceDown:false)` registra o gatilho depois
  de completar a invocação normal e eventuais tributos.
- `DemoDuel.ChangePosition`, quando a mudança é uma **Flip Summon
  voluntária**, registra o gatilho após a revelação.
- `DemoDuel.Attack` e `RevealMonsterByEffect` não enfileiram P165;
  tampouco `Summon(...,faceDown:true)` ou `PlaceSelectedMonster`.
- A fila compartilhada guarda o evento de invocação separado dos de
  campo→cemitério, impedindo que um gatilho anterior seja perdido.
- Com uma ou mais candidatas, inicia seleção opcional de exatamente
  **uma carta ou nenhuma**. Ao selecionar, remove a ocorrência indicada
  pelo índice de origem no Main Deck, a transfere à mão e embaralha o
  restante do deck com seed determinística **de sandbox**. Skip não
  altera o deck e não permite repetir a invocação normal.
- A UI provisória da raylib diferencia busca para a mão de recrutamento
  para o campo; não implementa layout/timings do seletor original.

## Validação

`src/tests/Joey.Tests/RitualSearcherChecks.cs` cobre identidade de
recurso vs índice DAT, todos os 16 pares, tipo classCode correto,
Normal Summon nos dois lados, Flip Summon, ausência de alvos, Skip,
seleção inválida, conservação de cartas, e ausência de ativação quando
a carta é apenas revelada em combate ou por efeito.

## Limites

Nenhuma carta de Ritual é **Ritual Summoned** por esta implementação;
somente busca no deck. Sem chains/negações, efeito de cartas que
modificam buscas, regras de deck-out na ação, diferenças de controlador,
ou prova de paridade diferencial nativa do P165. As versões atuais de
texto TCG podem ser distintas do executável original. `Done (sandbox)`
não significa `Verified`.
