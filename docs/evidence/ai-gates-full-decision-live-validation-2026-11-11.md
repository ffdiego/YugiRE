# Validação nativa dos filtros e do ranking da IA — 2026-11-11 UTC

## Captura e integridade

Fonte original do operador Windows, não versionada:
`commands-20261111-043826-452599-pid23536.jsonl`.

SHA256: `6c93df404998d8b0807ecfb98831a73db5485d274d0cfb29b5ae876a6d6fc5d3`.
Período UTC: 2026-11-11 04:35:06.310 a 04:38:14.465 (188,155s).
Um metadata + **890 eventos**, sequência 1..890 contínua,
`seen=retained=890`, `overwritten=0`, capacidade 20.000.
Distribuição: 345 `command`, 343 `ai_probe`, 126 `ai_detail`,
74 `ai_gate`, 2 `marker`. Não houve `readError` em `ai_gate`
ou `ai_detail`. `metadata.exeSha256=null`: o JSONL não
contém hash atestado do binário; associar ao contrato de instrumentação
do EXE esperado, não afirmar atestado automático.

Os markers iniciais são `ai-gates-primeira` (seq5) e
`estava-no-demo-agora-eh-o-duelo` (seq8). Há uma chamada
`select_priority` em seq18..24 com `duelRunning=false` e
CE28 para card307. **Não incluí-la no corpus de decisão normal ativa**:
o operador indicou que iniciou na demo antes do duelo.

Dentro do duelo ativo houve **sete** `select_normal` completos:
seis retornos de índice válido, um `-1`. Os seis retornos
positivos têm um `0x00CE` posterior do lado CPU, com
`ceSourceIndexCandidate` e `ceHandIndexCandidate` iguais à
carta/posição do retorno (seq148,364,518,626,801,878).
Há portanto **6/6 correspondências temporais CE**, não uma
prova de todas as ações posteriores resolvidas.

## O que foi observado nos filtros

Em 26 posições de mão examinadas pelos seletores normais:
- `eligibility_mask` (AAB90): 26 chamadas, 24 retornos não-zero
  e **duas rejeições zero**, card851 / Backup Soldier (seq621)
  e card729 / Giant Trunade (seq873);
- `resource_policy` (AE3E0): 24 chamadas, todas retornaram0
  (nenhuma rejeição);
- `special_policy` (AE220): 24 chamadas, 17 retornaram0 e
  **sete retornaram1**, todas para card839 / Penguin Soldier
  (uma por seleção).
- **17 cartas** ultrapassaram esses três filtros; **15**
  foram inseridas por `FUN_005B1610` e avaliadas, e **duas**
  não chegaram à simulação no seletor `pass=1`.
  A política estática do `pass=1` rejeita classes de nível
  1..4; a captura não registra diretamente a chamada de classe
  `FUN_004026C0` nem o branch tomado. Assim o motivo das duas
  ausências é consistente com o assembly, mas **inferido** e
  não medido individualmente.

Um retorno `reject` significa rejeição **no seletor normal da CPU**;
não significa carta proibida ao jogador ou impossibilidade de set
via outra política.

## Replay do comparador com as métricas capturadas

Para cada candidato efetivamente simulado foram usados:
ATK em `computed_stats` para o card simulado, slot retornado
por `simulate_card`, `aggregate_score.returnSigned`,
`restore_field.actionMaskOutput` e
`restore_field.summaryRaw`. O baseline e perdas rivais
iniciais vêm de `baseline` **interno** à chamada de seletor.
A transcrição usada é
`NativeAiRanking.AcceptNormalCandidate`, já existente em C#.

| Seleção (enter→leave) | Pass | Baseline | Candidatos simulados (card: score/aceitação) | Previsto = nativo |
| --- | ---: | ---: | --- | --- |
| 55→144 (ID 2954) | -1 | 0 | 24: 2300 **aceita**; 857: 2300 rejeita no desempate; 550: 2300 rejeita no desempate | **posição0 / card24** |
| 202→360 (ID 12651) | 0 | 2500 | 857: 4200 aceita; 550: 4400 aceita; 261: 3600 rejeita | **posição2 / card550** |
| 459→514 (ID 31727) | -1 | 0 | 857: 1700 aceita; 261: 1100 rejeita; 380: 1100 rejeita | **posição0 / card857** |
| 554→622 (ID 33504) | -1 | 0 | 261: 1700 aceita; 380: 1700 rejeita no empate | **posição1 / card261** |
| 672→701 (ID 37786) | 1 | 2800 | nenhum; 839 rejeitada no special_policy; 380 e 550 passam 3 filtros mas não chegam à hipótese | **-1** |
| 702→797 (ID 37800) | -1 | 2800 | 380: 3900 aceita; 550: 4700 aceita | **posição2 / card550** |
| 844→874 (ID 39379) | -1 | 0 | 380: 1100 aceita; 515: 0 rejeita (actionMask=0), mesmo tendo ATK2100 | **posição1 / card380** |

