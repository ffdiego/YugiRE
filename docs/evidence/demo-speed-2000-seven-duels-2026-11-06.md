# Execução repetida da demo com `--speed-value 2000` — 2026-11-06

## Proveniência e escopo

Captura: `commands-20261106-054903-749983-pid10560.jsonl`, fornecida pelo operador, produzida pelo laboratório Native Rules para `joey_pc.exe` (PE32 x86 verificado pelo launcher). Não existe no trace uma leitura independente de FPS real.

O registro `seq=2` comprova que o hook foi acionado e **substituiu 60 por 2000**:

```json
{"kind":"speedy_override","phase":"applied","previous":60,"requested":2000,"writerVa":"0x00411CA2","seq":2}
```

`2000` é o parâmetro configurado, **não** a medição de quadros realmente renderizados por segundo.

## Integridade e ciclo de demos

- 3073 linhas JSONL: uma `metadata` e **3072 eventos**.
- `seen=3072`, `retained=3072`, `overwritten=0` (nenhuma sobrescrita no buffer).
- 2734 comandos, 184 eventos de inicialização `demo_init`, 152 eventos do executor `demo_executor`, 1 marker e 1 evento `speedy_override`.
- Intervalo do primeiro ao último evento: **81,304 segundos**.
- **8** configurações `scene_config(requestedSceneMode=7)`, **7** comandos terminais `0x0004` (todos produzidos por `0x005BC545`). O oitavo duelo foi inicializado antes de terminar a captura, mas não consta seu término.
- Após cada comando terminal dos sete primeiros duelos, o próprio jogo voltou a configurar uma demo, sem intervenção do laboratório. **A repetição básica da demo já é automática**; o trabalho novo deve concentrar-se em controlar entrada de decks, coleta e análise dos resultados.
- O relatório não determina vencedor/resultado de cada duelo apenas a partir dos argumentos brutos do comando `0x0004`. É necessária decodificação dos campos.

## Durações de duelos e timing de turnos

A duração de cada duelo é o tempo entre a entrada em `scene_config` modo 7 e a emissão do primeiro `0x0004` antes da próxima entrada. Isso inclui preparação do duelo, não necessariamente apenas a execução de turnos.

| Duelo | Início (seq) | Terminal (seq) | Duração (s) | `0x0001` | Mediana `0x0001 → 0x0050` após o primeiro (ms) |
|---|---:|---:|---:|---:|---:|
| 1 | 9 | 340 | 6,362 | 10 | 76 |
| 2 | 343 | 614 | 4,692 | 7 | 75,5 |
| 3 | 617 | 1182 | 29,506 | 24 | 74 |
| 4 | 1185 | 1776 | 11,820 | 17 | 79 |
| 5 | 1779 | 2378 | 10,526 | 18 | 70 |
| 6 | 2381 | 2599 | 3,833 | 7 | 68 |
| 7 | 2602 | 3046 | 7,692 | 14 | 71 |
| 8 | 3049 | — | incompleto | 0 | — |

Do primeiro `scene_config` ao sétimo comando terminal decorrem **78,007 segundos**. A mediana da duração dos sete duelos é **7,692 s**.

**Método de agrupamento:** parear cada `0x0001` apenas com o `0x0050` posterior dentro do mesmo turno **e do mesmo duelo**. Desconsiderar a primeira amostra de cada partida (geralmente 1–2 ms).

Entre os sete duelos houve **89 pares após o primeiro turno**:
- Mediana: **74 ms**.
- Faixa: **66 a 18.560 ms**; a maioria concentra-se em 66–102 ms, com um segundo valor atípico moderado de 116 ms.
- Excluindo apenas o par atípico de 18.560 ms: **88 amostras**, média **75,56 ms**.

### Pausa anômala

No duelo 3, `seq=1064` (comando `0x0001`, +23,837 s) e `seq=1065` (comando `0x0050`, +42,397 s) estão separados por **18.560 ms**. Não existem eventos intermediários no JSONL.

O trace **não permite determinar a causa**: pausa do jogo, perda de foco, suspensão de thread, overhead do sistema ou condição interna são hipóteses, não fatos. Como não existem eventos de tick a cada quadro, a ausência de comandos não prova que o processo deixou de executar.

## Comparação com as capturas anteriores

| Configuração | Mediana do intervalo (ms) | Razão relativa ao valor 60 |
|---|---:|---:|
| `-speedy` padrão (60) | ~1.082 | 1,00× |
| `--speed-value 240` | ~269 | 4,02× |
| `--speed-value 2000` | **74** | **14,62×** |

Valor de configuração `2000/60 = 33,33×`, mas o ganho observado neste **segmento** é apenas 14,62×; de 240 para 2000 o segmento acelera aproximadamente **3,64×**, em vez de 8,33×.

Se o intervalo escalasse inversamente de forma perfeita, o esperado em 2000 seria ~**32,5 ms**; foram medidos 74 ms. Isso sugere componente fixo, quantização, limitação pelo loop, trabalho real ou outras esperas, mas **nenhuma dessas causas está confirmada**.

Duas advertências:
1. A comparação ocorre entre demos com decks e decisões diferentes, não entre simulações determinísticas do mesmo duelo. A métrica é um intervalo recorrente, não o FPS efetivo nem speedup fim-a-fim.
2. Nenhuma sobrescrita de buffer implica retenção dos eventos observados, não que um hook registre todas as operações do engine.

## Próximo experimento recomendado

- Preservar a opção de velocidade sem teto artificial, mas com proteção para o campo `uint32`, como já implementado.
- Adicionar análise estruturada **por duelo**, usando `scene_config` e `0x0004` como fronteiras. Registrar duração, turnos, contagem de eventos, decks, terminal e pares de timing com exclusão separada do primeiro turno.
- Instrumentar de forma opt-in os gates `FUN_0040CF20` e `FUN_005B9CA0` somente se ainda precisarmos ultrapassar o limite prático observado em `2000`; evitar alterar `Sleep` ou relógios globalmente.
- Determinar o sentido dos argumentos do `0x0004` e como confirmar/registrar vencedores; automatizar seleção dos decks e exportação, não reinicialização da demo que já acontece sozinha.

**Status:** achados do JSONL real; nenhum novo jogo foi executado pelo assistente.
