# Demonstração do Joey usa seletores nativos nos dois lados (2026-11-05)

**Evidência dinâmica:** `commands-20261105-043508-188126-pid3400.jsonl`,
fornecido pelo operador em 2026-11-05.
Teste com `--observe-only` e hooks opt-in:
`trace on 20000`, `trace ai on`, `trace demo on`.
O operador inseriu marcador `demo comecou` (seq 26).
Sem invocação ativa de seletores pelo YugiRE;
o runtime de IA/execução observado pertence ao próprio Joey.
O arquivo bruto não foi commitado.

## Resumo quantitativo

A captura tem **797 linhas: 1 metadata + 796 eventos**,
durante cerca de **89,7 segundos**; `capacity=20000`,
`seen=796`, `retained=796`, `overwritten=0`.

| Tipo | Eventos |
| --- | ---: |
| `command` | 266 |
| `ai_probe` | 515 |
| `demo_executor` | 14 (7 entradas + 7 saídas) |
| `marker` | 1 |

Todos os eventos de seletor e executor têm `threadId=23196`.
Os `ai_probe` dividem-se em:
- **19** entradas e **19** saídas de seletores
  (9 chamadas `select_normal`; 10 chamadas `select_priority`);
- **468** saídas de `computed_stats`
  durante seleção (405 com argumento side 0 e 63 com side 1);
- **9** saídas de `combat_event`
  (8 side 0, 1 side 1).

**Atenção:** o `side` de `computed_stats` é
argumento da rotina interna; não equivale
à propriedade do turno/decisão. Para
atribuir controle de IA, usar os *seletores*
e executores, que recebem `side` diretamente.

## IA dos dois lados demonstrada por chamadas reais

| Função | side 0 | side 1 |
| --- | ---: | ---: |
| `FUN_005B1360` / `select_normal` | 5 | 4 |
| `FUN_005B16C0` / `select_priority` | 2 | 8 |
| `FUN_005B6FF0` / executor, *entradas* | 5 | 2 |

Os `ai_probe` exibem endereços de retorno
do próprio motor e não são `ai_console`:
- `select_normal`: `0x005B73A2` (pass 0)
  ou `0x005B73BA` (pass -1);
- `select_priority`: `0x005B74E0`
  e `0x005B7623`;
- `FUN_005B6FF0`: `0x005B73D7`
  e `0x005B7672`.

A sequência confirma ligação causal plausível:
- `seq 84`: `select_priority(side=1)` retorna
  `handIndex=0`; `seq 85`: executor
  `FUN_005B6FF0(side=1,handIndex=0,modeSelector=1)`
  com `handInternalIndex=151`; `seq 87`: retorna 1.
- `seq 149`: `select_normal(side=0,pass=-1)`
  retorna 1; `seq 150`: executor para
  `side=0,handIndex=1,modeSelector=0`,
  `internalIndex=444`.
- `seq 399`: `select_priority(side=1)`
  retorna 0; `seq 400`: executor
  `side=1,handIndex=0,modeSelector=1`,
  `internalIndex=1052`.
- `seq 606`: `select_priority(side=0)`
  retorna 1; `seq 607`: executor
  `side=0,handIndex=1,modeSelector=0`,
  `internalIndex=522`.

O mesmo callsite de executor `0x005B7672`
foi atingido para **ambos os lados**.
O `modeSelector=1` corresponde normalmente
a *set*, e `modeSelector=0` normalmente a
*summon*, ressalvado que o executor ainda
pode ajustar postura conforme o contexto.

A primeira chamada ao executor, `seq 17`,
é `side=0` e ocorre com
`duelRunning=false`, pouco antes do
marcador `demo comecou` (seq 26).
Ela foi contada entre as sete entradas,
mas **não se deve tratá-la como uma
colocação resolvida no campo ativo**.
As outras seis entradas indicam
`duelRunning=true`.

O retorno bruto do executor é `1`
nas sete chamadas: sem contrato de saída
validado, isso **não prova resolução final
de campo**.

## Passagem de turno observada

A captura contém **9** comandos `0x01`
de início de turno (lado pelo bit 15):
`0,1,0,1,0,1,0,1,0` — cinco turnos
do lado 0 e quatro do lado 1.

Os primeiros **8** turnos têm exatamente
um par `0x02`/`0x03` para o lado corrente.
O último (lado 0) termina com
`0x8004` (`opcodeMasked=0x0004`), sem
par `0x02`/`0x03`: padrão compatível
com encerramento de duelo, sem dados
para atribuir vencedor.

| Turno | Side | Retornos de seletores | Executor |
| ---: | ---: | --- | --- |
| 1 | 0 | priority 1 | hand 1 / mode 0; inicialização (`duelRunning=false`) |
| 2 | 1 | normal -1, priority 0, priority 0 | hand 0 / mode 1 / card 151 |
| 3 | 0 | normal 1 | hand 1 / mode 0 / card 444 |
| 4 | 1 | normal -1, priority -1, priority -1 | sem invocação de monstro via executor observado |
| 5 | 0 | normal 2 | hand 2 / mode 0 / card 241 |
| 6 | 1 | normal -1, priority 0, priority 0 | hand 0 / mode 1 / card 1052 |
| 7 | 0 | normal -1, normal -1, priority 1 | hand 1 / mode 0 / card 522 |
| 8 | 1 | normal -1, priority -1, priority -1 | sem invocação de monstro via executor observado |
| 9 | 0 | normal 1 | hand 1 / mode 0 / card 241; termina com `0x8004` |

Os nomes das cartas com índices internos
151, 1052, 444, 241 e 522 não estão
catalogados em `card-callback-catalog.json`;
por isso o relatório não inventa nomes.
Também foram observados 7 comandos
`opcodeMasked=0x00CE` (colocação associada
a relatos anteriores), e diversos outros
opcodes que demonstram atividade do motor.

## Conclusão, limitações e direção

**Confirmado:** a demo de menu chama **a
seleção nativa de IA para ambos os lados**,
executa colocações com o mesmo executor
de CPU para ambos e avança turnos
automaticamente. É muito mais forte
que um simples replay que só injeta
comandos de saída.

**Não confirmado:** se a demo usa
uma única flag de "controle da CPU" para
ambos, como o menu escolhe/decks, se
aplica IA a todos os tipos de ação,
ou se há decisões roteirizadas adicionais.
Um sistema de replay que invoque
algumas rotinas de IA ainda é
tecnicamente possível.

O caminho de investigação mais rentável
agora é **descobrir a inicialização e os
gates do escalonador da demo**: quem
aciona `0x005B73A2/0x005B73BA`,
`0x005B74E0/0x005B7623` e
qual variável permite o mesmo callsite
`0x005B7672` com side 0 e side 1.
Usar `FUN_005B726C` e
`FUN_005BC700` como raízes para
analisar a máquina de estados.
Não ativar flags arbitrárias nem
copiar `0x03` diretamente como
`end turn`.

Próxima extração sugerida:
```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-demo-scheduler.txt `
  -Addresses @(
    '005b726c',
    '005bc700',
    'ins:005b72a0:220',
    'ins:005b7590:155'
  )
```

Além disso, repetir uma demo com
`--observe-only` para comparar as
sequências e investigar seleção de decks.
