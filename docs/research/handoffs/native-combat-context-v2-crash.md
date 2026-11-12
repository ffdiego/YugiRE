# Incidente Windows — piloto combat-context v2 (2026-11-12)

## Observação fornecida pelo operador

Durante um duelo normal, após armar `trace ai on combat-context` na branch
`feat/native-combat-context-v2` (commits `8227f4c`, `960d6ad`), o processo
`joey_pc.exe` encerrou por falha; o processo host do Frida continuou ativo.
O Event Viewer do Windows (Application 1000/1001, 2026-11-12 00:09:13–16,
horário local) registrou:

- Nome do módulo: `joey_pc.exe`; imagem `Z:\\YugiRE\\passion\\joey_pc.exe`.
- Código de exceção: `0xc0000005` (access violation).
- Offset da exceção: `0x001b1195`.
- PID reportado: `0x5b20` (23328).
- Dump WER listado pelo Windows: `C:\\ProgramData\\Microsoft\\Windows\\WER\\Temp\\WER.e108a41f-d059-4c84-8374-772f02a6a1c9.tmp.dmp` (disponibilidade ainda não verificada).
- ReportArchive mencionado: `C:\\ProgramData\\Microsoft\\Windows\\WER\\ReportArchive\\AppCrash_joey_pc.exe_2698a3e58e71e859ad2d3bfa3c40f2e4c34c60_466e90ba_cb3bbe45-e381-48b5-9565-05288869faa1`.

Com a base preferida do PE `0x00400000`, o offset aponta para a
VA `0x005B1195`, onde a F40 lê de memória as flags do lado rival.
O novo hook `defender_flags_consumed` fica na instrução seguinte,
`0x005B119B`. **O WER não fornece o valor de ESI, o endereço efetivo lido,
o tipo read/write do AV, a instrução efetivamente executada após relocação,
a pilha ou a lista de hooks ativos no instante.** A coincidência de endereços
é suspeita, mas não prova causalidade nem identifica um hook específico.

## Mitigação versionada na própria branch

Até a inspeção do dump, o console `trace ai on combat-context` arma somente
os dez listeners de entrada de função já usados antes da revisão v2; não instala
nenhum dos nove novos pontos internos. Esse modo emite `schemaVersion=1` e
não permite declarar contextos novos completos. O status reporta o esquema e
nomes de pontos internos ativos. O console não oferece combinação dos nove.

Para eventual diagnóstico **somente depois da triagem do dump**, é possível
escolher um ponto individual com `trace ai on combat-context-point <nome>`
(11 listeners, `schemaVersion=2`); o esquema continua incompleto, pois apenas
um dos nove testemunhos é armado. Os nomes/assinaturas permanecem em
`native_hooks/yugire_dual_loader.js`. `combat-context-all-unsafe` existe
somente para cobrir eventos sintéticos em mocks JS; não usar no Joey Windows.

Essa mudança é contenção de risco, **não uma correção da causa raiz** e ainda
não foi revalidada em jogo real. Os testes sintéticos não emulam instruções x86
ou o relocator do Frida. Não integrar à main alegando que o crash foi resolvido.

## Próximo dado necessário

Preservar dump WER original se ainda existir. Em um WinDbg com o dump aberto,
coletar `!analyze -v`, `.ecxr`, `r`, `kv` e desmontagem próxima de `@eip`,
registrando os registros, em especial ESI, EIP, ESP, endereço inválido da
exceção e bytes/desmontagem efetiva. Correlacionar com stdout do observador,
`trace status` e eventual JSONL parcial. Não inferir ESI a partir apenas do
endereço de falha. Se o dump não existir, parar no gate de evidência e planejar
uma coleta diagnóstica controlada antes de reabilitar hooks internos.

## Análise do minidump original (evidência adicional)

O operador recuperou o dump do PID **23328** e forneceu-o privadamente.
SHA256 do arquivo: `362f5f51a577783e4a64672e26c3fe0734a92a2c86f43905e6d22ab23f05476a`.
Ele é um MiniDump Windows de cerca de 5,7 MiB, criado em 2026-11-12
04:09:16 UTC, com 13 streams. **Não versionar ou publicar o dump bruto:**
contém memória do processo.

O stream `Exception` reporta thread **15384** (`0x3C18`),
`ExceptionCode=0xC0000005`, `ExceptionAddress=0x005B1195`, dois parâmetros:
`ExceptionInformation[0]=0` (**READ**) e
`ExceptionInformation[1]=0x0916763F` (endereço lido).
O contexto x86 de exceção registra:

| Registro | Valor |
|---|---|
| EIP | `0x005B1195` |
| ESI | `0x0871120F` |
| EDI | `0x00000000` |
| EBX | `0x00000001` |
| EAX | `0x00000000` |
| ECX | `0x00000000` |
| EDX | `0x00000000` |
| ESP | `0x001AE9A8` |
| EBP | `0x001AEA08` |
| EFLAGS | `0x00210202` |

