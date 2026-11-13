# Handoff — fixture x86 independente para investigação do ESI

Data: 2026-11-12. Trabalho desenvolvido diretamente na branch
research/esi-crash, como continuação da análise
[native-esi-crash-static-analysis.md](native-esi-crash-static-analysis.md).

## Objetivo e escopo

Implementar um experimento que distingue Interceptor.attach com listener
objeto de Interceptor.attach com callback função direta em instrução interna.
**O experimento não abre o Joey, não injeta no processo do Joey e não altera
o modo combat-context do jogo.** Não versiona executáveis, memória nem
registros privados.

Arquivos novos:

- native_hooks/experiments/esi_stack_probe/esi_fixture_x86.c:
  alvo Windows x86 descartável, __declspec(naked), prólogo com ESI salvo,
  janela de seis bytes A8 02 75 1B 0C 02 e saídas raw de ESI, ramo e ESP;
  nenhuma desreferência do ESI potencialmente corrompido.
- build.cmd: compilação MSVC x86 com símbolos de depuração.
- agent.js: duas formas de Interceptor.attach, uma por processo, com captura
  dos registradores e identificação do runtime Frida.
- run_fixture.py: três modos (baseline/probe/object), um processo filho
  exclusivo por execução, preflight MZ/PE x86/magic/nome/PID e handshake READY/GO.
  O modo object exige flag explícita de confirmação.
- native_hooks/tests/test_esi_fixture_harness.py: testes offline de preflight,
  filtros, contrato do handshake, dois caminhos de execução, classificação
  e consistência entre ESI/EIP/ESP observados e resultado do processo.
- README.md, .gitignore.

O probe é localizado pela assinatura exata e única no código da fixture,
sem endereços absolutos inventados; o runner só se conecta ao PID que
acabou de iniciar. A instância criada fica bloqueada no stdin até que a
instrumentação confirme o registro do hook; em timeout o filho é encerrado.
Cada execução tem um hit e um valor AL=0 ou AL=2. O programa não invoca
nenhum código do jogo. O programa deixa o processo vivo por 150ms após
emitir RESULT para permitir coleta do evento.

Saídas esperadas sem corrupção: ESI=0x00000D44, route=1 quando AL=0,
route=0 quando AL=2; diferença de ESP=+4 no POP. O teste com objeto pode
alterar o ESI salvo para o trampoline de saída; sua ausência não
refuta o incidente histórico. Se um processo cair antes de emitir RESULT,
a observação é inconclusiva, não um match.

## Verificações efetivamente realizadas fora do Windows

- 9 testes novos: python -m unittest discover -s native_hooks/tests
  -p test_esi_fixture_harness.py: **OK** (execução do teste isolado).
- python -m py_compile run_fixture.py: OK.
- node --check agent.js: OK.
- clang-cl --target=i686-pc-windows-msvc /W4 /Od /TC /Zs,
  com *headers de assinatura mínimos locais usados somente para validação
  sintática*, não com o SDK Microsoft: OK.
- Compilação de objeto COFF x86 com clang-cl e llvm-objdump: o
  disassembly confirmou TEST A8 02, JNE 75 1B, OR 0C 02,
  marcação de ramo, vinte NOPs, POP ESI e RET nas posições planejadas.
  O destino do JNE está após os 20 NOPs.
- Não foi realizado build/link com cl.exe do Windows, nem execução da
  fixture, nem attach de Frida Windows; não declarar que isso passou.
- Não foram executadas nesta unidade as demais suítes do repositório,
  .NET, C++ ou qualquer duelo.

## Procedimento de Windows

Ler native_hooks/experiments/esi_stack_probe/README.md. Abrir o x86
Native Tools Prompt do Visual Studio, compilar por build.cmd,
e rodar do PowerShell na ordem: baseline para AL=0/2, probe para AL=0/2.
**Somente se os quatro resultados forem pass**, testar object para AL=0
e eventualmente AL=2, no executável descartável e com flag explícita
--allow-unsafe-object-control.

Cada chamada gera um JSON privado sob scratch/esi_stack_probe, com
hashes, versão do Frida, runtime do script, PID, endereço real do probe,
registros, saída do filho e verdict. Preservar hashes sem publicar dumps.
Primeiro resultado errado, crash ou inconsistência na pilha encerra
o piloto até revisão. O monitor não aceita nome joey_pc.exe, não recebe
PID arbitrário e não contém alternativa para instrumentar o jogo.

