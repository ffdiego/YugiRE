# YugiRE — origem dos 119 argumentos brutos divergentes

> **Conclusão da terceira rodada (2026-11-08): 118/119 com
> origem identificada.** São 116 resíduos do ponteiro da
> tabela `CARD_ID` e 2 resíduos de escritas parciais em
> registradores (`DX` no opcode `0x61`; `AX` no opcode
> `0x37`). Resta **somente `0x86`**, cuja origem
> anterior ao wrapper `FUN_005747E0` ainda não foi provada.
> Todos os argumentos brutos permanecem preservados.
> O comparador já classifica as diferenças por callsite.

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
| `72 / 005783BC` | args[1] | 1 | 0 | `FUN_004022E0` comprovada no segundo relatório |
| `E6 / 005ACF72` | args[1] | 0 | 1 | `FUN_004022E0` comprovada no segundo relatório |
| `37 / 0058CF96` | args[0] | 1 | 0 | `MOV AX` após `CALL 005B9170` (confirmado) |
| `86 / 00574804` | args[1] | 1 | 0 | Origem ainda desconhecida |

**Total atualizado:** 116 alterações de alta palavra com origem
confirmada em `FUN_004022E0`; mais 2 (`0x61`, `0x37`)
confirmadas como resíduos de escritas parciais em registradores;
somente o comando `0x86` ainda não tem a origem comprovada.
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

A origem de 118 altas palavras está demonstrada no assembly.
Isso **não prova**, isoladamente, que todos os consumidores
do protocolo ignoram essas metades altas; a equivalência
integral do estado interno ainda depende de inspeção adicional.
Os arquivos JSONL preservam os valores completos; nenhuma
transformação destrutiva será aplicada a eles.

Os dois casos `72/005783BC` e `E6/005ACF72`
estão confirmados pelo segundo relatório do Ghidra,
incluindo `CALL FUN_004022E0` e `PUSH EAX`.
O `0x37` foi identificado no terceiro relatório por
desassemblagem transitória; `0x86` mantém origem
não comprovada. Ambos compartilham o alto `0x0635` →
`0x0647`, mas a coincidência não prova que o segundo
tenha a mesma causa.

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

## Segunda consulta que resolveu dois casos, antes do exame final

O novo `scripts/Run-RemainingHighWordRecon.ps1` consulta
`005783BC`, `005ACF72`, `0058CF96` e
`005747E0`, incluindo chamadas anteriores. Os dois arquivos
produzidos são `command-highword-remaining-card-id.txt` e
`command-highword-remaining-raw-callers.txt`.

Execute:

```powershell
.\scripts\Run-RemainingHighWordRecon.ps1
```

## Evidência da segunda consulta (antes do exame final)

Os novos relatórios comprovam:

- `0x72`, retorno `0x005783BC`: `0057839C CALL 004022E0`,
  `005783AC PUSH EAX`, `005783B7 CALL 005B9170`.
  Portanto o `args[1]` contém o `AX` com ID válido e a
  metade alta do ponteiro `CARD_ID` herdada de `EAX`.
- `0xE6`, retorno `0x005ACF72`: `005ACF57 CALL 004022E0`,
  `005ACF6B PUSH EAX`, `005ACF6D CALL 005B9170`.
  A mesma explicação vale para seu `args[1]`.

**Recontagem histórica após o segundo exame:** 116 casos `CARD_ID`,
1 caso `MOV DX` e 2 então desconhecidos. Esses dois casos
aparecem apenas no duelo de índice global 0 e compartilham
uma mesma metade superior em cada execução:

| Comando | Dado que muda | Execução A | Execução B |
|---|---|---|---|
| `0x0037`, retorno `0x0058CF96` | `args[0]` | `0x06350037` | `0x06470037` |
| `0x0086`, retorno `0x00574804` | `args[1]` | `0x06350445` | `0x06470445` |

