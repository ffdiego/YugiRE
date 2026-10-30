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

O catálogo não valida execução da função nativa. Seleção efetiva dos tributos,
exclusão de cópias, pagamento e colocação final ainda exigem seguir os helpers.

Consulta final do checkpoint: [0x00599570](../evidence/ritual-level-subset-search.txt)
faz busca recursiva por níveis. Com nível candidato L, requisito restante R e
limite M, admite diretamente `R <= L < R + M`; para `L < R`, pode procurar
entre entradas anteriores com `R-L` e `min(M,L)`. Comparações e chamada
recursiva foram conferidas no listing. A relação desse critério com todas as
escolhas permitidas ao jogador permanece em investigação pelo chamador
0x00599140. Não foi executado teste nativo desse algoritmo. Timing permanece futuro.
