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
