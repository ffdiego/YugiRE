# PLANS.md — YugiRE jogável primeiro

> **Meta:** completar a experiência de *Joey the Passion* em C# com regras reutilizáveis e apresentação clássica, usando o original como **oráculo sob demanda**. O código e a evidência já obtidos permanecem úteis.
>
> **Decisão 26/11/2026:** CARDS implementa rapidamente pelo DAT em Joey.Cards; ACTIONS/VISUALS mantêm rigor nativo; AI reproduz decisões em Joey.AI. Validação principal por duelos diferenciais e snapshots de decisões, sem exigir testes por carta.

## Foco atual

**A08/C12 — Joey.Cards e fila única, imediatamente.** Extrair efeitos por fatias, tornar a fila tipada o único caminho em App/CPU/Lab e retirar flag, fallback e resolvedores legados. Não bloquear novas cartas no C12: implementar pelo DAT apenas na fila. Priorizar remoção do legado e laboratório diferencial A09. Quatro trilhas, agente único, maior ganho provável.

<a id="trilhas-de-trabalho-como-despachar-um-agente"></a>

## Como despachar um agente

Quatro trilhas com IDs estáveis: ACTIONS (fila/scheduler, console, traces), CARDS (regras e cobertura), VISUALS (fidelidade visual), AI (decisões CPU). Comandos: `continue o trabalho`, `continue o trabalho em <TRILHA>`, `faça <ID>` ou `/goal <objetivo>`. **Preferência (25/11/2026): um agente único, uma fatia ativa por vez, com objetivo explícito e execução autônoma.** As quatro trilhas são uma organização do backlog, não uma obrigação de paralelizar.

1. Ler Foco atual, Contrato da fila e a trilha pedida, se houver. Respeitar objetivo/ID explícito, gates e dependências; conferir PRs/branches ativos. Na dúvida, escolher a frente e o item Pronto ou Parcial com maior ganho funcional provável por esforço (desbloqueios, cobertura jogável, fidelidade, evidência disponível, risco e custo de validação). Executar dependências mínimas de outras trilhas sem delegação por padrão. CARDS começa por A08/C12 e pode implementar novas famílias imediatamente pela fila; C18 é consulta opcional.
2. Entregar uma fatia vertical pequena (regra → fila → integração jogável → manifesto; sem teste por carta).
3. Atualizar **apenas checkbox ao concluir, Status e Próximo passo** do item no PLANS.md. Relato de fatia (PR, CI, evidência e limites) vai em `docs/plans/<TRILHA>.md` no bloco do ID. Nunca acrescentar relatos a PLANS.md.
4. Achados novos viram IDs no fim da trilha; nunca renumerar ou reutilizar. Revisar bloqueios quando dependências fecharem.
5. Relatório curto: comportamento, testes, limitações, próximo ID sugerido. C12 exige inventário do legado restante e lacunas de cobertura, sem meta de linhas. Em `/goal`, continuar autonomamente após cada checkpoint até alcançar o objetivo ou um limite real; se bloqueado, registrar a pendência e escolher outra fatia útil compatível com o objetivo.

Status: `Pronto`, `Em andamento`, `Bloqueado por X`, `Aguardando evidência`, `Parcial`, `Concluído`. Uma sessão escritora por vez na main; ver Colaboração.

## Badges das quatro frentes

ACTIONS: marcos A concluídos / marcos A numerados (não porcentagem de opcodes); regenerar `python scripts/publish_actions_progress.py` após mudança na trilha. CARDS: Done+Verified / 771 cartas e contagem de Partial separada, `python scripts/publish_playability.py`. VISUALS: Draw implementadas inclusive Partial / Draw identificadas, `python scripts/publish_visual_coverage.py`. Cada métrica tem seu próprio denominador e não é estimativa de esforço restante. O workflow publica os JSONs automaticamente; commitar os gerados atualizados, nunca ajustar percentuais manualmente.

AI: decisões equivalentes / comparáveis, com cobertura e inconclusivos separados; publicar badge somente quando houver corpus e comparador. Duelos equivalentes / comparáveis é métrica global.

## Regras de priorização

