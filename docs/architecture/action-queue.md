# Contrato da fila de ações — A01

Estado: contrato de implementação aprovado para iniciar A02; não é uma fila já implementada nem paridade nativa completa. Escopo: duelo local, inclusive headless. A02 não precisa reproduzir memória Win32, transporte remoto ou bytes proprietários.

## Proveniência

- [Análise estática do scheduler](../research/native-action-scheduler.md), [turno](../20-turn-scheduling.md), [custo de descarte](../cards/tribute-discard-cost.md) e [invocação/tributos](../26-normal-summon-tributes.md).
- EXE SHA-256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
- SQLite privado recebido: joey-knowledge-visual-decoded.sqlite, SHA-256 db798e9153a7374c13bf341f18089c804a7254d4a96b3e28141ae66ecac44bdb. Leitura somente; PRAGMA integrity_check = ok; 6518 funções e 6518 decompilações status ok. O fingerprint da tabela metadata coincide. Não há nova execução nativa nem novo disassembly neste lote. As confirmações de assembly vêm dos relatórios anteriores; a consulta ao SQLite corrobora ou limita sua interpretação. Não versionar banco, código decompilado integral ou dumps.
- O dispatcher 005B9470 tem switch comprimido (tabela de índices 005B9AEC e stubs 005B9838). Os números de case da decompilação **não são opcodes**. Usar tabelas/bytes ou produtores rastreados; não inventar mapeamentos.

## Máquina nativa local

Frame da cena 0044DD00 chama 005C0000 → 005BC700 antes de Update 0045D040 e Draw 0045D170, nos ramos examinados. Antes de retirar: gate 005BDC40 → 0040CF20. Se ocupado, Update/Draw continuam, sem avanço do consumidor. Depois de liberado, 005B9CA0 roda no máximo uma vez por passagem. Se retorna livre, o controlador de turno pode continuar na mesma passagem.

| Estágio (byte alto A5780E) | Operação |
| --- | --- |
| 0 | Se vazio retorna livre; senão copia exatamente um registro de A550A8 para A550A0, decrementa contagem A558A8, compacta o buffer, arma A558AC bit 14 e zera subestado; normalmente arma A58260 bit 8. Pode cair em 1 na mesma chamada. |
| 1 | Despacha o registro corrente em 005B9470; handler usa A558AC & 0x7F. Enquanto bit 14 estiver ativo, mantém o mesmo registro. Muitos handlers aguardam vtable[+4] dos objetos visuais; não são novos comandos. |
| 2 | Aguarda A58260 & 0x100, zera seletor alto; se houver mais registros retorna ocupado, sem retirar outro nessa chamada. Se vazio, pode liberar o controlador; replay tem feeder próprio. |

O gate anterior e o gate interno não são intercambiáveis. No caso CE, a retirada/inserção ocorre no subestado 1 de 004059C0, que arma 0040CDB0; a apresentação de movimento bloqueia a nova passagem e a conclusão ainda testa objetos no subestado 3. O registro de LP tem comportamento diferente: 00408470 usa sinal do handler, e 0047C560 chama 00573FB0 em parcelas de 100/10/1 durante Update, enquanto o handler aguarda. Assim a mutação nativa não está sempre toda concentrada no primeiro despacho nem é exclusivamente uma chamada de regras. A02 separa o delta lógico (uma vez) da contagem visual interpolada; outros comandos/continuações não podem observar um resultado intermediário por causa do FPS. Se houver evidência de uma regra que dependa desse intermediário, modelar explicitamente estágios lógicos, não consultar o relógio.

### Registro e limites

Quatro words little-endian: word0 = opcode nos 12 bits baixos, lado nos casos examinados no bit 15; preservar bits 12–14 e os três words brutos (+2/+4/+6) no adaptador de trace. Não confundir resource ID, índice DAT, chave da ocorrência e posição física. 005B9170 normaliza alguns argumentos; não assumir que os argumentos de todos os opcodes tenham a mesma representação. Buffer ativo 256×8 bytes; atual fora dele em A550A0. 00449D50 registra em contêiner dinâmico mesmo se buffer cheio; 005BC580 / 005B9370 alimentam replay separadamente. No Core não replicar perda silenciosa de comandos: fila extensível ou falha explícita de admissão. Replay nativo e sincronização de 005B9BF0/005BD2D0 ficam fora de A02.

## Mapa inicial (opcodes hexadecimais)

