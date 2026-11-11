# Native Rules — rotação de capturas e investigação da instância única

## Objetivo

O jogo já executa demos sucessivas no modo 7. Nas capturas
com `--speed-value 60000`, o buffer limitado a 20.000 eventos era o
principal limite para guardar um experimento longo. A repetição dos
duelos pelo executável não precisa ser reimplementada.

**Dois objetivos diferentes:**

1. **Rotação sequencial:** iniciar Joey suspenso → instrumentar antes
   do resume → coletar até o limiar → parar os hooks → exportar JSONL →
   restaurar observador → desanexar Frida → matar **somente o PID
   criado pelo launcher** → repetir. **Não exige duas instâncias.**
2. **Paralelismo:** investigar e, se seguro, tornar opcional a proteção
   de instância única do jogo para abrir múltiplos Joeys simultâneos.
   Ainda **não** se sabe como a proteção funciona.

## Captura sequencial com rotação (experimental)

Em `native_hooks`:

```powershell
.\.venv-frida\Scripts\python.exe .\native_rules.py --spawn --exe "..\..\passion2\joey_pc.exe" --observe-only --speed-value 60000 --rotate-events 15000 --cycles 5
```

`--rotate-events N` ativa o laboratório sem console interativo,
fazendo polling a cada ~250 ms. Aceita **16..19000** eventos
(o buffer tem capacidade fixa de 20000; não usar 20000
como gatilho por risco de sobrescrita durante o polling).
No batch, **o terminal não aceita entrada**: o launcher não executa
`input()`, pois controla o ciclo automaticamente. Use `Ctrl+C`
para solicitar interrupção e limpeza da sessão atual. A partir da
atualização de progresso, a cada passo de **5 pontos percentuais**
(ou no máximo a cada **5 segundos** mesmo sem eventos novos)
o launcher imprime uma linha com:

```text
[lab] cycle 2/5: 50.0% (7500/15000 events) | total 30.0%
```

Os dois percentuais significam **proporção de eventos da captura**,
não andamento de um duelo ou do número de duelos. A porcentagem
total só é definida para `--cycles > 0`; no modo ilimitado
aparece apenas `cycle N/unlimited`.

A nova exibição **só funciona em processos iniciados depois de
atualizar os scripts**; não pode aparecer retroativamente no
batch já em execução. Eventos também podem ultrapassar ligeiramente
o limiar entre polls; por isso o último `seen` pode exceder 15000.

Ao chegar ao limiar, para os hooks antes de salvar a captura via
`legacy.save_command_trace`, com criação exclusiva de um
JSONL diferente em `scratch/traces`. Depois só mata o **PID
que o próprio launcher fez spawn**.

`--cycles 5` cria cinco sessões em sequência, após a exportação
e o encerramento da anterior. Sem `--cycles`, executa uma sessão.
`--cycles 0` repete até `Ctrl+C` (encerramento da sessão atual
passa pelo `finally`, que tenta salvar e limpar antes de sair).
A cada ciclo o launcher valida o hash PE32 do arquivo e do módulo
carregado, reinstala os hooks desde a inicialização e reaplica
a velocidade configurada.

Regras de segurança:

- `--rotate-events` exige `--spawn --observe-only`.
  No modo interativo sem rotação, `stop` continua **desanexando
  sem matar** o Joey já iniciado, conforme comportamento anterior.
- Nunca mata processos encontrados por nome, PIDs fornecidos
  pelo usuário ou instâncias de terceiros. `device.kill` recebe
  exclusivamente o PID devolvido por `device.spawn` da sessão.
- Ao detectar `overwritten != 0`, a rodada reporta falha,
  ainda tenta exportar o que ficou retido, encerra seu processo e
  **não inicia outra rodada**.
- Falha na gravação ou no restore encerra o batch com código de erro.
  Se a exportação falhar, o script **não pode prometer preservar**
  os eventos na memória após desanexar o Frida.
- Sem eventos novos por 120 segundos, a rodada é interrompida
  para não ficar presa indefinidamente em erro de inicialização
  ou tela de segunda instância.
