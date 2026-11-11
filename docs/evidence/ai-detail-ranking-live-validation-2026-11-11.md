# Validação Windows — duas decisões normais com métricas reais (2026-11-11 UTC)

## Fonte e integridade

Captura original, recebida do operador Windows e **não versionada**:
`commands-20261111-034109-625866-pid29768.jsonl`.
SHA256 `ea1ed03746df2fbd56f4ed9c2fd8abee7f502b2687a683c4597346183f7df449`.

- 156 linhas (um metadata, **155 eventos sequenciais 1..155**);
  `seen=retained=155`, `overwritten=0`, capacidade 20.000.
- Janela 2026-11-11 03:40:17.231–03:41:05.006 UTC (~47,775s).
- 106 `command`, 26 `ai_probe`, 22 `ai_detail`, 1 `marker`.
- Dois pares `select_normal enter/leave`, três `simulate_card`,
  dois `combat_event`, cinco `aggregate_score`, cinco
  `restore_field`, cinco `select_combat_event` e quatro baselines
  (externo+interno por decisão).
- Sem erros de leitura nos eventos detalhados. `metadata.exeSha256=null`:
  o arquivo sozinho **não atesta** hash do processo; o contrato do
  observador fixa o build alvo.
- Probe em `--observe-only`, ativado com `trace ai on detail`, sem
  evidência de execução de funções artificiais.

**Resultado:** o comparator isolado de
[NativeAiRanking.cs](../../src/Joey.Core/NativeAiRanking.cs),
alimentado exclusivamente com métricas capturadas e com o baseline
interno da mesma seleção, reproduz as **três aceitações/rejeições dos
candidatos simulados** e os **dois índices da mão retornados pelo
nativo**. Isso é validação dinâmica **do ranking dos candidatos que
chegaram à simulação**, **não** reprodução completa dos gates que
descartam os outros índices da mão, nem de toda a IA.

## Primeira seleção — callId 225, seq26..53

- Baseline externo: seq25, caller `0x5b738a`, score0.
- Mão no enter26, `side=0`, `pass=-1`:
  `[834,375,178,272,1082,11]`.
- Baseline interno: seq29, score0, `summaryRaw=0`, perdas rivais de
  referência = 0.
- Somente **dois candidatos chegaram ao helper de hipótese**
  `FUN_005B1610`; os outros índices não foram avaliados nesse
  trecho, mas o trace **não mostra qual gate os eliminou**.

| Etapa | Candidato 834 (posição0) | Candidato 178, Karate Man (posição2) |
| --- | --- | --- |
| `simulate_card` | seq30, slot retornado0 | seq42, slot retornado0 |
| `computed_stats` | seq31..36: ATK1200 DEF700 | seq43..48: ATK1000 DEF1000 |
| `combat_event` (proposta) | seq37 `08080000`: score0, remoção rival útil | seq49 `00000000`: não útil |
| `select_combat_event` | seq38 `0C080000`: selecionado, score0; seq39 retorna0 | seq50 retorna0, sem evento selecionado |
| `aggregate_score` | seq40 score0 | seq51 score0 |
| `restore_field` | seq41 `summaryRaw=72` (`0x48`), `actionMaskOutput=1` | seq52 `summaryRaw=576` (`0x240`), `actionMaskOutput=0` |
| Predicado `NativeAiRanking` | **aceita**: score empatado, perdas rivais1, próprios sobreviventes1, ATK≥900, bit slot0 permitido | **rejeita**: máscara do slot0 apagada (e sem melhoria do score) |

A soma dos scores dos **eventos selecionados** é0 para cada
candidato, igual ao retorno de `aggregate_score`. Não somar propostas
`combat_event` como se já fossem selecionadas.

Resultado previsto percorrendo só os candidatos **que passaram à
hipótese**, nas posições observadas: **índice0**. Resultado nativo
seq53: `selectedHandIndex=0`, carta interna834. O `0x00CE`
no seq57 aponta `ceNativeState.source=834`,
`ceHandIndexCandidate=0`, lado CPU0.

Este é o primeiro **desempate de score zero observado com métricas
completas do comparator**: o aumento de perdas adversárias e a
sobrevivência própria aceitam a carta834 mesmo sem ganho numérico.

