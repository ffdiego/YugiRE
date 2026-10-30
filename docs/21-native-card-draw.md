# Compra nativa, mão inicial e callbacks de compra

Análise estática do EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
VA com image base 0x00400000. Sem executar o original ou alterar o banco.

## Fase e quantidade são comandos diferentes

No elemento 0x005A41C0 da sequência compartilhada, o estágio 0 emite
**0x50**, mas esse não transfere carta alguma. Os stubs 0x50–0x55 chamam
**0x00404920** com valores 0–5. O handler grava o parâmetro nos bits 1–3 de
0x00A577FA, atualiza a interface por 0x00481230 e espera apresentação.
Portanto, byte de fase, índice da sequência e quantidade de compra não são
o mesmo campo. O mapeamento completo dos rótulos da interface segue separado.

0x005A41C0 testa flags de jogador 0x2 e 0x800000; em caso de bloqueio limpa
a primeira e encerra seu elemento. O estágio 1 examina associação de ID
0x520 (**Cold Wave**) com lado/contador. O estágio 2 chama
**0x00578B60(lado, 1)** e o estágio 3 emite evento de tipo 1. Não expandimos
a expiração completa de Cold Wave neste checkpoint.

0x00578B60 encaminha para **0x00578860(lado, quantidade, 0)**. Esse produtor
percorre as cartas no início do deck e, para cada quantidade solicitada,
emite **0x61** com segunda/terceira palavras iguais a **1/1**. Existem também
caminhos de Parasite, Crush Card e Appropriate, que podem produzir outros
comandos ou pedidos. Não se deve substituir todo esse produtor por uma
transferência imediata de todas as cartas sem considerar esses caminhos.
A parte de Parasite já foi seguida no estudo específico.

## Consumidor e listas

**0x61 → índice 57 → stub 0x005B95E6 → 0x00571E30**.

| Estado baixo em 0x00A558AC | Operação seguida |
|---:|---|
| 0 | Verifica byte de contagem do deck; se zero, marca estado 0x100 do jogador e encerra o comando |
| 1 | Copia carta do topo e transfere por 0x005691D0; prepara apresentação |
| 2 | Decrementa a terceira palavra do comando; retorna ao estado 1 se ainda há quantidade |

O teste de deck vazio usa byte em **0x00A55D69 + lado*0xD44**. A marca é bit
0 do byte **0x00A55D6D + lado*0xD44**, ou máscara 0x100 no word de estado.
Uma compra comum de uma carta verifica esse teste antes de cada comando.
O lote inicial de cinco tem uma checagem na entrada do comando e repetição
interna; não supomos que todo lote faça o mesmo teste entre cada carta.

**0x005691D0 → 0x00569010(lado, 0, saída)** retira o elemento zero do deck.
0x00569010 verifica contagem, copia o DWORD de estado completo, decrementa
o byte alto de 0x00A55D68 e desloca os elementos ativos seguintes para baixo.
A cauda inativa não é zerada. Em caso de sucesso, **0x0056CEF0** acrescenta
a palavra à mão em `0x00A56434 + lado*0xD44 + contagem_mão*4` e incrementa
o byte baixo do mesmo word de contagens. Assim a chave da ocorrência é
preservada, junto aos demais bits; não se cria outra carta só pelo ID.

Há exceções na inserção de mão: índice vazio não entra; fichas IDs
0x780–0x783 não entram; cartas de categoria retornada como 2 por 0x00402740
são encaminhadas por 0x00568D10 à lista de base 0x00A566B4 do lado de origem
indicado no registro. Amostras locais conferidas: Flame Swordsman tem bits
18–19 iguais a 2, Blue-Eyes iguais a 0 e Black Luster Soldier iguais a 3.
O caminho corresponde ao tratamento de fusões já sugerido pelas listas,
mas não generalizamos todas as classes especiais dessa função.
Nenhum limite defensivo de capacidade aparece no append examinado; não
sondamos estados inválidos ou contagens fora do domínio pelo original.

## Mão inicial e embaralhamento