Em `FUN_005747E0`, o comando `0x86` simplesmente encaminha
`param_2` (32 bits) para o produtor `FUN_005B9170`.
O dado não é mascarado nem construído ali; sua origem
está em um dos numerosos chamadores. O Ghidra ainda não reconhecia instrução em
`0x0058CF90`. Esse bloqueio foi removido pela consulta
transitória de bytes do terceiro exame.

A correlação `0x0635` / `0x0647` é sugestiva, mas não
prova que ambas as diferenças têm o mesmo produtor ou
que a metade superior seja descartável.

A classificação por callsite de `yugilab/command_words.py`
foi ajustada: os dois casos antes `candidate` passaram
para `verified_card_id_pointer_residue`; os outros dois
permanecem `unresolved_high16` e são preservados em
`--strict-unverified`.

### Investigação final somente dos dois desconhecidos

O script `scripts/Run-FinalHighWordRecon.ps1` usa uma nova opção
`bytes:VA:SIZE` em `QueryArchive.java` para ler bytes da região
que o Ghidra deixou sem instruções definidas. A transação
é descartada e o projeto é aberto em `-readOnly`.

```powershell
.\scripts\Run-FinalHighWordRecon.ps1
```

Relatórios previstos: `command-highword-final-37.txt` e
`command-highword-final-86.txt`. A extração ajuda a reconstituir
o produtor do comando `0x37` e os chamadores que fornecem
`param_2` ao wrapper do `0x86`; não pressupõe equivalência.

## Terceiro exame: instruções do `0x37` agora recuperadas

Fonte: arquivo fornecido `command-highword-final-37.txt`, que
reconstituiu o trecho a partir de `0058CF50` sem editar a
base de dados do Ghidra:

```asm
0058CF7B CALL 005B9170   ; comando anterior 0x35
0058CF80 MOV AX,[ESI+2]  ; só os 16 bits baixos de EAX
0058CF86 SHL AX,0xF
0058CF8C OR  AL,0x37
0058CF90 PUSH EAX
0058CF91 CALL 005B9170   ; comando 0x37
```

Os bits 16–31 vêm do `EAX` que restou ao retornar
da emissão do comando `0x35`; o código posterior não
os inicializa. Não é um ID semântico de 32 bits. A
função `005B9170` é tratada como `void` pelo decompilador,
logo não há contrato de retorno de 32 bits a reproduzir
na engine C#. Não foi identificada a origem mais profunda
do valor específico do registrador; isso não impede
de classificar com segurança a preservação como resíduo.

Nova contagem: **116 resíduos `CARD_ID`, 2 escritas parciais
de registradores e 1 caso não resolvido = 119 diferenças**.

## Terceiro exame: `0x86` ainda requer atribuição ao chamador real

O arquivo `command-highword-final-86.txt` confirma que
`FUN_005747E0` simplesmente encaminha seu `param_2`
para `FUN_005B9170`, com opcode `0x86`. Existem muitos
callers possíveis (`FUN_00579790`, `FUN_0059D370`,
`FUN_005B8210` etc.). O baixo `0x0445` foi igual nas duas
capturas, mas o alto diferiu `0x0635` versus `0x0647`.
Não normalizar nem atribuir esse caso ao `CARD_ID`
sem observar o caller efetivo.

**Novo probe somente leitura:** o agente Frida, no callback
já existente de `FUN_005B9170`, registra
`wrapper86Caller` quando o comando é `0x86` e
seu return address é `0x00574804`. No prólogo dessa
função, `FUN_005747E0` empilha quatro parâmetros de
32 bits e chama o produtor. Logo, no início do produtor
x86, `[ESP+20]` é o endereço de retorno ao caller do
wrapper. O acesso está dentro de `try/catch` e não
altera argumentos ou processo.

O comparador mantém `--strict-unverified`: ele deve
continuar falhando para as duas capturas antigas, agora
por **somente um** comando, até o último caso ser explicado.
Na próxima captura determinística com `--seed 12345`,
inspecionar o evento `0x86` do índice global 0 e
seu `wrapper86Caller`, ou `wrapper86CallerReadError`
caso a leitura falhe. O offset x86 precisa ser validado
na execução real; ainda não foi executado no host Windows.
