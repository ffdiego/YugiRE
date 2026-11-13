# Handoff — combat-context v3 por fronteiras de função (pré-piloto)

Data: 2026-11-13. Branch \`research/esi-crash\`. Base: auditoria
[64 frames F40, um probe interno corrigido](../../evidence/combat-context-single-probe-audit-2026-11-13.md)
e [investigação da corrupção de ESI](native-esi-crash-static-analysis.md).

## Objetivo e entregas

Incluir um modo **separado, opt-in e mínimo** para observação passiva:
\`trace ai on combat-context-v3\`. O modo não executa a engine, não altera
argumentos, registradores ou callbacks, e não cria novos hooks de
instruções internas. Cinco listeners de fronteira, com \`onEnter\` e
\`onLeave\` naturais:

1. \`select_normal\` — \`0x005B1360\`;
2. \`select_priority\` — \`0x005B16C0\`;
3. \`combat_event\` (F40) — \`0x005B0F40\`;
4. \`own_stats\` (ADD60) — \`0x005ADD60\`, apenas return VA \`0x005B0F97\`;
5. \`opponent_stats\` (ADDC0) — \`0x005ADDC0\`, apenas return VA \`0x005B0FAE\`.

Há um gate de cinco nomes/endereços estáticos conhecidos: mudanças que
adicionarem outro alvo ou mudarem a lista esperada falham antes de
instalar hooks. A instalação possui rollback se algum listener falhar.

O modo \`combat-context\` v1 (10 listeners, schema 1) e o modo
\`combat-context-point <nome>\` (11 listeners, 1 probe, schema 2)
não foram substituídos. Os nove probes simultâneos continuam proibidos
em processos Windows, inclusive por RPC.

## Contrato de eventos e limites

- \`combatContextSchemaVersion=3\`, \`combatReadPointsArmed=[]\`,
  \`aiHooks=5\`. Metadados/captura de comandos mantêm esquema próprio.
- Cada entrada F40 continua oferecendo slots, identidades internas,
  posição, visibilidade e flags como **snapshots da entrada**,
  \`seedHex\` observado na entrada e saída de quatro bytes na saída.
  Propriedade \`provenance='F40-entry-snapshot'\`.
- Campos consumidos pela instrução interna:
  \`consumedPositionRaw=null\`,
  \`consumedDescriptorSeedRaw=null\`,
  \`consumedFlagsRaw=null\`. Não inferir valores pela igualdade
  de snapshots anteriores/posteriores.
- Cada wrapper \`own_stats\`/\`opponent_stats\` correlacionado ao pai
  \`combatCallId\`, \`threadId\` e caller natural recebe
  \`provenance='wrapper-boundary-snapshot'\`, parâmetros originais,
  posição antes/depois e buffer nativo de 12 bytes.
- Diagnóstico x86, bruto e **somente leitura**:
  \`esiExpectedRaw=(1-sideF40)*0xD44\` se lado 0/1;
  \`esiAtEnterRaw\`, \`esiAtLeaveRaw\`,
  \`espAtEnterRaw\`, \`espAtLeaveRaw\`,
  \`esiPreserved\`. Diferentes valores de ESI ou falha de
  amostragem são registrados como \`correlationError\` e/ou
  \`readError\`, **sem substituir o registrador**.
  O ESI capturado em entrada/saída da própria F40 pertence ao
  caller e não deve ser comparado ao deslocamento rival.
- Controles herdados: \`--observe-only\`, trace ligado,
  sem DLL de regras, sem \`trace prep\`/\`trace demo\`,
  sem overrides de velocidade/deck e sem multi-instância,
  até 64 avaliações F40, 24 filhos por F40,
  captura bounded com \`overwritten\` explícito.
  Campos desconhecidos permanecem \`null\`/Unsupported.

Esses cinco listeners **não são um substituto** para os testemunhos
de postura/flags/seed consumidos, nem para a proveniência independente
dos modificadores de atributos. O número de contextos completos para
\`Joey.Core\` continua **zero** até nova evidência.

## Verificações realizadas nesta unidade

- \`yugire_dual_loader.js\`, \`test_loader.cjs\` e \`agent.js\`
  passaram **análise sintática JavaScript** em V8, a partir do conteúdo
  recuperado da branch.
- Executou-se \`test_loader.cjs\` inteiro sob **V8 com mocks/shims
  Node e vm simplificados**: 3.147 asserções passaram, incluindo
  os testes v1/v2 existentes e o novo modo v3, ESI nos lados 0/1,
  omissão dos pontos internos, evento de corrupção simulada de ESI,
  rollback e bloqueio sem \`--observe-only\`.
  Esse ambiente de teste *não* é o Node.js real, nem reproduz o isolamento
  de realms de \`node:vm\`, x86 ou Frida. Identificou-se e corrigiu-se
  a tentativa de ler \`traceevents\` antes de parar o trace no teste novo.
- **Não foram executados** \`node native_hooks/tests/test_loader.cjs\`
  em Node.js real, \`python -m unittest\` ou Frida/Joey Windows nesta
  unidade. Não declarar esses estágios como PASS.
- Alterações apenas em \`native_hooks/yugire_dual_loader.js\`,
  \`native_hooks/patch_dual_rules.py\`,
  \`native_hooks/tests/test_loader.cjs\`,
  \`native_hooks/tests/test_console_commands.py\`,
  documentação e este handoff. Nenhuma alteração em
  \`Joey.Core\`, \`Joey.Lab\`, executável do jogo ou \`main\`.

## Próximo gate — Windows, somente após passar os testes reais

Na worktree Windows da branch experimental, atualizar por
\`git pull --ff-only\`, preservar worktree limpa, e rodar:

\`\`\`powershell
node .\native_hooks\tests\test_loader.cjs
.\native_hooks\.venv-frida\Scripts\python.exe -m unittest discover -s .\native_hooks\tests -p test_console_commands.py
\`\`\`

Se ambos passarem e o operador quiser prosseguir, abrir Joey original
de SHA256 \`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de\`,
e somente então iniciar \`patch_dual_rules.py --observe-only\`.
No console do observador:

\`\`\`text
trace on 20000
trace ai on combat-context-v3
trace mark boundary-v3-single-duel
trace status
# Exigir combatSchema=3, aiHooks=5, combatReadPoints=[] e nenhuma falha
# Um duelo natural, na velocidade normal, sem outros observadores ativos
trace off
trace status
trace dump
stop
\`\`\`

No primeiro crash, congelamento, comportamento anormal, erro de
leitura/correlação, overwritten, falha de assinatura/hash,
\`aiHooks\` diferente de 5, \`combatReadPoints\` diferente de [] ou
\`childOverflow\`, interromper e preservar JSONL/dump
**privadamente**. \`combat_context_limit\` após 64 avaliações é
saturação intencional, não perda de trace. Não reativar os
nove pontos simultâneos, não acelerar o jogo, não marcar paridade
C# sem contexto independente completo.

## Decisão seguinte

Após a captura real, analisar instantes antes/depois dos wrappers,
distribuição de ESI, divergências e chamadas correlacionadas;
definir próximo campo faltante por evidência. Propor um novo ponto
de observação apenas se as fronteiras forem insuficientes e
com um teste segregado. Não avançar automaticamente para novos hooks
ou para merge em main.
