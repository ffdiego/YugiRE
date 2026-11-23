# P168 — Bubonic Vermin (1307), recrutamento por FLIP

## Proveniência

- `resourceId=1307`, `internalIndex=588`, perfil `P168` na tabela
  `card_callbacks` do SQLite e callback primário `0058B040`.
- Giant Germ (1114) e Nimble Momonga (1115) compartilham esse callback,
  **mas não o mesmo gatilho**: o recrutamento deles ocorre após destruição
  em batalha e envio ao cemitério.
- `00599E30` foi decompilada no SQLite e reconhece os três IDs como
  seleção por mesmo resource ID no deck. O resolvedor específico de
  `0058B040` não está decompilado nesse SQLite; o estudo estático
  `docs/research/card-static-sweep/09-efeitos-fora-da-tabela.jsonl`
  associa a colocação a `005ADA20`, com comando `0x60`.
- Texto histórico do DAT, documentado no sweep: FLIP opcional para invocar
  **uma cópia** de Bubonic Vermin de seu deck em **defesa face-down** e
  embaralhar o deck.

## Escopo implementado no sandbox

- `FlipEffectRules` classifica Bubonic Vermin como FLIP de recrutamento.
- `DemoDuel.TriggerFlip` monta `PendingCardSelection` com origem Deck,
  destino Field, filtro `SameNameMonster`, `MinSelections=0`,
  `MaxSelections=1`, `RequiredResourceId=1307`,
  `SummonPosition=FaceDownDefense`.
- Uma escolha válida reusa `ResolveCardSelection`: confirma índice e
  identidade da ocorrência, seleciona zona livre, faz shuffle uma vez,
  invoca em defesa oculta e emite `MonsterSpecialSummoned`.
  `SkipOptionalCardSelection` resolve sem embaralhar nem invocar.
- Não há seleção quando falta cópia do deck ou espaço livre.
- O pedido de seleção suspende a continuação da batalha, permitindo
  que uma fonte destinada à destruição por batalha resolva seu FLIP antes
  de ir ao cemitério. O custo de Normal Summon não é consumido.
- A CPU pode escolher a revelação e usa o seletor existente. A interface
  utiliza a janela provisória de escolha; sem reivindicação de paridade
  de coordenadas ou timings com Joey.

## Regressões

`src/tests/Joey.Tests/BubonicVerminChecks.cs` cobre: despacho por ID,
origem/destino/filtro/postura, seleção válida e inválida sem mutação,
shuffle único, ausência de cópia ou espaço, recusa opcional, face-up
não disparando FLIP, CPU, revelação externa, batalha com destruição
diferida e recusa durante batalha.

## Limitações e estado

**Partial até o CI.** Não há paridade nativa validada, nem engine completa de
chains, Royal Command/supressão, bloqueio de Special Summon, controle
trocado, restrições de timing ou RNG original. O SQLite não contém
a decompilação do callback primário `0058B040`; não inferir dela
informação sobre custos ou janelas não demonstrados.
