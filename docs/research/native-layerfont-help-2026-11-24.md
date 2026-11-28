# V27/V28/V29 — Layerfont e ajuda

SQLite joey-knowledge-visual-decoded.sqlite, read-only, quick_check=ok.
SHA256 EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Layerfont Draws: decompilações transitórias do bundle
e589a5263234a28d0e93034d2d54abde536a784698fd592b6001ee7eef6414f7,
retry-012-00455FF0.txt e retry-013-00456360.txt.
Não há novo assembly nesta fatia. Cruzamento com construtores
00455D90/00456100 e chamador 00404580, todos persistentes.

## V27 e V28

005B9470 case 10h → 00404580 cria os objetos de Layerfont
no estágio zero, depois de 004516A0. 0044B990 retorna true
para DAT_005F3294=5 ou 7 (A); 0044B980 para modo=3 (B).
Não confundir o botão StartDemo do sandbox com modo nativo 5/7.
0044BEE0 recebe o modo na inicialização do duelo. Ambos carregam
layerfont.txt, usam Update vazio 004481F0; Draw altera estado.

A: contador inicial 0, incremento 4 módulo 256, antes de desenhar.
Recurso lookup 0. Intensidade C(2t-128)>>8 para t<64,
256 para 64<=t<=192, C(2t+128)>>8 para t>192.
Âncora (788,588) menos largura/altura; virtual +48.
B: dois campos inicialmente zero; Draw avalia 005BDAD0 !=0
e 0044BA10==0, soma 4 se verdadeiro e subtrai 4 senão,
saturando 0..256. Desenha apenas se valor>224, lookup 1,
virtual +4C. O decompilador perde EBX/EBP e pilha:
X=525-largura/2 e Y=((altura+16)*C(4*(v-224)-128)>>16)-altura
são a leitura provisória dos operandos de dimensão, não
assembly confirmado. Centro 525 é comprovado pelo bundle 01.
005BDAD0: gate 4000 desligado e soma do campo assinado
de quatro bits 21..24 de A58260 com predicado de turno
(A577F8 byte2, bit4 e lado relativo) >0. 0044BA10 testa
virtual +4 dos objetos em 005F3280+132C/1330.
O modelo aceita predicates/fatos do chamador; não implementa
IPresentationGate nem lê regras/estado Core.

A branch analise-visual (30e63ac, resource-inventory.csv)
confirma layerfont.txt com 29 bytes e prefixo demo; registra
demo_font_e.bmp 233x30 e wait_e.bmp 469x34, variantes f/g/i/s.
Não prova ordem ou texto integral do manifesto. O parser
NativeLayerFontResources exige exatamente dois BMPs existentes
em ordem do manifesto, resolve '?' para inglês como política
do cliente. Não adivinha nomes ou fallback quando faltam dados.

Modo isolado --review-layerfont=a ou b requer --native-cosine=
capturado. F7 executa uma chamada Draw lógica; F5 altera o
predicado B; F8 reinicia; F12 PNG/JSON. Redesenho da janela
e consulta de Sample são puros. Usa recursos originais do DAT,
não validado neste ambiente; compositores +48/+4C, mapeamento
dos registros de B e cor-chave ainda não provados.
Nenhuma integração com modo de duelo ou scheduler. Não aumentar
badge até haver uso no fluxo normal com contrato suficiente.

## V29 ajuda — evidência cumulativa v2

0044C3E0 cria 0045E250 com outros objetos persistentes do duelo.
Construtor carrega help/help.txt, vtable 005DB630, camada 0xF00,
obj+10=-1, +14=0, global 005EBEE4=-1. O snapshot v2 (SHA256
80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
integrity_check=ok) contém Update/Draw no bundle
87ff09fe3b5b864c91373efc4c7dea874cab9c547ab47eb12dafded9401ee888,
retry-002-0045E3D0.txt, retry-003-0045E4A0.txt, assembly indexado.
Não repetir roteiro 19.

### Produtor e Update (assembly confirmado)

0045E240 grava uma única chave em 005EBEE4 (não é FIFO).
Xrefs em 0056B5A0, 0056B7D0, 0057E490, 0057E650 e outros.
Decompilação dos produtores indica feedback contextual de
disponibilidade de ações: 0056B5A0 solicita 3, 0x10 ou 0x12
em caminhos que retornam falso; 005929D0 solicita 9. Não
atribuir texto aos IDs antes de resolver o manifesto.

