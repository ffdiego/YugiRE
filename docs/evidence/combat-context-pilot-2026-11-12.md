# S3 — primeira captura combat-context: correspondência escalar e lacunas

## Resultado

A revisão independente confirma **64/64 saídas de quatro bytes** em uma
reprodução Python da fórmula F40, alimentada por atributos e predicados nativos.
Isso é correspondência **condicionada às flags amostradas na entrada**; não é
cálculo independente de atributos, replay do seletor nem prova de ausência de
modificadores. Existem 64 pares estruturalmente íntegros; não promovemos nenhum
para contexto semântico estrito plenamente observado nesta revisão.

Os dados são utilizáveis como fixtures de leitura/correlação e diagnósticos
escalares com condições explícitas. **Ainda não são seguros como fixtures que
atestem `IsResolved` global ou equivalência end-to-end do Joey.Lab.**

## Proveniência e reprodução

Captura original privada: `commands-20261112-015214-332209-pid10848.jsonl`, em
`scratch/captures/`. SHA256 fornecido pelo operador e recalculado antes da análise:
`424cb09a6d990dbfb0e8795cf97d0260f644f0c2349980937e0b96d14831213f`.
Arquivo aberto somente para leitura; não foi normalizado nem regravado.

O EXE local tem o SHA256 esperado
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
**O JSONL contém `exeSha256: null`.** O hash do EXE local não atesta o módulo
usado no PID 10848. O operador declarou nesta revisão: “não há anomalias” (contexto declarado,
não medição do hook). Não foi fornecido log de preflight ligado ao PID; o relato
não especificou separadamente o encerramento normal do processo.

```bash
python3 scripts/audit_combat_context.py \
  scratch/captures/commands-20261112-015214-332209-pid10848.jsonl \
  --expected-sha256 424cb09a6d990dbfb0e8795cf97d0260f644f0c2349980937e0b96d14831213f
python3 -m unittest discover -s scripts/tests -p test_audit_combat_context.py
```

O [resumo reproduzível](combat-context-pilot-2026-11-12.audit.json) contém os IDs
call/selector e seq de todos os pares. A [amostra mínima](combat-context-pilot-2026-11-12.fixture.json)
contém somente três casos com buffers e retornos observados, sem endereços de
stack, para revisão/ingestão. Está qualificada como `diagnostic-only`, com lacunas
explícitas. O trace bruto não foi versionado. Nenhum arquivo da trilha T foi alterado.

Fontes estáticas: [F40 decompilada](ai-decision-combat-deep.txt),
[assembly](ai-combat-outcome-arithmetic.txt),
[conversão de recursos](card-resource-loader.txt),
[contrato S1](../research/ai-combat-observation-contract.md).
Conferência local adicional: `objdump -d -Mintel --start-address=0x5b1195
--stop-address=0x5b1247 ../passion/joey_pc.exe`. Não executa o jogo.

## Integridade observada

| Observação | Resultado |
| --- | --- |
| Eventos após metadata | 1285, seq contígua 1..1285; seen=retained=1285; overwritten=0 |
| Seletores | 22 pares, sem janela aberta ao fim |
| F40 | 64 enter/leave, filhos dentro do seletor e stack correta no mesmo thread 22400 |
| Atributos | 64 own_stats + 64 opponent_stats; argumentos side/slot consistentes |
| Consultas | 210 resource; 64 equipment_count; 72 context_count; 45 field_query; 0 phase_query |
| Erros/perdas registrados | Nenhum readError, readErrors não vazio, overflow, erro de correlação ou filho órfão |
| Limite | marcador seq 971; último par callId 716 em seq 958..970 |
| Janela de comandos | opcode 0014 seq 1; opcode terminal 0004 seq 1285; duração observada ~241,3 s |

O terminal observado não substitui relato sobre encerramento normal do processo.
A captura continua depois do limite, mas não contém novos contextos: 64 não é o
número de avaliações F40 de todo o duelo. Os filhos completos têm quantidade,
ordem, VA, caller e argumentos esperados; remover um predicado requerido faz a
auditoria falhar antes da comparação de saída. Saída esperada não escolhe inputs.

Os 64 casos se concentram em sete índices de atacante: 24, 591, 147, 515, 151,
1052 e 455. Há apenas 23 combinações de buffers próprios/rivais, postura, primeira
palavra do seed e flags de jogador (ignorando slots e caminho de consultas).
Essa contagem não é cobertura de regras nem deduplicação universal do contexto.
Não há exceção intrínseca F40 01DD/04E5/0199/04CF/04E3/053D como atacante neste corpus.
Metalmorph retorna zero em todos os 64; os 72 context_count também retornam zero.

