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

| Opcode | Handler | Rota |
|---|---|---|
| 48 | 00406B20 | Estágio 2: normal se slot resolvido e objeto visual corresponde ao índice interno solicitado; alternativa se slot -1, objeto ausente ou identidade de recurso divergente. |
| 49 | 00406F70 | Estágio 1 sempre instancia alternativa depois de aguardar objetos anteriores. |
| 18 | 0040AA40 | Estágio 1 procura todas ocorrências elegíveis por identidade, flags e lado; cria objetos ordinários para elas. Se nenhuma, cria alternativa. |

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