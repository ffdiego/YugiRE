# Handoff — native effect discovery queue v1

Branch: `feature/native-effect-discovery-queue-v1`.
Stacked on PR #5 (`feature/effect-lab-pilot-v1`) which depends on PR #4.
Do **not merge** ahead of dependencies or change unrelated native hooks.

## Work completed

- `yugilab/discovery.py`: pure Python, offline catalog selection and JSONL
  streaming. `prepare` composes rounds of 40-card demo decks using 16 already
  reviewed normal controls, 12 level-1..4 effect-category monsters (excluding
  the known special-summon restriction list), and 12 nonmonster class 21–23
  candidates. Different pairs rotate. No C# gameplay manifest is created
  for unknown effect cards.
- `analyze`: cross-references native resource-associated CF/CE and existing
  scoped P045 LP-delta linker with Joey card identities, reported callback
  availability and verified native deck overrides. Outputs JSON/Markdown
  ranked queue, hashes, examples with seq, and identity ambiguity warnings.
  All full-C#-comparison readiness remains false until causality/context
  is independently proven.
- `yugilab/tests/test_discovery.py`: synthetic deck rotation, fingerprint
  gates, provenance, 300-LP positive case, CE-only case, incomplete footer,
  and missing deck verification negative case.
- `docs/research/native-effect-discovery-queue-v1.md`: Windows operator
  commands, one-worker three-duel stop gate, and strict evidence taxonomy.

## Evidence and validation

- Actual original-game Raimei capture previously observed: 3/3 native
  duels, 99,429 events, 0 lost, nine resource-linked 300-LP deltas.
  This prior evidence is source data, **not** execution of new discovery code.
- Existing `yugilab.effect_report` v2 output was 9/9 exact LP deltas
  in the operator's Windows environment.
- **New `test_discovery.py` and `discovery.py` have not been executed**
  in this environment. The Windows operator must run offline unit tests
  before using the deck files; no DAT/EXE or native Joey executed here.

## Next action

1. Run `python -m unittest discover -s yugilab/tests -p test_discovery.py -v`.
2. Run `python -m yugilab.discovery analyze` on previous Raimei capture
   and check three complete duels, original nine bounded LP cases, and
   verified-deck batches (three on each side).
3. Generate three rounds of exploratory decks from local registry/attributes
   and reviewed normals. Inspect the plan for weird or illegal-looking cards.
4. Run *only* the first three-duel pair in a Windows backup/safe worktree,
   without `--trace-ai`; check JSONL footer, PID cleanup and deck overrides.
5. Analyze new report; prioritize first non-P045 associated resource with
   observable state context. Do not automatically scale to 100 duels.

Risks: DAT class bits do not guarantee correct card mechanics or
normal playable status; `CE` is often a placement command; a missing
command does not establish absent effect. An ambiguous resource ID may
map to multiple internal indices. Native demo rules differ from
normal game and from C#. No evidence registry promotion.

## Primeira rodada nativa verificada pelo operador (16/11/2026)

- Capture file: `duels-20261116-041556-587954-pid22896.jsonl`; SHA256 `179bab9f66660e1c546a66e717ad37c1a2575784e0551e71fb1d82ebba8292a0`.
- 3 duelos, 2.159 eventos, integridade true, issues [], 80 identidades em decks verificados, 3 verificações por lado, 52 resource IDs CF/CE; 1 delta P045 exato observado de Raimei.
- Distinção após analisar `discovery-report.json`/`.md`: 23 comandos CF em 14 identidades (13 cartas novas além de Raimei) e 54 CE em 38 identidades, incluindo 24 controles normais e 14 monstros com bits de categoria de efeito. `CE` não comprova efeito.
- Primeiro alvo: Rain of Mercy (internal 969 / resource 1315, CF seq 335 e 1191); verificar mudanças de LP independentes nos dois lados antes de qualquer claim. Segundo alvo: Tribute to The Doomed (internal 363 / resource 1023, CF seq 95, 1065, 1673), que exige campo/mão.
- Novo `yugilab.inspect_effect_windows` exporta apenas janelas de comandos e amostras LP do JSONL local, com teste sintético. Ainda **não executado no Windows**; executar testes e extrair `windows.md` e `windows.json` conforme protocolo. Não exigir ou versionar a captura completa.

## Texto de cartas e observação Rain+Skull (16/11/2026)

- Fonte oficial moderna: Skull Invitation é armadilha contínua; para cada carta enviada ao Cemitério, causa 300 de dano ao **dono daquela carta**. Rain of Mercy aumenta LP de ambos em 1000. Estes textos são **hipóteses externas, não contratos nativos do Joey**.
- Novos `windows(1).json`/`.md` com a captura nativa íntegra existente mostram Rain seq CF 335 e 1191, pares `0x42` de +1000 por lado, e deltas observados entre amostras `[+1000,+1000]` em ambos os casos. As amostras anteriores estão 21/22 comandos antes do CF. Um dano de 300 no lado 0 ocorre depois; sem campo/cemitério observados, a causa permanece **não atribuída**.
- Adicionado `docs/research/card-text-expectations.v1.json` com regras esperadas em paráfrase/fonte oficial e interação `Unverified`, 
  `yugilab/card_text_expectations.py` que valida identidades e cruza somente CFs temporalmente, e opção `yugilab.discovery analyze --card-text-expectations ...` para enriquecer relatório sem alterar engine ou inferir ativação.
- Testes sintéticos `yugilab/tests/test_card_text_expectations.py`; **não executados neste ambiente** por ausência de checkout acessível. Rodar no Windows, conferir identidade 969/1315 e 414/1306 e gerar relatório novo sem sobrescrever o antigo.
- Próximo trabalho da trilha nativa: comprovar presença ativa de Skull Invitation e movimento específico de Rain ao Cemitério antes de atribuir dano. Priorizar referência textual da versão realmente embutida no jogo se obtida e verificada.
