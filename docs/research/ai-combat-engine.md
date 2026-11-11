# Avaliador de combate da IA em C#

## Resultado e limites

A implementação produz atributos, eventos hipotéticos, sequências, score e
ranking **sem chamar o Joey nem receber scores nativos** no subconjunto
fechado de Axe Raider (índice interno24, recurso004D) e Gemini Elf
(índice550, recurso0213). Não está integrada ao DemoDuel: avaliação da IA e
regras que alteram o duelo permanecem separadas.

A implementação escalar aceita atributos já calculados e predicados de
campo resolvidos. O cálculo de atributos, os gates de campo e o seletor
completo só aceitam o subconjunto documentado. Recurso, vínculo ou condição
desconhecida retorna `IsSupported=false`, nunca efeito vazio ou score zero.
`Resolved()` é uma declaração do caller, não detecção automática de ausência
de efeitos.

Referência: EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços são VA, base00400000. Consultas Ghidra usam read-only/noanalysis.
Nenhum executável, DAT, save ou banco persistente foi modificado.

## Fronteiras e evidências

| VA | Implementação | Cobertura/confiança |
| --- | --- | --- |
| 005B0F40 | `NativeAiCombat.Evaluate` | Fórmula, buffers e exceções locais transcritos de assembly; compatibilidade dinâmica condicionada a inputs ausentes. |
| 005B0D90 | `NativeAiSequence.SelectTarget` / `SpecialTarget` | Comparadores e formato; elegibilidade só no contrato fechado. |
| 005B0B90 | `OrderAttackers`, seleção por ordem | Sort por chave e troca não estável; testes sintéticos. |
| 005B0A50 | `NativeAiSequence.Evaluate` | Máscara, remoções locais, soma e contadores; testes sintéticos. |
| 005B08B0 / 005B09C0 | `Baseline` / `PrepareBaseline` | Preparação isolada de posições para dois recursos comuns. |
| 005ADD60 / 005ADDC0 | Stats próprios e `OpponentStats` | Visíveis explícitos; ocultos estimados1000/1900 e metadados zerados. Não são atributos reais de combate. |
| 0056DD30 | `NativeAiAttributes` | Propriedades DAT e trecho final; não resolve todos os efeitos intrínsecos/vínculos. |
| 0056FD40 / 005674E0 / 005BE050 | Deltas, aliases e half | Primitivos transcritos; oráculos Python independentes do código C#. |
| 005ADE40 / 0056B5A0 / 005B05A0 | Política de ameaça/posição | Somente gates resolvidos do subconjunto fechado. |
| 005B1360 | `NativeAiCandidateSearch.SelectNormal` | Inserção comum de nível4 e ranking; sem tributos, cartas especiais ou seletor prioritário. |

Fontes anteriores: [combate](../evidence/ai-decision-combat-deep.txt),
[seletores](../evidence/ai-decision-selectors-deep.txt),
[controle](../evidence/ai-decision-control-flow.txt) e
[modelo de decisões](ai-decision-engine.md).
Novas consultas dirigidas:
[aritmética](../evidence/ai-combat-outcome-arithmetic.txt),
[política](../evidence/ai-combat-plain-policy.txt),
[equipamentos](../evidence/ai-combat-equipment-deltas.txt),
[metadados base](../evidence/ai-combat-base-stat-metadata.txt) e
[atributo base](../evidence/ai-combat-base-attribute.txt).

## Um confronto: representação e matemática

`NativeAiStats` conserva exatamente12 bytes little-endian: índice interno
uint16, tipo byte, atributo byte, ATK int32, DEF int32. ID de recurso é
separado. `NativeAiCombatEvent` conserva quatro bytes: flags uint16 e score
int16. As flags identificam atacante (bits4..6), alvo (7..9), remoção própria
(0400), rival (0800), utilidade (0008), caminho especial (0002) e seleção pelo
loop externo (0004). O seed preserva bitsF005 na rota F40.

ATK/ATK, ATK/DEF, empate positivo e empate zero seguem os ramos nativos.
Destruição hipotética não é aplicação de dano real. O cálculo usa operações
int32 com wrap; o score final é `int16(gain-loss)`. Utilidade é score positivo
**ou** remoção rival sem remoção própria; portanto score não positivo pode
ser útil. As exceções de recursos01DD/04E5/011F/053D/0199/04CF/04E3 seguem a
ordem nativa, inclusive overrides após proteção/supressão.

