# V25/V26 — ativação alternativa e exclusão

Fonte: joey-knowledge-visual-decoded.sqlite, read-only, quick_check ok.
EXE SHA256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Decompilações persistentes 00450260, 00450730, 00450A90, 00406B20,
00406F70, 0040AA40, 005B9470, 0043D810 e 004481F0. Sem nova
execução, sem nova evidência de assembly nesta rodada.

## V25: rota alternativa

00450730 chama o construtor 00450260 com descritor 8016 (região 11,
slot 0). Compartilha a inicialização de activate.txt e recursos
de cartas/miniaturas, com refcount 005F2FF0. Depois troca vtable
para 005DB304, fixa centro (DAT_005DB570,300), camada 0x110,
contador +10=-128 e argumento de carta +12C. O bundle 01 confirma
005DB570=525. Não reutilizar automaticamente o Draw comum:
00450AD0 é uma função distinta ainda ausente do SQLite.

00450A90: se o contador de entrada é zero, solicita
005C03D0(1000000D,0); soma 16; byte ativo = novo contador <385.
São 33 Updates desde -128, som no nono Update; último contador
400, objeto inativo. A ativação ordinária tem 17 Updates.
Não inferir milissegundos nem momento de desenho a partir disso.

| Opcode bruto (índice do switch) | Handler | Rota |
|---|---|---|
| 72 (48) | 00406B20 | Estágio 2: normal se slot resolvido e objeto visual corresponde ao índice interno solicitado; alternativa se slot -1, objeto ausente ou identidade de recurso divergente. |
| 73 (49) | 00406F70 | Estágio 1 sempre instancia alternativa depois de aguardar objetos anteriores. |
| 1E (18) | 0040AA40 | Estágio 1 procura todas ocorrências elegíveis por identidade, flags e lado; cria objetos ordinários para elas. Se nenhuma, cria alternativa. |

Os três handlers consultam virtual +4 de objetos anteriores e
dos novos antes de liberar 0x4000. Não identificar o gate com
qualquer byte ativo sem examinar seu predicado. O índice interno
vem de 00402460(ID), não passar resource ID como índice.
A camada alternativa é 0x110; o construtor normal tem 0x120
ou camada de mão 0x201+slot*16. Não confundir as duas.

NativeAlternativeActivationTimeline contém relógio e seleção
de ramo, recebe fatos do chamador, devolve pedido de áudio
sem executar áudio. Não contém regra de carta, relógio wall-time,
fila de comandos ou implementação de IPresentationGate.

## V26: exclusão

0043D810 instala 005DACB4, inicializa +10=64, +14 =
argumento de carta mascarado pelo bit 0x8000 do descritor,
+18 = descritor integral; camada 0x120. Carrega exclusion.txt,
usa os mesmos recursos partilhados 005F2FF4/005F2FF8,
refcount 005F2FF0 e chama serviço de som com argumentos
não recuperados. 004481F0 (slot Update apontado pelo inventário)
é no-op. 0043DBE0 Draw não está decompilada. Portanto não há
evidência de um relógio de duração fixa; não tratar como
explosão, nem como descarte de carta necessariamente.

NativeExclusionStart só porta inicialização/mascaramento.
Importante: não revelar argumento de carta oculta em render
ou F12. O construtor já o zera se bit 15 não está ligado.
Nenhum produtor direto do construtor apareceu em refs ou
no texto de decompilações; não atribuir opcode de banimento
apenas pelo nome do manifesto. Confirmar chamador/vtable.

## Limites, integração A03 e coleta

ACTIONS é dono da ponte ActionQueue/IPresentationGate; VISUALS
entrega modelos e renderers de uma única instância de efeito,
recebendo fatos/recursos autorizados. Nenhum novo FIFO ou
assinatura de eventos Core adicionados.

O gate precisa distinguir visível, ativo e bloqueante:
V23 permanece visível em 128 e deixa de bloquear; a saída
depende de liberação posterior. V26 tem Update vazio: um
gate de "N updates" não pode ser deduzido. As rotinas que
mutam contador no Draw ou ângulo (V11) exigem amostragem
explícita: consultas repetidas CanComplete não são Draw/Update.
Objetos de hover/pulso V24 não pertencem à sequência de regras.
Para remoção, a apresentação precisa de snapshot antes da
mutação (ocorrência/posição/identidade visível), pois a fila
executa o comando antes de CanComplete. A03 decide a forma
de publicar estes dados e substituir os adaptadores legados
PresentationSequence existentes no App; não alterá-los aqui.

Coleta 18-alternative-activation-exclusion-ghidra.sh: Draws ausentes, vtables, construtor de exclusão,
e entrada da alternativa para confirmar ABI e argumentos.
Para exclusion.txt, reutilizar --inspect-visual-dat.
Sem nova Draw integrada: cobertura 20/34, zero Verified.


## Bundle 18 recebido — Draws e revisão local

