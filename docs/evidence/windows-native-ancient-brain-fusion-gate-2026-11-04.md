# Native Trace: Ancient Brain, Fusion Gate e Wall of Illusion

Data: 2026-11-04. O operador identificou duas novas sessões:
1. Invocar Ancient Brain normalmente, depois colocar a magia de campo
   Fusion Gate.
2. Baixar Wall of Illusion virado para baixo.

Capturas do produtor `0x005B9170`, mesmo processo `pid12624`.
Foram analisados 13 e 3 comandos, ambos com `overwritten=0`,
`threadId=2204` e sem marcadores textuais. As amostras brutas
não foram incorporadas ao repositório. **O relato dá o contexto**, mas
não temos transição RAM sincronizada nem o momento exato da ação.

## Sessão 1: summons e jogada de campo

| Seq | Offset relativo ao primeiro evento | Opcode | Return address | arg0 | arg1 |
| ---: | ---: | --- | --- | --- | --- |
| 1 | 0ms | 0x0D | 0x579984 | 0x0000800D | 0x00000010 |
| 2 | 0ms | 0x0D | 0x579984 | 0x0000800D | 0x00000011 |
| 3 | 0ms | 0x0D | 0x579984 | 0x0000800D | 0x00000013 |
| 4 | 50ms | 0xE6 | 0x5ABD70 | 0x208080E6 | 0x00D900AE |
| 5 | 50ms | 0xCE | 0x5ABEAB | 0x208080CE | 0x00D900AE |
| 6 | 283ms | 0x6F | 0x5ABF22 | 0x2080806F | 0x00D900AE |
| 7 | 883ms | 0x92 | 0x5ABFA4 | 0x20808092 | 0x00000000 |
| 8 | 3850ms | 0xCF | 0x57977F | 0x000080CF | 0x00D9060B |
| 9 | 4649ms | 0x72 | 0x5783F2 | 0x00008072 | 0x00D9060B |
| 10 | 5133ms | 0x86 | 0x574804 | 0x00008086 | 0x00000A01 |
| 11 | 5133ms | 0x86 | 0x574804 | 0x00008086 | 0x00000A01 |
| 12 | 5133ms | 0x11 | 0x584BD0 | 0x00008011 | 0x0000000E |
| 13 | 6534ms | 0x0E | 0x5799EF | 0x0000800E | 0x0000000A |

Primeiros sete comandos são a mesma família observada na invocação
anterior (0x0D×3, 0xE6, 0xCE, 0x6F, 0x92).
O intervalo entre 0x92 e 0xCF foi de ~2967ms, coerente com a
segunda ação relatada. O `arg1 & 0xFFFF` de 0xCF e 0x72 vale
`0x060B = 1547`, que o catálogo
[`card-callback-catalog.json`](card-callback-catalog.json)
identifica como **Fusion Gate**, índice interno 795.
Portanto 0xCF e 0x72 integram **a sequência observada**
da ação com Fusion Gate. A semântica geral de cada opcode e sua
ordem de consumo **não estão comprovadas**.
Os dois 0x86 seguem o helper genérico `FUN_005747E0` descrito em
[`association-command-producers.txt`](association-command-producers.txt);
não presumir relação exclusiva com magias de campo.

## Sessão 2: monstro baixado

| Seq | Opcode | Return address | arg0 | arg1 |
| ---: | --- | --- | --- | --- |
| 1 | 0x0D | 0x579984 | 0x0000800D | 0x00000011 |
| 2 | 0x0D | 0x579984 | 0x0000800D | 0x00000013 |
| 3 | 0xCE | 0x5ABEAB | 0x402080CE | 0x00D90419 |

`arg1 & 0xFFFF = 0x0419 = 1049`, Wall of Illusion, índice 273
no catálogo. Os dois `0x0D` coincidem com a sequência de set anterior
(sem 0xE6, 0x6F e 0x92).

## Quatro observações do comando 0xCE (duas sessões anteriores + duas novas)

| Ação fornecida | ID de recurso | Context (low17) | Native context | Native source | Mão candidata | Zona candidata | Flags |
| --- | ---: | --- | --- | ---: | ---: | ---: | --- |
| Summon Mask of Darkness | 101 | 0x4001 | não capturado | não capturado | 0 | 0 | 0x4000 |
| Set Wall of Illusion (anterior) | 1049 | 0x8101 | não capturado | não capturado | 4 | 0 | 0x8000 |
| Summon Ancient Brain | 174 | 0x4101 | 0x4101 | 907 | 4 | 0 | 0x4000 |
| Set Wall of Illusion (atual) | 1049 | 0x8041 | 0x8041 | 273 | 1 | 0 | 0x8000 |

Fórmulas candidatas com respaldo nos consumidores/decompilação:

```c
source_hand_index_candidate = (context >> 6) & 0xFF;
destination_slot_candidate  = (context >> 1) & 0x1F;
summon_related_flag = (context & 0x4000) != 0;
set_related_flag    = (context & 0x8000) != 0;
```

`DAT_00A55080` guarda a palavra de contexto;
`DAT_00A55084` guardou `907` (Ancient Brain) e `273`
(Wall of Illusion) nos respectivos eventos. Em ambos,
`DAT_00A5508E=37 (0x25)`, cujo subestado
`(stage & 0xFE0) >> 5` resulta em **1**, justamente o ramo que
emite `0xCE` na função `FUN_005ABD00`.

A origem `source` e os bytes baixos do ID de recurso são congruentes
com a conversão índice interno → ID no produtor.
O contexto `0x4101` difere do anterior `0x4001` por `0x0100`,
ou 4 posições na mão segundo a fórmula; para os dois sets,
`0x8101` e `0x8041` indicam posições candidatas 4 e 1.
**Não há snapshot simultâneo da mão** nos arquivos para confirmar
o índice de seleção. Também não podemos considerar esses quatro casos
uma ABI geral para todas as invocações, tributos ou efeitos especiais.

O segundo argumento `arg1` compartilha byte alto `0xD9` nesta
sessão versus `0xD1` na sessão anterior. O significado desse
byte **não está determinado**. Em `0xCE`, o produtor foi observado
passando somente dois argumentos: `args[2]`/`args[3]`
podem conter valores residuais da pilha.

## Validação posterior: posição de mão, slot 1 e Field Zone

Uma terceira sessão (52 comandos + watch/state) confirmou
`(context >> 1) & 0x1F == 1` para a segunda invocação do jogador
no slot 1 e `(context >> 6) & 0xFF == 4` para o item 705
no final da mão. Também mostrou Umi no slot físico 10 e
uma invocação com tributo pela CPU. Ver
[`windows-native-second-monster-slot-and-field-2026-11-04.md`](windows-native-second-monster-slot-and-field-2026-11-04.md).

## Próximo experimento

- Isolar somente Fusion Gate: `state` antes, `trace on`,
  `watch on 250`, ativação/colocação, `trace off`,
  `trace dump`, `watch off`, `diff`. Confirmar qual slot
  físico fica ocupado (slot 10 ainda não foi confirmado como Field Zone).
- Para validar codificação de mão, realizar summon/set de cartas
  em **posições conhecidas e diferentes na mão**, com `hand 1`
  e `state` antes, `trace on` durante e `diff` depois.
- Localizar o produtor de 0xCF e o construtor completo de
  `DAT_00A55080`, investigando gates de turno e de zona antes de
  expor `summon`/`set` como comandos ativos.

**Não reproduzir `0xCE`/`0xCF` diretamente no console** só com
esta evidência: são comandos do motor, não uma API de validação.
