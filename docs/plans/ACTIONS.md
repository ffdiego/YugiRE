# ACTIONS — detalhes dos itens

Histórico e contratos movidos de PLANS.md na revisão de 24/11/2026 (base `42f7813`). [Plano vivo e status atuais](../../PLANS.md). Registrar novos relatos (PR, CI, evidência, limitações) no bloco do item; não duplicar relatos no plano. Os relatos anteriores são históricos, não certificam o estado atual de uma branch.

> **Política vigente (26/11/2026):** A08 imediatamente; C12 elimina toda execução legada (flags, fallback, resolvedores). A05/A09 evoluem para equivalência de duelos com mesmas decisões e estado/RNG inicial. Comparar argumentos e estado além da ordem; gaps são inconclusivos. Critérios históricos de C12/A08 abaixo não bloqueiam a nova política.

Primeira fatia A08/C12 (26/11): definições fixas de LP/compra extraídas em Joey.Cards, adaptadores no Core. App gráfico e Alpha headless sempre criam a fila tipada; opção --legacy-actions rejeitada. Fallbacks por operação e chamadas diretas ainda existem, portanto C12 permanece aberto. CI necessário. Decisão adicional: perda temporária de cobertura é aceitável; retirar fallback sem exigir migração de cada carta. Ataques em duelo com fila agora rejeitam configuração não suportada, sem execução direta. O modo sem fila da CPU ainda precisa ser retirado.

## Contexto: espinha dorsal da fila

### Por que importa

No Joey original, **os comandos examinados de mudanças visíveis do duelo passam por uma fila**, com um comando ativo por vez e gates de apresentação. Generalizar esse modelo para as mudanças observáveis é a proposta do C#; a cobertura de todos os caminhos nativos ainda não foi demonstrada. É isso que garante que comprar uma carta, mandar mão → cemitério ou destruir três monstros aconteçam de forma visível e sequencial, na ordem certa e com a cadência do original.

O estado atual do C# **não segue esse modelo**:

- `DemoDuel` (`Demo.cs`, mais de 2.000 linhas) resolve as regras diretamente e bloqueia ações com vários campos de estado pendente (`ActionsBlocked` combina nove condições: sete campos `Pending*`, `GameOver` e um flag de remoção adiada). Existe uma fila apenas para decisões de efeito (`queuedEffectDecisions`), não para ações.
- A apresentação vive em outra fila independente (`PresentationSequence` no `Joey.LookAndFeel`), que "o Core nunca espera".
- Consequência: a ordem e a granularidade das ações podem divergir do original, e cada carta nova implementada fora da fila aumenta a **dívida de migração** (ver `C12`).

### O que a evidência já mostra

Fonte: [`docs/research/native-action-scheduler.md`](../../docs/research/native-action-scheduler.md) (análise estática; `joey_pc.exe`, base `00400000`). Itens marcados como *hipótese* **não** estão confirmados.

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
8. O estado de jogo muda **na execução** do comando, não no enfileiramento (confirmado para CE/CF/8E; LP pode mudar durante o Update nativo, com comando ainda ativo). A generalização é contrato C#, não prova de todos os handlers; granularidade maior exige justificar que nenhum intermediário observável desaparece. Ver [A01](../../docs/architecture/action-queue.md).

## A01 — Consolidar o contrato do scheduler

<a id="a01"></a>

Transformar `native-action-scheduler.md` em `docs/architecture/action-queue.md`: máquina de estados do comando (estágios 0/1/2), gates, formato do registro, tabela opcode → comando C# (começar por `CE`, `CF`, `72/1E`, `43`, `57`, `8E`, `B1`), lista de invariantes com grau de certeza (`assembly confirmado`/`observado`/`hipótese`). Fechar ou registrar como aberta cada hipótese acima. Sem implementar ainda.
  *Entregue:* [contrato da fila](../../docs/architecture/action-queue.md), evidência anterior conferida com o SQLite de 22/11, casos de aceitação para A02, mapa inicial de opcodes e lacunas explícitas. Sem implementação; fases seguem em A07 (roteiro 11).

## A02 — `ActionQueue` e comandos tipados no `Joey.Core`

<a id="a02"></a>

Fila FIFO, comando ativo único, subestados, continuações e gate de conclusão injetável (`IPresentationGate`). Comandos iniciais: `Draw`, `Summon`, `Set`, `SendToGraveyard`, `Discard`, `Destroy`, `Damage`, `Heal`, `Flip`, `ChangePosition`. Reaproveitar `queuedEffectDecisions` como caso de continuação por decisão.
  *Pronto quando:* testes de sequência reproduzem as invariantes 1–6 (ordem, um por vez, destruir 3 = 3 comandos, headless x com gate resultam na **mesma** lista de comandos).
  *Entregue:* PR #67 integrado à main (93846a7), CI integral 45318593622 verde. A02.1–A02.5: FIFO/gates/trace, identidade por ocorrência, dez tipos iniciais com adaptadores experimentais, Raigeki e consequências vinculadas, Summon/Set com tributos, Flip/ChangePosition, Morphing Jar e decisão real Skelengel. Critérios 1–6 cobertos por testes de sequência, alvo obsoleto, gate imediato/retardado e decisão. A02 encerra infraestrutura e pilotos; não significa migração integral de cartas, App/CPU, escolhas concorrentes, fases ou paridade nativa. Ver [handoff A02](../../docs/architecture/action-queue-implementation.md). Migração restante em A03–A08/C12.

## A03 — Eventos de apresentação tipados ligados à fila

<a id="a03"></a>

