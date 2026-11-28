# V31/V18 — reações do Joey, pedidos sonoros e duração

SQLite privado v2 SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check=ok (leitura em 24/11). Fonte: decompilations, refs, pointers,
functions, visual_evidence_memory/instructions/transient_decompilations.
O snapshot v2 era a fonte do classificador e da duração. O complemento
de 27/11, abaixo, incorpora o assembly do Draw e o cruza com o DAT local.

## V31.1 — quadrilátero base e término da reação (27/11)

Assembly da coleta 35 no SQLite v9, com fingerprint conferido no EXE
local. Em `00416C48..00416D47`, a reação lê os inteiros dos cinco
primeiros registros de 8 bytes. Cada inteiro fornece `x = valor / 1000`
e `y = valor % 1000`, ambos com sinal. A chave 0 fornece a origem e
resolve o bitmap via `00417F40`. As chaves 1, 2, 4, 3 fornecem,
nessa ordem, os vértices de origem. A segunda lista soma a origem a
cada vértice. `00416D47` entrega as duas listas, o bitmap e a quantidade
4 a `005203C0`.

`--inspect-visual-dat` registra esses argumentos em
`bustUpReactionGeometry`, com dimensões e hash do bitmap base quando
encontrado no DAT. Nos dez manifests locais, os máximos das coordenadas
de origem são largura−1 e altura−1 do bitmap. Cinco têm quadriláteros
com pelo menos uma aresta inclinada, portanto as dimensões máximas não
bastam para descrever a passagem. O diagnóstico cobre a passagem base.
Os dois grupos seguintes usam um seletor por resto decimal e a tabela
em `00417A10`; a composição completa e os formatos escolhidos no
runtime permanecem em aberto.

O epílogo do mesmo Draw (`00417971..00417A0B`) pede o som primário
quando o contador é exatamente 8. Se há segundo pedido e
`005C0490` aceita o primário, move o ID secundário para o campo
primário, zera o secundário e pede o segundo som. Para os demais
caminhos após esse teste, finaliza quando contador ≥256 e
`005C0490` aceita o ID atual. A finalização limpa `obj+04h`; com
`obj+1Ch` e gerenciador presente, limpa também o byte `+05h` dele.
O caminho de áudio desabilitado em `005C0490` retorna 1. Esses
valores são contadores e predicados do executável. A cadência entre
Draws e a transição com uma segunda fala ainda dependem de observação.

