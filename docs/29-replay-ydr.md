# Replay YDR: amostra Relinquished, estrutura e primeiros eventos

Estado: análise estrutural de **uma única amostra** e observações relatadas pelo operador.
O arquivo original **não deve ser versionado**; fica em `scratch/replays/replaycomrelinquishe.ydr`
(essa pasta já consta no `.gitignore`). Não distribuir assets originais sem avaliar direitos de uso.

## Identidade da amostra

- Arquivo: `replaycomrelinquishe.ydr`
- SHA-256: `83ccbecef104789333c830fe6bc98553ebcb24ff889a2bc85acacd8a93c53f46`
- Tamanho: 8.716 bytes (0x220C)
- Assinatura: `YGR\0` (bytes 59 47 52 00)
- Cabeçalho em `0x04..0x0B`: 8 bytes que correspondem à identificação do `system.dat` estudado.
  O resumo público foi **redigido**, pois não precisamos versionar o identificador pessoal.
- Campo em `0x18`: pode ser timestamp Unix, **ainda não confirmado**.

## Estrutura verificável no arquivo (little-endian)

| Offset | Conteúdo |
|---|---|
| 0x00..0x1B | 28 bytes iniciais: assinatura, identificador e campos desconhecidos |
| 0x1C..0x75 | Conjunto de 3 listas do lado 0: tamanhos 42, 0, 0 |
| 0x76..0xD7 | Conjunto de 3 listas do lado 1: tamanhos 40, 0, 6 |
| 0xD8..0xDB | `uint32` = 1062 (número de registros) |
| 0xDC..0x220B | 1062 registros de 8 bytes = quatro `uint16` por registro |

A verificação `0xDC + 1062*8 == 0x220C` fecha exatamente com o tamanho.
As listas usam um `uint16` de contagem seguido por IDs de recurso `uint16`.
O bit alto do primeiro `uint16` de cada comando **parece** indicar lado; os
39 comandos com opcode `0x01` alternam perfeitamente o bit. Seu significado
completo exige evidências do consumidor nativo.

## Comandos relevantes e relato da partida

O operador relatou em 2026-10-31:

1. Venceu o duelo usando um deck que incluía Relinquished.
2. Relinquished equipou um monstro do adversário **virado para baixo, em defesa**.
3. Esse equipamento aparentemente não acrescentou ATK nem DEF ao Relinquished;
   um ataque com ele causou **zero dano**.
4. O golpe final foi de outro monstro, **possivelmente Mystic Tomato**, com
   1400 pontos de dano calculado, quando o oponente tinha cerca de **50 LP**.

As observações são evidência do operador, **não foram inferidas independentemente do YDR**.
Em especial, 1400 é o valor do comando, não necessariamente a quantidade de LP
que restava disponível para subtração (o motor satura LP em zero).

| Registro | Offset | `raw` | Args decimais | Evidência e interpretação cautelosa |
|---:|---:|---:|---|---|
| 1039 | 0x2154 | `0x8072` | 730, 9473, 0 | Referência a ID 730, Relinquished |
| 1044 | 0x217C | `0x80B2` | 1793, 257, 5 | Candidato a operação da carta/equipamento, **não comprovado** |
| 1051 | 0x21B4 | `0x8030` | 0, 331, 0 | Comparação de combate referindo índice interno 331, Relinquished |
| 1052 | 0x21BC | `0x8031` | 0, 0, 0 | Apresentação/resultado sem dano; **não segue um 0x43 de dano** |
| 1058 | 0x21EC | `0x8030` | 0, 331, 0 | Outra comparação contendo 331; identidade do atacante **não comprovada** |
| 1059 | 0x21F4 | `0x8031` | 1400, 0, 4 | Resultado com valor 1400 |
| 1060 | 0x21FC | `0x0043` | 1400, 0, 1 | Atualização de LP confirmada pela RE anterior |
| 1061 | 0x2204 | `0x8004` | 0, 0, 0 | Possível encerramento do duelo |

O catálogo do projeto associa Relinquished a **índice interno 331**, ID 730;
Mystic Tomato a **índice interno 221**, ID 1123. A presença de 331 no `0x30`
**não significa** necessariamente que o Relinquished foi o atacante. Não
rotular 1059/1060 como dano causado por Relinquished. O usuário apenas
identificou Mystic Tomato como possibilidade, ainda não confirmação absoluta.

Também há `0x8030 (178,221,0)` / `0x8031 (0,0,0)` nos registros 1015/1016;
esse contexto, 221 referencia Mystic Tomato. Não confundir esse episódio com
o final, nem concluir que todo registro `0x31` zerado corresponde ao mesmo tipo
de evento.

## O que investigar no Ghidra

- Consumidores/serializadores de assinatura `YGR`, contagem e registros
  `4 × uint16`; confirmar a natureza de `0x8000` e dos argumentos.
- Produtor e consumidor de `0xB2`, especialmente após `0x72` com ID 730.
- Relação entre `0x30`/`0x31` e os descritores de combate em `docs/22-battle-core.md`.
  Separar índice de carta, lado, zona, atacante/alvo e atributos calculados.
- Cálculo ATK/DEF de Relinquished quando o alvo equipado está virado para baixo.
  Confirmar se zero vem de face-down ou de outra condição.
- Caminho de `0x43` com valor 1400 e saturação de LP quando o alvo tem 50 LP.
- Investigar a possibilidade de converter replays em **entradas de ações** para
  regressão. Um log de comandos pós-resolução pode servir como *oráculo*, mas
  aplicá-lo diretamente no motor C# não testa as regras que produziram o log.

## Artefatos e validação

- Inspetor: `scripts/inspect_joey_replay.py` (somente leitura; `YGR` exploratório).
- CSV completo: `docs/evidence/replays/relinquished_commands.csv`.
- Estatísticas: `docs/evidence/replays/relinquished_summary.json` (ID de save omitido).
- Testes: `python -m unittest discover -s tests -p 'test_joey_replay.py'`.
- Amostra local opcional: `scratch/replays/replaycomrelinquishe.ydr` (não versionada).

O CSV não prova sozinho autoria de ataques, dano real aplicado ou semântica
completa do efeito do Relinquished. Anotações devem sempre distinguir
**bytes**, **hipóteses do Ghidra** e **relato do operador**.