## Ausência de predicados: reconstrução do caminho

As chamadas são consumidas em ordem e verificadas contra as entradas, sem
inventar retorno para uma função que não executou. Os contadores abaixo se
referem a direções ou chamadas, não a conjuntos disjuntos de confrontos.

| Condição estática | Ausência legítima | O que aconteceu na captura |
| --- | --- | --- |
| Não venceu um defensor em defesa | Todos os quatro D8D0 dispensados | 46 confrontos; demais 18 executam os quatro, todos zero |
| Atacante recurso 053D e venceu defesa | Duas consultas de perfuração para ganho dispensadas | Não coberto |
| Primeira D8D0(521) não zero | D8D0(604) da mesma direção dispensada | Não coberto; observar OR, não supor segunda contagem zero |
| gain <= 0 após proteção | A0/220 para dano ao defensor dispensados | 62 confrontos |
| loss <= 0 após proteção | A0/220 para dano ao atacante dispensados | 21 confrontos |
| A0(58F) <= 0 | B220(14D) daquela direção dispensada | 2 consultas de ganho + 43 de perda retornam zero; explicam todas as ausências de phase_query |

Os demais ramos dependem da ordem nativa: bônus intrínsecos → Metalmorph →
ATK/ATK ou ATK/DEF → proteção → supressão → overrides por carta → score int16 e
utilidade. A ausência de um predicado não vira `false` de campo. Pode virar
`NotEvaluated` com razão, ou uma expressão composta pode ficar resolvida para
**aquele caminho** (por exemplo, `A0<=0 AND B220` é falso independentemente de B220).
Ausência inesperada, caller errado ou filho extra é `Incomplete`/erro de contrato,
mesmo se alguma escolha de defaults reproduzisse os quatro bytes.

Exemplos vinculados ao arquivo por SHA acima:

| callId / seq | Entrada efetiva e fato observado | Saída |
| --- | --- | --- |
| 2 / 29..37 | 24 contra 550 revelado em ataque; ATK 1700 vs 1900; field_query de perda seq 35 retorna 0 | 000438FF |
| 26 / 56..64 | 515 contra 550 revelado em ataque; field_query de ganho retorna 0 | 0808C800 |
| 78 / 202..214 | 591 contra slot oculto em defesa; quatro context_count naturais zero, ganho/perda não positivos | 08080000 |
| 716 / 958..970 | 515 contra slot oculto; último par anterior ao limite | 48080000 |

## Completude semântica

**Stats e identidades.** Sete rivais são revelados, 57 ocultos. Nos ocultos,
ADDC0 escreve `00000000E8030000E8030000`: índice/tipo/atributo zerados e ATK/DEF
estimados 1000/1000. O índice real no snapshot continua distinto. O retorno de
004022E0 no callsite 005B1009 é `low16=FFFF` nesses 57 casos. Ele representa o
recurso **efetivo da avaliação**, não o recurso real do monstro oculto.

A conversão recebe na stack o DWORD completo do cabeçalho de stats; sua ABI
semântica usa `ushort`, conforme o decompilado. Exemplo seq 32: argumento raw
84869144 (=050F0018), retorno raw 14483533, recurso low16 004D. Não tratar o raw
inteiro como índice nem o EAX inteiro como ID; os bits altos de retorno não são
parte do resultado ushort. A auditoria compara o header completo do argumento
com o buffer, mas conserva índice, recurso efetivo e slot separadamente.

O modelo atual `NativeAiMonster.OpponentStats` volta a estimar ocultos usando
`Level`. Alimentá-lo com o buffer já estimado e deixar `Level=4` por default
seria uma segunda estimativa sem proveniência; não é adaptação válida só porque
estes 57 buffers têm 1000. T2/T3 precisam distinguir `AfterADD60/AfterADDC0` de
atributos reais e preservar a observação de visibilidade sem fingir face-up.
Não modificamos Joey.Core nem o adaptador da outra trilha.

**Instante das flags.** O primeiro campo essencial ainda sem observação no
instante de consumo é o bit 00400000 de jogador: o hook o lê ao entrar na F40;
o nativo lê em 005B1195 e 005B11C6, depois dos produtores e consultas naturais.
Todas as amostras de entrada têm esse bit limpo, mas não há amostra consumida
nem prova estática completa de que todas as chamadas intermediárias preservam
esse campo. A concordância da saída não prova estabilidade.

