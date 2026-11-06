# Laboratório de demos — duas partidas com velocidade configurada em 240

**Data:** 2026-11-06  
**Origem:** captura enviada pelo operador `commands-20261106-054050-121363-pid10844.jsonl`.  
**Branch do experimento:** `experiment/hybrid-native-probe`.  
**EXE alvo:** `joey_pc.exe` com SHA256 documentado `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de` (a captura é do agente compilado para esse alvo; o JSONL não contém, por si só, uma nova medição de hash).

## O override foi aplicado

O registro `seq=2` contém:

```json
{"kind":"speedy_override","phase":"applied","previous":60,"requested":240,"writerVa":"0x00411CA2","seq":2}
```

O script havia iniciado o processo com `-speedy` e instalado um hook em memória depois do escritor de `0x005EB618`. O evento confirma que o valor foi alterado de 60 para **240** durante a inicialização. Um marker manual `240fps` aparece em `seq=369`, durante o segundo duelo.

**Cuidado de interpretação:** `240` é o valor observado/configurado, e o operador informou visualmente 240 FPS. O JSONL registra **emissões de comandos e timestamps de parede**, não um contador de frames renderizados; por si só ele não mede FPS real.

## Integridade da coleta

- **623 eventos** depois da linha de metadata; `seen=623`, `retained=623`, `overwritten=0`; nenhuma perda reportada pelo buffer.
- Duração do primeiro ao último evento: **59,729 segundos**.
- Tipos: **542** comandos, **50** eventos de inicialização da demo, **28** eventos do executor de ações normais, **2** markers e **1** evento de override.
- **23** comandos `0x0001`, **23** comandos `0x0050`, **21** comandos `0x0002`, **21** comandos `0x0003` e **2** comandos terminais `0x0004`.

## Dois duelos encerrados

As configurações `scene_config(requestedSceneMode=7)` separam as partidas. Cada uma encerrou com um comando `0x0004` antes da próxima inicialização ou fim do arquivo:

| Indicador | Duelo 1 | Duelo 2 |
|---|---:|---:|
| Início (`scene_config`) | seq 9, +6,070 s | seq 292, +36,011 s |
| Terminal `0x0004` | seq 289, +27,349 s | seq 621, +59,145 s |
| Duração desse recorte | **21,279 s** | **23,134 s** |
| Inícios de turno `0x0001` | 13 | 10 |
| Primeiro intervalo `0x0001 → 0x0050` | 8 ms | 9 ms |
| Mediana demais intervalos | **265,5 ms** | **269 ms** |
| Faixa demais intervalos | 262–278 ms | 268–270 ms |
| Fonte do deck espelhada entre os lados | sim | sim |
| Listas do deck de origem (principal/extra1/extra2) | 41 / 6 / 3 | 40 / 0 / 9 |

Os dois eventos terminais têm `opcodeMasked="0x0004"` e são emitidos pelo chamador `0x005BC545`. Os campos brutos do primeiro argumento são `0x00070004` e `0x00030004`, respectivamente. **Não deduzir o vencedor apenas desses valores sem decodificação confirmada.**

As duas fontes de deck espelham a mesma composição de índices entre o lado 0 e 1 de cada duelo. Isso não significa que os embaralhamentos ou escolhas de IA sejam idênticos.

## Comparação objetiva contra a captura anterior com `-speedy`

A captura anterior com `-speedy` padrão (`0x005EB618=60`) tinha mediana de aproximadamente **1.081,5–1.082 ms** para os intervalos `0x0001 → 0x0050`, desconsiderando o primeiro turno do duelo.

O presente arquivo com override 240 apresenta **269 ms** como mediana da segunda partida e como mediana global dos intervalos após ignorar o primeiro de **cada** partida (21 intervalos após o warm-up por partida).

- Razão de tempo: **1081,5 / 269 ≈ 4,02×**.
- Queda da duração desse segmento: aproximadamente **75,1%**.
- Razão de valores escritos: **240 / 60 = 4×**.

O alinhamento quase exato entre a razão de configuração e a razão dos intervalos constitui **forte evidência funcional** de que `0x005EB618` influencia o pacing do duelo. Isso supera a limitação da análise estática anterior, que não encontrara leituras diretas desse global.

**Limites:** as demos são aleatórias e não controladas por semente fixa. A comparação mede um segmento recorrente do ciclo do turno, não um speedup global de cada jogo. O operador confirmou o comportamento visual a 240 FPS; o trace não prova 240 quadros de renderização por segundo. Ainda não sabemos por qual código o valor é consumido e se valores ainda maiores são estáveis.

## Próximas prioridades

1. **Considerar o ganho de pacing a 240 validado nesse segmento** e manter o override como opção de laboratório; não é preciso bloquear a automação das demos até descobrir a leitura indireta do global.
2. Automatizar repetição de modo 7, ciclos completos (inicio/terminal), exportação de resultados e troca controlada de decks. Métricas de sucesso incluem duelos terminados, contagem de eventos e ausência de perdas.
3. Para testar velocidades acima de 240, ampliar limite só após confirmar invariantes das regras e observar desempenho, estabilidade, comportamento da IA e avanço de estados; não alterar `Sleep`/relógios globalmente.
4. Separadamente, mapear os consumidores indiretos de `0x005EB618` para compreender o controle de frames e a margem de segurança.

**Status:** relatório produzido a partir de um JSONL real fornecido pelo operador; não foram executados duelos adicionais neste ambiente.