**0x005B9F50**, primeiro handler da tabela externa, emite 0x61 para ambos os
lados, com segunda palavra zero e **terceira palavra 5**. Isso comprova o
lote de cinco por lado nesse caminho inicial. Depois, a sequência normal
pode executar a compra de uma carta, se suas guardas permitirem. O código
não trata a primeira mão como um array simulado de seis cartas.

A chamada anterior **0x00568D80(lado, 8)** em 0x005B9E50 não compra oito.
Esse helper embaralha: num ramo faz `8 * tamanho_deck` pares de trocas de
posições aleatórias; outro ramo troca de trás para frente usando índice
aleatório no prefixo. Há ajustes para modo automático e um teste por
0x0044BAA0, que não foram interpretados completamente. Não avaliamos a
qualidade da aleatoriedade, a semente ou todos os caminhos de embaralhamento.

## Pot of Greed, Jar of Greed e Skelengel

| Carta | ID | Índice | Callback principal | Quantidade |
|---|---:|---:|---|---:|
| Skelengel | 539 / 0x21B | 440 | 0x005887C0 | 1 |
| Pot of Greed | 1010 / 0x3F2 | 291 | 0x005887C0 | 2 |
| Jar of Greed | 1447 / 0x5A7 | 290 | 0x005887C0 | 1 |

**0x00593CA0** prepara o destinatário como o lado do ator via 0x00592A50.
0x005887C0 lê esse lado de pedido+6, testa pedido+4 bit 4, escolhe a
quantidade pelo ID e chama 0x00578B60. O retorno baixo é zero. O tipo de
ativação e as demais condições de FLIP/armadilha não foram reconstruídos
novamente para cada carta neste estudo.

Pot e Jar também compartilham disponibilidade **0x00580780**. Ela exige
contagem de deck >=2 para ID 1010, >=3 para ID 0x3C8 e >=1 nos demais
casos. O lado consultado é `(pedido+2 bit14) XOR (pedido+2 bit0)`, não uma
constante “jogador local”. Skelengel não tem callback nesse slot.
Portanto disponibilidade prévia e falha na compra são mecanismos distintos.
O callback de resolução não repete esse helper nesse trecho; a interação
com modificações de deck entre ativação e resolução requer caminho específico
ou validação dinâmica, não foi demonstrada como cenário jogado.

## Falta de cartas e resultado

**0x005BC050**, consultado pelo agendador após contextos e callbacks, testa
primeiro máscara 0x100 dos dois lados. Com a marca de falta de cartas,
prepara bits 6–7 do byte 0x00A577FA e marca encerramento em 0x00A577F4.
O fluxo diferencia qual lado está marcado e mantém o caso de ambos. Sem
essa condição, há outros testes de vitória e de LP, ainda não expandidos
completamente aqui. Isso liga a tentativa de comprar do deck vazio ao
encerramento, sem inferir derrota só pela contagem zero isolada.

## Validação

`python3 scripts/validate_native_draw.py` confere fingerprint, três registros
de callbacks, despacho, sete transferências e cinco constantes/operadores.
[Relatório](evidence/native-draw-validation.json). É validação estática, não
execução de compras, embaralhamento ou cenários de correntes.

[Controladores](evidence/draw-sequence-controller.txt),
[stubs de fase](evidence/phase-command-stubs.txt),
[início e wrappers](evidence/native-draw-command-producer.txt),
[produtor comum](evidence/native-draw-shared-helper.txt),
[consumidor](evidence/native-draw-consumer.txt),
[transferência/embaralhamento](evidence/native-deck-to-hand-transfer.txt),
[mutações de listas](evidence/native-draw-list-mutations.txt),
[quantidades e marca](evidence/native-draw-counts-and-empty-flag.txt),
[exceções e encerramento](evidence/native-draw-special-kinds-and-terminal-checks.txt),
[callback](evidence/pot-of-greed-draw-callback.txt),
[disponibilidade e instruções](evidence/pot-of-greed-draw-eligibility.txt).
