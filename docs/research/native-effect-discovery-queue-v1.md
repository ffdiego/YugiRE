# Descoberta de efeitos por cobertura — laboratório nativo v1

## Resultado da primeira rodada Windows e próximo alvo (16/11/2026)

A captura de round-01 (um worker; sem trace-ai) contém **2.159 eventos**
e **3 terminais íntegros**, com **0 problemas** de integridade.
Ambos os decks foram verificados 3/3 vezes; **80 identidades** verificadas.
O relatório classificou **52 resource IDs com comandos CF/CE**:
**14 identidades CF**, 38 identidades CE (estes últimos podem ser
simples posicionamentos). Há 23 ocorrências CF e 54 CE no catálogo
observado. Foram recuperados 1 caso de Raimei com delta LP P045 exato.

**Próxima pergunta de pesquisa:** Rain of Mercy, índice interno 969,
resource ID 1315, com 2 CF (`seq` 335 no duelo 1 e 1191 no duelo 2).
A regra textual TCG afirma +1.000 LP para os dois lados, mas a
implementação do Joey original precisa ser **observada independentemente**.
Como o relatório não contém comandos vizinhos nem snapshots de LP, não
promover o efeito; executar o extrator OFFLINE sobre a captura já salva:

```powershell
git pull --ff-only
python -m unittest discover -s yugilab/tests -p test_inspect_effect_windows.py -v
python -m yugilab.inspect_effect_windows `
  --input .\scratch\native-discovery-round01-3 `
  --resource-ids 1315 --before 12 --after 16 `
  --output-dir .\scratch\rain-of-mercy-windows-v1
```

Enviar somente `windows.md` e `windows.json` para análise do caso;
não é necessário carregar o EXE, DAT ou JSONL inteiro. O extrator
verifica integridade com o auditor existente, não altera captura nem
liga hooks. Reporta mudanças entre amostras observadas **sem assumir
que foram causadas por Rain of Mercy**. Se nenhuma mudança explicável
aparecer, investigação nativa de callback/guardas e novas capturas
controladas vêm antes de alegar implementação. A segunda prioridade
é Tribute to The Doomed (índice 363, resource ID 1023, 3 CF);
esse efeito exige observar campo e descarte, não apenas LP.

## Análise das janelas Rain of Mercy — 16/11/2026

O operador enviou `windows.json`/`windows.md` da captura íntegra
`179bab9f66660e1c546a66e717ad37c1a2575784e0551e71fb1d82ebba8292a0`.
Duas ocorrências CF do resource ID 1315 (seq 335 e 1191) exibem
**dois comandos 0x42** em seq CF+3 e CF+4, de caller `0x578823`,
um para lado 0 e outro para lado 1, cada um com `amount=1000`.
Isso confirma a emissão de comandos de aumento de LP por lado,
mas **não comprova aplicação de +1000 nos LP**: as janelas originais
não continham amostra anterior ao par de comandos.

Após cada sequência há um comando `0x0043` com valor 300 e queda
exata observada de 300 LP no lado 0. Não atribuir esse dano à Rain.
Uma hipótese testável é Skull Invitation (resource ID 1306, CF
seq 66 no duelo 1 e 510 no duelo 2), cuja regra textual TCG
causa 300 por carta enviada ao cemitério. Não há prova nativa
de que a armadilha permanecia ativa ou de que o dano foi
produzido por ela. Ver evidência: 
`docs/evidence/rain-of-mercy-native-paired-lp42-2026-11-16.md`.

**Nova revisão offline do extrator**: reconhece pares de comandos
`0x42` com mesmo caller e thread, recolhe a última amostra de LP
anterior ao `CF` em todo o duelo e a primeira posterior ao par.
Exibe diferença entre amostras, distância e comandos LP intercalados,
sem atribuir causalidade. Rodar testes sintéticos e produzir
**um diretório novo**:

```powershell
git pull --ff-only
python -m unittest discover -s yugilab/tests -p test_inspect_effect_windows.py -v
python -m yugilab.inspect_effect_windows `
  --input .\scratch\native-discovery-round01-3 `
  --resource-ids 1315 --before 12 --after 16 `
  --output-dir .\scratch\rain-of-mercy-windows-v2
```

