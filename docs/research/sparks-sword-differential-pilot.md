# Piloto diferencial mínimo — Sparks e Legendary Sword

Preparação offline a partir de 2523483. Nenhuma execução/injeção/hook/captura
nesta tarefa. Não há scratch/captures neste checkout. Os resumos reais existentes
não contêm duas sequências completas destas cartas; não substituem os originais.
Não se alteram anotações de cobertura nem suporte de cartas.

## Infraestrutura revisada

| Componente | Reutilização | Limite relevante |
| --- | --- | --- |
| Joey.Lab CaptureReader/CaptureAnalyzer | Hash, JSONL, sequências, contagens, perda e buffers; usados para fontes `commands` | Não certificam causalidade ou contexto completo; buffers IA podem ser hipotéticos |
| Joey.Lab compare / RunComparison | Comparação de duelos seeded, argumentos raw/low16 e integridade | Compara capturas entre si, não uma regra C#; igualdade de comandos não é paridade |
| Joey.Lab experiment → yugilab | Host, preflight, logs, manifesto, captura e encerramento de PID próprio já validados em piloto Windows | Spawn, hooks, alterações de seed/deck/velocidade; não adequado ao primeiro piloto natural sem nova autorização |
| native_rules.py --observe-only + trace | Produtor005B9170, comandos, caller/thread/seq e lifePointsObserved | Sem DLL, mas Interceptor altera prólogo em memória; exige nova autorização. LP amostrado **antes de enfileirar** o comando atual |
| joeytrace.py --card-events | ReadProcessMemory externo; mãos, LP, campo11 slots e associações ativas; manifest/snapshots/markers | Não atômico; não conhece cemitério, stats efetivos ou callbacks. Slots5..10 são other_field, sem mapear automaticamente para zonas de magia |
| trace ai on / combat-context | Há validações reais e leitor C# de contexto | Stats de hipótese/estimativa de IA não são stats reais do campo. Não habilitar para preencher esta lacuna |
| DirectDamageRules, EquipmentRules, NativeAiAttributes e DemoDuel | Predições C# existentes no novo comparador | Guardas externas e política de sandbox devem ser declaradas; não são observação |

Referências: [LP real](duel-life-points-capture.md), [JoeyTrace](../30-joeytrace.md),
[piloto Lab real](../evidence/joey-lab-experiment-windows-pilot-2026-11-12.md),
[limites do combat-context](../evidence/combat-context-pilot-2026-11-12.md),
[políticas P025](../cards/p025-implementation.md), [P045](../cards/p045-direct-damage.md).
A captura combat-context histórica tem hash de EXE ausente no JSONL: não atestar
esse PID usando o hash do executável local. Mesmo problema deve ficar Inconclusive.

## Cenários reproduzíveis por estado, não por seed

Repetir um cenário apenas quando o estado inicial abaixo for atingido por jogo
normal. Registrar PID, hash do EXE, lado nativo (CPU0/jogador1), segmento, posições,
identidade/occurrence da carta, marcas e instantes. A mesma seed não foi demonstrada
para a partida manual; não prometer a mesma ordem de compras. Não alterar deck,
save, seed ou velocidade por console/automação. Usar cartas já disponíveis ao operador.

**Sparks:** duelo local, fase principal, Sparks índice755/recurso342 na mão do
jogador1, zona de magia livre, adversário vivo com LP>200. Anotar os LP reais
iniciais dos dois lados (não assumir8000), ambas as mãos/campo e ausência de
outra ação na janela. Marcar S-before, ativar **uma** Sparks pela interface normal,
aguardar animação/LP estabilizarem, marcar S-after. Observar aceitação da ativação,
alvo preparado/comando quando disponíveis, quantidade, LP antes/depois,
saída da mesma ocorrência da mão e presença no cemitério por interface normal.
Não concluir cemitério somente pela ausência na mão. Não usar o comando console
`sparks`, que chama uma rotina em vez de observar ativação natural.

