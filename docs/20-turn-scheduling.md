# Agendamento de contextos, callbacks e sequência de turno

Análise estática do EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
VA, image base 0x00400000. Banco mantido sem alterações persistentes.

## Evidência posterior: replay real e limites de automação

O arquivo `docs/evidence/replays/relinquished_commands.csv`,
derivado de uma única partida de 1.062 registros, contém:

| Opcode | Quantidade | Observação |
| --- | ---: | --- |
| `0x01` | 39 | Inícios de turno, alternando o bit alto |
| `0x02` | 38 | Uma emissão em cada turno concluído |
| `0x03` | 38 | Uma emissão em cada turno concluído |
| `0xCE` | 19 | Colocações distribuídas por 19 turnos distintos |
| `0x04` | 1 | Último registro, sem par final `0x02/0x03` |

A sequência corrobora a separação estática de início de
turno, limpeza e transição; não comprova que esses comandos
sejam seguros para emissão direta por um executor externo.
O end-turn original passa pelos contextos, handlers e fila
antes de concluir a mudança. Um futuro comando de passagem
de turno deve acionar o caminho de controle apropriado,
não simplesmente injetar `0x03`.

A amostra já é suficiente para montar um agrupador de
eventos por turno; não fornece distribuições da IA nem
evidência de sorteio de deck. Ver [plano de análise da IA](06-ai.md).

## Projeção da troca de turno em A07

O subestado 1 de `005A8A50` emite em `005A8A70–005A8A82` o comando
`03` com o bit 15 obtido de `00A577FA & 1`, antes da troca de lado, e
três operandos zero. A tabela `005B9AEC`, índice `03 - 1`, seleciona o
stub `005B94A8`, que salta para `004048C0`. O consumidor incrementa o
contador, alterna o bit do lado e zera o sequenciador `00A577FE`.
A decompilação do consumidor e o bundle 11 do produtor estão no SQLite v3.

Nos 38 registros `03` do CSV arquivado, o bit 15 coincide com o do último
início de turno `01`, e os três operandos são zero. O comparador associa
a forma exata a `AdvanceTurnCommand` aplicado. A entrada em End Phase e
o descarte continuam sem projeção, assim como a limpeza `02` do replay.
Os testes comparam os dois lados e mantêm a lacuna de limpeza no relatório.

## Dois níveis de tabelas, com estados diferentes

**0x005BC700** indexa a tabela de DWORDs em **0x005EFBB0** pelo byte alto do
word 0x00A577FC. Entradas observadas: 0x005B9F50, **0x005BC620**,
0x005BC3C0, zero. Quando o handler retorna valor não zero, incrementa esse
estado alto e zera apenas o byte alto de 0x00A577FE, preservando o baixo.
Os significados completos da primeira e terceira rotina não são atribuídos
neste checkpoint. Não confundir esse seletor com o estado privado do Ritual.

Antes de chamar o handler, o caminho local verifica apresentação pendente
por um thunk de 0x0040CF20 e processa comandos por **0x005B9CA0**. Só prossegue
quando ambos retornam zero. A rotina da fila mantém um comando atual e seu
próprio estágio em 0x00A5780E; copia oito bytes, compacta a fila e chama o
dispatcher. Enquanto bit 0x4000 em 0x00A558AC continua definido, retorna 1.
A etapa final também verifica flags de sincronização e novos comandos.
Não se pode reduzir essa função a “fila vazia?” nem contar um comando por
frame; o último comando concluído pode permitir progresso na mesma passagem.

## Retirada e continuação da fila local

O produtor `005B9170` anexa quatro words de 16 bits em
`00A550A8 + contagem * 8`, até 256 entradas. O consumidor `005B9CA0`
retira da frente, em ordem FIFO. O byte alto de `00A5780E` indica o estado:

