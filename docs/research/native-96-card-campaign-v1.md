# Campanha nativa de 96 cartas — 12 confrontos espelhados

**Estado:** 24 decks JSON e executor de capturas **preparados**, mas
nenhum destes duelos foi executado no `joey_pc.exe` por esta branch.
Decks verificam **identidade e inclusão**, não comportamento.
Este PR de pesquisa é empilhado sobre o PR #21 e não altera a `main`.

## Escopo concreto

Esta campanha responde à meta de investigar simultaneamente **50–100
cartas diferentes em muitos decks**, e **não** de colocar 50 cartas em
cada deck.

- **96 resource IDs únicos** do callback catalog, presentes no pack
  original Joey: 36 monstros, 36 magias e 24 armadilhas;
- **12 confrontos** (cohort-01..12). Cada confronto tem **8 cartas
  de interesse**: 3 monstros + 3 magias + 2 armadilhas;
- **3 cópias de cada carta de interesse em cada lado**, ou seja,
  24 cartas sob teste no Main Deck A **e** no Main Deck B;
- **16 cartas de controle** por lado (8 índices repetidos duas vezes),
  preservadas de demos anteriores. São controles históricos sem callback
  no catálogo; **ainda não são todos certificados como monstros
  normais/legais por atributo**;
- cada `main` tem exatamente **40 ocorrências**; índices internos
  (`internalIndex`, não resourceId), seções auxiliares vazias;
- mesma composição dos dois lados com ordem inicial diferente,
  embaralhamento determinístico para facilitar regressão;
- não reutiliza resource IDs entre os 12 confrontos.

Os arquivos são reproduzíveis via
`scripts/prepare_96_card_campaign.py`, sem EXE nem DAT. A versão
efetivamente utilizada deve ser a **versão commitada**, testada contra
o catálogo e `pack-index.json` no CI.

### Classes e evidência

A seleção foi **curada para investigação**, não são 96 cartas
implementadas nem 96 cartas com efeito nativo comprovado. A identidade
de uma carta no deck, a aceitação de seu override, um comando CF/CE
associado ao resource ID, uma alteração de estado correlacionada e
uma comparação diferencial de efeito são **níveis diferentes**.

Status inicial de TODAS as 96: `PlannedDeckExposureOnly`.
Não converter a `Observed`, `EffectConfirmed` ou `Verified`
sem provas. Efeitos podem não ser ativados pela IA; proteção,
counter traps, cartas com condição contextual e disponibilidade
de alvos devem ser avaliadas individualmente. Três cópias de alguma
carta podem não ser legais sob a lista de restrições normal; o
override demo não comprova legalidade do deck no jogo normal.

## Arquivos da campanha

`native_hooks/decks/mass-96/`:

- `campaign-plan.json`: 12 coortes, descrição da classe,
  identificadores internos, recurso, nome e callback por carta;
- `cohort-01-A.json` / `cohort-01-B.json` até
  `cohort-12-A.json` / `cohort-12-B.json`;
- total: **24 decks + 1 plano**.

A lista completa de 96 cartas, por coorte, consta do plano em
formato processável; não inserir manualmente cartas no editor do
jogo. O executor faz o swap em memória, conforme o suporte existente
do YugiLab. O executável original não é modificado no disco.

## Execução Windows: três níveis

Na raiz do worktree `research/native-96-card-campaign-v1`:

```powershell
git fetch origin
git switch research/native-96-card-campaign-v1

python -m unittest discover -s tests -p test_mass96_card_campaign.py -v
python scripts/run_96_card_campaign.py --cohort 1
```

O segundo comando é **somente uma prévia** (não inicia o jogo).
Verifique a lista em
`native_hooks/decks/mass-96/campaign-plan.json`.

### 1. Preflight: somente coorte 01, 3 duelos

```powershell
python scripts/run_96_card_campaign.py `
  --exe "..\passion\joey_pc.exe" `
  --python ".\native_hooks\.venv-frida\Scripts\python.exe" `
  --cohort 1 --duels-per-run 3 --repeats 1 `
  --speed-value 600 `
  --output-root ".\scratch\mass96-preflight-c01" --execute
