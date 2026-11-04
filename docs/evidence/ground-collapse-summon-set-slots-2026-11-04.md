# Ground Collapse: slot automático no construtor de summon/set

Data: 2026-11-04. EXE Joey conhecido, SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

Capturas fornecidas pelo operador:
- `commands-20261104-174230-566865-pid22488.jsonl` — summon;
- `commands-20261104-174321-916900-pid22488.jsonl` — set.

O operador informou que **Ground Collapse** estava aplicada,
tornando duas zonas de monstro indisponíveis. Os JSONLs contêm
49 e 21 eventos respectivamente, nenhum sobrescrito. Não temos
um snapshot separado das cinco zonas com seus bloqueios exatos;
o relato da carta fornece o contexto do experimento.

## Observações do construtor

Ambas as jogadas invocaram o **mesmo callsite de usuário**
`0x0059E208` (a instrução de chamada está em `0x0059E203`).
Isto confirma em execução real o contrato do Ghidra para
`FUN_005AD740`.

| Campo | Summon | Set |
| --- | --- | --- |
| Carta por `source` | Ancient Brain, índice 907 | Wall of Illusion, índice 273 |
| Args brutos do construtor | `[1,5,2,0,1]` | `[1,4,3,0,0]` |
| Modo | summon (1) | set (0) |
| Side | 1 | 1 |
| Índice na mão | 5 | 4 |
| **Slot decidido antes de entrar na função** | **2** | **3** |
| `context` antes | `0x4080` (operação anterior inativa) | `0x4084` (operação anterior inativa) |
| `context` depois | `0x4145` | `0x8107` |
| `source` depois | 907 | 273 |
| `stage` depois | `0x005` | `0x005` |
| `0xCE` raw arg0 | `0x20A280CE` | `0x408380CE` |
| `0xCE` resource low16 | 174 | 1049 |
| `0xCE` context/slot | `0x4145` / 2 | `0x8107` / 3 |
| Flags lado jogador no despacho | `0x000B0000` | `0x000A0000` |

O `normal_builder` chama `FUN_005AD8C0` e inicia o estado
`0x005`. A etapa de `0xCE` continua sendo emitida por
`FUN_005ABD00` em ticks posteriores; isso não requer
reprodução externa dos comandos individuais.

### A decodificação agora está validada em posições 2 e 3

```c
side = context & 1;
hand = (context >> 6) & 0xFF;
slot = (context >> 1) & 0x1F;
```

- `0x4145`: side 1; hand 5; slot 2; flag `0x4000`.
- `0x8107`: side 1; hand 4; slot 3; flag `0x8000`.

A coincidência do slot no argumento do construtor e no evento
`0xCE` elimina a hipótese de que a escolha do destino
ocorra apenas no consumidor da fila.

### Elegibilidade não equivale a autorização definitiva

A captura de summon registrou **sete** chamadas a
`FUN_005AAB90` antes da invocação:

| Índice interno examinado (low16 arg1) | Retorno bruto |
| ---: | --- |
| 206 | `0x11` (duas chamadas) |
| 273 | `0x11` (duas chamadas) |
| 582 | `0x00` (duas chamadas) |
| 907 | `0x11` |

A captura de set registrou uma chamada para a carta 273
com retorno `0x11`. Os bytes superiores do argumento
empacotado foram `0x02A8` no primeiro trace e `0x02A9`
no segundo. Os dados não permitem mapear os bits de retorno
nem garantir que `0x00` represente proibição global;
podem ser sondagens prévias da UI.

### Máquina de estados depois da construção

- Summon: `0x005→0x045→0x065→0x085→0x105→0x125→0x124`,
  com `0x0D`×3, `0xE6`, `0xCE`, `0x6F`, `0x92`.
- Set: `0x005→0x045→0x145→0x144`,
  com `0x0D`×2 e `0xCE`.

Para a função `FUN_005AD740`, o campo `returnRaw`
é irrelevante: seu contrato decompilado retorna `void`
e os valores capturados representam estado residual de registrador.

## Implicação para o executor

**Não selecionar manualmente o primeiro slot com índice vazio.**
Uma zona pode estar vazia, porém indisponível por efeito contínuo
como Ground Collapse. O construtor recebe o slot já selecionado
e apenas empacota os bits; portanto a validade da zona precisa
ser estabelecida **a montante**.

O callsite de jogador `0x0059E203` consulta
`FUN_0056A030` imediatamente antes de chamar o construtor.
É o próximo candidato prioritário para estudo de seleção de slot;
não podemos afirmar ainda que ele sozinho implementa todas
as restrições por efeitos. Seus três valores empilhados no
callsite exigem decompilação para validar a convenção/chamada.

## Resultado da consulta: escolha de slot confirmada

O relatório `summon-slot-selector.txt` demonstrou que
`FUN_0056A030(side)` percorre os cinco slots e chama
`FUN_00569F70(side,slot)` como predicado de elegibilidade
do destino; retorna `0xFFFFFFFF` se nenhum servir.
O ramo inicial também considera três registros especiais
de seleção (`0x00A57840/64/F4`).
Foi esclarecido também que o callsite `0x0059E1E0`
passa **um** argumento ao seletor, não três: dois pushes
anteriores ficam na pilha para o construtor. Ver
[`summon-slot-selector-2026-11-04.md`](summon-slot-selector-2026-11-04.md).

## Consulta de legalidade concluída

`FUN_00569F70(side,slot)` contém duas condições: a
zona deve estar vazia (low12 zero) e
`FUN_00569E10(side,slot)` deve aceitá-la.
`FUN_0056A000(side)` conta zonas aceitas por esse
mesmo predicado. O efeito de Ground Collapse ainda
não foi localizado diretamente, mas esse segundo
predicado é a próxima hipótese verificável. Ver
[`summon-slot-legality-2026-11-04.md`](summon-slot-legality-2026-11-04.md).

## Consulta Ghidra anterior (concluída)

```powershell
.\scripts\Run-ArchiveQuery.ps1 -Report summon-slot-selector.txt -Addresses @(
  '0056a030',
  '00579930',
  'ins:0059e1c8:55',
  'ins:0059e180:100'
)
```

Precisamos entender como `FUN_0056A030` escolhe uma zona
e se verifica explicitamente bloqueios por Ground Collapse;
também esclarecer o efeito de `FUN_00579930` no estado
posterior e as pré-condições do ramo `0x0059E203`.
Não expor `summon`/`set` nativos sem essas verificações.
