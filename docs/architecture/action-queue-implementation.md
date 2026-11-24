# A02 — Fila tipada no Joey.Core (parcial)

Implementa infraestrutura descrita em [A01](action-queue.md), sem substituir ainda os caminhos legados. Os caminhos de jogador/App/CPU continuam legados, mas o ingresso agora diferencia ocorrências e a fila opt-in bloqueia as entradas legadas que consultam ActionsBlocked enquanto ocupada.

- ActionQueue: FIFO, registro ativo separado, gate global e de conclusão, no máximo um novo comando por Tick. Mutação do executor uma vez. EnqueueBatch valida argumentos estruturalmente antes de admitir. Trace de admissão/início/execução/fim, inclusive resultados Skipped/Rejected. Exceção no executor/gate/continuação deixa fila faulted; nunca repetir uma mutação possivelmente parcial.
- DuelCommand: tipos Draw(1), Summon, Set, SendToGraveyard, Discard, Destroy, Damage, Heal, Flip, ChangePosition. Identidade CardOccurrenceId independente de resource ID, posição e referência de catálogo; a alocação e resolução inicial foram implementadas em A02.2 (abaixo). Side de movimento identifica o lado da ocorrência. Slots são destinos explícitos.
- IPresentationGate: contrato headless imediato ou gate manual. Sem relógios. Origem só no envelope/trace; não chega ao executor. A autorização de jogador/CPU/lab **não** está implementada aqui; Enqueue significa admitir trabalho já autorizado.
- Continuação: executor pode anexar comandos ou agendar uma continuação; ela retoma em Tick separado, após esvaziar FIFO e liberar gate. Reutiliza PendingEffectDecision / EffectDecisionAnswer como dados, sem migrar queuedEffectDecisions do DemoDuel ainda. Um passo de decisão não pode emitir comandos ao mesmo tempo. SubmitDecision valida ID e resposta e apenas registra a escolha. As continuações devem emitir comandos, nunca mutar estado diretamente.
- Piloto explícito DemoDuel.CreatePrimitiveActionQueue: Draw(1) de efeito, Damage, Heal por meio de DrawToHand/ChangeLife, conservando verificações Exodia/LP e eventos existentes. Não é a compra normal do turno, não dispara Standby. Draw sem deck perde no sandbox. Rejeita comandos não migrados e ações com duelo encerrado/decisão legada pendente. Não usar simultaneamente com chamadas legadas; não é API pública de admissão legal nem console. Outros produtores não usam esta fila ainda.

## Validação

ActionQueueChecks está registrado no runner Joey.Tests. Inclui admissão atômica, gates, FIFO, três destruições (executor de teste), ocorrência obsoleta, continuação/decisão, falha sem retry, e igualdade de trace lógico/estado/eventos Draw/LP no DemoDuel real com gate imediato e retardado. O executor de teste não demonstra paridade de regras de destruição. Executar:

```sh
dotnet build YugiRE.sln
dotnet run --project src/tests/Joey.Tests
python scripts/publish_actions_progress.py --check
```

CI do PR #67, commit 7ddc808, run 45316422970: sucesso (A02.1). A02.2 ainda não executada neste ambiente (SDK .NET indisponível). A02 continua Parcial, badge permanece 1/8; A03–A06/A08 continuam bloqueados.

## A02.2 — identidade e destruição de monstro no estado real

Cada entrada de Main Deck, mão, campo, cemitério e Fusion Deck importada nas fábricas de DemoDuel recebe uma cópia imutável de Card e um ID numérico por duelo, atribuído deterministamente. Igualdade de valor permanece a do catálogo; o dicionário usa ReferenceEqualityComparer. Movimentos existentes carregam a mesma instância (ex.: Hand → Monster). Não confundir ID com resource ID ou slot. Captura/resolução em Hand/Monster/SpellTrap/FieldSpell busca a ocorrência na zona/lado esperados, rejeitando objetos não registrados ou presentes em mais de uma zona. Arrays públicos continuam disponíveis para fixtures; não são API legal de escrita. O adaptador não pretende detectar remoção/reinserção da mesma instância física na mesma zona, nem oferecer IDs de persistência/cross-duel.

CreatePrimitiveActionQueue agora cria uma única fila por duelo. TryQueueRaigekiFromHand exige fila livre, fase/ativação legal, carta física identificada e alvos suportados. O executor revalida e produz, sem mover nada na ativação, SendToGraveyard da magia e Destroy por monstro; cada comando tem seu gate e evento. Ordem da lista permanece a de MonsterWipeRules. Após a fila e último gate drenarem, uma continuação propõe FinishFieldRemovalCommand, que publica LastMonsterWipe e libera buscas legadas (Sangan/Witch etc.). A continuação não move cartas. O estágio de finalização é lógico, não um opcode nativo. A resolução da escolha em si continua legada e deve ocorrer depois de a fila ficar livre.

