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
