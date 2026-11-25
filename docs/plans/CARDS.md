# CARDS — detalhes dos itens

Histórico e contratos movidos de PLANS.md na revisão de 24/11/2026 (base `42f7813`). [Plano vivo e status atuais](../../PLANS.md). Registrar novos relatos (PR, CI, evidência, limitações) no bloco do item; não duplicar relatos no plano. Os relatos anteriores são históricos, não certificam o estado atual de uma branch.

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

- [x] Lotes 6–14 integrados pelo [PR #62](https://github.com/ffdiego/YugiRE/pull/62), commit `97ec005`: Dream Clown, Bubonic Vermin, Greenkappa, Tornado Bird, Lady Assailant of Flames, Fire Sorcerer, The Immortal of Thunder e Dimensional Warrior Done no sandbox; Royal Command e Dark Necrofear Partial; Prohibition Pending. Baseline histórica: 383/771 e 119/350 Done+Verified, 9 Partial e 0 Verified. Consultar [consolidação](../../docs/research/card-rules-consolidation-lotes-6-14.md), manifesto atual e `scripts/pending/README.md` (05–10 ainda não coletados).

## C01 — Taxonomia de efeitos por texto local

<a id="c01"></a>

Roteiro 14 executado com o DAT local em Fedora/.NET 10. O fingerprint e as 771 entradas conferem com `docs/cards/pack-index.json`. Rotas: 264 sem efeito aplicável, 433 com fila/resposta, 72 para revisão e duas candidatas triviais (uma de compra já Done e uma de cura Pending). A classificação serve para triagem, preserva os status do manifesto e mantém as descrições apenas no pipe privado. [Reprodução e limites](../../scripts/pending/README.md#14--c01-taxonomia-de-efeitos-pelo-dat-local).

## C02 — Lotes triviais por família

<a id="c02"></a>

Usar a triagem de C01 e confirmar a mecânica antes de implementar. Priorizar a candidata de cura Pending com C05 e revisar manualmente os casos sem rota conclusiva antes de ampliar o lote. Usar primitivos reutilizáveis na fila, cenários por família/ID e atualizar `CARDS.md` e o manifesto a cada entrega.
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
1. Produtores UI/CPU das famílias já migradas e fila como **caminho padrão** do App e da CPU, preservando comportamento e testes; coordenar A06/A07/A08. Migrar a admissão de famílias já jogáveis que ainda não têm executor, conforme necessário para manter paridade ao ativar o padrão. PR #150 está aberto na base desta revisão (FIELD/P036, ativação Fusion Gate e Infinite Cards); conferir merge/CI antes de duplicar.
2. Combate: ampliar FLIPs em batalha e P164 além dos pilotos isolados; P168 (LP de batalha), Bistro Butcher/Masked Sorcerer e demais gatilhos legados; dependências mínimas de A06/A07.
3. Equipamentos P025: remover os 21 fluxos legados de mão e integrar produtores dos equipamentos existentes.
4. Armadilhas de ataque/Summon (Trap Hole, Torrential Tribute, Magic Cylinder, Negate Attack, Mirror Force) e fusões: verificar no código o que já foi migrado antes de implementar; migrar apenas caminhos existentes.
5. P117/P159/P160, Dark Necrofear e Prohibition: inventariar lacunas ([revival](../../docs/cards/p117-p159-p160-revival-spells-gap.md), C17); não implementar regras novas nem promover status para fechar C12. Pendências sem equivalente jogável ficam nos IDs próprios.

**Pronto quando:** (a) fila padrão no App/CPU; (b) nenhuma família migrada mantém resolução legada equivalente ativa (remover ou marcar obsoleto e impedir seu uso no caminho padrão); (c) `ActionsBlocked` não depende dos `Pending*` legados dessas famílias; (d) testes atuais e regressões passam, CI verde antes da integração e cobertura jogável inalterada; (e) Demo.cs diminui por fatia, com contagem de linhas antes/depois. Se precisar de uma adaptação transitória, justificar e registrar sua remoção; não criar novo resolvedor legado.

Não abrir novos lotes C02/C03 nem implementar cartas novas enquanto C12 estiver aberto. Não ampliar IA nativa, RNG, traces ou visuais como parte de C12; melhorias após validação. Fazer somente a menor fatia necessária de A06/A07/A08 e registrar a dependência; manter EffectContext em Joey.Core até C12 fechar. Relatar migração, legado restante, linhas de Demo.cs, testes e próximo passo.

### Histórico das fatias

**C12.1 — produtores Set/Normal Summon (PR #150):** Set de FIELD e S/T usa o mesmo `TryQueueSpellTrapSet` na UI; CPU migra Set de armadilhas e Normal Summon/monster Set (incluindo custos já suportados) para a fila, preservando heurísticas. App usa gate único e o destino FIELD correto para viagem/efeito de Set, sem criar timings; extensão da geometria do efeito S/T ao FIELD é aproximação de apresentação, validar com VISUALS. `CommitSpellTrapSet` compartilha a mutação com adaptador legado. `Demo.cs`: 2619 → 2615. Testes Set FIELD/S/T ambos lados e CPU summon/trap Set; CI pendente. Corrigido CS0136 dos testes MST/Remove Trap. Ainda opt-in; restante: posição/FLIP, respostas de summon/attack, seleções/fusão, ataques coordenados com ACTIONS. Não há carta nova.

**C12.1 — produtores MST/Remove Trap (PR #150):** seleção UI local pré-admissão guarda ocorrência de origem e candidatos, bloqueia ações normais, cancela sem custo e confirma pelo mesmo `TryQueueTargetedSpellTrap` usado pela CPU. CPU escolhe o primeiro alvo adversário legal como antes; não abre `PendingSpellTrapTarget` em modo fila. Gate de apresentação reutilizado. Spiritualism continua exclusivo da fila/console (sem ampliação de cobertura). A06 ainda precisa generalizar prompts bloqueantes do motor; este recorte não cria novo resolvedor. Fixture Duster corrigida com Set real em vez de escrita direta no array público. Regressões CPU ambos os lados, ausência de prompt legado e gasto, alvo/cue; CI 45543972092 Python/Node aprovado, .NET falhou por CS0136 (deck duplicado); nome corrigido na próxima fatia. `Demo.cs`: 2619 → 2619 linhas, adaptador sem fila continua disponível; próxima fatia Set de FIELD e demais produtores antes de tornar fila padrão. UI desktop não validada.

**C12.1 — produtores P025 (PR #150):** os 21 equipamentos legados de mão agora passam por `TryQueueEquipment` na UI/CPU quando a fila está habilitada. UI retém ocorrência da origem ao abrir seleção, captura alvo na confirmação; CPU preserva filtro e ordem existentes. `EquipCommand` projeta cue de ativação e App evita repetir animação da CPU. A mutação final de placement/link é compartilhada com adaptador legado, sem expandir whitelist para equipamentos dinâmicos exclusivos da fila nem ativação de Set (não era jogável pela UI). `Demo.cs`: 2620 → 2619 linhas. Regressões das 21 cartas × 2 donos × 2 lados de alvo, origem Cpu e estado final; todas passaram no CI 45543706973, mas suite .NET interrompida em Duster CPU (fixture com carta não registrada). Python/Node aprovado. Fixture corrigida na fatia seguinte, sem relaxar identidade. Mantido opt-in; próximo: Set de FIELD e produtores restantes; não declarar C12 concluído.

**C12.1 — produtores de ativação persistente (PR #150):** UI Hand/Set de FIELD (13 P036 e Fusion Gate) e Infinite Cards usa a fila quando habilitada; CPU migra a decisão existente de ativar Fusion Gate (não a fusão). CPU não escolhia outros FIELD/Infinite Cards, política mantida. Gate reutiliza a apresentação existente e a zona dedicada FIELD, projeção capturada antes da mutação do comando de ativação. O modo sem fila mantém os adaptadores legados, mas a escrita final das duas famílias agora usa os mesmos primitivos de commit da fila; Demo.cs 2622 → 2620 linhas. Testes novos de projeção Hand/Set nos dois lados e ativação CPU Fusion Gate seguida da seleção legada; CI 45543501122: Python/Node aprovado, .NET falhou em CS0136 por variável local cpu duplicada em FusionChecks; nome corrigido na fatia seguinte. Set de FIELD na UI ainda legado (apresentação de Set dedicado não migrada), fusão continua legada, fila ainda opt-in; cobertura/estratégia inalteradas. Próximo: produtores de Set/Equip e seleções restantes, remoção incremental do legado e fila padrão somente após paridade.

**C12.1 — produtores de magias fixas (continuação do PR #150):** CPU mantém os mesmos critérios de seleção para magias LP legadas, compra e limpeza S/T, mas em modo de fila chama `TryQueueActivation(..., ActionOrigin.Cpu)` em vez de `Play`. UI usa o mesmo dispatcher para suas famílias fixas existentes. Rejeição não cai no legado; a animação da CPU passa exclusivamente pelo gate do comando quando a fila está ocupada. Regressão para ambos os lados (Sparks, Pot of Greed, Harpie's Feather Duster), admissão sem mutação, origem Cpu, cue e paridade do estado final; CI 45536690195: Python/Node aprovado, .NET falhou por CS0122 (testes referenciavam comandos internos); corrigido verificando o nome do tipo pela API pública, sem expor comandos. Novo CI pendente. `Demo.cs`: 2622 → 2622 linhas; legado sem fila permanece para compatibilidade, não foi eliminado nesta fatia (remoção depende da fila padrão). Sem expansão da estratégia, do catálogo ou da cobertura. FIELD/Infinite Cards ainda carecem de produtores e apresentação; equip, FLIP/seleções, armadilhas e fusão permanecem no inventário C12.

**C12.1 — PR #150 (em validação):** ativação Hand/Set FIELD para 13 P036 e Fusion Gate (somente ativação, não a fusão), mais Infinite Cards Hand/Set pela fila/console. Substituição FIELD por ocorrência e comando separado; bônus e limite de mão derivados do estado. O primeiro CI (45535853633) falhou em ambos os jobs por CS1628 em `CanActivateInfiniteCards`: captura do parâmetro `out slot` em lambda. Corrigido usando cópia local; CI do commit e9cce3d aprovado (.NET build sem warnings/erros, 51.908 checks; Python/Node offline também verde). Branch reconciliada com main `f1d5cda`, preservando relatos anteriores. Testes de FIELD e Infinite Cards foram adicionados no PR; não executados localmente nesta revisão. `Demo.cs`: 2622 → 2622 linhas (sem alteração; fatia de infraestrutura/admissão apenas, legado ainda ativo). Sem promoção/rebaixamento no manifesto. Próximo passo obrigatório: conectar produtores UI/CPU, desativar resolução equivalente no caminho com fila e só então habilitar fila padrão sem perda de jogabilidade. [Contrato](../../docs/cards/c12-queued-persistent-spells.md).

Reexpressar FLIP, seleções, P037/P039/P092, armadilhas e fusões como comandos (invariantes 1–5). Registrar aqui as cartas implementadas fora da fila enquanto `A02` não existir. Dívida inicial inclui as famílias dos lotes 6–14 (ver nota de consolidação abaixo), além das famílias listadas neste item.
  Dívida adicional: 21 equipamentos P025 (C04), fluxo legado de mão a migrar. Fatia C12: dez magias LP já têm admissão e executor opt-in para mão/Set, ainda falta migrar os produtores UI/CPU. [Contrato](../../docs/cards/c12-queued-lp-spells.md). Pot of Greed e Upstart Goblin também usam admissão opt-in mão/Set, gasto separado e compras individuais; [C12 compra](../../docs/cards/c12-queued-draw-spells.md). P037/P039/P092: nove magias de destruição de monstros migradas ao executor opt-in mão/Set, com vínculos e gatilhos após lote; [C12 wipe](../../docs/cards/c12-queued-monster-wipes.md). P094/P141: Duster e Heavy Storm migradas, uma destruição por S/T ou FIELD e limpeza de vínculo; [C12 S/T](../../docs/cards/c12-queued-spelltrap-wipes.md). MST/Remove Trap: consulta de candidatos e admissão por ocorrências, gasto, alvo primário e cascata Destiny Board; prompt bloqueante/UI/CPU pendentes em A06. [C12 seletivas](../../docs/cards/c12-queued-targeted-spelltrap.md). Man-Eater Bug, Hane-Hane e Penguin Soldier: resposta de Flip Summon na fila, ReturnToHandCommand e cleanup separado; batalha, prompt UI/CPU e seleção de GY pendentes. [C12 FLIP](../../docs/cards/c12-queued-monster-flips.md). P018: Magician of Faith e Mask of Darkness com recuperação de ocorrência do GY para mão por comando, resposta manual e finalização após gate; deck/batalha pendentes. [C12 GY](../../docs/cards/c12-queued-graveyard-recovery.md). P006: Sangan/Witch com ocorrências Deck, MoveToHandCommand e ShuffleDeckCommand após transferência bem-sucedida; [C12 busca](../../docs/cards/c12-queued-deck-search.md). P164: seis recrutadores com resposta opt-in após batalha legada, SpecialSummonCommand Deck → Field e shuffle somente após sucesso; [C12 recrutamento](../../docs/cards/c12-queued-elemental-recruiters.md). P168 (Nimble Momonga/Giant Germ) e Bubonic Vermin: seleção de até duas cópias, invocação face-down quando exigida e shuffle único; efeitos de LP de batalha e FLIP durante ataque continuam legados. [C12 mesmo nome](../../docs/cards/c12-queued-same-name-recruitment.md). P169: Spear Cretin com respostas obrigatórias dos dois donos, SpecialSummonCommand GY → Field e finalização após gate; [C12 ressurreição](../../docs/cards/c12-queued-graveyard-revival.md). P165 Sonic Bird/Senju: resposta opcional Deck → Hand reutiliza P006, recusa sem RNG. [Contrato](../../docs/cards/c12-queued-ritual-search.md). P082/P088: Needle Worm e Hiro's Shadow Scout em Flip Summon manual, mill individual ou draw seguido de descartes por ocorrência e finalização Exodia/deck-out. Bistro Butcher/Masked Sorcerer aguardam dano de batalha na fila (A07). [C12 deck flow](../../docs/cards/c12-queued-flip-deck-flow.md). Greenkappa/Tornado Bird e Four-Starred Ladybug: par S/T/FIELD e wipe de Level 4 em Flip Summon manual; [contrato](../../docs/cards/c12-queued-flip-field.md). P051/P052: banimento individual de Deck/Hand e dano após pagamento completo em Flip Summon manual; [C12 banish](../../docs/cards/c12-queued-flip-banish-burn.md). Morphing Jar #2: ReturnToDeckCommand reservado, shuffle por dono e escavação por cota com SpecialSummon/SendToGraveyard individuais; [C12 Jar #2](../../docs/cards/c12-queued-morphing-jar-2.md). C16: seis procedimentos Partial de invocação inerente por banimento GY, 0059DEF0/00599E30, comando Hand → Field com custo reservado; efeitos pós-invocação e UI/CPU pendentes. [Contrato](../../docs/cards/c16-queued-graveyard-banish-summon.md). 1515/1516/1518: modificadores de batalha nativos em 0056DD30, invocação por fila e combate testado; Done no escopo headless após CI. [C16 atributos](../../docs/cards/c16-elemental-battle-attributes.md). P117/P159/P160 ainda sem resolvedor; coleta direcionada `scripts/pending/16-cards-revival-spells-ghidra.sh` e propriedade/controle/vínculos a coordenar A06/A08. [Lacuna](../../docs/cards/p117-p159-p160-revival-spells-gap.md).

## C13 — Conferir os 264 Normal com o DAT real

<a id="c13"></a>

`dotnet run --project src/tests/Joey.Tests -- ../passion/data.dat` validou em Fedora/.NET 10 os 264 IDs e seus cenários de invocação, tributo, Set e ataque no sandbox. A execução usou o fingerprint do índice de packs versionado.

## C14 — Confirmar Sea Serpent `classCode=5` e testar P036 em duelo real

<a id="c14"></a>

DAT local e teste opcional confirmam o código de classe para dois IDs dos packs. A comparação de P036 em duelo nativo permanece pendente. [Evidências](../../docs/cards/p036-field-spells-sandbox.md).

## C15 — Fatias `Verified`

<a id="c15"></a>

Uma carta por família comparada com captura do original (mesmo roteiro, mesmos comandos). Baixa prioridade frente a `C02`/`C03`.

## C16 — Seletor de custo de invocação especial por banimento

<a id="c16"></a>

Reutilizar `GraveyardBanishSummonRules`, seleção genérica e zona de banimento para Dark Necrofear (P252), Soul of Purity and Light, Spirit of Flames, Aqua Spirit, Garuda the Wind Spirit e demais membros confirmados da família. Validar filtro, quantidade, identidade, custo atômico, gates e seleção UI/CPU; não declarar implementados gates ainda desconhecidos. [Contrato P252](../../docs/research/p252-necrofear-control-contract.md). Se A02/A08 já estiverem disponíveis, usar a API de comandos; caso contrário registrar migração em C12. Controle na End Phase fica separado. P253: contrato puro de oportunidade Aqua (Standby adversária) / Garuda (End adversária), produtores 005A76E0/005A4990, callback compartilhado ausente; [contrato](../../docs/cards/c16-elemental-position-opportunities.md). Roteiro 17 coleta complementar ao 11; integração depende A06/A07/A08. Ambos Partial.

## C17 — Controle de Dark Necrofear e Prohibition

<a id="c17"></a>

Pendências distintas: identidade/owner/controller, saída do campo, vínculo e End Phase de P252 (roteiro 10); declaração e efetividade de Prohibition (roteiro 06). Dividir em novos IDs quando houver evidência suficiente. Royal Command permanece Partial; sua cadeia pertence a C06/C07. Não implementar por aproximação.
