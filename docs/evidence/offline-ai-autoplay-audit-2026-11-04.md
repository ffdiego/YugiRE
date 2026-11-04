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
do campo para avaliar candidato**. Este último
ponto torna urgente localizar as rotinas de
save/restore para não confundir simulação com
jogada real nem arriscar estado corrompido.
Ver [relatório dos helpers](ai-decision-helpers-2026-11-04.md).
Seleção de deck da CPU continua separada.

**Quando for possível executar o Windows novamente:**
o primeiro teste de `set` já foi concluído com sucesso.
O próximo experimento dinâmico é comparar o modo demo
com um duelo normal usando hooks passivos. Coletar
também dois ou mais decks da CPU em `.ydr` para
investigar variabilidade por comparação, não
por suposição.

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
