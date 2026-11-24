# PLANS.md — YugiRE jogável primeiro

> **Meta:** completar a experiência de *Joey the Passion* em C# com regras reutilizáveis e apresentação clássica, usando o original como **oráculo sob demanda**. O código e a evidência já obtidos permanecem úteis.
>
> **Distribuição de esforço (decisão 23/11/2026):** reimplementação **funcional e rápida** das cartas — boa parte das regras é trivial e não merece validação nativa exaustiva. A fidelidade rigorosa se concentra em dois lugares: a **apresentação visual** (VISUALS) e a **fila de ações** do scheduler nativo (ACTIONS), que é a espinha dorsal que liga regras e visual.

## Trilhas de trabalho (como despachar um agente)

O trabalho está dividido em três trilhas. Cada item tem um ID estável (`A03`, `C07`, `V15`).

| Trilha | Prefixo | Escopo | Exemplo de comando |
| --- | --- | --- | --- |
| **ACTIONS** | `A##` | Fila de ações / scheduler no `Joey.Core`, eventos de apresentação, console de duelo, traces comparáveis | "continue o trabalho em ACTIONS" |
| **CARDS** | `C##` | Regras e efeitos das cartas, cobertura de jogabilidade, manifest | "continue o trabalho em CARDS" |
| **VISUALS** | `V##` | Fidelidade visual: sprites, coordenadas, timelines, UI, comparação com o original | "continue o trabalho em VISUALS" |

Comandos aceitos: `continue o trabalho em <TRILHA>` (o agente escolhe o item) ou `faça <ID>` (por exemplo, `faça V07`).

**Protocolo do agente:**

