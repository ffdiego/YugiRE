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

## ACTIONS — aguardando coleta

| Roteiro | ID | Ambiente | Saída | Estado |
| --- | --- | --- | --- | --- |
| `11-actions-turn-phases-ghidra.sh` | A07 | Fedora/Ghidra local, projeto e EXE com fingerprint fixado pelo exportador | `scratch/ghidra-actions-turn-phases.zip` | Aguardando execução/importação |

Executar `bash scripts/pending/11-actions-turn-phases-ghidra.sh`. O SQLite de 22/11 não possui decompilações nos dez VAs-alvo. O roteiro extrai os drivers e handlers das tabelas, mais as duas tabelas brutas. Pergunta: como o índice de A577FE se liga às escritas em A577FA e aos rótulos exibidos? Encerrar a coleta após verificar fingerprint e cobertura dos alvos (ausências explícitas), importar evidências compatíveis em cópia cumulativa do SQLite e documentar o mapeamento ou a lacuna residual. Não chamar a coleta de implementação A07. Ver [A01](../../docs/architecture/action-queue.md).

### 14 — C01: taxonomia de efeitos pelo DAT local

- Ambiente: Windows (Git Bash) ou Fedora, Python 3 e .NET 10, DAT original local.
- Comando: `bash scripts/pending/14-card-effect-taxonomy.sh /caminho/para/data.dat`.
- Saída privada: `scratch/card-taxonomy.json` (IDs, fingerprints, famílias, rotas e status atuais; sem descrições).
- Pergunta: quais cartas dos packs são candidatas a lotes triviais e quais exigem revisão/continuações/respostas?
- Encerramento: conferir 771 registros, fingerprint do DAT, amostras e candidatos; não promover cobertura. O SQLite contém 442 identidades e não tem descrições do DAT.
- Estado: aguardando execução local; classificador testado com fixtures sintéticas, integração .NET ainda não executada. Nunca executar o modo `--taxonomy-input` diretamente ou redirecionar sua saída para arquivo: a descrição deve ficar no pipe privado do classificador.

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

## V20/V21/V22 — 16-coin-dice-break-ghidra.sh (aguardando Fedora)

Ambiente: Ghidra local, projeto e EXE original com fingerprint validado pelo exportador. Consulta SQLite já concluída: seis Update/Draw não presentes; construtores/handlers presentes com ABI ambígua.

    bash scripts/pending/16-coin-dice-break-ghidra.sh

Saída privada: scratch/ghidra-coin-dice-break-contract.zip. Validar vtables +8/+C, contadores, recortes, camada e argumentos reais (não inferir faces/RNG pelo nome do banco); integrar novas evidências em SQLite cumulativo após receber o ZIP. Para coin.txt/dice.txt/break.txt, usar --inspect-visual-dat existente e preservar campos numéricos. Ver [contrato](../../docs/research/native-coin-dice-break-contract-2026-11-23.md). Não repetir bundles 01–04.

## V23/V24 — 17-chain-affect-assembly.sh (aguardando Fedora)

Ambiente: Ghidra headless, mesmo projeto e EXE atestado. O SQLite já contém as decompilações transitórias de Update/Draw; não repetir os bundles 01–04 nem reexportar todos os métodos virtuais. Este roteiro pede assembly apenas das quatro funções com operandos, seletor e jump tables ainda ambíguos.

    bash scripts/pending/17-chain-affect-assembly.sh

Saída privada: scratch/ghidra-chain-affect-assembly.zip. Confirmar as chaves de lookup de chain.txt, pass decimal, composição scratch e dispatch de affect.txt. Para os dois manifests, reutilizar --inspect-visual-dat e preservar a ordem/campos/dimensões. Importar evidência compatível em cópia SQLite após receber a coleta; sem marcar Draw como implementada. Ver [contrato](../../docs/research/native-chain-affect-contract-2026-11-24.md).

## V25/V26 — 18-alternative-activation-exclusion-ghidra.sh (aguardando Fedora)

