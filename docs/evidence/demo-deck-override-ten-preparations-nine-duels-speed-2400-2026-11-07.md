# Dez preparações A × B e nove duelos concluídos a velocidade 2400

**Captura:** `commands-20261107-042441-971357-pid8448.jsonl` (enviada pelo operador)  
**Execução:** `native_rules.py --spawn --observe-only --speed-value 2400 --deck-0 decks/demo-A.json --deck-1 decks/demo-B.json`  
**Status:** validação dinâmica da substituição do **main deck** em 10 inicializações consecutivas; ainda não valida vencedor ou cartas de extra/segunda/terceira seção no jogo.

## Integridade e inicialização

- Uma linha `metadata` e **5.222** eventos JSONL.
- `seen=5222`, `retained=5222`, `overwritten=0`, `capacity=20000`.
- **100,122 segundos** entre primeiro e último evento.
- 4.692 `command`, 258 `demo_executor`, 230 `demo_init`, 40 `deck_override`, um `marker`, um `speedy_override`.
- O evento `speedy_override` `seq=2` confirma a escrita `60 → 2400`, não comprova FPS reais.
- Não foram encontrados eventos `deck_override/failed` ou `mismatch`, nem campos `error`/`readError` nos eventos exportados.

## Resultado: decks aplicados em todos os duelos

Há **10** entradas `demo_init/scene_config(requestedSceneMode=7)` e,
em cada uma, quatro eventos de substituição:

1. `deck_override/applied side=0`, main com 40 índices — preset A.
2. `deck_override/verified side=0`, `actualMainCount=40`.
3. `deck_override/applied side=1`, main com 43 índices — preset B.
4. `deck_override/verified side=1`, `actualMainCount=43`.

Contagem total: **applied=[10,10], verified=[10,10], failed=[0,0]**.
Em todas as dez demos, a lista de `mainIndices` escrita foi
idêntica à do respectivo preset; o `deck_prepare/leave`
confirmou a **mesma ordem e composição** do main efetivo.

A lógica `sourceSlot = side XOR deckSideBit` também foi testada
em ambos os caminhos: **4** demos com lado 0 na origem 0 e
**6** com lado 0 na origem 1. A correspondência ao preset
permaneceu correta independentemente da troca.

As **20** chamadas iniciais `deck_shuffle/enter` e
`deck_shuffle/leave` (uma por lado e por demo) preservaram
o multiconjunto do main solicitado. Houve outras
**3** chamadas `deck_shuffle` no lado 1 durante o duelo
(com contagem remanescente 34, 34, 24); comparar essas
listas parciais com os 43 índices originais seria inválido.
A causa desses embaralhamentos posteriores não foi investigada.

## Duelos individualizados

A duração é do `scene_config.enter` modo 7 até o próximo
`command opcodeMasked=0x0004` dentro da mesma demo;
inclui preparação e transições e não equivale somente ao tempo
de jogadas da IA. Cada `0x0001` é contabilizado como início
de turno, conforme métrica anterior.

| Demo | Cena seq | Terminal seq | Duração (s) | `0x0001` | Source A/B | Main verificados |
|---:|---:|---:|---:|---:|---|---:|
| 1 | 9 | 663 | 12,570 | 23 | 0 / 1 | 2 |
| 2 | 666 | 1023 | 6,345 | 12 | 0 / 1 | 2 |
| 3 | 1026 | 1504 | 8,627 | 16 | 0 / 1 | 2 |
| 4 | 1507 | 2167 | 12,593 | 24 | 1 / 0 | 2 |
| 5 | 2170 | 2596 | 7,502 | 12 | 1 / 0 | 2 |
| 6 | 2599 | 2948 | 5,679 | 10 | 0 / 1 | 2 |
| 7 | 2951 | 3331 | 6,141 | 12 | 1 / 0 | 2 |
| 8 | 3334 | 4384 | 20,915 | 41 | 1 / 0 | 2 |
| 9 | 4387 | 4822 | 7,172 | 12 | 1 / 0 | 2 |
| 10 | 4825 | — | incompleto | 12 | 1 / 0 | 2 |

- **9** duelos têm comando terminal `0x0004`, todos emitidos do
  caller `0x005BC545`; a décima demo não tem terminal antes do fim da captura.
- Mediana de duração dos nove completos: **7,502 s**.
- Intervalo do início do primeiro duelo ao nono terminal:
  **91,443 s** (inclui transições entre demos).
- Entre as dez demos há **174** emissões de `0x0001`.
- Tipos terminais brutos: `0x0004` (4) e `0x8004` (5).
  **Não interpretar o bit 15 nem `args[1]` como vencedor sem
  confirmar o contrato da rotina nativa.**

## Intervalo de pacing em velocidade 2400

Pareando `0x0001` ao `0x0050` seguinte dentro
da mesma demo, e excluindo a primeira amostra de cada uma:

- **164** pares medidos.
- Mediana: **73 ms**.
- Faixa: **65 a 150 ms**.
- Resultados anteriores: ~74 ms nos testes em 2000 e 60000.
  O trecho permanece aproximadamente no mesmo patamar, mas
  não representa o FPS renderizado.

## Limitações e próximas ações

**Confirmado:** escrita e releitura do source + correspondência
do **main deck efetivo** nas dez preparações e composição
preservada através do shuffle inicial dos dois lados.

**Ainda não confirmado:** correspondência das seções secundária
e terciária no deck *efetivamente usado*, resultado/vencedor de
cada duelo e estabilidade em múltiplos processos concorrentes.
O código do hook lê essas duas seções após a escrita para validar
o source, mas ainda não demonstra seu consumo nas regras do duelo.

Próxima prioridade: decodificar argumentos do terminal
`0x0004` e identificar o vencedor para medir taxa de vitória
de A vs B. Para estatística confiável, controlar ou registrar
semente RNG, ordem de início e o estado de jogo compartilhado;
depois, exportar resumo por duelo em CSV/JSONL a partir do trace.
