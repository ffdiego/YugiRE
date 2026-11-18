# Sogen — protocolo de controle negativo dos atributos nativos (17/11/2026)

**Status:** protocolo preparado; **nenhum novo duelo executado ou resultado inferido**. PR #9 permanece Draft. Este procedimento complementa `sogen-battle-attribute-pilot-validated-2026-11-17.md`, que já atestou 3/3 duelos, 69 amostras de `0x31`, 0 erros e seis observações de +200/+200 em classe nativa 15 com Sogen em campo.

## Pergunta verificável

Se retirarmos **Sogen (índice interno 490, recurso 332)** dos dois decks experimentais, os mesmos monstros da classe nativa 15 observados em combate continuarão mostrando delta `+200/+200` em relação ao DAT? A hipótese nula de trabalho é que a diferença pode decorrer de **outro modificador não medido**. O objetivo do contraste é reduzir essa possibilidade, **não** pressupor que a seed produza duelos idênticos depois de alterar o deck.

No positivo, o monstro interno 147 mostrou **800/800 sem Sogen** (duelo 2 seq943) e **1000/1000 com Sogen** (duelos 1/3 seq507/1337). A nova captura deve procurar especificamente 147, 151, 1052 e 24, sem restringir as conclusões a essas identidades. O tipo nativo 15 tem suporte estático como Warrior em `docs/cards/legendary-sword.md`; **Beast-Warrior não foi isolado**.

## Preparação — Windows, arquivos locais em scratch/

1. Partir dos decks usados no positivo, `scratch/multi-spell-decks-v1/spell-round-03-A.json` e `spell-round-03-B.json`, preservando os originais. Criar cópias em `scratch/sogen-control-decks-v1/` com os mesmos nomes.
2. Em **ambos** os novos decks, substituir **cada ocorrência** de índice `490` no `main` por outro índice interno válido do DAT original, sem efeitos conhecidos sobre ATK/DEF ou magia de campo. **Não escolher o substituto pelo resource ID**. Registrar no relatório o índice, nome, frequência e por que foi escolhido. Não supor que um índice específico é inerte sem verificação.
3. Preservar exatamente a quantidade e a ordem das demais cartas nos decks. Confirmar que `490` não restou no `main` (nem em `second`/`third`, se esses forem usados) de A **ou** B. Confirmar deck válido pelo `native_hooks/deck_config.py`, que exige 40–80 itens no `main`, índices 1..4095.
4. Anotar **SHA256 dos quatro JSON originais/controle** e do catálogo `scratch/coverage-current/attributes.json`. Registrar que mudar uma carta altera a composição, o embaralhamento e potencialmente a IA, mesmo com a mesma seed.

## Captura de controle (sem Sogen)

Na raiz do repositório atualizado na branch `feature/native-battle-attributes-sogen-v1`:

```powershell
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab `
  --exe ..\passion\joey_pc.exe `
  --duels 3 --workers 1 --trace-state --trace-battle-attributes `
  --deck-0 .\scratch\sogen-control-decks-v1\spell-round-03-A.json `
  --deck-1 .\scratch\sogen-control-decks-v1\spell-round-03-B.json `
  --seed 12345 --speed-value 600 `
  --output-dir .\scratch\native-battle-sogen-control-r03-v1
```

O executável deve ser o Joey original com o SHA256 pré-validado pelo launcher. Após a captura íntegra, auditar **com o mesmo arquivo de atributos** utilizado no positivo:

```powershell
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab.battle_attribute_audit `
  --input .\scratch\native-battle-sogen-control-r03-v1 `
  --attributes .\scratch\coverage-current\attributes.json `
  --field-spell-index 490 `
  --output-dir .\scratch\native-battle-sogen-control-audit-r03-v1
```

Preservar o JSONL original, `battle-attributes.json`, `battle-attributes.md`, metadados/hashes e eventuais logs de falha em `scratch/`; não versionar dados brutos, decks locais nem arquivos proprietários.

## Critérios antes de interpretar

- Exigir `capture_summary.complete=true`, exatamente 3/3 duelos, `battleAttributesTrace.samples > 0`, `battleAttributesTrace.readErrors=0`, `stateTrace.readErrors=0`, `overwritten=0` e verificação de término do processo. Se não houver observações de classe15, repetir **outro** piloto com decks/seed documentados, sem forçar uma conclusão.
- No relatório do controle, verificar `fieldSpellObserved=[]` em todos os `0x31` **e também** ausência de Sogen no rastreamento de slots, não apenas ausência de seu evento de ativação. Uma amostra não atômica não prova ausência em todos os instantes; essa limitação deve ser registrada.
- Comparar, por `cardIndexWord` e `baseClassCode`, as distribuições de `calculatedMinusBaseAttack/Defense` em momentos com e sem Sogen, com atenção à postura, presença de outros equipamentos/modificadores, visibilidade da carta e `fieldMatches`. **Nunca parear o mesmo seq de dois duelos modificados como se fosse o mesmo estado.**
- Se monstros da classe15 continuarem +200/+200 sem Sogen, investigar outras origens: equipamentos, efeitos intrínsecos, outros campos, estado dinâmico. Se o delta desaparecer, isso reforça a dependência de Sogen, mas não prova toda a implementação.
- Buscar um segundo piloto **dentro do mesmo duelo** em que a mesma identidade de monstro apareça nos `0x31` antes/depois da entrada de Sogen e sem outros modificadores observados. Isso é mais discriminativo que apenas comparar runs.
- Em separado, identificar pelo DAT original um **Beast-Warrior** elegível, seu classCode nativo e uma oportunidade de combate. Não assumir classCode sem evidência; não promover o bônus ao motor C# antes deste teste ou decisão explícita de implementar somente o caso Warrior com escopo restrito.

## Integração futura

O `src/Joey.Core/NativeAiAttributes.cs` contém `NativeAiAttributeContext` com equipamentos, Shield & Sword e Mirror Wall, mas **não modela magia de campo como modificador explicitamente resolvido**. Enquanto o campo permanecer desconhecido, não declarar `Resolved` para esse contexto e não adicionar +200 de Sogen a uma tabela genérica de equipamento. Após confirmar elegibilidade/timing, propor um contexto de field spell separado, com identificador de origem, condições explícitas e fixtures nativos de regressão. `0x0056DD30` é o ponto estático documentado de recálculo; `0x31` dá um ponto de observação, não a prova de todos os ramos internos.
