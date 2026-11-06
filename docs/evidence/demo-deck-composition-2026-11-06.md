# Demo: origem dos decks e duas capturas (2026-11-06)

Fontes: arquivos enviados pelo operador: demo-start-path.txt, demo-deck-sources.txt, commands-20261106-034405-325983-pid10612.jsonl. Mesmo SHA256 de joey_pc.exe já validado. Captura em --observe-only, sem mutação pelo YugiRE.

## Resultados dinâmicos

Foram 2016 eventos sem perdas: 1195 ai_probe, 752 command, 42 demo_executor, 24 demo_init e 3 markers; duração 257,98 s. Duas demos completas:

| Demo | Side 0 | Side 1 | Inícios de turno | Encerramento |
| --- | --- | --- | ---: | --- |
| 1 | 43 cartas, assinatura cded4cf368272b89 | 40 cartas, 9789d9744fca6da2 | 10 | 0x0004 |
| 2 | 41 cartas, 65d77013fdeb9210 | 43 cartas, cded4cf368272b89 | 15 | 0x8004 |

As assinaturas são os 16 primeiros hex chars de SHA256 sobre os índices internos ordenados, separados por vírgula. O deck de 43 cartas é **exatamente a mesma sequência de índices pré-shuffle** nas duas demos, mas mudou do side 0 para o side 1. Os words completos mudam por bits de contexto. A hipótese de deck persistente do jogador e oponente variável é **plausível**, mas não demonstrada.

## Resultado estático: FUN_00569380 copia, não escolhe

Dois buffers de origem ficam em 0x00A54E60 e 0x00A54F42, com stride 0xE2. FUN_00569380(side) copia uma lista de índices uint16 da origem para o array de cartas do lado correspondente, depois FUN_00568D80(side,8) embaralha. O layout do buffer de origem tem três seções 80/15/15 com contagens uint16 em offsets 0xDC/0xDE/0xE0. A lista principal começa em +0, a lista em +0xBE também é copiada por FUN_00569380. Não assumir a semântica da seção intermediária.

O buffer escolhido por FUN_00569380 é:
`sourceSlot = side ^ (DAT_00A54E5C & 1)`.
Logo, a escolha das **cartas** e a troca de **lado** são independentes. Ainda falta localizar quem preenche os dois buffers de origem.

## Caminho de bootstrap

FUN_00483500 -> FUN_0044C3E0 -> FUN_005BFEB0 -> FUN_005B9E50 -> FUN_00569380(0/1) -> FUN_00568D80(ambos).

FUN_0044C3E0 passa DAT_005F3288/8C/90 à inicialização. FUN_005BFEB0 consulta DAT_005F3294 (modos numerados 0..7) e prepara DAT_00A54E5C. **Ainda não se identificou o temporizador de inatividade do menu**, nem a função que escolhe/preenche os dois decks.

## Instrumentação adicionada

trace demo on agora captura, passivamente, `sourceDecks` (as duas origens) e `config.sceneMode` na entrada do bootstrap/reset, além de `sourceSlot`, `sourceDeck` e `config.deckSideBit` na entrada de deck_prepare. Validada em simulação; falta confirmação no Joey real. Commits: e421caca, 7e9a1c81, 2645aa2b.

## Próximos Ghidra

```powershell
.\scripts\Run-ArchiveQuery.ps1 -Report demo-menu-trigger.txt -Addresses @(
 '00483500', 'ref:00483500', 'xref:005f3294',
 'xref:005f3288', 'xref:005f328c', 'xref:005f3290'
)
.\scripts\Run-ArchiveQuery.ps1 -Report demo-deck-population.txt -Addresses @(
 'xref:00a54e60', 'xref:00a54f3c',
 'xref:00a54f42', 'xref:00a5501e',
 '00569380', '00599e30'
)
```

A próxima demo com nova instrumentação permitirá correlacionar composição, slot de origem, lado e modo de cena antes da cópia e do shuffle. Nenhum buffer deve ser alterado diretamente antes de identificar o código que o prepara.