Fonte: SQLite cumulativo v2 (SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4).
Bundle 0acb4d4ca588f4a9294c26c05a2c5916b9d3ce6c9455190bc570ea06512ff64f,
janelas 00450AD0 e 0043DBE0 (assembly batch-000 e retries).
Vtables lidas: 005DB304 = 004508F0,00403080,00450A90,00450AD0;
005DACB4 = 0043DA90,0043DBD0,004481F0,0043DBE0.
Não repetir roteiro 18.

### V25

00450ADE..00450B19: scale8=C(t)>>9 para t<0, senão 128;
intensidade=2*scale se t<256, senão C(t-256)>>8.
00450B1F..00450C03 resolve índice +12C via banco DAT_005F2FF8
(data/card/list_card.txt, confirmado pelo literal 005EB89C em
0043D8F1), mede width/height, escala ambos por SAR 8, centra
em (525,300), blit virtual +48. É a carta GRANDE, não mini.
00450C06..00450C19 chama 004505D0 (ativação normal) depois da
carta se 0<=t<=256. Scratch inicializado pelo construtor
00450260, descritor 8016, metade de width/height do banco grande
(005F2FFC/3000). Mantém categoria calculada por 00402690.
Não deduzir categoria pelo recurso na revisão; parâmetro
explícito diagnóstico. Update +16 até >=385, pedido de som no
Update cujo input é zero (33 updates). Draw não muda contador.

### V26

0043D9CA..0043D9F8 mascara identidade se bit15 não setado,
inicializa t=64. 0043DA56..0043DA63 pede som 10000024.
0043DC08..0043DC10 soma 8 NO DRAW, resolve 0045C140
(side,region,slot), mesmo centro para todos os passes.
0043DC2E..0043DC92: t<128 desenha mini (banco 005F2FF4,
mini/list_card.txt), 180*(side XOR (A54E5C&1)).
Ignora bit de defesa nesse passo. 0043DDE2..0043DEF3:
t<=128, três passes key1, intensidade C(2*t-256)>>8,
width=originalWidth*intensity>>8, height original,
centros X+(i-1)*(originalWidth+1).
0043DCA8..0043DDD2: t>128, mede key2, desenha key
(t/8)%6+2, scale8=128+(C(t-128)>>9), intensidade
C(t-128)>>7. Medir key2 mesmo quando quadro diferente.
0043DF01..0043E042: t>=96, key0 por último, escala 256
até 128, senão mesma escala anterior. Intensidade
C(4*t)>>8 até 128, depois C(t-128)>>8.
0043E045..0043E05D: t>=256 libera índice do banco mini
e só ENTÃO limpa ativo; 24 Draws desde t=64.
A consulta SampleNextDraw não consome um Draw nem libera
recurso. NativeExclusionStart impede construção pública
com identidade não mascarada. Sem novo gate ou fila.

Revisões diagnósticas em 800x600:
```bash
dotnet run --project src/Joey.App -- ../passion/data.dat --review-alternative=250,0 --native-cosine=scratch/visual-review/native-cosine.json
dotnet run --project src/Joey.App -- ../passion/data.dat --review-exclusion=250,8080 --native-cosine=scratch/visual-review/native-cosine.json
dotnet run --project src/Joey.App -- ../passion/data.dat --review-exclusion=0,0080 --native-cosine=scratch/visual-review/native-cosine.json
```
250 é exemplo de resource ID; usar carta existente no DAT.
F7 = Update em alternativa, Draw em exclusão; F9 = Draw
isolado em exclusão; F8 reinicia; F12 PNG/JSON. A revisão
de exclusão limita descritores às regiões de campo conhecidas;
ID 0 obrigatório quando oculto. NativeExclusionResources
valida a ordem hipotética do manifesto contra nomes dos oito
recursos do inventário (hikacard 58x80, hikatate 15x144,
ryusi1..6 50x72), não afirma leitura do DAT nesta sessão.
Se ordem real/remap divergir, rejeita. Compositor Raylib,
cor-chave, arredondamento dos sprites e scratch reutilizam
política de revisão, não prova de pixels. Nenhum disparo de
som real. Não contabilizar as revisões como Draw no duelo.
A03 fornece evento/snapshot e integração, não criar fila
paralela. Testes sintéticos de passes, fronteiras, duas
perspectivas, visibilidade e mutação de Draw.
**Correção de 25/11:** [auditoria da tabela comprimida](native-visual-dispatch-opcode-audit-2026-11-25.md) confirma as três rotas brutas de V25. Não altera V26 nem o compositor.

## V26.2 — gate, DAT e lacuna do produtor (27/11)

SQLite cumulativo v7 aberto read-only, quick_check=ok, SHA256
`a210880f69a0997492d81dd00e405e1e306d6cc4201f4948d7b8855e4236d379`.
Bundle 18, batch-000, janela 0043D810: **0043DBD0 XOR AL,AL;
0043DBD2 RET**. A vtable 005DACB4 +4 é uma consulta que sempre
retorna falso, inclusive durante os 24 Draws. +8 é no-op.
Não usar PresentationSequence de 24 updates, nem aguardar Active
em CanComplete. 0043E05D limpa o byte ativo após o último Draw.
O modelo expõe BlocksRelease=false; revisão F5 avança uma amostra
por Draw efetivo do review (cadência do host, não prova do original).
F7/F9 avançam manualmente e pausam; F8 reinicia.

