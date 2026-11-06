# Speedy — interpretação do argumento e fontes de tempo (Ghidra, 2026-11-06)

## Origem das evidências

Análise dos relatórios locais enviados pelo operador:
`speedy-recon-argument-strings.txt`,
`speedy-recon-timer-imports.txt` e
`speedy-recon-duel-handlers.txt`.

Todos apontam para o mesmo EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Consultas estáticas em Ghidra; não houve execução de nova demo, captura
de benchmarks adicionais ou modificação do executável nesta etapa.

## 1. `-speedy` altera um global específico

- String ASCII `-speedy` em **`0x005EB784`**.
- Xrefs na função **`FUN_00411B50`**, cujo chamador indicado é
  `entry` em `0x005C5F33`.
- Depois da busca por `-speedy` via `FUN_004127A0`, o ramo
  `if (iVar2 != DAT_005DA354)` executa:

```c
_DAT_005EB618 = 0x3C; // 60 decimal
```

**Fato:** o argumento altera `0x005EB618` para 60.
**Hipótese:** o global regula frequência de atualização, tick ou FPS.
Não há neste relatório os consumidores do global nem o valor inicial
sem o argumento; o número 60 não basta para concluir que ele seja FPS,
nem para determinar a aceleração efetiva de uma demo.

A busca de strings não mostrou outras rotinas consumidoras de `-speedy`.
Isso não exclui efeitos indiretos por meio do global alterado.

## 2. Imports de tempo e wrappers reais

- `Sleep` está importado e referenciado por diversas funções, incluindo
  `FUN_005C1E2D`, `FUN_004A8BE0`, `FUN_004A60C0`,
  `FUN_005C4668`, `FUN_00493A40`, `FUN_00494700`.
  Exemplos da decompilação incluem `Sleep(1)` para sincronização,
  `Sleep(500)` em inicialização e `Sleep(1000)` como segunda tentativa
  de `GetDeviceCaps`. **Nenhuma dessas chamadas foi ligada diretamente
  ao intervalo entre os comandos do duelo.**
- `FUN_005653E0` calcula o intervalo desde um timestamp guardado em
  `param_1 + 4`; `FUN_00565420` atualiza esse timestamp.
  Ambas escolhem **`GetTickCount` se `DAT_00A53738 == 0`**, ou
  **`timeGetTime` em caso contrário**.
- `FUN_00545730` também escolhe a fonte de tempo por
  `DAT_00A53738`. Já `FUN_00401370` e `FUN_00401770`
  fazem chamadas diretas a `timeGetTime`.
- Os símbolos `SleepEx`, `GetTickCount64`,
  `QueryPerformanceCounter` e `SetTimer` não foram encontrados
  **nessa busca de símbolos**; não concluir que o jogo careça de
  outros relógios, wrappers ou mecanismos internos.

## 3. Emissão dos comandos e hipótese de bloqueio

A mediana `0x0001 → 0x0050` foi ~1,08 s em duas capturas, **ambas
executadas com `-speedy`**, conforme
[baseline anterior](demo-speedy-timing-baseline-2026-11-06.md).

As instruções do relatório de handlers mostram:

- `FUN_005A59B0` emite o comando `0x0001` por `FUN_005B9170`
  no `CALL 0x005A5A08` (return address `0x005A5A0D`) e
  avança a subetapa de `0x00A577FE`.
- `FUN_005A41C0` emite `0x0050` por `FUN_005B9170`
  no `CALL 0x005A421F` (return address `0x005A4224`),
  condicionado a flags observadas no objeto associado ao lado.
- O despachante `FUN_005BC700` só chama o handler da fase após as
  condições anteriores de `thunk_FUN_0040CF20` e
  `FUN_005B9CA0` retornarem zero.

**Hipótese de trabalho:** a espera entre as duas emissões pode estar
associada a apresentação/animação ou processamento da fila impedindo
o avanço. **Não está demonstrado** que seja `Sleep(1080)`, 60
frames, ou que o delay resida em um desses dois handlers.

## Próxima consulta recomendada (sem alterar o jogo)

Use `scripts/Run-SpeedyConsumers.ps1` com o banco Ghidra já importado.
A consulta precisa responder, nesta ordem:

1. **Todos os xrefs de `0x005EB618`:** quem lê o parâmetro 60,
   qual é o valor inicial, e quais cálculos/limiares dependem dele.
2. **Relógio do loop:** consumidores de `DAT_00A53738` e
   chamadores de `FUN_005653E0`, `FUN_00565420`,
   `FUN_00545730`; distinguir temporizadores de UI, engine e áudio.
3. **Gates de progressão:** conferir `FUN_0040CF20` e
   `FUN_005B9CA0` e suas condições de retorno quando o
   `FUN_005BC700` tenta avançar.

Somente com as dependências conhecidas decidir por modificação
opt-in de timing em memória. Não substituir globalmente `Sleep`,
`timeGetTime` ou `GetTickCount`: esses serviços também controlam
sincronização e outras partes do processo.

## Estado de validação

As conclusões de código acima são suportadas pelos três relatórios
fornecidos. O novo roteiro de consulta não foi executado localmente.
