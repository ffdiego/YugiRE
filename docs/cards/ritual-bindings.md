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
