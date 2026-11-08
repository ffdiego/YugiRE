# YugiLab — laboratório de duelos do Joey

O **YugiLab** é a interface para executar e gravar uma quantidade
determinada de duelos automaticamente. O código de hooking, DLL C++,
instrumentação Frida e experimentos nativos continua em
[`native_rules/`](../native_rules/README.md). Assim, não precisamos
mover arquivos internos ou alterar as rotinas já validadas.

## Executar

No **diretório raiz do repositório**, usando o mesmo ambiente Python
com Frida já instalado em `native_rules/.venv-frida`:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 100 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai
```

O valor padrão de `--speed-value` no YugiLab é **2400**.
Para um teste curto, use `--duels 3`.
O script recusa `--duels 0` e não aceita `--pid`:
**não** é permitido matar um PID fornecido manualmente.
`--allow-multi-instance` é opcional para rodar dois
laboratórios simultâneos (atenção a saves/replays compartilhados).

## Ciclo de vida do PID

1. O Frida cria **um novo processo Joey suspenso**, usando
   `device.spawn()`. O PID exato é exibido no terminal.
2. O mesmo processo recebe os hooks e volta a executar.
3. A gravação JSONL começa antes do `resume`, incluindo
   `processId` no cabeçalho. A saída é
   `scratch/traces/duels-<timestamp>-pid<PID>.jsonl`.
4. O contador acompanha o comando terminal `0x0004/0x8004`,
   sem presumir que ele indica quem venceu.
5. Após o último duelo, ou em caso de erro/Ctrl+C, o launcher
   **interrompe os hooks, tenta finalizar a captura, restaura
   o agente, desanexa e encerra somente o PID que criou**.
   Não encerra instâncias existentes do Joey.

A captura concluída tem uma linha final
`{"kind":"capture_summary", "complete":true, ...}`.
Se ocorrer erro de exportação, a captura pode ficar parcial,
mas o jogo ainda deve ser encerrado. Confirme a mensagem
`[yugilab] Owned Joey PID ... closed`.
Uma execução não é considerada bem-sucedida se não concluir
a captura ou se falhar o encerramento do processo.

## Compatibilidade

As formas antigas (`native_rules/native_rules.py --spawn ...`)
continuam funcionando, inclusive console e DLL nativa.
O YugiLab usa as mesmas verificações de PE/SHA256,
o mesmo buffer de eventos e a mesma implementação de hooks.
Não há alteração no EXE original em disco.

Veja [validação anterior da captura contínua](../docs/evidence/demo-lab-100-duels-live-validation-2026-11-08.md).