Esperamos duas `pairedLp42Candidate` com `commandSeq` iguais a
`[338,339]` e `[1194,1195]`. Os campos `beforeCfLpSample` e
`netLpDeltaBetweenSamples` são **desconhecidos até executar**
a nova revisão sobre a captura completa. Analisar `windows.json`
sem supor que uma diferença de +1000 por lado é automaticamente
causal: conferir distância, outras ações e lacunas da amostragem.

## Descrições das cartas como expectativas, não oráculo nativo (16/11/2026)

Até aqui o YugiLab não utilizava textos das cartas no ranking: apenas
identidade (índice interno / resource ID), propriedades, callbacks e eventos.
A primeira revisão de `docs/research/card-text-expectations.v1.json` adiciona
duas **paráfrases revisadas** com links oficiais do TCG contemporâneo:

- **Rain of Mercy (internal 969, resource 1315):** aumento de 1.000 LP
  para cada jogador quando o efeito resolve.
- **Skull Invitation (internal 414, resource 1306):** armadilha contínua
  que, enquanto ativa, inflige 300 ao dono de cada carta enviada ao Cemitério.

Os textos do TCG atual são **hipóteses externas**; o Joey original pode
ter regras/timing próprios. Não são material da engine nem evidência dinâmica.
O vínculo exige coincidência exata de índice interno, resource ID, nome
e presença no pack Joey. O relatório novo pode incorporar as descrições
e mostrar a hipótese Rain→cemitério→Skull como possível interação
sem confundir CF anterior com confirmação de permanência em campo.

Na captura anterior, Skull Invitation tem CF seq 66 e 510, e
Rain of Mercy tem CF seq 335 e 1191 nos mesmos duelos. Rain
mostra pares nativos de comandos +1000 para cada lado, com LP
líquidos +1000/+1000 entre amostras 21–22 comandos separadas.
Depois ocorre -300 LP no lado 0 nas duas janelas. **Ainda falta observar**
que Skull estava ativa e Rain foi para o cemitério.

Para alimentar o relatório de descoberta com as descrições revisadas:

```powershell
git pull --ff-only
python -m unittest discover -s yugilab/tests -p test_card_text_expectations.py -v
python -m unittest discover -s yugilab/tests -p test_discovery.py -v
python -m yugilab.discovery analyze `
  --registry .\scratch\coverage-current\registry.json `
  --attributes .\scratch\coverage-current\attributes.json `
  --input .\scratch\native-discovery-round01-3 `
  --card-text-expectations .\docs\research\card-text-expectations.v1.json `
  --output-dir .\scratch\native-discovery-text-v1
```

Verifique `textBasedInteractionHypotheses`,
`externalCardTextReferences` e `perCard[].externalCardTextExpectation`
no novo `discovery-report.json`. `discovery-summary.md` mostrará
o **pareamento temporal** de CF por duelo, não prova causal.
Os novos testes ainda precisam ser executados no Windows.
Para estender a outras cartas, adicionar referências externas revisadas
com links, texto resumido, gatilhos/efeitos esperados e questões abertas.
Priorizar futuramente a versão do texto efetivamente presente no jogo,
se conseguirmos extraí-la e verificar sua procedência.

Este experimento é **nativo e exploratório**. A seleção por propriedades do
DAT e a presença na tabela de callbacks não estabelecem ativação nem
implementação no Core C#. O modo Demo pode se comportar de maneira
diferente do jogo normal. Não alterar/hooks internos novos, nem usar PR #1.

## 1. Verificação offline no Windows

Use um worktree atualizado da branch `feature/native-effect-discovery-queue-v1`.
Os arquivos de cobertura vêm do próprio DAT e EXE, e o manifesto
`scratch/pool-joey-sandbox.json` foi aprovado somente para monstros normais
como controles do sandbox anterior.

```powershell
git pull --ff-only
python -m unittest discover -s yugilab/tests -p test_discovery.py -v
python -m unittest discover -s yugilab/tests -p test_effect_report.py -v
```

## 2. Auditar primeiro a captura Raimei existente (nenhuma execução nativa)

```powershell
python -m yugilab.discovery analyze `
  --registry .\scratch\coverage-current\registry.json `
  --attributes .\scratch\coverage-current\attributes.json `
  --input .\scratch\effect-pilot-native-3 `
  --output-dir .\scratch\discovery-audit-raimei-v1