O comando ativo expõe a apresentação que precisa terminar; `Joey.LookAndFeel` consome o comando (não uma lista paralela) e sinaliza o gate. Substituir `PresentationSequence` paralela pela leitura dos comandos. Perfis Classic/Fast/Silent/Instant mantêm ordem e contagem.
  *Pronto quando:* a prévia de ativação e uma invocação normal rodam guiadas pela fila, sem tempo no Core.
  *Entregue:* PR #77, gate por comando, prévia de ativação, invocação e efeitos vinculados. Smoke Windows A04 (eceef63, CI 45328386043) executou 29/29 Applied, compras de ambos lados 265–267 ms, summon 1362 ms, Sparks/Raigeki 832/830 ms, dano 2117 ms, destruições 864/867 ms, fila vazia; operador confirmou animações de compra do Joey. Correção de abertura lab permanece em A04. Fecha piloto, não migração de toda apresentação/CPU nem fidelidade de timing nativo. [Contrato](../../docs/architecture/action-presentation.md).

## A04 — Console de duelo no `Joey.App`

<a id="a04"></a>

*Entregue:* PR #72, parser/runner compartilhado App/Lab, comandos legais e setup explicitamente privilegiado, state/diff, move-id, F10, scripts DAT. Smoke Windows: 18 comandos e 106 linhas idênticas entre gráfico e headless; correção de compra automática fff5e6e; eceef63 29/29 Applied, sem wait >=5s, decks 2/2, LP 8000/7800, operador confirmou animações dos dois lados e spawn instantâneo intencional. CI 45328386043 aprovado. Fecha critério de piloto; fase/lado, ataques e trace JSON arbitrário seguem A07/A05, sem alegar cobertura geral. [Contrato](../../docs/architecture/duel-console.md).
  Modo console que **enfileira os mesmos comandos que as cartas usarão**: spawnar carta (mão/campo/deck/cemitério), mover entre zonas, comprar, invocar/setar, destruir, mudar LP, avançar fase, forçar lado. Os nomes devem espelhar o console nativo de `native_hooks` (`draw N [lado]`, `damage N [lado]`, `state`, `diff`) para rodar o **mesmo roteiro** nos dois lados e comparar. Uso principal: implementar e depurar cartas do zero apenas por essas interfaces, com feedback visual imediato.
  **Contrato do console:** jogador, CPU, cartas, testes, scripts e console são produtores da mesma API de comandos tipados do Core. A origem pode mudar a autorização/admissão (jogadas legais vs. operações privilegiadas de laboratório), nunca o executor, os gatilhos, a ordem ou a apresentação de um comando equivalente. `draw`, `destroy`, `damage`, `heal`, `move` etc. são propostas de interface textual; não presumir que todos já existem no console nativo. Resolver a identidade da ocorrência e revalidar na execução; distinguir destruir, descartar e enviar ao cemitério, mesmo quando o destino coincide. `spawn`/forçar fase/lado são comandos explícitos de laboratório, não mutações ocultas nem jogadas legais. Consultas `state`/`diff` são somente leitura. O parser/runner deve ser reutilizável sem raylib (scripts/testes/headless); o App é um frontend opcional. Nunca contornar a fila nem conceder privilégios de debug ao jogador/CPU.
  *Pronto quando:* um roteiro de compra, destruição e LP é executável interativamente e headless, gera os mesmos comandos/eventos/estado final sob as mesmas condições, e cada ação é animada no App. `state`/`diff` comparáveis ao nativo para operações suportadas; testar admissões inválidas e gatilhos normais de destruição.

## A05 — Trace de comandos comparável

<a id="a05"></a>

*A05.1–A05.7:* YDR, shuffle, identidade, movimentos, trace, CE/CF e ataque direto; CI 45325522858 aprovado. *A05.8:* batalha com alvo em piloto restrito, snapshots por papel 3C antes do dano e 7C destino final, matriz de combate e identidade, comparação 33/3C/7C parcial. CI 45326231602 aprovado; integração com A04 em curso, App, efeitos/FLIP/respostas e paridade dinâmica pendentes. [Contrato](../../docs/architecture/native-replay-scenarios.md).
  *A05.9:* consulta `trace` compartilhada pelo console App/Lab e exportação headless de roteiros com `--trace-output=path`. O documento conserva a ordem dos comandos executados e distingue prefixos incompletos.
  *A05.10:* o comparador valida a sequência desde o primeiro evento e confere as contagens do cabeçalho ou do rodapé antes de comparar qualquer janela JSONL.
  *A05.11:* o console admite os ataques direto e com alvo dos pilotos. O Lab gera o trace compra → invocação → ataque → dano pelo mesmo console. A preparação da fase no piloto usa o setup existente até A07; captura dinâmica equivalente e cue de combate ainda pendentes.
  *A05.12:* os traces C# novos declaram a contagem de linhas executadas. O comparador valida a contagem disponível e a sequência desde 1 antes de aceitar qualquer janela, inclusive quando a lacuna fica fora dela.
  *A05.13:* o roteiro headless executa compra, invocação, entrada em batalha, ataque e saída pelo console e pela fila. O comando revalida lado e turno e mantém as regras de fase do sandbox. A projeção preserva essas linhas como `unmapped`. A07 segue aberto para o ciclo completo e a comparação nativa.
  *Aceitação A05.14:* o CI executa o Lab e compara sua saída com a janela arquivada 281–301. Compra, invocação, ataque direto e dano concordam nos quatro registros projetáveis. O relatório mantém 17 registros do arquivo e quatro comandos C# sem mapeamento e classifica a janela como inconclusiva. Outro roteiro produz quatro divergências no relatório. O PR #81 fecha geração e comparação de traces no escopo do piloto. Estado equivalente no executável e apresentação do combate seguem em A09.

