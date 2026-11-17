# Campanha nativa multi-magia v1 — laboratório por lotes

Status: PR empilhado sobre a captura de estado do PR #7. **Não requer merge na main para começar**, mas testes offline obrigatórios antes de executar Joey. Este recurso é um gerador de **candidatas DAT classe 22 disponíveis no pack Joey**, não um motor de regras.

## Correção da classificação (observações reais)
O relatório de descoberta nativa já enviado pelo operador confirma:
- **Rain of Mercy:** `internalIndex=969`, `resourceId=1315`, `nativeClass=22`, `categoryBits=0`, disponível no pack Joey.
- **Raimei:** `internalIndex=1049`, `resourceId=1007`, `nativeClass=22`, `categoryBits=0`, disponível.
- **Skull Invitation:** `internalIndex=414`, `resourceId=1306`, `nativeClass=21`, `categoryBits=2` — é **armadilha**, não magia.

A primeira versão do gerador usava erroneamente classe 21 e rejeitava os focos [969,1049]. A correção usa classe 22, preserva `categoryBits` no manifesto (tipos diferentes de magia) e valida o caso Rain/Raimei em teste de regressão. A classe 21 continua fora do lote. O `categoryBits` por si só não comprova o efeito da carta nem a ativação.

## Por que não usar o antigo piloto P045
O script histórico `scripts/prepare_effect_pilot.py` conhece cinco magias de dano: Sparks, Hinotama, Final Flame, Ookazi, Raimei. O runbook original `docs/research/native-effect-pilot-v1.md` confirma que **somente Raimei estava disponível no pack Joey** daquele ensaio. Não presumir que cartas do Yugi/Kaiba aparecem no Joey.

A seleção atual filtra o registry/attributes pelo fingerprint dos arquivos do jogo, exige `Joey=true`, `nonMonsterClass22` e resource IDs **unívocos**. Um mesmo resource ID compartilhado por índices diferentes impede atribuição individual de CF/CE; ambas as identidades são excluídas automaticamente e listadas no plano.

## Preparar campanha offline

No Windows, na branch `feature/native-multi-spell-campaign-v1`, com os três arquivos já gerados no laboratório:

```powershell
git fetch origin
git switch feature/native-multi-spell-campaign-v1

python -m unittest discover -s yugilab/tests -p test_prepare_spell_batch.py -v
python -m unittest discover -s yugilab/tests -p test_state_interactions.py -v
python -m unittest discover -s yugilab/tests -p test_discovery.py -v

python -m yugilab.prepare_spell_batch `
  --registry .\scratch\coverage-current\registry.json `
  --attributes .\scratch\coverage-current\attributes.json `
  --reviewed-normals .\scratch\pool-joey-sandbox.json `
  --focus-index 969 `
  --focus-index 1049 `
  --spells-per-deck 10 --copies-per-spell 2 `
  --rounds 3 --seed 12345 `
  --output-dir .\scratch\multi-spell-decks-v1

Get-Content .\scratch\multi-spell-decks-v1\spell-batch-plan.json
```

O gerador verifica **duas cópias de dez índices distintos por lado, vinte monstros normais revisados**: 40 cartas, nenhum índice repetido mais de três vezes. Na primeira rodada, Rain of Mercy (`969`) e Raimei (`1049`) serão fixas nos dois decks; as outras oito candidatas de classe 22 são rotacionadas por lado/rodada, com nomes e resource IDs listados em `spell-batch-plan.json`. Não executar o jogo se qualquer ID obrigatório não for elegível no catálogo, se faltar pool normal, ou se houver ambiguidade. Ajustar os índices de foco **somente após conferir o catálogo**.

## Piloto de 1 duelo

Backup dos saves e processo original com fingerprint; não reutilizar diretórios existentes:

```powershell
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab `
  --exe ..\passion\joey_pc.exe `
  --duels 1 --workers 1 --trace-state `
  --deck-0 .\scratch\multi-spell-decks-v1\spell-round-01-A.json `
  --deck-1 .\scratch\multi-spell-decks-v1\spell-round-01-B.json `
  --seed 12345 --speed-value 600 `
  --output-dir .\scratch\native-multi-spell-r01-one-v1
```

Critério: `complete=true`, 1 terminal, integridade true, `overwritten=0`, ambas as injeções de deck verificadas, `stateTrace.samples > 0`, `baselines >=1`, `readErrors=0` ideal. Os snapshots são **não atômicos e observados na entrada** do produtor de comandos. O estado do jogo pode mudar entre as observações; CF/CE não demonstra resolução da magia.

## Relatórios: várias cartas numa única captura

```powershell
python -m yugilab.discovery analyze `
  --registry .\scratch\coverage-current\registry.json `
  --attributes .\scratch\coverage-current\attributes.json `
  --input .\scratch\native-multi-spell-r01-one-v1 `
  --card-text-expectations .\docs\research\card-text-expectations.v1.json `
  --output-dir .\scratch\multi-spell-queue-r01-v1

python -m yugilab.state_interactions `
  --input .\scratch\native-multi-spell-r01-one-v1 `
  --output-dir .\scratch\multi-spell-states-r01-v1

$r = Get-Content .\scratch\multi-spell-queue-r01-v1\discovery-report.json -Raw | ConvertFrom-Json
$r.perCard | Where-Object { $_.group -eq 'nonMonsterClass21' -and $_.verifiedDeckBatches -gt 0 } |
  Select-Object name, internalIndex, resourceId, verifiedDeckBatches, cfCommands, ceCommands, evidence |
  Format-Table -AutoSize
```

Esse relatório identifica **exposição no deck**, **comandos associados** e **transições genéricas de estado**. A ausência de CF não significa que a magia não funcionou: ela pode não ter sido comprada, jogada ou ter seguido caminho sem CF do tipo considerado. Tampouco se deve mapear alterações em LP/campo ao CF por simples proximidade.

Somente depois de analisar essa captura de 1 duelo: até **3 duelos por lote, 1 worker**, em diretório novo, respeitando o limite atual de `--trace-state`. Rodadas 2 e 3 utilizam `spell-round-02-A/B.json` e `spell-round-03-A/B.json` para aumentar diversidade. Não elevar worker count nem instalar hooks por carta.

## Meta de cobertura

Investigação exploratória de até 18 identidades de magia por rodada (2 focos em ambos os lados, mais 8 rotacionados por lado, se houver candidatas suficientes) e mais nas rodadas seguintes. O número real de identidades distintas está no plano (`distinctSpellIndicesAcrossDecks`). Comparar famílias: LP/dano/cura, movimentação ao Cemitério, campo contínuo, seleção de alvo. **Nenhuma carta é automaticamente considerada validada em C#**.
