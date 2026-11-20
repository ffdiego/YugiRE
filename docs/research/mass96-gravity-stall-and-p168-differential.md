# mass96 — Gravity Bind, deck exhaustion e efeitos P168

**Fonte:** captura `mass96-pilot-36.zip`, SHA256
`12f518fac309b76bacc998e69c5d122bd79fc5ce95434c64ed0bdb6ae51e64b1`,
36 duelos / 12 coortes. Este documento distingue **observações no Joey
original**, **regras de carta conhecidas** e **hipóteses de implementação**.
Captura é privada e não é versionada; usa índices/seqs para localizar
episódios no ZIP original. Sem novas execuções nativas neste PR.

## 1. O viés introduzido por Gravity Bind

Gravity Bind = recurso **1323**, índice DAT **244**, armadilha contínua.
O texto da carta diz que monstros de **nível >= 4 não podem atacar**.
Isso é consistente com a observação do operador de que a IA ficou
travada com vários monstros nível 4 e terminou os duelos por falta de
compra. Não consideramos esse texto prova do momento nativo de
ativação/desativação.

### Eventos nativos efetivamente registrados

Coorte 11 (cada deck 3x Gravity Bind, Magic Drain, Revival Jam,
Goblin Attack Force etc.):

| Duelo | `0x0072` Gravity Bind | Seq terminal | LP lado 0 / lado 1 | Deck lado 0 / lado 1 | Terminal flag lado 0 |
| --- | ---: | ---: | ---: | ---: | ---: |
| 1 | **177** | 1026 | 7300 / 7600 | **0 / 0** | 256 |
| 2 | **1130** | 1968 | 7400 / 7500 | **0 / 0** | 256 |
| 3 | **2054** | 2981 | 7300 / 6800 | **0 / 0** | 256 |

As três ativações são comandos de efeito `0x0072` com resource
ID baixo16 **1323**. O estado amostrado mostra decks vazios e LP
positivos no comando terminal `0x0004`. Em contraste, os **33 duelos
das outras 11 coortes** possuem ao menos um lado com LP=0 na
observação terminal, sem o mesmo padrão de decks duplamente vazios.

**Classificação correta:** `both-decks-empty-positive-lp`. O bit
`terminalFlags=256` não está ainda decodificado como uma causa
nativa de derrota, por isso não elevamos para `Verified deck-out`.
Também não existe um log de cada tentativa de ataque bloqueado;
não afirmar que Gravity Bind foi causa exclusiva sem comparação
controlada.

`scripts/analyze_mass96_outcomes.py` reconstitui esses estados
diretamente do ZIP, por baseline e deltas de `stateObservation`.
Identificação de efeito exige **`opcodeMasked=0x0072`** com
resourceId 1323; estar no deck ou ser colocado Set não basta.
Tests: `tests/test_mass96_native_outcomes.py`.

### Experimento A/B adicional para isolar o viés

Preparados dois decks em
`native_hooks/decks/mass-96/controls/cohort-11-no-gravity-{A,B}.json`.
Comparados a `cohort-11-{A,B}.json`, a **única alteração de cada deck**
são as três ocorrências de Gravity Bind: cada uma vira uma cópia
adicional de um dos controles históricos internos 151, 241 e 525
(antes 2x, depois 3x). Os **outros sete alvos seguem 3x nos dois
jogadores**; o deck tem 40 cartas e não excede 3 cópias por índice.

Com a branch `research/native-stall-p168-fixtures-v1` no Windows:

```powershell
python -m unittest discover -s tests -p test_mass96_gravity_control.py -v
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion\joey_pc.exe" --duels 3 --workers 1 `
  --trace-state --seed 21361661 --speed-value 600 `
  --deck-0 ".\native_hooks\decks\mass-96\controls\cohort-11-no-gravity-A.json" `
  --deck-1 ".\native_hooks\decks\mass-96\controls\cohort-11-no-gravity-B.json" `
  --output-dir ".\scratch\mass96-c11-no-gravity"
```

O seed **21361661** é o mesmo da coorte 11 do piloto
`campaign-runs.json`. Isso constitui uma comparação *pareada na
configuração*, não um experimento em que a sequência de draws/actions
permanece idêntica: ao trocar uma carta, a IA e o RNG podem divergir.
Se a captura terminar com zero perda, `complete=true`, estado válido
e dois overrides `verified` por duelo, comparar LP/decks/turnos/
comandos. O resultado **não foi executado neste PR**.

## 2. P168 — Giant Germ e Nimble Momonga

`src/tests/Joey.Tests/Fixtures/p168-mass96-native-episodes.json`
preserva **quatro ocorrências nativas**, duas por efeito, com ambos
os lados representados.

| Carta, coorte, lado dono | Seq efeito | Delta de LP observado | Seq das 2 invocações | Redução de deck | `positionWord` bruto |
| --- | ---: | ---: | --- | ---: | ---: |
| Giant Germ C02, lado 1 | 716 | LP lado 0: 7400 → 6900 (-500) | 721, 724 | 32 → 30 | 514, 514 |
| Giant Germ C02, lado 0 | 758 | LP lado 1: 7600 → 7100 (-500) | 763, 766 | 32 → 30 | 514, 514 |
| Nimble Momonga C03, lado 0 | 133 | LP lado 0: 7600 → 8600 (+1000) | 137, 139 | 34 → 32 | 513, 513 |
| Nimble Momonga C03, lado 1 | 1076 | LP lado 1: 8000 → 9000 (+1000) | 1080, 1082 | 32 → 30 | 513, 513 |

As transferências `0x0092` mostram duas cópias
**internalIndex=383** (Germ) ou **453** (Momonga)
saindo do Main Deck e ocupando zonas diferentes.
Os `positionWord` nativos foram mantidos como números brutos;
a associação 514=ATK face-up e 513=DEF face-down permanece hipótese
sujeita à confirmação dos bits de posição e do visual original.
A origem do efeito é uma destruição em batalha observada no
episódio (os monstros saem antes do `0x0072`).

`NativeP168EvidenceChecks.cs` reproduz, em cenário controlado
com duas cópias elegíveis e batalha válida no C#, **apenas os deltas
LP + duas invocações + postura esperada**. Isso são verificações
*parciais vinculadas à evidência*, não replay exato da IA,
seleção de cartas, ordem de zonas, shuffle do Main Deck, cadeia
e timing do binário. **P168 continua `Done (sandbox)`,
não `Verified`.**

## Próximo corte da pesquisa

1. Capturar o A/B sem Gravity Bind e comparar os desfechos.
2. Usar os quatro P168 como base de novas fixtures diferenciais:
   seleção 0/1/2, capacidade de campo, alvo e ordem de efeito.
3. Instrumentar decodificação da posição face-up/face-down e da
   flag nativa de deck-out antes de promover hipóteses.
4. Desenhar sistema de eventos e chains LIFO sobre as janelas
   existentes; não inferir prioridade só por sequência de um episódio.