Há restrições inferíveis pelo caminho: nos 45 confrontos com consulta de dano
positiva em andamento, a execução natural dessa consulta testemunha que a
proteção correspondente não zerou aquele dano; no empate em defesa as proteções
não alteram resultado. Isso é **inferência a partir do caminho observado**, não
nova leitura do campo. Nos 18 sucessos contra defesa sem perfuração, a proteção
do defensor pode alterar o bit 0800 sem gerar consulta de dano. Não usamos os
outputs para escolher o valor ausente nem transformamos tal inferência em input
observado. A classificação estrita permanece conservadora para os 64.

**Atributos independentes.** Os stats são outputs nativos usados como inputs da
fórmula. O manifesto de modificadores, vínculos ativos e condições não foi
capturado. Não sabemos se um valor coincidente com base do DAT inclui efeitos
que se cancelam. As flags brutas de monstro não substituem esse manifesto.
A ausência dele impede provar o cálculo independente de atributos, mas não é
por si só um defeito de uma fixture explicitamente limitada à fórmula escalar.

| Escopo | Complete/match | Incomplete | Mismatch/unsupported |
| --- | --- | --- | --- |
| Estrutura e diagnóstico sob flags de entrada estáveis | 64 / 64 | 0 | 0 divergências diagnósticas |
| Contexto estrito com proveniência e instante de consumo atestados | 0 | 64 | Não executado no Joey.Lab |
| Cálculo independente de atributos/replay do seletor | 0 | Fora do escopo dos dados | Não demonstrado |

## Atestado ausente e correção de exportação

O host chama `authorizedeckobserver(checks['exe_sha256'])` após preflight. Porém
`traceStatus()` só expunha `deckFingerprint` pelo objeto de deck tracing; sem
`trace deck on`, esse objeto não existia. O exportador buscava exatamente essa
propriedade. Isso explica como um preflight bem-sucedido pode gerar metadata null,
mas não prova que ele ocorreu nesta execução particular.

Corrigimos a exportação futura: `hostAttestedExeSha256` vem do atestado armazenado
no agente, independentemente de deck tracing; metadata usa esse campo com fallback
legado. Sem autorização do host ele continua null. Testes JS e Python cobrem o
atestado sem deck probe. A correção não autentica retroativamente a captura.

## Próximo experimento que aumenta cobertura

Não aumentar simplesmente o número de duelos. Primeiro recolher log do preflight
associado ao PID/arquivo e confirmação do encerramento normal, sem editar o JSONL.
O relato de ausência de anomalias já foi recebido. No próximo
piloto, usar o exportador corrigido e armar o modo perto do cenário de interesse,
para os primeiros 64 casos não consumirem todo o orçamento em estimativas repetidas.

A prioridade discriminante é Goblin Attack Force (índice 307, recurso 0540,
2300/0) como alvo revelado: observar naturalmente antes/depois de seu ataque,
registrando postura efetiva F40, stats e contexto de preparação da IA. Incluir
atacantes 1800 e 2000 se surgirem naturalmente. Comparar ataque/defesa sem escolher
postura pelo resultado. Esta captura não contém índice 307 em nenhum dos 64 pares;
portanto não resolve os eventos históricos 304/319 e não demonstra o callback da carta.

Para fechar a lacuna temporal, propor revisão de uma captura passiva adicional
nos pontos efetivos de proteção: EAX após a carga em 005B1195 (próxima instrução
005B119B), e operando/condição da leitura em 005B11C6. É necessário validar offline
ABI/relocação e segurança de qualquer hook dentro da função antes de novo piloto;
esses pontos **não estão implementados nem aprovados** nesta entrega. Alternativa:
provar estaticamente a preservação das flags em todo o trecho e serviços chamados.
Amostras somente na saída da F40 não provam qual valor foi consumido.

Depois, um caso natural com MetalmorphCount>0, ou ganho contra defesa e D8D0>0,
ou A0(58F)>0 provocando B220, aumentaria a cobertura de ramos atualmente vazios.
Para independência de atributos, selecionar um par revelado do subconjunto fechado
(Axe Raider/Gemini Elf) e capturar um manifesto completo dos modificadores ativos,
com proveniência, antes de comparar stats calculados do DAT. Nenhum helper deve
ser chamado artificialmente para preencher lacunas.

Parada: revisão documental concluída; novas observações exigem operador Windows.
S4 continua bloqueada por falta de observação discriminante da carta 307.
