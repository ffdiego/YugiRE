# IA: produção de eventos de combate, seleção de alvos e elegibilidade

Data: 2026-11-04. Fonte: relatório enviado pelo operador,
`ai-combat-event-producer.txt` (256 linhas), extraído do Ghidra
para o executável SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

Continuação de [score e baseline](ai-simulated-combat-scoring-2026-11-04.md)
e [snapshot/restore](ai-simulation-state-lifecycle-2026-11-04.md).
Somente análise estática; nenhuma execução nova no jogo.

## FUN_005B0D90: duas rotas para produzir um evento de combate

Assinatura inferida:
`uint32 FUN_005B0D90(side, attackerSlot, context, outEvent4Bytes)`.

### Rota A: possível confronto sem alvo de monstro

Se `FUN_0056BF20(side,attackerSlot) != 0` **ou**
`FUN_0056BFD0(side,attackerSlot) != 0`, a função:

1. Exige `FUN_005B05A0(side,attackerSlot,5,context) != 0`.
2. Marca `outEvent.lowByte |= 0x02`.
3. Escreve `attackerSlot` nos bits 4..6 da primeira palavra,
   preservando os bits conforme máscara `0xF38F`.
4. Armazena `FUN_0056F690(side,attackerSlot)` no
   **segundo ushort do evento**, sem alteração além do cast.
5. Retorna `(int16_t)secondWord > 0`.

O destino numérico `5` é **candidato a ataque direto
ou ação sem alvo de monstro**, mas seu significado
depende de `FUN_005B05A0` e dos dois guards.
Não declarar como ataque direto confirmado.

### Rota B: varredura dos alvos adversários

Se a rota A não estiver disponível, itera
`targetSlot=0..4` do lado `1-side`:

- `FUN_0056BD80(side,attackerSlot,opponent,targetSlot)`
  valida o par atacante/alvo.
- `FUN_005B05A0(side,attackerSlot,targetSlot,context)`
  aplica uma segunda condição.
- `FUN_005B0F40(side,attackerSlot,targetSlot,&event)`
  gera **quatro bytes** de descritor e métrica.
- Considera um evento quando `event.lowWord & 0x08`
  está marcado. Se o melhor evento ainda não possui
  `0x08`, copia imediatamente os quatro bytes.
- Com eventos válidos, prefere o que tem
  **maior segundo ushort interpretado como int16**
  (comparação `(short)previousScore < (short)newScore`).
- Empates incluem um teste da posição do novo
  alvo (`field.positionFlags & 0x02`) e dos
  valores `FUN_0056F6B0` (DEF calculada) dos
  dois alvos. Na condição explícita, o novo
  alvo **não substitui** o anterior se
  `newDEF <= oldDEF`; fora desse ramo o novo
  evento pode substituir o anterior.
- Retorna `(outEvent.lowByte & 0x08) >> 3`
  (0 ou 1).

Isso comprova que a rotina **busca e compara alvos
para cada atacante**, com uma preferência baseada
na contribuição de 16 bits e desempate contextual
por defesa. Não comprova que o valor da contribuição
seja exclusivamente dano aos LP.

## Layout parcial do evento de 4 bytes

O descritor retornado por `FUN_005B0F40` é copiado
para `DAT_00A579A6` (`FUN_005B0B90` faz
`FUN_005BDE10(dst,src,4)`).

| Parte | Evidência estática |
| --- | --- |
| low word, bit `0x02` | Rota A marca este bit |
| low word, bit `0x08` | Rota B exige para considerar evento |
| low word, bits 4..6 | Slot atacante |
| low word, bits 7..9 | Slot adversário em algumas rotas |
| low word, bits `0x400` / `0x800` | `FUN_005B0A50` remove identidade temporariamente de um monstro de cada lado |
| high word, bits 16..31 | Valor de confronto gerado ou ATK da rota A |

