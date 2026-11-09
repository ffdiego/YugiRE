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
O RNG original usa o `srand`/`rand` do MSVC. Execuções
iniciadas no mesmo segundo podem compartilhar a seed;
prefira `--seed` para controlar os índices globais
de cada duelo e audite os resultados em `rng-audit.json`.

### Seed controlada e reprodutível (experimento)

**Agora existe `--seed` opcional**, para separar duelos em um
laboratório paralelo e tentar repetir exatamente as mesmas
condições iniciais em execuções diferentes.

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab `
  --exe "..\passion2\joey_pc.exe" `
  --duels 2 --workers 2 --seed 12345 `
  --deck-0 ".\native_rules\decks\demo-A.json" `
  --deck-1 ".\native_rules\decks\demo-B.json" `
  --trace-ai --speed-value 600
```

Com o mesmo `--seed`, cada índice de duelo global recebe
a mesma seed, mesmo que a distribuição entre workers ou
o intervalo de lançamento mude. Duelos diferentes
recebem seeds diferentes (mixer bijetivo `mix32-v1`),
e a seed efetiva é registrada em
`demo_init/rng_seed_candidate` nos JSONL.

O `--seed` foi **validado no Joey real com duas execuções
independentes**: mesmas ordens de cartas, comandos e avaliações
da IA. Os argumentos nativos brutos ainda apresentaram
diferenças em 16 bits superiores, de semântica não decodificada.
O hook é limitado ao `srand` na entrada do duelo demo 7.
Reinicializações de efeitos de cartas permanecem nativas.
A captura não é considerada concluída se uma aplicação
de seed esperada estiver ausente. Não é necessário
passar `--seed`: sem ele, o RNG original não é alterado.

Veja a [evidência estática e protocolo de teste](../docs/evidence/joey-rng-seed-control-2026-11-08.md).
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

Identificamos o caminho estático `FUN_00568D80` →
`FUN_005BDDB0` → `_rand @0x005C3C02`, e uma função
confirmada** de seed em `0x005C3BF5`.
Os hooks registram `demo_init/rng_seed_candidate` com o
argumento original, o chamador e, quando habilitado, o
valor efetivamente aplicado. O auditor associa a chamada
da entrada do duelo ao embaralhamento correspondente.
[Análise do setter e RNG](../docs/evidence/joey-rng-seed-control-2026-11-08.md).
### Comparar duelos reproduzidos (novo)

Após duas execuções com os mesmos decks, `--seed` e
`--speed-value`, compare os ZIPs ou as pastas
`scratch/labs/parallel-...` **sem executar o Joey**:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab.compare `
  ".\primeira-execucao.zip" ".\segunda-execucao.zip" `
  --output ".\scratch\comparison.json"
