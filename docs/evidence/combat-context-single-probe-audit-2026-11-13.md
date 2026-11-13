# Auditoria do piloto Windows — probe único `own_visibility_consumed` (2026-11-13 UTC)

Branch: `research/esi-crash`. Fonte: JSONL privado entregue pelo operador,
`commands-20261113-035632-190340-pid2760.jsonl`. **SHA256 do JSONL original:**
`059554ab4dce8ce2b0d2f2da5817ebc4f5e910e3e2f88ff025d6586dfc5d09dd`.
O arquivo bruto não é versionado por conter dados de execução do jogo.

## Ambiente e resultado funcional

- Windows, `patch_dual_rules.py --observe-only`, sem DLL de regras.
- `joey_pc.exe` PID 2760; SHA256 atestado pelo cabeçalho da captura:
  `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- Operador confirmou **duelo concluído normalmente, com vitória do jogador**.
  Último evento de comando: `seq=1452`, opcode mascarado `0x0004`,
  estado terminal com LP `[0,7750]` (lado 0 CPU, lado 1 jogador).
- Início do trace `2026-11-13T03:50:37.328Z`; último evento
  `2026-11-13T03:56:14.743Z` (timestamp dos eventos, não tempo total da partida).

## Integridade e correlação auditadas diretamente

| Verificação | Resultado |
| --- | --- |
| Linhas do JSONL | 1453 = 1 metadata + 1452 eventos |
| Metadados | `session=1`, `seen=1452`, `retained=1452`, `overwritten=0` |
| `seq` de eventos | 1–1452, contínuos e sem duplicatas |
| Erros `readError`/`readErrors`/`correlationError`/`childOverflow` | 0 |
| `combat_context` schema | 645 eventos, todos `schemaVersion=2` |
| Identificadores de chamadas F40 | 64 únicos |
| Entradas F40 e saídas F40 | 64 / 64, pareadas por `combatCallId` |
| Probe `own_visibility_consumed` | 64 hits, exatamente um por F40 |
| Wrapper `own_stats` / `opponent_stats` | 64 / 64, um de cada por F40 |
| Mesmo `threadId` / `selectorCallId` por grupo | 64/64 |
| Ordenação entrada F40 → probe → own_stats → opponent_stats → saída F40 | 64/64 |
| `childCount` = quantidade de filhos realmente emitidos | 64/64 |
| `valueRaw` do probe = `attacker.positionRaw` na entrada = `own_stats.positionBeforeRaw/AfterRaw` | 64/64 |
| `wrapperArgsRaw` probe = `own_stats.argsRaw` | 64/64 |
| Posição rival na entrada = antes/depois de `opponent_stats` | 64/64 |
| Saídas F40 de 4 bytes; dois buffers stats de 12 bytes | 64/64 |
| `combat_context_limit` | 1 evento, `limit=64`, `seq=1318` |
| `ai_probe` seletor `select_normal` | 21 enter + 21 leave |
| `ai_probe` seletor `select_priority` | 19 enter + 19 leave |

Os 64 frames pertencem ao thread 22344, em 18 chamadas seletoras
identificadas; o número de eventos filhos por frame foi 7 (35 frames),
8 (19) ou 12 (10). Maior contagem: 12, abaixo do limite de 24.

Os valores consumidos na posição própria foram `0x0002` (8 eventos)
e `0x0202` (56). Ambos têm o bit visível (`0x0002`) definido;
o probe preservou os bits brutos, sem reduzi-los a booleano.
Na entrada F40, 45 alvos rivais estavam ocultos em defesa e 19
revelados em ataque (amostras de posição, **não** testemunhos de
visibilidade consumida). Os 64 campos `seedHex` da entrada e
`outputHex` da saída foram preservados; 12 eram iguais e 52
diferentes, o que pode refletir a computação da F40 e **não é erro
de integridade**.

## Limites da conclusão

1. **Sucesso estrutural e funcional de um único probe** na execução observada:
   `own_visibility_consumed` passou pela API corrigida, houve eventos
   correlacionáveis, sem sobrescrita/leitura inválida reportada e partida
   terminada normalmente. Isso não atesta overhead nulo nem segurança de
   outros oito pontos ou da combinação de nove.
2. O `combat_context_limit` indica que a instrumentação parou de produzir
   novos frames após as primeiras 64 avaliações; o restante do trace continuou.
   Não inferir contagem total de avaliações F40 da partida.
3. **Contextos independentes completos para avaliar em `Joey.Core`: zero.**
   Faltam testemunhos de postura rival consumida, visibilidade rival
   consumida, seed e máscara consumidas, flags consumidas e manifesto
   independente de modificadores/efeitos com atributos reconstruídos.
   Os dois `outputHex` de stats são observações nativas, não
   proveniência independente para validar a fórmula em C#.
   Portanto, nenhum `Match`/`Mismatch` de paridade C# é declarado.
4. A ausência de erros estruturais no JSONL e o testemunho do operador
   não provam a causa histórica do crash; ela segue como hipótese fortemente
   sustentada pelo teste x86 sintético e pela análise da API.

## Próximo gate recomendado

Manter o modo de nove hooks desabilitado. Projetar a instrumentação mínima
`combat-context v3` por fronteiras de função com as invariantes definidas
no handoff `native-esi-crash-static-analysis.md`, ou validar o próximo
probe individual **somente em piloto separado e supervisionado**.
Não juntar capturas de duelos diferentes como se fossem o mesmo estado.
Continuar classificando a avaliação independente como `Incomplete`
até haver evidência completa segundo
`docs/research/ai-combat-observation-contract.md`.
