# Multi-magia r01 — reconstrução de estado em Forest, Mystic Box e Burning Land

**Origem:** `state-report(2).json` / `state-summary(2).md` compartilhados pelo operador em 17/11/2026. A captura corresponde à mesma das janelas CF já auditadas: `duels-20261117-035345-497597-pid8400.jsonl`, SHA256 **reportado** `403ee790bea2661456ec721d722a3dc9d451d37ae324ef646b99d9ce73d04a52`. Os bytes do JSONL original não foram examinados nesta etapa; a consistência aqui é com os relatórios derivados. A análise das janelas está em `docs/evidence/native-multi-spell-cf-windows-first-round-2026-11-17.md`.

## Integridade e medidas

- Um duelo; `integrityOk=true`; 516 amostras, 1 baseline, 148 deltas, 0 erros de leitura e 18 command entries sem amostra.
- 247 transições individuais: 105 `fieldSlot`, 39 `handCount`, 39 `handIndices`, 28 `lifePoints`, 18 `deckCount`, 18 `graveyardCount`.
- Skull Invitation índice414 ausente em todas as **516** amostras válidas; `presentSamples=0`.
- 18 incrementos unitários de Cemitério; cinco perdas de exatamente 300 LP no duelo, **zero** pareamentos de incremento seguido de perda −300 no mesmo lado nos seis próximos seq. Essas perdas não podem ser atribuídas automaticamente a Skull.
- Os snapshots são observações **não atômicas na entrada** do produtor, não o estado após o comando do mesmo seq. Não sabemos a identidade exata de cada carta inserida no Cemitério, só a contagem.

## Raimei (índice1049/recurso1007): magia de dano direto em dois jogadores

| Seq | Observação |
|---:|---|
| 62 | CF Raimei |
| 63 | Lado0: mão 6→5, índice1049 removido da mão, aparece slot5 ocorrência1, `positionWord=2`, flags2 |
| 65–66 | `0x43` 300, lado1 LP 8000→7700; flags da magia 2→6 |
| 67 | flags6→4 |
| 70 | Índice1049 some do slot5 lado0 e contador Cemitério lado0 0→1 |
| 543 | Segundo CF de Raimei |
| 544 | Índice1049 sai da mão do lado1 e entra slot6 ocorrência26 |
| 546–547 | `0x43` 300 e LP lado0 3350→3050; flags2→6 |
| 548–550 | flags6→4; índice1049 sai slot6 lado1 com contador Cemitério lado1 8→9 |

Forte suporte a sequência genérica `HandToSpellSlot → EffectCommand → LPDelta → SpellSlotExit + GraveCountIncrement`. A saída do slot e a contagem não identificam sozinhas o destino real, mas o padrão se repetiu em dois lados.

## Rain of Mercy (índice969/recurso1315): recuperação bilateral

| Seq | Observação |
|---:|---|
| 364 | Lado0 compra índice969, mão +1 e deck -1 |
| 368 | CF Rain |
| 369 | Índice969 sai da mão do lado0 e entra slot5 ocorrência18, `flagsWord=2` |
| 371–372 | Dois comandos `0x42` de 1000, um para cada lado |
| 373 | Lado0 LP 5350→6350, lado1 5500→6500; flags da Rain 2→6 |
| 374 | flags6→4 |
| 376 | Rain sai do slot5 lado0; contador do Cemitério lado0 5→6 |

Há queda −300 no lado1 seq386, mas não é efeito da Rain demonstrado: o monstro índice1033 sai do campo lado1 na seq385 e o Cemitério do lado1 aumenta na seq387. Skull está ausente em todas as amostras; são eventos posteriores distintos.

## Burning Land (índice987/recurso1311): candidato de dano contínuo

- Seq191: índice987 é comprado pelo lado1. Seq212: CF da carta. Seq213: índice987 sai da mão do lado1 e entra no slot físico5, ocorrência10, `positionWord=2`, flags2. Seq214: flags2→0. **Não houve observação de saída do campo** até o fim da captura.
- Seq223: `0x43` com valor decimal500; seq224 lado0 LP 7400→6900.
- Contando todas as transições de LP: existem **12 quedas de exatamente 500 LP depois da entrada**, 6 de cada lado, alternando rigorosamente: lado0 nas seq224, 290, 367, 455, 508, 574; lado1 nas seq261, 322, 404, 465, 542, 584. **Não há uma queda exatamente de 500 LP antes da seq213.**
- Esse padrão é **fortemente consistente** com dano periódico afetando ambos os jogadores enquanto a carta persiste. Ainda não está demonstrado que os 12 danos foram causados pela carta; a periodicidade por *fase do turno* não foi testada e a observação de presença não é contínua/atômica.
- Um bom próximo teste é associar os comandos `0x43 500` à mudança nativa de fase/turno e executar controle sem Burning Land.