[Coleta e proveniência](joey-knowledge-sqlite.md#complemento-cumulativo-v9--draw-dos-bustos-27112026).

## V31.2 — quadrilátero base observado em Proton (27/11)

Na demonstração automática do executável com fingerprint da coleta
34, em Proton 9.0, Xvfb a 800×600 e llvmpipe, o breakpoint de hardware
em `00416D47` registrou quatro vértices. A origem, na ordem da chamada,
foi (0,0), (326,0), (326,508), (0,508). O destino foi (450,40),
(776,40), (776,548), (450,548). A comparação em Python dos oito
vértices com os dez manifests do DAT encontrou uma correspondência
única, `reaction07.txt`. Origem e destino usaram a vtable
`005DC6EC` e formato 3 (RGB565), com o flag de superfície temporária
ativo.

O mesmo objeto passou 63 vezes por esse ponto, com contador de 8 a 504
em passos de 8. O primário observado foi `0x1000003A` e o secundário
ficou em zero. Em `004179F0`, o objeto tinha contador 504 e byte ativo
zero. O total é de chamadas ao Draw base sob GDB, sem medição de
cadência em tempo real. Ainda faltam os outros passes, o caminho de
duas falas e a comparação de pixels com o cliente.
[Roteiro 43](../../scripts/regression/README.md#43--v31-quadrilátero-base-da-reação-em-proton).

## V31.3 — camadas da reação observadas em Proton (27/11)

A coleta 35 no SQLite v14 contém o trecho `00416D4C..004171BF`.
A inspeção da tabela `00417A10` e do trecho `00417947..00417971`
no PE com o mesmo SHA-256 completou a leitura do despacho. Após o
quadrilátero base, o Draw processa duas posições de grupo, em ordem.
A soma de três registros avança do grupo com primeiro candidato 6
para o grupo com primeiro candidato 9 (índices a partir de zero). O
cabeçalho é o registro imediatamente anterior. Seu resto por 1000
seleciona o ramo.

Para os cabeçalhos 1 e 2, o assembly em `00416D8E..00416DE3`
indexa os deslocamentos −1, 0, 1, 0 pelos bits 1–2 do estado em
`005F2FB4`. O deslocamento negativo omite a camada. O cabeçalho 2
também a omite quando a máscara `0x18` do estado está ativa.
Nesses dois casos o quociente do cabeçalho por 1000 é zero, levando
ao slot de desenho `+0C` em `00416EDE`. A posição do candidato
selecionado é somada à origem do manifesto.

O roteiro 44 confirmou a ordem em 65 Draws do mesmo objeto. Dos 130
despachos, 60 chegaram a `+0C`. O grupo 6 desenhou os registros 6
e 7 em (572,264), 32 e 16 vezes. O grupo 9 desenhou os registros 9
e 10 em (541,219), 8 e 4 vezes. Os cabeçalhos e as posições
corresponderam unicamente a `reaction07.txt` no DAT. A comparação
em Python confirmou a variante ou a omissão em cada despacho
registrado. O objeto ficou inativo no contador 520. Os logs dos dois
pilotos registram contadores finais distintos sob GDB. A composição
em pixels, a cadência sem debugger e os demais ramos não foram medidos.
[Roteiro 44](../../scripts/regression/README.md#44--v31-camadas-da-reação-em-proton).

## V31.4 — troca de fala observada em Proton (27/11)

O roteiro 45 acompanhou o mesmo objeto da primeira chamada de som
até a desativação na demonstração do original, em Proton 9.0,
Xvfb a 800×600 e llvmpipe. Em `0041797D`, no contador 8, o pedido
foi `0x10000028` e o ID secundário era `0x1000002B`.

Na inspeção do PE, `005C0490` retorna 1 pelo atalho de áudio
desabilitado. Na captura, em `004179A5`, o retorno foi zero nos
108 contadores de 16 a 872, em passos de 8. No contador 880, o
primeiro retorno 1 precedeu o pedido em `004179BB` no mesmo Draw.
Nesse ponto, o primário já era `0x1000002B`, o secundário estava
zerado e o objeto continuava ativo. Em `004179F0`, ele terminou
com o novo primário, contador 1520 e byte ativo zero. A comparação
em Python confirmou os IDs, a ordem das consultas e uma única
promoção. Os SHA-256 do original e da cópia conferiram após o piloto.

Os contadores foram observados sob GDB. A captura confirma a
transição e os pedidos, com a escuta e a cadência real ainda por
validar. O chamador da invocação ficou fora deste traço.
[Roteiro 45](../../scripts/regression/README.md#45--v31-reação-com-duas-falas-em-proton).

## V31.5 — término com áudio desativado

O parser do EXE em `00411C46` marca a opção `-nosound`.
Em `00410F8C..00410FA0`, a flag impede a inicialização do
áudio. O predicado `005C0490` retorna 1 imediatamente
quando `00A53878` está zerado. Assim, o término já pode
ocorrer no primeiro Draw com contador ≥256. O Update
incrementa de 8 em 8, o que corresponde a 32 avanços
quando a reação começa em zero e segue sem pausas.

O roteiro 51 confirmou o flag em zero no Proton com
`-nosound`. O pedido de uma reação de fala única apareceu
em `0041797D` no contador 8, e o objeto foi desativado no
contador 256. O prazo da coleta terminou sem uma amostra
de duas falas nesse modo. Com áudio ativo, o roteiro 45 já havia
observado o objeto de duas falas aguardar os dois pedidos
e terminar em 1520 naquela execução.

A duração consultada em `00416B80` alimenta os seletores
de camada e tem um fallback próprio de 32. Já a
desativação do objeto depende do limiar 256 e do estado
de reprodução. Os testes usam a opção de inicialização.
O ajuste de volume durante a sessão não foi exercitado.
[Roteiro 51](../../scripts/regression/README.md#51--v31-reação-com-áudio-desativado-em-proton).

## Objetos distintos

00402E00(reaction,sound): sound=0 não cria objeto; reaction=0 cria
objeto 0x18, vtable 005DA3FC e inicia som via 005C03D0. reaction !=0
cria 0xBC via 004166F0, carregando bust_up/reactionNN.txt.
004166F0/00416A00 instalam 005DA840; +14 recebe ID de som principal,
+18 um segundo ID ou zero, +1C flag, layer +0C=0xF01.
Com flag e global 005F3400 disponível/estado permitido, escrevem
modo 3, contador 0x20. Não chamar esse contador de duração do busto.
00416B80 consulta 005C04E0(obj+14), divide por 33 (signed),
resultado <1 ->32. O lookup 005C04E0 retorna -1 se áudio desativado,
0 se ID de família 1 inválido via 005C0A00; caso válido lê
00A53894[index]. 005C0F80 percorre RIFF fmt/data e usa __ftol para
preencher a tabela; o decompilador perde a expressão x87.
Falta assembly para confirmar unidades/arredondamento. Não assumir
que este parâmetro seja o total de Updates ou usar um timer de 32
para liberar A03 sem renderer. 00416870 é destructor, não Draw.
005DA844/848/84C não aparecem em pointers nem nos chunks de memória;
nenhum corpo Draw/Update da reação foi identificado no banco.

## Roteamento com dano (00403900)

00408470 estágio 1 chama 00403CE0 quando o flag de 00A550A4
permite, após os dois predicados de apresentação de LP. Wrapper
omite o chamado nos estados globais 005F3294=3 ou 6 e despacha
005DA3F0→00403900(side,0,delta). O dano é negativo, o lado
deve ser (~00A54E5C)&1; LP anterior vem de 00A55D64.
Delta>=0, LP=0, lado diferente: sem reação. Para perda positiva D:
- D >= LP: índice 5.
- LP <= 4000 e não letal: D<=1000 índice 3; 1001..2000 índice 2;
  D>2000 índice 4. Preservar a ordem aparentemente invertida
  até verificar assembly.
- LP >= 4001 e não letal: clamp(trunc((D-1)/500)-1,0,3).
O som é 0x10000045 + índice. O manifesto não é reaction(índice):
lê dword da tabela 005DA3C0[índice] e formata número em dois
dígitos. Os 24 bytes da tabela não estão nos chunks de memória.
NativeJoeyReaction.Damage retorna índice e som, não nome de sprite.

## Outros caminhos

005DA3D8/3DC/3E0 apontam a 00402BE0/004030F0/00403310
(ativação, outros eventos, invocação). 00403310 usa resource ID
0x51 ou 0x2E8 para 00414A20 (summon.txt/summon2.txt);
o primeiro é Red-Eyes, o segundo continua sem identidade no SQLite.
Para caso forte no lado selecionado (ATK>0x739, tipos 1/2),
usa RNG de apresentação 0055C140, reaction13 ou 08, som 28/2A
ou 29 e possível segundo som, além de 0x2000000C. Não consumir
RNG de regras ou inferir quando cada tipo ocorre.
00403700 reaction05/som 41 com intervalo 8 do campo 00A577F8;
00403770 escolhe reaction04/som58 ou reaction05/som4F/50 por lado
e argumento; 00403800 consulta ATK/nível; 00403B20 reaction02/som40;
00403B70 reaction08/som30. Estes VAs e requests são evidência
de decompilação; sem mapear opcode/evento não conectar ao App.

## Áudio

005C03D0 aceita família alta 1 (005C0730) ou 2 (005C1480, parâmetro
mínimo 5); 005C0F00 usa os 12 bits baixos para índice da família 1
e limita pelo contador 00A53880. 005C0B60 monta nomes a partir da
tabela de descritores 005EFD48 (stride 16, terminador nulo), não
da ordem física do Voice.dat. Não usar índice do JSON do DAT como
ID de som. A rotina de inicialização constrói a tabela de duração.
Roteiro 27 coleta os ponteiros/assembly e 0x70 descritores, que
cobrem os requests citados até 0x6D. Conferir se a tabela termina
depois desse prefixo antes de ampliar. Resolver strings com o
SQLite; não exportar WAVs ou pixels. A família 2 fica separada.

## Implementação e pendências

NativeJoeyReaction tem modelos puros para seleção de severidade,
tipo de objeto, duração raw e índice de descritor. Testes sintéticos
cobrem fronteiras e gates; nenhuma ligação à fila/clock/raylib.
O roteiro 27 pede vtables 005DA840/005DA3FC (4 slots cada),
tabela de dano 005DA3C0, descritores e assembly dos caminhos
acima. Depois consultar os alvos Update/Draw no banco antes
de coletar outros. Reutilizar --inspect-visual-dat para os manifests
bust_up/reaction*.txt; para posição e composição, aguardar
Draw/Update. Não criar assets, não alterar denominador de Draw
sem inventário comprovado, nem marcar Verified. V18 sem player.


## Complemento — invocações, início e resultado

00403310 foi relida no SQLite v2. O accessor 004027B0 lê classe e
ATK do catálogo (bits 9..17 multiplicados por 10, exceções de classe);
não usa ATK efetivo no campo. O lado que não passa pelo teste
(~00A54E5C)&1 usa outro conjunto de requests: tipos 1/2, limiar
2000, reaction01 ou sound-only 0x10000054; tipo 4 chama diretamente
0x10000058; tipo 5 reaction01 + 0x10000055. Não equiparar números
de tipo a Normal/Special/Flip sem recuperar os chamadores.

No lado selecionado, IDs 0x51/0x2E8 saltam para construtor 00414A20
antes do teste de ATK/tipo. Ele carrega summon.txt ou summon2.txt,
faz preload via virtual +8 de todas as entradas, chama 005C0460(0)
e pede 0x2000000B. Isto não demonstra a duração da cena. Os outros
IDs especiais seguem 00403310: 0xF (Time Wizard) -> som61;
0x44 -> reaction02/som32; 0x2EF (Jinzo) -> som65 para tipo1/2,
som2F nos demais; 0x4CF -> som62; 0x2F8 -> som63;
0x2F9 -> som2E; 0x52E (The Legendary Fisherman) marca variante.
Os IDs e nomes acima só foram associados quando constavam em
card_identities; identidade de 0x2E8 segue não resolvida.
Tipo3 genérico -> som4D, sem sobrescrever os casos explícitos.

ATK>0x739 e tipo1/2 no lado selecionado consomem uma amostra
de 0055C140; reaction13 com som28/2A (bit0) e segunda fala
2C/2B (tipo) se não havia ID especial. IDs 0x2EF/0x2F9/0x52E
substituem por reaction08/som29; Jinzo com secundário65 usa
65 como primário e zera secundário. Mesmo nesses overrides a
amostra RNG já foi consumida. Em seguida request imediato
0x2000000C e construção 00416A00 com dois IDs. Modelo exige
amostra injetada só quando necessária; nunca chama RNG do Core.

004152D0 usa start.txt, preload de todas as entradas e
RNG %3 em +10. 00415C90 consulta 0044B9F0 (global 005F3254):
2 -> lose.txt + 0x10000059; 3 -> draw.txt + 0x1000005C;
demais -> win.txt + 0x1000005D. Não inferir orientação do
resultado do C# por estes rótulos. Preload de todas entradas;
lê parâmetro do banco, usa unsigned %1000; só consome RNG
se divisor diferente de zero e grava RNG%divisor em +18.
O significado do campo de manifesto ainda não está confirmado.
Os construtores podem configurar modo 3 e contador 0x20;
não usar isso como duração do Draw.

O índice público versionado data.dat-index.json contém 17
manifests: activate, start, draw/lose/win, summon/summon2 e
reaction01,02,04,05,06,07,08,10,12,13. As lacunas 03/09/11
não devem ser sintetizadas. O roteiro 27 passa a coletar o
bloco de vtables 005DA7F0..005DA83F e os quatro entrypoints
adicionais; nenhum alvo Draw/Update foi encontrado em
visual_virtual_methods ou decompilações transitórias deste
intervalo. O espaço 00416BAB..00417A4F não contém funções
indexadas, portanto não deduzir entrypoints por proximidade.
Verificar os VAs após ler vtables antes de pedir mais código.
NativeJoeyCinematicRouting é contrato isolado de decompilação;
nenhum renderer, player, gate ou badge novo.

## V18.1 — infraestrutura de áudio e revisão isolada (24/11)

SQLite v2 reaberto somente leitura, SHA256 e quick_check conferidos. 005C03D0 verifica enable global e despacha família 1 / 2. 005C0F00 usa low 12 bits e limite 00A53880; 005C1480 usa low 28 bits e limite 00A538A0, devolve a referência do Stream ativo quando já toca a mesma solicitação; senão chama 005C1500 e registra request pendente. 005C1180 conta descritores de stride 16 a partir de 005F0A30 (primeiro _Dummy.wav), com sentinela nula, separado de 005EFD48 (primeiro cursor.wav, Internal). 005C1960 usa parâmetro mínimo 5 e prepara transição a -5000 em unidades DirectSound, mas a cadência não foi confirmada. 005C0F40 converte nível 0..127 em (level*10000)/127-10000; inválido retorna 0. 005C1360/005C13F0 calculam nível Stream com dois produtos/divisões inteiros sucessivos. Não converter estes valores diretamente para ganho linear raylib; o modelo retorna valores raw.

O índice versionado Voice.dat contém 132 entradas, 131 WAVs: 111 Internal, 20 Stream (inclui _dummy.wav). Ordem física NÃO é ordem das tabelas. O roteiro 27 inclui o prefixo Stream e os três métodos acima; confirmar terminador antes de assumir quantidade. NativeWaveInfo interpreta RIFF/WAVE com limites, padding, fmt/data e consistência PCM. ByteRateDurationMilliseconds é estimativa pelo cabeçalho, não a duração x87 de 005C0F80. Não reproduz o walker inseguro do original. NativeAudioRouting isola lookup, parâmetros e volume, sem tocar áudio. Os testes sintéticos incluem RIFF ímpar e inválido, famílias e truncamento.

Comandos de diagnóstico (sem data.dat gráfico):

    dotnet run --project src/Joey.App -- --voice-dat=/path/to/Voice.dat --inspect-voice-dat
    dotnet run --project src/Joey.App -- --voice-dat=/path/to/Voice.dat --review-voice=joey_01.wav

O primeiro grava apenas metadados/erros em scratch/visual-review/voice-metadata.json; o segundo abre dispositivo de áudio raylib, decodifica WAV Internal original em memória por nome explícito, toca uma vez, libera sound/wave/device. Sem arquivo de áudio extraído, sem janela, sem IDs presumidos, sem gate ou fila. Reprodução limitada a 120s de áudio decodificado e incompatível com --mute-joey. Não toca Stream neste modo. Não há Voice.dat real/dispositivo disponíveis no ambiente do agente; validação de áudio nativo e integração ao duelo continuam pendentes. CI anterior falhou porque Reject captura InvalidDataException, não ArgumentException; teste V31 passou a usar helper local específico, mantendo o contrato do modelo e o Reject compartilhado.

## V18.2 — requests de efeitos e seleção de música

SQLite v2 SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4, quick_check ok nesta sessão. Consultadas decompilations, refs, pointers e assembly dos bundles existentes. Chamadas abaixo recuperadas sem assumir a associação do índice ao filename.

| VA | Evento/ponto de disparo | Request | Evidência |
|---|---|---|---|
| 0044FCC0 | construtor sp_summon.txt | 1000000E | decompilação + string |
| 00450590 / 00450A90 | Update de activate.txt, contador anterior zero, +16 | 1000000D | decompilação; assembly 00450A93..00450AA6 confirma teste e call |
| 00450C30 | construtor break.txt | 10000008 | assembly 00450E2B..00450E3A + string |
| 004511D0 | construtor explosion.txt | 10000007 | assembly 00451340..0045134F + string |
| 00451950 | Update de duelstart.txt, contador anterior zero, +8 | 10000004 | decompilação, construtor 004516A0 |
| 004580F0 | construtor Vija, modo 0 / não zero | 10000022 / 23 | assembly 004582FE..0045831A |
| 00481230 -> 00480B40 | lado muda / mesmo lado e código de fase 3 | 10000005 / 11 | decompilação; wrapper 00404920 |
| 00449740 | valor de display muda | 10000012 | decompilação; wrapper 004086C0 |
| 0044EF40 | códigos 6,7,9,25 | 10000010,0F,09,0A | decompilação; múltiplos callers, sem atribuir semântica a cada código |

0044C320, chamado por 0044BA00, compara LP uint16 dos lados relativos a
(~00A54E5C)&1 e 00A54E5C&1. Diferença >=3000 seleciona Stream 8; <=-3000
seleciona Stream 7; caso contrário global 005F3294=3 -> 6, =1 -> 5, demais -> 4.
Todos passam parâmetro 15. Não chamar a variável de fase, não atribuir m_yuri/m_furi
sem tabela de descritores. É um selector estático, não prova da frequência de polling.
005C1480 reaproveita referência quando request ativo igual e está tocando; não
recomeçar música a cada Update. NativeDuelAudioCues modela estes resultados sem
scheduler, playback nem novo Draw. Sem alteração de badge. Coleta 27 existente
continua necessária para resolver nomes; não abrir coleta redundante.

### Validação Windows pendente

O smoke Windows de `scripts/pending/28-voice-audio-windows.ps1` foi executado. O SQLite v3 registra 129 rejeições por cabeçalho RIFF truncado. Os três exemplos foram rejeitados antes da reprodução.
Build, inspeção, rejeição de recurso ausente e de mute + review, e amostras
Internal joey_01.wav/attack.wav/card_move.wav. Saída privada scratch/voice-smoke-<timestamp>
com summary.json, voice-metadata.json (se gerado nesta execução), logs e
listening-notes.txt. Preencher notas de escuta: código 0 não prova som audível.
O script não extrai nem envia WAVs/Voice.dat. Não confundir duração estimada
com duração nativa da tabela 005C0F80. No agente, PowerShell, DAT e dispositivo
não foram validados; aguardar execução real. Testes C# são sintéticos.

## V18.1 — limite do WAV no Voice.dat

O snapshot v3 registra 131 entradas no smoke Windows do roteiro 28.
O Voice.dat local com SHA-256
`50275ac744b8d029162dcedc2d19d318b34d6d56ee8dcabec2f1a9119438d6c8`
reproduziu as 129 rejeições `Truncated RIFF chunk header`. Das demais
entradas, uma segue RIFF convencional e uma excede o limite de leitura
do Archive.

Nas 129 entradas, `fmt ` é o primeiro chunk e tem tamanho 18, formato PCM
16-bit e extensão zero. São 74 mono e 55 estéreo. O chunk seguinte é
`data`. Seu tamanho deixa um sufixo de quatro bytes dentro do limite
declarado pelo RIFF e 40 bytes entre esse limite e o fim da entrada.

O disassembly do mesmo EXE usado na captura confirma que a rotina de
duração `005C0F80` lê o byte rate do `fmt ` em `005C10DC`, procura
`data` em `005C10E3–005C10FE` e usa o tamanho declarado em
`005C1100`. `005C1107–005C1115` divide tamanho por byte rate,
multiplica por 1000.0f (`005DDA98`) e trunca em `005C3E40`. Esse
caminho nativo termina a leitura de chunks no primeiro `data`.
A comparação cobre o consumidor de duração. A rotina de reprodução nativa
e o significado dos bytes do sufixo continuam em investigação.

O leitor específico do Voice.dat reconhece a estrutura acima e limita
a leitura ao fim declarado de `data`. Para o player isolado, adapta a
cópia em memória ao tamanho RIFF correspondente antes de chamar o
decodificador. O leitor RIFF genérico continua estrito. O teste sintético
cobre a estrutura exata, RIFF convencional, limites de chunks e
consistência PCM. O limite de tamanho do Archive permanece em vigor.


A inspeção passou a aceitar 106 de 131 entradas. Das 129 rejeições
originais, 105 passaram e 24 WAVs estéreo apresentaram dois bytes
a mais que um múltiplo do tamanho de frame PCM (quatro bytes).
A validação de frame continua em vigor até esclarecer como o player
nativo trata esse caso. Um smoke Linux com o sink ALSA nulo confirmou
a decodificação e o término do player Raylib de `joey_01.wav`.
O teste não verifica a escuta nem outras plataformas.


## V18 — primeira integração audível no Alpha e laboratório

SQLite v3 consultado: strings 005F07A4..005F09B4 e 005F0B20..
005F0BBC contêm nomes de efeitos e música; não há memória dos
descritores 005EFD48/005F0A30 nem referências de dados suficientes.
A ordem das strings não autoriza reconstruir a tabela de IDs.
DuelSoundAssets usa nomes do índice original Voice.dat, com
associação semântica provisória, separada de NativeDuelAudioCues.
Cues no início do passo visual de transferência, abertura,
tributo, destruição, special summon, ataque, entrada Glow,
turno/fase e novo alvo LP. Não dispara em cada Draw nem lê
mensagens textuais. Não altera Core, resultado ou gate.
Instant pode suprimir animações e respectivos sons.

DuelAudioPlayer possui device e Archive separados, usa correção
PCM NativeWaveInfo, cache de efeitos e uma música decodificada
(m_menu/m_duel1), repete quando termina. Ganho 0.35 provisório.
Sem crossfade, seleção LP dinâmica, streaming nativo ou falas.
Falha de arquivo/dispositivo/recurso não impede partida.
--no-audio / --no-music; --mute-joey não é mute global.
Testes sintéticos de nomes/roteamento; CI e desktop pendentes.

## V18.3 — descritores e estado do Voice.dat

A coleta 27 (bundle `9c86f197fd6c4353af56a0846bd0427f33332ece62ea4527b08970614cc88ff7`)
já tinha as duas tabelas. Os cinco blocos de memória em `005EFD48`,
`005EFF48`, `005F0148`, `005F0348` e `005F0A30` no SQLite v8
conferem byte a byte com a seção `.data` do EXE atestado. Cada
descritor tem stride 16 e começa com um ponteiro para o nome. Os
inicializadores `005C0B60` e `005C1180` avançam de 16 em 16 até
encontrar um ponteiro nulo. Internal tem 110 entradas em `005EFD48`
e sentinela `005F0428`. Stream tem 14 entradas em `005F0A30`
e sentinela `005F0B10`.

Os três estados da seleção normal em `0044C320` podem levar a
`m_duel2.wav` (Stream 4), `m_duel3.wav` (5) ou `m_duel1.wav` (6).
As duas alternativas por diferença de LP são `m_yuri.wav` (7) e
`m_furi.wav` (8). Os IDs Stream `0B` e `0C` dos pedidos de
cinemática resolvem para `m_red.wav` e `m_sum.wav`.
Entre os efeitos, Internal `06` é `attack.wav`, `0F` é
`card_move.wav` e `10` é `card_open.wav`. Todos os nomes das duas
tabelas foram encontrados nas respectivas pastas do Voice.dat em
inglês, com comparação sem distinção de maiúsculas e minúsculas.
Essa associação descreve o índice do arquivo e as tabelas do EXE.

Em 27/11, `--inspect-voice-dat` retornou 2. Entre as 111 entradas
em Internal, 106 passaram e cinco falharam por inconsistência PCM.
Em Stream, as 19 entradas que couberam no limite do leitor foram
rejeitadas por PCM, e a vigésima ultrapassou o limite de 16 MiB.
Isso inclui as músicas atualmente configuradas no cliente para menu
e duelo. A leitura de duração em `005C0F80` explica o tamanho
declarado, enquanto o tratamento do quadro estéreo incompleto
durante reprodução segue em aberto.

## V18.4 — quadros PCM e streaming no cliente

No Voice.dat registrado, as 24 entradas com problema de alinhamento
têm `nBlockAlign=4`, PCM estéreo de 16 bits e tamanho do chunk `data`
congruente a 2 módulo 4. Todas têm a estrutura de sufixo já confirmada.
A tabela nativa confirma nomes e as rotinas em `005C0B60` e
`005C0F80` separam carregamento e duração. A inicialização do banco
passa por `0055FF10`, vtable `005DCD68` e `00549BA0`, que chama
`005601D0` por `+40`. Esse caminho ainda não estabelece como o
dispositivo original consome o último quadro incompleto.

O adaptador para o Raylib descarta apenas a amostra sem par nessa
variante do contêiner. Reescreve o tamanho RIFF e o tamanho `data`
na cópia temporária, com validação estrita para RIFF comum.
A inspeção conserva o tamanho original do chunk para estimar a
duração. O limite de carregamento para reprodução permanece em
16 MiB. O comando de inspeção pode ler entradas armazenadas sem
compressão de até 32 MiB, inclusive `m_tuto.wav` (25.964.784 bytes).
O limite de descompressão permanece em 16 MiB.

O player de música usa a API de streaming do Raylib, com entrada WAV
em memória e decodificação em blocos. O teste Linux com ALSA nulo
carregou `m_menu.wav` (63,72 s), trocou para `m_duel1.wav`
(139,65 s), verificou avanço das duas faixas após GC compactante e
liberou os streams ao parar e ao trocar. O teste separado de
`card_move.wav` concluiu pela entrada pública de revisão. Esses
testes validam o caminho do cliente até o dispositivo virtual.
Escuta física, cadência, volumes e transições do original
continuam pendentes.

## V31.6 — renderer isolado de reaction07

```sh
dotnet run --project src/Joey.App -- /path/to/data.dat --review-reaction=07 --no-audio
```

F7 executa um Update (+8) e um Draw; F8 reinicia; F12 captura PNG/JSON
privados. O estado global de cena tem contador independente e pode iniciar
com `--reaction-scene-state=N`. O modo padrão simula o predicado de áudio
sempre satisfeito; `--reaction-wait-audio` exige F5 após o primeiro pedido.
A captura é do limite pós-step, omitindo objeto já encerrado. Nenhuma voz
real é tocada. O parser exige a geometria e os cabeçalhos de reaction07
observados nos roteiros 43/44, sem inferir os demais seletores. Usa a
ordem base, boca e olhos, bitmask e posições confirmadas. Bitmaps locais
são desenhados por Raylib como preview retangular; a superfície temporária,
composição packed e transparência ainda precisam de comparação. O modelo
puro de lifecycle testa a troca de duas falas nos contadores 880/1520,
mas o review de reaction07 usa apenas o pedido observado 0x1000003A.
Não liga gatilhos de gameplay, áudio real ou A03. Draw continua Pending
até integração; CI e smoke com DAT real pendentes.

## V31.15 — reação à colocação de uma magia do Joey

O GDB registrou `00402BE0(1, 1429, 0)` no opcode `CF`, com modo 7
(demonstração) e `00A54E5C & 1 = 0`. O catálogo identifica o
resource 1429 como Card of Safe Return e o índice DAT como 461.
O retorno `00402D1E` pediu `reaction07.txt` e `0x1000003A`; o primeiro
Draw observou contador 8, flag `+1Ch=1`, e nenhum ID secundário.
Outra colocação `CF` no lado 0, resource 1027, não pediu o busto
pelo mesmo seletor. Com o fingerprint v15, o descritor em `005F00E8`
aponta para `005F069C` e resolve `Joey_22.wav`.

Em `00405D8E`, `CF` chama o seletor de reação antes de movimentar
a carta. O teste dos objetos em `00405DE7..00405DF1` espera
o slot virtual `+4` antes de passar à colocação em `00405E02`.
Para o busto, `00416BB0` retorna ativo enquanto o flag está ligado.
O filtro de `00402BE0` usa o lado `(~00A54E5C)&1`, os IDs de
rotas especiais e a classe 22. No ramo genérico, `00402700` lê o
subtipo dos bits 17–19 e o subtipo 2 escolhe a fala `0x10000051`.
Essa variante e os demais ramos ficam fora deste recorte.

O Alpha usa o lado da CPU para a perspectiva do Joey e o snapshot
tipado de ativação da mão. O gate da ativação conserva seu estágio
de viagem até o Draw terminal da reação, antes do brilho e dos
comandos de resolução. O cliente usa o mesmo compositor do review
e o player existente. O seletor facial acompanha o contador de
apresentação do tabuleiro, correspondente ao incremento de
`005F2FB4` em `004112D9..004112E0`. A fase inicial desse contador
e a composição ainda precisam de comparação pareada. O recorte
aplica a transposição de perspectiva ao Single Duel do Alpha. O
traço de produtor desta coleta vem da demonstração.
[Reprodução](../../scripts/regression/README.md#73--v31-reação-a-colocação-de-magia).