Metalmorph, perfuração, proteção e supressão são predicados fornecidos
explicitamente. Não são descobertos pela classe escalar. `NativeHalf(x)` é
`unchecked(x*5+5)/10`, divisão assinada truncada; não é uma aproximação float.
Boost04E5 do rival opera em DWORD e o threshold04E3 usa comparação assinada.
Testes extremos preservam overflow e truncamento, mesmo fora dos ATK usuais.

O caminho especial B0D90 conserva seu próprio formato: score é ATK truncado
int16 e retorno é score>0, sem recomputar utilidade. Só pode ser usado após
os gates BF20/BFD0/B05A0 apropriados. Na sequência fechada é permitido quando
o rival não tem monstros e a política de ataque passa.

## Atributos: contrato resolvido

`FromPlainCard` deriva do catálogo DAT o ATK/DEF, tipo e atributo dos dois
monstros suportados. O getter00402650 retorna `propertyWord>>29` para esses
tipos. O manifesto de modificadores deve enumerar todos os equipamentos
já elegíveis e as contagens de Shield & Sword/Mirror Wall; supressão e sinal
dos deltas também devem estar resolvidos. Descobrir elegibilidade de links
continua sendo trabalho pendente.

A ordem final DD30 é swap → deltas int32 → clamp0 → halves repetidos.
Não há segundo clamp depois dos halves. A normalização de recursos subtrai
2000 uma vez; os cinco grupos de aliases permanecem separados de índices
internos. Equipamento desconhecido rejeita o cálculo inteiro.

Os testes confrontam70 casos de deltas e28 vetores de half com helpers Python
existentes. São oráculos derivados de análise estática, não capturas de98
confrontos. Dois blocos de stats calculados do DAT concordam byte a byte
com buffers nativos23/24 sob manifesto vazio declarado; a captura não prova
que todos os modificadores realmente eram ausentes.

## Sequência e baseline isolados

`NativeAiField` possui cinco zonas por lado e copia dados em cada alteração.
`ResolvedPlain` exige:

- Somente os dois recursos suportados, com identidade interna válida.
- Flags nativas de cada monstro conhecidas; null não é zero. Bit20000 trava
  posição, bit40000 bloqueia ataque. Override contraditório é recusado.
- LP e flags de jogador explícitos, sem gates/efeitos omitidos.
- Atributos que permanecem válidos após remoções e nas posturas usadas,
  sem modificadores não modelados dependentes de campo/visibilidade.
- Para candidatos: zonas vazias sem metadados residuais e sem modo especial
  de posicionamento A577EC.

Pseudocódigo do recorte suportado:

```text
baseline(field, side):                          // 005B08B0 / 005B09C0
    local = copy(field)
    preparar posturas permitidas em local
    return evaluate(local, side)

evaluate(field, side):                          // 005B0A50
    state = copy(field); mask = 0; score = 0
    até cinco eventos:
        attackers = ocupados revelados em ataque, elegíveis e fora de mask
        selection-sort por (classe << 16) | (ATK & 65535) // 005B0B90
        para cada atacante nessa ordem:
            testar alvos/política e avaliar F40             // 005B0D90
            preferir útil com maior score int16
            empate: novo oculto vence; senão maior DEF calculada vence
            parar no primeiro atacante com proposta útil
        se nenhum: terminar
        marcar evento escolhido, mask e somar score assinado
        remover identidades destruídas do estado local
        recomputar lista na próxima iteração
    empacotar perdas; obter sobreviventes para o wrapper de ranking

select_normal(field, full_hand, pass):           // recorte 005B1360
    bestScore, bestLosses = baseline(field)
    best = -1
    para cada carta na ordem original da mão:
        exigir carta/condições suportadas; rejeitar desconhecido como unsupported
        escolher primeira zona vazia permitida
        pass1 exclui níveis1..4
        preparar cópia original e inserir candidato revelado em ataque
        result = evaluate(cópia, side)
        exigir ATK>=900 e bit do candidato em result.mask
        aceitar score maior; no empate, mais perdas rivais com sobrevivente próprio
        se aceito: atualizar bestScore, bestLosses e best
    return best
```

A chave tem classe1 para05F3, classe3 para02F9/0226/02E8, classe2 para os
demais. Essas constantes no primitivo de sort não tornam tais cartas
suportadas no campo. O sort troca elementos e pode inverter empates.
O desempate usa visibilidade do **novo** alvo e DEF real calculada do alvo
anterior, inclusive quando este era oculto.