O mapa distingue operação nativa de proposta de tipo C#. Os nomes C# abaixo não são APIs existentes.

| Opcode | Semântica/limite nativo | Tipo ou evento proposto | Evidência |
| --- | --- | --- | --- |
| CE | Colocação de monstro da mão; +2 resource ID, +4 byte baixo posição da mão, alto contém destino; +6 flags | Summon/SetMonster (decodificação de variantes Pending) | assembly anterior + SQLite 004059C0; invocação observada |
| CF | Colocação de magia/armadilha, incluindo FIELD (região 10) | Set/PlaceSpellTrap; não é resolver o efeito | assembly anterior + SQLite 00405CB0; trace |
| 72 | Brilho/aviso de ativação, com ramos por região, identidade e fallback; não exclusivamente visual (ramo região 14 rearranja registro) | ActivationPresentation com comportamento de zona ainda Pending | assembly anterior + SQLite 00406B20; Burning Land observado |
| 1E | Brilho por identidade e escopo de lado, incluindo fallback | ActivationPresentation, variante distinta de 72 | assembly anterior + SQLite 0040AA40 |
| 43 | Dano, +2 magnitude, lado bit 15; produtor 005784E0 emite args (magnitude,0,1) | Damage(side, amount) | assembly anterior + SQLite 005784E0/00408470; Raimei observado |
| 57 | Etapa de apresentação de tributo por lado e slot; 005ABD00 emite 57 antes de chamar 00577B30 para movimentação; não é prova de retirada física pelo próprio 57 | TributePresentation; envio por custo separado | assembly anterior + SQLite 005ABD00/00407A90/004879A0/00577B30 |
| 8E | Movimento por chave, usado no descarte; +2/+4 origem/destino com lado, região e chave/posição; região 11 → 14 ou 15; valida ocorrência | Discard/Move (motivo explícito), não Destroy | estudo de custo + SQLite 00408EE0 |
| B1 | Sinalização emitida em 005BB720 estágio 4 e na negação por Big Shield Gardna; argumentos lado/slot e flag; significado integral do consumidor Pending | Pending, não atribuir genericamente a ativação ou negação | SQLite 005BB720; estudo de custo |
| 30 / 31 | Preparação de combate/apresentação de ataque no ramo seguido | AttackPresentation; 30 detalhes Pending | assembly anterior 005A2740/00407A20 |
| 3C | Retirada de monstro marcado em batalha para cópia temporária | BattleRemoval, não confundir com inserção final | assembly anterior 00405020 |
| 7C | Movimentação posterior da cópia ao destino, geralmente 14 | SendToGraveyard / finalização de batalha | assembly anterior 00405190 |
| 79 | Retirada/inserção por efeito, ex. Man-Eater Bug | Destroy/Move por motivo, variantes Pending | assembly anterior 00404B40 |
| 80 | Mudança de face no ramo examinado | Flip; gatilho posterior separado | assembly anterior 00407E60 |
| 01 / 02 / 03 | Início, limpeza, transição de turno; não equivalem a pedido legal de EndTurn | Turn stages (A07) | estudo de turno e replay |
| Draw, Heal, ChangePosition | Tipos C# necessários, opcode e variantes ainda Pending neste contrato | Draw (uma carta), Heal, ChangePosition | não inferir número pelo case do dispatcher |

Demais opcodes são Pending (não catalogados por A01); não interpretar como no-op. A05 poderá exportar um comando C# sem correspondência nativa como unmapped; não inventar words. Para os exemplos de combate, ver sequência e exceções no documento de pesquisa.

## Contrato C# para A02 (proposta normativa)