Ghidra local, EXE/projeto atestado. SQLite já consultado: dois Draws ausentes, construtores disponíveis; Update de exclusão no-op.

    bash scripts/pending/18-alternative-activation-exclusion-ghidra.sh

Saída privada: scratch/ghidra-alternative-activation-exclusion.zip. Confirmar slots da vtable, fases, recortes e gate de término (inclusive possível mutação no Draw), ABI e mascaramento de exclusão. Recuperar xrefs do construtor se disponíveis, não atribuir opcode por nome de asset. Usar --inspect-visual-dat para exclusion.txt. Importar evidências compatíveis em SQLite cumulativo após receber ZIP. [Contrato](../../docs/research/native-alternative-activation-exclusion-2026-11-24.md).

## V27/V28/V29 — 19-layerfont-help-ghidra.sh (aguardando Fedora)

Ghidra local, projeto/EXE atestado. SQLite já contém decompilações transitórias Layerfont; faltam operandos e composição confirmados em assembly. Help Update/Draw ausentes.

    bash scripts/pending/19-layerfont-help-ghidra.sh

Saída privada: scratch/ghidra-layerfont-help-contract.zip. Confirmar dimensões/operandos de +48/+4C, gates/seleção do help e slots de vtable. Reutilizar --inspect-visual-dat para layerfont.txt e help.txt; não enviar pixels. Importar resultados em SQLite cumulativo quando recebidos. [Contrato](../../docs/research/native-layerfont-help-2026-11-24.md).

## V30 / V11 — 20-vija-selection-origins-ghidra.sh (aguardando Fedora)

Ghidra local, mesmo projeto/EXE. O snapshot atual não possui Draw 004584C0; construtor, predicado e chamadores já consultados. Janelas dirigidas apenas para ABI ambígua e memória das tabelas não coletadas.

    bash scripts/pending/20-vija-selection-origins-ghidra.sh

Saída privada: scratch/ghidra-vija-selection-origins.zip. Conferir Draw, máscaras, ordem de letras, 005DA410, 005DB414/424 e centros 005DB5B0:80. Para vija.txt use --inspect-visual-dat, sem pixels. Importar evidência em nova cópia SQLite cumulativa. [Contrato](../../docs/research/native-vija-selection-origins-2026-11-24.md).

### Importação cumulativa (todas as coletas VISUALS)

Após receber ZIPs válidos, usar `scripts/import_visual_evidence.py` com `--source`, `--bundle` (repetível) e `--output` apontando a uma nova cópia. O importador histórico de métodos virtuais não aceita batches cumulativos. Ver [procedimento e consultas](../../docs/research/visual-evidence-cumulative-import.md). Não versionar evidência privada.

## V04/V06 — 21-attack-life-draw-ghidra.sh (aguardando Fedora)

Ghidra local, projeto e EXE atestado. Os dois Draws não existem nas tabelas de decompilação do SQLite atual; produtores e Update já consultados. 

    bash scripts/pending/21-attack-life-draw-ghidra.sh

Saída privada: scratch/ghidra-attack-life-draw-contract.zip. Recuperar instruções de seleção de sprite/pivô e âncoras de LP por slot/-1, ABI dos produtores e vtables. Importar com import_visual_evidence.py em nova cópia. [Contrato](../../docs/research/native-attack-life-routing-2026-11-24.md). Não repetir a coleta completa do inventário.

## V07 — 23-turn-notice-draw-ghidra.sh (aguardando Fedora)

Ghidra local, projeto/EXE atestado. SQLite confirmou ordem, preloads e contadores de painel; faltam 00480C20/00480C60 e virtual +4. 

    bash scripts/pending/23-turn-notice-draw-ghidra.sh

Saída privada: scratch/ghidra-turn-notice-draw-contract.zip. Verificar vtables, seletor de frame, âncora, composição e término; se o virtual +4 apontar para outra rotina ainda ausente, registrar VA para próxima coleta. Usar --inspect-visual-dat para phase_name.txt; importar bundle por import_visual_evidence.py. Não repetir 11/A07 nem os quatro bundles concluídos. [Contrato](../../docs/research/native-turn-notice-contract-2026-11-24.md).
