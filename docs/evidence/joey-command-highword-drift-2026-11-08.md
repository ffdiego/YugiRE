# Identificação preliminar das diferenças nos 16 bits superiores dos comandos

> **Atualização após os três relatórios do Ghidra (2026-11-08):**
> a causa de **115 das 119 diferenças** está demonstrada em
> instruções nativas (114 resíduos do ponteiro da tabela CARD_ID
> após `MOV AX` em `FUN_004022E0`, um resíduo de `EDX`).
> Dois casos têm o mesmo padrão mas não foram verificados em
> seus callsites, e outros dois permanecem sem explicação.
> Leia o relatório posterior
> [Origem dos high words](joey-command-highword-origin-2026-11-08.md).
> A hipótese preliminar de que todos os valores superiores
> representariam identificadores de instâncias de cartas
> **não deve mais ser adotada**.


**Data:** 2026-11-08. Capturas `parallel-mjj1z5ia.zip` e `parallel-a1xf_v2y.zip`, com mesmos decks, master seed 12345 e execução independente ~9 minutos depois.

## Resultado confirmado pelos JSONL

Em ambos os duelos, mesma sequência `kind/function/phase`, mesmas ordens dos decks (incluindo `deck.words`), mesmas avaliações de IA, mesmos opcodes e chamadores dos comandos, mesmos 16 bits inferiores de todos os argumentos e mesmo snapshot `terminalNativeState`. Não há prova de identidade do estado completo ou vencedor.

| Índice global | Comandos alterados | Arg1 majoritário | Exceções |
|---|---:|---|---|
| 0 | **51/354** | **49** vezes `0x00EA → 0x00F4` | 1 em `args[0]` opcode `0x0037`, 1 em `args[1]` opcode `0x0086`, ambas `0x0635 → 0x0647` |
| 1 | **68/557** | **67** vezes `0x00FC → 0x00DC` | 1 em `args[0]` opcode `0x0061`, `0x0537 → 0x0536` |

Os 119 comandos alterados têm **exclusivamente diferenças nos bits 16–31**. Contagens por origem (endereço de retorno para o produtor `FUN_005B9170`):

| Opcode | Return address | Global 0 | Global 1 | Argumento |
|---|---|---:|---:|---|
| `0x00CE` | `0x005ABEAB` | 10 | 17 | `args[1]` |
| `0x00E6` | `0x005ABD70` (um caso também `0x005ACF72`) | 8 | 14 | `args[1]` |
| `0x006F` | `0x005ABF22` | 8 | 13 | `args[1]` |
| `0x00CF` | `0x0057977F` | 8 | 8 | `args[1]` |
| `0x0072` | `0x005783F2` (um caso `0x005783BC`) | 8 | 6 | `args[1]` |
| `0x00DF` | `0x00595142` | 5 | 5 | `args[1]` |
| `0x0075` | `0x00578459` | 2 | 4 | `args[1]` |
| `0x0037` | `0x0058CF96` | 1 | 0 | `args[0]` |
| `0x0086` | `0x00574804` | 1 | 0 | `args[1]` |
| `0x0061` | `0x005BA000` | 0 | 1 | `args[0]` |

Exemplo real: `0x00CE` argumento 1 `0x00EA02E1` vs `0x00F402E1`. A palavra inferior `0x02E1` não muda. Esse padrão sugere um ID/campo de contexto compartilhado entre vários opcodes, **mas isso é hipótese**; nenhum desses bits deve ser apagado do trace. Não sabemos se fazem parte de identificadores únicos, efeitos gráficos, identificadores de sessão, ou outra estrutura.

## Próximo exame estático

Execute `scripts/Run-CommandHighWordRecon.ps1` com o projeto Ghidra. O script inspeciona as instruções de montagem dos argumentos antes de chamar `FUN_005B9170` em três famílias de comandos.

```powershell
.\scripts\Run-CommandHighWordRecon.ps1
```

Arquivos de saída: `docs/evidence/command-highword-placement.txt`, `command-highword-card-commands.txt` e `command-highword-other.txt`.

Objetivo: rastrear a escrita dos bits superiores até seus locais de origem, comparar com os registros nativos das cartas e só então decidir se são componentes semânticos, IDs normalizáveis sob regra explícita ou dados independentes da engine C#.

## Comparador agora disponível

`python -m yugilab.compare first.zip second.zip --output comparison.json` verifica a reprodutibilidade dos checkpoints observáveis, mas reporta os valores brutos alterados e permite exigir `--strict-raw`. Não se pode inferir resultado/vencedor apenas do comando terminal `0x0004`.