## Limites e próximo gate

Os testes Python e a inspeção do COFF confirmam **somente** o contrato
da fixture. A reprodução ou não reprodução do bug depende de execução
real no Windows com a versão Frida do incidente. Mesmo uma reprodução
não prova a ordem exata de execução do processo Joey que caiu.

Não alterar a main nem reativar os nove pontos internos. Após resultados
reais, revisar se a instrumentação v3 deve permanecer apenas nas fronteiras
de função ou se um probe individual será admissível em piloto próprio.

## Primeiro piloto Windows — false negative por formato de endereço

Em 2026-11-12 (horário local), o operador compilou com MSVC x86 e executou
baseline AL=0 e AL=2: ambos `verdict=pass exit=0`.
Os dois testes `probe` terminaram com `exit=0`, mas o runner reportou
`inconclusive_hook_address_mismatch`. O aviso opcional
`Unable to start idund` apareceu nos dois testes com Frida.

Na revisão estática do runner, foi encontrado um bug inequívoco de
comparação **textual** do alvo: o fixture imprime endereço com 8 dígitos,
enquanto `NativePointer.toString()` pode omitir zeros à esquerda.
O runner agora normaliza ambos como inteiros x86, verifica também o modo
no evento `armed` e preserva uma eventual falha anterior de invariantes
de registradores em vez de sobrescrevê-la pelo diagnóstico de endereço.
Foi adicionado teste de regressão para endereço com e sem padding e
para valores inválidos. O helper isolado passou 7 casos locais, mas a
suíte integral do runner e o reteste Windows após a correção ainda não
foram executados nesta atualização.

O `exit=0` não é sozinho prova de instrumentação íntegra: é necessário
confirmar um `armed` compatível, um `hit` e as invariantes de ESI/EIP/ESP.
Não iniciar o controle negativo `object` antes de ambos os probes
resultarem em `pass` no Windows.

## Resultado do piloto sintético Windows — mecanismo reproduzido

Em 2026-11-12 no horário local (arquivos de relatório com timestamp UTC
2026-11-13), o operador comunicou os seguintes resultados do executável
descartável Windows x86 compilado com MSVC:

