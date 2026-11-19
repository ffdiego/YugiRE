# P082/P088 — FLIPs de manipulação do Main Deck (sandbox)

Status: **implementado para testes sintéticos**, sem paridade dinâmica verificada
no `joey_pc.exe`. Branch empilhada em P168/P169, P164, P006 e P018; não altera
`main`.

## Identidades e regra-base

O catálogo `docs/evidence/card-callback-catalog.json` registra:

| Carta | Resource ID | Índice DAT (referência) | Perfil | Efeito em resolução |
| --- | ---: | ---: | --- | --- |
| Needle Worm | 561 | 674 | P082 | Envia ao GY as cinco cartas do topo do deck adversário |
| Hiro's Shadow Scout | 610 | 18 | P088 | Adversário compra três cartas; descarta as **magias recém-compradas** |

Os perfis e IDs são evidência estática do catálogo. Os resultados das
cartas seguem seus textos históricos; a execução no jogo original ainda
**não foi comparada** carta a carta. Referências públicas:

- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4568&ope=2&request_locale=en
- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4617&ope=2

## Arquitetura

A classe `DeckFlowRules` fornece primitivas de transferência ordenada.
É um helper reutilizável para outros efeitos de compra e descarte, **não**
um agendador de chains. `DemoDuel.TriggerFlip` o chama no Flip Summon,
na revelação por ataque ou em um efeito que vira a carta face-up.

- `SendTopToGraveyard`: retira até cinco ocorrências da fila do
  Main Deck adversário, na ordem exata, e as anexa ao GY do mesmo lado.
  **Não compra** cartas nem derrota automaticamente ao esvaziar o deck.
  O próximo draw obrigatório usa a regra existente de deck-out.
- `DrawThenDiscardNewSpells`: compra até três cartas ao fim da mão
  adversária, identifica apenas as recém-compradas e move as de classe
  DAT **22 (Spell)** para o GY preservando a ordem da compra. Trap=21
  não é descartada; magias que já estavam na mão, inclusive de identidade
  igual, também não.
- `DemoDuel.LastDeckFlow` registra recurso, controlador, lado afetado,
  quantidade solicitada, movida, retida na mão, enviada ao GY,
  magias descartadas e falha de compra. Não é um trace nativo.

Se o adversário não possui cartas suficientes para a compra obrigatória
de Hiro, o sandbox move as cartas restantes, processa as magias nelas
e marca a vitória do dono do efeito por `DrawFailed`. **Esta é uma
política explícita do sandbox**: a sequência exata de derrota e cartas
já compradas precisa ser conferida no Joey antes de promover a
`Verified`.

## Testes sintéticos

`src/tests/Joey.Tests/FlipDeckFlowChecks.cs` verifica:
- despacho pelos IDs corretos versus índices DAT;
- FLIP voluntário, revelação por efeito e revelação em combate;
- ordem do deck para o cemitério, incluindo deck com menos de 5 cartas;
- nenhuma derrota imediata ao esvaziar deck por mill, e derrota na
  próxima compra obrigatória;
- compra de três com Spell/Trap/Monster e identificação por faixa de mão,
  mesmo com um Spell já presente de identidade idêntica;
- adversários em ambos os lados, condição de deck-out por forced draw;
- coexistência com as zonas GY e as regras de recuperação anteriores.

## Não coberto

Chains, negação, efeitos de compras subsequentes, descarte reativo,
troca de proprietário/controlador, cartas que substituem envio ao GY,
side deck, ordem de timing nativo e UI. Os efeitos só são considerados
`Done (sandbox)` após CI, sem reivindicar paridade nativa.
