# Agendamento de contextos, callbacks e sequência de turno

Análise estática do EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
VA, image base 0x00400000. Banco mantido sem alterações persistentes.

## Dois níveis de tabelas, com estados diferentes

**0x005BC700** indexa a tabela de DWORDs em **0x005EFBB0** pelo byte alto do
word 0x00A577FC. Entradas observadas: 0x005B9F50, **0x005BC620**,
0x005BC3C0, zero. Quando o handler retorna valor não zero, incrementa esse
estado alto e limpa 0x00A577FE por meio da atualização do DWORD sobreposto.
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
| 7 | 0x005BC050 | Verificação/atualização restante; pode encerrar o estágio |

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

[Modo de controle e prioridades](evidence/turn-processing-mode-selection.txt),
[dois despachantes](evidence/turn-processing-sequence-drivers.txt),
[despacho externo](evidence/duel-stage-dispatch.txt),
[fila e instruções do chamador](evidence/duel-command-queue-gate.txt),
[guardas de interface/invocação](evidence/ritual-scheduler-pending-gates.txt).
Relatórios `phase-handler-*` e `duel-phase-table-indexer.txt` registram a
investigação inicial, antes de distinguir índice sequencial e bits de fase.
