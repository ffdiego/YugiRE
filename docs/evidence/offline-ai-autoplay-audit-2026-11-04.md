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
- pesos, aleatoriedade e função de avaliação das decisões;
- como a UI solicita o encerramento de turno de modo seguro;
- validação real do novo comando `set`.

## Experimentos futuros de alto rendimento

**No Fedora, se o Ghidra já estiver acessível:** extrair
as instruções ao redor de três chamadas à
`FUN_005B6FF0` e seguir os produtores do `handIndex`.
Esse estudo pode ocorrer sem o jogo rodando.

**Quando o Windows voltar:** testar `set check`/`set`
e usar hooks passivos para comparar o modo demo com um
duelo normal. Coletar também dois ou mais decks do lado
CPU em `.ydr` para investigar escolhas de deck por
comparação, não por suposição.

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
