# Auditoria offline: roteiro para laboratório de duelos

Data: 2026-11-04. Nenhuma execução do Joey neste estudo.
Fontes: arquivos já versionados em `docs/evidence`,
especialmente `summon-builder-callsites.txt`,
`relinquished_commands.csv`, `turn-transition-cleanup-sequence.txt`
e `turn-processing-mode-selection.txt`.

## Conclusões úteis sem Windows

1. A candidata `FUN_005B6FF0` **prepara uma jogada
   anteriormente escolhida**; sua assinatura é
   `(side,handIndex,modeSelector)`. Chamadores
   `0x005B73D2`, `0x005B7534`, `0x005B766D`.
   Eles são alvos melhores do que o construtor para
   investigar o algoritmo de seleção da IA.
2. `FUN_005A8A50` possui dois subestados que produzem
   `0x02` e `0x03`, mas a máquina de estados superior
   `0x005BC700` só os alcança após resolver apresentação,
   fila, contextos de colocação e callbacks.
   **Não usar um `0x03` direto como `end turn`**.
3. O replay `relinquished_commands.csv` tem 1.062
   eventos e 39 turnos inferidos por `0x01`.
   Dos 39, os primeiros 38 apresentam exatamente
   um `0x02` e um `0x03`; o 39º termina em `0x04`.
   Há 19 `0xCE`, no máximo um por turno na amostra.
   Esse conjunto oferece um modelo para um exportador
   de laboratório agrupado por turno.

## O que não conseguimos provar apenas com o repositório

- Se o modo de demonstração do menu usa replays ou IA;
- como o oponente seleciona um deck;
- pesos exatos, RNG e semântica completa da avaliação
  de cenário (a comparação por ATK/DEF e uma tabela
  de prioridades específicas já foram identificadas);
- como a UI solicita o encerramento de turno de modo seguro;
- validação real de `set` sob efeitos especiais
  e outros modos; um set básico já teve sucesso.

## Experimentos futuros de alto rendimento

**Ghidra, próximo passo:** `FUN_0056D340`
confirmou a busca linear por ID de carta;
`FUN_005AE0A0` expôs limiares de decisão
`300/1000` e retornos `-1/0/1`;
`FUN_005AFFD0` resume o maior atributo
de campo; **`FUN_005B1610` modifica registros
do campo para avaliar candidato**. As rotinas posteriores `FUN_005B08E0` e
`FUN_005B0950` **confirmaram** backup/restore
dos dois campos de monstros, mas não um isolamento
completo de todas as globais. Ver
[ciclo confirmado](ai-simulation-state-lifecycle-2026-11-04.md)
e [relatório dos helpers](ai-decision-helpers-2026-11-04.md).
Seleção de deck da CPU continua separada.

**Quando for possível executar o Windows novamente:**
o primeiro teste de `set` já foi concluído com sucesso.
O próximo experimento dinâmico é comparar o modo demo
com um duelo normal usando hooks passivos. Coletar
também dois ou mais decks da CPU em `.ydr` para
investigar variabilidade por comparação, não
por suposição.

**Pontuação já identificada:** `FUN_005B08B0`
calcula baseline do estado atual; cada hipótese
de `FUN_005B1360` calcula novo score
`FUN_005B0A50`. `FUN_005B0B90` seleciona
slots ordenados por prioridade e ATK;
`FUN_005B0D90` é o próximo alvo para
entender evento e contribuição para o score.
Ver [relatório](ai-simulated-combat-scoring-2026-11-04.md).

**Alvos de combate identificados:** a extração
de `FUN_005B0D90` confirmou a varredura
dos cinco alvos por atacante, os filtros de
elegibilidade e a comparação de eventos
de quatro bytes gerados por `FUN_005B0F40`.
`DAT_00A579A8` é a palavra alta do evento
e alimenta a soma do score; é prematuro
identificá-la diretamente como dano aos LP.
Ver [relatório](ai-combat-event-producer-2026-11-04.md).

**Fórmula de confronto identificada:** `FUN_005B0F40`
grava diferença assinada de contribuições em
`event[1]` (16 bits); o bit `0x08` permite
score positivo ou destruir adversário (`0x800`)
sem perda própria (`0x400`). Ramo de alvo
`5` sugere ataque direto; há exceções de
posicionamento, perfuração e efeitos de carta.
Ver [relatório da fórmula](ai-combat-outcome-scoring-2026-11-04.md).
O próximo passo é mapear as saídas de ATK/DEF
de `FUN_005ADD60`/`FUN_005ADDC0`.

**Antes de autoplay:** criar eventos semanticamente
separados (`turn_begin`, `action_candidate`,
`action_selected`, `action_submitted`, `action_resolved`,
`turn_end`); registrar snapshot da mão e do campo e
distinguir seleção da IA, submissão ao motor e resolução
pelo consumidor. O conjunto de 1.062 registros fornece
uma verificação offline de limites de turno, não de
correção das regras.

A primeira etapa do controlador deve ser um observador;
atuar nas duas mãos ou acelerar o duelo requer novos gates
de segurança, além dos já disponíveis ao `summon`/`set`.