0045E3D7–0045E3E5 incrementa +14 em 4 enquanto <256.
005F2FDC deve ser não zero e 005F2FE0 zero; senão fecha.
00418B50 copia a palavra contextual. Quando já visível, mantém
somente se chave e bits 0..13 da palavra coincidirem; a troca
fecha imediatamente, sem reabrir no mesmo Update. Quando oculto,
aceita chave não negativa do lado local, exceto 9 que aceita ambos,
e virtual +2C de 005F33E0 OU 005664A0(6). Ao aceitar, guarda a
palavra e zera o contador. Global de pedido volta a -1 em todas
as saídas. Portanto o produtor precisa renovar o pedido a cada
Update. Os predicados permanecem fatos fornecidos pelo chamador.

### Draw e geometria (assembly confirmado)

0045E4B4–0045E4D2 passa três argumentos a 0045C140:
lado=word&1, zona=(word>>1)&31, índice=(word>>6)&255.
O retorno EAX/EDX fornece X/Y. A decompilação perde o terceiro
argumento e vários registradores. A altura-base H vem de
005F3000/2 se zona=11, senão 005F3008; Y0=Y-H/2.
0045E508–0045E528: escala 8.8 = C(8*t-128)>>8 se t<16,
256 senão. Após quatro Updates desde a abertura, altura cheia.

0045E538–0045E574 resolve a chave no manifesto e consulta dimensões
via virtual +38 (largura w, altura h). 0045E577–0045E5B7:
X0=min(X,800-w/2), Y1=max(Y0,h), h2=trunc(h*escala/256).
0045E5BB–0045E62D resolve novamente a chave e chama +4C
com X0-w/2, Y1-h/2-h2/2, par (w,h2), outros argumentos zero.
Divisões inteiras truncam para zero. Não há clamp esquerdo
neste trecho. O tamanho original é independente da abertura.
NativeHelpTimeline modela esse recorte com amostra pura e
Update explícito. Testes sintéticos cobrem ambos os lados,
chave especial 9, identidade, renovação, gates, abertura e
clamps; não são comparação com o original.

### Lacunas

Manifesto help.txt (1119 bytes lógicos no inventário histórico),
textos/variantes, remap das chaves, dimensões reais, origem dos
predicados, compositor +4C e fornecimento do contexto no cliente.
Não criar mensagens por aproximação nem ligar ao Core por nome.
A Draw segue Pending no inventário; V29 é Partial (modelo isolado).
Sem fila paralela, sem gate de conclusão.

### V29.2 — banco de ajuda e resolvedor padrão

Em 27/11, o manifesto de 1.119 bytes e os metadados de imagem
foram conferidos no SQLite v8 e no DAT local. O manifesto tem
24 linhas de recursos em ordem, de `help01_?.yga` a
`help24_?.yga`. O campo após `//` é comentário. O construtor
`0045E28B` chama `00549520` para o subobjeto em `+1C`.
`005495B6` instala a vtable `005DC9F4` no resolvedor padrão
e `005495CD` guarda essa instância em `+50` do subobjeto.
O slot zero de `005DC9F4` é `0043A5D0`, cujo único
comando é `RET 4`. Assim, a chamada de `0045E53F` com um
ponteiro para o índice deixa o valor intacto nesse caminho.
`0045E541…0045E564` usa o índice resultante para carregar e
buscar a entrada da lista, com stride de oito bytes.

O relatório `--inspect-visual-dat` lista esse índice no campo
`helpRequests`. Por exemplo, o seletor `09` é a décima linha
(`help10_?.yga`) e `17h` é a última (`help24_?.yga`).
Estão presentes 136 YGAs, com 24 variantes para cada sufixo
`e/f/g/i/s` e 16 para `j`. O DAT omite `help17_j` a
`help24_j`. O relatório sinaliza a ausência e guarda
dimensões e erros de decodificação dos recursos encontrados.
As variantes inglesas medem de 164 a 250 pixels de largura
e todas têm altura 38. A comparação cobre a ordem, o
resolvedor padrão e os metadados do banco. A substituição efetiva
de idioma e a composição em tela aguardam observação no original.

## A03

Draw-driven != Update-driven. A03 continua dona da ponte,
não avançar fase visual em CanComplete. Os objetos de Layerfont
são criados pelo opcode 10h (não se trata de um comando de texto
separado) e podem exigir vida útil independente do gate.
Não criar FIFO visual nem mapear modo nativo para modo de
duelo do sandbox por nome. Cobertura ainda 20/34, 0 Verified.


