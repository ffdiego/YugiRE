# P018 — Magician of Faith e Mask of Darkness: seleção nativa de cemitério (21/11/2026)

**Alvo:** `joey_pc.exe` SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Extração read-only Ghidra com `tempfunc:`; **análise estática**, não replay nativo nem prova de paridade integral. Referência: [issue #28](https://github.com/ffdiego/YugiRE/issues/28) e [FLIP no sandbox](../cards/flip-sandbox.md).

| Carta | Resource ID | Índice DAT de referência | Perfil | Filtro |
| --- | ---: | ---: | --- | --- |
| Mask of Darkness | 101 / `0x65` | 1016 | P018 | Trap, classe DAT 21 / `0x15` |
| Magician of Faith | 427 / `0x1AB` | 471 | P018 | Spell, classe DAT 22 / `0x16` |

Ambas compartilham a tupla de callbacks `[0x00584270,null,0x00582860,null,0x00593530]`. A primeira consulta com `ins:` retornou **próximas instruções definidas**, em endereços diferentes, e não serve para atribuir comportamento a P018. A decompilação correta exigiu `tempfunc:` nas três entradas.

## Funções principais

- `FUN_00584270`, callback primary: exige `(ctx+4 & 4)==0` e `(ctx+4 & 0xE000)==0x4000`, extrai campos do contexto, chama `FUN_00568AD0` e, conforme resultado, `FUN_00575D50`. O significado exato do evento e o contrato do retorno não foram estabelecidos. O decompiler produz uma expressão final suspeita (`uVar4 & 0xFFFF0000`); verificar ABI antes de interpretar booleano.
- `FUN_00582860`, availability: obtém resource ID via `FUN_004022E0(cardIndex)` e consulta `FUN_00599E30(side,resourceId)`; retorna se há candidatos. `FUN_004022E0` busca em `CARD_ID.bin`, com limite `internalIndex < 0x45C`, senão `0xFFFF`.
- `FUN_00593530`, target preparation: usa a máscara `((DAT_00A54E5C>>1)&3)` por lado para escolher caminho automático versus humano. O automático chama `FUN_005AF5A0(side,cardIndex,0)` e, se índice >=0, consulta registro em `0x00A582A4 + index*4`, envia comandos por `FUN_005B9170` e chama `FUN_00592A50`. O humano usa estágios em `DAT_00A55C8E`: estado 0 consulta candidatos e chama `FUN_005BF800(0x5E)`; estado 1 solicita modo `6` via `FUN_0040C940(side,6,resourceId)`; estados posteriores leem escolha via `FUN_0040CAB0` e encaminham comandos. A decompilação de `FUN_0040CAB0` no caller tem parâmetros pouco confiáveis.

## Filtros e buffers de candidatos — confirmado no switch compartilhado

`FUN_00599E30(side,resourceId,...)` contém centenas de ramos por resource ID e prepara o buffer compartilhado. Nos casos P018:

```c
// Esquema sem os demais pós-filtros do helper:
case 0x65:  // Mask of Darkness
  for (occurrence in graveyard[side])
    if (FUN_004025D0(occurrence.cardIndex & 0xFFF) == 0x15)
      append_candidate(occurrence, sourceZone: 4);
  break;
case 0x1AB: // Magician of Faith
  for (occurrence in graveyard[side])
    if (FUN_004025D0(occurrence.cardIndex & 0xFFF) == 0x16)
      append_candidate(occurrence, sourceZone: 4);
  break;
```

- Contagem de GY em `0x00A55D6A + side*0xD44`; registros de ocorrência em `0x00A566F0 + side*0xD44` (registros de 4 bytes).
- Buffer em `0x00A582A4 + candidateIndex*4`, metadados de origem em `0x00A584A4 + candidateIndex*2` com valor **4**; contador nos 16 bits inferiores de `0x00A585A4`, limitado a **0x7F**.
- Há pós-processamento **compartilhado** após o switch, que pode filtrar/compactar a lista via `FUN_005675F0` e `FUN_00570BE0`; a lista final não é necessariamente idêntica à lista bruta do ramo. A condição de ambos os pós-filtros depende de flags/estado a verificar.
- A lista copia **registros físicos de ocorrência**, permitindo duplicatas de mesma identidade; resta provar a correlação índice selecionado ↔ ocorrência retirada do GY e a ordem final.

`CardSelectionRules.FlipGraveyardFilter(427)=Spell`/`(101)=Trap`, `IsEligible` com classes 22/21 e `PendingCardSelection` por ocorrência **coincidem conceitualmente** com a construção observada no executável. Ausência de candidato evita abrir seleção em ambos os lados. Isso não valida controle de dono versus controlador, quantidade de itens no destino ou exceções de cenário.

## CPU: mesmo gerador de candidatos, seletor separado

`FUN_005AF5A0(side,cardIndex,options)`:
1. `FUN_004022E0(cardIndex)` para resource ID.
2. `FUN_00599E30(side,resourceId)` para candidatos; sem candidato retorna `-1`.
3. `FUN_005AF600(side,cardIndex,options)` escolhe um índice; se negativo retorna `-1`.
4. `FUN_0040CAA0(chosenIndex)` registra a escolha, que também é devolvida.

A listagem de `FUN_005AF600` mostra estado em `0x00A585A4` e **despacho por Resource ID**, mas a função ainda não foi decompilada por completo. **Não afirmar** escolha aleatória, por primeira ocorrência ou melhor score antes de seguir seus casos `0x65`/`0x1AB`.

## Próximas etapas de baixo custo

1. Decompilar `0x005AF600` e recuperar exclusivamente os ramos de 101 e 427, incluindo tie-break e comportamento de múltiplas cópias.
2. Separar pós-filtros de `0x00599E30`: quando `FUN_005675F0` ou `FUN_00570BE0` remove candidato? A função pode modificar a lista em-place.
3. Seguir `0x0040C940`, `0x0040CAB0`, `0x0040CAA0`, `0x00592A50` para confirmar UI de escolha, representação de índice e transferência GY→Hand.
4. Validar o gate do FLIP em `0x00584270` e os dois contextos de revelação (Flip Summon e ataque).
5. Teste nativo controlado no Windows quando houver ocasião: cemitério misto, 2 spells/traps elegíveis incluindo duplicata, nenhum candidato, CPU versus humano. O Fedora **não** deve fingir execução nativa Windows.

**Estado:** filtros por classe e gerador de candidatos verificados estaticamente; demais contratos ainda em investigação. `Joey.Core` permanece experimental, **não Verified**.

**Relatórios locais:** `docs/evidence/p018-flip-retrieval-decompiled.txt`, `p018-filter.txt`, `p018-cpu-choice.txt` gerados no Fedora. Os dumps integrais não são versionados neste commit.
