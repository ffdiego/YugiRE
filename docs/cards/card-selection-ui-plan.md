# Próxima família: seleção de cartas por efeito — reutilizar a UI do Joey

Status: **P018 e P006 implementados no sandbox; P164 pendente.**
A etapa anterior foi Morphing Jar #2 (`1163`), que exige escavação automática
do deck e **não** uma escolha do jogador.

## Importante: selecionar cartas é uma ação de UI, não um gatilho de regra

Há vários efeitos que abrem uma seleção parecida no Joey original, mas
diferem em **evento de origem**, **zona**, **filtro** e **destino**. O
modelo deve permitir reutilizar a mesma UI raylib sem confundir seus
gatilhos. Endereços/funções da tela nativa de escolha ainda **precisam
ser mapeados no Ghidra**; sabemos que o jogo apresenta uma tela de
seleção, mas isso não prova qual handler pertence a cada carta.

| Carta | Resource ID | Callback catalogado | Gatilho | Candidatos | Destino |
| --- | ---: | --- | --- | --- | --- |
| Magician of Faith | 427 | P018 | FLIP | Magias no próprio cemitério | Mão |
| Mask of Darkness | 101 | P018 | FLIP | Armadilhas no próprio cemitério | Mão |
| Sangan | 47 | P006 | Envio do campo ao cemitério | Monstros no próprio deck com ATK ≤ 1500 | Mão |
| Witch of the Black Forest | 573 | P006 | Envio do campo ao cemitério | Monstros no próprio deck com DEF ≤ 1500 | Mão |
| Mystic Tomato | 1123 | P164 | Destruído em batalha e enviado ao cemitério | Monstros DARK com ATK ≤ 1500 no deck | Invocar no campo |
| Giant Rat | 1108 | P164 | Destruído em batalha e enviado ao cemitério | Monstros EARTH com ATK ≤ 1500 no deck | Invocar no campo |
| Shining Angel | 1117 | P164 | Destruído em batalha e enviado ao cemitério | Monstros LIGHT com ATK ≤ 1500 no deck | Invocar no campo |

Os IDs, nomes e callbacks P018/P006/P164 foram conferidos em
`docs/evidence/card-callback-catalog.json`. Os filtros descritos são
baseados no texto/terminologia histórica das cartas; regras e efeitos
podem divergir das erratas modernas, então a validação deve usar o
`data.dat` original de Joey. A identificação das flags de atributos
já existe parcialmente no `Joey.Core` e deve ser testada sobre o DAT.

**Sangan, Witch of the Black Forest, Mystic Tomato, Giant Rat e
Shining Angel NÃO são monstros FLIP.** A tela de seleção poderá ser
reutilizada, mas seus efeitos dependem de eventos de saída do campo,
destruição por batalha e condições apropriadas.

## Proposta de contrato para o Core

```csharp
public enum CardSelectionSource { Deck, Graveyard }
public enum CardSelectionDestination { Hand, Field }
public record PendingCardSelection(
    int Owner, CardSelectionSource Source,
    CardSelectionDestination Destination,
    IReadOnlyList<Card> EligibleCards,
    int MinSelections, int MaxSelections,
    string EffectId);
```

O contrato agora existe como `PendingCardSelection` em `CardSelectionRules.cs`, com candidatos identificados por `SourceIndex` (posição na zona, não índice DAT). Apenas `Graveyard -> Hand` tem resolver implementado.
O Core calcula a lista de identidades elegíveis segundo o efeito e
bloqueia ações normais enquanto há uma escolha; a UI apenas exibe
as opções e retorna a identidade/índice selecionado. Ao confirmar,
o Core revalida origem, elegibilidade e destino, preservando
atomicidade. Para busca no deck, após a seleção transfere o card
e embaralha; para cemitério, remove o card diretamente. Para invocação
do deck, também valida as vagas e posição.

## Sequência de implementação sugerida

1. **P018 (sandbox):** Magician of Faith + Mask of Darkness. Reutiliza o mesmo
   seletor filtrado por Spell ou Trap, lendo cartas do cemitério. Os
   casos sem cartas válidas não devem abrir uma tela vazia.
2. **P006:** Sangan + Witch. Introduzir evento genérico de envio do
   campo ao cemitério, seleção do deck e shuffle auditável, evitando
   acionar na saída para mão ou deck.
3. **P164:** Mystic Tomato, Giant Rat e Shining Angel. Distinguir
   destruição por batalha de outras causas e selecionar carta a
   invocar, verificando limite de zonas e flags de atributo.

Investigar estaticamente no Ghidra qual handler do original compõe a
tela de escolha, as fontes dos cartões e a ordenação original. Documentar
as coordenadas e os tempos separadamente no `Joey.LookAndFeel`.
**Não reabrir a trilha geral de animações**; somente a UI necessária
para cumprir os efeitos de cartas.

## Estado P018

Spell tem classe DAT 22; Trap tem classe 21. A seleção de uma carta é obrigatória se houver elegíveis. O Core revalida identidade e filtro antes da transferência; ações normais ficam bloqueadas até a decisão. Se o FLIP ocorre em combate, a destruição já calculada fica diferida até a escolha, inclusive para um defensor destruído. A interface raylib é um overlay paginado provisório, não a UI nativa verificada. Testes: `src/tests/Joey.Tests/CardSelectionChecks.cs`.

## Experimento P006 — Sangan / Witch

O callback nativo P006 (`0x00583D20`) tem os IDs 47 (Sangan) e 573 (Witch of the Black Forest) catalogados em `docs/evidence/card-callback-catalog.json`. O sandbox implementa eventos `campo -> cemitério` em `RemoveMonster`, inclusive tributos, destruição por batalha e efeitos, **não** retorno à mão, descarte da mão ou embaralhamento de monstros no deck. Seleção `Deck -> Hand` usa o mesmo `PendingCardSelection` de P018; Sangan filtra monstros com ATK até 1500, Witch DEF até 1500 (limites inclusivos), e a escolha embaralha o restante do Main Deck. Cada remoção elegível é enfileirada, evitando perda de múltiplos gatilhos no mesmo tributo/batalha. No sandbox, controlador = dono; sem cadeia, negação, substituição de envio ao cemitério ou paridade nativa de shuffle. Banlist rege montagem do deck, nunca a elegibilidade do buscador. Testes em `DeckSearcherChecks.cs`; os perfis nativos ainda exigem captura/diferencial.