```

O arquivo `discovery-report.json` deve registrar a integridade dos 3
terminais anteriores e os casos de 300 LP da Raimei. `rankedQueue` classifica
as próximas investigações; `perCard` inclui números de comandos CF/CE,
ocorrências em decks verificados, casos P045 com variação exata de LP e
exemplos de `seq` por capture. **Não** infere que uma carta sem comando
não foi comprada ou usada. O CE frequentemente é posicionamento.

## 3. Gerar decks exploratórios variados

```powershell
python -m yugilab.discovery prepare `
  --registry .\scratch\coverage-current\registry.json `
  --attributes .\scratch\coverage-current\attributes.json `
  --reviewed-normals .\scratch\pool-joey-sandbox.json `
  --seed 12345 --rounds 3 `
  --normal 16 --effect-monsters 12 --nonmonsters 12 `
  --output-dir .\scratch\native-discovery-decks-v1
```

O preparador exige os mesmos fingerprints entre registry/attributes e
identidades aprovadas para 16 monstros normais por deck. Os outros
24 índices por deck são **candidatos exploratórios não revisados**:
12 monstros cujo bit de categoria nativa é 1 e nível 1–4, excluindo IDs
conhecidos de invocação especial, e 12 cartas de classe 21, 22 ou 23.
São candidatos para o **jogo nativo**, não recebem `SandboxPartialOnly`
nem são aceitos para execução de efeito no C#.

Saídas: `round-01-A.json`, `round-01-B.json` etc. nos formatos conhecidos
do YugiLab, e `discovery-plan.json` com hashes, escopo e identidades.
Cada par contém 40 índices únicos por lado; os pares seguintes rotacionam
candidatos para aumentar a cobertura. Se uma quota não estiver disponível,
o script falha sem emitir decks. Ajuste quotas explicitamente, mantendo soma 40.

## 4. SOMENTE três duelos nativos, primeiro par

**Faça backup dos saves**. O YugiLab faz o spawn e encerra somente o PID
que criou, mas processos concorrentes podem compartilhar arquivos/configs.
Use um único worker, velocidade moderada e desative `--trace-ai` neste
piloto de descoberta de cartas, evitando o excesso de 95 mil probes do
experimento anterior. Desativar probes de IA **não** desativa os comandos.

```powershell
.\native_hooks\.venv-frida\Scripts\python.exe -m yugilab `
  --exe ..\passion\joey_pc.exe --duels 3 --workers 1 `
  --deck-0 .\scratch\native-discovery-decks-v1\round-01-A.json `
  --deck-1 .\scratch\native-discovery-decks-v1\round-01-B.json `
  --seed 12345 --speed-value 2400 `
  --output-dir .\scratch\native-discovery-round01-3
```

Interrompa a campanha se houver crash, perda de eventos, falha de override,
impossibilidade de encerrar PID ou ausência de 3 terminais. Não avance
automaticamente para dezenas de duelos.

## 5. Relatório por carta + fila de investigação

```powershell
python -m yugilab.discovery analyze `
  --registry .\scratch\coverage-current\registry.json `
  --attributes .\scratch\coverage-current\attributes.json `
  --input .\scratch\native-discovery-round01-3 `
  --output-dir .\scratch\native-discovery-report-round01-v1
```

Inspecione primeiro `integrityOk`, `captures[].issues`,
`observedResourceIds`, `perCard` e `rankedQueue`.
Estados de evidência:

- `ObservedFixedLpDeltaInScope`: casos P045 com associação restrita CF/LP
  e mudança observada; **não** valida a carta integral.
- `ResourceAssociatedCommandsOnly`: existe CF/CE mas falta prova causal.
- `DeckVerifiedNoResourceCommand`: deck confirmado, sem comando indexável;
  não significa que a carta não foi comprada/ativada.
- `CatalogCandidateNotObserved`: candidato ainda sem observação na captura.

Quando dois índices possuem o mesmo `resourceId`, o relatório marca
`sharedResourceIndices`: **não atribuir o comando a um índice específico**.
Todos os registros trazem `readyForFullCsharpComparison=false` até haver
contexto completo, ações alinhadas e uma validação independente.

A fila prioriza indícios de comportamento observado, mas a pontuação é
**heurística para investigação**, não porcentagem de implementação.
Depois de inspecionar o relatório do primeiro par, escolher qual carta
estudar (por exemplo, primeiro CF não-P045 com transição de estado
observável), em vez de escalar às cegas. Só então testar outros pares
de decks, e só mais tarde 100 duelos. Capturas, DAT, EXE e decks locais
permanecem em `scratch/`, fora do Git.