- Produtores (jogador, CPU, efeitos, console, testes) admitem comandos tipados, nunca escrevem diretamente nas zonas/LP. A origem só muda admissão; comando autorizado usa o mesmo executor, eventos e gate. Identidade de ocorrência estável, não apenas slot/ID. Resolver novamente no momento da execução; alvo desaparecido deve ser no-op explícito/rejeitado e registrado.
- FIFO, ativo único. Tick consulta gate de entrada, retira até um comando, executa estágio lógico e aguarda liberação do gate de conclusão. Um comando completo nunca reaplica sua mutação em outro tick. Ao concluir, não iniciar outro no mesmo Tick. Gate headless libera imediatamente; sem dependência de tempo/raylib. Não exigir ticks artificiais para imitar estados 0/1/2.
- Cada mudança observável tem granularidade explícita: Draw(1) por carta; Destroy por alvo; Damage/Heal por delta; Discard diferente de Destroy e SendToGraveyard. Ataque, FLIP, escolha e custos podem emitir vários comandos, não executar recursivamente. Granularidade C# não precisa ser bijeção de opcodes; nenhum intermediário relevante deve sumir.
- Continuação de efeito só retoma quando os comandos anteriores e eventual decisão estiverem liberados. A escolha deve ser validada, sem avanço do turno por engano; testes não dependem de interface. Não rodar outra continuação recursivamente dentro de execução. Quando o último comando termina, permitir ao controlador avançar sem começar outro comando naquele tick; escolha de prioridade mais ampla fica em A06/A07.
- Eventos/trace: registrar admissão, início, mutação/resultado (inclusive no-op), fim, identidade e origem; separar ticks/duração de ordem lógica comparável. Em A03, cliente consome a apresentação associada ao comando ativo e libera gate, não mantém uma fila independente de efeitos de regra. Em A05, separar representação tipada e adaptador de words nativos. Antes da migração A06/C12, os caminhos legados não passam a usar a fila por mágica.

## Matriz dos invariantes de PLANS

| Invariante | Evidência e grau de certeza |
| --- | --- |
| 1. Mudanças observáveis são comandos | CE/CF/8E mutam no consumidor (assembly anterior + SQLite). Generalização a todos os caminhos é contrato C#, hipótese nativa em aberto. |
| 2. Ativo único | Assembly anterior e SQLite 005B9CA0; caso local. |
| 3. Efeito dividido em eventos | Evidência de sequência 57 e CE, 30/31/3C/43/7C; várias retiradas; granularidade C# por alvo é contrato. Não provar ordem global de todas as destruições. |
| 4. Sem ação invisível / perfis preservam contagem | Gates e ordem visual confirmados para casos estudados; equivalência de perfis é requisito C#, não medição nativa. |
| 5. Continuação sem recursão | 005BC620, 005BB720, 005BC9B0 nos relatórios; SQLite 005BB720 corrobora produção de B1/callback. Append dentro de handlers diretos Pending. |
| 6. Gate injetável | Dois gates nativos confirmados; injeção/Instant é escolha C#, não comportamento original comprovado. |
| 7. Log comparável | Registro de oito bytes e contêiner/replay confirmados; correspondência C# parcial a construir em A05. |
| 8. Mutação na execução | Confirmado para CE/CF/8E; LP incremental no Update enquanto ativo (SQLite 0047C560 → 00573FB0). Generalização além dos casos e equivalência de granularidade permanecem hipóteses; C# aplica mutação no executor. |

## Casos de aceitação de A02 (a implementar)

| Cenário | Critério |
| --- | --- |
| A/B/C admitidos antes de Tick | Sem mutação na admissão; ordem FIFO; um novo comando no máximo por Tick. |
| Gate de entrada ocupado / de conclusão ocupado | Nenhum início / nenhum segundo comando respectivamente; mutação não repetida em ticks extras. |
| Destroy de três ocorrências | Três comandos e três mudanças distintas, na ordem da lista fornecida (ordem nativa de cada carta é contrato separado). |
| Alvo A removido antes de executar Destroy(A) | Sem destruir substituto no mesmo slot; resultado no-op/rejeição explícita. |
| Efeito que depende de compra/decisão | Retoma só após comando/decisão; nada recursivo; não avança turno enquanto aguarda. |
| Mesmo roteiro com gate imediato ou retardado | Mesmos comandos, eventos lógicos e estado final; só ticks/durações diferentes. |
| Fila vazia, comando ainda ativo | Continua ocupada até liberar; próximo comando só em nova passagem. |

## Lacunas e continuidade

A07: ligar o byte baixo de A577FE (índice da tabela de turno) aos bits de fase em A577FA e ao display. Os sete alvos das tabelas 005EFB78/005EFB94, drivers 005B7BD0/005BC5D0 e 005BC620 não estão em decompilations no snapshot consultado; o índice não é enum de fases. Roteiro 11 direcionado em [pending](../../scripts/pending/README.md) prepara coleta sem bloquear A02. Se assembly não resolver rótulos/condições, solicitar captura específica depois. Ponte indireta superior 00410B50 → cena, remoto, FLIP em ataque ponta a ponta, consumidor B1 e opcodes Draw/Heal/ChangePosition ficam explicitamente Pending; não impedem a fila local tipada. A02 deve começar por executor isolado/testes, sem reescrever DemoDuel de uma vez.