1. Ler esta seção, [Espinha dorsal: a fila de ações](#espinha-dorsal-a-fila-de-ações-scheduler) e a trilha pedida.
2. Escolher o **primeiro item aberto com Status `Pronto`** da trilha (ou o ID citado); um item `Parcial` com próxima fatia executável também pode ser retomado. Respeitar `Depende de`; se estiver bloqueado ou já ocupado, avisar e pegar o próximo item elegível. Antes de editar, conferir PRs/branches ativos e identificar o ID na branch/PR.
3. Cada item é **uma fatia vertical pequena** (regra/contrato → integração → teste → UI quando houver → `CARDS.md`/manifest quando houver). Nada de campanhas de validação antes de uma carta simples.
4. Ao concluir: marcar `[x]`, atualizar o Status, registrar evidência e limitações. Achados novos viram **novos IDs no fim da trilha** — nunca reutilizar nem renumerar IDs (commits e docs citam `V07`, `C12`...).
5. Relatório curto ao final: comportamento entregue, testes executados, limitações reais, próximo ID sugerido.

**Status:** `Pronto` (pode começar), `Em andamento` (agente/branch identificados), `Bloqueado por X`, `Aguardando evidência` (depende de extração nativa em `scripts/pending/`), `Parcial` (existe implementação, faltam critérios), `Concluído` (critérios do item satisfeitos). Um item `Parcial` é elegível se houver uma próxima fatia executável sem dependência bloqueante.

**Colaboração:** uma sessão escritora por vez em `main`; trilhas em paralelo usam branches/worktrees isolados e integram sequencialmente (ver [Colaboração](#colaboração)). ACTIONS é pré-requisito de partes de CARDS e VISUALS; os itens dependentes indicam isso em `Depende de`.

## Badges das três frentes

ACTIONS: marcos A concluídos / marcos A numerados (não porcentagem de opcodes); regenerar `python scripts/publish_actions_progress.py` após mudança na trilha. CARDS: Done+Verified / 771 cartas e contagem de Partial separada, `python scripts/publish_playability.py`. VISUALS: Draw implementadas inclusive Partial / Draw identificadas, `python scripts/publish_visual_coverage.py`. Cada métrica tem seu próprio denominador e não é estimativa de esforço restante. O workflow publica os JSONs automaticamente; commitar os gerados atualizados, nunca ajustar percentuais manualmente.

## Regras de priorização

1. Priorizar o que o jogador consegue **fazer de verdade** no duelo; não medir progresso por número de callbacks mapeados.
2. Entregar fatias verticais pequenas: mecânica + teste + integração de ativação/turno + `CARDS.md`. Quando necessário, UI/feedback mínimo.
3. Para a maioria das cartas, começar pela descrição do DAT local e agrupamentos de perfil, não pela decompilação. Investigar nativamente apenas ambiguidades com consequência observável. **Regras simples não pedem validação excessiva.**
4. Testes de `Joey.Tests` liberam `Done` **no escopo descrito**. Traces nativos permitem `Verified` quando comparem o **mesmo comportamento**, não por mera presença de um opcode.
5. Manter `Joey.Core` independente de Frida e de tempo de animação; não apagar o laboratório legado. Independência de **tempo** não significa independência de **ordem**: a ordem e a granularidade das ações seguem a fila nativa (ver ACTIONS).
6. Para **todos os elementos visuais**, a meta é reproduzir coordenadas, dimensões, orientação, recursos, composição, contadores e duração **do Joey original**, mesmo com código de renderização e scheduler C# diferentes. A análise estática recupera parâmetros; vídeo e capturas comparam frames. Consultar [meta visual em pixels](docs/architecture/visual-fidelity-goal.md). Uma aproximação é uma pendência, não o contrato final.
7. Os recursos de apresentação devem ser lidos do **`data.dat` original em runtime**; caches em memória são permitidos, mas a alpha fiel não deve usar sprites temporários/placeholders como aparência final. As animações devem preservar trocas de variante e quadros intermediários (ex.: espada inteira/espada aparentemente inserida na carta), não apenas posição inicial/final. Falta de recurso ou transição não investigada deve ser documentada e visível para revisão.
8. **Modo visual limpo obrigatório:** o modo fiel parte com overlays de diagnóstico desativados. Quando os overlays estiverem ativos, disponibilizar botão clicável `Ocultar debug`, atalho `F3` e reativação pelo menu; controles essenciais de jogo não desaparecem junto com o debug. Texto provisório, contadores, FPS e botões auxiliares devem estar em camada distinta, sem modificar a geometria/timeline da cena.
9. Revisar sobreposição e legibilidade da Alpha em 800×600 com capturas de modo limpo (sem debug) e modo diagnóstico; verificar HUD, mão, cartas, números, fases, ícones e cliques, sem cobrir sprites nativos por acidente.
10. **Melhorias ficam para depois da validação.** Liberar resolução, animações suaves, snapshots/undo e outras evoluções não entram agora; só depois que o código estiver validado contra o original. Evitar generalizações e arquitetura além do que o item em andamento exige.

## Espinha dorsal: a fila de ações (scheduler)

### Por que importa

No Joey original, **os comandos examinados de mudanças visíveis do duelo passam por uma fila**, com um comando ativo por vez e gates de apresentação. Generalizar esse modelo para as mudanças observáveis é a proposta do C#; a cobertura de todos os caminhos nativos ainda não foi demonstrada. É isso que garante que comprar uma carta, mandar mão → cemitério ou destruir três monstros aconteçam de forma visível e sequencial, na ordem certa e com a cadência do original.

O estado atual do C# **não segue esse modelo**:

- `DemoDuel` (`Demo.cs`, mais de 2.000 linhas) resolve as regras diretamente e bloqueia ações com vários campos de estado pendente (`ActionsBlocked` combina nove condições: sete campos `Pending*`, `GameOver` e um flag de remoção adiada). Existe uma fila apenas para decisões de efeito (`queuedEffectDecisions`), não para ações.
- A apresentação vive em outra fila independente (`PresentationSequence` no `Joey.LookAndFeel`), que "o Core nunca espera".
- Consequência: a ordem e a granularidade das ações podem divergir do original, e cada carta nova implementada fora da fila aumenta a **dívida de migração** (ver `C12`).

### O que a evidência já mostra

Fonte: [`docs/research/native-action-scheduler.md`](docs/research/native-action-scheduler.md) (análise estática; `joey_pc.exe`, base `00400000`). Itens marcados como *hipótese* **não** estão confirmados.

- **A engine de regras roda antes de Update/Draw dos objetos visuais** em cada frame da cena (`0044DD00` → `005C0000` → `005BC700`; Update `0045D040`, Draw `0045D170`). A cena continua atualizando e desenhando enquanto os gates impedem o avanço das regras.
- **Dois gates:** um de apresentação **antes** da fila (`005BC740` → `005BDC40` → `0040CF20`) e outro **dentro** de cada comando ativo (bit `0x4000` do estado do handler, mantido até os objetos visuais ficarem livres — consulta `vtable[+4]` dos objetos da cena, p.ex. `004188B0` enquanto a carta se move).
- **Fila:** buffer de 256 registros de 8 bytes (`00A550A8`). Cada registro: opcode nos 12 bits baixos, lado no bit 15 e três argumentos de 16 bits. Produtor `005B9170`; consumidor `005B9CA0` retira da frente; handler pelas tabelas `005B9AEC`/`005B9838`.
- **No máximo um comando novo por chamada do consumidor**, que roda no máximo uma vez por `005BC700`. Alguns comandos exigem vários ticks.
- **Produção por continuação**, sem recursão: resolução de efeito (`005BC620` → `005BBE40` → `005BB720`) e janelas de decisão (`005BC9B0`) deixam trabalho na fila para outra passagem. Vários registros podem sair de uma mesma jogada.
- **Opcodes já observados:** `CE` invocação normal, `CF` magia colocada, `72`/`1E` brilho de ativação, `43` dano/LP, `57` tributo, `8E` descarte como custo, `B1` (ver o documento para argumentos).
- **Em aberto:** a chamada indireta superior que liga o laço da aplicação (`00410B50`) à instância da cena; o índice da fase exibida ligado às tabelas de turno (`005EFB78/005EFB94`); o momento exato da mutação de estado dentro de cada subestado de comando.

### Invariantes que o C# deve preservar (contrato proposto)

Estes pontos são o contrato de ACTIONS. Itens que dependem de hipótese estão marcados para confirmação em `A01`.

1. Toda mudança de estado **observável** (comprar, invocar, setar, destruir, mão/campo → cemitério, dano/cura, virar, mudar posição) é um **comando tipado** na fila. Regras não movem cartas entre zonas fora da execução de um comando.
2. **Um comando ativo por vez.** O próximo só é retirado depois que o ativo termina (incluindo o gate visual).
3. **Efeito com vários eventos enfileira um comando por evento**, cada um executado isoladamente (ex.: destruir 3 monstros = 3 comandos de destruição em sequência, cada um visível).
4. **Nenhuma ação é invisível.** Não existe "teletransporte" de estado só para economizar animação; perfis Fast/Instant (`Joey.LookAndFeel`) encurtam a duração, mas **não** mudam a ordem nem a quantidade de comandos.
5. **Continuações:** efeitos que dependem do resultado de um comando, ou de uma escolha do jogador, retomam depois, via nova passagem — sem recursão e sem resolver tudo em um passo.
6. **O gate de apresentação é injetável.** O `Joey.Core` define comandos e espera uma liberação; o `Joey.App` libera quando a animação termina; modo headless/IA vs IA libera na hora. **A mesma sequência de comandos nos dois casos.**
7. **Log de comandos comparável com o trace nativo** (opcode, lado, argumentos), para comparar duelos entre C# e Joey.
8. O estado de jogo muda **na execução** do comando, não no enfileiramento (confirmado para CE/CF/8E; LP pode mudar durante o Update nativo, com comando ainda ativo). A generalização é contrato C#, não prova de todos os handlers; granularidade maior exige justificar que nenhum intermediário observável desaparece. Ver [A01](docs/architecture/action-queue.md).

### Itens da trilha ACTIONS

- [x] **A01 — Consolidar o contrato do scheduler.** Status: `Concluído`.
  Transformar `native-action-scheduler.md` em `docs/architecture/action-queue.md`: máquina de estados do comando (estágios 0/1/2), gates, formato do registro, tabela opcode → comando C# (começar por `CE`, `CF`, `72/1E`, `43`, `57`, `8E`, `B1`), lista de invariantes com grau de certeza (`assembly confirmado`/`observado`/`hipótese`). Fechar ou registrar como aberta cada hipótese acima. Sem implementar ainda.
  *Entregue:* [contrato da fila](docs/architecture/action-queue.md), evidência anterior conferida com o SQLite de 22/11, casos de aceitação para A02, mapa inicial de opcodes e lacunas explícitas. Sem implementação; fases seguem em A07 (roteiro 11).
- [x] **A02 — `ActionQueue` e comandos tipados no `Joey.Core`.** Status: `Concluído`.
  Fila FIFO, comando ativo único, subestados, continuações e gate de conclusão injetável (`IPresentationGate`). Comandos iniciais: `Draw`, `Summon`, `Set`, `SendToGraveyard`, `Discard`, `Destroy`, `Damage`, `Heal`, `Flip`, `ChangePosition`. Reaproveitar `queuedEffectDecisions` como caso de continuação por decisão.
  *Pronto quando:* testes de sequência reproduzem as invariantes 1–6 (ordem, um por vez, destruir 3 = 3 comandos, headless x com gate resultam na **mesma** lista de comandos).
  *Entregue:* PR #67 integrado à main (93846a7), CI integral 45318593622 verde. A02.1–A02.5: FIFO/gates/trace, identidade por ocorrência, dez tipos iniciais com adaptadores experimentais, Raigeki e consequências vinculadas, Summon/Set com tributos, Flip/ChangePosition, Morphing Jar e decisão real Skelengel. Critérios 1–6 cobertos por testes de sequência, alvo obsoleto, gate imediato/retardado e decisão. A02 encerra infraestrutura e pilotos; não significa migração integral de cartas, App/CPU, escolhas concorrentes, fases ou paridade nativa. Ver [handoff A02](docs/architecture/action-queue-implementation.md). Migração restante em A03–A08/C12.

- [ ] **A03 — Eventos de apresentação tipados ligados à fila.** Status: `Pronto`.
  O comando ativo expõe a apresentação que precisa terminar; `Joey.LookAndFeel` consome o comando (não uma lista paralela) e sinaliza o gate. Substituir `PresentationSequence` paralela pela leitura dos comandos. Perfis Classic/Fast/Silent/Instant mantêm ordem e contagem.
  *Pronto quando:* a prévia de ativação e uma invocação normal rodam guiadas pela fila, sem tempo no Core.
- [ ] **A04 — Console de duelo no `Joey.App`.** Status: `Pronto`.
  Modo console que **enfileira os mesmos comandos que as cartas usarão**: spawnar carta (mão/campo/deck/cemitério), mover entre zonas, comprar, invocar/setar, destruir, mudar LP, avançar fase, forçar lado. Os nomes devem espelhar o console nativo de `native_hooks` (`draw N [lado]`, `damage N [lado]`, `state`, `diff`) para rodar o **mesmo roteiro** nos dois lados e comparar. Uso principal: implementar e depurar cartas do zero apenas por essas interfaces, com feedback visual imediato.
  **Contrato do console:** jogador, CPU, cartas, testes, scripts e console são produtores da mesma API de comandos tipados do Core. A origem pode mudar a autorização/admissão (jogadas legais vs. operações privilegiadas de laboratório), nunca o executor, os gatilhos, a ordem ou a apresentação de um comando equivalente. `draw`, `destroy`, `damage`, `heal`, `move` etc. são propostas de interface textual; não presumir que todos já existem no console nativo. Resolver a identidade da ocorrência e revalidar na execução; distinguir destruir, descartar e enviar ao cemitério, mesmo quando o destino coincide. `spawn`/forçar fase/lado são comandos explícitos de laboratório, não mutações ocultas nem jogadas legais. Consultas `state`/`diff` são somente leitura. O parser/runner deve ser reutilizável sem raylib (scripts/testes/headless); o App é um frontend opcional. Nunca contornar a fila nem conceder privilégios de debug ao jogador/CPU.
  *Pronto quando:* um roteiro de compra, destruição e LP é executável interativamente e headless, gera os mesmos comandos/eventos/estado final sob as mesmas condições, e cada ação é animada no App. `state`/`diff` comparáveis ao nativo para operações suportadas; testar admissões inválidas e gatilhos normais de destruição.
- [ ] **A05 — Trace de comandos comparável.** Status: `Pronto`.
  Exportar o log da fila C# no mesmo formato dos registros nativos (opcode, lado, argumentos) e um comparador contra capturas do `yugilab`. Começar por invocação normal (já existem fixtures parciais) e dano/cura.
  *Pronto quando:* um roteiro curto (draw, invocar, atacar) gera listas comparáveis e as divergências saem em relatório.
- [ ] **A06 — Quebrar `DemoDuel` em motor + fila.** Status: `Pronto`.
  Mover as regras de `Demo.cs` para comandos e resolvedores pequenos; eliminar o OU de `Pending*` em favor do estado do comando ativo. Fazer **incremental**, uma família por vez, sem regressão nos testes existentes.
  *Pronto quando:* `Demo.cs` não decide ordem de ação; testes atuais passam.
- [ ] **A07 — Turno e fases como comandos/estágios da fila.** Status: `Aguardando evidência`.
  Ligar a fase exibida às tabelas de turno nativas (`005EFB78/005EFB94`) e ao despacho de `005BC620`. Depende de fechar a pendência registrada em [A01](docs/architecture/action-queue.md); roteiro 11 em `scripts/pending/` preparado, ainda não executado.
- [ ] **A08 — Extrair `Joey.Cards` e definir `EffectContext`.** Status: `Pronto`; coordenar a migração com A06/C12, antes de expandir o catálogo por essa nova API.
  Novo assembly com as definições de cartas por resource ID e as famílias de efeito. Dependências: `Joey.Cards` → `Joey.Core`, nunca o contrário; `Joey.App` monta Core + Cards + LookAndFeel. O Core fica com o que vale para qualquer duelo (fases, batalha, invocação, a fila e `EffectContext`); o catálogo e o console (`A04`) criam cartas por resource ID a partir de `Joey.Cards`.
  **`EffectContext`** expõe primitivos pequenos e combináveis (`Destroy(filtro)`, `Draw`, `Damage`, `Heal`, `Choose`, `Move`...), cada um **enfileirando comandos**; não muta o estado diretamente (invariante 1).
  **Açúcar para cartas triviais:** helpers como `DestroyMonsters(Side)` são só açúcar sobre `Destroy(filtro)`. Eles tiram uma foto dos alvos na resolução e enfileiram **um comando `Destroy` por monstro**; um comando cujo alvo já saiu do campo quando chega a vez é descartado. Ex.: Raigeki = `DestroyMonsters(Opponent)`; Dark Hole = `DestroyMonsters(Opponent)` + `DestroyMonsters(Own)`. A **ordem** entre lados e zonas é observável e deve ser conferida no original antes de ser fixada (hipótese até medir).
  **Efeitos com continuação** (escolha de alvo, gatilhos após a destruição, janelas de resposta) se expressam como passos esperados (`IEnumerable<Step>`; **sem snapshots por ora** — o replay vem de `seed + log de entradas`) e retomam em nova passagem da fila, sem recursão.
  Migrar para `Joey.Cards` as regras declarativas existentes (`DirectDamageRules`, equipamentos, campo etc.) junto com `C12`.
  *Pronto quando:* Raigeki, Dark Hole e uma carta com continuação (ex.: escolha + destruição) vivem em `Joey.Cards`, passam pelo console (`A04`) e produzem a mesma lista de comandos com e sem gate visual.

## Trilha CARDS

> Para a maioria das cartas basta o texto do DAT local + a família mecânica. Não esperar captura Windows. Efeitos novos devem **emitir comandos** (ACTIONS) assim que `A02` existir; até lá, seguir `DemoDuel` e registrar a carta em `C12` para migração.

### Concluído (histórico, sem ID)

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

- [x] Lotes 6–14 integrados pelo [PR #62](https://github.com/ffdiego/YugiRE/pull/62), commit `97ec005`: Dream Clown, Bubonic Vermin, Greenkappa, Tornado Bird, Lady Assailant of Flames, Fire Sorcerer, The Immortal of Thunder e Dimensional Warrior Done no sandbox; Royal Command e Dark Necrofear Partial; Prohibition Pending. Baseline histórica: 383/771 e 119/350 Done+Verified, 9 Partial e 0 Verified. Consultar [consolidação](docs/research/card-rules-consolidation-lotes-6-14.md), manifesto atual e `scripts/pending/README.md` (05–10 ainda não coletados).

### Itens abertos

- [ ] **C01 — Taxonomia de efeitos por texto local.** Status: `Parcial` (aguardando execução DAT do roteiro 14).
  Gerar, a partir das descrições do DAT local, a tabela de famílias mecânicas (compra, destruição, FLIP, cemitério, negação, stat boost, busca, retorno, mudança de posição etc.) com marcação de **trivial** (sem janela de resposta) vs. **precisa de fila/resposta**. Sem marcar `Done` por inferência. Esta tabela alimenta `C02`. Classificador inicial em `scripts/card_effect_taxonomy.py` com testes sintéticos; falta gerar e revisar as 771 entradas reais. Roteiro `scripts/pending/14-card-effect-taxonomy.sh`. Não confundir candidato trivial com ausência da fila de A02 ou status Done.
- [ ] **C02 — Lotes triviais por família.** Status: `Bloqueado por C01`.
  Implementar em lote as famílias marcadas como triviais, com primitivos reutilizáveis e tabelas declarativas (um teste de cenário por família + um por ID, como nos 264 Normal), **sem** validação nativa. Atualizar `CARDS.md` e o manifest a cada lote. É o item que mais aumenta o número de cartas jogáveis por unidade de esforço.
- [ ] **C03 — Monstros de efeito restantes (193 Effect).** Status: `Parcial`.
  Além dos já feitos (FLIP, P006, P164 etc.), cobrir gatilhos de campo/cemitério/batalha, efeitos contínuos e de invocação. Agrupar por mecânica, não por ID. Para Joey, priorizar os 109 Effect do pack. Fatia de intenções de dano de batalha P019/P023/P078/P239/P120 em [contrato C03](docs/cards/c03-battle-damage-effects.md), sem mutação nem promoção de cobertura. Integrar após evento de dano de batalha na fila A02/A06; P239/P120 exigem seleção e comandos adicionais.
- [ ] **C04 — Equipamentos `P025`.** Status: `Parcial`.
  Expandir `EquipmentRules`/`DemoDuel` por famílias de filtros e bônus, além dos três recursos modelados (Legendary Sword, Beast Fangs, Sword of Dark Destruction). O perfil `P025` tem 41 callbacks catalogados; **não assumir** que os 41 tenham a mesma regra. Fatia de 18 IDs dos packs com bônus fixos implementada no fluxo de mão existente; ver [contrato C04](docs/cards/c04-fixed-equipment.md). Testes C# registrados, execução pendente. Set e fila: fatia C04.2 com EquipCommand, 21 IDs fixos e 3 dinâmicos no sandbox opt-in, sem novo caminho legado. UI/CPU, supressões e demais efeitos continuam pendentes. Ver [contrato](docs/cards/c04-queued-dynamic-equipment.md). Stim-Pack/Germ Infection: aritmética estática fechada, produtor de avanço temporal não localizado; depende de A07. [Evidência e limites](docs/cards/c04-timed-equipment.md).
- [ ] **C05 — Curas/danos adicionais em tabelas de efeitos.** Status: `Pronto`.
  Sem prometer tamanho de lote antes de confirmar o catálogo original.
- [ ] **C06 — Demais armadilhas e janelas de resposta/correntes.** Status: `Pronto` (armadilhas simples sem corrente podem seguir antes).
  As cinco já feitas reaproveitam janelas de Summon/ataque. Faltam as outras, Special Summon como gatilho e correntes/respostas, **sem fingir que uma fila simples cobre todos os casos**.
- [ ] **C07 — Gatilhos de cemitério/batalha e refinamento de FLIP.** Status: `Parcial`.
  Janelas de resposta, negadores, deck-out nativo (hoje é política simplificada) e mais gatilhos sob demanda. Fatia pura de chegada ao cemitério: P066/P044/P068/P030; [contrato C07](docs/cards/c07-graveyard-arrival-effects.md). Integração depende da causalidade e ocorrência na fila (A02/A06). Roteiro 12 fecha ambiguidades de envio direto e callbacks faltantes; não bloquear outras famílias.
- [ ] **C08 — Ampliar Morphing Jar #2.** Status: `Pronto`.
  Fusion/Extra Deck, tokens, controle de cartas alterado e seleção quando >5 monstros invocáveis. Não declarar paridade nativa até medir callbacks P182 no Windows.
- [ ] **C09 — Rituais (`P269`).** Status: `Pronto`.
  Partir de `docs/evidence/ritual-rules-catalog.json`; executar **pelo menos um** cenário ponta a ponta antes de ampliar.
- [ ] **C10 — Fusões restantes.** Status: `Pronto`.
  Respostas a Special Summon e efeitos próprios de três resultados ainda Partial; conferir restrições da versão Joey.
- [ ] **C11 — Casos emblemáticos.** Status: `Pronto`.
  Exodia, Relinquished, Destiny Board, Scapegoat e condições especiais.
- [ ] **C12 — Migrar famílias Done para a fila de ações.** Status: `Bloqueado por A02/A08`.
  Reexpressar FLIP, seleções, P037/P039/P092, armadilhas e fusões como comandos (invariantes 1–5). Registrar aqui as cartas implementadas fora da fila enquanto `A02` não existir. Dívida inicial inclui as famílias dos lotes 6–14 (ver nota de consolidação abaixo), além das famílias listadas neste item.
  Dívida adicional: 21 equipamentos P025 (C04), fluxo legado de mão a migrar. Fatia C12: dez magias LP já têm admissão e executor opt-in para mão/Set, ainda falta migrar os produtores UI/CPU. [Contrato](docs/cards/c12-queued-lp-spells.md). Pot of Greed e Upstart Goblin também usam admissão opt-in mão/Set, gasto separado e compras individuais; [C12 compra](docs/cards/c12-queued-draw-spells.md). P037/P039/P092: nove magias de destruição de monstros migradas ao executor opt-in mão/Set, com vínculos e gatilhos após lote; [C12 wipe](docs/cards/c12-queued-monster-wipes.md). P094/P141: Duster e Heavy Storm migradas, uma destruição por S/T ou FIELD e limpeza de vínculo; [C12 S/T](docs/cards/c12-queued-spelltrap-wipes.md). MST/Remove Trap: consulta de candidatos e admissão por ocorrências, gasto, alvo primário e cascata Destiny Board; prompt bloqueante/UI/CPU pendentes em A06. [C12 seletivas](docs/cards/c12-queued-targeted-spelltrap.md). Man-Eater Bug, Hane-Hane e Penguin Soldier: resposta de Flip Summon na fila, ReturnToHandCommand e cleanup separado; batalha, prompt UI/CPU e seleção de GY pendentes. [C12 FLIP](docs/cards/c12-queued-monster-flips.md). P018: Magician of Faith e Mask of Darkness com recuperação de ocorrência do GY para mão por comando, resposta manual e finalização após gate; deck/batalha pendentes. [C12 GY](docs/cards/c12-queued-graveyard-recovery.md). P006: Sangan/Witch com ocorrências Deck, MoveToHandCommand e ShuffleDeckCommand após transferência bem-sucedida; [C12 busca](docs/cards/c12-queued-deck-search.md). P164: seis recrutadores com resposta opt-in após batalha legada, SpecialSummonCommand Deck → Field e shuffle somente após sucesso; [C12 recrutamento](docs/cards/c12-queued-elemental-recruiters.md). P168 (Nimble Momonga/Giant Germ) e Bubonic Vermin: seleção de até duas cópias, invocação face-down quando exigida e shuffle único; efeitos de LP de batalha e FLIP durante ataque continuam legados. [C12 mesmo nome](docs/cards/c12-queued-same-name-recruitment.md). P169: Spear Cretin com respostas obrigatórias dos dois donos, SpecialSummonCommand GY → Field e finalização após gate; [C12 ressurreição](docs/cards/c12-queued-graveyard-revival.md). P165 Sonic Bird/Senju: resposta opcional Deck → Hand reutiliza P006, recusa sem RNG. [Contrato](docs/cards/c12-queued-ritual-search.md). P082/P088: Needle Worm e Hiro's Shadow Scout em Flip Summon manual, mill individual ou draw seguido de descartes por ocorrência e finalização Exodia/deck-out. Bistro Butcher/Masked Sorcerer aguardam dano de batalha na fila (A07). [C12 deck flow](docs/cards/c12-queued-flip-deck-flow.md). Greenkappa/Tornado Bird e Four-Starred Ladybug: par S/T/FIELD e wipe de Level 4 em Flip Summon manual; [contrato](docs/cards/c12-queued-flip-field.md). P051/P052: banimento individual de Deck/Hand e dano após pagamento completo em Flip Summon manual; [C12 banish](docs/cards/c12-queued-flip-banish-burn.md). Morphing Jar #2: ReturnToDeckCommand reservado, shuffle por dono e escavação por cota com SpecialSummon/SendToGraveyard individuais; [C12 Jar #2](docs/cards/c12-queued-morphing-jar-2.md). C16: seis procedimentos Partial de invocação inerente por banimento GY, 0059DEF0/00599E30, comando Hand → Field com custo reservado; efeitos pós-invocação e UI/CPU pendentes. [Contrato](docs/cards/c16-queued-graveyard-banish-summon.md). P117/P159/P160 ainda sem resolvedor; coleta direcionada `scripts/pending/16-cards-revival-spells-ghidra.sh` e propriedade/controle/vínculos a coordenar A06/A08. [Lacuna](docs/cards/p117-p159-p160-revival-spells-gap.md).
- [ ] **C13 — Conferir os 264 Normal com o DAT real (Windows).** Status: `Aguardando evidência`.
  `dotnet run --project src/tests/Joey.Tests -- ../passion/data.dat`. Divergências de categoria devem ser tratadas antes de elevar paridade.
- [ ] **C14 — Confirmar Sea Serpent `classCode=5` e testar P036 em duelo real.** Status: `Aguardando evidência`.
  Não promover P036 a `Verified` sem comparação nativa.
- [ ] **C15 — Fatias `Verified`.** Status: `Bloqueado por A05`.
  Uma carta por família comparada com captura do original (mesmo roteiro, mesmos comandos). Baixa prioridade frente a `C02`/`C03`.

- [ ] **C16 — Seletor de custo de invocação especial por banimento.** Status: `Pronto` para contrato e fatia de seleção; coordenar alterações em `DemoDuel` com ACTIONS.
  Reutilizar `GraveyardBanishSummonRules`, seleção genérica e zona de banimento para Dark Necrofear (P252), Soul of Purity and Light, Spirit of Flames, Aqua Spirit, Garuda the Wind Spirit e demais membros confirmados da família. Validar filtro, quantidade, identidade, custo atômico, gates e seleção UI/CPU; não declarar implementados gates ainda desconhecidos. [Contrato P252](docs/research/p252-necrofear-control-contract.md). Se A02/A08 já estiverem disponíveis, usar a API de comandos; caso contrário registrar migração em C12. Controle na End Phase fica separado.
- [ ] **C17 — Controle de Dark Necrofear e Prohibition.** Status: `Aguardando evidência`.
  Pendências distintas: identidade/owner/controller, saída do campo, vínculo e End Phase de P252 (roteiro 10); declaração e efetividade de Prohibition (roteiro 06). Dividir em novos IDs quando houver evidência suficiente. Royal Command permanece Partial; sua cadeia pertence a C06/C07. Não implementar por aproximação.

## Trilha VISUALS

> **Método (decisão 22/11/2026): assembly-first.** O mantenedor aponta diferenças visuais; o agente investiga `joey_pc.exe` antes de escolher sprites, coordenadas, cores ou timing. Capturas são oráculos de validação, não substitutos da análise da rotina. Registrar endereços, ramificações de estado, argumentos, recursos, grau de certeza e cenários de teste; deixar `Pending` quando o assembly não estiver esclarecido. Detalhes: [pendências assembly-first](docs/research/visual-assembly-first-backlog-2026-11-22.md) e [instruções aos agentes](AGENTS.md#política-obrigatória-engenharia-reversa-primeiro-para-apresentação).
>
> **Apresentações de ações do duelo** devem se ligar à fila em `A03`, respeitando os gates e a ordem nativa; nem toda rotina Draw representa um comando (HUD, hover e objetos persistentes podem atualizar/desenhar independentemente). VISUALS pode implementar contratos e modelos isolados antes de A03, sem introduzir uma segunda fonte de ordem de regras.

**Critério de conclusão visual:** fidelidade observável de **posição/tamanho por pixel e tempo por update** em 800×600, cobrindo progressivamente todos os elementos (carta, mão, texto, HUD, LP, fases, efeitos e transições). Não é necessário reproduzir GDI/opcodes/estrutura interna, mas deve-se demonstrar de onde vieram os valores. Conferir o [contrato de fidelidade visual](docs/architecture/visual-fidelity-goal.md) e documentar divergências mensuradas.

### Concluído (histórico, sem ID)

- [x] `Joey.LookAndFeel` independente do Core e do raylib, com perfis Classic/Fast/Silent/Instant, clock não bloqueante, overrides e integração inicial de realce de texto no `Joey.App`.
- [x] Achados estáticos da `analise-visual` consolidados em `Joey.LookAndFeel`: mão/hover, posições espelhadas, update de ativação e explosão, passes/alpha e critérios de despacho, com testes e ligação parcial à raylib.
- [x] Prévia visual de ativação jogável em raylib com os quatro YGAs originais do DAT local, modelo nativo de três passes, clock de protótipo e perfis Classic/Fast/Instant.
- [x] Pausa histórica após a prévia de ativação para priorizar regras; **superada** pela meta atual de alpha com fidelidade visual.
- [x] `scripts/compare_visual_frames.py`: compara PNGs alinhados 800×600 sem redimensionar (diferença, máscara, overlay, métricas por painel). `VisualReviewScreen` registra a ação do mesmo sprite de cursor selecionado. Não implica paridade nativa. Ver [protocolo](docs/research/visual-frame-comparison-2026-11-22.md).

### Itens abertos (ordem sugerida)

- [ ] **V01 — Instrumentação Frida: tabela de cosseno e log de Draw.** Status: `Parcial`.
  Capturador dirigido `scripts/pending/15-visual-cosine-runtime.py` e loader `--native-cosine=` implementados; aguardam captura Windows. F12 distingue tabela aproximada e captura runtime. Log de Draw ainda pendente.
  (a) Capturar `00A52EE8` (512 valores x16.16) **depois** da inicialização em `0055DCF0` — o Ghidra estático não entrega a tabela, e hoje o C# usa `Math.Cos` (`ApproximateCosineTable`, explicitamente não bit-exata). (b) Hookar o primitivo de blit comum e registrar, por frame de duelo, `(rotina Draw, recurso/sprite, x, y, alpha/blend, ângulo/escala, quadro)` para as rotinas do inventário. Isso gera valores de referência **observados** para comparar com os extraídos do assembly e acelera V02–V16. Aproveitar a infraestrutura `native_hooks`/`yugilab`; sem versionar dumps brutos.
  *Pronto quando:* tabela de 512 inteiros disponível em `scratch/` e carregável pelo C# no lugar da aproximação; log de Draw para um duelo curto reproduzível por seed.
- [ ] **V02 — Contrato nativo de renderização da carta.** Status: `Parcial`.
  Ambos os lados, rotação (ataque/defesa e face-up/down), dimensões/pivô, e posição legível do ATK/DEF **acima no adversário e abaixo no próprio**.
- [ ] **V03 — Sprites originais no lugar de placeholders.** Status: `Pronto`.
  Substituir remanescentes do fluxo visual padrão por sprites carregados do DAT local, usando manifests e seleção de variantes por estado; não versionar os assets. Aviso explícito de asset faltante; nada de substituição silenciosa.
- [ ] **V04 — Troca de sprite da espada de ataque.** Status: `Pronto`.
  Reconstituir estaticamente e validar a troca ao atingir a carta (`j\duel\effect\atk_icon.bmp` e `atk_icon2.bmp`): frame/estado de transição, clipping, pivô e camada. Preservar a ilusão de penetração observada no vídeo, sem declarar seus detalhes como comprovados antes da análise. Rotina Draw `004142C0` (Partial).
- [ ] **V05 — ATK/DEF em monstros no campo.** Status: `Parcial`.
  Já ligados: BMPs originais, separador, variantes normal/aumentado/reduzido, `0xA0/0x100`, tween 7/8, gate nativo de `00418EF0 → 004194D0` (movimento `0x100`, saída estado 5, zona, lado, face-up `0x8000`, identidade do ocupante `0056DD30`); renderer e JSON F12 compartilham a visibilidade, evitando revelar atributos da CPU antes do contato da espada. Faltam âncoras de pixels, blending, validação e sprites dos indicadores adicionais (overlays `00419B70/0041A3B0/0041A070` não são números). **Partial, não Verified.** Ver [pesquisa](docs/research/native-field-attributes-2026-11-22.md) ; bundle 03 recebido e absorvido em 23/11, não repetir coleta. Ver [handoff dos bundles](docs/research/visual-contract-bundles-2026-11-23.md).
- [ ] **V06 — LP flutuante por origem do dano/cura.** Status: `Pronto`.
  Remover a escolha universal de ponto médio entre as fileiras. Rastrear `00414110` (combate), `00408470` (caso geral), `0047C4F0` (registro do delta/identificador), `0047C660` (Draw) e `004806F0` (números) até a passagem real de X/Y/slot. Comparar dano de batalha sobre monstro, dano direto/cura sobre a região de magias, ambos os lados, ataque direto e alvo face-down. **Nada de ajustar pela captura sem entender a seleção nativa de âncora.**
- [ ] **V07 — Aviso de mudança de jogador/turno.** Status: `Pronto`.
  Investigar `j/duel/phase_name/nex_e_b*.yga` / `nex_e_r*.yga`, transição e ordem relativas aos banners de fase. Seguir `00480B40`/`00480BC0`, `00480C20`, `00480C60` e seus chamadores antes de implementar texto e timeline no cliente.
- [ ] **V08 — Ícones e indicadores sobre cartas (`00419B70`, `0041A070`, `0041A3B0`).** Status: `Parcial`.
  Bundle 04 recebido: 17/17 funções e 12/12 janelas; `NativeStatusDrawLayout` implementa a geometria de sprites 4..9 (miniatura, quadros do atlas, âncoras A/B, espelhamento tipo/atributo e flashes 10..13); `NativeCardStatusDigits` usa atlas amarelo original. Preservar seletores, comandos `0x5F/0x60/0x69` e countdown 512→0 em 32 updates já isolados. Compositor ordenado e modo `--review-card-status=` usam os divisores do `card.txt` local e os contratos existentes; os dados sintéticos são exclusivos da revisão. **Pending no duelo:** faltam teste com DAT real, semântica A/B, compositor nativo e integração normal. São subpasses da Draw 004194D0 já contabilizada; não somar três novas Draws. [Contrato](docs/research/native-card-status-digits-2026-11-22.md); [pesquisa](docs/research/native-card-status-overlays-2026-11-22.md). Não repetir coleta 04.
- [ ] **V09 — Cursor `Attack` na Battle Phase.** Status: `Parcial`.
  A nuvem de ataque aparece no hover quando `Joey.Core.CanAttack(zone)` autoriza (testes negativos, sem segundo cursor). **Resta validar no dispatcher original** a precedência, o hotspot e os casos de seleção de alvo. Não confundir o cursor com a espada no campo.
- [ ] **V10 — Cursores `SetAttack`/`SetDefense`.** Status: `Pronto`.
  Recuperar condições/alternância nativas de mudança de posição e as variantes `cursor_e_setatk.yga`/`cursor_e_setdef.yga`; não exibir ação ilegal nem assumir que os nomes do DAT provam o fluxo.
- [ ] **V11 — Janela de seleção e fusão (foco de cobertura).** Status: `Parcial`.
  `NativeSelectionWindow` já contém cinco slots/scroll, entrada e saída, índice correto e interpolação confirmados. Bundle 01 recebido: âncora 525, gerador x87 (pi/256 e multiplicação final ainda não medida), Update/Draw das duas fusões, fases, recursos e clocks 4/8 com espera no segundo. **Coleta concluída**; falta tabela C runtime (V01), constantes/tabela de seletores e DAT, integração e validação. As duas Draws `004524F0` e `004877F0` devem permanecer distintas. `NativeFusionTimeline` porta os clocks e fórmulas dos passes principais em modelos separados (ângulo mutado no Draw, espera em 128 na fusão vinculada). A seleção estável `0043B280` agora é Partial no duelo: usa fundo do manifesto, variante por total>5, slots de 64px e scroll de cinco cartas; preserva decisões de busca/fusão no Core. Controles ainda provisórios, sem transição/ícones/scroll nativos completos. Fallback `--legacy-selection`. As duas Draws de fusão continuam Pending. Banco fusion.txt e revisão isolada vinculada com flag explícita e cosseno capturado; F7 Update+Draw, F9 Draw independente. Roteiro 13 pede apenas dados/compositor/liberação ausentes; não inserir pausa no scheduler por hipótese. Ver [contrato](docs/research/native-selection-window-fusion-2026-11-22.md) e [handoff](docs/research/visual-contract-bundles-2026-11-23.md). Não alterar badge só pelo conhecimento.
- [ ] **V12 — Indicador de corrente desativada (`004531C0`).** Status: `Parcial`.
  Bundle 02 concluído: opcode 0x72, gates, descritor, rotação 0x4000, 24 updates (dois ciclos de seis quadros), bit 0x40 e seis texturas inteiras documentados. `NativeDisabledChainTimeline` tem testes sintéticos. **Draw Pending no duelo:** modo de revisão isolada `--review-disabled-chain=` agora carrega as seis entradas de `disable.txt` em runtime, usa descritor/centro/rotação e captura F12. Falta testar com DAT real, confirmar compositor e mapear o gatilho do comando para a fila; não tratar 0x72 como negação genérica. Ver [contrato](docs/research/native-chain-disabled-2026-11-22.md). Não repetir coleta 02.
- [ ] **V13 — `DebugOverlayEnabled` na UI.** Status: `Pronto`.
  Default OFF em modo fiel, botão `Ocultar debug` enquanto ativo, atalho F3 e toggle em menu para restaurar. Centralizar Draw e HitTest do overlay, sem interferir no duelo; testes do estado/toggle.
- [ ] **V14 — Auditoria visual da Alpha em 800×600.** Status: `Pronto`.
  Separar **controles do jogo** de **diagnósticos**; corrigir layout provisório que cobre cartas, sprites e textos; comparar capturas sem debug. A imagem de 21/11/2026 mostra textos e controles sobrepostos ao tabuleiro; ela não determina, isoladamente, quais componentes são debug.
- [ ] **V15 — Matriz de comparação por elemento visual.** Status: `Pronto`.
  Coordenadas/dimensões/ângulos e contagem de updates por estado/quadro-chave contra o Joey original; registrar valores ainda provisórios em vez de arredondar silenciosamente. Usar `compare_visual_frames.py` e o log de `V01`.
- [ ] **V16 — Demais Draws do inventário.** Status: `Aguardando evidência`.
  Seleção nativa (`0043B280`, ver V11), ajuda `0045E4A0`, efeito de estado persistente `0040E260`, quebra/remoção alternativa `00450FD0`, ativação alternativa `00450AD0`, corrente `0041F870`, remoção/exclusão `0043DBE0`, moeda `00457520`, dado `00456B90`, Vija `004584C0`, Layerfont A/B `00455FF0`/`00456360`. Cada rotina vira um item novo (`V20`, `V21`...) quando começar. Estado: [cobertura visual](docs/architecture/VISUAL_COVERAGE.md).
- [ ] **V17 — Cadência nativa de update/draw e blending.** Status: `Aguardando evidência`.
  Confirmar cadência, tabela de cosseno x16.16 (ver `V01`) e pixel blending com capturas Windows; não traduzir contadores estáticos em milissegundos sem medição.
- [ ] **V18 — Áudio e vozes.** Status: `Pronto`.
  Reproduzir vozes/áudio a partir dos arquivos locais do usuário (a opção `--mute-joey` só configura a política; ainda não existe player), preservando a apresentação clássica.
- [ ] **V19 — Fidelidade sistemática das demais telas/efeitos.** Status: `Pronto`.
  Depois da fatia carta/ATK/DEF, sem congelar o progresso funcional da alpha.

## Arquitetura-alvo (propostas, não funcionalidades já existentes)

- **Fila de ações** no `Joey.Core` (ACTIONS `A02`): comandos tipados, um ativo por vez, gate de apresentação injetável.
- **Cálculo de atributos efetivos** a partir do estado: bases, magia de campo, equipamento, efeitos contínuos e temporários. Evitar aplicar bônus permanentes ao valor base.
- **Efeitos declarativos simples** para dano/cura, compra/descarte, busca, destruição, retorno e mudanças de posição — emitindo comandos.
- **Eventos de duelo/janelas de resposta** (futuros): summon, flip, ataque, dano, destruição, cemitério e transição de fase. Separar gatilho, condição, custo, alvo e resolução.
- **Eventos de apresentação** (A03): `Joey.Core` emite consequências de estado como comandos; `Joey.App` anima sem alterar a regra.
- **Console de duelo** (A04): ponto único para spawnar/mover cartas e testar efeitos, usando as mesmas interfaces das cartas.
- **Testes por cenário**: fixtures C# baratos; quando houver divergência real, executar uma captura curta com decks e seed reproduzíveis.

## Como validar com pouco atrito

### Teste local rápido de desenvolvimento

```powershell
dotnet build YugiRE.sln
dotnet run --project src/tests/Joey.Tests
```

### Captura nativa curta de estados (Windows)

Na raiz do repositório, com Python e o Frida da venv já preparados:

```powershell
python scripts/duelos.py
# ou escolha decks específicos:
python scripts/duelos.py .\scratch\batch-A.json .\scratch\batch-B.json --battle-attributes
```

Por padrão o atalho utiliza o original em `../passion/joey_pc.exe`, os decks `native_hooks/decks/demo-A.json` e `demo-B.json`, 3 duelos/1 worker, seed 12345, velocidade 2400 e `--trace-state`. O atalho aceita `--exe`, `--duels`, `--seed`, `--speed-value`, `--output-dir`, `--workers` e `--dry-run`; detecta a venv Frida no Windows.

O limite atual com estado é **até 3 duelos e 1 worker, sem --trace-ai**. Para volume sem state trace:

```powershell
python scripts/duelos.py --duels 10 --workers 2 --no-state
```

A captura nativa usa padrão `deck0_deck1_HHMMSSffffff-YYYYMMDD_v2.jsonl` (horário local, microssegundos para unicidade). `v2` se refere ao `metadata.formatVersion` do envelope JSONL, **não** às versões internas dos schemas de observação. O PID continua em `metadata.processId`. Capturas antigas mantêm seus nomes válidos.

Use `Joey.Lab summarize` ou ferramentas `yugilab` somente quando houver uma pergunta; não exigir uma campanha de captura antes de implementar uma carta simples.

## Evidência compartilhada — ACTIONS, CARDS e VISUALS

- Antes de solicitar nova coleta nativa, consultar a versão mais recente do SQLite disponível localmente e os bundles/docs já existentes. Para CARDS, efeitos claros continuam podendo partir do DAT sem investigação nativa obrigatória. Para ACTIONS e VISUALS, preservar o rigor e confirmar assembly quando necessário.
- Preferir enriquecer uma **nova cópia do SQLite compartilhado** com funções ausentes, disassembly, decompilações, xrefs, ponteiros, tabelas e metadados recuperáveis; reutilizar exportadores/importadores existentes ou estendê-los com testes quando não suportarem o formato. Não presumir que qualquer ZIP possa ser importado pelo importador visual existente.
- Se faltar acesso ao Windows (execução/Frida/DAT) ou Fedora (Ghidra/projeto de análise), preparar um roteiro executável em `scripts/pending/` com ID da trilha, ambiente, pré-requisitos, comando, evidência procurada, saída privada e critério de encerramento. Registrar no README e seguir para trabalho independente. Não criar pendências para coleta já concluída.
- Após receber a saída, validar fingerprint/proveniência, preservar dados anteriores e conflitos explícitos, executar `PRAGMA integrity_check` e testes do importador; publicar o resumo/auditoria e identificar qual snapshot privado foi produzido. Uma coleta recebida não implica implementação ou Verified. Quando dados dinâmicos/frames não couberem adequadamente no SQLite, guardar artefatos privados e registrar referência/metadados; não descartar os originais.
- O banco é um artefato **privado e compartilhado entre as três frentes**, mantido em `scratch/` ou transferido fora do Git. Um único escritor integra cada versão; agentes consultam cópias somente leitura. Nunca versionar SQLite, EXE, DAT, dumps integrais ou traces brutos. Ver [guia SQLite](docs/research/joey-knowledge-sqlite.md) e [fila de coleta](scripts/pending/README.md).

## Colaboração

**Em paralelo:** usar uma branch/worktree por trilha e ID (por exemplo, `actions/A01-scheduler`, `cards/C01-taxonomia`, `visuals/V03-assets`). Reservar o item com PR draft ou coordenação explícita; não presumir que uma tarefa está livre só pelo Markdown. ACTIONS tem precedência para contratos compartilhados e alterações estruturais em `DemoDuel`; CARDS pode avançar taxonomia, tabelas e testes isolados, VISUALS modelos/recursos sem alterar regras. Integrações que atravessam trilhas exigem coordenação, não uma proibição de trabalhar. `A02` entrega a infraestrutura, não converte automaticamente todos os caminhos legados: durante A06/C12, usar adaptadores explícitos e não duplicar mutação. Depois de migrado um caminho, nenhuma regra nova pode contornar sua fila.

Uma sessão escritora atualiza e publica em `main` após testes. Outra máquina continua com `git pull --ff-only`. Se houver múltiplos agentes escritores ao mesmo tempo (por exemplo, um em CARDS e outro em VISUALS), isolar branches/worktrees e integrar sequencialmente; itens de ACTIONS (`A02`, `A06`) mexem no núcleo e **não** devem rodar em paralelo com mudanças grandes em `Demo.cs`. Marcar `CARDS.md` de acordo com implementação atual, **não** com intenção, texto de carta ou trace de outra implementação.