Destroy reaproveita RemoveMonster com deferimento dos gatilhos. Envio comum da mão e Discard são comandos distintos; somente Discard emite a notificação legada de descarte. O piloto não resolve correntes, Banisher, controle, tokens ou cartas cujo RemoveMonster ainda altera LP (P065 revelado) ou destrói equipamentos vinculados diretamente. Rejeita essas remoções antes de gastar Raigeki, em vez de ocultar mutações fora da fila. Destruições de monstros por outros produtores legados ainda não foram redirecionadas. A admissão legal de outros comandos não está pronta; não intercalar chamadas diretas/fixture com a fila em uso.

Evidência: SQLite privado joey-knowledge-visual-decoded, consulta somente leitura, 00576960 percorre posições 0..10 em ordem crescente por lado; 00576B30 emite 79 no ramo de destruição comum. Callback P039 00584CB0 e candidato 0057A8A0 não constam decompilados no snapshot. A ordem numérica usada aqui é a do sandbox, consistente com o helper compartilhado; não é prova de ordem visual nativa específica de Raigeki. Ver [destruição](../cards/monster-destruction.md).

ActionQueueRemovalChecks: duas cópias idênticas da magia e de monstros, ambos os lados, três destruições reais com gates, fonte gasta uma vez, rosto oculto, gatilho Sangan após o lote, trace imediato/retardado, mudança de zona, alvo obsoleto com slot reaproveitado, alias de fixture, descarte versus envio e rejeição de LP vinculado. Executar Joey.Tests no CI; não promove Verified.

## Próxima fatia

Implementar os demais adaptadores tipados: Summon/Set/Flip/ChangePosition, envio de outras zonas, custos e consequências vinculadas, sem mutar fora dos comandos; separar escolhas legadas do executor e migrar a admissão comum. Um novo roteiro nativo só se necessário, SQLite primeiro. A02 segue Parcial até os critérios e testes passarem; A06/C12 são a migração incremental dos produtores legados, A08 extrai cartas. A03/A04/A05 ainda bloqueados.

## A02.3 — placement e correção de regressão

CI 45317261823: build e novos testes de A02.2 passaram; a suíte parou em NormalSummonCinematicChecks, porque a instância importada não é mais a referência de catálogo. Fixtures de identidade foram ajustadas para capturar a instância real da mão/campo/decisão, não para substituir identidade por igualdade de valor. A validação integral ainda precisa rodar.

TryQueueNormalPlacement prepara Summon/Set de monstro, valida todos os alvos de tributo/destino, revalida no executor, e produz Tribute por ocorrência seguido de Summon/Set. Cada custo usa RemoveMonster com isTribute, sem efeitos vinculados não migrados; a colocação compartilha CommitNormalPlacement com o fluxo legado. Após gate, FinishNormalPlacement libera resposta de invocação e buscas usando o mesmo helper legado. Alteração externa durante lote reservado gera fault; não é transação com rollback. TryQueueSpellTrapSet cobre magia/armadilha/Field Spell, sem ativação. Comandos continuam opt-in; Flip/ChangePosition e respostas legadas são próximas fatias. Testes ActionQueuePlacementChecks abrangem ambos os lados, Set/face-up, tributos, campo cheio, gates e trace idêntico. Não há promoção de paridade nativa.

## A02.4 — posição e FLIP

CI A02.3 (45317822589): build e os testes novos passaram; suíte legada parou em FusionChecks por restaurar um material substituído usando o objeto do catálogo. A02.4 restaura a ocorrência física capturada (também Field Spell). CI integral pendente.

TryQueueChangePosition admite alteração face-up (ChangePositionCommand) ou Flip Summon manual (FlipCommand); usa CommitManualPosition comum ao fluxo legado. ResolvePositionCommand é produzido só depois do gate. Efeitos de escolha sem movimentação são encaminhados à ponte legada, e Royal Command usa a checagem existente. Morphing Jar descarta ocorrências de ambas as mãos em ordem sandbox e compra individualmente até cinco por lado. O estágio FinishFlipBatch preserva o resultado de deck-out simultâneo/Exodia do legado; Immortal produz HealCommand antes da mesma finalização. A resolução de busca/armadilha continua legada. Outros FLIPs com mutação de massa rejeitam antes da revelação; não se afirma paridade nativa de ordem/timing. Flip de batalha e revelação externa não são Flip Summon. Testes com dois lados, quatro combinações de deck-out, gates e trace, posição e Heal. A02 segue Parcial.

## A02.5 — consequências vinculadas e decisões

