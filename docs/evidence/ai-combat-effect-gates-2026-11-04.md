# Efeitos e filtros que alimentam ATK/DEF no Joey (2026-11-04)

Fonte primária: `ai-combat-effect-gates.txt` (435 linhas),
extração Ghidra enviada pelo operador para o EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Cruzar com [cálculo de atributos](ai-combat-computed-stats-2026-11-04.md),
[layout de stats](ai-combat-stat-layout-2026-11-04.md) e
`card-callback-catalog.json`. Apenas análise estática;
não foram executados os helpers no jogo.

## FUN_0056FD40(ctx, associatedCard, &atkDelta, &defDelta)

**Contribuições por recurso de equipamento**, usando o tipo
(`ctx[+2]`), atributo (`ctx[+3]`) e ID de recurso
do monstro (`ctx[+0]`) quando necessário.
A rotina escreve em dois `int*`, somando os deltas.

Exemplos **confirmados no código**, antes de outros
modificadores e restrições:

| Recurso | Carta (catálogo YugiRE) | Δ ATK | Δ DEF | Condição |
| --- | --- | ---: | ---: | --- |
| `0x28A` | Kunai with Chain | +500 | 0 | Ramo direto |
| `0x291` | Metalmorph | +300 | +300 | Ramo direto |
| `0x12F` | Axe of Despair | +1000 | 0 | Ramo direto |
| `0x138` | Horn of Light | 0 | +800 | Ramo direto |
| `0x139` | Horn of the Unicorn | +700 | +700 | Ramo direto |
| `0x140` | Malevolent Nuzzler | +700 | 0 | Ramo direto |
| `0x412` | Sword of Deep-Seated | +500 | +500 | Ramo direto |
| `0x422` | Ring of Magnetism | -500 | -500 | Ramo direto |
| `0x58C` | Mask of Brutality | +1000 | -1000 | Ramo direto |
| `0x4DA` | Beast of Gilfer | -500 | 0 | Ramo direto |
| `0x58E` | Lightning Blade | +800 | 0 | tipo `0x0F` |
| `0x49E` | Sword of Dragon's Soul | +700 | 0 | tipo `0x0F` |
| `0x604` | Cyclon Laser | +300 | 0 | monstro de recurso `0x53B` |
| `0x12D` | Sword of Dark Destruction | +400 | -200 | atributo `0x0200` |

Há ainda ramos para tipos/atributos específicos, incluindo
alguns equipamentos que aplicam `+300/+300`,
`+400/-200`, ou `+700/0`. A reprodução offline
é **parcial**, com `None` para recursos não modelados.

**Distinção essencial:** o bônus direto de
Metalmorph (`+300/+300`) é produzido nesse helper,
mas `FUN_005B0F40` pode calcular também uma
contribuição **contextual durante batalha** com
`count(Metalmorph) * FUN_005BE050(stat)`.
Não misturar os dois caminhos como uma fórmula
universal de equipamento.

## FUN_0056DAA0: contagem de registros, não status de ativação

`FUN_0056DAA0(side,slot,resourceId)` percorre
os vínculos do monstro e conta entradas de
categorias `3, 11, 12, 13` cujo recurso
referenciado é igual ao solicitado.

O corpo **não demonstra que todos os vínculos
contados estejam ativos**: não há, nesta função,
o mesmo conjunto de filtros de suspensão
de `FUN_0056D790`.

`FUN_0056DD30` consulta
`FUN_0056DAA0(side,slot,0x3C7)`
para **Shield & Sword**. O resultado
é examinado quanto à paridade: quantidades
ímpares invertem os DWORDs ATK/DEF
(`out+4` ↔ `out+8`), quantidades pares
não invertem. Não se sabe ainda a origem
de todos os vínculos dessa categoria.

## FUN_0056D740: contagem contextual (Mirror Wall)

```cpp
int CountConditionalLinks(side, slot, resourceId) {
    int encoded = FUN_00402460(resourceId);
    if (FUN_004025D0(encoded) == 0x16 &&
        FUN_0056C460(side, slot) != 0)
        return 0;
    return FUN_0056D790(side, slot, resourceId);
}
```

