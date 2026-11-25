# PLANS.md — YugiRE jogável primeiro

> **Meta:** completar a experiência de *Joey the Passion* em C# com regras reutilizáveis e apresentação clássica, usando o original como **oráculo sob demanda**. O código e a evidência já obtidos permanecem úteis.
>
> **Distribuição de esforço (decisão 23/11/2026):** reimplementação **funcional e rápida** das cartas — boa parte das regras é trivial e não merece validação nativa exaustiva. A fidelidade rigorosa se concentra em dois lugares: a **apresentação visual** (VISUALS) e a **fila de ações** do scheduler nativo (ACTIONS), que é a espinha dorsal que liga regras e visual.

## Foco atual

**C12 — migrar as famílias já implementadas para a fila de ações.** CARDS deve suspender novas cartas e lotes C02/C03 até C12 fechar, independentemente do status histórico dos outros itens. Qualquer carta nova, depois de C12, só entra pela fila. ACTIONS e VISUALS mantêm suas trilhas e coordenam dependências. [Critério e ordem](#c12--migrar-as-famílias-done-para-a-fila-de-ações).

<a id="trilhas-de-trabalho-como-despachar-um-agente"></a>

## Como despachar um agente

Três trilhas com IDs estáveis: ACTIONS (fila/scheduler, console, traces), CARDS (regras e cobertura), VISUALS (fidelidade visual). Comandos: `continue o trabalho em <TRILHA>` ou `faça <ID>`.

1. Ler Foco atual, Contrato da fila e a trilha pedida. Escolher primeiro item Pronto ou Parcial executável (ou ID citado), respeitando o foco e dependências; conferir PRs/branches ativos antes de reservar o ID. CARDS começa por C12.
2. Entregar uma fatia vertical pequena (regra → fila → teste → UI se aplicável → CARDS.md/manifest se aplicável).
3. Atualizar **apenas checkbox ao concluir, Status e Próximo passo** do item no PLANS.md. Relato de fatia (PR, CI, evidência e limites) vai em `docs/plans/<TRILHA>.md` no bloco do ID. Nunca acrescentar relatos a PLANS.md.
4. Achados novos viram IDs no fim da trilha; nunca renumerar ou reutilizar. Revisar bloqueios quando dependências fecharem.
5. Relatório curto: comportamento, testes, limitações, próximo ID sugerido. C12 também exige linhas antes/depois e legado restante.

Status: `Pronto`, `Em andamento`, `Bloqueado por X`, `Aguardando evidência`, `Parcial`, `Concluído`. Uma sessão escritora por vez na main; ver Colaboração.

## Badges das três frentes

ACTIONS: marcos A concluídos / marcos A numerados (não porcentagem de opcodes); regenerar `python scripts/publish_actions_progress.py` após mudança na trilha. CARDS: Done+Verified / 771 cartas e contagem de Partial separada, `python scripts/publish_playability.py`. VISUALS: Draw implementadas inclusive Partial / Draw identificadas, `python scripts/publish_visual_coverage.py`. Cada métrica tem seu próprio denominador e não é estimativa de esforço restante. O workflow publica os JSONs automaticamente; commitar os gerados atualizados, nunca ajustar percentuais manualmente.

## Regras de priorização

1. Priorizar o que o jogador consegue fazer de verdade; fatias verticais pequenas, não número de callbacks.
2. Cartas simples: DAT local e família mecânica; análise nativa só para ambiguidades relevantes, sem validação excessiva.
3. Joey.Tests libera Done no escopo descrito; Verified exige comparação do mesmo comportamento com original.
4. Core independente de Frida e tempo de animação, mas ordem e granularidade seguem a fila nativa. Preservar laboratório de RE.
5. Visual: pixels, dimensões, orientação, recursos, composição, contadores e duração originais ([meta](docs/architecture/visual-fidelity-goal.md)); assets DAT em runtime, caches permitidos, sem placeholders finais, preservar quadros intermediários.
6. Debug desligado por padrão; F3/botão/menu; controles essenciais não somem. Revisar 800×600 com e sem debug.
7. VISUALS assembly-first: investigar joey_pc.exe antes de escolher sprites, posições, cores e timing. Capturas validam; não substituem análise. [Backlog](docs/research/visual-assembly-first-backlog-2026-11-22.md).
8. **Melhorias ficam para depois da validação.** Evitar generalizações e arquitetura além da fatia necessária.

<a id="espinha-dorsal-a-fila-de-ações-scheduler"></a>

## Contrato da fila de ações

Contrato e evidência: [action-queue.md](docs/architecture/action-queue.md); contexto integral em [ACTIONS](docs/plans/ACTIONS.md#contexto-espinha-dorsal-da-fila).

Estes pontos são o contrato de ACTIONS. Itens que dependem de hipótese estão marcados para confirmação em `A01`.

1. Toda mudança de estado **observável** (comprar, invocar, setar, destruir, mão/campo → cemitério, dano/cura, virar, mudar posição) é um **comando tipado** na fila. Regras não movem cartas entre zonas fora da execução de um comando.
2. **Um comando ativo por vez.** O próximo só é retirado depois que o ativo termina (incluindo o gate visual).
3. **Efeito com vários eventos enfileira um comando por evento**, cada um executado isoladamente (ex.: destruir 3 monstros = 3 comandos de destruição em sequência, cada um visível).
4. **Nenhuma ação é invisível.** Não existe "teletransporte" de estado só para economizar animação; perfis Fast/Instant (`Joey.LookAndFeel`) encurtam a duração, mas **não** mudam a ordem nem a quantidade de comandos.
5. **Continuações:** efeitos que dependem do resultado de um comando, ou de uma escolha do jogador, retomam depois, via nova passagem — sem recursão e sem resolver tudo em um passo.
6. **O gate de apresentação é injetável.** O `Joey.Core` define comandos e espera uma liberação; o `Joey.App` libera quando a animação termina; modo headless/IA vs IA libera na hora. **A mesma sequência de comandos nos dois casos.**
7. **Log de comandos comparável com o trace nativo** (opcode, lado, argumentos), para comparar duelos entre C# e Joey.
8. O estado de jogo muda **na execução** do comando, não no enfileiramento (confirmado para CE/CF/8E; LP pode mudar durante o Update nativo, com comando ainda ativo). A generalização é contrato C#, não prova de todos os handlers; granularidade maior exige justificar que nenhum intermediário observável desaparece. Ver [A01](docs/architecture/action-queue.md).

**Fronteira VISUALS/ACTIONS:** ACTIONS liga a fila e o gate; VISUALS entrega LookAndFeel independente e Draws sem segunda fila. Nem toda Draw corresponde a comando: HUD, hover e persistentes podem atualizar independentemente. Visível não significa bloqueante; CanComplete é consulta; capturar snapshot de remoção antes da execução; evitar emissão dupla. [Detalhes](docs/plans/ACTIONS.md#fronteira-visuals--actions-a03-decisão-do-mantenedor).

## Trilha ACTIONS

### Itens da trilha ACTIONS

- [x] **A01 — Consolidar o contrato do scheduler.** Status: `Concluído`. Contrato publicado em [action-queue.md](docs/architecture/action-queue.md). [Detalhes](docs/plans/ACTIONS.md#a01)
- [x] **A02 — `ActionQueue` e comandos tipados no `Joey.Core`.** Status: `Concluído`. ActionQueue, dez comandos tipados e identidade por ocorrência; migração restante A06/C12. [Detalhes](docs/plans/ACTIONS.md#a02)
- [x] **A03 — Eventos de apresentação tipados ligados à fila.** Status: `Concluído`. Gate por comando e prévia de ativação/invocação; [contrato](docs/architecture/action-presentation.md). [Detalhes](docs/plans/ACTIONS.md#a03)
- [x] **A04 — Console de duelo no `Joey.App`.** Status: `Concluído`. Console App/Lab compartilhado; [contrato](docs/architecture/duel-console.md). [Detalhes](docs/plans/ACTIONS.md#a04)
- [x] **A05 — Trace de comandos comparável.** Status: `Concluído`. Trace comparável compra → invocação → ataque → dano; [contrato](docs/architecture/native-replay-scenarios.md). [Detalhes](docs/plans/ACTIONS.md#a05)
- [ ] **A06 — Quebrar `DemoDuel` em motor + fila.** Status: `Parcial`. Produtores UI/CPU e remoção de Pending* nas famílias migradas; ampliar combate além dos pilotos isolados, coordenar C12. [Detalhes](docs/plans/ACTIONS.md#a06)
- [ ] **A07 — Turno e fases como comandos/estágios da fila.** Status: `Parcial`. Completar turno/fases na fila e correlacionar dispatcher, bits de fase e rótulos. [Detalhes](docs/plans/ACTIONS.md#a07)
- [ ] **A08 — Extrair `Joey.Cards` e definir `EffectContext`.** Status: `Parcial`. EffectContext no Core; extrair Joey.Cards somente após C12. Mínimas dependências durante a migração. [Detalhes](docs/plans/ACTIONS.md#a08)
- [ ] **A09 — Comparar combate em condição equivalente e apresentar o trace no App.** Status: `Parcial`. Smoke gráfico e captura nativa equivalente; conferir PRs #137/#140 de ataque direto antes de editar. [Detalhes](docs/plans/ACTIONS.md#a09)

## Trilha CARDS

> Novos efeitos somente pela fila; novos lotes suspensos durante C12. [Histórico](docs/plans/CARDS.md#histórico-concluído-sem-id).

- [x] **C01 — Taxonomia de efeitos por texto local.** Status: `Concluído`. Taxonomia local concluída: 264 sem efeito, 433 fila/resposta, 72 revisão, duas candidatas triviais. [Detalhes](docs/plans/CARDS.md#c01)
- [ ] **C02 — Lotes triviais por família.** Status: `Pronto`. Suspenso até fechar C12; preservar lotes já implementados, incluindo Final Destiny e P123. [Detalhes](docs/plans/CARDS.md#c02)
- [ ] **C03 — Monstros de efeito restantes (193 Effect).** Status: `Parcial`. Suspenso para efeitos novos até fechar C12; dano de batalha depende de A06/A07. [Detalhes](docs/plans/CARDS.md#c03)
- [ ] **C04 — Equipamentos `P025`.** Status: `Parcial`. Migrar fluxos legados de mão dos equipamentos P025 (C12); demais efeitos depois. [Detalhes](docs/plans/CARDS.md#c04)
- [ ] **C05 — Curas/danos adicionais em tabelas de efeitos.** Status: `Pronto`. Blue Medicine já na fila; Tremendous Fire após C12. [Detalhes](docs/plans/CARDS.md#c05)
- [ ] **C06 — Demais armadilhas e janelas de resposta/correntes.** Status: `Pronto`. Migrar armadilhas existentes em C12; novas respostas/correntes depois. [Detalhes](docs/plans/CARDS.md#c06)
- [ ] **C07 — Gatilhos de cemitério/batalha e refinamento de FLIP.** Status: `Parcial`. Integrar gatilhos já implementados à fila em C12; novos efeitos depois. [Detalhes](docs/plans/CARDS.md#c07)
- [ ] **C08 — Ampliar Morphing Jar #2.** Status: `Pronto`. Fusion/Extra Deck, tokens, controle e seleção >5; depois de C12. [Detalhes](docs/plans/CARDS.md#c08)
- [ ] **C09 — Rituais (`P269`).** Status: `Parcial`. Produtores UI/CPU de rituais existentes em C12; efeito próprio de Relinquished e restrições depois. [Detalhes](docs/plans/CARDS.md#c09)
- [ ] **C10 — Fusões restantes.** Status: `Pronto`. Confirmar migração das fusões existentes em C12; novos efeitos depois. [Detalhes](docs/plans/CARDS.md#c10)
- [ ] **C11 — Casos emblemáticos.** Status: `Pronto`. Exodia, Relinquished, Destiny Board, Scapegoat e condições especiais; ampliação após C12. [Detalhes](docs/plans/CARDS.md#c11)
- [ ] **C12 — Migrar famílias Done para a fila de ações.** Status: `Parcial`. Foco atual. Validar produtores persistentes e magias fixas; depois Set/Equip e seleções restantes, até fila padrão; ver bloco C12. [Detalhes](docs/plans/CARDS.md#c12)
- [x] **C13 — Conferir os 264 Normal com o DAT real.** Status: `Concluído`. 264 Normal validados com DAT real. [Detalhes](docs/plans/CARDS.md#c13)
- [ ] **C14 — Confirmar Sea Serpent `classCode=5` e testar P036 em duelo real.** Status: `Parcial`. Comparação P036 em duelo nativo pendente. [Detalhes](docs/plans/CARDS.md#c14)
- [ ] **C15 — Fatias `Verified`.** Status: `Aguardando evidência`. Uma carta por família comparada ao original, após migração; baixa prioridade. [Detalhes](docs/plans/CARDS.md#c15)
- [ ] **C16 — Seletor de custo de invocação especial por banimento.** Status: `Pronto`. UI/CPU dos procedimentos existentes em C12; efeitos pós-invocação depois. [Detalhes](docs/plans/CARDS.md#c16)
- [ ] **C17 — Controle de Dark Necrofear e Prohibition.** Status: `Aguardando evidência`. Dark Necrofear/Prohibition dependem de evidência; não implementar por aproximação. [Detalhes](docs/plans/CARDS.md#c17)

### C12 — migrar as famílias Done para a fila de ações

**Objetivo:** eliminar o caminho duplo (`Demo.cs` legado + `DemoActionQueue.cs`), sem alterar a cobertura jogável. O App ainda usa a fila via `--queued-actions` na base desta revisão. A migração não é expansão de catálogo.

**Inventário de partida:** LP (10 magias), compra (Pot of Greed, Upstart Goblin), wipes P037/P039/P092, Duster/Heavy Storm P094/P141, MST/Remove Trap, FLIPs, P018/P006/P164/P168/P169/P165/P082/P088, Greenkappa/Tornado Bird/Four-Starred Ladybug, P051/P052, Morphing Jar #2, procedimentos C16, equipamentos e rituais têm fatias na fila em escopos distintos (ver detalhes e contratos). Há pilotos de combate isolado para Skelengel, Penguin Soldier, Man-Eater Bug e P164 em A06. Não confundir executor/headless com integração completa. Conferir o código antes de declarar migração ou repetir trabalho.

**Pendente (ordem):**
1. Produtores UI/CPU das famílias já migradas e fila como **caminho padrão** do App e da CPU, preservando comportamento e testes; coordenar A06/A07/A08. Migrar a admissão de famílias já jogáveis que ainda não têm executor, conforme necessário para manter paridade ao ativar o padrão. PR #150 está aberto na base desta revisão (FIELD/P036, ativação Fusion Gate e Infinite Cards); conferir merge/CI antes de duplicar.
2. Combate: ampliar FLIPs em batalha e P164 além dos pilotos isolados; P168 (LP de batalha), Bistro Butcher/Masked Sorcerer e demais gatilhos legados; dependências mínimas de A06/A07.
3. Equipamentos P025: remover os 21 fluxos legados de mão e integrar produtores dos equipamentos existentes.
4. Armadilhas de ataque/Summon (Trap Hole, Torrential Tribute, Magic Cylinder, Negate Attack, Mirror Force) e fusões: verificar no código o que já foi migrado antes de implementar; migrar apenas caminhos existentes.
5. P117/P159/P160, Dark Necrofear e Prohibition: inventariar lacunas ([revival](docs/cards/p117-p159-p160-revival-spells-gap.md), C17); não implementar regras novas nem promover status para fechar C12. Pendências sem equivalente jogável ficam nos IDs próprios.

**Pronto quando:** (a) fila padrão no App/CPU; (b) nenhuma família migrada mantém resolução legada equivalente ativa (remover ou marcar obsoleto e impedir seu uso no caminho padrão); (c) `ActionsBlocked` não depende dos `Pending*` legados dessas famílias; (d) testes atuais e regressões passam, CI verde antes da integração e cobertura jogável inalterada; (e) Demo.cs diminui por fatia, com contagem de linhas antes/depois. Se precisar de uma adaptação transitória, justificar e registrar sua remoção; não criar novo resolvedor legado.

Não abrir novos lotes C02/C03 nem implementar cartas novas enquanto C12 estiver aberto. Não ampliar IA nativa, RNG, traces ou visuais como parte de C12; melhorias após validação. Fazer somente a menor fatia necessária de A06/A07/A08 e registrar a dependência; manter EffectContext em Joey.Core até C12 fechar. Relatar migração, legado restante, linhas de Demo.cs, testes e próximo passo.

Histórico e contratos: [C12](docs/plans/CARDS.md#c12).

## Trilha VISUALS

> Assembly-first; apresentação das ações ligada à fila, sem confundir Draws independentes com comandos. [Histórico](docs/plans/VISUALS.md#histórico-concluído-sem-id). [Cobertura visual](docs/architecture/VISUAL_COVERAGE.md).

- [ ] **V01 — Instrumentação Frida: tabela de cosseno e log de Draw.** Status: `Parcial`. Log de blit por frame; tabela de cosseno já capturada. [Detalhes](docs/plans/VISUALS.md#v01)
- [ ] **V02 — Contrato nativo de renderização da carta.** Status: `Parcial`. Fechar lados, rotação, pivô e posição ATK/DEF. [Detalhes](docs/plans/VISUALS.md#v02)
- [ ] **V03 — Sprites originais no lugar de placeholders.** Status: `Parcial`. Controles transitórios de seleção/descarte e validação DAT. [Detalhes](docs/plans/VISUALS.md#v03)
- [ ] **V04 — Troca de sprite da espada de ataque.** Status: `Pronto`. Pivô/compositor e constantes x87 (roteiro 21). [Detalhes](docs/plans/VISUALS.md#v04)
- [ ] **V05 — ATK/DEF em monstros no campo.** Status: `Parcial`. Âncoras em pixels, blending e validação. [Detalhes](docs/plans/VISUALS.md#v05)
- [ ] **V06 — LP flutuante por origem do dano/cura.** Status: `Parcial`. Geometria absoluta e slot de combate via A03. [Detalhes](docs/plans/VISUALS.md#v06)
- [ ] **V07 — Aviso de mudança de jogador/turno.** Status: `Parcial`. Rasterizador 0051EBD0 e predicado virtual +4; compositor/timeline; conferir App atual. [Detalhes](docs/plans/VISUALS.md#v07)
- [ ] **V08 — Ícones e indicadores sobre cartas (`00419B70`, `0041A070`, `0041A3B0`).** Status: `Parcial`. Sprites, blending e validação. [Detalhes](docs/plans/VISUALS.md#v08)
- [ ] **V09 — Cursor `Attack` na Battle Phase.** Status: `Parcial`. Precedência e hotspot no dispatcher. [Detalhes](docs/plans/VISUALS.md#v09)
- [ ] **V10 — Cursores `SetAttack`/`SetDefense`.** Status: `Parcial`. Code → asset/hotspot e integração do seletor. [Detalhes](docs/plans/VISUALS.md#v10)
- [ ] **V11 — Janela de seleção e fusão (foco de cobertura).** Status: `Parcial`. Seletores, overlay, validação; preservar duas Draws de fusão; conferir PR #143. [Detalhes](docs/plans/VISUALS.md#v11)
- [ ] **V12 — Indicador de corrente desativada (`004531C0`).** Status: `Parcial`. DAT, compositor e gatilho pela fila. [Detalhes](docs/plans/VISUALS.md#v12)
- [ ] **V13 — `DebugOverlayEnabled` na UI.** Status: `Parcial`. Comparação ON/OFF e smoke com V14/V15. [Detalhes](docs/plans/VISUALS.md#v13)
- [ ] **V14 — Auditoria visual da Alpha em 800×600.** Status: `Pronto`. Auditar controles e diagnósticos a 800×600. [Detalhes](docs/plans/VISUALS.md#v14)
- [ ] **V15 — Matriz de comparação por elemento visual.** Status: `Parcial`. Comparações pareadas com executável e log V01. [Detalhes](docs/plans/VISUALS.md#v15)
- [ ] **V16 — Demais Draws do inventário.** Status: `Aguardando evidência`. Índice das Draws restantes; V20–V31 têm IDs próprios. [Detalhes](docs/plans/VISUALS.md#v16)
- [ ] **V17 — Cadência nativa de update/draw e blending.** Status: `Parcial`. Fronteiras surface/blend e cadência por medição. [Detalhes](docs/plans/VISUALS.md#v17)
- [ ] **V18 — Áudio e vozes.** Status: `Parcial`. Associações, tabela de descritores e escuta no desktop. [Detalhes](docs/plans/VISUALS.md#v18)
- [ ] **V19 — Fidelidade sistemática das demais telas/efeitos.** Status: `Pronto`. Demais telas e efeitos depois de carta/ATK/DEF. [Detalhes](docs/plans/VISUALS.md#v19)
- [ ] **V20 — Banco coin (00457520).** Status: `Parcial`. Integração A03 e comparação; nome do banco não prova RNG. [Detalhes](docs/plans/VISUALS.md#v20)
- [ ] **V21 — Banco dice (00456B90).** Status: `Parcial`. Remap/blend e integração A03. [Detalhes](docs/plans/VISUALS.md#v21)
- [ ] **V22 — Remoção break (00450FD0).** Status: `Aguardando evidência`. Compositor/projeção break, sem alterar regras. [Detalhes](docs/plans/VISUALS.md#v22)
- [ ] **V23 — Corrente (0041F870).** Status: `Parcial`. Integração à fila e comparação de pixels. [Detalhes](docs/plans/VISUALS.md#v23)
- [ ] **V24 — Destaque contextual affect (0040E260).** Status: `Aguardando evidência`. Scratch, jump tables, manifest e composição; conferir PR #139. [Detalhes](docs/plans/VISUALS.md#v24)
- [ ] **V25 — Ativação alternativa (00450AD0).** Status: `Parcial`. Integração A03 e comparação. [Detalhes](docs/plans/VISUALS.md#v25)
- [ ] **V26 — Exclusão (0043DBE0).** Status: `Parcial`. Remap/blend e integração A03; não disparar em todo banimento. [Detalhes](docs/plans/VISUALS.md#v26)
- [ ] **V27 — Layerfont A (00455FF0).** Status: `Parcial`. Compositor e integração em modo nativo. [Detalhes](docs/plans/VISUALS.md#v27)
- [ ] **V28 — Layerfont B (00456360).** Status: `Parcial`. Compositor, dimensões provisórias e integração. [Detalhes](docs/plans/VISUALS.md#v28)
- [ ] **V29 — Ajuda (0045E4A0).** Status: `Parcial`. Ligar produtores ao Draw real; modelo isolado não conta. [Detalhes](docs/plans/VISUALS.md#v29)
- [ ] **V30 — Vija / Destiny Board (004584C0).** Status: `Parcial`. Fechar Draw (+4 por Draw; flag final diverge do predicado inclusivo). [Detalhes](docs/plans/VISUALS.md#v30)
- [ ] **V31 — Bust-up de reações do Joey.** Status: `Parcial`. Posição, compositor, Update/Draw e gate do bust-up. [Detalhes](docs/plans/VISUALS.md#v31)

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
