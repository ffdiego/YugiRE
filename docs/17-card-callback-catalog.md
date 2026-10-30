# Catálogo completo da tabela de callbacks

Inventário estático em 2026-10-29. Alvo SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Nenhuma execução do jogo, decompilação em massa ou modificação do banco.

## Resultado e cobertura

| Medida | Resultado |
|---|---:|
| Registros de 24 bytes | 443 |
| Combinações exatas dos cinco slots | 269 |
| Endereços distintos no slot principal, excluindo zero | 219 |
| Endereços distintos em todos os slots, excluindo zero | 482 |
| Registros com principal nulo | 18 |
| Registros com os cinco slots nulos | 15 |
| Registros com ao menos um nome não vazio | 439 |
| Registros associados a nome vazio | 3 |
| IDs registrados sem entrada correspondente em CARD_ID | 1 |

Foram catalogados **todos os registros desta tabela**, não todas as regras
do jogo. Dos 1114 IDs não sentinela em CARD_ID, 442 têm registro nela e 672
não têm. Ausência de registro ou callbacks nulos não demonstra ausência de
efeito: já encontramos regras globais e condições por ID fora dos callbacks.
Esses números não são uma medida de progresso da engenharia reversa.

## Fonte e interpretação dos slots

A tabela começa em **VA 0x005ED0A8**, offset de arquivo **0x001ED0A8**.
Cada registro contém ID uint32 seguido de cinco ponteiros uint32. No alvo,
os bits altos dos IDs são zero e todos os ponteiros não nulos estão na seção
executável .text, intervalo `[0x00401000, 0x005DA000)`.

O limite inclusivo **0x1BA** e o índice inicial **0xDD** da busca
**0x0059DBE0** sustentam a contagem de 443. O último registro é índice 442,
VA **0x005EFA18**. Os IDs são estritamente crescentes, de 15 a 1902.
[Listing da busca](evidence/card-handler-id-search.txt).

| Offset | Campo no catálogo | Uso observado nos estudos anteriores | Endereços distintos não nulos |
|---|---|---|---:|
| +4 | primary | Resolução principal por 0x0059DEB0 | 219 |
| +8 | candidate | Filtro por candidato, quando presente | 54 |
| +12 | availability | Verificação anterior à busca de candidato | 134 |
| +16 | preparation | Preparação; pode agendar custo | 29 |
| +20 | target_preparation | Preparação/seleção de alvo nos casos examinados | 46 |

Os nomes são convenções do catálogo baseadas no uso já seguido. Eles não
garantem que todos os callbacks de um slot tenham o mesmo contrato detalhado.
O catálogo registra ponteiros de entrada; não conta todas as funções chamadas
por eles, nem decide se dois endereços são wrappers de uma mesma implementação.

## Dois níveis de agrupamento

**Por principal:** cartas com o mesmo ponteiro em +4. Elas podem ter custos,
filtros e seleção diferentes, como Man-Eater Bug e Tribute.

**Por combinação exata:** igualdade dos cinco ponteiros. Os identificadores
`P001`–`P269` são atribuídos pela ordenação das tuplas. Mesmo essa igualdade
não prova comportamento igual: Sparks/Ookazi têm a mesma combinação, mas
o callback escolhe constantes diferentes por ID.

| Principal (VA) | Registros | Exemplos de nomes locais |
|---|---:|---|
| 0x00584500 | 44 | Cocoon of Evolution, Legendary Sword, Dark Energy |
| 0x00583F30 | 35 | Larvae Moth, Kuriboh, Toon World |
| nulo | 18 | Great Moth, Perfectly Ultimate Great Moth, Kinetic Soldier |
| 0x005999D0 | 16 | Black Luster Ritual, Zera Ritual, War-Lion Ritual |
| 0x00584E60 | 15 | Sparks, Ookazi, Catapult Turtle |
| 0x00584A90 | 13 | Forest, Wasteland, Mountain |
| 0x00588D90 | 9 | Magic Jammer, Royal Command, Seven Tools of the Bandit |
| 0x00585C80 | 8 | Man-Eater Bug, Tribute, Mystical Space Typhoon |
| 0x00586540 | 8 | Warrior Elimination, Eternal Rest, Stain Storm |
| 0x00584D00 | 6 | Mooyan Curry, Red Medicine, Dian Keto the Cure Master |

