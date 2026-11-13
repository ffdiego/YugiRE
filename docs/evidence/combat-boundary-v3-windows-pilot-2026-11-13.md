# Piloto real Windows — combat-context v3 com cinco fronteiras (2026-11-13 UTC)

## Fonte e escopo

Auditoria direta do JSONL privado enviado pelo operador:
`commands-20261113-042853-907607-pid19584.jsonl`.
SHA256: `c7a8b618418061f68665c7c3e59574371a2061e23eb1d55965ac456fa0aaf80a`.
**Não versionar a captura original.**

Operador confirmou duelo concluído **normalmente**, sem anomalias relatadas.
Registro terminal no trace, `seq=837`, opcode mascarado `0x0004`,
mostra `lifePoints=[7200,0]` (lado 0/CPU, lado 1/jogador), consistente
com final de duelo; não confundir a partida com a vitória do piloto
anterior. O arquivo não prova tempo de execução sem overhead.

Metadados do exportador:
- `processId=19584`, módulo `Z:\YugiRE\passion\joey_pc.exe`;
- SHA256 do executável atestado pela captura:
  `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`;
- `session=1`, `producer=0x5B9170`, `capacity=20000`;
- `seen=837`, `retained=837`, `overwritten=0`.
O modo foi marcado `boundary-v3-single-duel` no primeiro evento.

## Auditoria estrutural reproduzível do JSONL

| Controle | Resultado |
| --- | --- |
| Linhas do arquivo | 838: metadados + **837 eventos** |
| Sequências | `seq=1..837`, contínuas e sem duplicatas |
| Tipos | 608 `command`, 200 `combat_context`, 28 `ai_probe`, 1 `marker` |
| Esquema dos eventos de combate | **200/200** `schemaVersion=3` |
| Chamadas F40 únicas (`combatCallId`) | **50** |
| Entradas / saídas F40 | **50 / 50**, pareadas |
| `own_stats` / `opponent_stats` | **50 / 50**, um de cada por F40 |
| Ordem por frame | **50/50** entrada F40 → own_stats → opponent_stats → saída F40 |
| Correlação thread/selector/session, argumentos, caller/slots/lados | **50/50** |
| Contagem de filhos `childCount=2` | **50/50** |
| ESI de entrada e saída = `esiExpectedRaw=0xD44` | **100/100** wrappers |
| `esiPreserved=true` e ESP entrada=saída | **100/100** wrappers |
| Valores de posição nas fronteiras iguais ao snapshot F40 | **50/50** para cada wrapper |
| Seed de entrada (8 caracteres hex) e output F40 (8 caracteres hex) | **50/50** |
| Saída stats own e opponent (24 caracteres hex cada) | **50/50** |
| `readError`/`readErrors`/`correlationError`/`childOverflow` | **0** |
| Perda por sobrescrita | **0** |
| `combat_context_limit` | **0** |
| Pares de seletores `ai_probe` | 12 normal, 2 priority; todos pareados |

Todos os frames F40 observados tinham `side=0`, `threadId=4240`.
Os 50 frames foram associados a 10 IDs distintos de seletores.
Em todos os wrappers os parâmetros e os endereços de retorno
correspondiam aos dois CALLs diretos F40:
`own_stats` retorna a `0x005B0F97`, `opponent_stats` a
`0x005B0FAE`.

A análise verificou também:
- Posições rivais no snapshot F40: **26 ocultos em defesa**,
  **16 revelados em defesa** e **8 revelados em ataque**;
- `seedHex == outputHex` em quatro frames e diferente em 46
  (diferença não é erro: a F40 modifica o descritor);
- Os campos v3 `consumedPositionRaw`,
  `consumedDescriptorSeedRaw` e `consumedFlagsRaw`
  permaneceram explicitamente **`null`** em 50/50 entradas;
- `provenance='F40-entry-snapshot'` nos registros F40 e
  `provenance='wrapper-boundary-snapshot'` nos 100 wrappers.

O JSONL não contém indícios de corrupção de ESI, falha no
pareamento, ruptura da sequência ou saturação no limite de 64
nesta execução. A declaração do operador de término normal
complementa os controles estruturais.

## Inferências que **não** fazemos

- Um duelo concluído não prova que todos os caminhos de execução ou
  efeitos de carta são seguros para interceptação.
- Os 50 frames são **estruturalmente íntegros**, mas **zero** contêm
  todos os atributos/efeitos com proveniência independente, postura,
  visibilidade, seed e flags comprovadamente consumidos.
  Não há `Match`/`Mismatch` válido contra `Joey.Core`.
- A v3 observa *fronteiras de funções*, não instruções internas.
  Igualdade de posição antes/depois não é testemunho atômico do valor
  consumido. Os dois buffers `outputHex` são resultados nativos,
  não um cálculo independente de atributos.
- Este piloto não revalida os outros oito hooks internos nem a
  combinação v2, que permanece proibida em Windows.

## Próxima prioridade técnica

Agora a v3 tem **um piloto Windows funcional e estruturalmente íntegro**.
Antes de adicionar instrumentação, cruzar o subconjunto de cartas
capturadas com o catálogo `Joey.Core` e documentar o primeiro
campo faltante para obter um confronto independente fechado.
Inspecionar de forma estática proveniência de modificadores/efeitos
e o controle de fluxo dos campos consumidos. Se for indispensável,
habilitar **um** probe interno corrigido por sessão separada, com
gates/limites já demonstrados; nunca todos de uma vez.

Preservar o JSONL bruto local, sem merge na `main`, até
uma revisão de testes completos e de paridade.