Os seis bytes **presentes no dump** em `0x005B1195` são
`8B 86 30 64 A5 00`, ou `mov eax, dword ptr [esi+0x00A56430]`.
A soma `0x0871120F + 0x00A56430 = 0x0916763F` reproduz
exatamente o endereço inválido do stream Exception.
Pela inicialização estática de ESI na F40 e `EDI=0`, o offset de lado
rival esperado nesse ponto seria `0xD44`, não `0x0871120F`.
**Causa imediata demonstrada: ESI inválido ao consumir flags rivais.**
A origem dessa alteração de registrador não é demonstrada por este dump.

O dump inclui 256 bytes do código original **já interceptado** em
`0x005B1115..0x005B1215`. Nas instruções abaixo há desvios `E9` para
endereços próximos, compatíveis com trampolines do Frida:

| Hook instalado | Desvio observado |
|---|---|
| `0x005B119B` | `0x08711C04` |
| `0x005B11C6` | `0x08711E04` |
| `0x005B11CF` | `0x08712004` |
| `0x005B11DD` | `0x08712204` |

Extrapolando condicionalmente o incremento `0x200` por instalação
para os cinco pontos anteriores, o primeiro hook interno ficaria em
`0x08711204`. O ESI corrompido é `0x0871120F`, isto é,
**11 bytes após esse endereço inferido**. O primeiro ponto registrado
pela v2 foi `own_visibility_consumed` em `0x005ADD8D`; essa correspondência
é um **indício forte**, não prova de que esse hook foi o responsável.
As páginas dos trampolines em `0x0871....` não constam dos segmentos
de memória preservados no minidump e, portanto, não podem ser desmontadas
nem inspecionadas diretamente aqui. O dump não demonstra a ordem exata de
execução dos callbacks, nem se houve erro de relocação, preservação de registro
ou outra interferência. Como a exceção foi em `0x005B1195`, o hook na
instrução seguinte `0x005B119B` não é por si só evidência de que ele havia
sido chamado na avaliação que falhou.

A contenção agora também **rejeita** `combat-context-all-unsafe` dentro
de um processo Windows, mesmo via RPC direto; sua execução combinada fica
reservada aos mocks sintéticos. Testes de regressão foram ajustados, mas
**não executados neste ambiente**. O modo de função-entrada v1 e os
modos diagnósticos de um ponto não foram revalidados em execução real após
o incidente.

Próxima investigação: comparar a região `005ADD8D` e o epílogo do wrapper
`005ADD60` contra a preservação de ESI e considerar uma abordagem de
observação que não reescreva instruções internas. Não provocar novamente o
crash com todos os hooks; caso um piloto futuro seja aprovado, usar dump
persistente e isolamento de mudanças, interrompendo no primeiro sintoma.

## Reteste Windows com hooks internos desativados

Após atualizar a branch contendo a mitigação de modo padrão e correções de
asserções dos testes (commit `a10402c`), o operador informou que
**concluiu um duelo inteiro sem problemas** utilizando o procedimento de
`trace ai on combat-context` (modo padrão reduzido, sem pontos internos v2).
É um relato de validação funcional em jogo real, não uma medição independente
da integridade da captura; os arquivos de `trace dump`, contadores
`combatReadPointsArmed`, `overwritten`, erros de leitura e SHA256 do JSONL
não foram fornecidos nesta atualização. Nenhum número de contextos completos,
Match ou Mismatch deve ser inferido desse relato.

Esse resultado é consistente com a suspeita de que um dos pontos internos
ativados na execução anterior participou da corrupção de ESI, mas não
demonstra qual foi nem exclui diferenças entre partidas. A correção da causa
raiz e a validação individual de hooks internos continuam pendentes.
Manter o conjunto completo bloqueado no Windows. Próxima etapa recomendada:
preservar/inspecionar a captura do reteste e investigar estaticamente os
trampolines e alternativas de observação sem hooks internos simultâneos.

## Análise estática adicional do ponto 005ADD8D

A assinatura esperada do primeiro ponto interno, registrada em
`kCombatReadPoints`, é `A8 02 75 1B 0C 02`. Decodificação x86 de
acordo com a posição inicial:

| VA | Bytes | Instrução |
| --- | --- | --- |
| `0x005ADD8D` | `A8 02` | `test al, 0x02` |
| `0x005ADD8F` | `75 1B` | `jne 0x005ADDAC` |
| `0x005ADD91` | `0C 02` | `or al, 0x02` |

O `Interceptor.attach(0x005ADD8D)` precisa preservar a semântica de um
salto **condicional curto** incluído na região movida para o trampoline.
O preflight de assinatura atual só confere os bytes originais; **não testa
relocação, salto reescrito nem preservação de registradores**. O salto tem
destino fora dos seis bytes sobrepostos. Este fato aumenta a prioridade
de analisar o trampoline associado àquele hook, mas não prova erro no Frida.

Próximo avanço sem risco de executar o jogo: inspecionar em Ghidra o
controle de fluxo de `FUN_005ADD60` por volta de `0x005ADD8D`, os caminhos
de retorno, a preservação de ESI e a sequência de chamadas/uso de ESI na
`FUN_005B0F40`. Priorizar observação em entradas/saídas de função
existentes ou snapshots passivos antes de reabilitar instruções internas.
Manter `main` e os hooks combinados intactos até haver nova evidência.