B05A0 é política da IA, distinta de legalidade. Quando B5A0 permite mudar
posição, compara outras ameaças com ATK+1000. ADE40 usa B7D0, inclui defesa
ou ocultos e ignora a máscara de atacantes já usados. Flags de monstro e LP
filtram ameaças; bloqueio global de batalha do jogador rival não as apaga.

`ScoreSummary` conserva bits altos antigos do bufferAA, como A50.
`RankingSummary` preenche sobreviventes, como o wrapper de candidato.
Flags de jogador são a fonte autoritativa de proteção; o seed nesta rota é
side0/1. A soma inclui apenas eventos selecionados.

## Integração com candidatos

`NativeAiCandidateSearch.FromCatalog` produz atributos em C#; `SelectNormal`
recebe a mão inteira com índices contíguos e calcula baseline e cada hipótese
antes de chamar `NativeAiRanking`. Um candidato desconhecido torna a busca
inteira unsupported: não se retorna um melhor parcial como se fosse decisão
completa. Passes0/-1 compartilham o recorte comum; pass1 filtra níveis baixos.

Os13 checks do componente incluem mão ordenada, empate, máscara, zona
permitida, ATK mínimo, baseline com monstro existente, contextos desconhecidos
e duas verificações DAT → atributos → hipótese → score → ranking.
São casos sintéticos fundamentados no fluxo estático, **não** uma nova
validação dinâmica de select_normal. Os15/15 candidatos e7/7 retornos já
validados continuam usando métricas capturadas nos testes anteriores.
Tributos, select_priority e atributos gerais permanecem fora do recorte.

## Corpus diferencial e proveniência

`build_ai_combat_corpus.py` verifica hash e integridade da captura, pareia
stats DD30 por thread/intervalo de callId e não usa outputs para inventar
posição ou efeitos. O fixture versionado contém22 eventos F40,14 com ambos
os buffers de stats e oito incompletos. A captura disponível é
`commands-20261111-022940-355228-pid36348.jsonl`, SHA256
`dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90`.

Um único cenário declarado para todos os14 pares (ataque revelado, nenhum
modificador externo, seed0) reproduz score e bits calculados0FFA em12:
25,111,123,131,157,162,179,184,193,310,822,1029. Eventos304/319 divergem.
Não ajustamos a posição/efeitos para fazê-los concordar. **Zero contextos
completos foram capturados**: isso é compatibilidade condicional, não replay
integral. Os JSONL recentes de gates/detail não estão locais; seus relatórios
e fixtures continuam sendo a fonte para a validação anterior do ranking.

## Executável independente e verificação

Da raiz do repositório:

```bash
dotnet run --project src/tools/Joey.Combat -- \
  ../passion/data.dat 550 24 attack --declared-neutral
```

Os números são índices internos. O CLI verifica o SHA do DAT, calcula stats
e imprime evento e sequência. O resultado esperado é0808C800, evento
selecionado0C08C800, score200, mask1. `defense` troca a postura do rival.
`--declared-neutral` é obrigatório.307/Goblin Attack Force é recusado por
atributos/regras ainda não suportados. Não há janela nem execução do Joey.

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

## Próxima evidência necessária

O avanço de maior valor é um snapshot passivo **antes da preparação do
baseline** e os inputs completos de F40 nos confrontos304/319:

1. Entrada005B08B0/005B1360: side, pass, mão, campos completos dos dois lados,
   LP, flags, palavras de posição, máscara de zonas e estado de duelo.
2. Entrada005B0F40: slots, descritor antes da escrita, posição do defensor,
   flags de jogador e buffers produzidos por ADD60/ADDC0 nesta chamada.
3. Retornos de D790/D8D0/699A0/B220 consultados dentro dessa mesma chamada,
   com argumentos e vínculo por thread/callId. Não chamar esses helpers fora
   do fluxo natural. Saída F40 e agregação A50 para comparação independente.

Os endereços e ABI já investigados estão nas evidências referidas acima e
no laboratório existente; não foi acrescentada instrumentação Windows.
Na falta desses inputs, expandir a descoberta de atributos/gates com análise
estática e manter casos sem contexto como inconclusivos. A fronteira de um
harness nativo foi avaliada em [headless-feasibility.md](headless-feasibility.md);
não mostrou vantagem sobre continuar o port C#.
