# Checkpoint — motor de decisões da IA

## Objetivo e estado atual
Investigação estática consolidada em [ai-decision-engine.md](ai-decision-engine.md).
Seletores, ranking, score e dispatcher foram detalhados; escolha real26→CE30
**correlacionada, não reproduzida integralmente**. Próximo avanço prioritário:
Windows, observar métricas/gates de um único caso natural, conforme seção7.
Somente `main`, sem spawn/jogo no Fedora, saves, patches ou chamadas nativas.

## Piloto instrumentado — próxima captura Windows

O modo `trace ai on detail` (opt-in, só `--observe-only`) adiciona
cinco hooks **nas entradas reais** de B08B0, B1610, B0A50, B0B90
e B0950 aos quatro existentes. Eventos `ai_detail` registram baseline,
hipótese/slot, score assinado, eventos de confronto realmente
selecionados, AA/AC e máscara antes/depois do restore. Baseline externo
é distinguido por caller 005B738A, sem forçar um `selectorCallId` fictício.
O modo normal `trace ai on` mantém o contrato anterior.

A simulação dos hooks, rollback, saída de eventos e teardown passou no
fixture JS; **ainda não validado no Joey Windows**. O piloto deve capturar
somente uma decisão natural da CPU, e só depois ampliar a cobertura aos
gates por candidato. [Protocolo exato](ai-decision-detail-protocol.md).
Não repetir o teste em caso de crash.

## Descobertas confirmadas
- EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`;
  DAT `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.
- Política do caller 005B72A0: AE9C0>=0→normal(pass 1); caso contrário,
  baseline externo!=0→pass 0; baseline0→direto-1. Falha1/0→-1.
  Corrige esboço antigo “sempre0depois-1”.
- Ranking 005B154B..15AF: ATK>=900, bit(slot) em AC.low, score>best ou empate
  com mais perdas rivaisAA[3..5] e sobreviventes própriosAA[6..8]!=0.
  Empate integral mantém anterior; perdas própriasAA[0..2] não desempataram.
- Priority 005B16C0: retornos imediatos por ID (sem os filtros gerais), depois
  máximo de ATK/DEF base contra thresholds, com exceção de tributo que pode
  reduzir o threshold, depois primeiro nível<5 com filtro modo1/2 porpressão.
- Score 005B0A50 soma int16/MOVSX de eventos **escolhidos** porB0B90.
  Não somar propostasB0F40 capturadas indiscriminadamente. Máscara deações
  impede repetir atacante e limita a sequência a cinco sobcontrato normal.
- B0B90: selection sort crescente classe/ATK16; não estável emempates.
  B0D90: empate dealvo substitui por novo oculto, ou por DEF maior do novo
  visível. B0F40: score=ganho−perda truncado16; útil sepositivo ou mata
  rival sem perder atacante; exceções porrecursos/vínculos emS2.
- Snapshot2x720bytes demonstros ewords próprios6E/70. AA/AC,eventos,
  backups persistem como métricas/scratch. Feedback 005EBEE4 é escrito por
  0045E240 emhelpers e **fica fora do restore**. Não prova isolamento total.
- Dispatcher:005BC700→005BC620→005B7BD0→fase3/tabela 005EFB78→005B7B50.
  Tabela 005EFB50: estágio 2=005B72A0,4=005B7A50(ataques),5=005B75A0.
  Retorno1 avançaestágio e zera subestados. Duas priority podem ser etapas
  diferentes; não fallback direto/recursivo do normal.

## Evidência dinâmica disponível / inferências abertas
Original emscratch/traces/commands-20261111-022940-355228-pid36348.jsonl,
SHA `dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90`.
1120eventos íntegros;13normal(pass-1 todas,7negativos),10priority,12CE_CPU
com source igual à última seleção válida no mesmo lado/thread.
Caso enter10→leave26→CE30: mão894,359,893,307,364,550; retornaíndice5=550.
307=GoblinAttackForce/0540,364=PantherWarrior/02E8,550=GeminiElf/0213.
Evento25=0808C800 (score 200, remove rival) compatível1900−1700 contra
AxeRaider24. Filtros de Goblin (limiares1900ATK/2000DEF) ePanther (custo/aliado
maisfraco) são explicação plausível para550; **gates não capturados**.
Metadata.exeSha256=null: hash do EXE vem dodocumento associado, não atestado
peloheader do JSONL. Faltam baselines,score agregado,actionMask eAA porhipótese.

## Hipóteses / limites restantes
- Primeiro passe direto-1 é consistente com baseline externo0 nas13amostras,
  mas baseline interno pode ser outra avaliação; capturar ambos.
- Rota especial target5 compatível comataque direto, semamostra nofixture22.
- Reentrância/isolamento transitivo econsumoRNG não provados; só buffers
  globais compartilhados egates/efeitos explícitos foram auditados.
- Cálculo de todos osefeitos/etapas mágicas anteriores não foi portado.

## Caminhos descartados
- MaiorATK não explica a decisão26; legalidade não implica preferência.
- Não chamar helpers no duelo para“ver resultados”. Não ler estado durante
  hipótese como campo real; não chamar feedback não restaurado de corrupção.
- Propostas combat_event não são escolhasB0B90; A579A8 é zerado pela última
  tentativa semevento. Ler só depois deB0A50 não reconstrói a sequência.
- Relatórios ai-*.txt históricos citados nos MD não estavam no repo: nove
  relatórios novos preservam fontes primárias. ConsultarS1..S9 no documento.
- Tempfunc5B7B40/5B6C20 falharam por endereço estimado. Entradas corretas
  do dispatcher5B7B50 e5B7BD0 já resolvidas; não repetir guesses.

## Próximos passos priorizados
1. Captura mínima natural: B08B0/B0A50 retornos, B1610 args/slot,
   AAB90/AE3E0/AE220/AECE0 retornos, B0B90 eventos escolhidos, AA eAC
   emB0950 antes/depois. Ver tabela de momentos/endereço na seção7.
2. Confrontar primeiro elo divergente antes de ampliar corpus. Valores só
   emstack/registers: debugger separado no Windows; evitar midfunction Frida.
3. Auditar hashes/diffs de campos ewords antesB08E0/depoisB0950 sem escrever.
4. Capturar priority commodo/estágios, empate, alvo oculto,trib while mantendo
   os motivos de rejeição separados de legalidade. Só depois portar maisIA.

## Última ação / validação
Nove relatórios headless read-only concluídos. Build solução semwarnings/erros;
444checks C# passaram, incluindo7 novos e25344 combinações do comparator
contra oráculo debranches estáticos. `NativeAiRanking` é predicado puro,
isolado, sem IA ligada ao demo. TestesPython:16 ai* +13 decode_ai*, todosOK.
Auditor JSONL:12/12CE_CPU,0erros leitura. Fixture reduzido22eventos+2stats
conferido contraSHA/linhas originais; igualdade deutility/layout, não replayIA.
Nenhum Joey executado, banco semsave/reanalysis/import, EXE/DAT preservados.

## Commits relevantes
- `01152e4`: quatro relatórios, ranking e política depasses corrigida.
- `5b66125`: dispatcher/feedback, auditor/corpus real, predicadoC# e testes.
- Consolidação documental incluiS8/S9, pseudocódigos, casos e arquitetura.

## Preservação / bloqueios
Sóvalidação dinâmica faltante requer operadorWindows; não há decisão de
produto pendente. Dirty anteriores mantidos fora doscommits: docs/15,
scripts/README.md, scripts/run-archive-query.sh, duel-crash-sites.txt e
arquivos toolchain. Novos commits desta tarefa ainda sempush.
