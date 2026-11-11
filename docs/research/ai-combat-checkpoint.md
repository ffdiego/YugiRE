# Checkpoint — engine de combate independente

## Objetivo atual

Expandir o produtor C# de atributos e scores sem substituir desconhecidos
por zero nem depender de métricas intermediárias do Joey. O confronto, a
sequência e um recorte do seletor comum já são executáveis; a próxima
fronteira é validar inputs completos e ampliar atributos/gates de forma
comprovada. Documentação principal: [ai-combat-engine.md](ai-combat-engine.md).

Branch main. Não executar Joey no Fedora, alterar EXE/DAT/saves/replays ou
salvar mudanças no banco Ghidra. Consultas read-only/noanalysis. Não usar
--spawn nem chamar helpers nativos para preencher lacunas.

## Descobertas e implementação confirmadas por análise estática

- `NativeAiCombat`: F40, buffers12/4 bytes, ATK/ATK e DEF, score int16,
  flags, empates/zero, ordem de proteção/supressão e exceções locais.
  Boost rival04E5 usa DWORD; threshold04E3 é assinado. NativeHalf preserva
  wrap32 e divisão assinada, não uma aproximação float.
- `NativeAiAttributes`: propriedades DAT para Axe Raider004D/índice24 e
  Gemini Elf0213/índice550; deltas FD40, aliases674E0 e trecho final DD30.
  Contexto completo declarado obrigatório; não descobre links/elegibilidade.
- `NativeAiSequence`: B0D90/B0B90/B0A50/B08B0/B09C0 em campo local imutável,
  com sort não estável, escolha de alvos, máscara, remoções e contadores.
  Somente dois recursos comuns e stats invariantes nas mudanças suportadas.
- Flags brutas de monstro são obrigatórias:20000 trava posição;40000 bloqueia
  ataque. Contexto/predicado explícito não inventa flags faltantes.
  ADE40 considera ameaças em defesa/ocultas, ignora actedMask e o bloqueio
  global de batalha do jogador rival; respeita LP e flags de monstro.
- ScoreSummary conserva bits altos do bufferAA; RankingSummary preenche
  sobreviventes no wrapper de candidato. Não confundir essas duas saídas.
- `NativeAiCandidateSearch`: mão inteira ordenada, inserção comum de nível4,
  baseline e scores C# seguidos de NativeAiRanking. Sem tributos/priority.
  Candidato não suportado invalida a busca, não vira candidato rejeitado0.
- CLI `src/tools/Joey.Combat` calcula do DAT sem engine original. Exemplo:
  `dotnet run --project src/tools/Joey.Combat -- ../passion/data.dat 550 24 attack --declared-neutral`.
  Gera0808C800 e sequência0C08C800, score200/mask1. Não é replay de captura.

## Evidência dinâmica disponível e hipóteses

Corpus `src/tests/Joey.Tests/Fixtures/ai-combat-differential.json`:
22 buffers F40,14 com dois stats pareados, oito sem stats suficientes,
**zero contextos completos**. Sob um único cenário neutro de ataque
explicitamente declarado,12/14 concordam.304/319 divergem: C# prevê
perda própria-500/-300, enquanto os buffers nativos indicam remoção rival0.
Postura de defesa0 é explicação possível, não comprovada; não ajustar inputs
pelo output. Faltam posições, flags, links e seed antes da escrita.

Captura local:
`scratch/traces/commands-20261111-022940-355228-pid36348.jsonl`, SHA256
`dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90`.
Builder e teste verificam hash/integridade e pareamento por thread/callId.
Capturas recentes gates/detail não estão locais; fixtures/relatórios
versionados preservam a validação anterior15/15 candidatos e7/7 seletores,
que ainda usa scores nativos. Não confundir com replay independente da IA.

## Caminhos delimitados ou descartados

- Não repetir catálogo de callbacks ou investigação visual nesta etapa.
- Não ampliar whitelist de monstros por parecerem comuns: gates completos e
  atributos intrínsecos precisam estar demonstrados primeiro.
