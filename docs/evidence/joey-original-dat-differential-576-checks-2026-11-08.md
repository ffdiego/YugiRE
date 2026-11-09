# Validação diferencial com o DAT original: 576/576 checks

**Data:** 2026-11-08.
**Fonte:** log PowerShell fornecido pelo operador em `Texto colado(8).txt`, executado no Windows.
**Escopo:** 67 fixtures `auto-g*.json` da execução `parallel-7yq78u8y`,
gerados a partir de dez duelos automatizados do Joey original.
**Execução .NET:** no computador do operador; não há SDK .NET nesta sessão.

## Comando executado

```powershell
$env:YUGIRE_DIFFERENTIAL_FIXTURES = (
    Resolve-Path ".\scratch\differential-parallel-7yq78u8y"
).Path
try {
    dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj -- "..\passion2\data.dat"
} finally {
    Remove-Item Env:YUGIRE_DIFFERENTIAL_FIXTURES -ErrorAction SilentlyContinue
}
```

O Git fez fast-forward de `51efce1` para `d038c4d`.
A saída encerrou com **`576 checks passed.`**, sem falhas relatadas.

## Origem da contagem

| Grupo | Checks |
|---|---:|
| Suíte anterior sem DAT, incluindo dois fixtures manuais e 67 automáticos | 351 |
| Identidade `CE resourceId == Card.Id` no DAT original (67) | 67 |
| Elegibilidade básica para invocação normal sem tributos com a carta REAL (67) | 67 |
| Reexecução do `Summon` com o catálogo original e projeção observada (67) | 67 |
| Checks adicionais que acessam recursos do DAT original (menus, índices, sprites, catálogo, etc.) | 24 |
| **Total** | **576** |

O bloco de **201** verificações com DAT real é particularmente útil:
cada uma das 67 observações foi reexecutada após carregar
`Catalog.Load(archive)` de `data.dat`, sem propriedades
substitutas para cartas da mão/campo naquele segundo passe.
O código confrontou `Card.Id`, o gate
`Card.IsMonster && Level in 1..4 &&
!NormalSummonRules.RequiresSpecialSummon(card)`, e a projeção
depois de `DemoDuel.Summon`.

O log também confirmou os hashes de recursos originais
`card_id.bin` e `card_nameeng.bin`, e verificações de arte,
decks, paginação e propriedades básicas.

## Conclusão e limites

**Demonstrado pelo experimento:** o pipeline
Joey nativo → YugiLab/Frida → fixtures JSON → `DemoDuel` C#
→ observações de mão/campo/LP funciona em dez duelos;
todos os 67 episódios de invocação aceitos coincidiram
usando os registros originais do catálogo.

**Não demonstrado:** universo das 1114 cartas, 67 regras
distintas (os fixtures contêm apenas **18 índices internos
distintos**), regras de tributo no Joey vivo, rejeições
nativas e negativos, efeitos/correntes, todas as condições
dinâmicas de elegibilidade, combate ou estado completo
de duelo/IA. Os snapshots nativos são observações
em pontos separados da execução, não transações atômicas.

### Próxima prioridade técnica

Antes de aumentar o volume de duelos idênticos:
1. Extrair **um inventário de nomes, níveis e classes**
   das 18 cartas observadas, usando `data.dat` em vez
   de inferir propriedades do resource ID.
2. Planejar decks que apresentem monstros nível 5–6, 7+
   e cartas com restrições nativas; medir a diversidade
   de comportamentos, não apenas número de checks.
3. Instrumentar **gates de elegibilidade** e
   **tributos** em probes separados, mantendo retorno bruto,
   caller, estado observado e negativas explícitas.
4. Só considerar paridade nativa negativa quando ela tiver
   sido observada no executável original, nunca deduzida
   de um teste negativo sintético em `DemoDuel`.

## Proveniência

Este relatório se baseia no log de console fornecido
pelo operador e nos 67 fixtures ZIP fornecidos anteriormente.
O arquivo `data.dat` não foi enviado e não foi replicado
nesta sessão; os 201 checks com DAT original são resultados
reportados pelo `Joey.Tests` no Windows.
