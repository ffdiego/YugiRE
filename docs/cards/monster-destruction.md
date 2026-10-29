# Destruição por efeito — continuação de Man-Eater Bug

Análise estática de 2026-10-29, mesmo executável e fingerprint do
[estudo de seleção](man-eater-bug-targeting.md). Todos os endereços são VA,
image base 0x00400000. Consultas Ghidra 12.2 somente leitura; nenhum jogo
executado nem banco alterado.

## Resultado

O caminho de Man-Eater Bug passa de alvo validado para uma máscara de posições
e usa o mecanismo compartilhado de movimentação. Para o caso comum, emite
**0x79**, cujo consumidor retira a origem e insere a carta na **região 14**.
Um desvio associado a **Banisher of the Light** seleciona a **região 15**.
Interpretamos essas regiões como cemitério e remoção de jogo, respectivamente:
além das escritas distintas, a descrição local de Banisher diz explicitamente
que cartas destinadas ao cemitério são removidas de jogo.

As quatro cartas de ficha têm tratamento por intervalo de ID e não são
acrescentadas a nenhuma dessas duas listas. A análise fecha o encaminhamento
e a escrita de estado do caso comum; não reconstrói todas as interações.

## Máscara, filtros e dependências

A cadeia já identificada é:

```text
0x00585C80 → 0x00577880 → 0x00576940
          → 0x00576960(pedido, máscara, 14, 2)
```

A máscara usa `1 << (lado*16 + posição)`; cada lado tem onze posições
percorridas. **0x00576300** filtra somente os bits solicitados chamando
**0x00576200**. Este rejeita origem vazia e bit já presente em
**0x00A577F0**, a máscara global de posições marcadas. Também consulta
**0x0056C460** quando a classe da carta de origem é 0x16. Há outra condição
para posições >4, classe 0x15 e flags do pedido; não participa de um alvo
de monstro nas posições 0–4.

0x0056C460 tem condições por IDs 0x52E/0x531, presença de 0x14D, flags de
posição e 0x0056B440. Esses nomes e a semântica completa da proteção não
foram estabelecidos aqui. Isso refina a condição extra encontrada no filtro
de Tribute; não é evidência suficiente para dar um nome geral a toda proteção.

Depois do filtro, **0x00576360** faz uma preparação recursiva: marca posições
e percorre dependências por IDs e listas de associação. A máscara impede
revisitar posições já marcadas nesse trecho. Há ramos explícitos para
**Premature Burial (0x488)** e **Call Of The Haunted (0x447)**, cujas descrições
locais mencionam destruição vinculada. Outros ramos incluem Toon World e
Destiny Board; não foram convertidos em uma matriz completa de regras.

Após atualizar flags por 0x00598D70, a rotina consulta
**0x005699C0(0x453)** e pode acrescentar flag **0x10000**. Esse ID é
**Banisher of the Light**, índice 741. 0x005699C0 soma as consultas dos dois
lados em **0x00569820**. Para monstros, o contador exige flags específicas,
ausência de bits de bloqueio e sucesso da verificação por 0x0056B440; também
**exclui posições já marcadas em 0x00A577F0**. Portanto, a carta simplesmente
existir em algum slot não basta para ativar o desvio. Em particular, uma
Banisher marcada na preparação não é contada nesse ramo. A ordem da consulta
está comprovada; o resultado visual de destruições simultâneas fica para teste.

0x00576960 restaura a máscara de trabalho como `máscara_filtrada | máscara_anterior`
e percorre os alvos filtrados. Antes de chamar **0x00576B30**, retira da
máscara o bit da posição em processamento. O helper pode então marcá-la e
emitir comandos sem ser barrado pela própria marca anterior.
Após o processamento, o retorno é `máscara_global_final XOR máscara_anterior`;
não tratá-lo como confirmação de animação concluída ou estado já aplicado.

## Emissão e consumo de 0x79

**0x00576B30(lado, posição, região, flags, índice_da_origem)** volta a conferir
ocupação, marca da posição e a condição de classe 0x16/0x0056C460.
A flag de posição `+0x8C & 4` barra outros destinos, mas o trecho permite
regiões 14/15 passar por essa verificação. Isso não elimina as demais restrições.

Para `região=14` e `flags & 2 != 0`, como no caminho examinado:

| Caso | Destino interno | Segundo argumento de dados de 0x79 |
|---|---:|---:|
| Carta comum, sem desvio | 14 | 1 |
| Desvio por flag 0x10000 | 15 | 16 / 0x10 |
| ID 0x780–0x783, sem desvio | 14 solicitado | 16 / 0x10 |

O comando inclui o lado no bit 15 do opcode. No primeiro argumento de dados,
a posição fica no byte baixo e a região de origem no byte alto. Para o
caminho de campo examinado, esse byte de região é zero.
O segundo argumento resulta de `(-contribui_para_cemitério & 0xFFFFFFF1) + 16`.
Não confundir **opcode 0x79** com endereço, posição ou flag 0x10000.

O dispatcher 0x005B9470 normaliza 0x79 para **79 decimal**, lê o ponteiro
**0x005B966D** e executa `JMP 0x00404B40`. O consumidor real desse comando
é **0x00404B40**.

O consumidor é uma máquina de estados. No **estado 2**, ele copia a palavra
de estado para 0x00A558B0 e chama:

1. **0x00570790** para retirar a origem.
2. **0x005708D0** para inserir no destino. O destino é 15 se o segundo
   argumento for 16; nos outros casos desse caminho, é 14.

Para uma posição de campo, 0x00570790 chama 0x0056A490 e zera os
**0x90 bytes** do registro por **0x005BDDF0**. Esse último helper contém
loops explícitos de escrita de zeros. Também limpa o bit da posição na
máscara global. Assim, a retirada é uma mudança concreta do estado.

0x005708D0 despacha a região 14 para **0x0056C590** e a 15 para
**0x0056C640**. Ambos derivam o bloco de destino do **bit 12 da palavra
copiada**, e não diretamente do lado em que a carta estava no campo.
Esse bit é consistente com o vínculo ao dono; mudança de controle não foi
testada dinamicamente.

| Região | Base da lista, por bloco | Contador |
|---|---|---|
| 14 | `0x00A566F0 + bloco*0xD44`, palavras de 4 bytes | byte em `0x00A55D6A + bloco*0xD44` |
| 15 | `0x00A5686C + bloco*0xD44`, palavras de 4 bytes | byte em `0x00A55D6C + bloco*0xD44` |

Os helpers copiam a palavra para o fim da lista e incrementam o contador.
Ambos retornam antes da inserção para IDs **0x780–0x783**, confirmados nos
recursos como Insect Monster Token, Kuriboh Token, Sheep Token e Slime Token.
Consequentemente, mesmo quando 0x79 encaminha uma ficha ao seletor de região
15, ela não fica armazenada nessa lista. Não é uma carta comum banida.

Após emitir a operação principal, 0x00576B30 chama **0x00577280**. Esse helper
percorre dependências e pode voltar a chamar 0x00576B30 para outras posições,
com destino 14 e flag 2. Isso liga a preparação recursiva à emissão efetiva
das operações vinculadas; a etapa inicial de marcação sozinha não move cartas.

## Validação e limites

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_monster_destruction.py
```

Esperado: oito linhas `PASS`. Revalida o estudo de alvos e preservação dos
originais/banco, o despacho de 0x79, trechos de instruções e as identidades
locais das exceções. O modelo transcrito cobre 64 combinações de lado,
bloco de destino, desvio e IDs, incluindo os limites externos 0x77F/0x784.
Não executa funções nativas nem emula os filtros e eventos completos.

Evidências: [orquestração](../evidence/monster-destruction-core.txt),
[filtros e preparação](../evidence/monster-destruction-filters.txt),
[elegibilidade](../evidence/monster-destruction-eligibility.txt),
[dispatch e remoção/inserção](../evidence/monster-destruction-commands.txt),
[consumidor e listas](../evidence/monster-destruction-application.txt),
[operações vinculadas](../evidence/monster-destruction-links.txt),
[instruções selecionadas](../evidence/monster-destruction-selected-instructions.txt),
[escritas e zeragem](../evidence/monster-destruction-write-instructions.txt),
[produtor já exportado](../evidence/parasite-initial-transfer.txt),
[relatório de validação](../evidence/monster-destruction-validation.json).

Pendências dinâmicas: monstro comum, Banisher ativa, Banisher incluída na
operação, ficha e carta controlada por outro lado. Comparar pedido, fila,
registro de origem e contadores/listas antes e depois. Permanecem fora desta
etapa o evento inicial de FLIP de Man-Eater Bug, todos os ramos de proteção,
ordem completa de efeitos vinculados, animações e equivalência com regras oficiais.