## V29.1 — produtores da ajuda contextual (24/11)

SQLite cumulativo v2, SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4, quick_check ok, leitura somente. Consultados 15 xrefs a 0045E240, decompilations dos produtores e card_identities; assembly de Update/Draw já recebido no bundle 19. Os produtores seguintes são portas de **decompilação**, não uma nova prova de assembly.

| Chave raw | Produtor | Condição observada (não é o texto da imagem) |
|---|---|---|
| 0 | 005AAB90/005AAE00 | contagem de tributos elegíveis <1 para nível 5/6 ou <2 para demais níveis altos |
| 3 | 0056B5A0 | flag 20000 no slot ocupado |
| 4 | 0057E490/0057E650/005A0530/005A0800/005A08F0 | probe de ativação; outros checks podem substituir |
| 7/8 | 005A0530 | fase raw 3, bit do slot em 00A55D70 limpo/set; 7 antecede 0056B730 |
| 9 | 005929D0 | zona hover 0..11, sem teste de lado |
| 0B | 005AAB90/005AAE00 | 0056A000==0 (sem zona elegível) ou 005AAB30 falso depois de contar tributos |
| 10 | 0056B5A0/005AAB90/005AAE00/etc | 0056B440 retorna não zero (Prohibition, ID 48A) |
| 11 | 005AAB90/005AAE00 | falha de 00570A70/00570B40 (restrições de invocação) |
| 12 | 0056B5A0 | grupo de restrições de posição (ver abaixo) |
| 13 | 0056B7D0 | saída comum de restrição de ataque; não atribuir motivo único |
| 14 | 0057EAC0/0057E490/0057E650 | restrição associada a tipo/carta, Jinzo/Royal Command ou Cold Wave |
| 15/16 | 0057E880 | restrição de resposta/ativação, dois pontos distintos |
| 17 | 00570A10 via 005AAB90/005AAE00, 0057EAC0 | custo deve ser estritamente menor que LP (inclui Chain Energy) |

0056B5A0: slot vazio retorna sem write; flag 20000 gera 3; grupo de
005709D0(3CA), 0056D790(15C/4DC), 005699C0(536) com exceção
do próprio 536, 0056DB30(540), 005699C0(148) condicionado a
bits de slot e 0056F6D0 -> 12; só depois 0056B440 -> 10.
IDs confirmados: Mesmeric Control, Spellbinding Circle, Shadow
Spell, Thousand-Eyes Restrict, Goblin Attack Force, Dragon Capture Jar.
Não copiar regras em LookAndFeel: receber fatos agregados do chamador.

005AAB90: zero/não monstro não escreve; Prohibition ->10;
00570A10 false ->17 (00570A10 também testa Prohibition e
Chain Energy ID436, 500 por ocorrência); 00567A00 não zero
retorna sem write. 00570A70 falso ->11 mas continua; pode
ser sobrescrito por 0 ou 0B nas restrições de nível/tributo/zona.
0056A000 conta slots vazios elegíveis através de 00569F70;
0056A200/290 contam tributos com filtros diferentes (inclui
Mask of Restrict); 005AAB30 testa zona existente ou liberável.
00570A70 testa flag 100000 e Jam Breeding Machine/Last Warrior;
00570B40->00570AF0 usa flag 200000 e Jam/Jowgen/Last Warrior.
Não juntar 005AAB90 e 005AAE00: latter possui casos de IDs especiais
e saídas distintas. A porta só cobre as escritas do primeiro, não
máscara de ações permitidas. 0057EAC0 usa lado por XOR dos bits
0/14 do descritor; custo de 0057C450 + Chain Energy quando zone=11.
Não duplicar cálculo de custo ou semântica de cartas.

NativeHelpMailbox representa 005EBEE4 com sentinela -1, overwrite
e Consume reset. O produtor sem write preserva o pedido anterior;
key 0 é válido. Range 0..23 é a política do modelo limitada às
chaves observadas, não uma checagem feita por 0045E240.
NativeHelpProducers recebe resultados de predicados, não estado Core.
Testes sintéticos cobrem precedência, ausência de write, lado,
limiares e igualdade de custo. Não substituir o cálculo legal do
cursor nem integrar ao App antes de resolver o manifesto.

