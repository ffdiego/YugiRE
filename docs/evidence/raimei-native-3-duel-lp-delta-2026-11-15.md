# Raimei — nove sequências nativas de dano direto com LP observado (15/11/2026)

**Fonte:** captura Windows de três duelos nativos, recebida do operador. Dados brutos **não versionados**.
**SHA256 do JSONL:** `a09b8d487183f1d798f070576263cae87dd842872b8868dd7a4bf32d922ffb81`.
**Escopo:** somente o comando associado ao resource ID `1007` (Raimei) e a mudança de LP observada; **não** valida a carta inteira, legalidade, cadeia, IA ou equivalência com o C#.

## Integridade

- `capture_summary.complete=true`, três comandos terminais em três duelos; `eventsWritten=99429`, `overwritten=0`, sequência contínua.
- Decks com 38 monstros normais e duas cópias de Raimei (índice interno 1049) por lado; aplicação/verificação dos dois decks reportada nas três partidas.
- Limpeza do PID original confirmada pelo YugiLab no log do operador.
- `ai_probe` corresponde a 95.017 eventos; `command`, 4.000. Para pesquisas apenas de P045, desabilitar `--trace-ai` reduz o volume sem precisar modificar os hooks de comando.

## Nove sequências com a assinatura de Raimei

Todas apresentam `0xCF` emitido por caller `0x57977f`, com `args[1] & 0xffff = 1007`.
Exatamente três números de sequência depois há um comando `0x0043` ou `0x8043` emitido pelo caller `0x578513`, com valor `300`.
A **próxima observação de LP** (seq +1) mostra queda de 300 exatamente no slot de destino e zero no outro. Todos os eventos correlacionados ocorrem na mesma thread.

| Duelo | Seq CF | Seq dano | Seq LP pós-dano | LP antes [slot0, slot1] | LP depois [slot0, slot1] | Slot que perdeu LP |
|---:|---:|---:|---:|---|---|---:|
| 1 | 65 | 68 | 69 | [8000, 8000] | [8000, 7700] | 1 |
| 1 | 19348 | 19351 | 19352 | [4000, 6800] | [4000, 6500] | 1 |
| 1 | 30598 | 30601 | 30602 | [2400, 5700] | [2100, 5700] | 0 |
| 1 | 46548 | 46551 | 46552 | [1800, 3900] | [1500, 3900] | 0 |
| 2 | 58028 | 58031 | 58032 | [7800, 5400] | [7800, 5100] | 1 |
| 3 | 66714 | 66717 | 66718 | [6200, 4900] | [5900, 4900] | 0 |
| 3 | 71160 | 71163 | 71164 | [3700, 4250] | [3700, 3950] | 1 |
| 3 | 79264 | 79267 | 79268 | [1500, 3000] | [1500, 2700] | 1 |
| 3 | 85479 | 85482 | 85483 | [1100, 1300] | [800, 1300] | 0 |

**Resultado:** 4/1/4 ocorrências por duelo; quatro quedas de LP no slot 0 e cinco no slot 1; **9/9 quedas exatas de 300**. Nas nove ocorrências, o bit 15 do comando de origem e o do destinatário são opostos, coerente com o preparo usual do oponente.

## Distinção entre observação e reconstrução

O laboratório C# reportou seis ações `FixedDamage` da Raimei em dez duelos: é uma verificação da política do **protótipo**, não paridade de frequência com nove ocorrências em três duelos do original. Seeds iguais **não** tornam IA, shuffle ou decisões equivalentes entre os motores.

A assinatura de comando e LP do original sustenta uma **projeção observada de 300 LP** nos casos acima; não demonstra todas as condições de ativação, de pagamento/cemitério, supressões, efeitos concorrentes, nem comportamento para valores de LP que cruzem zero. Esses aspectos permanecem abertos. Não promover automaticamente a cobertura integral no registry.

O novo `yugilab.effect_report` organiza essas sequências em `linkedLpCases` e mantém `exactLpDelta` distinto de um simples comando candidato, com testes sintéticos de falsos positivos. A execução dessa nova revisão do auditor ainda precisa de validação Windows.