1. Priorizar o que o jogador consegue fazer de verdade; fatias verticais pequenas, não número de callbacks. Na ausência de prioridade explícita, maximizar ganho funcional provável por esforço e franquia, privilegiando desbloqueios reutilizáveis e trabalho validável; não alternar frentes só por equilíbrio de badges.
2. CARDS: descrição do DAT local e regras da edição Joey; efeitos por família em Joey.Cards. Sem teste, callback, pesquisa ou documento individual obrigatório. Ghidra sob demanda para divergências. Atualizar manifesto com cobertura e limitações.
3. Done significa implementada e integrada no escopo jogável anunciado, não verificada; Verified exige comparação nativa. Testes por carta e comparadores legados são históricos, fora da execução padrão; não manter por obrigação. CI mantém build, infraestrutura, scheduler, visual e duelos diferenciais. Não apagar evidência.
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
- [ ] **A08 — Extrair `Joey.Cards` e definir `EffectContext`.** Status: `Parcial`. Iniciar imediatamente com regras puras e adaptadores; remover regras de cartas do Core por fatias sem dependência circular. [Detalhes](docs/plans/ACTIONS.md#a08)
- [ ] **A09 — Laboratório diferencial de duelos.** Status: `Parcial`. Trace C# registra cálculo de batalha por ocorrência. Comparar atributos nativos e C# em pares explícitos e validar um duelo real equivalente. [Detalhes](docs/plans/ACTIONS.md#a09)

## Trilha CARDS

> Novos efeitos somente pela fila em Joey.Cards; C12.1 não bloqueia expansão. DAT primeiro, validação diferencial posterior. [Histórico](docs/plans/CARDS.md#histórico-concluído-sem-id)

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
- [ ] **C12 — Eliminar toda resolução legada de duelo.** Status: `Parcial`. Produtores App/CPU/Lab usam fila; próximo: tornar fila intrínseca, apagar mutadores internos/estados legados e retirar acesso histórico. [Detalhes](docs/plans/CARDS.md#c12)
- [x] **C13 — Conferir os 264 Normal com o DAT real.** Status: `Concluído`. 264 Normal validados com DAT real. [Detalhes](docs/plans/CARDS.md#c13)
- [ ] **C14 — Confirmar Sea Serpent `classCode=5` e testar P036 em duelo real.** Status: `Adiado`. Sea Serpent confirmado no DAT; comparação P036 em duelo nativo preservada, sob demanda. [Detalhes](docs/plans/CARDS.md#c14)
- [ ] **C15 — Fatias `Verified`.** Status: `Adiado`. Comparações por família e interações em duelos dirigidos após massa crítica; antecipar divergências materiais. [Detalhes](docs/plans/CARDS.md#c15)
- [ ] **C16 — Seletor de custo de invocação especial por banimento.** Status: `Parcial`. Comparar o seletor do tabuleiro com o original; efeitos pós-invocação restantes depois. Produtores UI/CPU/console já feitos pertencem a C16, não ao C12. [Detalhes](docs/plans/CARDS.md#c16)
- [ ] **C17 — Controle de Dark Necrofear e Prohibition.** Status: `Aguardando evidência`. Dark Necrofear/Prohibition dependem de evidência; não implementar por aproximação. [Detalhes](docs/plans/CARDS.md#c17)

- [ ] **C18 — Triagem de risco por slots de callback e mecânica.** Status: `Parcial`. Aplicar o cruzamento C01 local na revisão semântica por família. [Detalhes](docs/plans/CARDS.md#c18)

<a id="c12--migrar-as-famílias-done-para-a-fila-de-ações"></a>

### C12 — fila única, sem legado

**Objetivo:** limpeza arquitetural, não migração comportamental. Eliminar toda resolução paralela, não apenas o inventário Done original. O gate reduzido C12.1 é histórico. A08 e novas cartas pela fila começam agora.

1. Inventariar App, CPU, Lab/console; retirar modo legado e fallback por operação.
2. Migrar famílias restantes para comandos/continuações, apagar resolvedores antigos e Pending legados; operação não suportada falha explicitamente sem mutação.
3. Extrair regras em Joey.Cards junto às migrações; Core mantém executor, legalidade, estado e scheduler. Não duplicar regra para preservar teste histórico.
4. Aceitar perda temporária de cobertura em vez de manter ou migrar o legado por obrigação. Retirar operações sem produtor, registrar lacunas e reclassificar o manifesto conforme suporte real; CARDS recupera a cobertura depois. Build e CI ativos verdes; sem testes novos por carta.

**C12 fecha quando:** nenhuma entrada de duelo usa resolução legada, nenhuma flag/fallback seleciona esse caminho, não há resolvedor duplicado nem segunda fila de regras; build e CI ativos aprovados. Relatar caminhos restantes.

[Histórico](docs/plans/CARDS.md#c12).

## Trilha VISUALS

> Assembly-first; apresentação das ações ligada à fila, sem confundir Draws independentes com comandos. [Histórico](docs/plans/VISUALS.md#histórico-concluído-sem-id). [Cobertura visual](docs/architecture/VISUAL_COVERAGE.md).

- [ ] **V01 — Instrumentação Frida: tabela de cosseno e log de Draw.** Status: `Parcial`. Validar coleta 34 dos blits da espada no Windows e ampliar para as demais famílias. [Detalhes](docs/plans/VISUALS.md#v01)
- [ ] **V02 — Contrato nativo de renderização da carta.** Status: `Parcial`. Fechar lados, rotação, pivô e posição ATK/DEF. [Detalhes](docs/plans/VISUALS.md#v02)
- [ ] **V03 — Sprites originais no lugar de placeholders.** Status: `Parcial`. Validação DAT passa. Investigar os controles transitórios de seleção e descarte. [Detalhes](docs/plans/VISUALS.md#v03)
- [ ] **V04 — Troca de sprite da espada de ataque.** Status: `Parcial`. Projeção inteira ligada ao ataque declarado; validar pixels, recorte nativo e compositor. [Detalhes](docs/plans/VISUALS.md#v04)
- [ ] **V05 — ATK/DEF em monstros no campo.** Status: `Parcial`. Chave e intensidade RGB565 ligadas ao renderer; validar frames pareados e retorno à base. [Detalhes](docs/plans/VISUALS.md#v05)
- [ ] **V06 — LP flutuante por origem do dano/cura.** Status: `Parcial`. Slot do combate comum ligado à fila. Mapear flags do dano especial, integrar hold e comparar gráficos/cadência. [Detalhes](docs/plans/VISUALS.md#v06)
- [ ] **V07 — Aviso de mudança de jogador/turno.** Status: `Parcial`. Predicado +4 confirmado; rasterizador 0051EBD0 (coleta 25), compositor e integração via A03/A07. [Detalhes](docs/plans/VISUALS.md#v07)
- [ ] **V08 — Ícones e indicadores sobre cartas (`00419B70`, `0041A070`, `0041A3B0`).** Status: `Parcial`. Banco DAT confirmado; semântica A/B, compositor, integração e validação. [Detalhes](docs/plans/VISUALS.md#v08)
- [ ] **V09 — Cursor `Attack` na Battle Phase.** Status: `Parcial`. Elegibilidade conferida na Alpha. Comparar precedência, hotspot e seleção de alvo com o original. [Detalhes](docs/plans/VISUALS.md#v09)
- [ ] **V10 — Cursores `SetAttack`/`SetDefense`.** Status: `Parcial`. Variantes conferidas na Alpha. Comparar code → asset, hotspot e prioridade com o original. [Detalhes](docs/plans/VISUALS.md#v10)
- [ ] **V11 — Janela de seleção e fusão (foco de cobertura).** Status: `Parcial`. Chave RGB565 ligada aos fundos; validar capturas pareadas, ambos os lados, scroll e outras famílias. Validar as duas rotas de fusão. [Detalhes](docs/plans/VISUALS.md#v11)
- [ ] **V12 — Indicador de corrente desativada (`004531C0`).** Status: `Parcial`. Capturar o cenário de Jinzo e confirmar o compositor e os gatilhos de integração à fila. [Detalhes](docs/plans/VISUALS.md#v12)
- [x] **V13 — `DebugOverlayEnabled` na UI.** Status: `Concluído`. [Detalhes](docs/plans/VISUALS.md#v13)
- [ ] **V14 — Auditoria visual da Alpha em 800×600.** Status: `Parcial`. Respostas e descarte auditados. Fechar composição da seleção e revisar sobreposição dos controles provisórios. [Detalhes](docs/plans/VISUALS.md#v14)
- [ ] **V15 — Matriz de comparação por elemento visual.** Status: `Parcial`. Comparações pareadas com executável e log V01. [Detalhes](docs/plans/VISUALS.md#v15)
- [ ] **V16 — Demais Draws do inventário.** Status: `Parcial`. Capturar a saída do resultado e investigar as famílias ainda sem Draw confirmado. [Detalhes](docs/plans/VISUALS.md#v16)
- [ ] **V17 — Cadência nativa de update/draw e blending.** Status: `Parcial`. Comparar o despacho, a entrega do buffer e o compositor da seleção no Windows. Medir a exibição em um display físico. [Detalhes](docs/plans/VISUALS.md#v17)
- [ ] **V18 — Áudio e vozes.** Status: `Parcial`. Leitura PCM e streaming do menu e duelo validados no cliente. Comparar escuta, transições e associação dinâmica no Windows. [Detalhes](docs/plans/VISUALS.md#v18)
- [ ] **V19 — Fidelidade sistemática das demais telas/efeitos.** Status: `Pronto`. Demais telas e efeitos depois de carta/ATK/DEF. [Detalhes](docs/plans/VISUALS.md#v19)
- [ ] **V20 — Banco coin (00457520).** Status: `Parcial`. Integração A03 e comparação; nome do banco não prova RNG. [Detalhes](docs/plans/VISUALS.md#v20)
- [ ] **V21 — Banco dice (00456B90).** Status: `Parcial`. Remap/blend e integração A03. [Detalhes](docs/plans/VISUALS.md#v21)
- [ ] **V22 — Remoção break (00450FD0).** Status: `Parcial`. Break por causa de remoção (efeitos em campo), Explosion para combate; validar DAT, exceções, cauda e compositor. [Detalhes](docs/plans/VISUALS.md#v22)
- [ ] **V23 — Corrente (0041F870).** Status: `Parcial`. Integração à fila e comparação de pixels. [Detalhes](docs/plans/VISUALS.md#v23)
- [ ] **V24 — Destaque contextual affect (0040E260).** Status: `Parcial`. Resolver os ramos restantes de vínculos e integrar o compositor nativo. [Detalhes](docs/plans/VISUALS.md#v24)
- [ ] **V25 — Ativação alternativa (00450AD0).** Status: `Parcial`. Integração A03 e comparação. [Detalhes](docs/plans/VISUALS.md#v25)
- [ ] **V26 — Exclusão (0043DBE0).** Status: `Parcial`. Remap/blend e integração A03; não disparar em todo banimento. [Detalhes](docs/plans/VISUALS.md#v26)
- [ ] **V27 — Layerfont A (00455FF0).** Status: `Parcial`. Compositor e integração em modo nativo. [Detalhes](docs/plans/VISUALS.md#v27)
- [ ] **V28 — Layerfont B (00456360).** Status: `Parcial`. Compositor, dimensões provisórias e integração. [Detalhes](docs/plans/VISUALS.md#v28)
- [ ] **V29 — Ajuda (0045E4A0).** Status: `Parcial`. Banco DAT e resolvedor padrão confirmados. Validar o compositor e ligar os produtores à apresentação. [Detalhes](docs/plans/VISUALS.md#v29)
- [ ] **V30 — Vija / Destiny Board (004584C0).** Status: `Parcial`. Fechar Draw (+4 por Draw; flag final diverge do predicado inclusivo). [Detalhes](docs/plans/VISUALS.md#v30)
- [ ] **V31 — Bust-up de reações do Joey.** Status: `Parcial`. Review reaction07 implementado; validar DAT/capturas, compositor e outros seletores antes de ligar áudio e fila. [Detalhes](docs/plans/VISUALS.md#v31)

## Trilha AI

- [ ] **I01 — Corpus de decisões nativas IA vs IA.** Status: `Pronto`. Capturar estado pré-decisão, escolhas, RNG/contexto e resultado. [Detalhes](docs/plans/AI.md#i01)
- [ ] **I02 — Extrair Joey.AI.** Status: `Pronto`. API legal comum e estado somente leitura, sem fila própria. [Detalhes](docs/plans/AI.md#i02)
- [ ] **I03 — Comparador de decisões.** Status: `Pronto`. Comparar snapshots independentes; medir equivalência, divergências e inconclusivos. [Detalhes](docs/plans/AI.md#i03)
- [ ] **I04 — Validação IA vs IA ponta a ponta.** Status: `Pronto`. Duelos equivalentes, corpus separado de ajuste e validação. [Detalhes](docs/plans/AI.md#i04)

## Arquitetura-alvo (propostas, não funcionalidades já existentes)

- **Fila de ações** no `Joey.Core` (ACTIONS `A02`): comandos tipados, um ativo por vez, gate de apresentação injetável.
- **Cálculo de atributos efetivos** a partir do estado: bases, magia de campo, equipamento, efeitos contínuos e temporários. Evitar aplicar bônus permanentes ao valor base.
- **Efeitos declarativos simples** para dano/cura, compra/descarte, busca, destruição, retorno e mudanças de posição — emitindo comandos.
- **Eventos de duelo/janelas de resposta** (futuros): summon, flip, ataque, dano, destruição, cemitério e transição de fase. Separar gatilho, condição, custo, alvo e resolução.
- **Eventos de apresentação** (A03): `Joey.Core` emite consequências de estado como comandos; `Joey.App` anima sem alterar a regra.
- **Console de duelo** (A04): ponto único para spawnar/mover cartas e testar efeitos, usando as mesmas interfaces das cartas.
- **Testes diferenciais por duelo**: decisões legais, decks/ordem inicial, RNG, trace e checkpoints; fixtures sintéticas para infraestrutura.

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

## Evidência compartilhada — ACTIONS, CARDS, VISUALS e AI

- Antes de solicitar nova coleta nativa, consultar a versão mais recente do SQLite disponível localmente e os bundles/docs já existentes. Para CARDS, efeitos claros continuam podendo partir do DAT sem investigação nativa obrigatória. Para ACTIONS e VISUALS, preservar o rigor e confirmar assembly quando necessário.
- Preferir enriquecer uma **nova cópia do SQLite compartilhado** com funções ausentes, disassembly, decompilações, xrefs, ponteiros, tabelas e metadados recuperáveis; reutilizar exportadores/importadores existentes ou estendê-los com testes quando não suportarem o formato. Não presumir que qualquer ZIP possa ser importado pelo importador visual existente.
- Se faltar acesso ao Windows (execução/Frida/DAT) ou Fedora (Ghidra/projeto de análise), preparar um roteiro executável em `scripts/pending/` com ID da trilha, ambiente, pré-requisitos, comando, evidência procurada, saída privada e critério de encerramento. Registrar no README e seguir para trabalho independente. Não criar pendências para coleta já concluída.
- Após receber a saída, validar fingerprint/proveniência, preservar dados anteriores e conflitos explícitos, executar `PRAGMA integrity_check` e testes do importador; publicar o resumo/auditoria e identificar qual snapshot privado foi produzido. Uma coleta recebida não implica implementação ou Verified. Quando dados dinâmicos/frames não couberem adequadamente no SQLite, guardar artefatos privados e registrar referência/metadados; não descartar os originais.
- O banco é um artefato **privado e compartilhado entre as quatro frentes**, mantido em `scratch/` ou transferido fora do Git. Um único escritor integra cada versão; agentes consultam cópias somente leitura. Nunca versionar SQLite, EXE, DAT, dumps integrais ou traces brutos. Ver [guia SQLite](docs/research/joey-knowledge-sqlite.md) e [fila de coleta](scripts/pending/README.md).

## Colaboração

**Padrão: agente único.** Trabalhar sequencialmente com `/goal` definido, atravessando trilhas quando necessário; preferir commits incrementais validados na main. Não criar agente coordenador nem distribuir trabalho por trilha apenas porque existem quatro backlogs.

**Paralelismo excepcional:** somente quando o ganho esperado superar claramente a coordenação, preferencialmente em pesquisas independentes somente leitura ou tarefas isoladas. Nesse caso, usar uma branch/worktree por trilha e ID (por exemplo, `actions/A01-scheduler`, `cards/C01-taxonomia`, `visuals/V03-assets`). Reservar o item com PR draft ou coordenação explícita; não presumir que uma tarefa está livre só pelo Markdown. ACTIONS tem precedência para contratos compartilhados e alterações estruturais em `DemoDuel`; CARDS pode avançar taxonomia, tabelas e testes isolados, VISUALS modelos/recursos sem alterar regras. Integrações que atravessam trilhas são responsabilidade do agente principal; se houver concorrência excepcional, exigem coordenação. `A02` entrega a infraestrutura, não converte automaticamente todos os caminhos legados: durante A06/C12, retirar adaptadores de execução e não duplicar mutação. Depois de migrado um caminho, nenhuma regra nova pode contornar sua fila.

Uma sessão escritora atualiza e publica em `main` após testes. Outra máquina continua com `git pull --ff-only`. Se houver múltiplos agentes escritores ao mesmo tempo (por exemplo, um em CARDS e outro em VISUALS), isolar branches/worktrees e integrar sequencialmente; itens de ACTIONS (`A02`, `A06`) mexem no núcleo e **não** devem rodar em paralelo com mudanças grandes em `Demo.cs`. Marcar `CARDS.md` de acordo com implementação atual, **não** com intenção, texto de carta ou trace de outra implementação.

V02/V03 complemento de integração Set/Summon: NativePlacementTravel usa centro cosseno, escala linear, pose e face interpoladas (00418B60; largura candidata 00418EF0). Duelo local também usa voo. Manifestos v3 set/summon/sacrifice validados; Set monster amarelo, S/T azul; summon segundo passe usa rainbow, não miniatura rolando. Recorte horizontal/blend/raster ainda candidatos, scratch +AC não portado. Sem alteração de regra/gate. Desktop pendente.
