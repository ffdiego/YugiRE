# V31/V18 — reações do Joey, pedidos sonoros e duração

SQLite privado v2 SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check=ok (leitura em 24/11). Fonte: decompilations, refs, pointers,
functions, visual_evidence_memory/instructions/transient_decompilations.
As rotinas de reação abaixo não têm assembly de entrada no snapshot;
classificador e duração são portas de decompilação, não paridade confirmada.

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