```

**Não avançar** se houver falhas de processo, integridade JSONL,
`complete`, 3 terminais, `overwritten=0`, amostras sem baseline,
ou injeção de deck não verificada para os dois lados. O executor
confere automaticamente o SHA256 do EXE, o plano/arquivo de decks,
footer de captura, estado e registros nativos
`deck_override` com fase `verified` **para ambos os lados em cada
duelo**. Compara `actualIndices` com os baralhos esperados em
`metadata.deckPlans` e interrompe ao observar `mismatch`, `failed`
ou verificação ausente. O EXE esperado tem SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

### 2. Piloto completo: 36 duelos de 12 confrontos

Somente após o primeiro piloto satisfazer os gates:

```powershell
python scripts/run_96_card_campaign.py `
  --exe "..\passion\joey_pc.exe" `
  --python ".\native_hooks\.venv-frida\Scripts\python.exe" `
  --duels-per-run 3 --repeats 1 `
  --output-root ".\scratch\mass96-pilot-36" --execute
```

Isso faz **12 execuções supervisionadas, sequenciais**, 3 duelos em
cada, com `--trace-state --workers 1` e seeds distintas por
coorte. Cria pastas por coorte, logs e
`campaign-runs.json`; interrompe a campanha na primeira falha
de captura. Cada execução conserva seu JSONL bruto, footer
e mudanças de estado. Nenhum envio para servidores externos
ou repositório de arquivos `scratch/` é realizado.

### 3. Amostragem ampliada: 180 duelos

Somente depois de revisar as 12 coortes (identidades, integridade
e efeitos com chance real de ocorrer):

```powershell
python scripts/run_96_card_campaign.py `
  --exe "..\passion\joey_pc.exe" `
  --python ".\native_hooks\.venv-frida\Scripts\python.exe" `
  --duels-per-run 3 --repeats 5 --seed-offset 1000000 `
  --speed-value 2400 `
  --output-root ".\scratch\mass96-expanded-180" --execute
```

São 60 invocações independentes, não um único teste de 180
duelos em RAM. `--trace-state` segue limitado a três duelos,
um worker e sem `--trace-ai`, como o YugiLab exige.
Esse lote pode gerar **muitos arquivos JSONL grandes**: revisar
espaço em disco e CPU antes; interromper com Ctrl+C se necessário,
preservando os diretórios e logs criados. Não executar em paralelo
com outras instâncias do Joey ou serviços que usem os mesmos saves.

Se apenas coortes específicas apresentarem efeitos observáveis,
usar `--cohort 3 --cohort 5` no comando expandido para concentrar
os duelos. Pastas novas são obrigatórias: o executor rejeita
sobrescrever um output root existente.

## Análise e critérios para progresso

A primeira avaliação é por **coorte e por recurso**:

| Estado | Critério |
| --- | --- |
| Planned | ID/classe/pack conferidos; aparece 3x por lado em JSON |
| DeckVerified | `deck_override/verified` observado para ambos os lados nos duelos |
| ResourceCommandObserved | CF/CE associado, após verificar provenance do comando |
| StateCorrelated | Estado antes/depois compatível, com janela e advertência de polling não atômico |
| NativeEffectSupported | Evidência causal restrita (LP/campo/mão/GY) com controles adequados |
| CoreParity | Mesmo estado e evento produzem mesmo efeito no C#, com comparação validada |

`DeckVerified` sozinho não comprova que a CPU comprou,
colocou, ativou ou resolveu a carta. **Nenhuma contagem de 96
deve ser mostrada como número de efeitos reproduzidos**.
O `Joey.Lab` e ferramentas `yugilab.discovery`,
`yugilab.state_interactions` e
`yugilab.inspect_effect_windows` podem analisar as capturas
por coorte depois de verificados os dados nativos
`registry.json`/`attributes.json` do Windows.

Para compartilhar resultados, faça ZIP das pastas de captura
e `campaign-runs.json` (idealmente o piloto completo de 36).
Não versionar capturas privadas ou gigantes em Git. Se houver
falha no piloto, envie primeiro o ZIP e interrompa a ampliação;
a ausência de atividade por carta não é falha do experimento.

## Quando parar de acrescentar cartas

Depois de mapear **quais** dentre as 96 produziram mudanças
de estado atribuíveis, priorizar: efeitos comuns não representados
no C#, casos de divergência e o motor de chains LIFO.
A taxa `NativeEffectSupported` não é uma porcentagem da
engine completa; será cobertura de **evidências amostradas neste
conjunto de 96 cartas**, com denominador fixo e explicação.