Em **15/15 candidatos simulados**, a aceitação ou rejeição
prevista pelo comparador C# explica a atualização sequencial de
`bestIndex`, `bestScore` e contagem de perdas rivais.
Em **7/7 seletores**, o índice final reconstruído concorda com
o retorno original (incluindo um retorno -1).

### Casos ilustrativos

**Seleção 2954, primeiro candidato:** card24 (Axe Raider), ATK
1700, slot1, score2300, máscara3, summary136. Atualiza score0→2300.
Cards857 (ATK1700) e550 (Gemini Elf, ATK1900) produzem ambos
score2300, máscara3, summary136, sem melhora de perdas rivais,
portanto não substituem o primeiro candidato. A IA não desempata
pelo ATK maior.

**Seleção 12651:** baseline2500; card857 score4200 e
Gemini Elf550 score4400, de modo que a última substitui a primeira.
Card261 (Witch of the Black Forest) score3600 não supera 4400.

**Seleções 37786→37800:** mesmo estado de mão `[839,380,550]`
e mesma thread. Em `pass=1`, special_policy rejeita839,
380/550 não entram na simulação e o resultado é -1.
O caller imediatamente executa `pass=-1`; scores 3900 e 4700
fazem a Gemini Elf vencer. É evidência dinâmica da existência
de dois passes com máscaras de admissibilidade distintas;
as regras exatas de classe/tributo provêm do assembly.

**Seleção 39379:** card380 tem ATK1100, score1100,
actionMask1 e é aceito. Card515 tem ATK2100 mas score0 e
actionMask0; não passa no comparador. Outra refutação direta da
heurística de ATK máximo como regra única.

## Conferência dos confrontos selecionados

Em todos os **15** estados hipotéticos simulados, a soma assinada
dos highWords dos eventos com `select_combat_event.returnSigned!=0`
bate com o `aggregate_score.returnSigned`.
Exemplos: card550 da seleção12651: 200+1900+2300=4400;
card550 da seleção37800: 1100+1700+1900=4700.

Há eventos de rota especial (lowWord bit0x2) com valor positivo,
compatíveis com ataques diretos; não confundir proposta de
`combat_event` com o evento **selecionado** pelo loop do score.
Os descritores continuam sendo resultados hipotéticos, não comandos
emitidos ao executor.

## LP e resultado observado

A captura registra uma condição terminal no evento890 com
`opcodeRaw=0x8004`, `lifePointsObserved.lifePoints=[5700,0]`
e `terminalNativeState.lifePoints=[5700,0]`.
A CPU/lado0 tinha 5700 LP e o jogador/lado1 tinha0.
A queda final observada do jogador para0 ocorre após
`0x0043` de 1100 quando ele tinha200 LP.
Isso demonstra terminal por dano compatível com vitória da CPU,
sem atribuir a todos os bits de `0x8004` uma semântica não validada.
O evento no início da demo (`duelRunning=false`) não é
base para essa conclusão.

## Limites da validação e próximos passos

- **Reproduzimos o fluxo de decisão usando saídas capturadas dos
  helpers nativos**, não uma IA C# independente que calcula atributos,
  eventos, masks e legalidade do zero.
- Alguns filtros internos e o branch de nível/passe ainda são
  determinados pelo assembly, não medidos na mesma captura.
- Nenhuma captura de restore byte-a-byte prova isolamento completo
  dos buffers ou independência do RNG. Não introduzir chamadas
  artificiais à engine ou patches na lógica nativa.
- Próxima coleta, se necessária: classe `FUN_004026C0` e
  disponibilidade de tributos somente nas chamadas diretas do
  seletor em um experimento opt-in separado. Também vale capturar
  casos de priority, ataque contra ocultos e empate de score com
  contadores de perdas diferentes.
- O JSONL original permanece no Windows/uploads, não no repositório.
  Os testes de regressão codificam as métricas derivadas e os
  sete retornos esperados; não chamam o jogo.

Relacionados:
[especificação da IA](../research/ai-decision-engine.md),
[protocolo dos gates](../research/ai-selection-gates-protocol.md),
[validação anterior de dois seletores](ai-detail-ranking-live-validation-2026-11-11.md).
