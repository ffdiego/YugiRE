# Laboratório contínuo de 100 duelos — captura offline

**Objetivo:** rodar N duelos de IA contra IA no Joey original, guardar um JSONL contínuo e estudar depois comandos, decisões, cartas, bootstrap e eventos de preparação.

**Estado:** validado no Windows real. Um piloto de 3 duelos e uma captura contínua de 100 duelos encerraram com `complete=true`, `overwritten=0` e sequências sem lacunas. [Relatório da validação](demo-lab-100-duels-live-validation-2026-11-08.md).

## Como usar

A partir de `native_hooks`:

```powershell
.\.venv-frida\Scripts\python.exe .\native_rules.py --spawn --exe "..\..\passion2\joey_pc.exe" --observe-only --speed-value 2400 --deck-0 ".\decks\demo-A.json" --deck-1 ".\decks\demo-B.json" --duels 3
```

Depois de validar com 3 duelos, repetir com `--duels 100`. Durante a coleta, o terminal não solicita comandos, mostra progresso e o número de eventos persistidos. O launcher encerra o **PID que ele mesmo iniciou** ao alcançar o alvo. Não é necessário reiniciar o jogo a cada duelo.

## Critério de conclusão

O agente conta uma conclusão quando `FUN_005B9170` emite comando com `(arg0 & 0x7fff) == 0x0004`, incluindo `0x8004`. Em capturas anteriores, cada demo concluída emitiu um terminal, mas **o vencedor ainda não está decodificado**. É possível haver início de próxima demo logo após o centésimo terminal e antes do último stop.

O progresso reflete o **número de comandos terminais** observados, não o número de mãos, turnos ou decisões da IA.

## Formato do arquivo

O arquivo é criado com exclusividade em `scratch/traces/duels-<timestamp>-pid<PID>.jsonl`, antes de continuar o executável suspenso:

- Primeira linha `kind=metadata`, `formatVersion=2`, `requestedDuels`, `speedValue`, `deckPlans`, `processId`, `session`, `startedUtc`.
- Linhas de eventos com `seq` monotônico contínuo e tipos originais (`command`, `demo_init`, `deck_override`, etc.). Para comandos, salva também `opcodeRaw`, `opcodeMasked`, `sideBit15`, `argsHex` e os campos CE/CF já interpretados no exportador legado.
- Linha final `kind=capture_summary`: `requestedDuels`, `capturedDuels`, `eventsWritten`, `overwritten`, `complete`, `finishedUtc`.

Guarde o arquivo bruto; futuras interpretações de opcode podem ser executadas **offline** sem repetir os duelos.

## Por que não lota o buffer de 20.000?

O Frida mantém um buffer limitado, mas o Python agora drena páginas de até 256 eventos:

1. `tracelivepeek(256)` lê os eventos mais antigos **sem** descartá-los;
2. Python verifica continuidade de `seq`, escreve as linhas e faz `flush+fsync`;
3. Só depois `traceliveack(session, firstSeq, lastSeq)` libera as entradas já persistidas;
4. Repete durante a partida, com polling de aproximadamente 250 ms;
5. No encerramento, chama `tracestop()`, exporta o resto com a interface antiga, confere contagens e acrescenta a linha de resumo.

O novo `tracestatus` diferencia `drained` (eventos removidos **depois** de salvar) de `overwritten` (perdas reais). A invariante esperada é `drained + retained + overwritten = seen`. Um salto de `seq`, sobrescrita, problema de disco ou falha no reconhecimento impede a coleta de ser declarada completa.

Embora o buffer seja drenado continuamente, isso **não garante zero perda em qualquer cenário**: se o Python não acompanhar uma taxa excepcional de eventos ou o sistema travar por tempo suficiente, ainda pode ocorrer sobrescrita. O verificador detecta e recusa afirmar sucesso.

## Limitações

- O estado da engine e as chamadas não instrumentadas não estão representados no JSONL. `complete=true` significa somente captura consistente dos **eventos monitorados**.
- O resultado vencedor/perdedor não foi inferido dos comandos `0x0004`; essa é uma investigação futura.
- O conteúdo secundário/terciário dos decks não foi validado end-to-end.
- `--duels` não deve ser combinado com `--rotate-events` ou `--cycles`.
- `Ctrl+C` tenta finalizar uma captura parcial com `complete=false`; ausência de footer ou saída de erro indica arquivo parcial.
- Uma instância em paralelo requer a opção `--allow-multi-instance`, e não há isolamento automático de saves, replay, configuração ou arquivos compartilhados.
- O monitor encerra uma execução sem novos eventos por 120 segundos, marcando-a incompleta.
- Começar com `--duels 3` antes de usar `--duels 100` é importante: os testes são simulados e ainda não comprovam funcionamento em produção.
