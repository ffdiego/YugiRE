# Cobertura de 67 fixtures diferenciais de invocação — execução Windows

**Data:** 2026-11-08.
**Fonte auditada:** ZIP recebido do operador, `differential-parallel-7yq78u8y.zip`.
Contém 67 fixtures `auto-g*.json`, um `export-summary.json` e nenhum
JSONL bruto do Frida. O relatório é uma inspeção offline dos JSON exportados
pelo YugiLab, não uma reexecução nativa independente dos eventos de origem.

## Integridade e limites

- 67 arquivos de fixture, 67 eventos marcados observados pelo resumo,
  67 exportações e 0 transições rejeitadas pelo exportador.
- 10 índices globais de duelo: 0–9, todos presentes.
- 18 **índices internos de cartas diferentes** nas invocações;
  18 associações consistentes `internalIndex → resourceId`.
  Nenhum índice teve mais de um ID de recurso no ZIP.
- **Todos os 67** `action.nativeLevelClass` e
  `action.nativeEligibility` são `null`.
  Portanto nenhum fixture, sozinho, comprova a legalidade da
  invocação de uma carta original.
- Os 67 cenários são invocações aceitas e observadas; **não há
  casos negativos** de summon inválida nem cenários de tributo
  ou efeitos especiais. O gate do hook exclui esses casos.
- `before` e `after` são observações em pontos diferentes
  da thread do Joey, não snapshots atômicos.

## Distribuição

| Dimensão | Ocorrências |
|---|---|
| Lado 0 | 30 |
| Lado 1 | 37 |
| Slot 0 | 26 |
| Slot 1 | 18 |
| Slot 2 | 11 |
| Slot 3 | 10 |
| Slot 4 | 2 |
| 0 monstros próprios antes | 27 |
| 1 monstro próprio antes | 27 |
| 2 monstros próprios antes | 10 |
| 3 monstros próprios antes | 3 |
| 1 carta na mão antes | 4 |
| 2 cartas na mão antes | 9 |
| 3 cartas na mão antes | 18 |
| 4 cartas na mão antes | 16 |
| 5 cartas na mão antes | 17 |
| 6 cartas na mão antes | 3 |
| LP abaixo de 8000 | 25 |
| LP iguais a 8000 | 11 |
| LP acima de 8000 | 31 |

LP observado varia de **600** até **13.100**, e permaneceu
igual entre a observação inicial e final de cada um dos 67 eventos.

### Cartas internas e IDs de recurso

| Índice interno | Resource ID observado | Invocações |
|---:|---:|---:|
| 100 | 44 | 2 |
| 151 | 99 | 1 |
| 385 | 1117 | 6 |
| 525 | 63 | 8 |
| 591 | 1351 | 2 |
| 630 | 15 | 3 |
| 700 | 1443 | 1 |
| 828 | 607 | 4 |
| 834 | 3 | 6 |
| 862 | 141 | 1 |
| 868 | 113 | 1 |
| 885 | 737 | 6 |
| 886 | 800 | 6 |
| 887 | 756 | 9 |
| 1038 | 362 | 2 |
| 1052 | 1401 | 1 |
| 1093 | 1231 | 1 |
| 1099 | 760 | 7 |
| **Total** | **18 combinações distintas** | **67** |

Há **dois casos** em que a carta invocada aparecia mais
de uma vez na mão inicial (índices internos 385 e 1099).
A comparação usa a **posição na mão** e a sequência completa
dos demais índices, não apenas uma diferença entre conjuntos.

### Quantidade por índice global do duelo

| Índice | Fixtures |
|---:|---:|
| 0 | 8 |
| 1 | 11 |
| 2 | 6 |
| 3 | 9 |
| 4 | 12 |
| 5 | 6 |
| 6 | 3 |
| 7 | 2 |
| 8 | 4 |
| 9 | 6 |
| **Total** | **67** |

## Próximo estágio — utilizar cartas reais

O ZIP **não contém** os atributos `card_prop.bin` nem as
identidades completas dos cards originais. Não é possível
inferir níveis, tipos ou restrições especiais a partir do
resource ID ou do resultado da invocação.

O YugiRE agora possui um caminho **opcional** no harness C#,
`DifferentialNormalSummonTests.RunWithCatalog`.
Quando são fornecidos simultaneamente:
1. `YUGIRE_DIFFERENTIAL_FIXTURES` com os 67 fixtures locais;
2. o caminho do `data.dat` original como primeiro argumento
   de `Joey.Tests`;

a suíte carrega `Catalog.Load(archive)` e, para cada fixture,
verifica:
- o `Card.Id` real do índice da carta contra o
  `resourceId` gravado pelo CE nativo;
- o gate normal do protótipo (monstro, nível 1–4,
  não requer special summon);
- o resultado de `DemoDuel.Summon` usando **as cartas
  originais** em toda a mão e slots conhecidos.

Esses checks **ainda não foram executados no Windows**.
Uma falha deve ser tratada como evidência de discrepância
ou limitação no gate/modelo do demo, não mascarada com
um surrogate nível 4.

Os checks anteriores (150 base e 201 dos 67 fixtures)
continuam funcionando sem `data.dat`. O DAT não precisa
ser enviado nem colocado no repositório.

Comando proposto, na raiz do repo, após definir
`YUGIRE_DIFFERENTIAL_FIXTURES`:

```powershell
dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj -- "..\passion2\data.dat"
```

Use o **caminho real** do DAT instalado; o exemplo acima
é apenas ilustrativo.

## Próxima expansão recomendada

1. Obter diversidade real de cartas através de novos
   decks; 67 movimentos **não** são 67 regras distintas.
2. Comparar atributos e legality gate a partir do DAT.
3. Instrumentar **tentativas rejeitadas**, níveis 5/6,
   tributos e sets, separadamente do pipeline atual.
4. Só depois avançar a aplicação de estados/ações de
   invocação para uma API de domínio `GameAction` /
   `GameState` mais abrangente.