Inventário privado visual-dat-20261124 contém exclusion.txt com
8 linhas na ordem implementada (comentários Shift-JIS após //).
Confirma key0=58x80, key1=15x144, keys2..7=50x72, sem erro de
leitura. A revisão agora verifica dimensões, além dos nomes.
Não prova o remap interno nem o blend pixel a pixel.

A busca de referências de chamadas, decompilações e instruções do
v7 não encontrou produtor direto externo de 0043D810. 8F vai para
00409240, que registra transferência via 0040CDB0 (mailbox tipo 7,
A5506A/A5506C/A55070); o consumidor 0040D070 usa 0041D6C0 para
movimentação ou 0041D3F0 para variantes de token. Não há chamada
explícita de 0043D810 nessas rotinas; chamadas virtuais/rotas não
indexadas continuam possíveis. Isso NÃO prova que a animação seja
inutilizada, nem que banir via 8F dispare exclusão. 7C também pode
ter destino de banimento sem trocar o construtor de Explosion.
Portanto não ligar automaticamente RemovalCause.Banish nem
FusionMaterial a V26. Nem toda operação de banimento tem origem
visual no campo (deck, mão, cemitério, custos).

Roteiro 34 modo exclusion: observa constructor, return RVA,
identidade mascarada, gate, Draw e contexto de comandos/transferência.
Saída 0 requer um objeto observado desde t64 até t256, 24 Draws,
sem perda e sem gate bloqueante observado. Ausência de objeto é
resultado inconclusivo sobre produtor, não erro de regra; guardar
controle com contexto de transferência. Associação temporal não
prova causalidade; examinar retorno do construtor antes de ligar
uma rota A03. Sem ativação sintética no duelo ou aumento da badge.

## V26.5 — primeira integração no duelo

A evidência posterior do Codex localiza 0043D440 no slot +18 de
005DAC74, usado pelas zonas 0F e pelo despachante 0041E7B0.
Não houve ainda uma captura positiva de transferência a 0F.
Leitura do bloco v7 em 005DB5B0 (80 bytes) confirma a geometria
para 0F: pares em 005DB5F0 e 005DB5F8, (762,308) e (292,292).
Ordem relativa ao campo/deck do cliente, perspectiva nativa fina
pendente. Usar BanishCommand aplicado como inferência de rota de
destino, não prova de que toda remoção dispare V26. A observação
CardBanished só contém Card para origem pública (cemitério); deck/mão
mascarados porque o Core não registra face de banimento. O App cria
24 Draws com recursos DAT, sem espera no gate. Não conecta outros
caminhos legados, battle replacement ou material de Fusion Gate.
Compositor aproximado e som 10000024 pendente. Partial no inventário.

## V25.1 — produtores identificados de 72/73 (27/11)

SQLite cumulativo v7, SHA256
`a210880f69a0997492d81dd00e405e1e306d6cc4201f4948d7b8855e4236d379`,
quick_check=ok. As janelas de assembly 005778F0/00577CF0/
005783C0 confirmam os wrappers 00578390 e 005783C0
(OR 72 em 005783B3/005783E9; bit 15 deriva do lado) e
00578400 (OR 73 em 00578420, CALL 005B9170 em 00578424).
O wrapper 73 converte o índice interno em resource ID por
004022E0 e empilha as palavras B=1, C=0, D=0.
005783C0 empacota a zona e o identificador de ocorrência nos
bytes baixo/alto de B; C=0. O 72 escolhe o ramo pelo objeto
visual, não simplesmente pelo tipo de carta.

Três chamadores diretos de 00578400 no v7, todos com
CALL confirmado por assembly:
- 00575A62 (00575A10): após 00570C70 não zero, converte
  resource 443h (Graverobber, índice 703), emite 73 e então
  chama 005784E0 com 2000. Decompilação: consulta a marca de
  ocorrência do empréstimo, não uma ativação ordinária.
- 005BBB41 (005BB720): a mesma sequência de Graverobber
  na fase 7 de resolução da corrente, guardada por descritor
  e pelo retorno de 00570C70; 005BBB52 emite os 2000.
- 005788E8 (00578860): quando a quantidade pedida é não zero
  e 005709D0 retorna não zero, converte resource 294h
  (Crush Card, índice 369) e emite 73 para o lado oposto
  antes do laço que movimenta cartas. A descrição de 005709D0
  vem da decompilação, não de uma nova captura.

O 1E também aparece nos produtores de Dragon Capture Jar
(0059CBB0, 148h), Ring of Magnetism (005A1580, 422h)
e Goblin Fan (005AC990, 2AEh), por decompilação. Esses
chamadores não demonstram que o fallback alternativo seja
escolhido: o handler procura ocorrências elegíveis primeiro.
Não tratar ativação de mão ou qualquer gatilho no cemitério
como prova de 73. Para integrar, portar eventos específicos
dos efeitos com snapshots de origem e validar sua sequência
contra o original. Não alterar regras de cartas neste lote.
