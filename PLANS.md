# PLANS.md — YugiRE jogável primeiro

> **Meta:** completar a experiência de *Joey the Passion* em C# com regras reutilizáveis e apresentação clássica, usando o original como **oráculo sob demanda**. O código e a evidência já obtidos permanecem úteis.
>
> **Distribuição de esforço (decisão 23/11/2026):** reimplementação **funcional e rápida** das cartas — boa parte das regras é trivial e não merece validação nativa exaustiva. A fidelidade rigorosa se concentra em dois lugares: a **apresentação visual** (VISUALS) e a **fila de ações** do scheduler nativo (ACTIONS), que é a espinha dorsal que liga regras e visual.

## Foco atual

**C12 — fila de ações como caminho padrão e remoção do legado (gate reduzido, decisão 26/11/2026).** Escopo congelado no inventário Done original; a fila vira padrão para as famílias que já têm produtor, e a remoção efetiva do legado é a métrica das fatias de migração. Cumprido o gate reduzido, C02/C03 reabrem pela fila e a remoção do legado restante segue em fatias. ACTIONS e VISUALS mantêm seus backlogs; o agente único atravessa as dependências necessárias sequencialmente. [Critério e ordem](#c12--migrar-as-famílias-done-para-a-fila-de-ações).

<a id="trilhas-de-trabalho-como-despachar-um-agente"></a>

## Como despachar um agente

Três trilhas com IDs estáveis: ACTIONS (fila/scheduler, console, traces), CARDS (regras e cobertura), VISUALS (fidelidade visual). Comandos: `continue o trabalho`, `continue o trabalho em <TRILHA>`, `faça <ID>` ou `/goal <objetivo>`. **Preferência (25/11/2026): um agente único, uma fatia ativa por vez, com objetivo explícito e execução autônoma.** As três trilhas são uma organização do backlog, não uma obrigação de paralelizar.

1. Ler Foco atual, Contrato da fila e a trilha pedida, se houver. Respeitar objetivo/ID explícito, gates e dependências; conferir PRs/branches ativos. Na dúvida, escolher a frente e o item Pronto ou Parcial com maior ganho funcional provável por esforço (desbloqueios, cobertura jogável, fidelidade, evidência disponível, risco e custo de validação). Executar dependências mínimas de outras trilhas sem delegação por padrão. CARDS começa por C12.1; após seu gate, priorizar famílias de risco baixo/médio (C18) com primitivas disponíveis, sem abandonar o C12 restante.
2. Entregar uma fatia vertical pequena (regra → fila → teste → UI se aplicável → CARDS.md/manifest se aplicável).
3. Atualizar **apenas checkbox ao concluir, Status e Próximo passo** do item no PLANS.md. Relato de fatia (PR, CI, evidência e limites) vai em `docs/plans/<TRILHA>.md` no bloco do ID. Nunca acrescentar relatos a PLANS.md.
4. Achados novos viram IDs no fim da trilha; nunca renumerar ou reutilizar. Revisar bloqueios quando dependências fecharem.
5. Relatório curto: comportamento, testes, limitações, próximo ID sugerido. C12 também exige linhas antes/depois e legado restante. Em `/goal`, continuar autonomamente após cada checkpoint até alcançar o objetivo ou um limite real; se bloqueado, registrar a pendência e escolher outra fatia útil compatível com o objetivo.

Status: `Pronto`, `Em andamento`, `Bloqueado por X`, `Aguardando evidência`, `Parcial`, `Concluído`. Uma sessão escritora por vez na main; ver Colaboração.

## Badges das três frentes

ACTIONS: marcos A concluídos / marcos A numerados (não porcentagem de opcodes); regenerar `python scripts/publish_actions_progress.py` após mudança na trilha. CARDS: Done+Verified / 771 cartas e contagem de Partial separada, `python scripts/publish_playability.py`. VISUALS: Draw implementadas inclusive Partial / Draw identificadas, `python scripts/publish_visual_coverage.py`. Cada métrica tem seu próprio denominador e não é estimativa de esforço restante. O workflow publica os JSONs automaticamente; commitar os gerados atualizados, nunca ajustar percentuais manualmente.

## Regras de priorização

1. Priorizar o que o jogador consegue fazer de verdade; fatias verticais pequenas, não número de callbacks. Na ausência de prioridade explícita, maximizar ganho funcional provável por esforço e franquia, privilegiando desbloqueios reutilizáveis e trabalho validável; não alternar frentes só por equilíbrio de badges.
2. CARDS: texto do DAT local e regras da edição Joey; definições declarativas por família mecânica (records/tabelas C#, sem DSL desnecessária), testes por ID e cenário de família. Triagem C18 orienta o rigor, não impede efeitos claros; análise nativa sob demanda para ambiguidades materiais.
3. Joey.Tests libera Done no escopo completo anunciado; headless isolado não prova jogabilidade UI/CPU, e integração incompleta é Partial no escopo jogável. Verified exige comparação do mesmo comportamento com original. C14/C15 ficam adiados como campanha, salvo divergência material; validar famílias e interações por decks/traces quando houver massa crítica.
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
- [ ] **A06 — Quebrar `DemoDuel` em motor + fila.** Status: `Parcial`. Ampliar o piloto para outros FLIPs, combatentes e contínuas; coordenar C12. [Detalhes](docs/plans/ACTIONS.md#a06)
- [ ] **A07 — Turno e fases como comandos/estágios da fila.** Status: `Parcial`. Completar turno/fases na fila e correlacionar dispatcher, bits de fase e rótulos. [Detalhes](docs/plans/ACTIONS.md#a07)
- [ ] **A08 — Extrair `Joey.Cards` e definir `EffectContext`.** Status: `Parcial`. EffectContext no Core; extrair Joey.Cards somente após C12. Mínimas dependências durante a migração. [Detalhes](docs/plans/ACTIONS.md#a08)
- [ ] **A09 — Comparar combate em condição equivalente e apresentar o trace no App.** Status: `Parcial`. Smoke gráfico e captura nativa equivalente; conferir PRs #137/#140 de ataque direto antes de editar. [Detalhes](docs/plans/ACTIONS.md#a09)

## Trilha CARDS

> Novos efeitos somente pela fila; novos lotes suspensos até o gate C12.1. Depois, DAT primeiro e triagem C18 para famílias independentes. [Histórico](docs/plans/CARDS.md#histórico-concluído-sem-id).

- [x] **C01 — Taxonomia de efeitos por texto local.** Status: `Concluído`. Taxonomia local concluída: 264 sem efeito, 433 fila/resposta, 72 revisão, duas candidatas triviais. [Detalhes](docs/plans/CARDS.md#c01)
- [ ] **C02 — Lotes triviais por família.** Status: `Pronto`. Selecionar famílias independentes de risco baixo/médio (C18) com produtor na fila. [Detalhes](docs/plans/CARDS.md#c02)
- [ ] **C03 — Monstros de efeito restantes (193 Effect).** Status: `Parcial`. Priorizar gatilhos independentes com comandos disponíveis. Dano de batalha depende de A06/A07. [Detalhes](docs/plans/CARDS.md#c03)
- [ ] **C04 — Equipamentos `P025`.** Status: `Parcial`. Migrar fluxos legados de mão dos equipamentos P025 (C12); demais efeitos depois. [Detalhes](docs/plans/CARDS.md#c04)
- [ ] **C05 — Curas/danos adicionais em tabelas de efeitos.** Status: `Parcial`. Ampliar efeitos independentes de LP pela triagem C18; dano por armadilha e FLIP depende do gatilho na fila. [Detalhes](docs/plans/CARDS.md#c05)
- [ ] **C06 — Demais armadilhas e janelas de resposta/correntes.** Status: `Pronto`. Migrar armadilhas existentes em C12; novas respostas/correntes depois. [Detalhes](docs/plans/CARDS.md#c06)
- [ ] **C07 — Gatilhos de cemitério/batalha e refinamento de FLIP.** Status: `Parcial`. Integrar gatilhos já implementados à fila em C12; novos efeitos depois. [Detalhes](docs/plans/CARDS.md#c07)
- [ ] **C08 — Ampliar Morphing Jar #2.** Status: `Pronto`. Fusion/Extra Deck, tokens, controle e seleção >5; depois de C12. [Detalhes](docs/plans/CARDS.md#c08)
- [ ] **C09 — Rituais (`P269`).** Status: `Parcial`. Revisar restrições, interações e efeito próprio de Relinquished após o gate C12.1. Produtores UI/CPU de Ritual já feitos pertencem a C09, não ao C12. [Detalhes](docs/plans/CARDS.md#c09)
- [ ] **C10 — Fusões restantes.** Status: `Pronto`. Confirmar migração das fusões existentes em C12; novos efeitos depois. [Detalhes](docs/plans/CARDS.md#c10)
- [ ] **C11 — Casos emblemáticos.** Status: `Pronto`. Exodia, Relinquished, Destiny Board, Scapegoat e condições especiais; ampliação após C12. [Detalhes](docs/plans/CARDS.md#c11)
- [ ] **C12 — Migrar famílias Done para a fila de ações.** Status: `Parcial`. Após o gate integrado, apagar resolvedores legados equivalentes e substituir comparadores por testes de cenário. [Detalhes](docs/plans/CARDS.md#c12)
- [x] **C13 — Conferir os 264 Normal com o DAT real.** Status: `Concluído`. 264 Normal validados com DAT real. [Detalhes](docs/plans/CARDS.md#c13)
- [ ] **C14 — Confirmar Sea Serpent `classCode=5` e testar P036 em duelo real.** Status: `Adiado`. Sea Serpent confirmado no DAT; comparação P036 em duelo nativo preservada, sob demanda. [Detalhes](docs/plans/CARDS.md#c14)
- [ ] **C15 — Fatias `Verified`.** Status: `Adiado`. Comparações por família e interações em duelos dirigidos após massa crítica; antecipar divergências materiais. [Detalhes](docs/plans/CARDS.md#c15)
- [ ] **C16 — Seletor de custo de invocação especial por banimento.** Status: `Parcial`. Comparar o seletor do tabuleiro com o original; efeitos pós-invocação restantes depois. Produtores UI/CPU/console já feitos pertencem a C16, não ao C12. [Detalhes](docs/plans/CARDS.md#c16)
- [ ] **C17 — Controle de Dark Necrofear e Prohibition.** Status: `Aguardando evidência`. Dark Necrofear/Prohibition dependem de evidência; não implementar por aproximação. [Detalhes](docs/plans/CARDS.md#c17)

- [ ] **C18 — Triagem de risco por slots de callback e mecânica.** Status: `Parcial`. Aplicar o cruzamento C01 local na revisão semântica por família. [Detalhes](docs/plans/CARDS.md#c18)

### C12 — migrar as famílias Done para a fila de ações

**Objetivo:** eliminar o caminho duplo (`Demo.cs` legado + `DemoActionQueue.cs`), sem alterar a cobertura jogável. Não é expansão de catálogo.

**Escopo (congelado em 26/11/2026):** apenas o inventário Done original: LP, compra, wipes (P037/P039/P092), Duster/Heavy Storm, MST/Remove Trap, FLIPs, P018/P006/P164/P168/P169/P165/P082/P088, Greenkappa/Tornado Bird/Four-Starred Ladybug, P051/P052, Morphing Jar #2, equipamentos P025, persistentes, fusões e caminhos de armadilhas de ataque/Summon já jogáveis no inventário original. Produtores de famílias Partial (C16, rituais/C09, Dream Clown e similares) **saem do C12** e seguem em C16, C09 e C07. Preservar as implementações já integradas; não criar produtor novo para família fora do inventário como parte do C12.

**Estado registrado no plano de 26/11/2026:** Demo.cs 2.607 linhas (2.623 no início do C12.1, 18 fatias); fila ≈5.300 linhas (`DemoQueued*` + `DemoActionQueue`) contra ≈2.800 do legado; fila ainda opt-in via `--queued-actions`. Conferir esses números na base de execução antes de medir a próxima fatia.

**Gate C12.1 (reduzido):**
1. Fila como caminho padrão no App e na CPU para as famílias que já têm produtor. Modo legado explícito atrás de flag (`--legacy-actions`); no modo padrão, fallback transitório somente para operação ainda não suportada pela fila, antes de qualquer admissão, reserva, mutação ou emissão de evento. Rejeições por ilegalidade, fila ocupada, seleção stale ou falha após admissão não autorizam fallback. Cada fallback deve ser identificado e registrado com sua remoção prevista; não criar bypass para cartas novas.
2. Testes atuais e CI verde. Regressão descoberta depois vira bug com ID próprio; não reabrir automaticamente o gate. Não integrar código sabidamente quebrado nem suprimir testes.
3. Registrar fechamento com commit/PR, CI e linhas de Demo.cs antes/depois.

Não exigido para o gate: paridade de famílias sem produtor, smoke gráfico, comparação pareada com o original, famílias Partial. Essas validações continuam acionáveis em seus respectivos marcos; o gate reduzido não equivale a C12 concluído ou a Verified.

**Regra de fatia:** progresso da remoção do legado deve apagar resolvedores e reduzir Demo.cs, substituindo comparadores legados por testes de cenário que preservem as regressões relevantes. Fatia necessária que não reduza Demo.cs (incluindo a troca do default) justifica o motivo e registra a dívida restante; redução de linhas não justifica remoção de comportamento ou deslocamento artificial de código. Nenhum resolvedor legado novo.

**Depois do gate:** C02/C03 reabrem para famílias independentes de risco baixo/médio (C18), somente pela fila, alternando com a remoção do legado restante segundo o ganho funcional provável.

**Pendente (ordem):**
1. Virar o padrão (gate acima).
2. Apagar o legado das famílias migradas e seus comparadores; desacoplar `Pending*` de `ActionsBlocked`.
3. Combate: gatilhos legados restantes (FLIPs em batalha, P164 além dos pilotos e demais gatilhos do inventário); mínimo de A06/A07.
4. Equipamentos P025: remover os 21 fluxos legados de mão.
5. Armadilhas de ataque/Summon: verificar no código o que já migrou; migrar só caminhos existentes.

P117/P159/P160, Dark Necrofear e Prohibition ficam fora do C12 (C17 e [lacuna](docs/cards/p117-p159-p160-revival-spells-gap.md)).

**C12 fecha quando:** nenhuma família do inventário mantém resolução legada ativa, `ActionsBlocked` não depende dos `Pending*` dessas famílias, testes e CI verdes, cobertura jogável do inventário preservada e Demo.cs reduzido. Não ampliar IA nativa, RNG, traces ou visuais no C12; manter EffectContext em Joey.Core até fechar.

Histórico e contratos: [C12](docs/plans/CARDS.md#c12).

## Trilha VISUALS

> Assembly-first; apresentação das ações ligada à fila, sem confundir Draws independentes com comandos. [Histórico](docs/plans/VISUALS.md#histórico-concluído-sem-id). [Cobertura visual](docs/architecture/VISUAL_COVERAGE.md).

- [ ] **V01 — Instrumentação Frida: tabela de cosseno e log de Draw.** Status: `Parcial`. Log de blit por frame; tabela de cosseno já capturada. [Detalhes](docs/plans/VISUALS.md#v01)
- [ ] **V02 — Contrato nativo de renderização da carta.** Status: `Parcial`. Fechar lados, rotação, pivô e posição ATK/DEF. [Detalhes](docs/plans/VISUALS.md#v02)
- [ ] **V03 — Sprites originais no lugar de placeholders.** Status: `Parcial`. Controles transitórios de seleção/descarte e validação DAT. [Detalhes](docs/plans/VISUALS.md#v03)
- [ ] **V04 — Troca de sprite da espada de ataque.** Status: `Parcial`. Roteiro 32: fechar constantes x87/__ftol; ligar quadrilátero após confirmar formato/key e compositor; comparar frames. [Detalhes](docs/plans/VISUALS.md#v04)
- [ ] **V05 — ATK/DEF em monstros no campo.** Status: `Parcial`. Centros confirmados (coleta 33); comparar pixels, blending e cadência. [Detalhes](docs/plans/VISUALS.md#v05)
- [ ] **V06 — LP flutuante por origem do dano/cura.** Status: `Parcial`. Centros e trajetória recuperados; slot de combate via A03 e comparação gráfica/cadência. [Detalhes](docs/plans/VISUALS.md#v06)
- [ ] **V07 — Aviso de mudança de jogador/turno.** Status: `Parcial`. Predicado +4 confirmado; rasterizador 0051EBD0 (coleta 25), compositor e integração via A03/A07. [Detalhes](docs/plans/VISUALS.md#v07)
- [ ] **V08 — Ícones e indicadores sobre cartas (`00419B70`, `0041A070`, `0041A3B0`).** Status: `Parcial`. Banco DAT confirmado; semântica A/B, compositor, integração e validação. [Detalhes](docs/plans/VISUALS.md#v08)
- [ ] **V09 — Cursor `Attack` na Battle Phase.** Status: `Parcial`. Manifesto confirmado; precedência e hotspot no dispatcher. [Detalhes](docs/plans/VISUALS.md#v09)
- [ ] **V10 — Cursores `SetAttack`/`SetDefense`.** Status: `Parcial`. Nuvens de posição integradas; validar desktop, code → asset/hotspot e prioridade. [Detalhes](docs/plans/VISUALS.md#v10)
- [ ] **V11 — Janela de seleção e fusão (foco de cobertura).** Status: `Parcial`. Validar composição da fusão geral e investigar o gatilho da fusão vinculada. [Detalhes](docs/plans/VISUALS.md#v11)
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
- [ ] **V24 — Destaque contextual affect (0040E260).** Status: `Parcial`. Resolver os ramos restantes de vínculos e integrar o compositor nativo. [Detalhes](docs/plans/VISUALS.md#v24)
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

**Padrão: agente único.** Trabalhar sequencialmente com `/goal` definido, atravessando trilhas quando necessário; preferir commits incrementais validados na main. Não criar agente coordenador nem distribuir trabalho por trilha apenas porque existem três backlogs.

**Paralelismo excepcional:** somente quando o ganho esperado superar claramente a coordenação, preferencialmente em pesquisas independentes somente leitura ou tarefas isoladas. Nesse caso, usar uma branch/worktree por trilha e ID (por exemplo, `actions/A01-scheduler`, `cards/C01-taxonomia`, `visuals/V03-assets`). Reservar o item com PR draft ou coordenação explícita; não presumir que uma tarefa está livre só pelo Markdown. ACTIONS tem precedência para contratos compartilhados e alterações estruturais em `DemoDuel`; CARDS pode avançar taxonomia, tabelas e testes isolados, VISUALS modelos/recursos sem alterar regras. Integrações que atravessam trilhas são responsabilidade do agente principal; se houver concorrência excepcional, exigem coordenação. `A02` entrega a infraestrutura, não converte automaticamente todos os caminhos legados: durante A06/C12, usar adaptadores explícitos e não duplicar mutação. Depois de migrado um caminho, nenhuma regra nova pode contornar sua fila.

Uma sessão escritora atualiza e publica em `main` após testes. Outra máquina continua com `git pull --ff-only`. Se houver múltiplos agentes escritores ao mesmo tempo (por exemplo, um em CARDS e outro em VISUALS), isolar branches/worktrees e integrar sequencialmente; itens de ACTIONS (`A02`, `A06`) mexem no núcleo e **não** devem rodar em paralelo com mudanças grandes em `Demo.cs`. Marcar `CARDS.md` de acordo com implementação atual, **não** com intenção, texto de carta ou trace de outra implementação.

V02/V03 complemento de integração Set/Summon: NativePlacementTravel usa centro cosseno, escala linear, pose e face interpoladas (00418B60; largura candidata 00418EF0). Duelo local também usa voo. Manifestos v3 set/summon/sacrifice validados; Set monster amarelo, S/T azul; summon segundo passe usa rainbow, não miniatura rolando. Recorte horizontal/blend/raster ainda candidatos, scratch +AC não portado. Sem alteração de regra/gate. Desktop pendente.
