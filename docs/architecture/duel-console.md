# A04.1 — Console de duelo headless (piloto)

O console é um produtor de laboratório explícito. DuelConsoleSession no Core cria a fila única do duelo, faz parsing sem mutar zonas/LP, resolve ocorrência no slot e admite comandos com ActionOrigin.Console. O host dirige Tick; não há relógio nem raylib no parser. Não habilitar em partida normal sem opt-in.

```sh
dotnet run --project src/Joey.Lab -- duel-console --fixture
dotnet run --project src/Joey.Lab -- duel-console --fixture scripts/fixtures/duel-console.txt
```

O primeiro frontend aceita stdin ou arquivo e usa fixture sintética, não DAT; não exige exe. Exemplo: state, draw 2, destroy 1 0, damage 300 1, heal 100 0, diff. Lados 0/1 (default 0); slots de monstro 0–4. draw gera N Draw(1); damage/heal são delta único; destroy resolve ID de ocorrência antes de admitir e executor revalida. state registra baseline somente na sessão, diff compara sem alterar duelo. decision ID accept|decline responde apenas ao scheduler. Comando admitido não significa aplicado; runner imprime resultado do executor. Consultas permanecem acessíveis durante gate. Não há execução de código arbitrário.

Testes DuelConsoleChecks: admissão inválida sem trace, busy, mesmo roteiro com gate imediato/retardado, estado e trace iguais, stdin. Próximas fatias: DAT/deck real, spawn/move tipados e validação, invocação/Set, escolhas legadas e fases (A07), integração App/gate A03, adaptador comparável ao nativo A05. State/diff atual são formato diagnóstico do sandbox, não schema nativo. A04 permanece parcial; não há console no App ainda.

## A04.2 — montagem de cenário

```sh
dotnet run --project src/Joey.Lab -- duel-console --dat /path/to/data.dat
```

