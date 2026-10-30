# Rituais — tabela de magias, monstros e níveis

Inventário estático em 2026-10-29, mesmo executável SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, base 0x00400000. Nenhuma execução do jogo ou gravação do banco.

## Resultado

As **16 cartas da combinação P269** têm correspondência exata com uma tabela
compactada no EXE. Cada entrada liga uma magia de Ritual a seu monstro e
ao nível total exigido. Todos os requisitos também coincidem com os níveis
base dos respectivos monstros nos recursos locais.

| Magia | Monstro | Nível exigido |
|---|---|---:|
| War-Lion Ritual | Super War-Lion | 7 |
| Zera Ritual | Zera The Mant | 8 |
| Black Luster Ritual | Black Luster Soldier | 8 |
| Beastly Mirror Ritual | Fiend's Mirror | 6 |
| Commencement Dance | Performance of Sword | 6 |
| Hamburger Recipe | Hungry Burger | 6 |
| Novox's Prayer | Skull Guardian | 7 |
| Resurrection of Chakra | Chakra | 7 |
| Turtle Oath | Crab Turtle | 8 |
| Garma Sword Oath | Garma Sword | 7 |
| Javelin Beetle Pact | Javelin Beetle | 8 |
| Fortress Whale's Oath | Fortress Whale | 7 |
| Revival of Dokurorider | Dokurorider | 6 |
| Dark Magic Ritual | Magician of Black Chaos | 8 |
| Black Illusion Ritual | Relinquished | 1 |
| Curse of the Masked Beast | The Masked Beast | 8 |

## Layout e consumidores

Tabela em **0x005ED060**, offset de arquivo **0x001ED060**, palavras uint32:

| Bits | Conteúdo |
|---|---|
| 0–12 | ID de recurso do monstro |
| 13–25 | ID de recurso da magia |
| 26–31 | Nível total exigido |

Após 16 entradas, a palavra zero em **0x005ED0A0** termina a tabela.
Não interpretar os campos zero da sentinela como uma carta de ID zero.

**0x00598ED0** converte o índice de magia para ID e busca nos bits 13–25.
**0x00598F30** procura na mão o monstro indicado nos bits 0–12, acrescentando
a verificação por 0x0056B440. **0x00599850** é a disponibilidade compartilhada:
consulta condições globais, presença do monstro, posições e materiais.
O callback principal **0x005999D0** usa a mesma entrada em uma máquina de
estados para seleção, pagamento e preparação da colocação.

Para Black Luster Ritual: índice **145**, ID **669**, registro **122**,
entrada **2**, palavra **0x2053A16B**. A entrada seleciona monstro ID
**363**, Black Luster Soldier, e requisito **8**. A descrição local exige
tributos da mão/campo com soma igual a 8 ou mais, corroborando o requisito.

O acessor de nível **0x004026C0** usa bits **25–28** de CARD_Prop para os
tipos dos monstros examinados. Ele possui ramos especiais para outras classes;
o catálogo não generaliza essa extração para todas as cartas.

Esse caso acrescenta uma forma de parametrização: os callbacks são código
compartilhado, mas o par magia/monstro e o requisito estão em dados compactados
do EXE. Os nomes e propriedades usados no cruzamento vêm do DAT.

