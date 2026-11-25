# CARDS — detalhes dos itens

Histórico e contratos movidos de PLANS.md na revisão de 24/11/2026 (base `42f7813`). [Plano vivo e status atuais](../../PLANS.md). Registrar novos relatos (PR, CI, evidência, limitações) no bloco do item; não duplicar relatos no plano. Os relatos anteriores são históricos, não certificam o estado atual de uma branch.

> **Decisão 25/11/2026 — aceleração com segurança:** texto do DAT local e regras da época de Joey como ponto de partida; definições declarativas por família e testes por ID. Não exigir verificação nativa individual para Done; preservar escopo e dependências UI/CPU. Novos efeitos somente após gate C12.1 e pela fila. C18 é triagem consultiva; C14/C15 ficam adiados como campanha, não apagados. Evidência de divergência continua acionável.

## Histórico concluído (sem ID)

- [x] Manifest por resource ID com status e famílias de efeito, separado do registry legado de RE.
- [x] `scripts/card_playability.py` lê `data.dat` com Joey.Coverage, filtra as 771 cartas dos três packs ou 350 de Joey, separa Monster/Spell/Trap e produz CSV/JSON/Markdown com porcentagens.
- [x] Executado no DAT original local: 771 registros nos packs, 350 apenas Joey. Snapshot de grupos e badges sem texto/arte original versionados.
- [x] Classificação dos 499 monstros por nomes públicos e aliases: 264 Normal, 193 Effect, 34 Fusion, 8 Ritual; 87 nomes antes não classificados resolvidos. Fonte não substitui validação da categoria do DAT.
- [x] Badge da trilogia e do Joey no README e relatório de porcentagens atualizados pelo GitHub Actions após edições do manifest.
- [x] 264 resource IDs Normal cobertos pela regra genérica em C# (invocação, tributos, set, ataque), com cenários sintéticos por ID e teste opcional com registros reais do DAT.
- [x] Magias de dano direto `P045` e cinco de recuperação de LP (338–341, 1315) em fluxo único de ativação da mão/set. Escopo: sandbox C#, sem correntes/negação nativa.
- [x] Zona FIELD, Sogen (Warrior, depois Beast-Warrior), seis magias por atributo e Forest, Wasteland, Mountain, Umi, Yami e Chorus of Sanctuary em `P036` (tabela por tipos, bônus negativos, regra por posição).
- [x] Família `OnFlip`: Man-Eater Bug, Morphing Jar, Penguin Soldier (601), Hane-Hane (540) e Morphing Jar #2 (1163) no sandbox.
- [x] Seletor genérico de cartas pelo efeito (`CardSelectionRules`) com UI raylib: P018 (Magician of Faith 427, Mask of Darkness 101), P006 (Sangan 47, Witch of the Black Forest 573), P164 (Mystic Tomato 1123, Giant Rat 1108, Shining Angel 1117 etc.), P168/P169, P165, P082/P088. **Não rotular P006/P164 como FLIP.**
- [x] Armadilhas e magias de destruição: Trap Hole/Torrential Tribute (P196/P038), Magic Cylinder/Negate Attack (P200/P192), Mirror Force (P201), Dark Hole/Raigeki (P037/P039), destruição por tipo (P092); fusão por receita para 31 resultados dos packs.

