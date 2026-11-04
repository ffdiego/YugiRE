# Duas capturas de comandos: invocação normal versus monstro baixado

Estudo dinâmico fornecido pelo operador em 2026-11-04, usando o
`native_rules` na branch `experiment/hybrid-native-probe`.
O operador identificou o **primeiro** arquivo como invocação normal
(`summon`) e o **segundo** como colocação virada para baixo (`set`).
Ambos os traces vieram do mesmo processo do jogo e produtor
`0x005B9170`; os arquivos completos **não foram adicionados ao Git**.

## Cobertura da captura

| Ação relatada | Comandos | Marcadores | Comandos perdidos pelo buffer |
| --- | ---: | ---: | ---: |
| Summon | 7 | 2 | 0 |
| Set | 3 | 2 | 0 |

As marcações `inicio invocacao`/`fim invocacao` foram registradas pelo
console; não são hooks exatos no momento da escolha do jogador.

Ambas as sequências foram emitidas pela mesma thread do jogo.
A ordem abaixo é a ordem do produtor observado, **não** confirmação do
consumo dos comandos ou das mudanças de memória.

## Sequências (opcode com bit 15 separado)

| Ordem summon | Opcode | Chamador | Primeiro argumento |
| ---: | --- | --- | --- |
| 1 | 0x0D | 0x00579984 | 0x0000800D |
| 2 | 0x0D | 0x00579984 | 0x0000800D |
| 3 | 0x0D | 0x00579984 | 0x0000800D |
| 4 | 0xE6 | 0x005ABD70 | 0x200080E6 |
| 5 | 0xCE | 0x005ABEAB | 0x200080CE |
| 6 | 0x6F | 0x005ABF22 | 0x2000806F |
| 7 | 0x92 | 0x005ABFA4 | 0x20008092 |

| Ordem set | Opcode | Chamador | Primeiro argumento |
| ---: | --- | --- | --- |
| 1 | 0x0D | 0x00579984 | 0x0000800D |
| 2 | 0x0D | 0x00579984 | 0x0000800D |
| 3 | 0xCE | 0x005ABEAB | 0x408080CE |

Os argumentos completos dos três `0x0D` no summon são
`[0x10, 0x11, 0x13]` no segundo DWORD e `[0x11, 0x13]` no set.
Não atribuímos significado definitivo a estes códigos.

## Descodificação do 0xCE com respaldo estático

O produtor `FUN_005ABD00`, já transcrito em
[`normal-summon-tribute-payment.txt`](normal-summon-tribute-payment.txt),
emite no ramo de colocação:

```c
FUN_005B9170((DAT_00A55080 << 15) | 0xCE,
             FUN_004022E0(...));
```

O segundo argumento é o ID de recurso produzido pela conversão
índice interno → ID. O primeiro contém apenas os **17 bits baixos**
do contexto original (os demais são truncados pelo shift em DWORD).
No consumidor `0x004059C0`, documentado em
[`normal-summon-placement-boundaries.txt`](normal-summon-placement-boundaries.txt),
o `0xCE` encaminha a retirada da mão e colocação no campo.

| Campo | Summon (primeiro trace) | Set (segundo trace) |
| --- | --- | --- |
| Primeiro argumento bruto | `0x200080CE` | `0x408080CE` |
| Contexto decodificado (baixo 17 bits) | `0x04001` | `0x08101` |
| Bits do contexto presentes | `0x0001, 0x4000` | `0x0001, 0x0100, 0x8000` |
| Segundo argumento bruto | `0x00D10065` | `0x00D10419` |
| ID do recurso (baixo 16 bits) | `101` | `1049` |
| Nome e índice interno (catálogo) | Mask of Darkness, 1016 | Wall of Illusion, 273 |
| Chamador | `0x005ABEAB` | `0x005ABEAB` |

Os nomes foram conferidos contra o
[`card-callback-catalog.json`](card-callback-catalog.json).
O código `0xE6` aparece no primeiro trace, mas não no segundo:
o produtor `0x005ABD00` emite `0xE6` quando o contexto tem bit
`0x4000`, coerente com os contextos capturados. O primeiro trace
também contém `0x6F` e `0x92` de ramificações posteriores do mesmo
produtor. **Esses extras não são prova de que toda invocação exige
sempre os mesmos comandos**.

## Limites e direção para implementação

- O caminho registrado passa pelos produtores corretos, mas não prova
  quais validações de turno, tributos, efeitos, slots e cartas ocorreram
  antes de `FUN_005ABD00`.
- Neste call site, a transcrição estática mostra **dois argumentos** de
  `FUN_005B9170`. O observador Frida lê quatro posições de pilha por
  padrão, portanto os argumentos de índice 2 e 3 podem ser lixo residual.
  Não foram usados para inferir a semântica de summon/set.
- A parte alta do segundo argumento (`0x00D1` nas duas amostras) também
  não tem significado demonstrado. Usar somente os 16 bits baixos para
  identificar a carta nessa chamada.
- Um comando `0xCE` produzido fora do controle nativo de turno poderia
  retirar a carta incorreta, não atualizar flags, pular custos ou invocar
  cartas proibidas. **Não habilitar `summon`/`set` apenas reproduzindo
  `FUN_005B9170` com os argumentos acima.**

O trace foi ampliado **após estas duas capturas** para registrar os globais
`DAT_00A55080`, `DAT_00A55084` e `DAT_00A5508E` como
`ceNativeState` durante a emissão de `0xCE` por `0x005ABEAB`.
Portanto os arquivos originais deste relatório **não têm** esse campo.

Próximo experimento: observar os campos `DAT_00A55080`,
`DAT_00A55084`, `DAT_00A5508E`, os parâmetros de
`FUN_005AB820`/`FUN_005ABD00` e o estado antes/depois em
`FUN_004059C0`, em dois duelos isolados. Isso poderá identificar
o contrato da ação completa, inclusive controle de invocação por turno,
origem e posição escolhida, e determinar se existe um produtor reutilizável
com baixo risco.