## Reprodução e limites

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s YugiRE/tests -p 'test_ritual_rules.py'
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/catalog_ritual_rules.py
```

Esperado: **seis testes** do parser passam sem arquivos do jogo. O gerador
imprime dois `PASS`: preservação dos originais/banco e cruzamento da tabela
com os 16 registros de callbacks, identidades e níveis base. Exporta somente
metadados em [ritual-rules-catalog.json](../evidence/ritual-rules-catalog.json).

Evidências: [callback/disponibilidade](../evidence/ritual-primary-and-availability.txt),
[busca, mão, materiais e nível](../evidence/ritual-map-and-materials.txt),
[instruções](../evidence/ritual-level-and-binding-instructions.txt).

O catálogo não valida execução da função nativa. A continuação abaixo avança
a seleção, exclusão de uma cópia e encaminhamento do pagamento. Colocação
final e sincronização ainda exigem seguir os helpers.

Consulta final do checkpoint: [0x00599570](../evidence/ritual-level-subset-search.txt)
faz busca recursiva por níveis. Com nível candidato L, requisito restante R e
limite M, admite diretamente `R <= L < R + M`; para `L < R`, pode procurar
entre entradas anteriores com `R-L` e `min(M,L)`. Comparações e chamada
recursiva foram conferidas no listing. A relação desse critério com todas as
escolhas permitidas ao jogador permanece em investigação pelo chamador
0x00599140. Não foi executado teste nativo desse algoritmo. Timing permanece futuro.

## Continuação: critério numérico da seleção

O estado mantém **R**, nível restante, no byte 0x00A55C8B, e **M**, menor
nível já selecionado, no byte 0x00A55C8C. A entrada do Ritual inicializa
R pelo requisito da tabela e M=255. A seleção de uma carta de nível L faz:

```text
R = max(0, R - L)
M = min(M, L)
```

Isso aparece tanto no modo automático de 0x005999D0 quanto no manipulador
interativo **0x005995E0**, chamado por 0x00441AD0. No trecho de mão, as
instruções 0x005996EE–0x0059972D confirmam subtração, saturação e mínimo.
O retorno do manipulador testa R=0. Há guardas de interface e confirmação
antes de atualizar o estado; não equivale a selecionar só porque o cursor
está sobre uma carta.

O filtro 0x00599140 admite diretamente `R <= L < R+M`; fora disso prepara
níveis restantes e chama 0x00599570 com `R-L` e `min(M,L)`.
A transcrição numérica foi comparada com enumeração independente de todos
os subconjuntos, para requisitos positivos, níveis não negativos e valores
sem overflow. Nesse domínio, a existência de uma conclusão aceita equivale a:

```text
soma >= requisito restante
soma < requisito restante + min(M, níveis dos novos materiais)
```

Para uma seleção completa desde o início, com requisito T e soma S, isso
corresponde a `S >= T` e `S - menor_nível < T`: retirar qualquer material
já faria faltar nível. Portanto evita materiais redundantes. **Não exige a
menor soma entre todas as combinações disponíveis.** Exemplos de nível 8:

- 6+4 é permitido numericamente: retirar qualquer um deixa menos de 8.
- 8+4 é redundante; começar pelo 4 não tem conclusão válida usando só o 8.
- 6+6 pode ser permitido mesmo existindo outra carta de nível 8 disponível.

A equivalência é uma interpretação matemática do modelo e das comparações
nativas, não execução do binário. Não inclui elegibilidade de cartas, limites
de arrays, filas pendentes ou todos os contextos de interface.

## Materiais, cópias e pagamento

**0x00598FF0** soma níveis da mão e exclui exatamente uma ocorrência do
índice do monstro de Ritual. Outras cópias continuam na soma.
**0x005990A0** percorre cinco posições de cada lado, consulta 0x0056A140 e
exige face aberta para posições adversárias. Essas somas são usadas pela
disponibilidade; não substituem a busca combinatória durante a seleção.

**0x0056A140** exige posição ocupada até 4 e ausência do ID 0x58A
(**Mask of Restrict**). Para posição do outro lado, procura por 0x0056DA10
uma associação com ID 0x4C4 (**Soul Exchange**) e confere um byte de lado.
[Produtor da associação e restrição de combate](soul-exchange.md).
Isso explica por que alguns loops incluem ambos os lados; não significa
permissão irrestrita para tributar qualquer monstro adversário.

No filtro de candidato da mão, só o lado do ator é aceito. Se a carta é o
próprio monstro esperado pelo Ritual, o contador de cópias precisa ser pelo
menos dois, preservando uma para a colocação. Sem posição livre própria,
o candidato de mão é recusado, e o candidato de campo deve ser próprio e
passar por 0x00569E10 para liberar lugar.

Há uma sutileza ainda não generalizada: na montagem dos níveis adicionais
de campo em 0x00599140, a comparação de lado no pseudocódigo usa o lado do
**candidato**, não simplesmente o lado do ator. A equivalência matemática
acima vale para o array fornecido, não comprova que o conjunto de candidatos
seja idêntico em todos os contextos. Esse ramo merece conferência adicional
no listing antes de afirmar uma política global de elegibilidade.

No modo interativo 0x005995E0, o caminho de mão encaminha a carta para
0x00575AF0; o de campo chama **0x00577B30**. Este monta pedido temporário e
máscara de uma posição, e chama 0x00576960 com **destino 14** e **flags
0x1000**, reaproveitando o mecanismo de retirada já estudado. O manipulador
atualiza R/M antes dessas chamadas; não supomos que as filas de apresentação
já tenham terminado naquele instante.

O modo automático 0x005999D0 tenta cartas da mão e depois cinco posições do
próprio campo; não percorre o campo adversário nesse trecho. Quando R=0,
passa ao estado 100, depois procura a cópia preservada, guarda-a e encaminha
para 0x005ADB10 no estado 99. A colocação final e a sincronização dessas
etapas ainda não estão fechadas. Não concluímos que todos os modos tenham
as mesmas escolhas somente por compartilharem o filtro.

## Verificação desta continuação

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s YugiRE/tests -p 'test_ritual_selection_model.py'
```