Esse é o contrato do wrapper de contagem.
`FUN_0056D790` tem suas próprias condições
para contar vínculos válidos; o wrapper pode
**zerar a contagem** quando o tipo do recurso
é `0x16` e `FUN_0056C460` retorna não zero.

No final de `FUN_0056DD30`, o recurso
`0x44B` foi identificado no catálogo como
**Mirror Wall**. A contagem retornada por
`FUN_0056D740(side,slot,0x44B)`
determina quantas vezes aplicar:

```cpp
attack = (attack * 5 + 5) / 10;  // valor não negativo
```

Se há duas ocorrências, são **duas rodadas
com arredondamento individual**, não uma
única divisão por quatro. Por exemplo:
`2001 → 1001 → 501`.
O trecho de assembly `0x0056F258..`
confirma a iteração.

Isso demonstra o comportamento do **calculador
de atributos**; a contagem de vínculos em
um duelo real continua condicionada ao seu
estado e às regras do motor.

## FUN_005674E0: equivalência limitada de recursos

Os argumentos nativos são **índices internos**,
convertidos em ID de recurso por
`FUN_004022E0`. Para IDs maiores ou iguais a
2000, o código subtrai **2000 uma única vez**
(`if (1999 < resourceId)`).

Após isso, a função retorna 1 para recurso
idêntico e para alguns pares específicos:

| Recurso A | Recurso B | Nomes confirmados pelo catálogo |
| --- | --- | --- |
| `0x3EB` | `0x40A` | Polymerization / Polymerization |
| `0x39` | `0x76D` | Kuriboh / Kuriboh |
| `0x3D` | `0x4E1` | Sem nomes no catálogo parcial |
| `0x22` | `0x4BA` | Sem nomes no catálogo parcial |
| `0x4DD` | `0x76E` | Só `0x76E` consta como Scapegoat |

Ela **não** considera qualquer recurso distinto
equivalente, mesmo quando os nomes se parecem.
Essa equivalência aparece como parte da validação
usada no filtro associado a **Prohibition**;
não significa automaticamente que as cartas
tenham efeitos intercambiáveis em todo o jogo.

## Ferramenta offline adicionada

`scripts/ai_combat_effect_rules.py` contém
implementação pura e **parcial** de:
- normalização de ID de recurso;
- os cinco pares de equivalência;
- deltas diretos e condicionais de equipamentos
  selecionados de `FUN_0056FD40`;
- arredondamento de Mirror Wall com repetição.

```powershell
python -m unittest discover -s tests -p test_ai_combat_effect_rules.py -v
```

Cinco testes sintéticos de equivalência,
bônus condicionais e redução sucessiva foram
executados localmente. **Nenhum teste comparou
a saída com o Joey em execução.**

A ferramenta não verifica se o efeito está
ativo ou se a carta está equipada, não lê
a memória do duelo e não substitui o motor
de regras. `None` significa regra fora
do subconjunto modelado.

## Próximo passo recomendado: observação passiva

O maior retorno, agora, tende a vir de **dados
dinâmicos de avaliação**, não de mais uma
cadeia longa de `tempfunc`:

- Registrar entrada/saída de `FUN_005B1360`
  (`side`, modo do passe, `handIndex`
  escolhido), **apenas com hook passivo**.
- Observar `FUN_005B0F40` (evento de
  quatro bytes) e o resultado de
  `FUN_0056DD30` (contexto de 12 bytes).
- Registrar se o evento foi produzido no
  **snapshot hipotético** ou depois de
  uma ação resolvida. Não tratar `FUN_005B0950`
  como transação completa sobre toda a memória.
- Cruzar traces com os dois decodificadores
  offline já disponíveis.

Os buffers do simulador são globais e sua
reentrância é desconhecida. **Não chamar**
`FUN_005B1610`, `FUN_005B0A50` nem
qualquer mutador hipotético de forma avulsa
pelo Frida. O loader ativo continua restrito
a `summon`/`set` no lado 1.
