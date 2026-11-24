# scripts/pending — fila de coleta de evidência nativa

Os quatro roteiros de 22/11 foram executados e seus ZIPs privados
recebidos em 23/11/2026. **Coleta concluída, conhecimento consolidado.**
Os scripts são preservados para reproduzir evidências quando
necessário; não são executados por build, CI ou desenvolvimento
normal. Não versionar ZIPs, executável, DAT ou dumps de memória.

| Roteiro | Bundle recebido | Estado da coleta |
| --- | --- | --- |
| `01-selection-fusion-ghidra.sh` | `ghidra-selection-fusion-contract.zip` | Concluída (6/6, 3 reads) |
| `02-chain-disabled-ghidra.sh` | `ghidra-chain-disabled-contract.zip` | Concluída (5/5) |
| `03-field-attributes-ghidra.sh` | `ghidra-field-attributes-contract.zip` | Concluída (8/8) |
| `04-card-status-ghidra.sh` | `ghidra-card-status-contract.zip` | Concluída (17/17, 12 janelas) |

Manifestos confirmam o fingerprint original e missing=[].
Para os limites do disassembly e contratos extraídos,
ver [handoff dos quatro bundles](../../docs/research/visual-contract-bundles-2026-11-23.md).
**Não confundir coleta concluída com Draw implementado/Verified.**

## Dependências restantes, não repetir os quatro scripts

- Seleção/fusão: tabela runtime `00A52EE8` depois de
  `00A536E8=1`; constantes de centro, tabela de seletores e
  manifesto fusion/list; flag de espera. O ZIP não contém
  o valor de `005DCD00` nem a conversão `005C3E40`.
- Corrente desativada: `disable.txt` original, semântica do
  comando 0x72, integração da rotação e validação.
- ATK/DEF: comparação pareada em 800×600 e blend/cadência.
- Status: `card.txt` original incluindo o inteiro auxiliar de
  cada entrada (divisor de colunas); semântica de flags A/B,
  ramo especial e composição. Geometria ordinária já resolvida.

## Metadados DAT — coleta separada, quando necessário

Na máquina com o DAT original, o relatório integral já pode
ser extraído sem Ghidra:

```bash
dotnet run --project src/Joey.App -- /caminho/para/data.dat --inspect-visual-dat
python3 scripts/summarize_native_chain_dat.py
python3 scripts/summarize_native_card_status_dat.py
```

As saídas em `scratch/visual-review/` são privadas.
O relatório integral de manifests preserva os inteiros
auxiliares de cada entrada; o resumo de card-status ainda
não registra as colunas. Não deduzi-las só pela dimensão
do bitmap. Inspecionar o texto antes da integração.

## Protocolo de novas coletas — todas as trilhas

Fila única para ACTIONS (`A##`), CARDS (`C##`) e VISUALS (`V##`), incluindo Windows (execução/Frida/DAT) e Fedora (Ghidra/projeto local). Preservar números e histórico dos scripts 01–10; novos roteiros recebem o próximo número livre, nome descritivo e ID(s) da trilha na ficha. Cada novo roteiro deve especificar hipótese/pergunta, endereços quando aplicáveis, ambiente e dependências, comando reproduzível, artefato privado e critério de encerramento. Antes de preparar coleta, consultar o SQLite mais recente e os bundles já existentes. Enquanto a coleta estiver bloqueada, avançar em outra tarefa.
O exportador valida SHA-256, usa Ghidra -readOnly com
rollback e grava sob scratch/. Depois de consumir um bundle,
registrar conclusões em docs/research e marcar coleta como
concluída nesta fila. Preferir integrar evidências recuperáveis em nova cópia cumulativa do SQLite, com proveniência, fingerprint, auditoria e verificação de integridade; usar ou ampliar importadores existentes conforme o formato. Capturas/frames incompatíveis permanecem em artefatos privados referenciados. Registrar snapshot de origem e destino, importação concluída ou pendente, sem confundir coleta com implementação. Consultar [guia SQLite](../../docs/research/joey-knowledge-sqlite.md). Novas pendências devem corresponder a evidências específicas, não reexecutar coletas completas.

Ficha mínima para novos scripts: `número | ID da trilha | ambiente | pré-requisitos | comando | saída | pergunta | estado da coleta/importação | referência de conclusão`. Não executar automaticamente.

## Roteiros de regras de cartas — aguardando coleta

| Roteiro | Pré-requisito | Produz | Estado |
| --- | --- | --- | --- |
| `05-flip-spelltrap-targets-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-flip-spelltrap-two-targets.zip` | **Aguardando execução** |
| `06-prohibition-links-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-prohibition-links-contract.zip` | **Aguardando execução** |
| `07-flip-banish-burn-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-flip-banish-burn-contract.zip` | **Aguardando execução** |
| `08-immortal-thunder-graveyard-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-immortal-thunder-contract.zip` | **Aguardando execução** |
| `09-dimensional-warrior-banish-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-dimensional-warrior-banish.zip` | **Aguardando execução** |
| `10-dark-necrofear-control-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-dark-necrofear-control.zip` | **Aguardando execução** |