## A06 — Quebrar `DemoDuel` em motor + fila

<a id="a06"></a>

Mover as regras de `Demo.cs` para comandos e resolvedores pequenos; eliminar o OU de `Pending*` em favor do estado do comando ativo. Fazer **incremental**, uma família por vez, sem regressão nos testes existentes.
  *Pronto quando:* `Demo.cs` não decide ordem de ação; testes atuais passam.
  *Fatia de isolamento:* durante `Tick`, só o executor emite pelo sink. Continuações devolvem comandos no passo e callbacks de apresentação observam o comando e liberam o gate. `ActionQueueChecks` exercita tentativas nos três callbacks e na continuação, além do caminho autorizado de follow-up. O executor recebe um sink próprio, disponível durante a execução. Callbacks que recebem a fila pública não podem inserir comandos durante o tick. A migração das famílias e dos estados pendentes continua aberta.
  *Fatia de ativação com alvo:* o console App/Lab admite ativações direcionadas já migradas. A localização textual resolve para a ocorrência física antes de entrar na fila, com revalidação na execução. A admissão segue distinta de uma seleção interativa no tabuleiro.
  *Fatia de respostas para a mão:* o console lista e responde as seleções Deck/Cemitério → Mão já migradas. O script espera resposta explícita e sinaliza trace incompleto enquanto a escolha estiver pendente.
  *Fatia de respostas FLIP:* console App/Lab lista alvos de monstros e pares S/T (incluindo FIELD) e responde pelo produtor existente, por ocorrência, sem resolução duplicada. Bloqueia operações alheias enquanto a escolha está pendente; trace incompleto até resposta. Testes ambos lados, gates, duplicatas, recusa e identidade. PR #115, CI 45404987816 aprovado; FLIP em batalha e demais seleções ainda abertas.
  *Fatia de respostas para o campo:* console lista dono, origem, posição e slots livres e admite recrutamento individual, múltiplas cópias e ressurreição por ocorrência pelos produtores C12 existentes. Testes de escolha, gate, shuffle e donos consecutivos; combate legado é pré-condição para pedidos de recrutadores de batalha. PR #115, CI 45405582157 aprovado. A06 permanece parcial.
  *Fatia Damage Step:* ataque contra defesa oculta dos IDs restritos A05 usa revelação e cálculo em comandos distintos, sem Flip Summon. Gates, revalidação da reserva e comparação com legado nos dois lados; novos comandos unmapped. PR #119, CI 45429034187 aprovado antes da reconciliação; paridade nativa e cue gráfico ainda pendentes.
  *Fatia FLIP em batalha:* Skelengel (539/P118) em defesa oculta usa reserva de destruição, dano com gate, decisão opcional da fila, compra e finalização após o efeito (inclusive Exodia). CI 45515196933 aprovado; ambos lados, matriz ATK/DEF, aceitação/recusa, deck vazio, dano letal e trace independente do gate. PR #119. Continua restrito ao campo isolado; FLIPs com alvo e demais gatilhos de batalha seguem abertos.
  *Fatia FLIP com alvos em batalha:* Penguin Soldier (601/P087) reutiliza PendingFlipTarget e retorno por ocorrência; seleção bloqueia trace/roteiro, exclui destruição marcada e retoma combate após FinishFieldRemoval e gate. Testes de ambos lados, zero/um/dois alvos e paridade com legado não terminal. PR #129, CI pendente; campo isolado e ausência de novos opcodes preservadas.
  *Fatia destruição por efeito no Damage Step:* Man-Eater Bug (500/P072) exige alvo único, admite destruir defensor marcado e registra destino da ocorrência; cancela remoção de batalha duplicada quando a mesma carta já chegou ao GY por efeito. Testes espelhados, gates, identidade e contagem de eventos. PR #131, CI pendente. Recrutadores e campo ampliado seguem abertos.
  *Fatia recrutamento elemental P164:* seis IDs em combate isolado, gatilho só após chegada ao GY por batalha, ambas chegadas antes da primeira escolha, invocação/embaralhamento pela fila e próximo dono serializado. CI 45523672687 verde (51.135 checks); PR #132, projeção 7C de P164 unmapped, coleta 31 pendente. P168 e demais gatilhos não ampliados.

  *Consolidação:* PR #98, CI 45393879231 aprovado no head 4419fe2; dois roteiros Lab/DAT relatados no PR. Integrar fatia sem marcar A06 concluído; reconciliada com evidência de fases da main 15b54a0.

  *Resposta após Normal Summon (C12.1):* a janela que `FinishNormalPlacement` abre depois do gate conserva as opções e a prioridade existentes. App, CPU e console admitem o passe ou P196/P038 pelo mesmo comando, com revalidação da ocorrência e do Set. Ativação projeta a origem no gate A03. O gasto e cada destruição/vínculo têm um comando, e as buscas P006/P165 esperam a finalização. Regressões verificam ambos os lados, rejeição de substituição, passe, Trap Hole e a ordem de Torrential. O smoke sem janela usa o produtor normal do App em Classic e Instant. O SQLite v6 identifica P196/P038 e seus slots; decompilação e assembly no snapshot não cobrem os callbacks de ativação. A ordem demonstrada é do sandbox. `Demo.cs` 2618 → 2612 linhas. As respostas após Flip Summon e declaração de ataque continuam legadas.