| Modo | AL | Saída reportada |
| --- | --- | --- |
| baseline | 0 | \`verdict=pass exit=0\` |
| baseline | 2 | \`verdict=pass exit=0\` |
| probe (callback função) | 0 | \`verdict=pass exit=0\` |
| probe (callback função) | 2 | \`verdict=pass exit=0\` |
| object (listener \`{onEnter}\`) | 0 | **\`verdict=object_overwrote_saved_esi exit=20\`** |

O modo object foi executado somente na fixture, com
\`--allow-unsafe-object-control\`. Relatório privado informado pelo
operador: \`scratch/esi_stack_probe/esi-20261113T034044533004Z-object-al0.json\`.
O programa detectou um valor de ESI diferente de \`0xD44\`, e o runner
classificou o resultado como substituição da palavra salva. O processo
retornou 20 intencionalmente nessa condição. Nos modos baseline e probe,
ambos os caminhos passaram após corrigir a comparação textual do endereço
do hook.

**Alcance:** esse resultado é reprodução funcional no alvo sintético do
mecanismo esperado da API Frida; ele fortalece substancialmente a hipótese
de sobrescrita do save de ESI no incidente histórico. **Não prova** que
o valor escrito no piloto corresponda a um trampoline específico sem
conferir os campos brutos do JSON; tampouco reconstitui a execução do
PID 23328 no Joey. Versão exata/runtime do Frida e valores raw de
\`restoredEsi\`, \`probeAddress\` e \`fridaMessages\` ainda não foram
auditados neste relatório (somente a saída resumida foi fornecida).

Próxima ação: examinar o JSON privado em busca do valor raw de ESI,
runtime Frida, endereço observado e evento \`hit\`, sem publicar dump nem
fazer outra injeção. Registrar conclusão discriminante e então elaborar
piloto mínimo separado para Joey; não executar hooks combinados da v2.

## Evidência raw adicional — fixture x86 / objeto

O operador consultou o JSON do piloto \`object --al 0\` e apresentou:

- Frida Python: \`17.25.2\`; runtime do agente: \`QJS\`; arquitetura: \`ia32\`.
- Endereço do ponto de instrução: \`0x00837290\` (o Frida apresentou \`0x837290\`).
- Evento \`hit\`: \`ESI=0xD44\`, \`EIP=0x837290\`,
  \`ESP=0xF6FAA4\`, \`EAX=0\`, um único hit.
- Saída da fixture: \`restoredEsi=22021647\` = \`0x0150060F\`,
  \`fallthroughMarker=1\`, \`reportedHealthy=false\`, código \`20\`.
- \`espAfterFirstPop=16186024\`; diferença de \`+4\` com o ESP do hook.

A diferença \`0x0150060F - 0x0B = 0x01500604\` fornece um **candidato**
para o trampoline de saída nesta execução sintética. O ESI do crash
histórico (\`0x0871120F\`) também termina em \`+0x0B\` relativamente à
entrada candidata \`0x08711204\`. Esses endereços de base **não foram
desmontados ou confirmados como trampolines reais** aqui; o que está
comprovado é a substituição do ESI salvo no executável sintético por
um valor incorreto. A semelhança do offset fortalece a hipótese
mecânica, sem reconstituir o evento histórico dentro do Joey.

Próximo gate: testar **um** probe de instrução da branch corrigida
no Joey com \`--observe-only\`, hash validado, limite de eventos,
sem outros patches, em sessão curta e supervisionada. A captura deve
ter status com exatamente um \`combatReadPointsArmed\`, integridade sem
perdas/erros e comportamento normal; não reativar o conjunto completo
nem afirmar contextos completos só por sucesso desse teste.

## Piloto Joey Windows com um probe corrigido — auditoria de contadores

Relato do operador em 2026-11-12 (horário local) sobre a branch
\`research/esi-crash\`, após correção do overload de instrução.
Joey original anexado com \`patch_dual_rules.py --observe-only\`, sem DLL
de regras. SHA256 de executável informado pelo preflight:
\`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de\`.

Comandos usados:
\`trace on 20000\`;
\`trace ai on combat-context-point own_visibility_consumed\`;
\`trace mark esi-single-probe-pilot\`;
\`trace status\`;
\`trace off\`;
\`trace dump\`;
\`stop\`.

O status **antes do duelo** confirmou \`combatSchema=2\` e
\`combatReadPoints=['own_visibility_consumed']\` (apenas um ponto de
instrução armado). Ao interromper, o console mostrou
\`combatContextCount=64\`, \`seen=1452\`, \`retained=1452\`,
\`overwritten=0\`. Exportação local reportada:
\`scratch/traces/commands-20261113-035632-190340-pid2760.jsonl\`.
A execução do \`stop\` concluiu sem erro de restauração reportado.

Uma contagem local PowerShell, excluindo a linha de metadados,
encontrou **1452 eventos**, **64 testemunhos**
\`combat_context/function=own_visibility_consumed\`,
**1 evento \`combat_context_limit\`** e **0 eventos** com
\`readError\`, \`correlationError\`, \`childOverflow=true\` ou
\`readErrors\` não vazio. Os primeiros valores raw reportados:
2, 2, 514, 514, 514, associados aos \`combatCallId\` 3, 14, 33,
42 e 60. A primeira contagem de erros (1389) era um falso positivo
provocado por \`@($_.readErrors).Count\` no PowerShell quando
\`readErrors\` era \`null\`; foi corrigida pela verificação explícita
de null.

**Interpretação limitada:** o único probe interno corrigido produziu
64 observações sem erros estruturais detectados nos campos verificados,
sem sobrescrita do buffer de trace e sem crash apresentado no log do
observador. O limite de 64 truncou deliberadamente a cobertura de
contextos de combate, mas não é \`traceDropped\`. A conclusão do
duelo pelo jogo e a auditoria de pares completos F40/filhos,
sequência e conteúdo integral do JSONL permanecem **pendentes**;
o arquivo bruto não foi anexado nem auditado diretamente neste
relatório. Não inferir 64 contextos completos, paridade com C# ou
segurança dos outros oito probes.

**Próxima etapa:** auditar o JSONL local preservado, agrupando por
\`combatCallId\` e correlacionando os eventos de entrada/saída de
\`combat_event\` com cada \`own_visibility_consumed\`; identificar
ausência de pares, qualquer sequência perdida e os campos que
continuam desconhecidos. Uma segunda coleta ou ativação de outro
ponto só depois dessa auditoria e da confirmação de comportamento
normal do jogo.
