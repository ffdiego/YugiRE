# Segunda rodada de engenharia reversa de `-speedy`: consumidores, clock e gates

**Data:** 2026-11-06  
**Executável:** `joey_pc.exe` — SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`  
**Origem:** três relatórios Ghidra do operador: `speedy-consumers-global.txt`, `speedy-consumers-clock.txt` e `speedy-consumers-duel-gates.txt`.  
**Escopo:** análise estática; sem novos duelos, patching ou benchmarking.

## Conclusões verificadas

### 1. Apenas uma xref direta a `0x005EB618`

A consulta `ref:005eb618`/`xref:005eb618` retornou somente:

```text
XREF From: 00411CA2 To: 005EB618 Type: WRITE ... FUNCTION 00411B50
```

As instruções conferem:

```asm
00411C95 CALL 004127A0
00411C9A CMP EAX,[005DA354]
00411CA0 JZ 00411CAC
00411CA2 MOV DWORD PTR [005EB618],0x3C
```

Logo, `-speedy` escreve **60 decimal** nesse endereço, mas **o relatório
não identificou leitores diretos**. Não é correto declarar o global
como `FPS` ou `game speed` sem evidência de consumo. O Ghidra pode não
resolver acessos indiretos, por ponteiros, indexação de uma estrutura,
cópia de memória ou referências codificadas de outra forma. A ausência de
xrefs diretas **não prova** que o campo não é utilizado.

### 2. `DAT_00A53738` é um seletor da fonte de tempo

`FUN_005651B0` usa `timeGetDevCaps` para obter informações de resolução,
define `DAT_00A53738 = DAT_00A53730 < 10`, e chama
`timeBeginPeriod(DAT_00A53730)` quando as condições são satisfeitas.
`FUN_00565220` chama `timeEndPeriod` na limpeza.

Os leitores encontrados da flag incluem:

- `FUN_005653E0`: devolve um delta desde timestamp armazenado;
- `FUN_00565420`: registra novo timestamp;
- `FUN_00545730`: registra outro timestamp.

Esses wrappers usam `GetTickCount` quando a flag vale zero,
ou `timeGetTime` quando não vale zero. Isso é gerenciamento de
resolução/fonte de relógio, **não um multiplicador de velocidade**.
Nenhum elo com `0x005EB618` foi identificado.

`FUN_00494C10` espera até `FUN_005653E0()` alcançar `0x19`
(25 ms) num laço; seu papel específico não foi vinculado à demo.

### 3. Dois gates impedem a progressão do duelo

Na rotina `FUN_005BC700`, a condição equivalente é:

```c
if (thunk_FUN_0040CF20() == 0 &&
    FUN_005B9CA0() == 0 &&
    currentHandler() != 0) {
    advancePhase();
}
```

- **Gate de apresentação `FUN_0040CF20`:** testa o bit 0 de
  `DAT_00A5506A`; se ativo, despacha para um dos handlers
  `FUN_0040CFA0`, `FUN_0040D070`, `FUN_0040D550`,
  `FUN_0040D840`, `FUN_0040D900` e retorna **1** para casos conhecidos.
  Retorna **0** caso não haja tratamento ativo.
- **Gate de fila `FUN_005B9CA0`:** processa a fila
  (`DAT_00A558A8`), estágios de comando (byte alto de
  `0x00A5780E`) e flags de continuação
  (`DAT_00A558AC & 0x4000`, `DAT_00A58260 & 0x100`).
  Pode retornar **1** enquanto houver processamento pendente e
  **0** quando a sequência permitir progredir. As condições não
  equivalem simplesmente a `queueCount > 0`.

**Hipótese:** o intervalo de aproximadamente 1,08 segundo entre os
comandos `0x0001` (em `FUN_005A59B0`) e `0x0050` (em
`FUN_005A41C0`) é dominado por apresentação, fila, outro handler ou
pacing do loop externo. Não é possível distinguir as causas com
decompilação estática isoladamente. As duas capturas dessa medida foram
realizadas **com `-speedy` ativo**.

## Próximo experimento: profiling passivo dos gates

Antes de testar `speed 2` ou alterar `Sleep`, adicionar ao agente
Frida um **profiler opcional, somente leitura**, com as seguintes
características:

1. Hooks de entrada/saída em `FUN_0040CF20` e `FUN_005B9CA0`
   e, se o custo for aceitável, contador de entradas no agendador
   `FUN_005BC700`. Não mudar argumentos, retornos ou estado.
2. Contar por função quantas chamadas retornam zero/não zero.
   Contar também estados do byte `DAT_00A5506A`, estágio de
   `0x00A5780E` e presença de `DAT_00A558A8` no momento
   do retorno; tudo agregado em memória para não inundar o JSONL.
3. Anexar um snapshot dos contadores **nos eventos** do produtor
   `FUN_005B9170` de opcode `0x0001` e `0x0050`.
   As diferenças entre dois snapshots do mesmo turno indicarão
   quais gates permaneceram ativos durante o intervalo.
4. Manter os hooks **opt-in**, instalar/desinstalar reversivelmente
   e comparar a captura com e sem profiling para medir seu overhead.
5. Se a maior parte dos bloqueios vier da apresentação, examinar
   os handlers `FUN_0040CFA0`, `FUN_0040D070`,
   `FUN_0040D550`, `FUN_0040D840`,
   `FUN_0040D900`. Se vier da fila, continuar por
   `FUN_005B9470` e pelos flags de confirmação.

A gravação do contador pode ocorrer apenas nos eventos de
comando/fronteira: **não criar um evento JSONL para cada tick**.

## Restrições e próximo critério de avanço

Não trocar globalmente `GetTickCount`, `timeGetTime` ou `Sleep`;
essas funções atendem a sincronização e temporizadores além da demo.
Não forçar `FUN_005BC700` a avançar sem completar contextos/filas.
Testar eventual aceleração de forma reversível, inicialmente apenas
na demo e com comparação da sequência de comandos e integridade
dos decks/efeitos.

**Status:** análise concluída a partir dos três relatórios recebidos;
profiler dinâmico ainda não implementado nem validado no Windows.
