# Laboratório YugiRE: primeira captura contínua de 100 duelos

**Validação real no Windows, 2026-11-08.** Fontes: arquivos enviados pelo operador:
`duels-20261108-051557-565189-pid13980.jsonl` (piloto de 3 duelos)
e `duels-20261108-051720-709901-pid23392.jsonl` (100 duelos).

## Integridade da captura

| Métrica | Piloto | Principal |
|---|---:|---:|
| Velocidade solicitada | 2400 | 4800 |
| Duelos terminados por `opcodeMasked=0x0004` | 3/3 | 100/100 |
| JSONL `capture_summary.complete` | true | true |
| Eventos com `seq` | 1.945 | 53.538 |
| Sequências faltantes ou repetidas | 0 | 0 |
| Eventos sobrescritos | 0 | 0 |
| Deck 0 `applied/verified` | 3/3 | 100/100 |
| Deck 1 `applied/verified` | 3/3 | 100/100 |
| Erros de `deck_override` | 0 | 0 |

No arquivo principal, houve **100 eventos de `demo_init/scene_config` com
`requestedSceneMode=7`**, cada um seguido de um comando terminal.
A captura começa com `metadata` (decks A e B completos,
velocidade, PID) e termina com `capture_summary`.
O buffer de 20.000 eventos não impediu salvar 53.538 eventos;
o esquema de drenagem ao vivo efetivamente funcionou.

A coleta principal cobriu **1.037,326 s (17 min 17 s)**,
equivalente a aproximadamente 5,8 duelos finalizados por minuto.
Isso reflete o ambiente e o experimento observado, não uma
promessa de throughput em outras máquinas.

## Duração e ações dos duelos

Nos 100 duelos completos, intervalo entre `scene_config.enter`
modo 7 e comando terminal `0x0004`:

- Mínimo **3,368 s**; máximo **28,551 s**.
- Mediana **8,4095 s**; média **9,7350 s**.
- Foram capturados **1.795** comandos `0x0001`
  (início de turno, conforme interpretação anterior), mediana
  **16** por duelo, máximo **56**.
- **48.234** eventos `kind=command` e
  **1.311** chamadas `demo_executor/normal_placement.enter`
  (mais 1.311 eventos de saída).

## Decks e atribuição de slots

Os decks solicitados foram `demo-A.json` para o lado 0
(40 índices no main) e `demo-B.json` para o lado 1
(43 índices no main).

Ambos foram aplicados e verificados **100 vezes**, sempre sem
`deck_override/mismatch` ou `failed`. Houve
**200** embaralhamentos iniciais (um por lado e duelo),
todos com o multiconjunto correto de cartas principais.
Foram observados **37** embaralhamentos adicionais durante
duelos, com decks já parcialmente consumidos; são acontecimentos
a investigar, não divergências do deck de origem.

O par de slots de origem `(lado 0, lado 1)` alternou:
**(0,1) em 46 duelos** e **(1,0) em 54 duelos**.
Em todos os 100 terminais, `sideBit15` de
`opcodeRaw 0x0004/0x8004` coincidiu com o slot de origem
do lado 0: **0x0004 nas 46 vezes com slot 0**,
**0x8004 nas 54 com slot 1**.
A mesma correlação ocorreu nos **3/3** duelos piloto.
É uma pista forte de que o bit codifica algum mapeamento
de lado/cena; **não prova que sinaliza vencedor**.
Os campos brutos `args[0]` e `args[1]` variam e o contrato
de resultado segue desconhecido.

## Limitações e próximos passos

- O sucesso da substituição está confirmado para o **main**
  no deck efetivo. As seções `second` e `third` são
  gravadas e relidas no source, mas seu uso efetivo nas regras
  não foi validado independentemente.
- A captura monitorou comandos, bootstrap e posicionamento
  normal, **não** executou `trace ai on`, portanto não há
  logs `ai_probe` dos seletores específicos de decisão.
- `complete=true` garante a meta e integridade dos **eventos
  instrumentados**, não cobertura de todas as funções do Joey.
- Investigar os argumentos e o estado ao redor de `0x0004`
  com Ghidra e experimentos controlados para identificar vencedor
  sem depender de correlações espúrias.
- Preservar os dois arquivos JSONL brutos como base de
  regressão e gerar tabelas por duelo, sem perder eventos.

A análise offline também gerou `yugire_100_duels_summary.csv`
com 100 linhas e métricas de duração, número de comandos,
posicionamentos, origem de deck, embaralhamentos e argumentos
terminais brutos. O CSV é um artefato da conversa; não foi
incluído neste commit.
