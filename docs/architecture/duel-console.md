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

## Integração A04/A05 — trace estruturado

Branch de integração une #72/#77/#81 e main 431dccf preservando produtores CARDS recentes. A05.6 CI 45324763088 aprovado. `trace` é consulta somente leitura, disponível no console gráfico e headless, usa ActionTraceJson (schema comum); imprime snapshot do trace Executed, inclusive comandos setup/unmapped e estado busy/fault. Para arquivo: `dotnet run --project src/Joey.Lab -- duel-console --dat data.dat cenario.txt --trace-output=scratch/trace.json`. Exige roteiro, recusa sobrescrever, resultado de falha torna complete=false mesmo que fila esteja livre. Sem autoaprovação de regra; comparar janela explícita. App usa F10 e `trace`, JSON no stdout. Testes de query, summon real, setup, arquivo e falha. CI integrado pendente; PR de integração não substitui validação visual nem merge automático dos três marcos.
