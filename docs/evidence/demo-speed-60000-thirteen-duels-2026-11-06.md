# Execução de demonstrações com velocidade configurada em 60000

**Data:** 2026-11-06  
**Arquivo de evidência:** `commands-20261106-055226-679965-pid20916.jsonl` (enviado pelo operador)  
**Alvo:** `joey_pc.exe` — agente Native Rules na branch `experiment/hybrid-native-probe`  
**Natureza dos dados:** eventos de Frida com `Date.now()`, NÃO contador de FPS renderizado nem replay determinístico.

## Override aplicado e integridade

O evento `seq=2` registra `kind=speedy_override`, `phase=applied`,
`previous=60`, `requested=60000`, `writerVa=0x00411CA2`.
O valor 60000 foi escrito em memória, mas isso não prova que o processo
tenha renderizado 60000 frames por segundo.

- 6491 linhas, sendo 1 metadata e **6490 eventos**.
- Metadata: `capacity=20000`, `seen=6490`, `retained=6490`,
  **`overwritten=0`**. Nenhuma sobrescrita reportada; isso não implica
  que os hooks cubram todas as operações internas da engine.
- 5850 `command`, 318 `demo_init`, 320 `demo_executor`,
  1 `marker`, 1 `speedy_override`.
- Janela observada entre primeiro e último evento: **131,685 segundos**.
- Foram identificadas **14 inicializações** de `scene_config` com
  `requestedSceneMode=7`, **13 comandos terminais `0x0004`**;
  a 14ª partida permanece sem terminal na gravação.
- 230 emissões `0x0001` na captura integral.

A repetição das demos segue automaticamente no executável original;
não precisa ser adicionada ao Native Rules para este modo. O comando
`0x0004` sinaliza terminal, mas seus parâmetros ainda não permitem
atribuir o vencedor com segurança.

## Duelos individualizados

Definição do intervalo: `scene_config(mode=7).enter` até o
`command 0x0004` seguinte, **dentro da própria demo**.
Os intervalos `0x0001 → 0x0050` são pareados somente no mesmo turno
e na mesma demo; para a mediana excluí o primeiro par de cada demo.

| Demo | Scene seq | Terminal seq | Tempo (s) | Comandos `0x0001` | Mediana após 1º par (ms) |
|---:|---:|---:|---:|---:|---:|
| 1 | 9 | 804 | 17,707 | 35 | 76 |
| 2 | 807 | 1026 | 3,438 | 7 | 73,5 |
| 3 | 1029 | 1412 | 7,206 | 13 | 73,5 |
| 4 | 1415 | 1755 | 5,976 | 9 | 70 |
| 5 | 1758 | 2286 | 10,276 | 21 | 69 |
| 6 | 2289 | 2627 | 5,751 | 11 | 68,5 |
| 7 | 2630 | 2862 | 3,889 | 8 | 67 |
| 8 | 2865 | 3475 | 12,547 | 22 | 79 |
| 9 | 3478 | 3674 | 3,300 | 7 | 70 |
| 10 | 3677 | 4525 | 15,813 | 34 | 74 |
| 11 | 4528 | 5506 | 17,923 | 32 | 70 |
| 12 | 5509 | 5968 | 9,620 | 14 | 76,5 |
| 13 | 5971 | 6290 | 6,608 | 10 | 89 |
| 14 | 6293 | — | incompleta | 7 | 77,5 |

- Duração total de recortes completos: **120,054 segundos**;
  do início da primeira demo até o 13º terminal: **126,304 segundos**
  incluindo os intervalos entre duelos.
- Mediana da duração por duelo completo: **7,206 segundos**,
  faixa de **3,300 a 17,923 segundos**.
- **212** pares `0x0001 → 0x0050` após descartar o primeiro de
  cada uma das 14 demos; **mediana 74 ms**, mínimo 65 ms,
  máximo 128 ms. Restritos às 13 completas: **206** pares.
- Não há nesta coleta anomalia como a pausa isolada de 18.560 ms
  da captura anterior em 2000.

## Comparação das configurações

| Parâmetro | Mediana do mesmo trecho (ms) | Ganho relativo ao baseline 60 |
|---:|---:|---:|
| 60 (`-speedy`) | ~1082 | 1× |
| 240 | ~269 | 4,02× |
| 2000 | **74** | 14,62× |
| 60000 | **74** | 14,62× |

Entre 2000 e 60000, o parâmetro foi multiplicado por **30**,
mas o trecho medido não acelerou de maneira perceptível.
**Conclusão restrita:** há um patamar de ~70–80 ms nesse intervalo
específico; os dados não determinam o gargalo exato, o FPS real
nem a velocidade de todos os outros aspectos do jogo.

Comparar duelos/segundo de amostras diferentes exige cuidado:
os decks, quantidade de turnos e decisões da IA variaram. Em 2000
foram 7 duelos terminados em 78,007 s de laboratório; nesta
captura foram 13 em 126,304 s, portanto o ganho global também não
é proporcional à mudança de valor.

## Descoberta adicional: três decks diferentes no modo 7

Os `scene_config` de saída identificam **três composições diferentes**,
validando índices concretos de cartas (`mainIndices`, `secondIndices`,
`thirdIndices`), não apenas quantidades:

| Assinatura local | Main / Second / Third | Ocorrências entre 28 slots de origem |
|---|---|---:|
| A | 40 / 0 / 9 | 13 |
| B | 43 / 6 / 3 | 8 |
| C | 41 / 6 / 3 | 7 |

Das 14 inicializações:
- **5** carregaram a mesma composição nos dois slots de origem.
- **9** carregaram composições distintas.
- Combinações observadas: A/A (2), A/B (2), A/C (4),
  B/A (2), B/B (2), C/A (1), C/C (1).
- Os mesmos três conjuntos também aparecem na captura anterior em 2000.
- A relação entre slots de origem e lados ativos deve ser extraída
  dos eventos `deck_prepare`/`sourceSlot`; os dois slots não são
  necessariamente idênticos aos dois jogadores em todos os caminhos.

Isso reforça a necessidade de registrar os **dois decks efetivos por duelo**
e suas opções de seleção para futuro experimento de IA contra IA.
Não inferir como o RNG escolhe os decks apenas dessas frequências.

## Próxima limitação operacional: retenção do trace

O buffer atual mantém somente **20.000 eventos**.
À taxa observada (6490/131,685 ≈ **49,3 eventos/s**),
ele atingiria a capacidade em aproximadamente **406 segundos**
(**6 min 46 s**), se a taxa permanecesse semelhante.
Eventos antigos começariam a ser sobrescritos caso a captura
não fosse exportada antes. O JSONL desse experimento tem
aproximadamente **1,97 MB**.

Prioridades:
1. Implementar extração periódica/streaming de eventos em arquivo
   com cursor de sequência e detecção explícita de lacunas, ou
   capturas segmentadas e rotacionadas ao fim de N duelos.
2. Resumir eventos em **linhas por duelo** (seq, início/fim, turnos,
   duração, deck de cada lado, opcode terminal, parâmetros brutos,
   contadores e avisos) sem supor vencedor não decodificado.
3. Automatizar seleção controlada de decks e repetição estatística;
   o mecanismo de reinício da demo já está operante.
4. Investigar o patamar de tempo somente se a taxa de partidas
   alcançada não satisfizer o laboratório; não alterar relógios globais.

**Estado de validação:** evidências verificadas no JSONL anexado;
nenhum duelo novo foi executado neste ambiente.
