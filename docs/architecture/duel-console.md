# A04.1 — Console de duelo headless (piloto)

O console é um produtor de laboratório explícito. DuelConsoleSession no Core cria a fila única do duelo, faz parsing sem mutar zonas/LP, resolve ocorrência no slot e admite comandos com ActionOrigin.Console. O host dirige Tick; não há relógio nem raylib no parser. Não habilitar em partida normal sem opt-in.

```sh
dotnet run --project src/Joey.Lab -- duel-console --fixture
dotnet run --project src/Joey.Lab -- duel-console --fixture scripts/fixtures/duel-console.txt
```

O primeiro frontend aceita stdin ou arquivo e usa fixture sintética, não DAT; não exige exe. Exemplo: state, draw 2, destroy 1 0, damage 300 1, heal 100 0, diff. Lados 0/1 (default 0); slots de monstro 0–4. draw gera N Draw(1); damage/heal são delta único; destroy resolve ID de ocorrência antes de admitir e executor revalida. state registra baseline somente na sessão, diff compara sem alterar duelo. decision ID accept|decline responde apenas ao scheduler. Comando admitido não significa aplicado; runner imprime resultado do executor. Consultas permanecem acessíveis durante gate. Não há execução de código arbitrário.

Testes DuelConsoleChecks: admissão inválida sem trace, busy, mesmo roteiro com gate imediato/retardado, estado e trace iguais, stdin. Próximas fatias: DAT/deck real, spawn/move tipados e validação, invocação/Set, escolhas legadas e fases (A07), integração App/gate A03, adaptador comparável ao nativo A05. State/diff atual são formato diagnóstico do sandbox, não schema nativo. A04 permanece parcial; não há console no App ainda.