**Legendary Sword:** duelo local em fase principal, Sword índice614/recurso300
na mão do jogador1; Axe Raider índice24/recurso77 face-up em alvo conhecido,
sem equipamento anterior, remapeamento, campo ou efeito global conhecido.
Registrar ATK/DEF **reais exibidos** antes; tipo dinâmico15 e guarda genérica só
ficam Observed se houver evidência independente desses predicados. Marcar E-before,
ativar Sword normalmente, selecionar exatamente o alvo e registrar E-linked após
estabilizar. Conferir associação tipo1 no alvo, fonte ocupada, identidade/occurrence,
ATK/DEF após (+300/+300 na hipótese neutra). Depois, na mesma partida, retirar
somente Sword usando De-Spell ou Mystical Space Typhoon pela interface original,
se disponível legitimamente; manter o monstro no campo. Marcar E-removed e verificar
fonte vazia, desaparecimento da associação ativa e carta no cemitério.
O efeito que causa a retirada é **estímulo nativo**, não suporte novo na engine:
compara-se somente o pós-estado de retirada comum via Demo.RemoveEquipment.
Se não houver retirada natural disponível, encerrar com campos de remoção ausentes.
Não invocar helpers, não adicionar cartas por console e não fabricar esse estágio.

Os dois cenários são sessões curtas separadas, uma ocorrência de cada inicialmente.
Se a CPU agir entre amostras ou houver outra mudança de LP/campo, a janela não é
isolada: preservar dados e marcar Inconclusive, não escolher o próximo valor que bate.

## Campos mínimos e pontos de coleta

Todos os campos incluem origem, hash do arquivo e localizador; valores ausentes
permanecem null. `inputs` alimentam o C#; `observed` são resultados independentes.
Não usar o resultado nativo depois do efeito para resolver um input desconhecido.

| Cenário / campos | Coleta e semântica |
| --- | --- |
| Ambos: sourceResourceId/sourceInternalIndex/sourcePlayer, exeSha256, windowIsolated | Identidade por índice RAM+catálogo pinado, ocorrência, lado; hash ligado ao PID/captura; revisão da janela inteira. Marca temporal sozinha não confirma causalidade |
| Sparks: requestFlags, preparedTarget | Pedido+4/+6 no instante correto. Trace de produtor atual **não lê esses campos**: Missing/Assumed, não flags0 inferido pelo sucesso |
| Sparks: lifeBefore0/1; observed lifeAfter0/1 | LP de snapshots/trace. No evento43, LP ainda pode ser anterior; usar amostra posterior à execução, sem ações intermediárias. Próximo comando cronológico não basta |
| Sparks: observed preparedTarget/commandTarget/damageAmount | Preparação593CC0; comando43/8043 e argumentos amount,0,1. Trace fornece args e bit15; buffer de pedido ainda é lacuna. Não inferir alvo preparado do LP que caiu |
| Sparks: activationAccepted, sourceInHandAfter, sourceInGraveyardAfter | Ação UI+mudança RAM/visual por ocorrência; cemitério explicitamente observado. `sandboxApplicable` declara fase principal/zona livre/sem cadeia, não legalidade nativa integral |
| Sword: targetPlayer/targetZone/targetInternalIndex/targetResourceId/faceUp | Alvo exato, ocorrência e posição. Índice24/recurso77 não são zona24/77 |
| Sword: dynamicType/dynamicAttribute/genericGuardsSatisfied | Predicados no instante de resolução; ClassCode estático e ausência visual de efeitos não os observam. Guardas genéricas ainda não têm captura dedicada |
| Sword: attackBefore/defenseBefore; observed attackAfter/defenseAfter | Stats reais antes/depois no mesmo alvo. Polling atual não os decodifica; registrar UI com instantes ou futura observação passiva autorizada; não usar estimativas/buffers hipotéticos de IA |
| Sword: activationAccepted/linkCountAfter/linkOwner/linkSourceZone/linkTargetPlayer/linkTargetZone/equipmentPresentAfter | Conferir vínculo novo tipo1 e coordenadas. Contar somente associações tipo1 que apontam para esta fonte, no prefixo ativo; não confundir com o contador de todos os tipos nem contar bytes residuais além dele. Modelo tem zona lógica0 (primeira livre); coordenada física nativa5..10 não é zona lógica por subtração presumida |
| Sword: equipmentRemovalConfirmed/linkCountAfterRemoval/equipmentPresentAfterRemoval/equipmentInGraveyardAfterRemoval | Retirada efetiva por causa documentada; comparar helper de limpeza do sandbox, não causalidade do estímulo. Não deduzir cemitério de slot vazio |
| Sword: noOtherModifiers, sandboxApplicable | Declaração de aplicabilidade ao subconjunto fechado. Se ausência de modificadores/guardas não foi observada, usar Assumed: predição condicional, campos dependentes Inconclusive |