| Estado | Trabalho |
| --- | --- |
| 0 | Copia o primeiro registro para `00A550A0`, decrementa a contagem e desloca os restantes um registro para a esquerda. Inicializa a execução do comando corrente. |
| 1 | Chama `005B9470` para processar o **mesmo** comando corrente. O bit `0x4000` em `00A558AC` mantém esse estágio enquanto estiver ativo. |
| 2 | Espera a condição de sincronização `00A58260 & 0x100`, volta ao estágio 0 e ainda bloqueia o avanço se houver comando na fila. Há uma condição adicional para reapresentação em `005BC580`. |

O caminho remoto de `005B9BF0` e a reapresentação têm condições próprias.
Na alpha, a ordem visual de Trap Hole segue a observação do vídeo sem
reimplementar o protocolo de comandos. O modelo de fases do sandbox ainda é
simplificado. [Consumidor](evidence/duel-command-queue-gate.txt) ·
[Produtor](evidence/command-queue-entrypoints.txt)

## Prioridade dos trabalhos pendentes

No handler **0x005BC620**, os contextos são consultados em ordem, com guardas
pelos retornos. Endereços exatos das chamadas foram conferidos nos bytes:

| Ordem | Função | Papel seguido ou limite |
|---:|---|---|
| 1 | 0x005BF9C0 | Estado da interface/abertura de diálogos |
| 2 | 0x005BC9B0 | Ações de seleção pendentes em 0x00A57810; inclui descarte |
| 3 | 0x005AB820 | Contexto de colocação/invocação |
| 4 | 0x005B8C00 | Outro contexto pendente, não expandido aqui |
| 5 | 0x005BBE40 | Pedidos e resolução dos callbacks |
| 6 | 0x005BA060 | Processamento adicional compartilhado de eventos |
| 7 | 0x005BC050 | Condições de encerramento; falta de cartas seguida na análise de compra |

Só depois desses trabalhos o fluxo escolhe o despachante da sequência.
Não atribuímos ao segundo helper um teste de vitória: ele despacha por tipo
de ação e chama, por exemplo, 0x005A91F0, já seguido no custo de descarte.
Retornos de helpers distintos não são booleanos semânticos intercambiáveis.

Para a colocação, **0x005AB820** retorna 1 enquanto um handler de modo
permanece incompleto. Ao concluir, limpa bit 0 do controle 0x00A5508E,
processa consequências e retorna o bit ativo final. O modo 5 encaminha
0x005AD210 → 0x005ACF10, conforme estudo dos Rituais. Não há concorrência
livre entre esse contexto e a resolução seguinte do callback nesse caminho.

## Sequência com variantes por modo de controle

O bit de controle do jogador seleciona **0x005B7BD0** ou **0x005BC5D0**.
Há outro caminho condicionado ao modo compartilhado/remoto que não foi
expandido. As duas tabelas locais usam o **byte baixo de 0x00A577FE**:

| Índice | Tabela 0x005EFB78 | Tabela 0x005EFB94 |
|---:|---|---|
| 0 | 0x005A59B0 | 0x005A59B0 |
| 1 | 0x005A41C0 | 0x005A41C0 |
| 2 | 0x005A8090 | 0x005A8090 |
| 3 | 0x005B7B50 | 0x005A5A40 |
| 4 | 0x005A5120 | 0x005A5120 |
| 5 | 0x005A8A50 | 0x005A8A50 |
| 6 | zero | zero |

Um retorno não zero do elemento incrementa o byte baixo e zera o byte alto
de continuação. O primeiro despachante também limpa contexto 0x00A579A0 na
entrada do índice 0/subestado 0. O byte alto é usado pelos próprios handlers;
no último, controla a emissão de limpeza 0x02 e alternância 0x03.

Esse índice não é extraído dos bits de fase em 0x00A577FA. Ainda não foi
fechado o mapeamento entre cada elemento e os rótulos das fases da interface.
A tabela não deve ser publicada como uma enumeração de fases só pela ordem.
O último elemento se liga concretamente à transição de turno. 0x03 zera
0x00A577FE, permitindo recomeçar essa sequência, além de alternar lado e
incrementar contador. Não inferimos que todos os outros elementos tenham
uma duração fixa ou sejam exclusivamente apresentação.