- Não inferir contexto ausente para fazer os buffers baterem.
- Harness nativo sem UI não mostrou vantagem: globais, ABI32, recursos lazy,
  heap/critical sections e chamadas indiretas permanecem. Ver
  [headless-feasibility.md](headless-feasibility.md).
- CoreBoundaryReach:8 raízes/118 nós depth5; dispatchers409 nós depth3;
  F40 com sete serviços cortados9 nós e raiz OPEN. PURE não significa puro.
  Default005B726C estava dentro do executor; corrigido para005B7B50, confirmado
  por ponteiro PE005EFB84, prólogo e consulta read-only depth1.
- Self-test MinGW não executado: compiladores GCC/Clang/MinGW ausentes;
  não instalar nem fingir validação. Teste PE específico da raiz passou.

## Próximos passos priorizados

1. Validar o contrato fechado contra um snapshot completo de campo no Windows
   antes de B08B0/B1360 preparar a hipótese. Não requer forçar uma carta rara.
2. Para304/319 ou novo caso equivalente, capturar naturalmente inputs F40,
   posição, flags, descritor de entrada, stats ADD60/ADDC0 e retornos dos
   helpers D790/D8D0/699A0/B220 dentro da mesma chamada. Experimento detalhado
   em ai-combat-engine.md; manter observação passiva e thread/callId.
3. Estudar DD30 e descoberta de vínculos/gates para um terceiro monstro comum,
   com contexto integral e referência nativa verificável. Não retirar
   Unsupported dos casos gerais antes disso.
4. Comparar eventos escolhidos, máscara e agregação A50, depois ranking, com
   a captura completa. Sem isso, não declarar o seletor inteiro reproduzido.

## Última ação e validação

Baseline desta sessão: HEAD22bbd06;454 checks C#,16 Python ai e13 decode.
Resultado atual:600 checks C#;38 testes Python focados (16+13+8+1).
Build da solução inteira: zero avisos/erros. Cinco cenários CLI passaram:
ataque, defesa, reverso, carta não suportada, contexto obrigatório ausente.
Sem validação visual: nenhuma tela foi alterada ou executada nesta etapa.

Comandos da raiz do repositório:

```bash
export DOTNET_CLI_HOME=/tmp/joey-dotnet
export NUGET_PACKAGES=/home/diego/.nuget/packages
dotnet build YugiRE.sln --no-restore -m:1
dotnet run --project src/tests/Joey.Tests --no-build -- ../passion/data.dat ../passion/deck.ydc
python3 -m unittest discover -s tests -p 'test_ai*py'
python3 -m unittest discover -s tests -p 'test_decode_ai*py'
python3 -m unittest discover -s tests -p test_build_ai_combat_corpus.py
python3 -m unittest discover -s tests -p test_core_boundary_roots.py
```

## Commits desta sessão

- `e8646c7`: confronto puro e testes de layout/aritmética/exceções.
- `715fe34`: atributos do DAT e primitivas de equipamentos/half/aliases.
- `bffef95`: relatório de fronteiras e viabilidade headless.
- `0e83b1e`: sequência isolada, baseline e CLI independente.
- `8ff50e6`: candidato→score→ranking e corpus diferencial com proveniência.
- `23bef47`: correção da raiz do analisador e teste contra PE original.
- Commit seguinte: divergências explícitas do corpus e checkpoint consolidado.

## Preservação e bloqueios

EXE e DAT tiveram SHA256 reconferidos e continuam correspondendo aos valores:
EXE `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`;
DAT `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.

Mudanças anteriores preservadas, sem incluí-las nos commits:
`docs/15-rea-integration-todo.md`, `scripts/README.md`,
`scripts/run-archive-query.sh`, `docs/evidence/duel-crash-sites.txt` e
`docs/evidence/toolchain*.{json,txt}`. O bloqueio é evidência dinâmica completa,
não compilação ou ferramentas C#. Análises estáticas adicionais continuam
possíveis; não há necessidade de executar código original para prosseguir.
