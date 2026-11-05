# IA: layout computado e restrições de efeitos (2026-11-04)

Fonte primária: `ai-combat-computed-stats.txt` enviado pelo operador,
relatório de Ghidra com ~1019 linhas para EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Análise estática, sem execução do Joey ou injeção de funções.
Continua [layout de atributos](ai-combat-stat-layout-2026-11-04.md)
e [fórmula de confronto](ai-combat-outcome-scoring-2026-11-04.md).

## O layout de 12 bytes está confirmado

`FUN_0056DD30(side,slot,out)` inicializa:

| Offset | Tipo de memória | Origem | Interpretação |
| --- | --- | --- | --- |
| +0 | uint16 | identidade `field & 0xFFF` | índice interno |
| +2 | uint8 | `FUN_0056F6D0` | código de tipo |
| +3 | uint8 | `FUN_0056F9E0` | código de atributo |
| +4 | int32 | `FUN_004027B0` e ajustes | ATK calculado |
| +8 | int32 | `FUN_00402800` e ajustes | DEF calculada |

O assembly em `0x0056DD79`–`0x0056DE22`
grava campos em `[EBP]`, `[EBP+2]`,
`[EBP+4]` e `[EBP+8]`; os bits de
identidade são limitados por `0xFFF`.
O pseudocódigo usa `param_3[2]` e
`param_3[4]` para os dois DWORDs.

```cpp
struct CombatStats {
    uint16_t internalCardIndex; // +0
    uint8_t typeCode;           // +2
    uint8_t attributeCode;      // +3
    int32_t attack;             // +4
    int32_t defense;            // +8
};
static_assert(sizeof(CombatStats) == 12);
```

Se a identidade é 0 ou a zona é >=5,
a rotina retorna após inicializar atributos
com zero. Caso a posição não tenha bit `0x02`,
retorna após preencher os valores-base.
**O código nativo aplica modificadores
somente em certos contextos de visibilidade.**

## Não é só ATK base + equipamentos

Sob um gate `FUN_0056B440(...)=0`, o
código ativa uma árvore grande de condições
por IDs de recurso e estado, incluindo:
- bônus/penalidades dependentes de mão,
  cemitérios, posições, atributos e turnos;
- transformações para metade ou dobro
  de atributos em recursos específicos;
- efeitos de magias/armadilhas e equipamentos
  associados ao monstro, processados em
  categorias de vínculos;
- efeitos de troca dos dois atributos;
- modificadores globais e de campo.

O recurso `0x3C7` do catálogo é
**Shield & Sword**. `FUN_0056DAA0(side,slot,0x3C7)`
controla um teste de paridade que, quando
satisfeito, faz:

```cpp
swap(*(int32_t*)(out+4), *(int32_t*)(out+8));
```

`DAT_00A577EC & 0x100000` altera o sinal
de aplicação de um grupo de modificadores;
valores negativos finais são limitados a zero.
Por fim, `FUN_0056D740(side,slot,1099)`
determina quantas vezes aplicar
`FUN_005BE050(attack)`: metade arredondada
para cima para ataques não negativos.

**Cuidado:** a função inteira é longa e
repleta de branches por carta; a extração
não permite reproduzir todos os efeitos
exatamente apenas com esses exemplos.
`FUN_0056DD30` pode ler diversos globals
de estado da partida.

## FUN_0056B440: vínculos e Prohibition

A função é usada como filtro de supressão
em vários contextos, e o relatório revela:
- para `param_2 != 0`, percorre os cinco
  slots de magia/armadilha (5..9) de ambos
  os jogadores;
- procura cartas com recurso `0x48A`,
  identificado no catálogo como **Prohibition**;
- testa flags de ativação e de bloqueio da
  própria carta;
- percorre entradas de vínculos e compara
  códigos `9` ou `0xB`;
- para vínculos tipo 9, chama `FUN_005674E0`
  para comparar a carta/parâmetro;
- para tipo `0xB`, compara uma referência
  com `FUN_00402460(0x48A)` e o byte de
  metadados com `param_2`.

O resultado **1** indica um bloqueio/gate
para os callsites observados; **0** permite
prosseguir. O contrato detalhado de exceções
e o papel do segundo argumento requerem
análise de `FUN_005674E0`.
Não nomear esse helper como simples
`IsProhibited(card)` sem verificar os
vínculos que ele consulta.

A análise corrige a descrição anterior
de `FUN_0056B440` como uma caixa-preta
genérica; há ligação direta com Prohibition,
sem provar o comportamento exato de cada
carta sob esse efeito.

## Ferramentas offline para o laboratório

Foi criado o leitor `scripts/decode_ai_combat_stats.py`,
com testes de layout, valores assinados,
estatísticas ocultas, validação de formato e
saída JSON. Ele **somente interpreta os 12 bytes
já capturados**; não calcula modificadores
nativos nem executa o jogo.

```powershell
python .\scripts\decode_ai_combat_stats.py 1101060F40060000B0040000
python .\scripts\decode_ai_combat_stats.py --json 00000000E8030000E8030000
python -m unittest discover -s tests -p test_decode_ai_combat_stats.py -v
```

O primeiro exemplo é um contexto **sintético**
para índice 273, tipo 6, atributo 15, ATK 1600,
DEF 1200. O segundo é o contexto sintético
de monstro oculto avaliado como 1000/1000.
Não há amostras reais de buffer de combate.

## Próximo estudo Ghidra

A investigação de atributos deve focar
os helpers responsáveis pelas contribuições
dinâmicas e pela restrição contextual:

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-combat-effect-gates.txt `
  -Addresses @(
    '0056fd40',
    '0056daa0',
    '0056d740',
    '005674e0',
    'ins:0056f200:80'
  )
```

Depois, instrumentar passivamente
`FUN_005B1360` e `FUN_005B0F40`
e comparar dados capturados com o
decodificador sem chamar os helpers
de alteração de campo diretamente.
