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


## Vários laboratórios em paralelo

O YugiLab agora divide uma quantidade de duelos entre
**processos Python/Frida independentes**. Cada worker cria um
Joey, usa o bypass de mutex por PID e grava seu próprio JSONL.
Na raiz do repositório:

```powershell
# Valide primeiro 2 workers / 3 duelos cada
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 6 --workers 2 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai
```

Se funcionar sem erros de instância, decks ou sobregravação:

```powershell
# Dez workers independentes, dez duelos para cada
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 100 --workers 10 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai
```

Se o total não for divisível pela quantidade de workers,
os primeiros recebem um duelo a mais: 23 duelos com quatro
workers distribuem `6 + 6 + 6 + 5`.

O limite atual é **16 workers**, mas quantidade maior
não significa ganho linear: cada Joey usa CPU, memória,
recursos gráficos e Frida. Compare a duração real de
2, 4 e 10 workers antes de definir o ideal.
O programa inicia cada worker com ~1,1 s de diferença
(`--stagger-seconds` pode alterar isso) para reduzir
possíveis sementes idênticas de RNG baseadas em relógio.
**Ainda não verificamos como o Joey escolhe sua semente**:
comparar partidas entre instâncias é necessário antes de
tratar resultados como amostras estatisticamente independentes.

### Auditoria de aleatoriedade (novo)

O modo paralelo agora audita automaticamente o **primeiro
embaralhamento de cada lado em cada duelo**. Ele gera
`rng-audit.json` na pasta da execução e inclui `rngAudit`
em `manifest.json`, com os campos:
`fingerprintedScenes`, `uniqueFingerprints`,
`duplicateObservations`, `crossProcessDuplicateGroups`.

Se duas instâncias observarem a mesma dupla de ordens
iniciais, o YugiLab exibirá um aviso. Isso **não prova** que
as seeds ou os duelos completos sejam iguais, mas indica
possível correlação entre as amostras. O aviso não torna
uma captura incompleta; a integridade e a independência
estatística são critérios separados.

O mesmo detector pode ser executado **offline**, até sobre
um ZIP antigo, sem Frida ou Joey:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab.rng_audit `
  .\scratch\labs\parallel-EXEMPLO `
  --output .\scratch\rng-audit.json
```

Em uma bateria real de dez workers, foram observadas 100
cenas com shuffle inicial e somente **65 combinações únicas**.
Veja a [evidência e as limitações](../docs/evidence/yugilab-rng-collision-audit-2026-11-08.md).

Ainda precisamos identificar a função que inicializa o
RNG original antes de oferecer `--seed`. O script de
consulta estática `scripts/Run-RngRecon.ps1` foi incluído
para essa investigação.
### Arquivos e validação

Cada execução paralela cria uma pasta exclusiva
`scratch/labs/parallel-.../` com:

- `worker-01.log` etc. (logs isolados, inclusive o PID);
- `captures/duels-...-pid<PID>.jsonl` para cada worker;
- `manifest.json` com o total pedido, a distribuição,
  PIDs, arquivos, status por worker, eventos gravados e
  `complete`.

O coordenador **só marca `complete=true` se todos
os workers encerraram com sucesso, cada JSONL termina
com `capture_summary.complete=true`, as contagens
correspondem exatamente às metas individuais e cada
PID tem `processTerminationVerified=true`**.

Em `Ctrl+C` ou falha de um worker, o coordenador cria
um sinal de cancelamento observado pelos outros workers.
Eles tentam parar seus hooks, salvar o restante dos dados,
desanexar Frida e matar apenas seu próprio Joey.
O coordenador **não mata o processo Python de um worker
por força**, pois isso poderia deixar um Joey órfão.
Se o Frida ficar permanentemente travado e o worker não
responder ao cancelamento, o coordenador pode continuar
aguardando a limpeza; confira logs e processos manualmente
nesse cenário.

### Limitação importante de isolamento

O bypass de mutex permite a coexistência dos PIDs,
mas **não isola arquivos, configurações, registro, saves,
replays ou outros recursos compartilhados** do jogo.
As gravações JSONL são exclusivas por worker, mas o
comportamento do Joey original em concorrência ainda
não foi validado. Faça o teste curto antes de dez instâncias;
não deixe saves/replays importantes expostos a risco de
concorrência, e use cópias de segurança quando pertinente.

No modo `--workers 1`, o comportamento antigo é mantido.
O `native_rules/` continua existindo e seus comandos
permanecem compatíveis. O coordenador não aceita `--pid`
de outra instância para encerramento.

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
mas o jogo ainda deve ser encerrado.

**Correção de encerramento (2026-11-08):** no Windows, o worker
agora mantém um *handle* persistente para o processo criado
pelo Frida. Depois da captura, chama `TerminateProcess`
**nesse handle**, e `WaitForSingleObject` confirma que o
processo realmente saiu. O handle continua válido mesmo
quando o agente Frida gera `script has been destroyed`.
Também elimina o risco de encerrar um PID reutilizado entre
criação e limpeza.

**Atualização após teste real com 2 workers:** foi observado
`WinError 5 (Acesso negado)` em `TerminateProcess`, mesmo após
ambos os workers concluírem 3/3 e salvarem os JSONL.
Agora tentamos `device.kill(pid)` **antes de desmontar Frida**,
verificamos a saída pelo HANDLE, e se necessário tentamos
`TerminateProcess` e `taskkill /PID ... /F`, sempre verificando
o mesmo processo. O fluxo de captura em lote evita
`script.unload()`/`session.detach()` durante o encerramento
e chama `frida.shutdown()` para finalizar o runtime Python.
**Esse último ajuste ainda exige validação no Windows.**
[Diagnóstico do teste e instruções](../docs/evidence/yugilab-two-workers-cleanup-error-2026-11-08.md).

A mensagem de sucesso agora é:

```text
[yugilab] Owned Joey PID 12345 closed (verified by Windows process handle).
```

Se o encerramento não for confirmado, o worker sai com erro
e o coordenador grava `processTerminationVerified=false`
no `manifest.json`. **Não basta mais que
`device.kill(pid)` retorne sem exceção**.
Fora do Windows, continua existindo apenas o caminho
legado por Frida, explicitamente marcado como não verificado.
A versão anterior registrava `closed` sem esperar o
término do processo — um falso positivo possível.

## Compatibilidade

As formas antigas (`native_rules/native_rules.py --spawn ...`)
continuam funcionando, inclusive console e DLL nativa.
O YugiLab usa as mesmas verificações de PE/SHA256,
o mesmo buffer de eventos e a mesma implementação de hooks.
Não há alteração no EXE original em disco.

Veja [validação anterior da captura contínua](../docs/evidence/demo-lab-100-duels-live-validation-2026-11-08.md).