CI A02.4 (45318150043) completo verde. A02.5: planejar, antes de admitir o lote de Raigeki/tributos, Destroy do monstro/Tribute, Destroy do equipamento vinculado e Damage de Immortal previamente revelado. RemoveMonster compartilha movimentação e gatilhos, mas permite suprimir somente as consequências já planejadas. FIFO não é reordenada; Destroy avulso com vínculos exige ação isolada. Se a perda de LP for letal, o lote reservado termina as movimentações já admitidas, como o sandbox legado. Não cobre Banisher/tokens/controle nem destruição genérica de outras magias/armadilhas. A guarda de admissão do DemoDuel fecha o lote após o primeiro Tick e recusa novos comandos quando decisão legada está pendente. Respostas legadas não podem ser consumidas durante o gate ativo. Callbacks de apresentação são observadores, não produtores reentrantes.

Skelengel sem resposta de invocação concorrente usa PendingDecision da ActionQueue: escolha após gate, SubmitDecision somente registra, continuação produz RecordFlipDrawDecision + Draw(1) se aceito. Com resposta concorrente conserva ponte legada. Testes de vínculo/equipamento, perda letal, equivalência de trace e decisão de aceite/recusa. CI A02.5 pendente. Badge permanece parcial até validação e revisão dos critérios.

## Fechamento A02

CI integral A02.5 [45318593622](https://github.com/ffdiego/YugiRE/actions/runs/45318593622) aprovado, PR #67 integrado à main 93846a7 (A01 #64 integrado antes). Aceitação: ActionQueueChecks cobre admissão sem mutação, FIFO/ativo único, gate global/final, três destruições, alvo obsoleto, continuação não recursiva e falha sem retry. ActionQueueRemovalChecks, ActionQueuePlacementChecks, ActionQueueFlipChecks e ActionQueueLinkedChecks exercitam estado real, ambos os lados, efeitos/tributos individuais, equivalência de trace imediato/retardado e decisão Skelengel. Os dez tipos têm caminho experimental. As regressões anteriores de identidade foram corrigidas; a suíte legada passa. Concluído o marco de infraestrutura; A03/A04/A05/A06/A08 liberados. Não promover paridade nativa, nem declarar integração App/CPU ou todos os efeitos concluídos. A07 permanece dependente da evidência de fases. Próxima prioridade operacional: A04, começando pelo parser/runner headless; integração visual exige A03.

## A07 — Encerramento de turno no sandbox

`turn end` do console admite `EndTurnCommand`, que entra na End Phase.
A troca de lado usa `AdvanceTurnCommand` separado. Com mão acima do limite,
`discard-end INDEX` seleciona uma ocorrência para `EndTurnDiscardCommand`.
Cada descarte produz a notificação usual de movimento. O último prepara
a troca após seu gate, e um trace que termina no prompt fica incompleto.
A execução revalida lado, turno e a ocorrência da mão.

Quando o efeito de encerramento do sandbox tem uma mensagem disponível,
a fila captura a ocorrência da mão ou do deck em um comando de colocação.
O comando revalida o efeito, a fonte e o slot. A colocação precede a troca
e o duelo termina ali em caso de vitória. O fluxo legado reaproveita
a mesma escolha e colocação. Demais oportunidades de fase e mapeamento
no replay nativo ficam em fatias próprias de A07.

## A07 — Compra legal e retorno de Standby no sandbox

`turn draw` usa `TurnDrawCommand` com lado e turno esperados. Uma compra
bem-sucedida emite a projeção usual de uma carta (`0x61`). A perda por
deck vazio aplica a regra do sandbox e fica sem projeção. A operação
privilegiada `draw N [SIDE]` preserva a permissão de compra legal do turno.

Depois do gate de compra, a fila processa cada retorno atrasado vencido
do lado ativo, na ordem do resolvedor legado. Cada promessa é consumida
uma vez. Com a ocorrência ainda no cemitério e um slot livre, um
`SpecialSummonCommand` separado invoca em defesa revelada. Campo cheio ou
fonte ausente consome a promessa com o custo já pago. O caminho admite
a compra manual no sandbox e deixa em aberto os gates nativos completos.

O App também admite esse lote automaticamente com `--queued-actions`.
O aviso de Draw pode acompanhar a fila, que continua respeitando pausas
e movimentos. Standby e Main aguardam a resolução do lote inteiro.
O teste de startup sem janela cobre Classic e Instant, a compra dos
dois lados e a perda por falta de cartas. No modo experimental, os botões
Battle, Main2 e End admitem os produtores comuns e permitem que a apresentação
observe a mudança depois da execução. O descarte obrigatório usa a mesma fila.
O App padrão ainda usa o caminho legado e o Lab usa comandos explícitos.
