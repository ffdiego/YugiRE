# YugiLab: 2 workers completam 6/6 duelos, mas não encerram o Joey

**Fonte real:** `parallel-u405p7z4.zip`, compartilhado em 2026-11-08,
com 2 logs, 2 JSONL e o manifest da execução Windows.

## Evidência

Comando: `--duels 6 --workers 2 --speed-value 600 --trace-ai`.

| Worker | PID Joey | Capturado | Eventos | Capture footer | Cleanup |
|---|---:|---:|---:|---|---|
| 1 | 8964 | 3/3 | 3.312 | `complete=true`, `overwritten=0` | `WinError 5` |
| 2 | 14556 | 3/3 | 5.255 | `complete=true`, `overwritten=0` | `WinError 5` |

No final, ambos escreveram
`Failed to verify termination of owned Joey PID <pid>: [WinError 5] Acesso negado`.
O coordenador permaneceu exibindo `6/6 duels; 2 workers still running`
e só produziu manifest após Ctrl+C, com `exitCode=2` e
`processTerminationVerified=false` para ambos.
O operador precisou finalizar manualmente os processos.

**Conclusão:** o sistema de captura está íntegro, mas a rotina
Windows `TerminateProcess(HANDLE)` falhou no teste real e
a estratégia anterior não tinha fallback. `WinError 5` não
identifica sozinho por que o Windows recusou a chamada.
A documentação da Microsoft também descreve esse erro
quando o processo *já terminou*; neste teste o relato de
janelas ainda abertas indica que não podemos presumir
o estado sem uma verificação pelo handle.

## Nova correção a validar

A ordem de encerramento dos workers passa a ser:

1. Salvar e fechar JSONL; tentar `restore` no agente.
2. Enquanto a sessão Frida existe, chamar
   `device.kill(pid)`; verificar o HANDLE com
   `WaitForSingleObject`.
3. Se ainda estiver rodando, chamar
   `TerminateProcess` no HANDLE original e verificar.
4. Se a chamada falhar/timeout, usar `taskkill /PID <pid> /F`
   como último recurso e verificar o MESMO HANDLE;
   registrar stdout, stderr e código do taskkill.
5. Somente depois liberar o HANDLE e executar
   `frida.shutdown()`. No fluxo *batch*, não executar
   `script.unload()`/`session.detach()` ao fechar,
   evitando uma possível corrida de descarregamento
   do agente durante a terminação.

Uma mensagem `Owned Joey PID ... closed (verified by Windows process handle)`
continua dependendo da confirmação efetiva do Windows.
O sucesso do manifest exige essa mensagem, além
do resultado `capture_summary.complete=true`.

**Atenção:** o fallback `taskkill` usa o PID do processo
criado, enquanto o HANDLE para o processo original permanece
aberto e não sinalizado. Não é permitido passar PID arbitrário
ao laboratório.

## Plano curto de verificação

Após atualizar a branch:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 2 --workers 2 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai --speed-value 600
```

Conferir se ambos exibem `closed (verified by Windows process handle)`,
`exitCode=0`, e `manifest.complete=true`.
Para inspecionar processos ainda ativos, sem matar
instâncias manuais:

```powershell
Get-CimInstance Win32_Process -Filter "Name='joey_pc.exe'" |
  Select-Object ProcessId, ParentProcessId, CreationDate
```

Se continuar falhando, os novos logs identificam em qual
tentativa (Frida, Win32 direto, taskkill) o encerramento falhou.

## Limitações

Esta revisão acrescenta simulações de fallback e proteção de PID;
**não foi testada no Windows real durante a alteração**.
Existe um relato público de regressão de unload do Frida
17.24.x no Windows, mas não há evidência de que o mesmo
bug esteja envolvido nesta execução específica.
