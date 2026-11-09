# YugiLab → Joey.Core: primeira execução Windows com fixtures diferenciais automáticos

**Data:** 2026-11-08. **Fonte:** saída do PowerShell fornecida pelo operador em `Texto colado(7).txt`, execução no Windows. Resultados extraídos do log; não foi recebido o ZIP/JSONL nativo dessa rodada para reauditoria independente.

## Execução do jogo original

Comando informado:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab --exe "..\passion2\joey_pc.exe" --duels 10 --workers 2 --seed 12345 --deck-0 ".\native_rules\decks\demo-A.json" --deck-1 ".\native_rules\decks\demo-B.json" --trace-ai --speed-value 600
```

- Execução paralela: **10 duelos / 2 workers**, partição **[5,5]**.
- Pasta de execução: `scratch/labs/parallel-7yq78u8y`.
- Workers Python: PID 10904 e PID 22064.
- Ambos encerraram com `exit 0`.
- Status reportado pelo YugiLab: **`10/10 verified duels. Complete=True`**.

## Exportador

Comando: `python -m yugilab.export_summons <run> --output-dir <scratch/differential-run>`.

Saída literal relevante:

```
[yugilab.export_summons] 10 complete duels, 67 observed transitions, 67 exported fixtures, 0 rejected; output=scratch\differential-parallel-7yq78u8y
```

Os 67 fixtures gerados passam pelo gate do exportador: referência ao evento de entrada do executor, `0xCE` nativo, identidade da carta, projeções compatíveis antes/depois de mão e campo e flag de invocação consumida. Os **0 rejeitados** referem-se a eventos `summon_transition` emitidos, não a todos os eventos do duelo; o hook já filtra candidatos incompletos.

## Resultado no Joey.Tests

O operador configurou `YUGIRE_DIFFERENTIAL_FIXTURES` e executou:

```powershell
dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj
```

Resumo final observado: **`351 checks passed`**; nenhum `FAIL`, `ERROR` ou exception relatada.

Decomposição auditável na própria saída:
- **150** checks preexistentes, incluindo 2 fixtures diferenciais manuais;
- **67 fixtures automáticos distintos × 3 checks = 201** checks novos;
- total **351** checks.

Os três checks por fixture são: `DemoDuel` remove exatamente uma carta da mão; projeção do resultado coincide com os campos nativos observados (ordem de mão, slot e cartão, LP se disponível); controle negativo (trocar a carta esperada) falha como deveria.

### Cobertura por índice global do duelo

| Índice global | Fixtures automáticos | Checks |
|---:|---:|---:|
| 0 | 8 | 24 |
| 1 | 11 | 33 |
| 2 | 6 | 18 |
| 3 | 9 | 27 |
| 4 | 12 | 36 |
| 5 | 6 | 18 |
| 6 | 3 | 9 |
| 7 | 2 | 6 |
| 8 | 4 | 12 |
| 9 | 6 | 18 |
| **Total** | **67** | **201** |

Todos os dez índices globais apresentaram pelo menos uma invocação exportada. São **67 casos observados**, não necessariamente 67 tipos distintos de carta ou de regra.

## Conclusão delimitada

A cadeia **Joey nativo → Frida/YugiLab → exportação JSON automática → projeção de ação em `Joey.Core` → testes C#** foi comprovadamente executada com sucesso no Windows em 10 duelos, 67 fixtures e 351 checks.

Isso **não prova**:
- equivalência de duelos inteiros, IA ou decisões de deck;
- legalidade completa da invocação de cada carta original;
- regras de tributo, efeitos especiais e sincronização atômica de todas as mudanças;
- cobertura de casos negativos (invocação inválida, falta de tributos etc.).

O harness ainda usa **surrogates de monstro comum nível 4** para cartas cujos atributos não fazem parte do fixture. Testa a movimentação observada, não a classificação verdadeira ou as restrições de cada carta.

## Próximas prioridades

1. Acrescentar aos fixtures as **propriedades reais da carta** do catálogo original, substituindo o surrogate para que o teste valide também `NormalSummonRules`.
2. Adicionar um relatório de cobertura (índice global, lado, slot e índices de cartas distintos), para não confundir volume de fixtures com diversidade de cenários.
3. Ampliar as capturas diferenciais para **tributos** e **combate**, com critérios rigorosos para campos conhecidos e ações ambíguas.
4. Quando possível, arquivar ou fornecer o ZIP `parallel-7yq78u8y` para validar a consistência interna da execução além do resumo de console.

**Observação metodológica:** a confirmação `351 checks passed` veio do usuário. Os arquivos JSONL individuais não foram entregues nesta rodada, portanto não foi executada recontagem direta das transições nativas nem inspeção do conteúdo das cartas.