Quatro testes incluem enumeração de 10.912 configurações de busca e 10.200
configurações de candidato obrigatório, além de exemplos e progresso R/M.
O modelo fica em `scripts/ritual_selection_model.py`, isolado da aplicação
.NET e sem executar funções originais. Não valida overflow, todos os limites
de nível possíveis, seleção gráfica nem a aplicação real de tributos.

Evidências novas: [seleção e somas](../evidence/ritual-selection-progress.txt),
[elegibilidade e instruções de progresso](../evidence/ritual-eligibility-and-progress-instructions.txt).

## Colocação: modo 5 e comando 0x70

O estado 99 de 0x005999D0 chama
`0x005ADB10(lado, &0x00A55C90, 1, 1, 11, posição_mão_preservada)`.
O helper exige posição livre por 0x0056A030 e prepara contexto compartilhado:

| Endereço | Conteúdo seguido |
|---|---|
| 0x00A55080 | Lado, posição livre, local de origem e flags |
| 0x00A55084 | Índice do monstro nos 12 bits baixos |
| 0x00A55088 | Cópia do registro preservado |
| 0x00A5508C | Argumento de flags OR 4; no Ritual, valor 5 |
| 0x00A5508E | Controle: força bits 0x14 e limpa bit 3 |

0x005AD8C0 limpa o progresso, ativa bit 0 e preserva o seletor de modo.
Não coloca a carta diretamente: prepara trabalho posterior. Pode também
encaminhar um contexto de 20 bytes por 0x005BD090 conforme modo global.

A entrada transitória **0x005AB820**, identificada após nove NOPs que seguem
o RET de 0x005AB816, lê esse controle. O dispatcher em 0x005AB868 calcula:

```text
índice = ((controle >> 2) & 7) - 1
```

O contexto do Ritual resulta em **modo 5, índice 4**. A tabela em
0x005ABCA8 aponta para **0x005AB8CF → CALL 0x005AD210**. Não confundir com
0x005AD350, que pertence ao índice 5/modo 6: apenas olhar a ordem dos helpers
sem o decremento levaria ao ramo errado.

0x005AD210 passa por escolha de posição/flags, interativa ou automática,
e encaminha os estágios posteriores a **0x005ACF10**. No estado 3, para o
monstro comum de Ritual, esse helper emite **0x70** em 0x005AD05B/0x005AD05D:
transporta lado/posição de destino, chave de ocorrência do registro preservado
e duas flags. Há um caminho separado para fichas, fora do caso Ritual.
No estado seguinte, testa ocupação e emite comandos/eventos posteriores.
Os significados completos das flags de posição e os gatilhos não foram
reconstruídos nesta continuação.

0x70 normaliza para **70 decimal**, stub **0x005B962C**, consumidor
**0x004063E0**. No estado 2 do consumidor:

1. Localiza a ocorrência por **0x005687D0**, usando a chave transportada.
2. Obtém o registro por 0x005700F0 e copia para temporário 0x00A558B0.
3. Chama **0x00570790** para retirar a origem encontrada.
4. Chama **0x005708D0** para inserir no destino transportado pelo comando.
5. Atualiza apresentação e flags por helpers compartilhados.