- O limiar pode ocorrer no meio de um duelo: o último duelo
  capturado pode estar **incompleto**. Os consumidores do JSONL
  precisam reconhecer ausência do comando terminal `0x0004`.
- O polling e a exportação não são atômicos em relação a
  processos externos. Não há armazenamento contínuo em streaming;
  ainda pode haver perda caso a taxa de eventos ultrapasse
  a margem de 1000+ eventos entre polls.
- O novo batch foi coberto por testes simulados de Frida (sem
  executar o original). Ainda requer validação de integração no Windows.

## Investigar proteção de instância única

Executar no Windows com o banco Ghidra já analisado:

```powershell
.\scripts\Run-SingleInstanceRecon.ps1
```

Esse comando reutiliza o `QueryArchive.java` em modo **readOnly**,
produzindo:

- `docs/evidence/single-instance-mutex.txt`: xrefs de
  `CreateMutexA/W`, `OpenMutexA/W`, `ReleaseMutex` e
  `CloseHandle`.
- `docs/evidence/single-instance-windows.txt`: xrefs de
  `FindWindowA/W`, `FindWindowExA/W`,
  `RegisterClassA/W` e `CreateEventA/W`.
- `docs/evidence/single-instance-locks.txt`: xrefs de
  `GetLastError`, `CreateFileA/W`, `LockFile` e
  `LockFileEx`.

### Resultado da primeira consulta (relatórios recebidos)

Os relatórios `single-instance-mutex.txt`,
`single-instance-windows.txt` e `single-instance-locks.txt`,
para o hash do executável documentado, estabeleceram:

- **`FUN_0054D460`** chama `CreateMutexA(NULL, TRUE, lpName)`,
  chama `GetLastError()` e compara o código com **`0xB7`**
  (`ERROR_ALREADY_EXISTS`). Retorna 1 na condição de objeto
  já existente e 0 no outro ramo.
- O chamador `FUN_0055DB10`, em `0x0055DBEC`, fica no caminho
  de inicialização antes da rotina de argumentos de execução.
  Isso é um candidato muito forte à checagem de instância única,
  mas ainda é necessário confirmar o nome e a reação do caller.
- **`FUN_0054D550`** libera e fecha o mutex mantido no objeto;
  portanto, substituir globalmente `CreateMutexA` é arriscado.
- Não foram encontrados imports simbólicos `FindWindowA/W`;
  os dois usos de `CreateEventA` encontrados criam **eventos sem nome**.
  Isso torna o mutex mais provável do que a detecção por janela/evento.
- **Ainda não foi realizado bypass**; as evidências são estáticas.

Segunda consulta, também somente leitura:

```powershell
.\scripts\Run-SingleInstanceFlow.ps1
```

Gera `docs/evidence/single-instance-flow-startup.txt` e
`single-instance-flow-mutex-name.txt`. Deve revelar nome do mutex,
origem do parâmetro e como o retorno se propaga até o encerramento
da segunda instância.

A presença de imports isoladamente não basta para comprovar uso na
restrição. A cadeia `CreateMutexA → GetLastError(0xB7)` já é uma
pista específica, mas ainda requer validação de fluxo e teste dinâmico
antes de decidir por um bypass opt-in do Frida sem alterar o EXE.

### Atenção a estado compartilhado

Mesmo que a verificação seja desativada, múltiplos processos
podem colidir em arquivos de configuração/decks, replays,
registro do Windows, diretório de jogo, portas de rede ou DirectPlay.
Antes de habilitar concorrência, verificar o que é compartilhado
e se é possível isolar diretórios e dados por processo. Para a
rotação sequencial acima, isso não é necessário.

## Próximos passos

1. Rodar os testes de rotação e um batch pequeno de 2 sessões
   no Windows; verificar que cada JSONL foi salvo antes do
   encerramento do respectivo PID.
2. Fornecer os três relatórios Ghidra da instância única.
3. Somente então implementar/desenvolver um bypass opcional,
   com logs de verificação e teste abrindo duas instâncias
   isoladas, quando os recursos compartilhados forem conhecidos.
