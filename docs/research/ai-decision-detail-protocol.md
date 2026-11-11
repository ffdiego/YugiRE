# Piloto passivo — métricas do seletor normal da CPU

**Estado:** instrumentação implementada em 2026-11-10 e **validada em
execução real no Windows em 2026-11-11 UTC**, com duas decisões completas,
155 eventos íntegros, 22 eventos detalhados e três comparações de candidato
reproduzidas pela regra C#. [Evidência](../evidence/ai-detail-ranking-live-validation-2026-11-11.md).
O restante desta página descreve o protocolo replicável; os gates das cartas
não simuladas permanecem sem telemetria.
Alvo fixo: `joey_pc.exe`, SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Base de endereços: `0x00400000`.

## Objetivo

Compreender uma decisão da CPU com **métricas reais**, e não apenas
associar `select_normal` à próxima carta colocada. Primeira referência:
[duelo normal observado](../evidence/normal-duel-lp-ai-live-validation-2026-11-11.md),
no qual enter10→leave26 selecionou Gemini Elf, índice interno 550, e
comando CE30 preparou essa carta. A análise estática completa está em
[ai-decision-engine](ai-decision-engine.md), principalmente as seções 2, 4,
5 e 7.

O novo modo `trace ai on detail` preserva os quatro hooks originais de
`trace ai on` e instala **cinco hooks adicionais exclusivamente em entradas
de funções**. Os hooks observam chamadas naturais, não executam funções
do Joey, não escrevem em memória ou no save e não alteram argumentos
nem retornos. Não se usam callbacks em endereços no meio de funções.

| Função | VA | Campo `ai_detail.function` | Observação |
| --- | --- | --- | --- |
| Baseline | `005B08B0` | `baseline` | `returnSigned`, AA/AC; externo com caller `005B738A` ou interno ao seletor |
| Inserção hipotética | `005B1610` | `simulate_card` | `internalCardIndex`, `tributeCount`, slot em `returnSigned` |
| Acumulador | `005B0A50` | `aggregate_score` | score assinado em `returnSigned`, AA, AC, máscara atual |
| Evento escolhido | `005B0B90` | `select_combat_event` | `selectedEventHex` só quando retorno !=0; zero também registrado |
| Restore | `005B0950` | `restore_field` | máscara antes do restore, AA/AC e máscara atual depois |

Todos os eventos adicionais têm `kind=ai_detail`, `phase=leave`,
`side=0`, `threadId`, `caller`, `functionVa`, `returnRaw` e
`returnSigned`. A maioria traz `selectorCallId`, que é o
`ai_probe.callId` da seleção `select_normal` ativa na mesma thread.
Para o baseline **externo** anterior à entrada do seletor, vale `null`;
cruzar por `caller=0x5b738a`, thread, ordem e proximidade, sem inventar
um ID causal. Chamadas auxiliares fora de `select_normal` ou do caller
externo conhecido, e lado1, não são registradas nesse piloto.

`summaryRaw` corresponde ao WORD global `0x00A579AA`;
`actionMaskOutput` corresponde à metade baixa de
`0x00A579AC`; `actionMaskCurrent` corresponde ao WORD em
`0x00A55D70` para CPU/lado0. Em `restore_field`,
`actionMaskBeforeRestore` é lido na **entrada** do helper e
`actionMaskCurrent` em sua saída. O primeiro não é a mesma coisa que
a máscara restaurada.

A proposta escolhida é lida como quatro bytes de `0x00A579A6` na
saída positiva de `005B0B90`. Não somar todos os `combat_event`
do trace anterior: esses são candidatos de `005B0F40`, não
necessariamente a sequência selecionada pelo score.

**Limites:** a captura ainda não registra *todos os gates por carta*
(AAB90, AE3E0, AE220, tributos), nem os registradores/stack do comparador
em `005B154B`, nem prova restauração de todos os buffers. A primeira
coleta visa confirmar ABI, frequência e associação de score com as
hipóteses sem sobrecarregar o jogo. `NativeAiRanking` só poderá ser
comparado com entradas reais quando houver também a máscara/AA e o
bestScore correto na sequência completa.

## Execução sugerida (Windows, uma decisão)

Antes, faça backup do save e sincronize o repositório:

```powershell
git pull --ff-only
node .\native_hooks\tests\test_loader.cjs
.\native_hooks\.venv-frida\Scripts\python.exe .\native_hooks\native_rules.py --observe-only
```

Abra o Joey **manualmente** e aguarde o menu; o Python detecta o PID
quando há só uma instância. No console:

```text
trace on 20000
trace ai on detail
trace mark ai-detail-piloto
```

Inicie um **Single Duel normal** contra a CPU. Espere a **primeira
decisão de colocar um monstro da CPU** (se ela não ocorrer imediatamente,
avance alguns turnos, mas não precisa terminar o duelo). Então:

```text
trace ai off
trace off
trace dump
stop
```

Confira se `aiDetailActive=True` depois da ativação. Não habilite
`trace deck`, `trace demo`, comandos mutadores, overrides de velocidade
ou qualquer outro instrumento nesta sessão. Envie o JSONL íntegro.
O limite de 20.000 é compartilhado com `command` e `ai_probe`;
qualquer `overwritten>0` invalida uma janela considerada completa.

Se houver crash, **não repetir** a instrumentação detalhada: preservar
a captura parcial e o Event Viewer/minidump. Desligar o modo detalhado e
isolar hooks; não declarar a causa sem EIP/registradores.

## Critérios para a análise seguinte

1. Relacionar baseline externo (caller `005B738A`) e baseline interno
   (`selectorCallId`) na mesma thread e sessão.
2. Agrupar cada `simulate_card` com os `aggregate_score`,
   `select_combat_event` e `restore_field` subsequentes da janela.
3. Recalcular o total apenas dos eventos **escolhidos** com
   `score=int16`, conferir com o `returnSigned` de `aggregate_score`.
4. Comparar `summaryRaw`, máscara e score de cada candidato com
   `NativeAiRanking.AcceptNormalCandidate` sem preencher os gates
   ausentes. Classificar correspondência como `match`, `mismatch` ou
   `incomplete` e localizar o primeiro elo não observado.
5. Somente após uma decisão validada ampliar para filtros por candidato,
   outros modos, prioridades e duelos em lote.

**Resultado da primeira captura Windows:** a observação nativa registrou
baselines, scores, máscara e summary suficientes para reproduzir em C#
três avaliações: carta 834 aceita em empate por remoção adversária;
Karate Man 178 rejeitado por máscara0; e, na decisão seguinte, o mesmo
Karate Man aceito com score+1000. Os dois índices retornados pelo nativo
coincidem com o ranking dos candidatos simulados. O teste também registrou
um `selectedEventHex=0600E803`, da rota especial alvo5, compatível com
um comando posterior de 1000 de dano ao jogador.

Isso **ainda não reproduz uma decisão a partir da mão completa**:
os motivos pelos quais outras cartas não chegaram ao helper de simulação
não foram registrados. O próximo avanço deve observar os filtros por
candidato em chamadas naturais, mantendo a instrumentação passiva e
opt-in. [Relatório evento a evento](../evidence/ai-detail-ranking-live-validation-2026-11-11.md).