## Segunda seleção — callId3379, seq111..128

- Baseline externo seq110 e interno seq114: ambos0.
- Mão no enter111, `side=0`, `pass=-1`:
  `[375,178,1082,455]`.
- Único candidato hipotético: seq115, `internalCardIndex=178`
  (Karate Man), slot0.
- Stats seq116..123: ATK1000, DEF1000.
- `select_combat_event` seq124 retorna1 com descritor
  `0600E803`: lowWord `0x0006`, indicador de **rota de alvo
  especial** (bit 0x2) e evento selecionado (bit0x4); highWord
  assinado **+1000**. Seq125 retorna0.
- `aggregate_score` seq126 retorna **+1000**, igual à soma
  do evento escolhido.
- `restore_field` seq127: `summaryRaw=64` (`0x40`),
  `actionMaskBeforeRestore=1`, `actionMaskOutput=1`,
  `actionMaskCurrent=1` naquele instante.
- `NativeAiRanking.AcceptNormalCandidate` retorna **true** porque
  ATK≥900, slot0 habilitado e score1000 > baseline0.
- Índice previsto **1**; nativo seq128 `selectedHandIndex=1`,
  carta interna178; CE132 `ceNativeState.source=178`,
  `ceHandIndexCandidate=1`.

Temos agora uma observação real da **rota especial do alvo**
em `FUN_005B0B90`, ausente do fixture anterior de 22 eventos.
É **compatível com ataque direto**, mas a equivalência semântica
universal de alvo5 ainda não foi provada. O comando seq143 pede dano
de1000 ao jogador/lado1 e seq144 registra queda real de LP
8000→7000: coerente com o score, **não prova causalidade direta
sem mapear o executor do ataque**.

## Reprodução limitada do comparator com dados observados

As três chamadas concretas a
`NativeAiRanking.AcceptNormalCandidate(bestScore, bestOpponentLosses,
candidateScore, calculatedAttack, slot, actionMask, summary)` seriam:

```text
# selector 225, baseline score0/perdas rivais0
(0,0,0,1200,0,1,72) => true    # 834; score0, desempate por perdas
(0,1,0,1000,0,0,576) => false  # 178; bestLosses atualizadas para1

# selector 3379, baseline score0/perdas rivais0
(0,0,1000,1000,0,1,64) => true # 178; score maior
```

Os valores de ATK correspondem aos buffers `FUN_0056DD30`
observados para cada carta candidata, e `summary`/`mask`
são amostras **depois** de `FUN_005B0950`, no ponto que precede
o comparator. Os números da função de comparação são válidos no
contrato documentado do EXE original; o retorno de
`restore_field.returnSigned=720` **não é interpretado como status
semântico**, porque o helper não possui contrato de retorno
estabelecido para essa finalidade.

## Limites e próximo experimento

- A correspondência do comparator não explica **por que** os outros
  índices da mão foram excluídos antes de `simulate_card`. Para
  reproduzir uma decisão a partir do *estado inicial completo*,
  observar **gates por candidato** (`FUN_005AAB90`,
  `FUN_005AE3E0`, `FUN_005AE220`, tributos) com chamada nativa
  natural e correlação por thread/selector, sem invocar helpers.
- Os dois baselines externos e internos coincidiram em0 nestes dois
  casos; não afirmar equivalência universal nem baseline de todos
  os níveis/pools.
- O campo `selectedEventHex` é a escolha de um confronto interno
  de simulação; não prova que ele foi executado de fato em campo.
- O critério de ranking foi reproduzido em **três candidatos, duas
  escolhas**. Não afirmar transcrição completa da engine, atributos,
  regras de efeitos, legalidade ou avaliação de todas as cartas.
- Manter o próximo trace opt-in, limitar ao primeiro turno da CPU,
  preservar o save e priorizar baixa sobrecarga. A validação do
  tempo de vida dos snapshots e restauração completa ainda falta.

Relacionado:
[especificação da IA](../research/ai-decision-engine.md) ·
[protocolo da captura](../research/ai-decision-detail-protocol.md) ·
[LP e seleções anteriores](normal-duel-lp-ai-live-validation-2026-11-11.md).