Isso liga o Ritual ao mecanismo de transferência da ocorrência preservada
para o campo, em vez de fabricar um índice novo na posição. Para o caso
normal, a origem esperada é a mão já examinada; não assumimos comportamento
seguro se a chave deixar de existir antes da resolução. A busca, as rotinas
de retirada/inserção e seus endereços já aparecem nos estudos de movimentação.
Timing, cancelamento, destino invalidado e todas as respostas possíveis
continuam para análise adicional ou validação dinâmica futura.

Evidências: [preparação](../evidence/ritual-final-placement-driver.txt),
[inicialização](../evidence/ritual-placement-state-machine.txt),
[dispatcher em instruções](../evidence/ritual-placement-mode-dispatch-instructions.txt),
[modo e seleção](../evidence/ritual-placement-mode-references.txt),
[estágios e emissão](../evidence/ritual-special-placement-route.txt),
[consumidor](../evidence/ritual-command-70-consumer.txt).
As consultas auxiliares `ritual-placement-dispatch.txt`,
`ritual-placement-mode-five.txt` e `ritual-placement-global-references.txt`
preservam também os ramos inspecionados e descartados como rota principal.

`python3 scripts/validate_ritual_placement.py` confirma fingerprint, seletor,
tabela de seis saltos, quatro CALLs e despacho 0x70 diretamente nos bytes.
[Relatório](../evidence/ritual-placement-validation.json).
Nenhum desses checks executa o jogo ou comprova o timing dos estágios.

## Conferência das condições de campo

O listing confirmou a comparação de lado do candidato, antes deixada como
dúvida do decompilador. Após SUB ESP,0xB8 e quatro PUSHes na entrada de
0x00599140, os argumentos estão em ESP+0xCC (pedido), +0xD0 (lado do
candidato), +0xD4 (região), +0xD8 (posição). Em 0x005994A4, o CMP usa
**ESP+0xD0 contra EDI**, o lado sendo percorrido. No lado correspondente,
exclui a própria posição quando região=0; no outro lado, exige bit 1 do
byte de face. O array aceita no máximo 42 contribuições, incluindo a parte
de mão já acumulada. Isso confirma a leitura local, sem provar alcance de
todas as combinações em execução.

Exemplo puramente de modelo: ator no lado 0 e candidato elegível no lado 1.
Um material adicional virado para baixo no lado 0 será excluído desse array;
um material no lado 1 poderá entrar sem esse teste de face, ainda sujeito a
0x0056A140. O candidato adversário em si exige face aberta no filtro inicial.
Portanto não se deve substituir a montagem pelo atalho “todos os próprios
mais adversários abertos” em uma reprodução fiel. Não classificamos isso
como bug: contexto, disponibilidade e condições de associação ainda limitam
os casos possíveis.

0x00569E10 também foi seguido: verifica associações de tipo 2 que apontem
para **Ground Collapse, ID 0x528**, face aberta e flags permitidas; o byte de
metadados precisa corresponder à posição. Depois percorre registros de dois
blocos com marcador 0x40 e coordenadas de lado/posição, também capazes de
impedir o retorno verdadeiro. O papel completo desses registros continua
aberto. Esse helper explica por que “tributar próprio monstro” não basta
para garantir espaço utilizável no teste de disponibilidade com campo cheio.

A busca 0x0056DA10 usada para Soul Exchange percorre associações de tipo
3 ou 11–13 e
compara o ID da origem referenciada; a confirmação de entrada em 0x005AA480
consulta máscara 0x201 após atualizar o estado de entrada por 0x004182D0.
Não atribuímos botões físicos específicos a essa máscara nesta rodada.

Novas evidências: [filtro/espaço](../evidence/ritual-field-eligibility-detail.txt),
[argumentos/associações/entrada](../evidence/ritual-remaining-material-array.txt).
O modelo ganhou duas verificações de montagem de contribuições e limite,
passando a seis testes de seleção; elas não executam nem o filtro de
associações nem o original.

## Continuação e encerramento do callback