Índice versionado data.dat-index.json: 24 YGA ingleses
help01_e.yga..help24_e.yga; não supor key+1 até inspecionar
help.txt (pode ter parâmetros/remap). O comando já existente
--inspect-visual-dat inclui help.txt e dimensões/hash; não abrir
nova coleta Ghidra, não solicitar assets/pixels. Draw continua Pending.

## V29.3 — primeira integração de ajuda no duelo (27/11)

Fonte privada cumulativa v7, SHA256
`a210880f69a0997492d81dd00e405e1e306d6cc4201f4948d7b8855e4236d379`,
PRAGMA quick_check=ok. O manifesto help.txt em Shift-JIS confirma
que o comentário da décima linha (chave 9) é 対象に出来ません
("não pode ser alvo"). `help10_e.yga` mede 179×38 no inventário
do DAT, sem erro de decodificação. A chave não é uma dica de hover
genérica. As decompilações de 00593440, 00591040 e 005A9500
chamam 005929D0 durante a escolha de alvo, antes de verificar
o retorno do input. 0040BAD0 escreve 005F2FE0=1 quando o
descritor de foco coincide com o selecionável; 0044DBC0 zera
os dois globals de foco por iteração. A dimensão 005F3008 é
obtida em 00418560 consultando o banco mini via +38, não a
altura do retângulo de destaque.

O App liga apenas a chave 9 a cartas ocupadas não elegíveis nas
seleções não comprometidas de equipamento, magia direcionada
a monstro e magia direcionada a S/T. Reutiliza os predicados
existentes e os centros de campo. O adaptador de foco/seleção
é provisório: não demonstra equivalência do virtual +2C ou
grupo 6. A timeline mantém o pedido a cada update, fecha quando
o alvo muda, e a amostra usa a altura da miniatura e o DAT
inglês. Instant mostra a mensagem inteira. Não há alteração de
regra, divulgação de identidade oculta, nova espera ou FIFO.
O renderer usa a política YGA existente e composição Raylib,
não a implementação exata do +4C.

Checks adicionados: chave/ordem do manifesto e rejeição de asset
ausente. Sem SDK .NET no ambiente desta alteração; build, CI,
captura pareada e teste interativo dos seletores ainda pendentes.
Inventário/badge permanecem Pending até validar a integração.

## V29.5 — aviso de tributo no fluxo real (28/11)

O snapshot cumulativo v14 foi aberto somente para leitura, quick_check=ok.
O manifesto confirma a primeira linha help01_?.yga, comentário de
tributo necessário; a variante inglesa e help10_e.yga medem 179×38.
A captura do roteiro 70 na main observou a chave 0 e descritor 0x8117
após clique em Flame Cerebrus sem tributo; hover isolado não disparou.
A timeline já modelava o contador 0..256, incremento 4 e encerramento
por pedido -1. O cliente usa a consulta de falta de tributos da própria
legalidade de invocação normal, registra a ocorrência quando um clique
não admite ação e renova apenas enquanto o foco e a condição persistem.
O descritor usa região 11, lado e índice; a âncora usa HandBounds e a
altura 290 do banco grande descrita por HandPresentation. Não cria
comando, nova espera, mutação ou cálculo de tributos em LookAndFeel.
A equivalência de foco, a precedência de outros avisos, as restrições
não modeladas e a composição +4C seguem pendentes. Testes sintéticos
adicionam chave 0, manifesto, geometria e encerramento; build/CI e
revisão interativa ainda não executados nesta sessão. Cobertura oficial
continua Pending até validação do PR #263.

## V29.6 — validação do cliente e comparação

No Alpha em Xvfb a 800×600, o DAT original mostrou o aviso de
tributos ao clicar em Red-Eyes com o campo próprio vazio. Tirar o
cursor da carta removeu o aviso. O PNG nativo do roteiro 70 com
Flame Cerebrus confirmou o mesmo recurso inglês, a ancoragem
acima da mão e a saída por perda de foco. As cartas ocupavam
slots diferentes. A comparação é visual, sem pareamento de
pixels ou contadores de abertura.

Na seleção de Mystical Space Typhoon do Alpha, mover o cursor
para o Axe Raider com uma armadilha elegível em campo mostrou
o aviso de alvo inválido acima do monstro e manteve a seleção.
O texto, o tamanho 179×38 e a chave 9 conferem com o conjunto
DAT/assembly nativo. A comparação em execução dessa chave
continua pendente no roteiro 75. O cenário local foi preparado
com edições de cenário do console vivo, seguido pelos cliques
normais do jogador. A cobertura passa a Partial.
