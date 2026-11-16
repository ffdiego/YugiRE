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