O executor compartilhado **0x005BB720** distingue dois estados no DWORD
0x00A55C88:

| Campo | Uso neste caso |
|---|---|
| Bit 0 | Flag de resolução ativa, consultada pelo wrapper 0x005BBE40 |
| Bits 1–7 | Etapa do executor genérico |
| Byte +2, 0x00A55C8A | Continuação privada do callback |
| Byte +3, 0x00A55C8B | R do Ritual, não etapa genérica |

Na etapa genérica 4, o executor inicializa a metade alta com 0x0080: o
callback começa com continuação 0x80 e o byte R inicialmente zero. Na etapa
6, **0x0059DEB0** procura o registro da carta e chama seu slot principal.
As instruções 0x005BBA78–0x005BBA99 mostram uso de **AL**, com máscara 0xFF,
para substituir apenas o byte de continuação, preservando R. Se esse byte
for diferente de zero, mantém a etapa 6; zero marca uma flag global e avança
o executor. Retorno não é simplesmente “sucesso/falha”. Não interpretar bits
altos de EAX recuperados pelo decompilador como parte desse protocolo.

No Ritual, a sequência comum é:

```text
0x80 -> 0x78 automático, ou 100 depois de abrir seleção interativa
0x78 -> 0x78 enquanto R != 0; ao terminar -> 100
100  -> procura/copia o monstro preservado -> 99
99   -> prepara o contexto de colocação -> 98 (0x62)
98   -> ramo default do callback -> zero
```

As instruções de 0x00599BB4 confirmam CALL 0x005ADB10 e retorno AX=0x62.
A etapa 98 não é uma chamada explícita ao consumidor 0x70: a colocação
segue pelo contexto de invocação já documentado. Não concluímos apenas
por essa sequência quando o próximo polling ocorre em relação à animação,
a fila ou à janela de resposta. O wrapper 0x005BBE40, por sua vez, processa
os pedidos ativos e alimenta a resolução; foi encontrado como chamador em
uma etapa de outro contexto compartilhado, 0x005AC310.

0x005BA2D0 pode desviar a chamada na etapa 6: depende do bit global 0x10 em
0x00A577FA e de bits do pedido comparados a 0x00A54E5C. O mesmo predicado
pode encaminhar cópias de pedidos por 0x005BD090/0xF071 na etapa anterior.
Este caminho é registrado numericamente; não precisa ser suposto como
cancelamento local nem como validação do alvo.

## Interface: guarda de apresentação não prova cancelamento

0x00441AD0 atualiza sua base, verifica campos do objeto e consulta
0x0041CEC0 → **0x0041D050** antes de chamar o seletor interativo 0x005995E0.
Este último helper de guarda chama 0x0041D0C0 e métodos virtuais de vários
objetos; retorna 1 quando encontra um estado pendente. Portanto, o early
return não deve ser nomeado como “cancelar Ritual” nesta análise.

Quando o seletor retorna R=0, 0x00441AD0 chama **0x0043E0A0(0)** para
encaminhar o fechamento. Esta rotina altera campos do objeto e chama
0x005BFCC0, que grava o valor em 0x00A558B4 e atualiza a seleção global por
0x005BDBD0. Nenhum reembolso de materiais ou restauração de R/M foi visto
nesse caminho. Isso não prova ausência de cancelamento em outros handlers.
A validade de cancelar após o primeiro tributo continua pendente para
identificação de um chamador específico ou teste dinâmico futuro.

Evidências: [entrada e referências](../evidence/ritual-callback-resume-and-input.txt),
[executor](../evidence/ritual-generic-resolution-driver.txt),
[wrapper e fechamento](../evidence/ritual-resolution-gates.txt),
[guardas e predicado](../evidence/ritual-ui-and-resolution-synchronization.txt),
[instruções de continuação](../evidence/ritual-callback-state-instructions.txt),
[referências de polling](../evidence/ritual-placement-polling-references.txt).
A ausência de xrefs diretos para 0x005AB820 no banco atual não demonstra
que o dispatcher não seja chamado: sua entrada foi recuperada por decoding
transitório e há chamadas/tabelas que o banco ainda não tipou.