DAT local: catálogo carregado via Catalog.Load, duelo vazio. spawn ID side zone [slot [attack|defense|set]] usa resource ID decimal (IDs ausentes/ambíguos rejeitados). move side sourceZone index destZone [slot [attack|defense|set]] preserva a ocorrência e não reordena as demais cartas. Zonas hand/deck/grave/banished (append, deck no fundo), monster/spell (slot 0–4), field (dedicada). Magias/armadilhas entram Set; monstros attack por default, defense/set explícitos. Editar cenário não é jogada legal, não dispara gatilhos, não consome summon nem verifica Exodia; para regras reais use comandos semânticos. Vínculos de equipamentos, fontes ativas de magia/armadilha e revivals pendentes rejeitam move. Não suporta mudança de lado, Fusion Deck ou substituição de slot ocupado. IDs físicos em state/diff (resource/index#occurrence), não schema nativo. LabSpawn/LabMove são tipos internos aceitos apenas em fila explicitamente lab e origem Console/Test; ActionOrigin.Effect/Player/CPU não autorizam edição. Admissão não muta; executor revalida. CI A04.1 falhou por teste que confundia origem de continuação com origem do operador; teste corrigido. A04.2 CI pendente. Main reconciliada preservando planos CARDS/VISUALS.

## A04.3 — jogadas e efeitos reais

CI A04.2 completo aprovado (45319725401). summon INDEX SLOT [tribute slots], set INDEX SLOT [tribute slots] para monstro, set INDEX para magia/armadilha (zona usual), flip SLOT, position SLOT attack|defense, activate hand|set INDEX usam lado ativo, produtores comuns DemoDuel e origem Console (sem ignorar regras). Ativação cobre a família MonsterWipeRules (inclui Dark Hole/Raigeki), DrawSpellRules e LifePointSpellRules: fonte gasta por SendToGraveyard, cada compra/LP/destruição separada, conclusão após gate. Set normal Spell segue permissão de mesmo turno legada. Outras ativações rejeitam; não há novo modelo de chain/respostas. A04.2 spawn/move continuam setup, não jogadas. discard SIDE INDEX e send SIDE INDEX são operações de efeito/lab autorizadas pelo console, não uma jogada livre ou descarte de fim de turno; preservam distinção observável. Testes ambos lados, tributos/limites, Set, draw/LP, wipe, FLIP, equivalência de gate. A04.3 CI pendente. Próxima dependência A03 para apresentação; escolhas legadas e fases ficam A06/A07, não contornar fila.

## Reconciliação com C12

A04.3 CI 45320160383 verde antes da reconciliação. A main integrou compra/LP/equipamento; removidos ActivateSimpleSpell/FinishSimpleSpell duplicados da branch A04. TryQueueActivation encaminha compra/LP para os produtores C12 e mantém a generalização de wipes. Novo equip hand|set INDEX SIDE SLOT encaminha TryQueueEquipment; nenhum executor paralelo. Preservados os testes de CARDS e os testes headless do console. CI da reconciliação pendente. A03 segue em PR independente #77. A04 parcial.

## Integração com A03 e C12 wipes

Base atual A03 #77 inclui a main a2fce55. TryQueueActivation delega todas as quatro famílias de magia aos produtores C12 (compra, LP, destruição de monstros e de S/T); não há segundo resolvedor de wipe. A04 fica empilhado até A03 ser integrado. A05.1 (#81) gera roteiros nativos parciais consumíveis pelo mesmo frontend.

## A04.4 — frontend gráfico

`dotnet run --project src/Joey.App -- /path/data.dat --duel-console` inicia laboratório vazio (mesma factory do Lab), sem decisões CPU. F10 alterna overlay debug, Enter envia, F6 pausa apresentação. `--duel-script=scripts/fixtures/duel-console-visual-smoke.txt` roda roteiro; DuelConsoleScript compartilhado admite uma linha por vez quando fila livre, espera decisão explícita e para em comando rejeitado/obsoleto. Log stdout com trace e state/diff; não inventa apresentação de setup spawn/move. App usa AppActionPresentationGate; sem fila lógica adicional. Limites: legacy pending/fases ainda não migrados, setup sem animação; não é emulador YDR. Roteiro 22 pendente para validação runtime DAT. A05 #81 exporta intervalos compatíveis para esse mesmo formato.

## A05 — guarda de origem para projeção de replay

`move-id ID SIDE SOURCE_ZONE INDEX DEST_ZONE [SLOT [position]]` é a mesma operação de setup `move`, mas exige o resource ID esperado antes de admitir. Resolve a ocorrência única na mão (ou zona suportada) e captura seu ID físico, revalidado pelo executor. Não dispara efeitos nem paga tributos/consome normal summon: é uma projeção de movimento gravado, não uma nova jogada. Usar apenas no laboratório explícito. A05.3 usa esse comando para CE/CF suportados; divergência de ID interrompe o roteiro. CI de A04.4 anterior 45322528267 verde; novo CI pendente.

## Smoke Windows 2026-11-24 — first-frame deck-out

Operator supplied private actions-console.zip; no DAT or raw logs committed. Headless ran 18 Applied commands, final turn1 Main, P0 8000/P1 7800, Mystic Tomato at slot2, both enemy monsters in GY, no winner, Script completed. App loaded DAT (3568 entries/1114 records) and raylib successfully but first executable script line2 `spawn` returned `Duel ended.`; no executed commands, YOU LOSE observed. Root cause: AdvanceDuelPhases reset to Draw, then called legacy duel.Draw before AdvanceConsoleLab, so empty lab deck lost instantly outside the queue. Fix A04: lab phase mirror only (no implicit draw/phase notices), no auto optional-effect policy in lab, legacy mode unchanged. `dotnet run --project src/Joey.App -- --check-console-startup` runs real App initialization and 120 phase passages without window/DAT, checks exact state/trace unchanged and phase gate idle; also checks legacy auto draw path. CI pending; repeat Windows smoke only after update. Runtime visual validation remains pending.

## Smoke Windows — segunda execução

Operador repetiu visual após fff5e6e. Comparação de 106 linhas de trace/state/diff com headless anterior: igualdade completa (ignorando whitespace final), 18 Applied, Script completed, fila vazia, LP 8000/7800, sem winner. Relato visual: decks vazios, ações do lado Joey imediatas, pausas em alguns pontos. Decks vazios são próprios do roteiro anterior (P0 uma carta, P1 zero); spawn é edição de setup, não uma invocação animada; não houve mudança de turno. Ainda não afirmar paridade visual nem ausência de stalls: log anterior não tinha tempos. Novo scripts/fixtures/duel-console-two-sides-smoke.txt: cinco cartas em cada deck, três draws por lado, summon ativo, setup explícito dos monstros adversários, Sparks/Raigeki; dois decks não vazios no final. [lab timing] ms monotônicos para Started/Completed; [lab wait] a cada 5s sem progresso com causas de bloqueio, exceto pausa voluntária/decisão. Diagnósticos não alteram trace/state e não são timings nativos. Repetir headless e gráfico com novo roteiro; confirmar compra nos dois lados e duração/ordem das animações, sem confundir setup com jogada. Não mergear até nova evidência visual.

## A06 — Ativação com alvo explícito

- `activate hand INDEX SIDE field` escolhe a zona de campo indicada.
- `activate set INDEX SIDE spell SLOT` escolhe a zona de magia/armadilha.
- `hand` e `set` aceitam ambas as formas de alvo. Os lados e slots começam
  em zero. O lado da fonte continua sendo o jogador ativo.

O mesmo console App/Lab resolve fonte e alvo como ocorrências da fila.
A admissão aplica as regras existentes de legalidade e idade do Set. A
execução revalida as duas ocorrências antes de gastar a carta. Um alvo
substituído antes da ativação cancela a ação sem gastar a fonte. A forma
sem alvo continua usando o fluxo anterior.

## A06 — Seleção pendente para a mão

`choices` mostra o dono, a origem e os índices das ocorrências elegíveis
para as buscas Deck → Mão e recuperações Cemitério → Mão já migradas.
`select-hand INDEX` responde à escolha atual e
`select-hand decline` recusa uma busca opcional. Os índices são posições
da carta na zona de origem. O console resolve a ocorrência e deixa o
produtor do Core revalidar pedido, dono e filtro.

O roteiro admite leituras enquanto espera e retoma as operações depois da
resposta. Um EOF com seleção pendente produz trace incompleto. O App usa as
mesmas permissões de entrada para responder pelo console de laboratório.
