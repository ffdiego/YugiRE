# Inventário validado com data.dat: 67 invocações, 18 cartas

**Data:** 2026-11-08. **Origem:** saída do `Joey.Tests` fornecida pelo operador após
`RunWithCatalog`, com os 67 fixtures automáticos de
`parallel-7yq78u8y`. O operador havia confirmado `576 checks passed` para
esta execução. Esses valores foram lidos do `data.dat` original no Windows;
o DAT não foi recebido no ambiente de análise.

## Distribuição por nível

| Nível da carta | Cartas distintas | Invocações observadas |
|---|---:|---:|
| 2 | 1 | 3 |
| 3 | 7 | 13 |
| 4 | 10 | 51 |
| 5–6 | 0 | 0 |
| 7+ | 0 | 0 |
| **Total** | **18** | **67** |

Portanto, esta bateria **não testa invocações com tributos** nem rejeições
nativas. Os testes C# com o DAT real verificaram: (1) identidade da carta
no comando CE; (2) gate estático `IsMonster / Level<=4 /
!RequiresSpecialSummon`; (3) retirada da carta da mão e colocação no slot
observado, usando `DemoDuel.Summon`. Não demonstram o gate dinâmico completo
nem a resposta a uma tentativa que o original não aceitou.

## Inventário de cartas reais

| Index | ID de recurso | Nível | Classe original | Invocações | Nome no DAT |
|---:|---:|---:|---:|---:|---|
| 100 | 44 | 3 | 19 | 2 | Oscillo Hero #2 |
| 151 | 99 | 3 | 15 | 1 | Battle Warrior |
| 385 | 1117 | 4 | 17 | 6 | Shining Angel |
| 525 | 63 | 4 | 12 | 8 | Tiger Axe |
| 591 | 1351 | 4 | 15 | 2 | Gearfried the Iron Knight |
| 630 | 15 | 2 | 18 | 3 | Time Wizard |
| 700 | 1443 | 3 | 17 | 1 | Bio-Mage |
| 828 | 607 | 4 | 17 | 4 | Shining Friendship |
| 834 | 3 | 3 | 1 | 6 | Baby Dragon |
| 862 | 141 | 3 | 18 | 1 | Flame Manipulator |
| 868 | 113 | 3 | 18 | 1 | White Magical Hat |
| 885 | 737 | 4 | 6 | 6 | Alpha The Magnet Warrior |
| 886 | 800 | 4 | 6 | 6 | Gamma The Magnet Warrior |
| 887 | 756 | 4 | 6 | 9 | Beta The Magnet Warrior |
| 1038 | 362 | 4 | 18 | 2 | Fairy's Gift |
| 1052 | 1401 | 3 | 15 | 1 | Swordsman of Landstar |
| 1093 | 1231 | 4 | 15 | 1 | Rocket Warrior |
| 1099 | 760 | 4 | 11 | 7 | Alligator's Sword |
| **Total** | | | | **67** | |

Os três Magnet Warriors de nível 4 (Alpha, Beta, Gamma) respondem por
**21/67 = 31,3%** das observações. Os 18 índices cobrem oito códigos
internos de classe; essa variedade de classificação **não** é prova de
oito regras de invocação distintas. Algumas cartas têm efeitos próprios,
que **não** são exercitados nem comparados por esses fixtures de invocação.

## Como desenhar o próximo experimento sem duplicar as 67 amostras

Separar os cenários, sem misturar o resultado de um preflight com
a prova de uma tentativa rejeitada:

1. **Invocação com um tributo:** monstros reais nível 5–6 + pelo menos um
   monstro elegível já presente, observar escolha do tributo, comando `0x57`,
   retirada do monstro e `0xCE`, mão, slot, cemitério, LP e flags.
2. **Invocação com dois tributos:** monstros reais nível 7+ + dois
   elegíveis, capturar a ordem de pagamentos e o estado final.
3. **Rejeição por falta de tributos:** mesmo monstro de nível 5+ sem
   candidatos; verificar que nenhuma carta é movimentada ou paga.
4. **Segunda invocação no mesmo turno:** observar recusa com a flag de
   invocação consumida; não afirmar que um gate consultado equivale a
   uma ação efetivamente tentada.
5. **Rejeição por invocação especial obrigatória:** por exemplo,
   uma carta de categoria Fusion/Ritual do catálogo original; identificar
   o retorno do gate nativo, sem converter um simples preflight em
   “tentativa do jogador” sem evidência.
6. **Campo cheio versus tributos disponíveis:** verificar a distinção
   já documentada no Ghidra: campo cheio não implica recusa quando
   os tributos podem liberar uma zona.

A IA da demonstração pode simplesmente não escolher determinadas ações;
**mais duelos não garantem** cobertura desses cenários. Um harness
determinístico precisará observar decisões de seleção antes de
intervir, e não tratar ausência de evento como validação negativa.
`FUN_005AAB90` é um gate de elegibilidade, não toda a orquestração
da invocação; `FUN_005ABD00` emite tributos `0x57` antes de `0xCE`.
São alvos diferentes para captura e exportadores separados.

**Prioridade sugerida:** construir primeiro a captura read-only
dos resultados do gate nativo (com estado/argumentos/retorno),
seguida de um piloto controlado de tributo; manter
o harness `DemoDuel` existente como baseline sem normalizar
resultados até a evidência do original ser observada.