### FLIP de duas S/T (Greenkappa e Tornado Bird)

O quinto roteiro investiga Greenkappa (`585`, P085) e Tornado Bird
(`1521`, P255). Dos oito callbacks específicos catalogados, apenas
a disponibilidade de Tornado Bird (`00583120`) está decompilada
no SQLite atual. O ZIP deve trazer assembly ou ausência explícita
para os outros sete endereços. Confirmar seleção de duas ocorrências,
filtro face-down, zona FIELD, ação de retorno/destruição, possíveis
resoluções parciais e supressão (Royal Command) antes de elevar as
cartas a Verified. **Este roteiro ainda não foi executado**.
Consultar [P085/P255](../../docs/cards/p085-p255-two-spelltrap-flip-sandbox.md).

### Prohibition e efetividade das fontes contínuas

O sexto roteiro exporta os callbacks de **Prohibition** (resource ID 1162,
índice DAT 229): primário `0058C430` e preparação de alvo
`00595F20`, que **não estão decompilados no SQLite atual**.
Inclui `0056B440` e `005674E0` (ligações/identidade) e os
contadores `00569820`/`005699F0`, além da recomputação de Jinzo,
Royal Decree e Royal Command. Confirmar o significado dos vínculos
de tipos 9/11, o momento da declaração e o efeito sobre cartas já
presentes, antes de implementar Prohibition por aproximação.
**Roteiro ainda não executado**.

### FLIP P051/P052 — banimento de Deck / mão e dano 800

O sétimo roteiro extrai os gates de disponibilidade e produtores do
custo de Lady Assailant of Flames (`1435`) e Fire Sorcerer (`1436`).
O callback de dano `00584E60` está decompilado, porém as quatro
rotinas específicas de custo ainda não estão no SQLite. Confirmar
consumo e ordem de seleção, RNG do modo 4, destino `0x62`,
restrições por Royal Command e comportamento com mão/deck
insuficientes. **Roteiro não executado.**

### P065 — The Immortal of Thunder: histórico de FLIP e saída para o cemitério

O roteiro 08 explora os callbacks P065 (`00581040`, `0057C5F0`,
`00585970`), que ainda não foram decompilados, e dois helpers
(`00576F40`, `00577CF0`) que consultam o ID `0x1CD` e bits
da ocorrência. O objetivo é verificar exatamente **quando** a perda
de 5000 LP é admitida e como ela interage com negação, carta
face-down e saída por tributo/fusão. **Ainda não executado.**

### P188 — Dimensional Warrior: combate e banimento duplo

O roteiro 09 busca o callback primário `0058CC00` (catalogado,
mas sem decompilação no SQLite) e as rotinas de dano e movimentação
por zona. O objetivo é confirmar a ordem nativa entre cálculo de
dano, efeito P188, FLIP de outro monstro, destruição normal em
batalha e ausência de envio ao cemitério. **Ainda não executado**.

### P252 — Dark Necrofear

Roteiro 10: recuperar disponibilidade, preparação e callback primário, rastrear vínculo de controle, evento de saída e pedido da End Phase. Custo de três Fiends no GY já sustentado por 0059DEF0/00599E30; não confundir com Spirit Elimination. **Ainda não executado.**

Executar somente o roteiro necessário com Ghidra local (por exemplo, `bash scripts/pending/05-flip-spelltrap-targets-ghidra.sh`). Não versionar bundles privados.

## ACTIONS — evidência de fases

| Roteiro | ID | Ambiente | Saída | Estado |
| --- | --- | --- | --- | --- |
| `11-actions-turn-phases-ghidra.sh` | A07 | Fedora/Ghidra local, projeto e EXE com fingerprint fixado pelo exportador | `scratch/ghidra-actions-turn-phases.zip` | Coletado e importado no SQLite cumulativo v2 |

