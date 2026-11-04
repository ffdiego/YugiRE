# Sessão live: segundo summon no slot 1, campo e tributo

2026-11-04. Captura fornecida pelo operador:
`commands-20261104-155543-556771-pid6840.jsonl`, mais `state`,
`hand 1` e `watch on 250` copiados no chat. A primeira invocação
do jogador **já havia acontecido antes** de iniciar o trace:
`side 1 monsterIndices=[221,0,0,0,0]`.
Não foi necessário capturar essa primeira invocação para observar a
colocação no segundo slot. O produtor `0x005B9170` registrou **52
comandos em uma única thread (7592)**, sem perda de buffer
(`seen=retained=52`, `overwritten=0`).

## Confirmação direta: lado 1, segunda invocação

No início, a mão do jogador era `[272,1016,261,795]` e o slot
0 já tinha o monstro interno 221. Mais tarde, o watch capturou
a compra de 705 para o final da mão e em seguida:

```text
hand: [272,1016,261,795,705] -> [272,1016,261,795]
side 1 field slot 1: - -> 705
side 1 field slot 1 position: 0x0000 -> 0x0202
```

No mesmo trace, os eventos **46–52** ocorreram juntos, após o início
do turno do jogador:

| Seq | Opcode | raw arg0 | return address | Observação |
| ---: | --- | --- | --- | --- |
| 46–48 | 0x0D (3x) | 0x0000800D | 0x00579984 | argumentos 0x10,0x11,0x13 |
| 49 | 0xE6 | 0x208180E6 | 0x005ABD70 | imediatamente antes de 0xCE |
| **50** | **0xCE** | **0x208180CE** | **0x005ABEAB** | `arg1=0x00BE010C`, ID de recurso **268** |
| 51 | 0x6F | 0x2081806F | 0x005ABF22 | após a emissão do 0xCE |
| 52 | 0x92 | 0x20818092 | 0x005ABFA4 | após 0x6F |

O evento 50 traz:

```json
{
  "ceNativeState": {
    "context": 16643,
    "source": 705,
    "stage": 37
  },
  "ceContextLow17Hex": "0x04103",
  "ceResourceIdLow16": 268,
  "ceHandIndexCandidate": 4,
  "ceFieldSlotCandidate": 1
}
```

`16643 = 0x4103`. As fórmulas previamente hipotéticas
tiveram sua primeira confirmação em par **trace + watch**:

```c
side = context & 1;                  // 1
hand_index = (context >> 6) & 0xFF;  // 4 (posição de 705 na mão)
field_slot = (context >> 1) & 0x1F;  // 1 (slot ocupado)
```

O `arg0` inteiro confere com
`((0x4103 << 15) | 0xCE) mod 2^32 == 0x208180CE`.
O operador não informou o nome da carta 705; o trace confirma
`resource_id=268` e o índice interno 705, sem nome inventado.
`stage=37` também aparece nas duas capturas anteriores que
registraram o campo bruto.

**Limite:** apenas valida essa decodificação para a operação observada;
não prova regras de tributo, turno, posição de defesa, índices de cartas
em condições especiais ou uma API segura de injeção de comandos.

## Lado 0: campo e tributo na mesma sessão

O `watch` observou a CPU comprar uma carta (mão 3→4), depois colocar
o índice **86** no slot físico **10**, removendo-o da mão.
O catálogo de callbacks mapeia `86 → Umi, resource ID 333`.
O evento **9** tem `0xCF` com `arg1=0x00BE014D`,
baixo word **0x014D=333**, compatível com a colocação de Umi.
O evento **11** é `0x72` com o mesmo recurso. Isso estabelece
empiricamente que **o slot físico 10 é usado para Field Spells**,
pelo menos no duel/EXE examinado. Não prova que não possa ser
usado também para outro fim em circunstâncias especiais.

Depois a CPU colocou o índice **616** no slot 0 substituindo
o monstro **241** e o cemitério foi de 0→1. O índice 616 é
**The Legendary Fisherman**, recurso **1326**, no catálogo.
O trace registrou (eventos 17–22):

```text
0xE6 (seq17)
0x57 (seq18)
0x95 (seq19)
0xCE (seq20)
0x6F (seq21)
0x92 (seq22)
```

O comando 0xCE (seq20) tem:
```text
arg0=0x204000CE
arg1=0x00BE052E           // resource ID 1326
ceNativeState.context=0x02004080
ceNativeState.source=616
ceNativeState.stage=37
side=0, hand_index=2, field_slot=0
```

A mão imediatamente antes era `[20,929,616]`; logo, o
`hand_index=2` também corresponde à posição real de 616.
O contexto possui bit `0x02000000`, o mesmo testado por
`FUN_005ABD00` para emitir um comando de tributo `0x57`.
A relação entre `0x57` e a retirada por
`FUN_00577B30` já consta em
[`normal-summon-tribute-payment.txt`](normal-summon-tribute-payment.txt).
Esses elementos são **compatíveis com uma invocação por tributo**
que removeu 241 e colocou 616, mas o trace não captura diretamente
cada passo do consumidor. Não atribuir semântica a `0x95`
sem investigação adicional.

Posteriormente, a carta armadilha do jogador (índice **467**,
**Mirror Force**, ID 1056 no catálogo) saiu do slot 5 e entrou no
cemitério; a CPU perdeu o monstro 616 e seu cemitério chegou a 2.
Há `0x72` (seq30) com low word `0x0420=1056` e
`0x79` (seq31). Consistente com o efeito de Mirror Force,
mas **não prova causalidade isolada**.

## Conclusões e próximos passos

- **Confirmado neste teste:** `(context >> 1) & 0x1F` previu slot 1
  para a colocação da carta 705, de acordo com o watch, enquanto
  `(context >> 6) & 0xFF` previu sua posição 4 na mão.
- **Segunda corroboração independente:** o contexto do lado CPU
  predisse mão índice 2 / campo slot 0 e coincide com a evolução
  observada.
- **Field Zone:** slot físico 10 ocupado por Umi (índice 86),
  com `0xCF` da mesma carta registrado.
- O código pode documentar as decodificações, mas ainda não deve
  emitir `0xCE` diretamente como comando `summon` público.
  Falta determinar o produtor completo do descritor, validações
  de turno e o contrato de tributos/set.
