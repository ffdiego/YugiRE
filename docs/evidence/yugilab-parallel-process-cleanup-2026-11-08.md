# Falha de encerramento dos workers do YugiLab: diagnóstico e correção

## Evidência recebida

ZIP: `parallel-usos_27u.zip` com manifest, dez logs e dez arquivos JSONL.
Execução no Windows em 2026-11-08, `--duels 100 --workers 10 --trace-ai`.

- Duração total do coordenador: **684,14 s**, aproximadamente 11min24s.
- **9/10** workers terminaram com `exitCode=0`, footer
  `capture_summary.complete=true`, dez terminais cada,
  totalizando **90 duelos completos verificados** e
  **131.218 eventos** desses nove arquivos.
- O **worker 9, PID 23632**, produziu **17.461 eventos gravados**
  e **9 comandos terminais** antes do erro
  `script has been destroyed`; não existe footer
  `capture_summary`. Por isso o manifest marca `failed`.
  Isso **não implica** que esses nove duelos parciais não
  sejam úteis offline; só significa que o trace não foi
  finalizado/validado.
- Logs dos dez workers imprimiram
  `Owned Joey PID ... closed`, inclusive o worker 9,
  mas essa linha vinha **imediatamente após um
  `device.kill(pid)` sem verificação do Windows**.
  Portanto os logs antigos **não comprovam** que os
  processos desapareceram do sistema. O relato de
  instâncias remanescentes exige checagem do PID real.
- O `script has been destroyed` é uma perda da sessão Frida
  e precisa ser investigado separadamente: pode resultar
  de término/crash do jogo ou desconexão da instrumentação.
  Não afirmar causa a partir desse trace.

## Falha arquitetural

O `native_rules.run_spawn()` usava
`device.kill(pid)` após o detach e imprimia
`closed` assim que retornava. O método retorna
sem um contrato que demonstre que o processo Windows
entrou em estado de término sinalizado.
Se houver desconexão do Frida, perdemos ainda mais
capacidade de controlar o destino do Joey.

## Correção aplicada

Novo `native_rules/owned_process.py`, sem dependências externas:

1. Imediatamente após `frida.device.spawn()`, adquirir com
   `OpenProcess` um HANDLE persistente do **PID recém-criado**.
2. Manter o HANDLE até terminar a gravação, tentar limpar hooks
   e desanexar Frida.
3. Forçar a parada com `TerminateProcess(handle, 1)`
   **naquele processo originalmente criado**, e esperar
   `WaitForSingleObject(handle, 5000)` retornar
   `WAIT_OBJECT_0`.
4. Se o processo já terminou, o HANDLE sinalizado confirma
   esse fato sem tentar matar PID reutilizado.
5. Mensagem positiva **somente com confirmação do Windows**:
   `[yugilab] Owned Joey PID 12345 closed (verified by Windows process handle).`
6. Em erro/timeout, retornar código não zero e registrar
   `Failed to verify termination...`.
7. `yugilab/parallel.py` agora exige a mensagem validada
   e grava `processTerminationVerified` por worker.
   Sem confirmação de término não declara `complete=true`.

Mesmo que o Frida tenha destruído o script, o handle do
processo permanece válido até seu fechamento.
A alternativa Windows não utiliza `kill` por PID após
a execução para evitar reutilização de identificadores.
Fora do Windows permanece o legado Frida, **sem alegar
verificação de término**.

## Como validar

Atualize a branch e comece com um teste **2 workers / 3 duelos cada**:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 6 --workers 2 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai
```

Confirme no `manifest.json` que todos os workers apresentam
`processTerminationVerified=true` e `complete=true`.
Para conferir sobreviventes sem encerrar outras instâncias:

```powershell
Get-CimInstance Win32_Process -Filter "Name='joey_pc.exe'" |
    Select-Object ProcessId, ParentProcessId, CreationDate, CommandLine
```

Compare os `ProcessId` existentes com os PIDs da execução
recém-finalizada. Essa verificação é importante porque pode
haver uma outra instância de Joey iniciada manualmente ou
um eventual subprocesso diferente do PID retornado pelo Frida.

### Estado de validação

Há testes simulados de handles, encerramento normal, falha
ao terminar e perda de sessão Frida. O patch **não foi
executado no Windows real nesta alteração**; a validação
de comportamento e permissões `OpenProcess/TerminateProcess`
depende do teste acima.