Não atribuímos rótulos semânticos completos a esses grupos somente pelos nomes.
Em particular, a diversidade em 0x00583F30 exige seguir a função antes de
concluir onde as regras dessas cartas são aplicadas.

## Relação com os casos já estudados

| Carta | ID | Registro | Combinação |
|---|---:|---:|---|
| Sparks | 342 | 66 | P045 |
| Ookazi | 345 | 69 | P045 |
| Man-Eater Bug | 500 | 91 | P072 |
| Parasite Paracide | 762 | 150 | P103 |
| Tribute to The Doomed | 1023 | 187 | P074 |
| Royal Command | 1530 | 418 | P126 |

O gerador confere os cinco ponteiros desses seis registros contra as
evidências anteriores. Um caso estudado não torna automaticamente conhecidos
os outros ramos por ID da mesma função compartilhada.

## Entradas sem nome atribuído

| Registro | ID | Situação |
|---|---:|---|
| 318 | 1245 / 0x4DD | Nenhum índice de CARD_ID corresponde a esse ID |
| 322 | 1251 / 0x4E3 | Índice 872, nome vazio |
| 327 | 1257 / 0x4E9 | Índice 171, nome vazio |
| 328 | 1258 / 0x4EA | Índice 1024, nome vazio |

As entradas são mantidas no catálogo. Não foram atribuídos nomes externos
nem classificadas como conteúdo removido ou código sem uso. IDs com +2000
e nomes iguais também permanecem distintos; não são unidos pelo nome.

## Próximas amostras sugeridas pelo inventário

1. **Legendary Sword / P025:** 41 registros com principal 0x00584500,
   candidato 0x0057A470 e preparação de alvo 0x00593A20. O conjunto de nomes
   sugere equipamentos; seguir uma amostra pode revelar vínculos e modificadores
   de atributos. O grupo por principal tem 44 registros, pois os demais slots
   variam em três entradas.
   A [amostra foi seguida](cards/legendary-sword.md): tipo dinâmico, associação
   tipo 1 e cálculo do bônus. A [limpeza comum](cards/equipment-cleanup.md)
   também foi seguida; outros movimentos e ramos por ID continuam abertos.
2. **Black Luster Ritual / P269:** 16 registros com principal 0x005999D0 e
   disponibilidade 0x00599850. Candidato para estudar seleção de materiais
   e invocação. O [mapeamento de pares e níveis](cards/ritual-bindings.md)
   foi concluído; seleção/pagamento/colocação permanecem em investigação.
3. **Giant Rat / P164:** seis registros com a mesma combinação, incluindo
   UFO Turtle e Shining Angel. Candidato para estudar gatilhos e busca no deck.

Essa seleção prioriza mecanismos novos e código compartilhado. Não exige
analisar cada registro individualmente nem montar cenários dinâmicos agora.

## Artefatos e reprodução

- [CSV completo](evidence/card-callback-catalog.csv): uma linha por registro,
  incluindo VA, ID, nomes/índices, combinação e os cinco ponteiros.
- [JSON completo](evidence/card-callback-catalog.json): hashes, estatísticas,
  registros, membros das combinações e agrupamento por principal. As listas
  `rows` referenciam o índice em `records`; ponteiros nulos são `null`.

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s YugiRE/tests -p 'test_card_callback_catalog.py'
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/catalog_card_callbacks.py
```

Esperado: **oito testes unitários** passam sem arquivos do jogo. Cobrem
truncamento, IDs inválidos/duplicados/fora de ordem, ponteiros fora do código,
nulos, diferenças entre os agrupamentos, identidades duplicadas, sentinela,
nomes vazios e limites da busca.

O gerador imprime duas linhas `PASS` e o resumo. Confere preservação pelo
validador existente, hashes do EXE/CARD_ID/nomes/propriedades, limites de
busca e seis registros conhecidos. Compara o modelo da busca nativa com um
dicionário para **todos os 65536 IDs uint16**, incluindo não registrados.
Isso valida a transcrição finita da busca, não a execução da função original.

Para reutilizar dados conhecidos, o gerador usa o inspetor limitado existente,
que carrega quatro recursos em memória e reescreve seu relatório determinístico
de Parasite. Somente nomes, IDs, classes numéricas e registros selecionados
para este catálogo são exportados; não são salvos payloads de recursos ou
bancos do Ghidra. Os relatórios são reproduzíveis para este fingerprint.