A segunda palavra de `DAT_00A579A6` fica no endereço
`DAT_00A579A8`. Portanto, ela é o **valor acumulado**
por `FUN_005B0A50` em sua sequência de confrontos.
O código de comparação de targets usa conversão para
`int16`; o tipo/sinal da soma em `FUN_005B0A50`
merece verificação separada.

O formato é **parcial**. Bits e semântica exatos da
saída de `FUN_005B0F40` ainda não foram inspecionados.

## FUN_0056B730: filtro de elegibilidade de monstro

`FUN_0056B730(side,slot,context)` exige:

1. identidade `field[side][slot] & 0xFFF != 0`;
2. `positionFlags & 0x02 != 0`;
3. se `positionFlags & 0x01 != 0`, a carta deve
   ter recurso `FUN_004022E0(internalCard) == 0x31C`;
4. `(alreadyConsideredMask & (1 << slot)) == 0`,
   lendo `0xA55D70 + side*0xD44`;
5. `FUN_0056B7D0(side,slot,context) != 0`.

O recurso `0x31C` ainda não foi associado a
um nome no catálogo **parcial** de callbacks;
não atribuir uma regra de carta específica.
O filtro vale neste contexto e aparece também
em outros chamadores: nomear "pode atacar"
sem qualificar os gates de contexto seria
excessivo.

## FUN_005A0D60: calcula bitmap de ações elegíveis

Recebe `(base, side, unused?, resetMask)`. O
caller identificado passa
`base=&DAT_00A55D64, side, 0, 1`.

- Salva `*(uint16_t *)(base+12+side*0xD44)`
  (mask de controle).
- Zera `base+10+side*0xD44` (bitmap de resultado).
- Se `resetMask != 0`, zera temporariamente
  `base+12+side*0xD44`.
- Testa slots 0..4 com
  `FUN_0056B730(side,slot,1)`. Cada slot
  aprovado marca `1<<slot` em `base+10`.
- Restaura a máscara de controle em `base+12`
  ao final.

Com `base=0xA55D64`, o bitmap de resultado
reside em `0xA55D6E + side*0xD44` e a máscara
de controle em `0xA55D70 + side*0xD44`.
Em `FUN_005A0DD0`, esta preparação é
consultada após o gate de duelo ativo e
`playerFlags & 0x40000 == 0`.

É uma construção **ativa de máscara de elegibilidade**,
não apenas uma leitura, embora restaure a máscara
de controle no fim.

## FUN_005B1310

É um wrapper `void` que chama apenas
`FUN_005B0B90(side)`. Sem critérios
novos demonstrados no corpo.

## Implicações para o modelo da IA

Com as análises anteriores, a cadeia fica:

```text
FUN_005B1360            escolhe carta na mão
  FUN_005B08B0           calcula baseline via simulação
    FUN_005B0A50         soma contribuições de confrontos
      FUN_005B0B90       escolhe possível atacante
        FUN_005B0D90     encontra evento/alvo
          FUN_005B0F40   produz flags + valor de 16 bits

  Para cada invocação hipotética:
    FUN_005B08E0         snapshot campos dos dois jogadores
    FUN_005B1610         limpa tributos e insere carta
    FUN_005B0A50         recalcula contribuições
    FUN_005B0950         restaura os campos
  Compara score e critérios adicionais
```

O jogo avalia uma possível sequência de ataques/trocas
num estado candidato; **não há prova de minimax,
profundidade de múltiplos turnos ou aleatoriedade
na escolha dos ataques**.

## Próxima extração Ghidra — prioridade clara

A função de maior valor é **`FUN_005B0F40`**:
ela monta diretamente os quatro bytes do evento,
inclusive a métrica acumulada. Para interpretar
se o valor é dano potencial, troca de monstros
ou penalidade, capturar também os filtros.

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-combat-outcome-scoring.txt `
  -Addresses @(
    '005b0f40',
    '005b05a0',
    '0056bd80',
    '0056bf20',
    '0056bfd0',
    'ins:005b0d90:85'
  )
```

**Não invocar esses helpers diretamente via Frida**
enquanto dependem de snapshots globais e dos
contextos originais de avaliação.
