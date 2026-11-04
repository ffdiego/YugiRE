# Preparação de summon e set — traces e xrefs (2026-11-04)

Esta análise usa duas capturas fornecidas pelo operador:
`commands-20261104-162203-248341-pid8888.jsonl` (summon),
`commands-20261104-162306-855446-pid8888.jsonl` (set)
e `Texto colado(6).txt`, exportação de `SummonDescriptorXrefs.java`.
Os arquivos brutos não são adicionados ao GitHub.

## Comparação dos traces

| Campo | Summon | Set |
| --- | --- | --- |
| eventos retidos/perdidos | 35 / 0 | 19 / 0 |
| thread | 21000 | 21000 |
| elegibilidade `FUN_005AAB90` args | `(1,0x02B700CE)` | `(1,0x02B70105)` |
| retorno da elegibilidade | `0x11` | `0x11` |
| contexto no primeiro `FUN_005AB820` | `0x4081` | `0x8003` |
| origem | 206 | 261 |
| estágio inicial | `0x005` | `0x005` |
| flags lado 1 | `0x000B0000` | `0x000A0000` |
| comando no produtor | `0xCE` (ID 511) | `0xCE` (ID 573) |
| contexto no `0xCE` | `0x4081` | `0x8003` |
| estágio **durante** o `0xCE` | `0x025` | `0x025` |
| mão calculada `(context>>6)&255` | 2 | 0 |
| slot calculado `(context>>1)&31` | 0 | 1 |

O segundo argumento de elegibilidade é uma palavra empacotada
cujos 16 bits baixos coincidem com o índice interno da carta;
os 16 bits altos são `0x02B7` nas duas capturas. O retorno
`0x11` não deve ser interpretado como autorização genérica
sem analisar seus bits e o contexto de turno.

O primeiro `FUN_005AAB90` ocorreu **antes** de o descritor de
operação atual estar pronto. No summon, a captura de elegibilidade
mostra `context=0,source=0,stage=0`; no set, mostra valores
residuais `context=0x4040,source=385,stage=0x124`.
O primeiro dispatcher já observa `stage=0x005` com a carta
selecionada. Assim, o construtor é chamado depois da elegibilidade
e antes do primeiro despacho, mas sua identidade ainda é desconhecida.

## Máquina de estados observada

- Summon: `0x005 → 0x045 → 0x065 → 0x085 → 0x105 → 0x125 → 0x124`.
  Emissões `0x0D`×3, `0xE6`, `0xCE`, `0x6F` e `0x92`.
- Set: `0x005 → 0x045 → 0x145 → 0x144`.
  Emissões `0x0D`×2 e `0xCE`.

O helper `FUN_005ABD00` retorna 0 nas etapas intermediárias
e 1 na última; `FUN_005AB820` retorna 1 enquanto a operação
prossegue e 0 quando remove o bit ativo. Isso é **observação**,
não especificação completa de seus valores de retorno.
O comando `0xCE` é emitido durante a primeira etapa, quando
o estágio já vale `0x025`; no retorno do despachante vale
`0x045`. Não há contradição entre as duas leituras.

## Inventário do Ghidra

262 referências exportadas, com contagens READ e WRITE que
podem se sobrepor em READ_WRITE:

| Endereço | Read | Write |
| --- | ---: | ---: |
| `0x00A55080` (contexto) | 104 | 21 |
| `0x00A55084` (origem) | 36 | 5 |
| `0x00A55088` | 4 | 0 |
| `0x00A5508C` (flags auxiliares) | 6 | 8 |
| `0x00A5508E` (estágio) | 38 | 35 |

Cinco funções escrevem **diretamente** contexto, origem e
estágio — os principais candidatos a construtores:

| Função | Write context | Write source | Write stage | Write +8C |
| --- | ---: | ---: | ---: | ---: |
| `FUN_005AD740` | 3 | 1 | 1 | 1 |
| `FUN_005AD960` | 1 | 1 | 1 | 2 |
| `FUN_005ADA20` | 1 | 1 | 1 | 2 |
| `FUN_005ADB10` | 1 | 1 | 1 | 2 |
| `FUN_005ADBF0` | 2 | 1 | 1 | 1 |

`FUN_0059DF40` escreve nove vezes apenas o contexto
(e não diretamente origem/estágio), o que a torna candidata
mais fraca a inicializador integral. `FUN_005ABD00` altera
estágio, mas não constrói origem/contexto. `FUN_005AD8C0`
possui referência DATA `PUSH 0x00A55080`, podendo passar o
descritor a outra função que o escreva indiretamente.

**Limite:** xrefs estáticos não capturam todas as escritas
indiretas. Nenhuma dessas funções pode ser declarada a
entrada principal sem examinar decompilação, callers e execução.

## Próxima consulta no Ghidra

```powershell
.\scripts\Run-ArchiveQuery.ps1 -Report summon-descriptor-builders.txt -Addresses @(
  '005ad740','005ad960','005ada20','005adb10','005adbf0','005ad8c0'
)
```

Extrai pseudocódigo e callers no arquivo
`docs/evidence/summon-descriptor-builders.txt`.
Compare o contrato e as validações de cada construtor com
a ordem dinâmica dos dois JSONLs, antes de expor
`summon`/`set` ou injetar `0xCE`.