O ZIP local tem SHA-256 `c3f2fc53f0af6554aa90e65e75d2fc6a558ba5b19547f405a34c865fccea54b3`.
No SQLite v2 o mesmo digest identifica dez resultados de decompilação, janelas de
assembly e dois blocos de 28 bytes, em `005EFB78` e `005EFB94`. Os dois blocos
coincidem com o EXE local de fingerprint fixado. A pergunta restante é a
correlação entre índice do dispatcher, bits de fase e rótulos no jogo.
Consultar a [evidência estática](../../docs/20-turn-scheduling.md#reprodução-e-evidências)
antes de ampliar a coleta. O roteiro 11 fica disponível para reproduzir a coleta original.

### 14 — C01: taxonomia de efeitos pelo DAT local

- Ambiente: Windows (Git Bash) ou Fedora, Python 3 e .NET 10, DAT original local.
- Comando: `bash scripts/pending/14-card-effect-taxonomy.sh /caminho/para/data.dat`.
- Saída privada: `scratch/card-taxonomy.json` (IDs, fingerprints, famílias, rotas e status atuais; sem descrições).
- Pergunta: quais cartas dos packs são candidatas a lotes triviais e quais exigem revisão/continuações/respostas?
- Encerramento: conferir as 771 entradas por ID, máscara e categoria e validar o fingerprint com `docs/cards/pack-index.json`. O SQLite v2 contém 442 identidades. O DAT local fornece as descrições para a triagem.
- Estado: execução concluída em Fedora/.NET 10. Rotas validadas no lote local: 264 sem efeito aplicável, 433 fila/resposta, 72 revisão e duas candidatas triviais. A compra já consta como Done e a cura permanece Pending. Mantenha a saída `--taxonomy-input` no pipe privado do classificador. A classificação preserva os status do manifesto e a saída em `scratch/` contém somente metadados.

### 12 — C07: chegada ao cemitério e causa do descarte

- Ambiente: Fedora/Ghidra, projeto Joey original, Python 3 e Ghidra headless. Roteiro 14 reservado em C01 (#66).
- Comando: `bash scripts/pending/12-graveyard-arrival-ghidra.sh`.
- Saída privada: `scratch/ghidra-graveyard-arrival-contract.zip`.
- Questão: callbacks P066/P068/P030 ausentes e argumentos do caminho 00575970 → 005759A0 → 00575AF0; separar custo, descarte adversário e envio direto. Conferir descrições DAT locais também.
- Encerramento: resolver ou registrar ausência dos callbacks e disassembly dos chamadores; confirmar origem da flag, retorno ao topo e ordem, registrar evidências e limites em C07. Não repetir exportação geral.
- Estado: aguardando coleta; análise inicial SQLite concluída. Ver [C07](../../docs/cards/c07-graveyard-arrival-effects.md).

## V01 / V11 — 15-visual-cosine-runtime.py (aguardando Windows)

Com o executável original aberto em um duelo e Frida instalado na venv:

```powershell
python scripts/pending/15-visual-cosine-runtime.py --pid <PID> --exe ..\passion\joey_pc.exe
dotnet run --project src/Joey.App -- ..\passion\data.dat --alpha --native-cosine=scratch/visual-review/native-cosine.json
```

Valida SHA-256 e PID, lê somente 512 inteiros em 00A52EE8 após
00A536E8=1, confirma duas amostras iguais; não altera memória do jogo
nem termina o processo. Saída privada: scratch/visual-review/native-cosine.json.
Não sobrescreve captura anterior. Critério: captura válida carregada pelo
cliente e F12 indicando native-runtime-capture. O log de Draw de V01
continua pendente. Não é necessário repetir os bundles 01–04.


## V11/V17 — 13-fusion-remaining-contract-ghidra.sh (aguardando Fedora)

Apenas lacunas novas: dados de centro 005DB278/27C e janela de
seletores 005DB280 (48 bytes brutos, NÃO presumir tipo/stride);
fator final x87 005DCD00; compositor 00422C00/00422840/00422E30
e caminho de liberação 00486CE0/00487930/00487DF0.
Pré-requisito: Ghidra local e mesmo projeto/fingerprint de Joey.
Executar:

    bash scripts/pending/13-fusion-remaining-contract-ghidra.sh

Saída privada scratch/ghidra-fusion-remaining-contract.zip.
Não repetir as seis funções do bundle 01; o epílogo ainda exige
assembly se a evidência existente não bastar. Conferir tipo/stride
dos seletores, relação dos centros com a perspectiva, regra de
transformação e escritor da flag. Importação SQLite pendente
até receber o ZIP; não confundir a coleta com implementação.
Para os recursos, utilizar --inspect-visual-dat já existente
e compartilhar privadamente apenas registros de fusion.txt e
dimensões das imagens referenciadas (sem pixels).

### 16 — CARDS: P117/P159/P160 ressurreição

`bash scripts/pending/16-cards-revival-spells-ghidra.sh` no Fedora/Ghidra local. Saída privada `scratch/ghidra-revival-spells-contract.zip`. Extrair sete callbacks ausentes nos dois SQLite, com assembly e helpers de movimentação/identidade. Confirmar propriedade/controle, seleção, custo e vínculo; não promover cobertura apenas pela coleta. Ver [lacuna](../../docs/cards/p117-p159-p160-revival-spells-gap.md).

### 17 — CARDS: P253 mudança de posição elemental

`bash scripts/pending/17-cards-elemental-position-ghidra.sh` — coleta complementar ao roteiro 11 de ACTIONS: callbacks 0057B1F0/0057CD40/005907D0, helpers e tabela de produtores. Saída privada `scratch/ghidra-elemental-position-contract.zip`. Confirmar dispatch, filtro, lock de Aqua e mudança de posição por efeito. [Contrato](../../docs/cards/c16-elemental-position-opportunities.md).

## A03/A04 — 22-actions-console-visual-smoke.sh (piloto validado no Windows)

Roteiro 22 executado via PowerShell, headless e gráfico: 18 Applied e 106 linhas relevantes idênticas após fff5e6e. Novo roteiro `scripts/fixtures/duel-console-two-sides-smoke.txt` executou 29 Applied, sem espera >=5s; operador confirmou compras animadas de ambos lados. CI 45328386043. Manter script como regressão opcional. Não implica paridade visual nativa; A05/A07 pendentes.