- [x] Lotes 6–14 integrados pelo [PR #62](https://github.com/ffdiego/YugiRE/pull/62), commit `97ec005`: Dream Clown, Bubonic Vermin, Greenkappa, Tornado Bird, Lady Assailant of Flames, Fire Sorcerer, The Immortal of Thunder e Dimensional Warrior Done no sandbox; Royal Command e Dark Necrofear Partial; Prohibition Pending. Baseline histórica: 383/771 e 119/350 Done+Verified, 9 Partial e 0 Verified. Consultar [consolidação](../../docs/research/card-rules-consolidation-lotes-6-14.md), manifesto atual e `scripts/completed/README.md` (05–10 ainda não coletados).

## C01 — Taxonomia de efeitos por texto local

<a id="c01"></a>

Roteiro 14 executado com o DAT local em Fedora/.NET 10. O fingerprint e as 771 entradas conferem com `docs/cards/pack-index.json`. Rotas: 264 sem efeito aplicável, 433 com fila/resposta, 72 para revisão e duas candidatas triviais (uma de compra já Done e uma de cura Pending). A classificação serve para triagem, preserva os status do manifesto e mantém as descrições apenas no pipe privado. [Reprodução e limites](../../scripts/completed/README.md#14--c01-taxonomia-de-efeitos-pelo-dat-local).

## C02 — Lotes triviais por família

<a id="c02"></a>

Após C12.1, usar a triagem de C01/C18 e confirmar a mecânica antes de implementar. Começar por famílias independentes de baixo/médio risco com primitivas já disponíveis; não criar nova DSL. Priorizar a candidata de cura Pending com C05 e revisar manualmente os casos sem rota conclusiva antes de ampliar o lote. Usar primitivos reutilizáveis na fila, cenários por família/ID e atualizar `CARDS.md` e o manifesto a cada entrega.
  C02.1: Eternal Rest (655/P092, Kaiba) usa o mesmo executor de wipe, filtro por vínculos vivos dos dois lados e destruição individual; testes mão/Set, identidade e equipamento múltiplo. [Contrato](../../docs/cards/c02-eternal-rest.md). Done condicionado ao CI; nenhuma promoção Verified.
  C02.2: Fusion Sage (1040/P134, Joey), gasto por comando seguido de abertura de seleção Deck → Hand e reutilização do resolvedor de busca; dois IDs de Polymerization. [Contrato](../../docs/cards/c02-fusion-sage.md). Done condicionado ao CI; sem Verified.
  C02.3: Giant Trunade (1080/P152, Joey) reaproveita lote S/T e ReturnToHandCommand, com gasto separado e preservação dos vínculos/identidade. [Contrato](../../docs/cards/c02-giant-trunade.md). Done condicionado ao CI; sem Verified. Outras buscas (Last Will, Painful Choice, Thunder Dragon) não equivalem ao filtro simples de Fusion Sage; requerem gatilho, custo ou seleção múltipla.
  C02.4: Spiritualism (1539/P262, Joey) combina seleção adversária de S/T e retorno por ocorrência, preservando limpeza seletiva de Destiny Board. [Contrato](../../docs/cards/c02-spiritualism.md). Done no sandbox sem correntes condicionado ao CI; imunidade à negação pendente de integração, não Verified.
  C02.5: Tribute to The Doomed (1023/P074, Joey/Kaiba) introduz seleção independente de custo e alvo, descarte por DiscardCommand, gasto e resolução condicionada aos dois resultados. [Contrato](../../docs/cards/c02-tribute-to-the-doomed.md). Done no sandbox condicionado ao CI; console/UI/CPU, correntes e proteção nativa de alvo pendentes.
  C02.6: Thousand Knives (1211/P077, Joey/Kaiba) reutiliza resolução seletiva de Tribute, adicionando fonte condicional sem descarte, identidade Dark Magician 34/1210/2034 e alvo adversário. [Contrato](../../docs/cards/c02-thousand-knives.md). Done condicionado ao CI; sem Verified.
  C02.7: Fissure (1001/P114, Yugi) reutiliza destruição unitária e seleciona pelo menor ATK calculado entre adversários face-up; único mínimo automático, empate exige escolha explícita. [Contrato](../../docs/cards/c02-fissure.md). Done condicionado ao CI; empate/timing nativo pendentes no script 30, sem Verified.
  C02.8: Offerings to the Doomed (1451/P105, Joey) adiciona skip de Draw Phase por dono/turno, coalescido, independente da destruição e das compras por efeito; preserva Standby. [Contrato](../../docs/cards/c02-offerings-to-the-doomed.md). Done condicionado ao CI; janela Quick-Play fora da Main própria e correntes pendentes de ACTIONS. Script 30 ampliado com 2 callbacks ausentes.
  C02.9: Final Destiny (1067/P144, Kaiba) exige cinco ocorrências distintas da mão; custo completo e gasto precedem snapshot não seletivo de monstros, S/T e FIELD, com limpeza vinculada única. [Contrato](../../docs/cards/c02-final-destiny.md). Done condicionado ao CI; script 30 ampliado com dois callbacks ausentes no v3.
  C02.9: P123 (1015/1016/1075/1076/1082, Yugi/Kaiba) usa gasto e ATK/DEF temporário por ocorrência, Main/Battle ociosa, expiração na troca de turno. [Contrato](../../docs/cards/c02-p123-temporary-stats.md). Done headless condicionado ao CI, sem Damage Step, correntes ou Verified.
  `python scripts/card_effect_backlog.py scratch/card-taxonomy.json` resume Pending e Partial por rota e família usando o manifesto atual. Use `--pack joey` para filtrar o pack Joey. Exportações iniciais v1 que também rotulavam compra simples como busca são aceitas como compra.

## C03 — Monstros de efeito restantes (193 Effect)

<a id="c03"></a>

Além dos já feitos (FLIP, P006, P164 etc.), cobrir gatilhos de campo/cemitério/batalha, efeitos contínuos e de invocação. Agrupar por mecânica, não por ID. Para Joey, priorizar os 109 Effect do pack. Fatia de intenções de dano de batalha P019/P023/P078/P239/P120 em [contrato C03](../../docs/cards/c03-battle-damage-effects.md), sem mutação nem promoção de cobertura. Integrar após evento de dano de batalha na fila A02/A06; P239/P120 exigem seleção e comandos adicionais.

## C04 — Equipamentos `P025`

<a id="c04"></a>

Expandir `EquipmentRules`/`DemoDuel` por famílias de filtros e bônus, além dos três recursos modelados (Legendary Sword, Beast Fangs, Sword of Dark Destruction). O perfil `P025` tem 41 callbacks catalogados; **não assumir** que os 41 tenham a mesma regra. Fatia de 18 IDs dos packs com bônus fixos implementada no fluxo de mão existente; ver [contrato C04](../../docs/cards/c04-fixed-equipment.md). Testes C# aprovados. Set e fila: fatia C04.2 com EquipCommand, 21 IDs fixos e 3 dinâmicos no sandbox opt-in, sem novo caminho legado. C04.4: 21 fixos Done no escopo sandbox após CI de ciclo completo mão/Set, ambos lados e quatro saídas; seleção por ocorrência. C04.5: três dinâmicos Done no escopo sandbox após CI, composição e ciclo de vida; [contrato](../../docs/cards/c04-dynamic-equipment-lifecycle.md). [Ciclo fixo](../../docs/cards/c04-fixed-equipment-lifecycle.md). UI/CPU, supressões e demais efeitos continuam pendentes. Ver [contrato](../../docs/cards/c04-queued-dynamic-equipment.md). Stim-Pack/Germ Infection: aritmética estática fechada, produtor de avanço temporal não localizado; depende de A07. [Evidência e limites](../../docs/cards/c04-timed-equipment.md).

## C05 — Curas/danos adicionais em tabelas de efeitos

<a id="c05"></a>

C05.1: Blue Medicine (1006/P041), cura 400 LP por fila, texto confirmado e testes mão/Set. Tremendous Fire (346/P053) exige semântica de dano aos dois lados e empate em A08; Mooyan Curry (337) fora do índice de 771. [Contrato](../../docs/cards/c05-queued-blue-medicine.md).
  Sem prometer tamanho de lote antes de confirmar o catálogo original.

## C06 — Demais armadilhas e janelas de resposta/correntes

<a id="c06"></a>

As cinco já feitas reaproveitam janelas de Summon/ataque. Faltam as outras, Special Summon como gatilho e correntes/respostas, **sem fingir que uma fila simples cobre todos os casos**.

## C07 — Gatilhos de cemitério/batalha e refinamento de FLIP

<a id="c07"></a>

Janelas de resposta, negadores, deck-out nativo (hoje é política simplificada) e mais gatilhos sob demanda. Fatia pura de chegada ao cemitério: P066/P044/P068/P030; [contrato C07](../../docs/cards/c07-graveyard-arrival-effects.md). Integração depende da causalidade e ocorrência na fila (A02/A06). Roteiro 12 fecha ambiguidades de envio direto e callbacks faltantes; não bloquear outras famílias.

## C08 — Ampliar Morphing Jar #2

<a id="c08"></a>

Fusion/Extra Deck, tokens, controle de cartas alterado e seleção quando >5 monstros invocáveis. Não declarar paridade nativa até medir callbacks P182 no Windows.

## C09 — Rituais (`P269`)

<a id="c09"></a>

C09.1 implementa as 16 receitas P269 headless com mão/Set, tributos próprios e colocação por ocorrência. Oito magias e sete monstros do índice Done no escopo indicado após CI; Relinquished Partial. Produtores UI/CPU, efeito próprio de Relinquished, interações, restrições de reviver e comparação nativa pendentes. [Contrato](../../docs/cards/c09-queued-ritual-summon.md).

## C10 — Fusões restantes

<a id="c10"></a>

Respostas a Special Summon e efeitos próprios de três resultados ainda Partial; conferir restrições da versão Joey.

## C11 — Casos emblemáticos

<a id="c11"></a>

Exodia, Relinquished, Destiny Board, Scapegoat e condições especiais.

## C12 — Migrar famílias Done para a fila de ações

<a id="c12"></a>

**Objetivo:** eliminar o caminho duplo (`Demo.cs` legado + `DemoActionQueue.cs`), sem alterar a cobertura jogável. O App ainda usa a fila via `--queued-actions` na base desta revisão. A migração não é expansão de catálogo.

**Inventário de partida:** LP (10 magias), compra (Pot of Greed, Upstart Goblin), wipes P037/P039/P092, Duster/Heavy Storm P094/P141, MST/Remove Trap, FLIPs, P018/P006/P164/P168/P169/P165/P082/P088, Greenkappa/Tornado Bird/Four-Starred Ladybug, P051/P052, Morphing Jar #2, procedimentos C16, equipamentos e rituais têm fatias na fila em escopos distintos (ver detalhes e contratos). Há pilotos de combate isolado para Skelengel, Penguin Soldier, Man-Eater Bug e P164 em A06. Não confundir executor/headless com integração completa. Conferir o código antes de declarar migração ou repetir trabalho.

**Pendente (ordem):**
1. Produtores UI/CPU das famílias já migradas e fila como **caminho padrão** do App e da CPU, preservando comportamento e testes; coordenar A06/A07/A08. Migrar a admissão de famílias já jogáveis que ainda não têm executor, conforme necessário para manter paridade ao ativar o padrão. O #177 incorporou os persistentes do #150, e o #179 integrou as fusões de Polymerization e Fusion Gate.
2. Combate: ampliar FLIPs em batalha e P164 além dos pilotos isolados; P168 (LP de batalha), Bistro Butcher/Masked Sorcerer e demais gatilhos legados; dependências mínimas de A06/A07.
3. Equipamentos P025: remover os 21 fluxos legados de mão e integrar produtores dos equipamentos existentes.
4. Armadilhas de ataque/Summon (Trap Hole, Torrential Tribute, Magic Cylinder, Negate Attack, Mirror Force) e fusões: verificar no código o que já foi migrado antes de implementar; migrar apenas caminhos existentes.
5. P117/P159/P160, Dark Necrofear e Prohibition: inventariar lacunas ([revival](../../docs/cards/p117-p159-p160-revival-spells-gap.md), C17); não implementar regras novas nem promover status para fechar C12. Pendências sem equivalente jogável ficam nos IDs próprios.

**Pronto quando:** (a) fila padrão no App/CPU; (b) nenhuma família migrada mantém resolução legada equivalente ativa (remover ou marcar obsoleto e impedir seu uso no caminho padrão); (c) `ActionsBlocked` não depende dos `Pending*` legados dessas famílias; (d) testes atuais e regressões passam, CI verde antes da integração e cobertura jogável preexistente preservada (ganhos de lotes independentes contabilizados separadamente); (e) Demo.cs diminui por fatia, com contagem de linhas antes/depois. Se precisar de uma adaptação transitória, justificar e registrar sua remoção; não criar novo resolvedor legado.

**Gate C12.1:** a fila deve ser o caminho padrão real no App e na CPU, com paridade de cobertura jogável preexistente, regressões UI/CPU (incluindo admissão, escolhas e gates aplicáveis) e CI obrigatório verde; registrar o fechamento do item 1 com PR e evidências. Antes disso, não implementar cartas novas nem abrir lotes C02/C03. Depois, CARDS pode entregar novas famílias independentes exclusivamente pela fila, em paralelo à migração restante A06/A07/C12, sem depender de resolvedores legados, criar bypasses ou alterar contratos compartilhados sem coordenação. AÇÕES estruturais ficam com ACTIONS; CARDS pode continuar fatias de migração acordadas. O C12 continua aberto até todos os critérios originais; comparar paridade da migração com a baseline anterior ao gate, separando ganhos de cobertura dos novos lotes.

Não ampliar IA nativa, RNG, traces ou visuais como parte de C12; melhorias após validação. Fazer somente a menor fatia necessária de A06/A07/A08 e registrar a dependência; manter EffectContext em Joey.Core até C12 fechar. Relatar migração, legado restante, linhas de Demo.cs, testes e próximo passo.

### Histórico das fatias

Reexpressar FLIP, seleções, P037/P039/P092, armadilhas e fusões como comandos (invariantes 1–5). Registrar aqui as cartas implementadas fora da fila enquanto `A02` não existir. Dívida inicial inclui as famílias dos lotes 6–14 (ver nota de consolidação abaixo), além das famílias listadas neste item.
  Dívida adicional: 21 equipamentos P025 (C04), fluxo legado de mão a migrar. Fatia C12: dez magias LP já têm admissão e executor opt-in para mão/Set, ainda falta migrar os produtores UI/CPU. [Contrato](../../docs/cards/c12-queued-lp-spells.md). Pot of Greed e Upstart Goblin também usam admissão opt-in mão/Set, gasto separado e compras individuais; [C12 compra](../../docs/cards/c12-queued-draw-spells.md). P037/P039/P092: nove magias de destruição de monstros migradas ao executor opt-in mão/Set, com vínculos e gatilhos após lote; [C12 wipe](../../docs/cards/c12-queued-monster-wipes.md). P094/P141: Duster e Heavy Storm migradas, uma destruição por S/T ou FIELD e limpeza de vínculo; [C12 S/T](../../docs/cards/c12-queued-spelltrap-wipes.md). MST/Remove Trap: consulta de candidatos e admissão por ocorrências, gasto, alvo primário e cascata Destiny Board; prompt bloqueante/UI/CPU pendentes em A06. [C12 seletivas](../../docs/cards/c12-queued-targeted-spelltrap.md). Man-Eater Bug, Hane-Hane e Penguin Soldier: resposta de Flip Summon na fila, ReturnToHandCommand e cleanup separado; batalha, prompt UI/CPU e seleção de GY pendentes. [C12 FLIP](../../docs/cards/c12-queued-monster-flips.md). P018: Magician of Faith e Mask of Darkness com recuperação de ocorrência do GY para mão por comando, resposta manual e finalização após gate; deck/batalha pendentes. [C12 GY](../../docs/cards/c12-queued-graveyard-recovery.md). P006: Sangan/Witch com ocorrências Deck, MoveToHandCommand e ShuffleDeckCommand após transferência bem-sucedida; [C12 busca](../../docs/cards/c12-queued-deck-search.md). P164: seis recrutadores com resposta opt-in após batalha legada, SpecialSummonCommand Deck → Field e shuffle somente após sucesso; [C12 recrutamento](../../docs/cards/c12-queued-elemental-recruiters.md). P168 (Nimble Momonga/Giant Germ) e Bubonic Vermin: seleção de até duas cópias, invocação face-down quando exigida e shuffle único; efeitos de LP de batalha e FLIP durante ataque continuam legados. [C12 mesmo nome](../../docs/cards/c12-queued-same-name-recruitment.md). P169: Spear Cretin com respostas obrigatórias dos dois donos, SpecialSummonCommand GY → Field e finalização após gate; [C12 ressurreição](../../docs/cards/c12-queued-graveyard-revival.md). P165 Sonic Bird/Senju: resposta opcional Deck → Hand reutiliza P006, recusa sem RNG. [Contrato](../../docs/cards/c12-queued-ritual-search.md). P082/P088: Needle Worm e Hiro's Shadow Scout em Flip Summon manual, mill individual ou draw seguido de descartes por ocorrência e finalização Exodia/deck-out. Bistro Butcher/Masked Sorcerer aguardam dano de batalha na fila (A07). [C12 deck flow](../../docs/cards/c12-queued-flip-deck-flow.md). Greenkappa/Tornado Bird e Four-Starred Ladybug: par S/T/FIELD e wipe de Level 4 em Flip Summon manual; [contrato](../../docs/cards/c12-queued-flip-field.md). P051/P052: banimento individual de Deck/Hand e dano após pagamento completo em Flip Summon manual; [C12 banish](../../docs/cards/c12-queued-flip-banish-burn.md). Morphing Jar #2: ReturnToDeckCommand reservado, shuffle por dono e escavação por cota com SpecialSummon/SendToGraveyard individuais; [C12 Jar #2](../../docs/cards/c12-queued-morphing-jar-2.md). C16: seis procedimentos Partial de invocação inerente por banimento GY, 0059DEF0/00599E30, comando Hand → Field com custo reservado; efeitos pós-invocação e UI/CPU pendentes. [Contrato](../../docs/cards/c16-queued-graveyard-banish-summon.md). 1515/1516/1518: modificadores de batalha nativos em 0056DD30, invocação por fila e combate testado; Done no escopo headless após CI. [C16 atributos](../../docs/cards/c16-elemental-battle-attributes.md). P117/P159/P160 ainda sem resolvedor; coleta direcionada `scripts/completed/16-cards-revival-spells-ghidra.sh` e propriedade/controle/vínculos a coordenar A06/A08. [Lacuna](../../docs/cards/p117-p159-p160-revival-spells-gap.md).

## C13 — Conferir os 264 Normal com o DAT real

<a id="c13"></a>

`dotnet run --project src/tests/Joey.Tests -- ../passion/data.dat` validou em Fedora/.NET 10 os 264 IDs e seus cenários de invocação, tributo, Set e ataque no sandbox. A execução usou o fingerprint do índice de packs versionado.

## C14 — Confirmar Sea Serpent `classCode=5` e testar P036 em duelo real

<a id="c14"></a>

**Adiado:** DAT local e teste opcional confirmam o código de classe para dois IDs dos packs. A comparação de P036 em duelo nativo permanece pendente. [Evidências](../../docs/cards/p036-field-spells-sandbox.md).

## C15 — Fatias `Verified`

<a id="c15"></a>

**Adiado:** após massa crítica, comparar famílias e interações em decks dirigidos, com cenários equivalentes, estados, comandos e resultados observáveis; seed quando reproduzível. Um teste de família não promove automaticamente todos os IDs a Verified. Divergências materiais podem antecipar a coleta e devem virar regressões.

## C16 — Seletor de custo de invocação especial por banimento

<a id="c16"></a>

Reutilizar `GraveyardBanishSummonRules`, seleção genérica e zona de banimento para Dark Necrofear (P252), Soul of Purity and Light, Spirit of Flames, Aqua Spirit, Garuda the Wind Spirit e demais membros confirmados da família. Validar filtro, quantidade, identidade, custo atômico, gates e seleção UI/CPU; não declarar implementados gates ainda desconhecidos. [Contrato P252](../../docs/research/p252-necrofear-control-contract.md). Se A02/A08 já estiverem disponíveis, usar a API de comandos; caso contrário registrar migração em C12. Controle na End Phase fica separado. P253: contrato puro de oportunidade Aqua (Standby adversária) / Garuda (End adversária), produtores 005A76E0/005A4990, callback compartilhado ausente; [contrato](../../docs/cards/c16-elemental-position-opportunities.md). Roteiro 17 coleta complementar ao 11; integração depende A06/A07/A08. Ambos Partial.

C12.1 integra os produtores CPU e console dos seis procedimentos C16 pelo executor existente. Em modo fila, após tentar a invocação normal, a CPU escolhe a primeira fonte elegível da mão, as primeiras ocorrências elegíveis do cemitério e a primeira zona livre. O console App/Lab aceita as ocorrências escolhidas pelo operador. O teste cobre ambos os lados, cópias, rejeição de custo inválido, origem e o gate entre banimento e invocação. O seletor gráfico reutiliza o produtor. A comparação visual e os efeitos posteriores pendentes continuam no escopo de C16/C17.

## C17 — Controle de Dark Necrofear e Prohibition

<a id="c17"></a>

Pendências distintas: identidade/owner/controller, saída do campo, vínculo e End Phase de P252 (roteiro 10); declaração e efetividade de Prohibition (roteiro 06). Dividir em novos IDs quando houver evidência suficiente. Royal Command permanece Partial; sua cadeia pertence a C06/C07. Não implementar por aproximação.

## C18 — Triagem de risco por slots de callback e mecânica

<a id="c18"></a>

**Objetivo:** orientar o custo de investigação, não substituir a interpretação do DAT ou o teste funcional. Fonte de slots: `docs/evidence/card-callback-catalog.json` (primary, candidate, availability, preparation, target_preparation). A proposta de 25/11 registra 443 entradas: 0 = 15, 1 = 94, 2 = 134, 3 = 146, 4 = 47, 5 = 7. Contagens reproduzidas em 25/11 cruzando os 269 perfis com os 443 registros de callback do catálogo versionado; não representam os 771 IDs jogáveis.

| Slots preenchidos | Risco inicial |
| --- | --- |
| 0 | Indeterminado; revisar |
| 1 | Baixo |
| 2 | Médio |
| 3–5 | Maior |

Ajustar pelo comportamento: dano/cura/compra simples e bônus fixo tendem a baixo; busca/FLIP com seleção a médio; correntes/negação, controle, efeitos contínuos com timing complexo, vitória especial ou texto ambíguo a maior. Um único callback não prova simplicidade; muitos callbacks não provam complexidade. Divergência observada aumenta a prioridade de investigação e vira teste de regressão.

**Fluxo:** cruzar por resource ID (não índice interno), preservar os casos sem correspondência e os status de playability; acrescentar risco inicial, risco ajustado, justificativa e lacunas à taxonomia C01. Começar pela descrição local e evidências existentes; usar o SQLite cumulativo/bundles antes de pedir Ghidra. Para dúvidas materiais, investigar os predicados availability/candidate e demais callbacks pertinentes por família, agrupando apenas as lacunas em scripts/pending. Não exigir exportação de todas as cartas de risco maior, nem promover uma hipótese a Verified. Se a dúvida impedir regra confiável, registrar Partial/Pending e seguir com outra família. Não versionar texto proprietário do DAT.

**Pronto quando:** a classificação é reproduzível e testada (incluindo 0 slots, IDs ausentes e ajustes por mecânica), com contagens auditáveis, exemplos, justificativas e fila de dúvidas direcionadas. C18 pode avançar antes de C12.1 como triagem/testes independentes; nenhuma nova implementação de cartas antes do gate. Depois, usar o risco para ordenar lotes e registrar testes por ID/família, sem tornar C18 inteiro pré-requisito de uma carta clara.

### C18.1 — Inventário reproduzível e triagem semântica opcional

Base `66f1fe1`, catálogo versionado e pack-index (771 entradas únicas nesta revisão). O cruzamento exato por resource ID e manifest vigente encontrou:

| Slots | Pending | Partial | Done |
| --- | ---: | ---: | ---: |
| 0 | 12 | 0 | 2 |
| 1 | 58 | 2 | 29 |
| 2 | 74 | 0 | 41 |
| 3 | 88 | 2 | 45 |
| 4 | 36 | 5 | 5 |
| 5 | 6 | 0 | 1 |
| Sem registro | 48 | 0 | 317 |

Sem Verified nesta base. Dos 48 Pending sem registro, 31 pertencem ao pack Joey e todos são classificados Effect no inventário de subtipos. Ausência de registro NÃO prova ausência de efeito; revisar regras por ID e callbacks globais existentes antes de solicitar Ghidra.

`python scripts/card_effect_risk.py` cruza os metadados públicos sem DAT/EXE e escreve somente IDs, slots, risco inicial, status atual e motivos em scratch. `python scripts/card_effect_risk.py --taxonomy scratch/card-taxonomy.json --pack joey` usa C01 real (valida fingerprints/referência) e eleva risco por famílias detectadas. Não baixa risco alto automaticamente e distingue ausência de registro, zero slots e Normal. O campo `reviewResourceIds` orienta a revisão, não prescreve extração nativa. O C01 é triagem lexical: vitória especial, timing contínuo, custos e efeitos específicos ainda exigem leitura semântica do texto local; nenhum resultado automático serve como ruling. O script não altera o manifesto, o catálogo nem as badges. Sem C01 local, a classificação semântica permanece explicitamente não avaliada. Testes offline em `tests/test_card_effect_risk.py` cobrem joins, fingerprints, risco, registros inválidos e a distribuição real. CI pendente na criação do PR.

Próximo passo: rodar com o C01 local já produzido, agrupar candidatos por família e revisar os casos ambíguos usando SQLite. Não repetir extração se scratch/card-taxonomy.json já existir; não implementar novas cartas enquanto C12.1 não fechar.

### C18.2 — Reutilizar a campanha estática antes de pedir Ghidra

Cruzamento adicional sobre `66f1fe1`: 15 JSONL, 998 IDs sem duplicatas, 133 famílias. Entre as 331 entradas Pending/Partial do manifesto, 183 têm registro Mapeado, 127 Parcial, 1 Pendente e 20 não aparecem nos JSONL (ver estudos anteriores em [reconciliação](../research/card-static-sweep/reconciliacao.md); ausência no JSONL não é ausência de evidência). Status estático nunca promove playability.

As 60 entradas inacabadas sem slots úteis já estão nos JSONL: 48 sem registro (33 Mapeado, 15 Parcial) e 12 com slots nulos (9 Mapeado, 2 Parcial, 1 Pendente). O script agora cruza os 15 estudos e inclui fonte/família/status/lacunas por ID, contagem de pendências por família e pisos semânticos conservadores para as famílias auditadas sem slots. Não exporta descrições nem duplica os estudos. C18 continua Parcial até a execução do C01 real e a revisão dirigida dos riscos por família.

**Candidatos para depois de C12.1, não executar ainda:**
- Atributos derivados: Machine King, Shadow Ghoul, Lava/Swamp Battleguard, Harpie's Pet Dragon, Buster Blader, Muka Muka, Flash Assailant, Dark Magician Girl e Wodan (406/367/553/11/385/808/515/1111/759/234). Os dez efeitos estão Mapeado; reutilizar recálculo, não comandos que alterem ATK base. Nuvia, Boar Soldier, Pumpking e Maha Vailo têm restrições/lacunas adicionais (lotes 10/11); não promover por semelhança.
- Standby LP: Spirit of the Breeze, Dancing Fairy, Cure Mermaid, Marie the Fallen One (1437/1438/1441/1446), todos Mapeado. Depende da passagem de fase A07 e de emissão por ocorrência.
- Retorno/posição de fase: Sinister Serpent, The Wicked Worm Beast, Dark Zebra (474/22/1113), Mapeado; requer Standby/End e escolha/gate aplicável.
- Combate, controle, proteção, invocação restrita e Toons dependem dos contratos de ACTIONS; não antecipar com bypass. The Unfriendly Amazon (1431) continua Pendente estático; usar DAT/SQLite primeiro e coleta dirigida apenas se material.
- Exodia (20) é Pending no manifesto nesta base apesar de regra central Mapeado. Conferir escopo realmente acionável e testes (inclusive fila e término) antes de qualquer promoção; não contar esta revisão como implementação.

### C12.1 — Produtores FLIP de par S/T (Greenkappa e Tornado Bird)

Greenkappa (585) e Tornado Bird (1521) já têm executor de par tipado, incluindo FIELD e limpeza de vínculos. UI/CPU passam pelo produtor compartilhado quando o prompt pertence à fila. A primeira seleção mantém a ocorrência exata e permite desmarcar; segunda seleção distinta admite os dois alvos, sem movimento imediato. Em rejeição, não chama o resolvedor legado; substituição do primeiro alvo invalida a seleção sem movimentar o segundo. Combate legado permanece separado. Testes ampliam QueuedFlipFieldChecks para FIELD, seleção/desmarcação, stale, busy, ambos os lados e origem CPU; gates e limpeza de equipamento existentes preservados. Demo.cs 2623 → 2623 linhas. Sem carta nova, sem alteração de manifesto, CI obrigatório. Coordenar com #150 e demais produtores C12.1; default continua opt-in.

### C12.1 — Auditoria de gate sobre PR #150

Inspeção do head `bae311a` do draft #150, sem editar a branch reservada. CI do head passou; GitHub recusou merge enquanto draft. O título não significa gate fechado. Evidências de código:
- `src/Joey.App/Program.cs:41`: fila ainda opt-in (`args.Contains("--queued-actions")`).
- `src/Joey.App/DuelScreen.cs:199–216, 418–425, 553–559`: ativação fixa e seletiva já têm produtores específicos, mas fallback `Play`/`ActivateSetSpell` e `BeginFusionGate` continuam legados. Não remover fallback sem paridade das famílias jogáveis.
- `src/Joey.Core/CpuDuelController.cs:203–232, 317–330`: caminho de Fusion Gate ativo/fusão e ataques ainda chamam métodos legados nesta branch; coordenar #137/#140/A09 antes de alterar.
- `src/Joey.Core/DemoActionQueue.cs:92–139`: dispatcher da fila cobre compra, LP, busca, wipes, persistentes e Fissure com mínimo único; empate de Fissure exige escolha explícita. Conferir integração UI/CPU dos seletores e outras ativações existentes, não assumir que executor implica produtor.
- `src/Joey.Core/Demo.cs`: 2616 linhas no head #150; `ActionsBlocked` ainda inclui `LegacyActionsBlocked`. Nenhuma linha removida nesta auditoria/triagem, nem mudança de comportamento.

Próximo passo C12: responsável do #150 deve concluir/revisar a fatia, reconciliar com a main e PRs ACTIONS; validar defaults, disponibilidade e escolhas, ordens de comandos e rejeições sem fallback mutante em regressões UI/CPU. Não marcar item 1 fechado nem iniciar novas cartas nesta rodada.

### C12.1 — Produtores de seleção de monstros FLIP (P072 e bounce)

Man-Eater Bug (500), Hane-Hane (540) e Penguin Soldier (601) usam o executor tipado existente também via UI/CPU. O primeiro alvo opcional é apenas seleção de interface, guarda a ocorrência e não altera zonas; a resposta final (0/1/2 para Penguin; 1 para os demais) é um comando, com limpeza e gate já existentes. Rejeição de resposta não cai no legado; troca por outra ocorrência do mesmo ID invalida a escolha. CPU preserva sua preferência por alvos adversários e origem Cpu. A posse do prompt é separada do estado ocupado da fila. O combate legado diferido permanece no caminho anterior; o produtor também reconhece os pilotos de FLIP em batalha já reservados pela fila. Nenhuma carta nova.

Testes em QueuedMonsterFlipTargetChecks: ambos os lados, três cartas, seleção stale, duplicata, busy, zero/um/dois alvos, troca da ocorrência, execução e origem CPU. Demo.cs 2623 → 2623 linhas (não se remove legado ainda; reutilização do executor). CI obrigatório; gate C12.1 permanece aberto. Coordenar alterações próximas em DuelScreen/CPU com #150 e ACTIONS.

### C12.1 — Produtores de seleção Deck/GY → Hand (P006/P165/P018/Fusion Sage)

Base `bd4dc3e`. A janela CardSelectionScreen e a CPU usavam ResolveCardSelection/SkipOptionalCardSelection mesmo para prompts com executor tipado. Fusion Sage é exclusivo da fila e o clique legado era recusado; os demais podiam mutar imediatamente. Novo adaptador `DemoQueuedHandSelection.cs` compartilha admissão Player/Cpu, preserva a identidade da requisição e da ocorrência no índice mostrado, e nunca recorre ao legado após rejeição ou fila ocupada. P006 Sangan/Witch, P165 Sonic Bird/Senju, P018 Magician of Faith/Mask of Darkness e Fusion Sage (C02.2) usam a resposta existente quando no escopo suportado. P018 permanece restrito ao FLIP manual Main; batalha legada com deferredBattle, Deck→Field, GY→Field e escolhas múltiplas não são migradas nesta fatia. Sem novas cartas. Não alterar layout visual.

Teste: estende QueuedDeckSearchChecks e QueuedGraveyardRecoveryChecks para ambos os lados, duplicatas, resposta stale, escolha inválida, não mutação na admissão, bloqueio de segundo clique e origem CPU. Os checks de executor/gate/shuffle existentes permanecem. CI requerido. Demo.cs 2623 → 2623 linhas (adaptador novo, nenhum resolvedor legado removido nesta fatia; preservar comparadores legados até C12 fechar). PR #150 também toca CPU em outras regiões; reconciliar sequencialmente. Gate C12.1 permanece aberto.

### C12.1 — Produtores de seleção Deck/GY → Field (P164/P168/P169/Bubonic Vermin)

Base `269ac62` (#161 integrado). Novo `DemoQueuedFieldSelection.cs`: produtor compartilhado de seleção de recrutas (P164), até duas cópias (P168), Bubonic Vermin e revival obrigatório de Spear Cretin (P169). UI mantém escolhas parciais por ocorrência em memória local da janela, sem mutar Deck/campo ou a lista de seleção legada; Done/Skip e clique final usam os comandos existentes. CPU escolhe no máximo as vagas disponíveis e admite o lote completo. Preserva posição fixa de recrutamento, opção Set DEF de P169, zero seleções quando permitido, identidade, shuffle após sucesso e apresentação por comando. Inclui P164 após batalha concluída (inclusive piloto já suportado); batalha FLIP ainda diferida continua legado até ACTIONS concluir. Não altera layout, motor de combate, manifest nem cobertura.

Checks estendidos: QueuedDeckRecruitmentChecks, QueuedGraveyardRevivalChecks, QueuedSameNameRecruitmentChecks e QueuedRitualSearchChecks (resposta opcional P165 do #161). Ambos os lados, escolhas inválidas, cópias, 0/1/2, posição, gate e CPU. Demo.cs 2623 → 2623; produtores migrados, resolvedores legados preservados para caminhos ainda não migrados. CI obrigatório. Gate C12.1 continua aberto.

### C12.1 — Ativação Fusion Sage de mão e Set

Fusion Sage (1040/P134) já tem executor e o produtor de resposta obrigatória do #161. O App agora consulta a mesma disponibilidade do produtor para mostrar a ativação de mão e admite mão/Set pela fila. CPU admite a fonte legal e responde pelo fluxo existente. O gate de apresentação recebe a projeção da ativação antes do gasto. Testes C# cobrem ambos os lados, mão/Set, alvo inexistente, fila ocupada, projeção e ativação seguida de busca com CPU. Demo.cs 2622 → 2622 linhas. Sem lógica de carta nova ou alteração de cobertura. O gate C12.1 continua aberto.

### C12.1 — Produtores Fissure de mão e Set

Fissure (1001) usa o executor seletivo existente. App admite mínimo único pelo dispatcher e, em empate, mantém a ocorrência da magia e oferece somente os alvos válidos do mesmo grupo. O clique revalida fonte e alvo no Core; Backspace fecha a escolha. CPU registra a primeira ocorrência válida como escolha explícita pelo mesmo produtor. A apresentação reutiliza a projeção de ativação para mostrar a fonte antes do gasto. Sem ampliação de catálogo, alterações de manifesto, mudanças em Demo.cs (2622 → 2622 linhas) ou novos critérios de alvo. #150 cobre outras famílias e deve preservar o produtor ao reconciliar. Testes automatizados cobrem CPU de mão/Set nos dois lados, empate, pose oculta inelegível, admissão, projeção de apresentação e efeito. O gate C12.1 permanece aberto.

A reconciliação preserva a fatia de Fissure já integrada em `b6d7b91`: o mesmo helper de disponibilidade de mão e o produtor CPU conservam Fissure, acrescentando Fusion Sage sem fallback mutante. Novo CI obrigatório; a base antiga de #172 permanece intacta.


### C12.1 — Produtores de magias fixas no App e CPU

O App tinha uma lista de famílias própria e usava `CanActivateFromHand` legado, ocultando executores já existentes para Blue Medicine, Eternal Rest e Giant Trunade. A CPU chamava `Play` diretamente mesmo com fila ativa para magias LP, compra e limpeza S/T. Agora `SupportsQueuedFixedSpell` e `CanQueueFixedSpellActivation` no Core reutilizam validadores dos produtores, incluindo identidade/targets, fonte mão/Set e bloqueio. O App usa a admissão `TryQueueActivation`, sem fallback após rejeição. CPU em modo fila usa os mesmos produtores para LP, compra e limpeza S/T de mão/Set, preservando a heurística anterior (compra deixa uma carta no Deck; limpeza exige vantagem numérica, excluindo a própria fonte Set). Blue Medicine e Giant Trunade já tinham executor; não há efeito novo. Não acrescenta estratégia de uso de wipes de monstros à CPU. A admissão não retorna metadados de movimento legado: apresentação lê a fonte pelo gate A03.

Regressões em CpuDuelChecks: 32 combinações CPU (8 IDs, 2 lados, mão/Set), query sem mutação, busy/face-up, apresentação antes do gasto, LP/compra/retorno/destruição, ausência de alvo/Deck, Raigeki pela admissão App. Demo.cs 2622 → 2622 linhas; resolvedores legados permanecem para o modo legado e famílias não migradas. Sem mudança de manifesto/badges, sem carta nova, sem alteração do default; CI obrigatório. Smoke gráfico local ainda pendente. Gate C12.1 continua aberto; reconciliar #150 antes da migração dos persistentes e restantes produtores.


### C12.1 — Produtores de seleção S/T individual

MST (1079), Remove Trap (1004) e Spiritualism (1539) já possuíam executor tipado. O App agora usa o query de candidatos por ocorrência em mão/Set, incluindo FIELD. A escolha provisória permanece local à UI, sem abrir PendingSpellTrapTarget legado, gastar a fonte ou criar uma segunda fila; bloqueia outros inputs de jogo, cancela por Backspace ou mudança de turno/estado. O clique revalida a ocorrência exata na posição exibida e o produtor revalida a fonte/alvo; rejeição não cai no legado. CPU usa o mesmo produtor e preserva preferência por alvos adversários. A03 recebe snapshot da fonte antes do gasto. Preservadas as restrições do executor, inclusive MST não ativável no turno do Set, Remove Trap face-up e retorno de Spiritualism.

Regressões em CpuDuelChecks: 20 combinações (dois lados, mão/Set, MST/Remove Trap/Spiritualism, FIELD onde legal), gate, origem CPU, ausência de prompt legado, retorno vs destruição e substituição por outra ocorrência de mesmo ID. Demo.cs 2622 → 2622 linhas. Sem cartas novas, alteração de manifesto ou cobertura; fila continua opt-in, legado ainda existe para o modo antigo e outros fluxos. Smoke gráfico pendente. C12.1 permanece aberto.


### C12.1 — Reconciliação de persistentes, P025 e produtores CPU (#150)

Base `15a36f79` (#176 integrado, CI 45656718361 verde). Recuperados os executores, contratos e regressões de #150 sem alterar a branch draft original. As fatias fixas e seleção S/T já integradas (#174–176) permanecem autoritativas; não duplicar seus prompts/testes. FIELD (13 P036 + Fusion Gate), Infinite Cards e Set FIELD passam pelos comandos existentes/recuperados; substituição FIELD envia cada ocorrência separadamente ao GY e revalida a lista antes da ativação. `CommitFieldActivation`, `CommitInfiniteCardsActivation`, `CommitSpellTrapSet` e `CommitEquipmentActivation` compartilham mutações finais com adaptadores sem fila. App usa query de legalidade e gate A03 com destino FIELD correto, inclusive voo Set; o Set stamp de S/T no FIELD é aproximação já documentada em #150, não fidelidade validada.

Equipamentos P025: produtor de mão/Set por ocorrência no App e CPU, incluindo executores dinâmicos já existentes; seleção local guarda candidatos, revalida identidade, permite cancelamento sem custo e não chama legado após rejeição. CPU mantém preferência pelo próprio monstro de maior ATK elegível. CPU em modo fila também admite Normal Summon, monster Set, tributos, Set de armadilhas e mudança manual de posição/Flip Summon pelo mesmo produtor do App. Decisão opcional que fica na fila usa política da CPU independentemente de Ask/Off do humano. O App não repete a apresentação após admissão. Fusion Gate: só ativação, seleção/fusão permanece legada. Não ampliar heurística de outros FIELD/Infinite Cards. Respostas a armadilhas e efeitos de batalha ainda dependem A06.

Regressões recuperadas de #150: 56 combinações de FIELD, substituições, stale e gates, Infinite Cards mão/Set, Set FIELD/S/T, 84 combinações P025 e CPU, Fusion Gate CPU, Normal Summon/Set e Flip/Skelengel nos dois lados. Metadados CPU dos equipamentos ajustados à apresentação única. Demo.cs 2622 → 2615 linhas. Manifesto recebe somente referências de evidência, sem alteração de status/badge. CI novo obrigatório; revisão gráfica local não executada. Gate C12.1 segue aberto: fusão/ritual, decisões de resposta/combate, procedimentos C16 e demais fluxos já jogáveis precisam ser inventariados e reconciliados antes de default.


### C12.1/A06 — Produtores de ataque com alvo

Reconciliação integral de #173 (CI anterior 45621745102 verde) sobre main `d45553f`, preservando produtores de #177. `TryQueueAttack` reúne ataque direto e com alvo nos pilotos existentes, reutilizado por App, CPU e console. O gate diferencia a mira já executada pelo jogador do cue completo de CPU/console. Regressões de App e headless, ambos lados, Classic/Instant, trace e estado final. Não ampliar regras de combate nesta fatia. Fora dos pilotos, `Attack` legado ainda existe; não declarar paridade/default. Demo.cs 2615 → 2615 linhas. Novo CI obrigatório; não altera cobertura/Verified.


### C12.1 — Fusão: Polymerization e Fusion Gate

Base `a975b26` (#178 integrado). A janela existente continua produzindo escolha sem gasto; a validação de receita/material/destino foi extraída de DemoFusion para compartilhar com o comparador legado. A confirmação App/CPU admite PrepareFusionSummon por requisição e ocorrências exatas (incluindo FusionDeck, anexado ao enum sem renumerar zonas). Após a prévia A03, Polymerization gasta a fonte; cada material da mão/campo emite SendToGraveyard ou Banish para Fusion Gate; cada vínculo/LP decorrente tem seu comando e gate; SpecialSummon move o resultado exato do Fusion Deck; FinishFusionSummon abre gatilhos somente depois de toda a fila drenar. Gate próprio/adversário fica ativo, banimento não dispara gatilhos de GY. Materiais não são destruição nem tributo. A fila admite a resposta somente durante a requisição exata; mudanças antes da execução rejeitam sem gasto, alterações externas durante lote reservado faultam em vez de prosseguir com pagamento parcial. Invocação do FusionDeck e banimento de campo não são autorizados fora do lote reservado. CPU preserva escolha de maior ATK e materiais sugeridos, não retorna metadados de animação legados.

Regressões: 12 combinações fonte/lado/posição com resultado duplicado, campo oculto, gate antes de gasto, stale antes/depois de admissão, trace, CPU nos dois lados, gatilho Witch após colocação. Demo.cs 2615 → 2618 (contrato de snapshot V11); DemoFusion conserva comparador legado e compartilha validação. Sem cartas novas, sem alteração de cobertura/default. Não afirma timings nativos nem integração V11; validação gráfica e reconciliar PR #143 pendentes. CI obrigatório. Próximo: ritual/C16, respostas de armadilhas/combate, inventário de bypass restante antes do gate C12.1.

Complemento: primeira CI 45678471983 verde. Caso de Gate adversário/equipamento falhou na fixture (contexto de equipamento omitido); corrigido sem alterar elegibilidade. Incluído contrato `DemoSpecialSummon.FusionMaterials` do #143 em ambos os caminhos, com materiais imutáveis na ordem escolhida e teste por referência no executor. Facilita reconciliação V11 sem depender do resolvedor legado. Ainda não integra a camada Draw/áudio de #143; sem claim de fidelidade. Regressões de comandos nus e vínculo de equipamento separado. Novo CI obrigatório.

### C12.1 — Invocação inerente de C16 pela CPU e pelo console

Base `067aa87` (#179). CPU em modo fila e console App/Lab usam `TryQueueGraveyardBanishSummon` com origens distintas. Cada custo aguarda o próprio gate antes da invocação. SQLite v6 validado pelo hash e fingerprint do EXE. As funções `0059DEF0` e `00599E30` constam nas decompilações, nas funções suplementares e nas janelas de assembly. A heurística CPU é do sandbox. No Lab com DAT local, Dark Necrofear foi invocado em defesa aberta após três banimentos de Kuriboh por ocorrência, na ordem selecionada. `Demo.cs` tem 2618 linhas antes e depois. O comparador legado P252 continua disponível para os testes. Próximas lacunas do gate C12.1 incluem o seletor gráfico C16, rituais e respostas de combate/armadilhas. CI obrigatório.

### C12.1 — Seletor clicável de C16

Base `3b0eaa7` (#180). A ação da mão abre a lista de custos quando a
fila pode admitir o monstro. O clique registra a ocorrência localmente.
Ao completar o custo exato, o App usa o mesmo produtor da CPU e do
console. A lista usa os modos 7–9 de `0040C940`; o branch default de
`0043A060` escolhe `selectlist`. Consultados o SQLite v6 e o
disassembly no EXE de mesmo fingerprint. O adaptador usa o Draw, os recursos e a geometria da lista estável de V11. Posição aberta e cancelamento reutilizam
controles provisórios. O teste App sem janela passa pelo produtor do
clique nos dois lados, em ATK/DEF, com custos de uma a três ocorrências,
desmarcação, cancelamento, bloqueio de mutação na admissão e custo no
trace. Ainda falta comparação pareada do componente e do cursor,
incluindo transições e controles de confirmação originais. Demo.cs
2618 → 2618. O gate C12.1 segue aberto em rituais e respostas de
armadilhas/combate. CI obrigatório.
