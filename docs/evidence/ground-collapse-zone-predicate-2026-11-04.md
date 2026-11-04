# Ground Collapse confirmado no predicado de elegibilidade de zona

Data da investigação: 2026-11-04. Fonte: extração Ghidra fornecida
pelo operador, `summon-zone-effect-predicate.txt`. Executável
SHA-256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Identificação inequívoca da carta consultada

`FUN_00569E10(side, slot)` contém a comparação direta
`FUN_004022E0(cardInternalIndex) == 0x528`.

O catálogo interno
[`card-callback-catalog.json`](card-callback-catalog.json)
mapeia **resource ID 0x528 = 1320** para
**Ground Collapse**, índice interno **376** (row 345).
Assim, está **confirmado estaticamente** que o predicado
possui um ramo dedicado ao bloqueio causado por essa carta.

### Primeiro mecanismo: registros de efeitos ligados ao jogador

O Ghidra decompilou este padrão, aqui reescrito para leitura.
Os nomes dos registros e campos são *candidatos*, não símbolos
originais:

```cpp
bool HasGroundCollapseZoneBlock(uint32_t side, uint32_t slot) {
  const uint32_t n = effectCount[side & 1]; // 0x00A563AE
  for (uint32_t i = 0; i < n; ++i) {
    const EffectRecord e = effectRecords[side & 1][i];
    if ((e.type & 0xF) != 2) continue;

    const uint32_t owner = e.sourceEncoded & 1;
    const uint32_t sourceSlot = (e.sourceEncoded >> 8) & 0xFF;
    const uint32_t sourceCard = monsterOrSpellSlots[owner][sourceSlot] & 0xFFF;
    const uint32_t resourceId = FUN_004022E0(sourceCard);

    if (resourceId != 0x528 || slot != e.targetSlot) continue;

    const uint8_t pos = sourcePositionFlags[owner][sourceSlot];
    const uint32_t flags = sourceAuxFlags[owner][sourceSlot];
    const bool active = ((pos & 0x02) != 0) &&
                        ((flags & 0x20) == 0) &&
                        ((flags & 0x02) == 0);
    if (active) return true;
  }
  return false;
}
```

Os endereços relevantes da função decompilada:

- `0x00A563AE + side*0xD44`: contagem `ushort` de registros;
- `0x00A563F0 + side*0xD44`: pares de bytes, tipo no low nibble
  e slot alvo no segundo byte;
- `0x00A563B0 + side*0xD44`: palavra de origem associada a
  cada registro (`sourceSlot` no byte alto, `owner` no bit 0);
- `0x00A55D74 + sourceSlot*0x90 + owner*0xD44`: identidade
  interna da carta de origem (low12);
- `0x00A55D7A`, `0x00A55E00` com deslocamentos de slot/lado:
  bits de estado que determinam se o efeito continua ativo.

**Cuidado:** as três condições de estado são provadas
pela operação de bits, mas não está provado se cada bit
corresponde a face-up, desativado, negado, etc.
A origem pode estar em qualquer posição válida do campo.

O método reconstruído descreve *apenas esse ramo*,
não é implementação executável nem API do jogo.

## Segundo mecanismo: entrada de restrição codificada como '@'

A mesma `FUN_00569E10` percorre **ambos os lados** (0 e 1),
usando a contagem por lado em `0x00A55D6C` e uma tabela de
registros de 2 bytes em `0x00A569E8 + side*0xD44`.

Se existir uma entrada cujo primeiro byte é `0x40` (`'@'`),
com o alvo codificado no segundo byte, a zona é recusada:

```cpp
if (entry[0] == 0x40 &&
    requestedSide == (entry[1] & 1) &&
    requestedSlot == (entry[1] >> 4))
    return 0;
```

O relatório não identifica a origem/gerador dessas entradas,
nem demonstra que elas sejam especificamente de Ground Collapse.
É uma **segunda classe independente** de restrição de zonas.

## Resultado combinado

```text
FUN_0056A030(side)           // escolhe o primeiro candidato permitido
  └─ FUN_00569F70(side,slot)  // rejeita ocupação
       ├─ se campo[side][slot] tem carta -> false
       └─ FUN_00569E10(side,slot)
            ├─ Ground Collapse ativa, alvo correspondente -> 0
            ├─ registro '@' para o mesmo lado/slot          -> 0
            └─ nenhuma restrição detectada                 -> 1

FUN_0056A000(side)           // conta quantos slots passariam
```

Portanto, o experimento com duas zonas indisponíveis
e escolha dos slots 2 e 3 pelo construtor
`FUN_005AD740` é coerente com uma implementação nativa
**explícita** da regra de Ground Collapse. A captura do
primeiro teste não incluiu os bytes dos registros nem o
retorno do predicado para cada slot; o código prova a
regra estática, não cada decisão individual dessa captura.

## Próxima etapa recomendada

A seleção de destino está mapeada. O próximo esforço deve
ser um **preflight passivo** do comando externo:
avaliar, na thread de duelo, lado/carta atual, estágio ocioso,
normal summon ainda não consumida, elegibilidade e disponibilidade
do slot, **sem chamar `FUN_005AD740`**.

Antes de permitir o executor ativo, falta confirmar a origem
da máscara de elegibilidade de `FUN_005AAB90` (retorno
`0x11` nas capturas) e a condição exata que leva o
controlador `FUN_0059DF40` a setar o bit 17 pelo
`FUN_00579930(side,0x11,1)`.
A leitura simples de slot vazio não substitui o predicado.

Se for desejado validar o vínculo dinamicamente, um
probe temporário `Interceptor.attach(0x00569E10)`, em
curto experimento local, pode registrar `side,slot,retval`
enquanto Ground Collapse está ativa, sem alterar valores;
repetir após a carta ser removida seria prova diferencial.
Não é necessário reexecutar a análise estática para isso.
