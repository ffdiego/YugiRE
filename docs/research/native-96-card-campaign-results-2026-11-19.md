# Evidências do piloto mass96: 36 duelos, 96 cartas

**Proveniência:** ZIP `mass96-pilot-36.zip`, capturado pelo usuário no Joey original e analisado offline em 19/11/2026 (data local); SHA256 do ZIP `12f518fac309b76bacc998e69c5d122bd79fc5ce95434c64ed0bdb6ae51e64b1`. Não houve execução adicional do `joey_pc.exe` nem comparação determinística com `Joey.Core` neste relatório.

## Gates de captura

- `campaignStatus=Complete`, **12/12 runs**, **36/36 duelos concluídos**, **36/36 seeds aplicadas**.
- **22.805 eventos** (19.984 comandos), **19.320 amostras**, **36 baselines**, **5.612 deltas**, **0 erros de leitura**, `overwritten=0`. A sequência `seq` de todos os runs é contígua.
- **72 verificações de override** (3 por lado, em cada coorte), todos os `actualIndices` compatíveis com os decks em `metadata.deckPlans`.
- Cada coorte tem 8 resource IDs diferentes, **3 cópias por lado**; no total, **96 cartas** (36 monstros, 36 magias, 24 armadilhas), sem sobreposição entre coortes.

### Cobertura por *evidência*, não por paridade

| Família | Cartas | Comando de efeito `0x0072` com resource ID | Algum sinal indexado CF/CE/`0x0072`/campo | Sem sinal |
| --- | ---: | ---: | ---: | ---: |
| Monstros | 36 | 28 | 36 | 0 |
| Magias | 36 | 23 | 24 | 12 |
| Armadilhas | 24 | 21 | 24 | 0 |
| **Total** | **96** | **72** | **84** | **12** |

No conjunto há **496 comandos `0x0072`** e **81 transições confirmadas de Normal Summon** (20 monstros distintos). `0x0072` **não é contagem de ativações distintas**: efeitos contínuos/repetidos podem emitir múltiplos comandos; `CF` pode indicar colocação de carta (não ativação), e `CE` não cobre todas as Special Summons/FLIPs. Todas as 96 foram incluídas no deck; **nenhuma foi promovida para `Verified`** por este estudo.

## Episódios particularmente úteis

| Coorte | Carta | Comandos e deltas observados | Próxima validação |
| --- | --- | --- | --- |
| 01 | Raimei | 8× `0x0072` resource 1007, cada um acompanhado de `0x0043` por **300 LP** | Fixar lado/dano e compara C# |
| 01 | Rain of Mercy | 10× `0x0072` resource 1315; cada sequência inclui dois `0x0042` de **1.000 LP** | Paridade de limites de LP |
| 01 | Magic Cylinder | 7× `0x0072` resource 1214, com `0x0043` de 1.100 a 1.700 LP | Correlacionar atacante e ATK atual |
| 01 | Torrential Tribute | 5× `0x0072` resource 1425 com remoções de monstros | Corrente e ordem de destruição |
| **02** | **Giant Germ** | `seq 716`: `0x0072` id 1114, `0x0043` **500**; delta LP lado 0 -500; índices **383** inseridos em duas zonas (`seq 721,724`). Repetição lado oposto em `seq 758–766` | Prioridade: teste diferencial de LP, escolha 0/1/2, posições de invocação |
| **03** | **Nimble Momonga** | `seq 133`: `0x0072` id 1115, `0x0042` **1.000**, delta LP lado 0 +1.000; índice **453** inserido em duas zonas (`seq 137,139`). Repetição lado oposto `seq 1076–1082` | Prioridade: teste diferencial de LP e posição face-down |
| 05 | Enchanted Javelin | 8× `0x0072`; `0x0042` por 500–1.400 LP. `seq 241–252` mostra +1.400 LP seguido de -200 de combate | Conferir ATK e sequência da Battle Step |
| 07 | Burning Land | 21× `0x0072`, 17 próximos de `0x0043` por 500 | Diferenciar ativação e tick recorrente |
| 09 | Mask of the Accursed | 24× `0x0072`, 19 próximos de `0x0043` por 500 | Contínuo / timing |
| 10 | Skull Invitation | 23× `0x0072`, com várias emissões `0x0043` por 300 (`seq 155–157`) | Evento campo→GY, ordem de efeitos |
| 12 | Marie the Fallen One | 6× `0x0072`, seguidos por `0x0042` de **200** | Condição de zona e tempo |
| 12 | Lady Assailant of Flames | 6× `0x0072`, seguidos por `0x0043` de **800** | FLIP / condição de revelação |

**Limite:** amostragem de estado é **não atômica**; comandos próximos são evidência de correlação temporal, não prova isolada de causalidade exata.

## Cartas sem evento indexado (12 / 96)

Todas são Magias: **Monster Reborn** (C01), **Change of Heart** (C02), **Inspection** (C04), **Prohibition**, **Riryoku** e **Seal of the Ancients** (C05), **Makiu** (C06), **Insect Imitation** e **Dimensionhole** (C08), **Lightning Blade** (C10), **United We Stand** e **Mage Power** (C11).

Elas estavam fisicamente no deck 3× em ambos os lados e foram verificadas. **Não inferir ausência ou falha de efeito**: a IA pode não comprá-las ou não ter alvos/condições de ativação. Preparar confrontos específicos com contextos apropriados antes de aumentar apenas a contagem de seeds.

## Decisão para os próximos PRs

1. **Parar temporariamente a expansão horizontal de cartas.** Converter primeiro as observações de **Giant Germ** e **Nimble Momonga** em fixtures determinísticas nativo→C# com estado anterior, comando, estado posterior, LP, zonas e position bits.
2. Fazer comparação semelhante para **Magic Cylinder, Rain of Mercy, Raimei**, depois **Marie/Lady Assailant**.
3. Projetar ensaios direcionados para as 12 magias silenciosas e captura de efeito contínuo para **Mirror Wall, Fairy Box, Jam Breeding Machine, Burning Land, Skull Invitation**.
4. Usar evidências sobre janelas de ativação e GY para consolidar o resolvedor de eventos e correntes LIFO, em vez de supor equivalência a partir da ordem de um único duelo.
5. **Não interpretar os 36 duelos como 39 amostras independentes:** coorte 01 replica o seed e as condições terminais do preflight anterior, não é uma nova amostra independente para as oito primeiras cartas.

A evidência bruta é o ZIP fornecido pelo usuário, não commitado no repositório. Sem cópia dos dados sensíveis/volumosos no Git, apenas este registro com hash e sequências para localização posterior.
