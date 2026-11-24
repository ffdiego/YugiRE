# Reteste Windows após isolamento dos pontos internos — combat-context v1

Data: 2026-11-12. Branch: `feat/native-combat-context-v2`.
Esta é uma **auditoria estrutural** da captura real fornecida pelo operador.
Não há execução de Joey.Core, nem contexto v2 completo, nem prova de paridade.
A captura JSONL original é privada e **não está versionada**.

## Identidade e integridade

- Arquivo original: `commands-20261112-115000-128612-pid15040.jsonl`.
- SHA256 do JSONL original: `fc9daddea0116855ec2bc6b63a1966f645c294440fa76b1f2d2ec4b41fb1f77c`.
- Metadata: `session=1`, `producer=0x5b9170`, `capacity=20000`,
  `seen=311`, `retained=311`, `overwritten=0`, `processId=15040`.
- `modulePath=Z:\\YugiRE\\passion\\joey_pc.exe`; EXE SHA256 atestado na
  metadata: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- 312 linhas JSONL: uma de metadata e **311 eventos** com `seq` contínuo
  de 1 a 311, sem duplicações, lacunas ou sobrescrita.
- Distribuição: 235 `command`, 58 `combat_context`, 18 `ai_probe`.
- Nove seletores pareados: quatro `select_normal` e cinco
  `select_priority`, cada um com enter/leave.
- Um comando terminal `opcodeMasked=0x0004` em seq 311,
  com `terminalNativeState.lifePoints=[0,9000]`. Esse valor é
  observação de LP, **não** autoriza inferir vencedores apenas do opcode.
- O operador relatou um duelo inteiro concluído normalmente, sem crash.
  A inspeção estrutural não mede alteração de timing ou ausência de todos os
  efeitos colaterais da instrumentação.

## Avaliações F40

Seis pares `enter/leave` da F40, todos no thread 8308 e associados a
seletores realmente observados. Cada um tem um `own_stats` e um
`opponent_stats`, buffers de saída de quatro bytes e contagem de filhos
coerente com os eventos correlacionados. Nenhum `readError`,
`correlationError`, `readErrors` de snapshot ou `childOverflow`
não vazio foi encontrado.

| combatCallId | Seq enter..leave | selectorCallId | Índice atacante | Índice rival | Rival no snapshot | Filhos | OutputHex |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 2 | 30..38 | 1 | 409 | 954 | oculto | 7 | `0000E0FC` |
| 10 | 39..47 | 1 | 920 | 954 | oculto | 7 | `00009CFF` |
| 21 | 104..112 | 20 | 409 | 823 | revelado | 7 | `800494F8` |
| 29 | 113..121 | 20 | 920 | 823 | revelado | 7 | `800450FB` |
| 37 | 122..134 | 20 | 384 | 954 | oculto | 11 | `08080000` |
| 49 | 135..143 | 20 | 384 | 823 | revelado | 7 | `80040CFE` |

Total de 46 eventos filhos: 6 `own_stats`, 6 `opponent_stats`,
19 consultas de `resource`, 6 `equipment_count`, 5 `field_query`
e 4 `context_count`. O resource efetivo `FFFF` do rival oculto
não é o recurso verdadeiro da carta oculta.

## Conclusão e limites

O arquivo confirma observação em modo `combat_context.schemaVersion=1`,
coerente com a mitigação que deixa de instalar os nove pontos internos
suspeitos. A capture registrou seis avaliações de combate pareadas,
três com rival revelado, sem falha estrutural ou perda de eventos.

**Isso não valida os hooks v2 nem resolve o crash `0xc0000005`** de PID 23328.
As lacunas do contrato v2 permanecem, inclusive postura e bit de proteção
consumidos, descritor inicial consumido e manifesto independente de
modificadores/atributos. O número de contextos estritamente completos
para `Joey.Core` continua **zero**; Match/Mismatch não foram avaliados.

O arquivo foi auditado com um script Python pontual de leitura e invariantes
de correlação, não com uma execução de Joey.Lab/Joey.Core. Não declarar que a
suíte .NET, as regras, ou os hooks internos foram validados por este reteste.

Recomendação: manter o modo combinado bloqueado no Windows e revisar
estaticamente o ponto `own_visibility_consumed` (`0x005ADD8D`),
a relocação do trampoline e a preservação de ESI antes de autorizar outro
piloto instrumentado. Comparar com
[relatório de crash](../research/handoffs/native-combat-context-v2-crash.md).
