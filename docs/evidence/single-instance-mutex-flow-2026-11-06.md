# Proteção de instância única: evidência e bypass opcional por nome do mutex

**Data:** 2026-11-06  
**SHA256 do alvo no relatório Ghidra:** `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`  
**Fonte:** relatórios fornecidos pelo operador `single-instance-mutex.txt`,
`single-instance-locks.txt`, `single-instance-windows.txt` e
`single-instance-flow-mutex-name.txt`. Não foi anexado aqui
`single-instance-flow-startup.txt`; a análise do chamador de
topo é limitada ao código já visível.

## Cadeia de controle documentada

1. `FUN_00411B50` chama `FUN_0055DB10` em `0x00411B7A`.
2. `FUN_0055DB10` (`0x0055DB10`) chama
   `GetModuleFileNameA(DAT_005F3484, local_214, 260)`,
   transforma cada `'\\'` em `'+'` e prepara uma string
   derivada do caminho do executável.
3. Em `0x0055DBEC`, chama `FUN_0054D460`. A montagem registrada:
   ```asm
   0055DBEC CALL 0x0054D460
   0055DBF1 XOR EDI, EDI
   0055DBF3 CMP EAX, EDI
   0055DBF5 JZ  0x0055DC4E
   ```
4. `FUN_0054D460` usa `CreateMutexA(NULL, TRUE, lpName)`
   e imediatamente consulta `GetLastError()`.
   Se o valor é `0xB7` (`ERROR_ALREADY_EXISTS`),
   retorna **1**; caso contrário, **0**.
5. No chamador, zero conduz ao bloco em `0x0055DC4E`
   e define `*(param_1+8)=1`. Quando o mutex já existe,
   o outro ramo define `*(param_1+8)=0` e invoca
   `FUN_005445C0`. Isso explica a escolha binária de
   disponibilidade durante o boot. O relatório não traz
   todos os consumidores desse campo booleano, mas o conjunto
   é evidência forte de bloqueio de segunda instância.

**Detalhe importante:** `DAT_005DA40C` aparece como fallback
para `lpName` na rotina de mutex e em centenas de outros
xrefs, provavelmente uma constante de string vazia utilizada
por funções auxiliares. A evidência do nome aplicado pela
inicialização vem de `GetModuleFileNameA` e da transformação
do caminho, **não** de identificar `DAT_005DA40C` como nome
literal do mutex.

## Intervenção escolhida

O Native Rules agora expõe a opção de linha de comando
`--allow-multi-instance`, **desligada por padrão** e restrita
a `--spawn --observe-only`.

Antes de `resume`, o agente Frida intercepta `CreateMutexA`,
mas só altera seu argumento `lpName` quando:

- `this.returnAddress` cai na função do executável
  `FUN_0054D460`, entre `0x0054D460` e `0x0054D550`;
- o nome original coincide (ignorando caixa) com o caminho
  do módulo recebido por Frida, substituindo `'\\'` por `'+'`;
- o argumento `lpName` não é nulo.

Nessas condições, `lpName` passa a ser
`YugiRE.Joey.<PID>`. O kernel cria um **mutex distinto para
cada processo**; não é necessário forçar retorno de
`GetLastError()` nem desabilitar sincronização do resto do jogo.
O executável no disco permanece inalterado. O trace inclui
`kind=singleton_mutex`, `phase=remapped`; não registra o caminho
original local em texto.

Se a checagem de nome falhar, o hook **não modifica** a chamada.
`multi status` retorna `remapped` e
`skippedUnexpectedName`, para distinguir sucesso de uma
inicialização em que o hook foi armado mas não acionado.

## Como testar duas instâncias sem modificar o EXE

Abrir uma instância em cada um de dois terminais PowerShell,
a partir de `native_hooks`:

```powershell
.\.venv-frida\Scripts\python.exe .\native_rules.py --spawn --exe "..\..\passion2\joey_pc.exe" --observe-only --speed-value 240 --allow-multi-instance
```

Em cada console, digitar `multi status`. O resultado
esperado **depois do boot** é `remapped: 1`,
`skippedUnexpectedName: 0` e `armed: True`.
Caso `remapped: 0` ou o segundo jogo encerre, o
experimento **não foi validado**; anotar as saídas e
inspecionar o evento de trace antes de avançar.

**Este modo ainda não foi validado executando duas instâncias
no Windows real.** Os testes de Python usam Frida simulado,
e não provam que o kernel aceitou os dois mutexes.

## Cuidados com concorrência

O bypass impede apenas a colisão do mutex ligado ao caminho
do executável. Duas instâncias ainda podem acessar o **mesmo**
diretório de decks, configurações, saves, replays, registro e
DirectPlay. Recomenda-se primeiro testar apenas
demonstrações automáticas sem gravar alterações de deck.
Para produção ou paralelismo intensivo, mapear e isolar o
estado mutável por instância.

A rotação sequencial com `--rotate-events` / `--cycles`
**não precisa** do bypass de mutex e continua habilitada
sem `--allow-multi-instance`.

## Próxima validação

- Rodar os testes de regressão e abrir duas instâncias em paralelo.
- Verificar `multi status` nas duas, gerar um pequeno
  `trace dump` de cada e conferir caminhos/PIDs.
- Confirmar que ambos executam demos, emitem `0x0004` e
  não corrompem dados em disco.
- Só então considerar workers paralelos controlados pelo Python;
  evitar o encerramento de PIDs que não sejam de sua própria sessão.