*Evidência do P164 (PR #138):* o roteiro 31 recuperou o callback e confirmou o opcode de embaralhamento `60`, com seed produzida pelo RNG nativo. O trace C# conserva `ShuffleDeckCommand` como `unmapped`. [Contrato](../architecture/native-replay-scenarios.md#roteiro-31--callback-p164-e-shuffle-no-trace).

*Produtores de ataque (C12.1):* o App, a CPU e o console admitem ataques diretos ou contra um slot por `TryQueueAttack`, que reusa os dois pilotos existentes. Quando o alvo já foi mirado pelo jogador, o gate preserva a etapa inicial da animação que a interface já aplicava. O executor, as restrições de campo e as regras de combate permanecem as mesmas. Testes do App exercitam ataque com alvo humano e CPU em Classic/Instant, comparando fila e estado final ao headless. Sem carta nova, sem alteração de `Demo.cs` e sem ampliar a cobertura nativa. A admissão fora do piloto conserva o caminho anterior até a migração dos casos restantes.

*Campo ampliado (C12.1):* os monstros normais dos IDs 24 e 1351 também podem ocupar slots fora de um ataque com alvo enfileirado. S/T, FIELD, equipamentos e espectadores de outros IDs continuam fora deste piloto. A finalização de FLIP verifica todas as ocorrências no destino escolhido, incluindo alvos fora do combate. Os testes cobrem retorno de duas cópias de Penguin Soldier em ambos os lados, recrutamento P164 com slots ocupados, rejeição de espectador não suportado e produtores jogador/console/CPU no smoke sem janela em Classic e Instant. O SQLite v6 contém `0057A930`, `005861F0` e `00594200` para P087. A preparação e o primário tratam slots e lados de múltiplos alvos. A ordem dinâmica desta fatia permanece validada no sandbox. `Demo.cs` tem 2618 linhas antes e depois. O ataque direto conserva suas restrições. Próxima lacuna de A06/C12.1: resposta a armadilhas e ampliação da classificação de campo.


*Resposta a ataque direto (C12.1):* o piloto de um atacante admite somente
Magic Cylinder elegível nos slots de S/T adversários. O App, a CPU e
o console compartilham o passe ou a ativação. A janela abre depois
da declaração. A espada usa o gate do passe e o brilho usa o gate
da ativação. A fila gasta a ocorrência Set antes de emitir o dano ao
atacante e mantém a Battle Phase. Os testes exercitam ambos os lados,
cópias do mesmo ID, revalidação e a sequência dos gates. O ramo mantém
o trace da declaração sem projeção nativa. A apresentação reaproveita
os cues existentes e ainda não foi comparada em captura pareada.
Demo.cs 2614 → 2614. Respostas a ataques com alvo, Mirror Force, Negate
Attack, outros S/T e campos fora do piloto continuam no fluxo legado.

*Negate Attack no piloto direto (C12.1):* o Set elegível entra na mesma
janela do App, CPU e console. A fila gasta a ocorrência antes de emitir
a saída da Battle Phase pelo comando de fase existente. A fase não pode
ser reaberta nesse turno. Os testes cobrem os dois lados, passe,
substituição de ocorrência, ausência de dano e a fase final. Ataques com
alvo e Mirror Force continuam legados. Demo.cs 2614 → 2614.

*Mirror Force no piloto direto (C12.1):* o mesmo produtor admite as três
armadilhas de declaração. Com uma resposta elegível, os normais 24 e
1351 também podem ocupar slots aliados. A fila reutiliza o lote de
destruição, gasta a armadilha Set, destrói uma ocorrência por comando e
registra o resultado ao terminar. Regressões dos dois lados verificam
dois monstros em ataque, defesa aberta e oculta, gates e LP intactos.
Ataques com alvo que abrem respostas e campos fora do piloto ficam no
legado. Demo.cs 2614 → 2614, cobertura preservada.

*Respostas a ataques com alvo (C12.1):* os combatentes já admitidos pelo
piloto podem enfrentar as três armadilhas Set elegíveis. O produtor
guarda ocorrência e posição do alvo no momento da declaração. O passe
usa a origem da declaração para a espada e retoma a mesma resolução
antes da revelação. Qualquer das três ativações cancela o ataque sem
revelar o defensor. Os testes cobrem os dois lados, a espera no gate,
passe, dano refletido, fim da fase e destruição. Não há novo
mapeamento de opcode; contexto fora do piloto permanece legado.
Demo.cs 2614 → 2614 e cobertura preservada.

*Respostas a Flip Summon (C12.1):* o App, a CPU e o console reutilizam
o passe e a ativação enfileirados para P196/P038 quando o FLIP não
abre outra seleção simultânea. O gate de revelação precede a resposta.
Skelengel toma a decisão na fila depois do passe ou da finalização da
armadilha, inclusive quando a carta já foi para o cemitério. Os lotes
FLIP já migrados preservam seus comandos antes da abertura da janela.
O recorte preserva a ordem C# do sandbox. A coleta 31 descreve o callback
P118, mas a ordem dinâmica no original ainda precisa de comparação.
Demo.cs preservado em 2614 linhas. Seleções de cartas ou alvos que
coexistem com a resposta permanecem legadas.

*Recuperação P018 após resposta de Flip Summon (C12.1):* Magician of
Faith e Mask of Darkness conservam a seleção original enquanto o
respondente passa ou ativa a armadilha. Após o último gate, a
recuperação volta a ficar disponível mesmo se Torrential destruir a
fonte e resolve antes das buscas de cemitério geradas na destruição.
Os testes cobrem ambos os lados, passe, destruição, escolha entre
cópias do mesmo ID e ordem com Sangan. A supressão é reavaliada antes
de retomar a seleção. Demo.cs 2614 → 2614. Seleções de alvos no campo
ainda usam o fluxo legado. Ordem verificada em C#, sem comparação
nativa equivalente.

*Recrutamento após resposta de Flip Summon (C12.1):* Bubonic Vermin
retoma a seleção opcional Deck → campo após o passe ou o gate final de
Torrential Tribute. Os testes C# cobrem ambos os lados, recusa sem
embaralhamento e escolha da ocorrência exata em defesa oculta depois
que Torrential destrói a fonte. Demo.cs permanece com 2614 linhas.
A ordem ainda aguarda comparação nativa.

*Pares de Magias/Armadilhas após resposta de Flip Summon (C12.1):*
Greenkappa e Tornado Bird guardam a ocorrência da fonte durante a
resposta e reapresentam os alvos originais ainda válidos. Se restar menos
que o par obrigatório, o sandbox encerra a escolha sem efeito. Os testes
C# cobrem os dois lados, passe, ativação de Torrential com e sem par
restante e o gate de destruição. Demo.cs permanece com 2614 linhas.
A precedência entre armadilha e FLIP aguarda comparação nativa.

*Alvo monstro após resposta de Flip Summon (C12.1):* Man-Eater
Bug, Hane-Hane e Penguin Soldier retomam o produtor compartilhado de
seleção com a ocorrência original da fonte. Escolhas obrigatórias
sem candidatos terminam após o último gate de Torrential. Penguin
Soldier pode recusar, inclusive pela CPU com campo vazio. Os testes C#
cobrem ambos os lados, passe, Torrential e Trap Hole em fixture com
ATK suficiente. A ordem dinâmica entre FLIP e armadilha segue pendente
de comparação nativa. Demo.cs permanece com 2614 linhas.

*Combate com Magias e Armadilhas baixadas (C12.1):* o produtor
compartilhado admite ataques diretos e com alvo quando as zonas
comuns contêm apenas Magias/Armadilhas baixadas com ocorrência
rastreável. Somente as armadilhas oponentes já elegíveis abrem resposta.
Os testes C# cobrem os dois lados, conservação das cartas baixadas,
dano e os três tipos de resposta em combate com alvo. Modificadores
ativos e vínculos de equipamento continuam fora deste recorte.
Demo.cs permanece com 2614 linhas.

*Magias de Campo no combate enfileirado (C12.1):* ataques diretos
e com alvo admitem Magias de Campo já suportadas, baixadas ou ativas,
com ocorrência rastreável. Ambos reutilizam os atributos efetivos
do Core. As regressões cobrem os dois lados, o bônus de ATK de Yami
no dano direto e a diferença entre Chorus of Sanctuary ativo e
baixado depois da revelação do defensor. Equipamentos e contínuas
nas zonas comuns seguem fora do piloto. Demo.cs permanece com
2614 linhas. Validação em C#.

*Equipamentos no ataque direto (C12.1):* o produtor admite
equipamentos fixos e dinâmicos que tenham fonte e alvo rastreáveis.
Dano direto e Magic Cylinder usam o ATK efetivo. Mirror Force
remove o equipamento ligado em um comando separado. Os testes C#
cobrem os dois lados e a limpeza no cemitério do dono do
equipamento quando o alvo pertence ao outro jogador. O combate
com alvo ainda requer migração da limpeza por destruição de
batalha. Demo.cs permanece com 2614 linhas.

*Equipamentos no combate com alvo (C12.1):* o combate comum
reserva a limpeza de cada Equip rastreável após o envio do monstro
ao cemitério. O comando de destruição do Equip reservado completa
mesmo após dano letal. As regressões C# cobrem dois lados, empate,
dano letal e equipamentos cruzados, com duas cópias do mesmo ID.
O recorte de FLIP em batalha ainda usa campo sem equipamento.
Demo.cs permanece com 2614 linhas.

*FLIP em batalha com equipamento (C12.1):* o piloto de Skelengel,
Man-Eater Bug e Penguin Soldier usa o ATK efetivo do atacante equipado.
Se o FLIP remove o atacante, a fila limpa o equipamento antes de
finalizar a destruição de batalha do defensor. Os testes C# cobrem
os dois lados, a compra, a destruição e o retorno à mão com um
equipamento controlado pelo defensor. Demo.cs permanece com 2614
linhas. A ordem nativa com equipamento ainda aguarda comparação.

*Normais do catálogo no combate (C12.1):* o Core incorpora o
inventário versionado de subtipos como recurso do assembly. Os
264 normais de C13 entram no combate com alvo e são admitidos
como aliados em ataque direto. O teste, sintético e com DAT
local, compara LP, revelação e movimentação com o legado nos
dois lados. Os IDs históricos do piloto continuam aceitos.
Demo.cs permanece com 2614 linhas. Sem alteração de catálogo ou
validação contra o processo nativo.

*Hane-Hane em batalha (C12.1):* a revelação em defesa usa o
produtor enfileirado de retorno obrigatório de um monstro. O
gate de dano precede a escolha. O defensor marcado não é um
alvo válido, enquanto Hane-Hane não marcado pode retornar a
si mesmo. Os testes cobrem ambos os lados, recusa inválida,
retorno do atacante equipado e retorno do próprio defensor.
Demo.cs permanece com 2614 linhas. Comparação em C#.

*Compra por dano de batalha (C12.1):* Masked Sorcerer e Bistro
Butcher usam a mesma intenção do sandbox legado. A fila aplica
o dano e suas esperas antes das compras individuais e conclui a
remoção de batalha depois do lote. Dano letal ou ataque cancelado
não inicia as compras. Exodia mantém a precedência sobre deck-out,
e o lote obrigatório termina de comprar as cartas disponíveis.
Regressões C# cobrem os dois lados, defesa revelada, passe de
armadilha, dano zero, remoção, deck-out e Exodia. Demo.cs
2614 → 2609 linhas. Ainda falta comparação com o executável.

*P168 em batalha (C12.1):* Nimble Momonga e Giant Germ
chegam ao cemitério pela fila e recebem, respectivamente, cura
de 1000 e dano de 500 ao oponente por comando. Somente após as
remoções, alterações de LP e respectivos gates o produtor oferece
as cópias; os dois donos escolhem na ordem das remoções. Os testes
cobrem os dois lados, empate, dano letal, recusa, duas cópias e
posição de invocação. Dano letal preserva a alteração de LP do
sandbox e impede a oferta. Demo.cs 2609 → 2607 linhas. A ordem
nativa segue pendente de comparação.

*P006 em batalha (C12.1):* Sangan e Witch of the Black
Forest agora podem combater pela fila e abrem buscas obrigatórias
Deck → mão pelo produtor existente quando chegam ao cemitério.
No empate, a busca do atacante precede a do defensor e a segunda
aguarda a transferência, o embaralhamento e o gate da primeira.
Testes C# cobrem os dois lados, filtros distintos, console, CPU
e proteção contra resposta legada. Demo.cs permanece com 2607
linhas. Comparação nativa pendente.

*P169 em batalha (C12):* Spear Cretin (1116) entra na fila de
combate. O snapshot guarda o histórico de revelação antes de limpar
o slot. Após a chegada ao cemitério e o gate, o mesmo produtor de
ressurreição oferece a escolha do controlador e depois a adversária.
A segunda espera a invocação da primeira. Testes espelhados cobrem
revelação em batalha, cópia distinta, ausência de disparo sem FLIP,
gates, console e CPU. Demo.cs 2607 → 2600 linhas pela reutilização
do registro do gatilho. A resolução de combate legada ainda atende
configurações fora do piloto. A comparação de P169 no Joey segue
pendente. [Contrato](../cards/c12-queued-graveyard-revival.md).
[PR #212](https://github.com/ffdiego/YugiRE/pull/212) ·
[CI aprovado](https://github.com/ffdiego/YugiRE/actions/runs/45831426885).

*P018 em batalha (C12):* Magician of Faith e Mask of Darkness
abrem o seletor de recuperação por ocorrência quando reveladas no
combate. O App e a CPU aguardam o mesmo comando de movimento.
Uma carta marcada permanece até o fim do gate da recuperação.
Sem candidato, a fila conclui o combate sem abrir escolha.
Demo.cs 2600 → 2600 linhas. O despacho foi ligado à regra de seleção
e ao executor existentes. A batalha legada fica disponível fora
do piloto. SQLite v7 registra `00593530` para preparação de alvo.
Comparação dinâmica pendente.
[Contrato](../cards/c12-queued-graveyard-recovery.md).
[PR #217](https://github.com/ffdiego/YugiRE/pull/217) ·
[CI aprovado](https://github.com/ffdiego/YugiRE/actions/runs/45874884888).

*P082/P088 em batalha (C12):* Needle Worm e Hiro's Shadow Scout
usam o mesmo lote do Flip Summon manual. Cada carta é movimentada
por comando e o defensor marcado aguarda a conclusão do lote.
A limpeza ocorre mesmo se a compra do Scout encerrar o duelo.
Demo.cs 2600 → 2600 linhas. A fatia liga o produtor de batalha
ao executor existente e preserva os caminhos de revelação fora dele.
Casos C# nos dois lados, incluindo deck insuficiente e gate.
[Contrato](../cards/c12-queued-flip-deck-flow.md).
[PR #219](https://github.com/ffdiego/YugiRE/pull/219) ·
[CI aprovado](https://github.com/ffdiego/YugiRE/actions/runs/45876461293).

*P086 em batalha (C12):* Morphing Jar usa o mesmo lote do Flip
Summon para reservar os descartes e emitir as compras dos dois
lados. A finalização adjudica a insuficiência de deck antes da
remoção do defensor. Exodia e o empate por dois decks insuficientes
mantêm as políticas do sandbox. Testes C# cobrem gates, ambos os
lados, decks vazios e finalização após Exodia. Demo.cs 2608 →
2608 linhas. O produtor passa pelo executor já migrado e os
caminhos de revelação fora do recorte ainda usam o resolvedor
legado. O catálogo aponta `005860E0` para P086, sem corpo nas
tabelas consultadas do SQLite v7. Comparação dinâmica pendente.
[Contrato de FLIPs de deck](../cards/c12-queued-flip-deck-flow.md).
[PR #225](https://github.com/ffdiego/YugiRE/pull/225) ·
[CI aprovado](https://github.com/ffdiego/YugiRE/actions/runs/45880054620).

## A07 — Turno e fases como comandos/estágios da fila

<a id="a07"></a>

A05 enfileira entrada e saída de batalha no sandbox. O roteiro 11 já foi recebido e importado no SQLite cumulativo v2, com dez funções e duas tabelas verificadas. Continuam abertos o ciclo completo de turnos na fila e a correlação entre índice do dispatcher, bits de fase e rótulo exibido. [Evidência e lacuna](../../docs/20-turn-scheduling.md#reprodução-e-evidências).
  *Encerramento no sandbox:* `turn end` admite entrada na End Phase e agenda a troca de jogador em outro comando. Acima do limite, `discard-end INDEX` responde por ocorrência; a troca espera o gate do último descarte. O efeito de encerramento existente também usa um comando por ocorrência após o gate de descarte e pode encerrar o duelo antes da troca. O trace projeta a troca aplicada como `03`, com lado que está encerrando e operandos zero. A entrada em End Phase, o descarte e a limpeza `02` seguem sem projeção. [Evidência](../20-turn-scheduling.md#projeção-da-troca-de-turno-em-a07).
  *Compra no sandbox:* `turn draw` reserva a compra legal do jogador ativo e revalida o turno na execução. A compra comum projeta `0x61`. A fila honra o gate da compra antes do processamento dos retornos atrasados de Standby e invoca a ocorrência do cemitério com `SpecialSummonCommand`. O modo experimental `--queued-actions` do App usa essa compra na abertura do turno e liga os botões de Battle, Main2 e End, mais o descarte obrigatório, aos mesmos produtores da fila. A CPU experimental admite as mesmas operações de fase, inclusive o descarte obrigatório. As oportunidades de fase e a correlação de sua cronologia nativa continuam abertas.
  *Reconciliação com A06:* PR #108 integra draw/Standby/end/discard/CPU em fatia incremental, mantendo seleções do console, trace incompleto e admissão isolada. CI anterior 45398551966 verde, novo CI pendente. A07 não concluído: ciclo/cronologia nativa e visual restantes.
  *Curas de Standby (C03.4):* o produtor de compra agenda a continuação de cura após a compra e os gates dos retornos atrasados. A compra pulada também entrega a fase e deck-out encerra antes das curas. Os testes cobrem CPU, ambos os lados, pose, duplicatas e um comando por ocorrência. [Cartas e limites](CARDS.md#c034--ganhos-de-lp-na-standby).
  *Retornos de End Phase (C03.6):* o produtor serializa as ocorrências de The Wicked Worm Beast, deixa a limpeza vinculada passar pelo gate e só então consulta o limite de mão. O descarte obrigatório e a troca de turno continuam tipados. [Cartas e limites](CARDS.md#c036--retorno-de-the-wicked-worm-beast-no-fim-do-turno).

## A08 — Extrair `Joey.Cards` e definir `EffectContext`

<a id="a08"></a>

Novo assembly com as definições de cartas por resource ID e as famílias de efeito. Dependências: `Joey.Cards` → `Joey.Core`, nunca o contrário; `Joey.App` monta Core + Cards + LookAndFeel. O Core fica com o que vale para qualquer duelo (fases, batalha, invocação, a fila e `EffectContext`); o catálogo e o console (`A04`) criam cartas por resource ID a partir de `Joey.Cards`.
  **`EffectContext`** expõe primitivos pequenos e combináveis (`Destroy(filtro)`, `Draw`, `Damage`, `Heal`, `Choose`, `Move`...), cada um **enfileirando comandos**; não muta o estado diretamente (invariante 1).
  **Açúcar para cartas triviais:** helpers como `DestroyMonsters(Side)` são só açúcar sobre `Destroy(filtro)`. Eles tiram uma foto dos alvos na resolução e enfileiram **um comando `Destroy` por monstro**; um comando cujo alvo já saiu do campo quando chega a vez é descartado. Ex.: Raigeki = `DestroyMonsters(Opponent)`; Dark Hole = `DestroyMonsters(Opponent)` + `DestroyMonsters(Own)`. A **ordem** entre lados e zonas é observável e deve ser conferida no original antes de ser fixada (hipótese até medir).
  **Efeitos com continuação** (escolha de alvo, gatilhos após a destruição, janelas de resposta) se expressam como passos esperados (`IEnumerable<Step>`; **sem snapshots por ora** — o replay vem de `seed + log de entradas`) e retomam em nova passagem da fila, sem recursão.
  Migrar para `Joey.Cards` as regras declarativas existentes (`DirectDamageRules`, equipamentos, campo etc.) junto com `C12`.
  *A08.1–A08.2:* `EffectContext` compõe compra, LP, envio do custo ao cemitério e destruições de ocorrências verificadas. A limpeza vinculada segue imediatamente após a destruição que a dispara. O executor continua dono da admissão, das reservas e dos gatilhos. A extração do assembly e as decisões pendentes seguem abertas no [PR #101](https://github.com/ffdiego/YugiRE/pull/101).
  *A08.3:* a continuação de compra opcional admite primeiro a resposta. O executor revalida supressão, turno e disponibilidade antes de emitir a compra dependente.
  *Pronto quando:* Raigeki, Dark Hole e uma carta com continuação (ex.: escolha + destruição) vivem em `Joey.Cards`, passam pelo console (`A04`) e produzem a mesma lista de comandos com e sem gate visual.

  *Reconciliação:* PR #101 preserva A06/A07; CI anterior 45392730663 verde, extração de Joey.Cards ainda pendente.

## A09 — Comparar combate em condição equivalente e apresentar o trace no App

<a id="a09"></a>

Obter um cenário nativo com estado inicial e comandos comparáveis ao piloto A05, preservar as lacunas do trace e validar a apresentação dos comandos de combate no gate do App. A05 entrega a projeção e o relatório headless. A07 cobre fases nativas. A reprodução visual do combate permanece na trilha VISUALS.
  *A09.1:* projeção somente leitura da declaração de ataque direto/com alvo; App usa o cue de espada existente no gate da declaração, antes de remoção/dano, sem novo relógio ou fila. Roteiro sem mira manual usa alreadyAimed=false. Regressão App sem DAT/display Classic/Instant, CI 45400089826 aprovado; smoke gráfico e captura nativa equivalente ainda necessários.
  *A09.2:* regressão App Classic/Instant espelhada para ambos os lados, CI 45402454877 aprovado. Roteiro DAT e comparador de trace/state/ordem dos gates com testes negativos; execução real no desktop e captura equivalente do original ainda pendentes. Ver [roteiro](../../docs/architecture/native-replay-scenarios.md#a092--ambos-os-lados-e-smoke-dat).
  *A09.3 (PR #137):* o clique de ataque direto reutiliza a admissão tipada no campo isolado. A regressão de startup exercita o manipulador nos dois lados em Classic/Instant e exige a origem Player. O smoke gráfico e o cenário nativo continuam pendentes.
  *A09.4 (PR #140):* a CPU admite o ataque direto isolado pela mesma fila. A declaração origina um único cue e antecede o dano. Regressões de App e Core comparam CPU headless, Classic, Instant e o resultado legado.

### Fronteira VISUALS / ACTIONS A03 (decisão do mantenedor)

ACTIONS é responsável por conectar ActionQueue / IPresentationGate à apresentação.
VISUALS preserva Joey.LookAndFeel independente do Core/raylib, entrega
componentes reutilizáveis de um efeito e Draws; não cria novo FIFO de ações
ou efeitos nem implementa uma ponte paralela. A03 deve migrar os adaptadores
legados PresentationSequence no App, sem duplicar mutações ou enfileiramento.

**Pontos de atenção arquitetural para A03:** (1) visível/ativo não significa
bloqueante: V23 aguarda em 128 visível e não bloqueia; V24 é contextual e não
deve bloquear; (2) CanComplete é uma consulta, não um Update/Draw: V11 tem
mutação no Draw, V26 Update vazio e 24 Draws; V30 Draw incrementa +4 e flag final diverge do predicado inclusivo; não avançar
efeitos ao consultar gates nem inventar timeout; (3) remoções/transferências
precisam de snapshot visível por ocorrência e geometria antes da execução
(ActionQueue executa antes de CanComplete); (4) ao migrar, impedir emissão
dupla pelos eventos DemoDuel e pelo comando ativo. A03 define a política de
cancelamento/Instant e identidade por Sequence, inclusive comandos Skipped/
Rejected, sem obrigar LookAndFeel a conhecer tipos do Core. Não bloquear
comando por objeto que ainda não tem Draw/contrato de conclusão.

### C12/A09 — Sandbox headless usa o mesmo executor

O runner de política restrita não resolve mais diretamente ações. JSONL conserva snapshots e acrescenta comandos executados; resultado Unsupported não significa derrota, empate ou validação do original. Para A09 ainda são necessários estados/decisões equivalentes e comparação com o executável. Não confundir esse smoke sintético com duelo diferencial.

### A09.5 — Checkpoints do roteiro headless

O Lab exporta o estado inicial e os estados após as linhas
admitidas, com o número da linha, a decisão pendente e a última
sequência executada. As observações usam o estado do Core no
limite em que o roteiro pode avançar. Uma falha conserva o
prefixo observado e marca a saída como incompleta. A validação
usa roteiro sintético. A correlação com checkpoints nativos
equivalentes, incluindo associação de ocorrências e RNG,
permanece pendente.
[Contrato de checkpoints](../architecture/duel-differential-policy.md#checkpoints-do-laboratório).

### A09.6 — Comparação explícita de checkpoints

O comparador reaproveita a validação de integridade e a reconstrução do
estado da captura nativa na entrada do produtor. Recebe pares ordenados
de sequência nativa e linha C#, incluindo o estado inicial. Compara LP,
ordem da mão por índice interno e contagens de deck e cemitério por lado.
Relata o prefixo concordante, a primeira diferença observada e as lacunas.
Fixtures sintéticas exercitam concordância, divergência e resultado
inconclusivo. Ainda falta parear uma captura real com decisões e estado
equivalentes. A comparação de campo, fase e ocorrências segue pendente.
[Uso e limites](../architecture/duel-differential-policy.md#comparação-de-checkpoints-pareados).

### A09.7 — Campo nos checkpoints pareados

O comparador passa a apontar o lado e o slot da primeira diferença de
ocupação, índice interno, face ou posição de defesa. O SQLite cumulativo
v7 foi conferido com o fingerprint do EXE. `0056C537` testa os 12 bits
do índice e zero indica slot vazio. As decompilações de `00568610` e
`005686E0` confirmam o bit 0 de defesa de monstro e o bit 1 de face
aberta em registro +6. `00405CB0` e o decodificador CE/CF documentam
os slots 5–9 de magia/armadilha e 10 de magia de campo. Testes
sintéticos cobrem divergência de postura, remoção e face, além de bit
alto irrelevante e slot vazio com flags residuais. Atributos
efetivos, associações e identidade de ocorrências seguem em aberto.

### A09.8 — Inventário para pareamento de checkpoints

O comparador disponibiliza `--inventory` e uma janela nativa opcional.
Lista argumentos do produtor, sequência, duelo, projeções de estado e
lacunas de leitura, além dos limites C# com contexto e escolha pendente.
Não escolhe pares por igualdade de estado nem atribui causalidade a comandos.
A validação integral do JSONL continua obrigatória. Fixture sintética cobre
lacuna, restauração por baseline, janela, limites C# e rodapé inválido.
CI pendente; primeiro pareamento real e atributos/decisões equivalentes
continuam em aberto. [Uso](../architecture/duel-differential-policy.md#inspeção-antes-de-parear).
