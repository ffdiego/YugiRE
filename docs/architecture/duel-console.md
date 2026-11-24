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
