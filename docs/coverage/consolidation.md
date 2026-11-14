# Registry consolidado — revisão c93b52a

Baseline explícito: `c93b52a` (hash completo em consolidated.annotations.json).
Os overlays históricos permanecem intactos e apontam para os seus baselines.
Não devem ser passados junto ao consolidado: o merge rejeita a divergência.
O schema v1 e o gerador não foram alterados; a revisão é específica destes blobs,
não uma ferramenta de migração automática de estados.

## Revisão de implementação

| Arquivo conferido em c93b52a | Comparação e conclusão |
| --- | --- |
| NativeAiAttributes.cs | Idêntico a 7e5ca13 e 0a9b443. FromPlainCard/FromResolvedContext continuam fechados a recursos 77/531, exigem manifesto resolvido e rejeitam 573/1523. EquipmentDelta(300) continua tipo15 => +300/+300; não resolve elegibilidade. |
| Demo.cs | Idêntico a 0a9b443. Diff completo contra 7e5ca13: whitelist/API de dano, seleção de alvo e mensagem; Equip, Bonus e RemoveMonster preservados. Legendary Sword continua Partial. |
| DirectDamageRules.cs | Idêntico a 0a9b443. Cinco IDs explícitos, guarda +4&4, alvo preparado obrigatório; desconhecidos Unsupported. |
| AiAttributeChecks.cs | Idêntico aos dois baselines; teste sintético, sem novo oráculo nativo. |
| Program.cs (testes) | Contra 7e5ca13 acrescenta somente DirectDamageChecks.Run; casos de espada e Sparks preservados. Idêntico a 0a9b443. |
| DirectDamageChecks.cs | Idêntico a 0a9b443; combinações sintéticas não são observação independente. |

Cada arquivo revisto tem evidence `review-*` com commit e SHA256 próprios.
As evidências antigas continuam pinadas; `history-phase1` e `history-p045`
preservam integralmente as avaliações anteriores. Os testes verificam esses
blobs, o código atual e a igualdade de todos os estados dos escopos preservados.

## Sobreposição e escopos

Cinco cartas históricas + cinco P045 = **nove identidades**, porque Sparks
é card:755 / recurso342 nos dois arquivos. A avaliação de carta inteira continua
Partial/Partial/Unknown (compreensão/implementação/validação).
O mecanismo histórico `rules.lp.sparks` foi substituído pelos três escopos P045:
comando com quantidade, preparação escalar de alvo e integração no demo.
Conservar também o mecanismo antigo contaria o mesmo dano duas vezes.
Sua avaliação anterior permanece em `history-phase1`; as perguntas históricas
foram conservadas no registro atual de Sparks.

`ai.stats.sword-delta` e `rules.equip.sword` permanecem separados: contribuição
aritmética com elegibilidade previamente resolvida versus seleção/vínculo/retirada
parcial no demo. O segundo depende do primeiro. Analogamente, os três mecanismos
P045 distinguem projeção de comando, preparação de alvo e consumo no demo;
a integração depende dos dois escalares. Compartilhar callbacks não cria escopos.

Resultado: **1114 cartas, nove anotadas, oito mecânicas, vinte vínculos**.
Identificação: nove Identified / 1105 Unknown. Compreensão e implementação:
sete Partial / 1107 Unknown. Validação integral: **1114 Unknown**.
Witch e Zombyra permanecem Unknown em implementação. Legendary Sword conserva
os estados históricos; nenhuma outra carta P025 é promovida.
ConditionalMatch de Axe Raider exige contexto declarado, não paridade integral.

## Reprodução

```bash
dotnet build YugiRE.sln --no-restore -m:1
dotnet build src/Joey.Coverage --no-restore
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/p025-consolidated-registry --baseline-commit "$(git rev-parse c93b52a)" --annotations docs/coverage/consolidated.annotations.json --coverage-report --investigation-priorities
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_consolidated_registry.py' -q
```

`registry.json`, `registry.csv`, relatórios e inventário bruto ficam no diretório
de saída. O overlay revisado é a fonte versionada reproduzível; não duplicamos
os 1114 registros extraídos no Git. Os testes com arquivos locais geram duas
vezes, comparam todos os bytes e confirmam preservação do inventário bruto.
Sem os artefatos locais, a integração é SKIP explícito; os quatro testes
sintéticos restantes continuam disponíveis. Nenhum teste executa o jogo.
