# YugiRE — origem dos 119 argumentos brutos divergentes

**Data:** 2026-11-08. **Exe verificado por SHA256:**
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

**Entradas:** `command-highword-card-commands.txt`,
`command-highword-placement.txt`, `command-highword-other.txt`
fornecidos pelo operador. Conferência contra os JSONL de
`parallel-mjj1z5ia.zip` e `parallel-a1xf_v2y.zip`
(mesma seed global 12345, decks iguais, execuções independentes).

O operador confirmou **139 checks passaram em `dotnet run`**
para o projeto Joey.Tests, incluindo o módulo nativo de RNG/shuffle.
Essa validação é independente da análise dos comandos.

## A descoberta central: `FUN_004022E0` devolve EAX com lixo do ponteiro nos bits superiores

`FUN_004022E0` converte um índice interno de carta no ID de recurso
da tabela `CARD_ID`. O código em
`docs/evidence/card-id-instructions.txt` contém:

```asm
00402322 MOV ECX,0x5F2448
00402327 CALL 0x00542220      ; EAX = ponteiro para CARD_ID
0040232C AND ESI,0xFFFF
00402332 MOV AX,[EAX+ESI*2]   ; escreve apenas os 16 bits baixos!
00402336 POP ESI
00402337 RET
```

Portanto, quando o chamador faz `PUSH EAX` sem estender `AX`,
o valor é composto pelos 16 bits altos do ponteiro carregado
naquele processo e pelos 16 bits baixos do ID verdadeiro.
Não é uma identidade de carta de 32 bits.

Dois exemplos reais do mesmo opcode `0x00CE`:
`0x00EA02E1` e `0x00F402E1`, com ID real `0x02E1`.
No segundo duelo, os ponteiros davam os altos
`0x00FC` e `0x00DC`. Esses altos mudam com a alocação
de memória do processo, **não** com a seed controlada.

## Classificação auditada por opcode e return address

| Opcode / endereço após CALL | Arg alterado | Global 0 | Global 1 | Diagnóstico |
|---|---:|---:|---:|---|
| `CE / 005ABEAB` | args[1] | 10 | 17 | `FUN_004022E0` comprovada |
| `CF / 0057977F` | args[1] | 8 | 8 | `FUN_004022E0` comprovada |
| `E6 / 005ABD70` | args[1] | 8 | 13 | `FUN_004022E0` comprovada |
| `6F / 005ABF22` | args[1] | 8 | 13 | `FUN_004022E0` comprovada |
| `72 / 005783F2` | args[1] | 7 | 6 | `FUN_004022E0` comprovada |
| `DF / 00595142` | args[1] | 5 | 5 | `FUN_004022E0` comprovada |
| `75 / 00578459` | args[1] | 2 | 4 | `FUN_004022E0` comprovada |
| `61 / 005BA000` | args[0] | 0 | 1 | `MOV DX` preserva EDX[31:16] |
| `72 / 005783BC` | args[1] | 1 | 0 | Padrão da tabela, callsite a confirmar |
| `E6 / 005ACF72` | args[1] | 0 | 1 | Padrão da tabela, callsite a confirmar |
| `37 / 0058CF96` | args[0] | 1 | 0 | Origem ainda desconhecida |
| `86 / 00574804` | args[1] | 1 | 0 | Origem ainda desconhecida |

**Total:** 114 alterações de alta palavra com origem
confirmada em `FUN_004022E0`; mais 1 (`0x61`)
confirmada como resíduo de escrita parcial do registrador;
2 casos *candidatos* de `CARD_ID`; 2 casos ainda desconhecidos.
Todas as 119 diferenças preservam a metade baixa do argumento.

### Por que o opcode `0x61` também contém bits residuais

O relatório `command-highword-other.txt` registra:

```asm
005B9FE6 MOV DX,[0x00A577FA]
005B9FEF SHL DX,0xF
005B9FF5 OR EDX,0x61
005B9FFA PUSH EDX
005B9FFB CALL 0x005B9170
```

`MOV DX` e `SHL DX` preservam a metade superior de
`EDX`; o `OR EDX` também não a limpa. Na nossa amostra
ela variou de `0x0537` para `0x0536`, sem modificar o
código terminal baixo do comando.

### Limites da conclusão

A origem de 115 altas palavras está demonstrada no assembly.
Isso **não prova**, isoladamente, que todos os consumidores
do protocolo ignoram essas metades altas; a equivalência
integral do estado interno ainda depende de inspeção adicional.
Os arquivos JSONL preservam os valores completos; nenhuma
transformação destrutiva será aplicada a eles.

Os dois casos `72/005783BC` e `E6/005ACF72`
têm alta palavra exatamente igual à herdada de `CARD_ID`
no mesmo processo, mas precisamos verificar as instruções
dessas duas rotas. Os `37` e `86` possuem outro valor alto
(`0x0635` → `0x0647`), e não serão rotulados como IDs
sem análise de seus chamadores.

## Implementação adicionada

`yugilab/command_words.py` classifica diferenças de 32 bits
**somente** quando opcode, endereço de retorno e posição
do argumento batem com uma origem demonstrada no Ghidra.

`python -m yugilab.compare` inclui:
- `highWordOrigins`: classes por opcode/callsite;
- `verifiedRegisterResidueCommands` e
  `unverifiedRawDifferenceCommands` em cada duelo;
- `totalUnverifiedRawDifferenceCommands` agregado;
- `--strict-unverified`: rejeita diferenças com origem
  ainda não demonstrada, sem bloquear diferenças já explicadas.
- `--strict-raw`: rejeita QUALQUER diferença nos valores
  originais de 32 bits.

**Nenhum dos três modos normaliza ou remove os argumentos originais**.
Para a engine C#, `ushort` é o tipo correto para o
ID retornado por `FUN_004022E0`. Não modelar um ponteiro
residual como propriedade da carta.

## Próxima etapa (apenas quatro casos restantes)

O novo `scripts/Run-RemainingHighWordRecon.ps1` consulta
`005783BC`, `005ACF72`, `0058CF96` e
`005747E0`, incluindo chamadas anteriores. Os dois arquivos
produzidos são `command-highword-remaining-card-id.txt` e
`command-highword-remaining-raw-callers.txt`.

Execute:

```powershell
.\scripts\Run-RemainingHighWordRecon.ps1
```
