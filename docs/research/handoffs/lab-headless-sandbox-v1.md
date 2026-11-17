# Handoff — primeiro runner headless do sandbox C#

Branch: `feature/headless-duel-sandbox-v1`, criada a partir de `main` após PR #3. Escopo solicitado explicitamente pelo operador: passar a produzir duelos automáticos C# com um caminho conservador até um pool de 100 cartas. **Não** é continuação da instrumentação de combate do PR #1.

## Arquivos e limites

- `src/Joey.Lab/SandboxDuelRunner.cs`: `duel-sim` headless que usa apenas `DemoDuel`, política simples, opção fixture sem DAT, execução com manifesto revisado e trilha JSONL determinística. Identificadores, slots, LP e hashes; nenhum hook/injeção nativa.
- `src/Joey.Lab/SandboxPoolCandidates.cs`: `pool-candidates` gera arquivo local não executável com até N candidatos estruturais do DAT. Não aprova efeitos ou cartas por inferência.
- `src/Joey.Lab/Program.cs`: comandos adicionais no CLI; não modifica subcomandos anteriores.
- `src/tests/Joey.Tests/SandboxDuelChecks.cs` e registro em `Program.cs`: repetibilidade, eventos, escopos e rejeições de manifesto.
- `docs/research/headless-sandbox-duels-v1.md` e `README.md`: comando Windows e fronteira de evidência.

Sem alterações em `Joey.Core`, `native_hooks/`, `yugilab/`, `PLANS.md` ou registry. Nenhum EXE, DAT ou dado privado incluído.

## Escolhas deliberadas

1. O pool tem identidade por `internalIndex` e `resourceId`; as entradas recebem política `BaseMonsterSandbox` ou `DirectDamageSandbox`, nota do operador e `mode: SandboxPartialOnly`. Sem isso a CLI rejeita.
2. `pool-candidates` emite `UnreviewedCandidates`, sem notas, por padrão. Não assume que classe/nível/ausência de gate de special summon signifique ausência de efeitos.
3. Os dois decks têm 40–80 cartas, máximo de três cópias por índice, contidas na whitelist.
4. Por ora a política só executa regras básicas com monstros até nível 4 e magias de dano P045. Equipamentos P025, monstros com efeitos, tributos e correntes não são processados. Um jogador pode vencer por esgotamento de deck segundo as regras do demo.
5. A saída nunca é representada como nativa: `nativeParity:false`, `origin` e estados antes/depois, inclusive ativo/turno/fase. Hash SHA256 dos mesmos bytes de manifesto usados no parse; o DAT é identificado por SHA256 de leitura local.
6. O formato ainda não equivale a um dataset de transições *nativas*. O próximo passo real é traduzir observações do Joey com contexto suficiente e comparar campo a campo.

## Validação executada versus pendente

A revisão em tempo de autoria inspecionou as assinaturas públicas de `DemoDuel`, `Card`, `Catalog`, `NormalSummonRules`, `DirectDamageRules` e os caminhos do CLI/testes no GitHub. **Não foi possível executar .NET neste ambiente**, que não tem SDK, DAT local ou clone Git acessível para build. Não declarar qualquer teste PASS ou existência de 100 cartas aprovadas.

Validação requerida no Windows (pelo operador):

```powershell
git fetch origin
git switch feature/headless-duel-sandbox-v1
dotnet build .\YugiRE.sln -m:1
dotnet run --project .\src\tests\Joey.Tests --no-build -- ..\passion\data.dat
dotnet run --project .\src\Joey.Lab --no-build -- duel-sim --fixture --duels 10 --seed 12345 --output .\scratch\sandbox-10-a.jsonl
dotnet run --project .\src\Joey.Lab --no-build -- duel-sim --fixture --duels 10 --seed 12345 --output .\scratch\sandbox-10-b.jsonl
Get-FileHash -Algorithm SHA256 .\scratch\sandbox-10-a.jsonl, .\scratch\sandbox-10-b.jsonl
dotnet run --project .\src\Joey.Lab --no-build -- pool-candidates --dat ..\passion\data.dat --count 100 --output .\scratch\pool-candidates-100.json
```

**Nota sobre deck de referência:** `DeckTests.Original` espera exclusivamente uma fixture YDC de contagens `42 / 1 / 0` e IDs fixos. Não usar um `deck.ydc` arbitrário no comando de testes: passar apenas `data.dat` mantém os testes de catálogo e os novos testes do runner sem essa fixture específica.

**Primeira execução reportada pelo operador:** 320 checks passaram antes de a suite parar em `Reference YDC counts are 42 / 1 / 0` ao fornecer um deck não correspondente. Não é ainda evidência de que os testes do runner passaram; repetir sem o argumento YDC.

Os hashes dos dois JSONL sintéticos devem coincidir. O arquivo de candidatos tem `mode=UnreviewedCandidates` e notas vazias, e **deve falhar** se passado diretamente a `duel-sim --manifest`. Duelos reais com pool revisado ainda não foram executados.

Ponto de parada: alterações commitadas em branch para revisão. Não realizar merge automático, não disparar Frida e não promover nenhum estado de cobertura sem evidência apropriada.