```

O comparador lê JSONL em streaming por duelo, agrupa
pelo **índice global da seed**, confere integridade de
sequência e footer, compatibilidade de configuração e
a seed efetivamente aplicada.

Ele verifica os embaralhamentos, a sequência de comandos
(opcodes, chamadores e **16 bits inferiores de cada argumento**),
eventos de IA, executor de demo, estado terminal capturado e
reinicializações intermediárias do RNG. Registra os argumentos
completos diferentes e agrupa seus 16 bits superiores
separadamente; **não assume que tais bits podem ser descartados**.

Se os checkpoints coincidirem mas valores brutos divergirem,
o relatório dirá `observed_match_with_raw_differences`.
Esse status não comprova identidade integral do estado nativo,
nem identifica vencedores.

**O Ghidra já demonstrou a origem de 118 das 119
diferenças de bits superiores.** Em 116 casos o conversor
`FUN_004022E0` devolve o ID em `AX` e conserva os
bits superiores do ponteiro da tabela em `EAX`. O
comando `0x61` escreve só `DX`; o `0x37` escreve
só `AX` após uma chamada anterior ao produtor
`FUN_005B9170`. O único caso ainda sem origem comprovada
é `0x86/0x00574804`.

O comparador agora apresenta `highWordOrigins`,
`verifiedRegisterResidueCommands` e
`unverifiedRawDifferenceCommands`, sem alterar os valores
originais. Use `--strict-unverified` para rejeitar **qualquer
diferença ainda não explicada por instruções nativas**
(1 neste experimento), ou `--strict-raw` para rejeitar
qualquer diferença, inclusive as 118 justificadas.

[Relatório do Ghidra e classificação completa](../docs/evidence/joey-command-highword-origin-2026-11-08.md).

Códigos de saída: 0 = checkpoints observados iguais;
1 = divergência relevante (ou exigência `--strict-raw`);
2 = captura inválida/incompleta ou configuração incompatível.

Nos dois ZIPs reais de 2026-11-08, o resultado foi
**2/2 duelos equivalentes nos checkpoints**, com
**119 comandos** contendo diferenças exclusivamente em
bits superiores. [Evidência](../docs/evidence/joey-rng-seed-reproducibility-two-runs-2026-11-08.md).
O script `scripts/Run-FinalHighWordRecon.ps1` recuperou o
assembly do `0x37`. No evento de `0x86`, o agente
passou a registrar `wrapper86Caller` lendo, sem alterar,
o endereço de retorno do wrapper na pilha x86
(`[ESP+20]` durante a chamada ao produtor).
O comparador também confronta esse novo campo quando
ele estiver disponível em execuções futuras.
Seu comportamento no jogo real ainda depende de validação.

**Novo contrato de fila confirmado:** `FUN_005B9170` armazena
quatro palavras de 16 bits. Para os opcodes `0x37`, `0x61` e
`0x86` (sem remapeamentos internos nesse caminho), o
comparador verifica adicionalmente `sameDefaultBranchQueuePackets`.
Nos dois ZIPs reproduzidos, **41/41 pacotes são iguais**, mesmo
com diferenças no alto dos argumentos brutos. Isso resolve a
**equivalência do pacote** para `0x86`, mas ainda não
identifica a origem de seus bits altos. Outros opcodes
podem transformar parâmetros antes de armazená-los.

O módulo `Joey.Core/NativeDefaultQueuePacket.cs` implementa
somente esses três casos para os testes de regressão C#,
sem substituir o protocolo completo.

### Exportar casos de invocação nativa para os testes C#

A nova instrumentação passiva de `trace demo` observa uma
transição de invocação em três pontos da thread do Joey:
entrada de `FUN_005B6FF0`, emissão de `0xCE` e
retorno posterior de `FUN_005BC700`. Ela só registra
`summon_transition/confirmed` quando consegue observar
a remoção de exatamente uma carta da mão, sua colocação em
uma zona vazia, LP inalterado, campos restantes preservados
e o uso da flag nativa de invocação. A sequência real dos
eventos de entrada e `0xCE` é verificada pelo exportador,
não apenas declarada pelo agente.

Com uma **captura nova e completa que use `--seed`**,
converta os eventos elegíveis em fixtures locais:

```powershell
.\native_rules\.venv-frida\Scripts\python.exe -m yugilab.export_summons `
  ".\scratch\labs\parallel-XXXXX" `
  --output-dir ".\scratch\differential-parallel-XXXXX"

$env:YUGIRE_DIFFERENTIAL_FIXTURES = (Resolve-Path ".\scratch\differential-parallel-XXXXX").Path
try { dotnet run --project ".\src\tests\Joey.Tests\Joey.Tests.csproj" }
finally { Remove-Item Env:YUGIRE_DIFFERENTIAL_FIXTURES -ErrorAction SilentlyContinue }
```

Os dois fixtures manuais e os 150 checks anteriores
continuam funcionando sem essa variável. Quando há
fixtures gerados, o C# compara adicionalmente a
sequência completa da mão e todas as posições de monstro
observadas. Nenhum fixture é gerado se a captura não
tiver provas suficientes. A saída deve permanecer em
`scratch/`, não ser comitada: revela cartas ocultas.

**Não é replay integral nem validação de efeitos/legitimidade
da invocação.** Os cards de setup ainda usam propriedades
sintéticas onde não temos metadados nativos; a nova
instrumentação necessita validação no Windows.
[Procedimento completo e limites](../docs/evidence/yugilab-automatic-summon-fixtures-2026-11-08.md).

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
mantém um *handle* persistente do processo criado pelo Frida.
Ao terminar, solicita o encerramento pelo Frida e verifica pelo
handle; se necessário usa `TerminateProcess` e `taskkill`,
confirmando o encerramento com `WaitForSingleObject`.
O handle permanece válido após `script has been destroyed`.
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
As duas rodadas reais com `--seed 12345`, dois workers e
um duelo por worker encerraram os processos corretamente.
[Diagnóstico da falha original](../docs/evidence/yugilab-two-workers-cleanup-error-2026-11-08.md).

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

**Próxima consulta direcionada:** `scripts/Run-FinalHighWordRecon.ps1`
cria `command-highword-final-37.txt` e `command-highword-final-86.txt`
sem modificar o jogo. A nova opção `bytes:` no `QueryArchive.java`
permite inspecionar memória mesmo em endereços que o Ghidra
não definiu como instruções.