## Consequência para o Ritual

O callback de Ritual no estado 99 prepara um contexto de invocação e retorna
98. Na passagem seguinte, o laço externo trata comandos e o dispatcher de
invocação tem prioridade sobre 0x005BBE40. Enquanto há trabalho bloqueante,
o callback não avança para seu ramo default de encerramento. Quando volta a
ser chamado, a continuação 98 produz retorno baixo zero.

Isso fecha a ordem lógica que faltava ao estudo: preparar contexto não é
inserir imediatamente, e encerrar o callback não dispensa o processamento
anterior do contexto/fila. A validade de uma ocorrência que desapareça, as
respostas de cartas e o timing visual ainda exigem caminhos específicos ou
execução futura. Nenhum frame foi medido nesta análise.

## Reprodução e evidências

`python3 scripts/validate_turn_scheduling.py` confere fingerprint, três
tabelas e dez CALLs. [Relatório](evidence/turn-scheduling-validation.json).
São checks estáticos, não execução do agendador.

O bundle 11 está no SQLite cumulativo v2 com dez decompilações e as
duas tabelas locais. Os blocos de 28 bytes conferem com o executável
local. Essa verificação complementa o relatório estático e conserva a
lacuna de associação entre índice, bits de fase e rótulo em execução.
[Proveniência do bundle](../scripts/pending/README.md#actions--evidência-de-fases).

[Modo de controle e prioridades](evidence/turn-processing-mode-selection.txt),
[dois despachantes](evidence/turn-processing-sequence-drivers.txt),
[despacho externo](evidence/duel-stage-dispatch.txt),
[fila e instruções do chamador](evidence/duel-command-queue-gate.txt),
[guardas de interface/invocação](evidence/ritual-scheduler-pending-gates.txt).
Relatórios `phase-handler-*` e `duel-phase-table-indexer.txt` registram a
investigação inicial, antes de distinguir índice sequencial e bits de fase.

## A07 — Fases, seletores e replay (SQLite v14, 28/11/2026)

Proveniência: snapshot privado `joey-knowledge-visual-cumulative-v14.zip`,
SQLite SHA256 `e4778b59f6d679a36657d212499a5718f8e697c0457047d38a7933aaf9bcb428`,
`PRAGMA quick_check=ok`, metadata.executable_sha256 igual ao fingerprint
acima. Consulta sem alterações. Evidência anterior do roteiro 11
(bundle `c3f2fc53f0af6554aa90e65e75d2fc6a558ba5b19547f405a34c865fccea54b3`)
e assembly de apresentação e dispatcher já presentes; nenhuma coleta nova.

### Separar fase de interface, índice de trabalho e opcode

A decompilação de 00404920 (consumidor comum de fase) atualiza
`word[A577FA] = (word[A577FA] & 0xFFF1) | ((param & 7) << 1)`
e chama 00481230 com lado do word0 bit15 e o parâmetro de fase.
No subestado posterior aguarda vtable+4 de 005F33F0 antes de limpar o
bit ativo 0x4000. Não confundir com a espera anterior da fila.
A sequência de stubs no assembly de 005B9470 confirma PUSH 0..5
em 005B958B/96/A1/AC/B7/C2, CALL 00404920 em cada um.
O snapshot não contém os bytes da tabela comprimida de índices
005B9AEC; a associação opcode -> stub ainda precisa dessa conferência
direta. Não usar números de case da decompilação como opcodes.

00481230 armazena lado em +144 e fase em +148; encaminha a 00480B40
que guarda fase em +A0. As vtables 005DBB1C e 005DBB2C apontam
respectivamente Draw 00480C60 e 00481420. Assembly 00480C6D–00480CB9:
lê +A0, testa <6 (ramo de fase normal), calcula fase + 6 * seletor de
lado relativo, passa chave ao resolvedor. 004809A0 referencia
phase_name.txt e 00481040 referencia phase.txt. O manifesto privado
DAT em visual_dat_manifests (visual-dat-20261124) lista, em ambos,
os seis recursos na ordem dr, st, m1, ba, m2, en. Portanto o índice
0..5 é coerente com Draw, Standby, Main1, Battle, Main2, End;
3 tem tratamento de animação próprio. O índice do dispatcher baixo
A577FE continua distinto. 004048C0 alterna lado e força bits 1..3
a 111 (sentinela 7, não uma fase jogável).

| Opcode candidato | Fase / índice | Evidência do produtor | Registros no replay |
| --- | --- | --- | ---: |
| 50 | Draw / 0 | índice de trabalho 1, 005A421F; subestado 2 chama 00578B60(side,1) | 39 |
| 51 | Standby / 1 | índice 2, 005A813C, antes dos trabalhos de Standby | 39 |
| 52 | Main1 / 2 | índice 3 no caminho 005A5A40, 005A5AA6 | 39 |
| 53 | Battle / 3 | decompilação 005A1200 emite 32(1,0,0) e depois 53(0,0,0) | 13 |
| 54 | Main2 / 4 | índice 3, 005A5C02 após rotina 005A4090 | 9 |
| 55 | End / 5 | índice 4, 005A5196, antes da resolução de fim de turno | 38 |

Os CALLs dos produtores 50/51/52/54/55 foram conferidos nas
instruções do bundle 11; 53 foi conferido por decompilação e replay,
não por assembly do produtor neste lote. No caminho alternativo
005B7B50 o índice de trabalho 3 usa seu próprio dispatcher
A579A0/tabela 005EFB50; não supor que o caminho CPU seja idêntico
ao 005A5A40 nem que o índice de trabalho represente fase.

### Conferência dos 1062 registros do replay arquivado

Filtrando opcodes 01/02/03/50..55 (sem alterar o CSV), os 38 turnos
concluídos apresentam:
- 26 turnos sem Battle: 01 50 51 52 55 02 03.
- 9 com Battle e Main2: 01 50 51 52 53 54 55 02 03.
- 3 com Battle sem Main2 (33,35,37): 01 50 51 52 53 55 02 03.
- turno 39 termina antes de 55/02/03: 01 50 51 52 53.

Todos os registros 01/02/03/50..55 têm três operandos zero.
Há 49 registros 61 no total, incluindo duas compras iniciais (turno
0); em cada um dos 39 turnos há um 61 entre 50 e 51. Há compras
adicionais por efeitos, inclusive em Battle. Não contar qualquer
61 como compra de turno. A amostra não prova que fases nunca
possam ser puladas: 005A41C0 tem guardas que encerram o trabalho
sem emitir 50, 005A8090 também tem guarda antes de 51.

### Consequências para implementação e validação

Separar BeginTurn, fase, Draw(1), Standby work, limpeza e AdvanceTurn.
A fase é uma alteração observável e deve poder aguardar a apresentação,
não ser avançada implicitamente por uma compra. O estado do scheduler,
o estado lógico de fase e a timeline visual não são o mesmo contador.
Aceitar Main1 -> End e Battle -> End sem inventar Main2; revalidar
as permissões e as respostas do motor. Não inferir ordem universal
de efeitos por essa única amostra. Perfis headless/Classic devem manter
a sequência lógica de comandos, independentemente do tempo de gate.

Limites: ainda não há prova direta dos bytes da tabela 005B9AEC ligando
cada opcode ao stub, nem confronto em execução de A577FA, A577FE,
A550A0 e rótulo apresentado nos dois modos de controle. Não promover
A07 a Concluído e não mudar o adaptador de trace apenas por esta
correlação. Próximo: recuperar tabela comprimida (índices 0x50..0x55
e os destinos) no Ghidra e comparar o ciclo C# com os três caminhos
observados, incluindo fase pulada, gates, decisão de descarte e limpeza
02. Não há alteração C# neste checkpoint.