## Forest (índice990/recurso329): slot de campo e associações

- Seq405: CF Forest. Seq406: lado1 remove índice990 da mão e o coloca no **slot físico10**, ocorrência20, `positionWord=2`, flags2. Seq411: flags do slot10 2→0; nenhuma saída posterior é observada.
- **Seq411:** simultaneamente ao flag da carta, dois monstros em lados distintos ganham exatamente **uma mesma associação**, sem mudança de índice de carta:
  - lado0 slot1, monstro índice24 ocorrência5: `associationCount 0→1`, `referenceWord=2561 (0x0A01)`, `metadataWord=8`.
  - lado1 slot0, monstro índice887 (occurrenceWord observado 0): **mesmos valores `referenceWord=2561` e `metadataWord=8`**.
- `0x0A01` é **compatível** com possível codificação de referência ao slot físico10 do lado1, mas o formato da referência não foi comprovado. A associação de ambos os lados é evidência de **referência compartilhada**, não de uma alteração quantificada de ATK/DEF; o sampler não mede atributos efetivos.
- Monstro24 sai na seq426 e sua associação é removida; índice887 ainda tem essa associação na última transição observada, seq608.
- Forest e Burning Land coexistiram no campo nas amostras após seq406, sem evidência de remoção de Forest.

## Mystic Box (índice370/recurso1213): troca de controle observada

| Seq | Transição |
|---:|---|
| 419 | Lado1 coloca monstro índice1038 no slot1, ocorrência21, flags131074 |
| 421 | CF Mystic Box; monstro1038 do lado1 altera flags 131074→196608 |
| 422 | Mystic Box índice370 sai da mão lado1 e entra slot6, ocorrência22, flags2 |
| 423–427 | Na janela CF aparecem `0x08` duas vezes, `0x79`, `0x86`, `0x83`. Seus significados isolados ainda não estão demonstrados |
| 426 | Monstro índice24 **sai do slot1 lado0**; contador do Cemitério lado0 6→7 |
| 428 | Monstro índice1038, **a mesma ocorrência21**, sai do **lado1 slot1** e aparece no **lado0 slot1** simultaneamente. No destino traz `flagsWord=65536` e `associationCount=1` com **`referenceWord=370`, `metadataWord=13`**. A associação com o índice da própria Mystic Box é uma pista forte |
| 429–431 | Mystic Box flags6→4, depois sai do slot6 lado1; contador do Cemitério lado1 5→6 |
| 440 | Monstro1038 sai do **lado0 slot1**, removendo a associação `(370,13)` |
| 441–442 | Lado0 perde 300 LP na seq441; contador do Cemitério **lado1** 6→7 na seq442 |

A seq428 constitui evidência explícita de **mudança do campo controlador** preservando `(cardIndex=1038, occurrenceWord=21)`. A mesma jogada também remove monstro24 do lado0. É fortemente compatível com as operações usuais da Mystic Box; não prova todas as pré-condições nem que cada opcode seja uma primitiva única. O contador de Cemitério lado1 aumentar após o monstro1038 sair do lado0 é consistente com a distinção entre **proprietário original e controlador atual**, mas não identifica diretamente a carta remetida ao Cemitério.

## Consequências de arquitetura / próxima prioridade

Três mecanismos genéricos emergem de um único duelo e não devem ser implementados como scripts isolados de carta:

1. `TransferControl` ou `MoveMonsterBetweenSides`: chave de identidade `(cardIndex, occurrenceWord)`, origem/destino físico distintos e associação de origem `(370,13)` no destino.
2. `ContinuousFieldAssociations`: uma carta de campo no slot10 passa a ser referenciada por monstros de ambos os lados; estudar o encoding de `referenceWord=0x0A01` e o significado de `metadataWord=8` antes de modelar o buff de Forest.
3. `PeriodicLpDeltaCandidate`: sequência recorrente de −500 lado0/lado1 após Burning Land ficar no campo; investigar fase do turno e emitir controle negativo para isolar causalidade.

Manter as hipóteses separadas da paridade completa em C#. Como o relatório fornece apenas diferenças, não usar um campo que não sofreu delta como prova de ausência universal de efeito. Para validar ATK/DEF, adicionar **captura de atributos efetivos** junto ao estado do monstro em momento confiável no ciclo do jogo, com testes de integridade e sem provocar efeitos.

**Limites:** não atribuir a Rain o dano −300 da seq386; não atribuir a Forest o −500 ocorrido **antes do CF** na seq404; não contar janelas CF sobrepostas como ações duplicadas; e não atribuir automaticamente a Burning Land toda perda -500 só pela coincidência temporal. As observações usam um duelo e não constituem paridade de regras/IA completa.
