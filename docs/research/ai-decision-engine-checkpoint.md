# Checkpoint — motor de decisões da IA

## Objetivo
Reconstruir seletores005B1360/005B16C0, score005B0A50/baseline005B08B0,
snapshot/restore e correlacionar evento26→CE30 da captura normal real.
Somente main, Ghidra read-only, sem executar jogo, saves, patches ou helpers.

## Estado e evidências já obtidas
- Lidos os seis documentos solicitados e complementos ai-combat-*.md.
- EXE/DAT SHA conferidos: db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de / 7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed.
- Quatro consultas completas, sem reanalysis/import ou persistir símbolos:
  `../evidence/ai-decision-selectors-deep.txt` (seletores/snapshot/comparador),
  `ai-decision-combat-deep.txt` (score/eventos/atributos),
  `ai-decision-control-flow.txt` (assembly decisivo e tributos),
  `ai-decision-dispatch-and-scratch.txt` (callers temporários e scratch).
- Caller005B72A0 (ponteiro005EFB58), subestado byteA579A2: caso0 consulta
  AE9C0(side,-1,1). Se>=0 chama normal(side,1); senão calcula baseline08B0
  em7385: se!=0 chama normal(side,0), se==0 pula diretamente para -1.
  Se normal(1/0)==-1 também tenta -1. Corrige docs antigos 'sempre0depois-1'.
- Caso4 do mesmo caller usa priority e só executa modo0; caller005B75A0
  (ponteiro005EFB64) aceita resultado de AE270 diretamente. Explica rotas
  distintas de priority, mas dispatcher externo ainda precisa rastreamento.
- Comparador normal: ATK>=900, bit(slot) emAC.lo, score>best; empate aceita
  perdas rivaisAA[3..5] maiores e sobreviventes própriosAA[6..8]!=0.
  Empate integral mantém anterior. AA[0..2]=perdas próprias, [9..11]=rivais
  sobreviventes após candidato; baseline só computa perdas, preserva altos.
- Score0A50 soma int16 com MOVSX em005B0B5E; usa máscara açõesA55D70,
  eventos4bytesA579A6/A8 e altera campos hipotéticos até não haver combate.
- Snapshot só monstros2x720bytes e words própriosA55D6E/A55D70; A579AA/AC,
  scratch/evento/backups persistem. Determinar outros writes transientes.

## Captura dinâmica existente
`scratch/traces/commands-20261111-022940-355228-pid36348.jsonl`, SHA
`dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90`.
1120eventos;13normal(pass=-1 todas,7retornos-1),10priority;12CE_CPU coincidem.
Evento10: normal side0 passe-1 caller5B73BA, mão894,359,893,307,364,550.
Evento26: retornaíndice5→550; evento30 CE source550. Stats capturados incluem
307ATK2300,364ATK2000,550ATK1900 e rival24ATK1700/DEF1150. Evento25
0808C800=remoção rival,score200, compatível1900−1700. Falta baseline/gates/
score agregado/máscaras por candidato: **decisão ainda não reproduzida**.

## Arquivos em desenvolvimento após interrupção do servidor
- `scripts/ai_decision_trace_audit.py` + tests/test_ai_decision_trace_audit.py:
  autor live_case informou7 testes passados; root ainda deve revisar/rodar.
- Fixture22eventos: src/tests/Joey.Tests/Fixtures/ai-live-combat-events.json
  e tests/test_ai_live_combat_math.py; revisar conteúdo/testar.
- Agentes anteriores interrompidos pelo restart; sem outros agentes ativos.
  Notas ai-combat-math-notes.md e ai-live-case-notes.md ainda não existem.

## Próximos passos
1. Seguir tabela005EFB58/64 até dispatcher005B7Bxx e update005BC700;
   confirmar ordem/fallback sem inventar estado de carta legal.
2. Auditar writes diretos e limites de restore; tratar demais helpers como
   fronteira ainda não provada, sem declarar isolamento total.
3. Finalizar docs/research/ai-decision-engine.md com pseudocódigo detalhado,
   tabela/confiança, cadeia da captura, lacunas/experimentos mínimos e
   arquitetura C# proposta; transcrever só funções puras demonstradas.
4. Rodar testes offline scripts/fixture; commits pequenos e checkpoint.
5. Conferir hash originais, main e preservação de dirty files alheios.

## Preservação
Dirty anteriores: docs/15-rea-integration-todo.md, scripts/README.md,
scripts/run-archive-query.sh, docs/evidence/duel-crash-sites.txt e toolchain.
Não incluir nos commits desta pesquisa. Commit01152e4 preserva os quatro primeiros relatórios e este checkpoint.

## Avanço após retomada
- `ai-decision-outer-dispatch.txt` e `ai-decision-dispatch-table.txt`:
  dispatcher005B7B50 usa tabela005EFB50 e byteA579A0 (0..7+sentinela).
  índice2=005B72A0, índice4=005B7A50 (ataques), índice5=005B75A0.
  Retorno1 avança estágio e zera substagesA579A1/A2. Priority repetido
  pode vir dos estágios2 e5, não invocações redundantes sem contexto.
- 0045E240 apenas escreve005EBEE4; AAB90 e B7D0 podem chamá-la.
  **Global de feedback realmente fora do restore**, não só hipótese.
- DAT identifica307=GoblinAttackForce/0x540;364=PantherWarrior/0x2E8;
  550=GeminiElf/0x213;24=AxeRaider/0x4D. Filtro5B05A0 rejeita Goblin
  contra alvo oculto ou ATK<1900 ou DEF<2000; Panther tem filtros de custo
  e aliado mais fraco. Compatível com escolha26, sem causa dinâmica provada.
- NativeAiRanking transcreve somente gate final;25.344 combinações contra
  oráculo de branches;444 checks C# passaram. Nenhuma IA ligada ao demo.
-11 testes Python novos e5 anteriores de ai* passaram; auditoria real12/12CE
  CPU,13normalpass-1,10priority,0errosleitura. Fixture22combates rastreável
  aoSHAoriginal, incluindo1900−1700=200 e score0útil vs score0mútuo inútil.
- Duas consultas tempfunc falharam por entrada estimada incorreta (5B7B40,
  5B6C20); refazer só após identificar prólogo. Entrada correta dispatcher
  5B7B50 já resolvida. Não repetir guesses nem alterar banco persistente.