Os instrumentos atuais permitem um piloto **parcial**. Faltam pedido Sparks,
resolução das guardas e propriedades dinâmicas, stats efetivos e cemitério em RAM.
UI/filmagem/registro do operador podem fornecer observações visuais delimitadas,
mas não convertem predicados internos não medidos em fatos. Não preparar novos
hooks nesta tarefa. Se o piloto exigir esses campos para fechar o caso, parar e
propor contrato separado de observação para autorização futura.

## Formato do dossiê e comparador

`Joey.Lab effect-pilot recipe.json` lê um documento `pilotVersion:1`, independente
do schema v1 do Card Coverage Registry. `sources` lista id/path/format/sha256;
paths relativos à receita. Formatos: json, jsonl e commands. `commands` reutiliza
CaptureReader/Analyzer, exige hash do EXE no cabeçalho, contadores sem perda e
sequência íntegra. JSON/JSONL genéricos são extratos **revisados**, não certificados
pelo parser como estado nativo. Não há associação automática de sessões ou tempos.

Cada caso contém id, scenario (`Sparks` ou `LegendarySword`), evidenceKind
(`Native` ou `Synthetic`), inputs e observed. Campo observado referencia um
arquivo pinado, linha JSONL 1-based (linhas não vazias), e JSON pointer RFC6901:

```json
{"origin":"Observed","source":"trace","line":17,"pointer":"/lifePointsObserved/lifePoints/0"}
```

Não há valor literal de observação na receita. Para uma transformação necessária
(bit15, args[1], membership por ocorrência, mapeamento de coordenadas), produzir
um pequeno extrato auditado separado, com fórmula, hash/seq/linha do bruto,
timestamp e revisor. Referenciar o extrato, preservar o bruto. O comparador
confere hashes/seletores, **não** certifica a interpretação ou a honestidade das
etiquetas. Linkar vídeo/notas e seus hashes no extrato para leituras da UI.

Hipótese explícita:

```json
{"origin":"Assumed","value":15,"reason":"classe estática usada pelo sandbox; tipo dinâmico não capturado"}
```

Ausência: omitir ou usar `{"origin":"Missing"}`. Fonte/hash/ponteiro inválido,
null, campo incompleto ou hipótese em dependência => Inconclusive por campo.
Um expected pode aparecer sob hipótese para diagnóstico, mas não vira Match.
EXE/pedido/identidade desconhecidos não recebem defaults de aprovação.
Janela não isolada bloqueia a comparação, sem deduzir causalidade da proximidade.

Predições: DirectDamageRules para alvo/quantidade, Demo.Play para transição LP e
movimento no sandbox; EquipmentRules para elegibilidade, EquipmentDelta e
ApplyResolvedTail para stats, Demo.Equip/RemoveEquipment para vínculo e limpeza.
SwordDemo é projeção de uma carta/alvo e primeira zona lógica livre0, não replay
integral da partida. Uma incompatibilidade de escopo é Inconclusive.
Resultados independentes por campo podem divergir (ex.: comando bate, LP difere).
Caso: Mismatch se algum campo diverge, senão Inconclusive se algum incompleto,
senão Match. Match é correspondência escalar sob dossiê revisado, não paridade.
CLI: 0=Match, 1=Mismatch, 2=receita inválida, 3=Inconclusive.
Saída determinística, sem timestamp/path absoluto volátil e sem gravar cobertura.

Fixtures `effect-pilot-synthetic*` têm dataOrigin/evidenceKind Synthetic e servem
somente à infraestrutura. Relabeling de uma fonte declaradamente sintética como
Native é bloqueado. Isso não constitui autenticação de uma captura contra fraude.
O template nativo entregue está vazio de observações e deve retornar Inconclusive.

## Critérios futuros de promoção (sempre revisão explícita)

Não promover por este comando. Antes de propor ValidatedInScope, exigir identidade
EXE ligada ao PID, bruto pinado, integridade, proveniência individual, entradas
independentes completas no ponto correto, nenhuma hipótese necessária e janela
isolada/correlacionada. Repetir o escopo em pelo menos duas ocorrências independentes,
incluindo ambos os lados quando o escopo os incluir, conferir origem/occurrence,
comando e pós-estado, retirada e slot reutilizado. Acrescentar negativos/limites
apenas em escopo autorizado. Match de campos incompletos/sintéticos não promove
carta inteira. Inventário Open e validação integral Unknown permanecem preservados.

Roteiro executável Windows e ponto de parada no handoff native-sparks-sword-pilot.md.
