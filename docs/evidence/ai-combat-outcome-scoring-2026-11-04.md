# Como o Joey pontua o resultado de cada combate hipotético (2026-11-04)

**Fonte primária:** `ai-combat-outcome-scoring.txt`, extração Ghidra
enviada pelo operador (515 linhas), EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
**Fontes cruzadas:** [eventos de combate](ai-combat-event-producer-2026-11-04.md),
[baseline/score](ai-simulated-combat-scoring-2026-11-04.md)
e `card-callback-catalog.json` para nomes de IDs.
Resultados **estáticos**; a fórmula ainda não foi medida no
Joey em execução por hook de observação.

## Resultado central: score assinado + flags de destruição

`FUN_005B0F40(side,attackerSlot,targetSlot,outEvent)`
usa `FUN_005ADD60` no atacante e `FUN_005ADDC0`
no alvo para obter estruturas temporárias de atributos.
Os nomes/offsets exatos dos campos são hipóteses até
decompilar esses dois produtores; os campos são usados
na comparação de força.

O evento tem **quatro bytes** em dois `ushort`.
A função constrói a palavra baixa:

```cpp
low = (((targetSlot & 7) << 3 | (attackerSlot & 7)) << 4)
      | (oldLow & 0xF005);
// bits 4..6: atacante; bits 7..9: alvo
```

As contribuições `param_1` e `param_3` (locais
reutilizados pelo Ghidra, **não** os argumentos
originais da função) são ajustadas por posição,
atributos, efeitos e proteções. No desfecho:

```cpp
outEvent[1] = (int16_t)param_1 - (int16_t)param_3;
```

A subtração é armazenada em 16 bits; os comparadores
posteriores interpretam o `ushort` como **signed short**.
A métrica é uma **vantagem líquida estimada**
no combate, não necessariamente variação real de LP.

A função define flags da palavra baixa:

| Bit | Significado comprovado no fluxo |
| --- | --- |
| `0x08` | Evento considerado útil/selecionável |
| `0x400` | Remoção hipotética do monstro do atacante |
| `0x800` | Remoção hipotética do monstro do defensor |
| `0x10..0x40` | Slot atacante (bits 4..6) |
| `0x80..0x200` | Slot alvo (bits 7..9) |

O significado de `0x400` e `0x800` vem também
de `FUN_005B0A50`, que limpa a identidade
do respectivo monstro **na simulação**.

### Regra de utilidade: não basta infligir dano de LP

O trecho final:

```c
if ((short)score < 1 &&
    ((flags >> 1 & 0x400) <= (flags & 0x400)))
    useful = false;
else
    useful = true;
```

é logicamente equivalente, para essas duas flags, a:

```cpp
bool useful =
    (int16_t(score) > 0) ||
    ((flags & 0x800) != 0 && (flags & 0x400) == 0);
```

**Isso é evidência direta de que o avaliador
considera a vantagem de remover uma carta adversária
sem perder a própria, mesmo quando o score é
zero ou negativo.** O bit de utilidade resultante
é consumido por `FUN_005B0D90`, que procura
o melhor alvo.

## Ramos gerais de batalha

O teste `targetPositionFlags & 1` separa
dois comportamentos coerentes com ataque e defesa:

| Comparação | Sem bit de defesa no alvo | Com bit de defesa no alvo |
| --- | --- | --- |
| Força do atacante maior | Marca `0x800` (defensor removido), contribuição positiva | Pode marcar `0x800`; dano depende de efeitos de perfuração |
| Igualdade | Marca `0xC00` (ambos removidos), se força positiva | Sem remoção padrão |
| Força do defensor maior | Marca `0x400` (atacante removido), contribuição negativa | Contribuição negativa; sem remoção padrão |

Essa tabela resume apenas os ramos-base.
`FUN_0056D8D0`, `FUN_0056D790`,
`FUN_005BE050` e exceções por ID podem
modificar atributos, dano e flags.
Também há flags `playerFlags & 0x400000`
de cada lado que limpam `0x400`/`0x800`
e zeram a respectiva contribuição; não atribuir
nome definitivo à flag sem análise da origem.

## Cartas/especificidades identificadas pelo catálogo

| Recurso | Nome em `card-callback-catalog.json` | Evidência em `FUN_005B0F40` ou filtros |
| --- | --- | --- |
| `0x4E5` | Kinetic Soldier | bônus de 2.000 sob condição de tipo `0x0F` |
| `0x291` | Metalmorph | consulta `FUN_0056D790` e adiciona termo baseado em valor transformado por `FUN_005BE050` |
| `0x53D` | Mad Sword Beast | ramo especial de contribuição quando enfrenta monstro em defesa |
| `0x521` | Fairy Meteor Crush | condição de perfuração via `FUN_0056D8D0` |
| `0x604` | Cyclon Laser | segunda condição de perfuração via `FUN_0056D8D0` |
| `0x58F` | Tornado Wall | combinado com `0x14D`, pode zerar dano |
| `0x14D` | Umi | campo exigido no ramo de Tornado Wall e nas exceções de alvo |
| `0x52E` | The Legendary Fisherman | usado por `FUN_0056BD80` e `FUN_0056BF20` ao avaliar alvos sob Umi |
| `0x540` | Goblin Attack Force | filtros específicos em `FUN_005B05A0` |

IDs `0x1DD`, `0x11F`, `0x199`, `0x4CF`,
`0x4E3` e outros também têm ramos no código;
**não atribuir nome a eles com base no catálogo
parcial de callbacks**. O caso `0x4CF` altera
a contribuição para um valor associado a 500,
mas exige estudo próprio.

### Rota especial com destino 5

`FUN_0056BF20(side,slot)` verifica que os
registros de monstro do adversário estejam
vazios ou preenchidos **apenas** com
The Legendary Fisherman (`0x52E`) em
condições de Umi (`0x14D`), mais uma exceção
por recurso `0x5F3` do atacante.

`FUN_0056BFD0` habilita outros casos por IDs
de carta e funções auxiliares. Os dois
alimentam a rota de `FUN_005B0D90` que
usa `targetSlot=5` e `score=ATK` (como
`int16` no evento), **muito provavelmente
ataque direto**. O sentido de 5 ainda
deve ser confirmado pelo consumidor de
comandos reais; esta evidência não prova
que todo `5` implique dano direto efetivado.

## Restrições de preferência de combate: FUN_005B05A0

Além dos gates de legalidade `FUN_0056BD80`,
há **heurísticas específicas**:

- para recursos `0x2F9` e `0x2E8`,
  sob contexto marcado, exige a existência
  de outro monstro aliado com ATK inferior;
- recurso `0x540` (Goblin Attack Force)
  condiciona ataques a alvos e patamares
  de ATK/DEF (ex.: 1900 ou 2000);
- na rota `targetSlot > 4`, compara ATK
  com LP do rival e o LP próprio, incluindo
  uma proteção condicional para LP próprio
  acima de 4.000;
- em outros ramos, compara a força máxima
  adversária com `attackerATK + 1000`;
- há exceção especial quando o monstro
  adversário é Dreamsprite (`0x5F2`).

Esses filtros mostram uma política mais
elaborada que simples legalidade de ataque:
`FUN_005B05A0` é um **filtro de decisão da IA**,
não apenas uma função do motor de regras.
É necessário cuidado ao transportar essas
heurísticas para uma reimplementação.

O pseudocódigo de `FUN_005B05A0` contém
`FUN_0056F6B0()` sem parâmetros em um ramo,
provavelmente artefato de assinatura do Ghidra:
verificar o assembly antes de chamar esse helper.

## Próxima etapa: validar o layout dos atributos

A formula já esclareceu a orientação
da vantagem e as flags. Falta confirmar os
campos e ajustes provenientes de:

- `FUN_005ADD60` e `FUN_005ADDC0`: saída
  para atacante/alvo, atributos e bits de posição;
- `FUN_005BE050`: transformação numérica
  aplicada a atributos, particularmente
  com Metalmorph;
- `FUN_0056D790`: contribuição de Metalmorph;
- `FUN_0056D8D0`: consulta de efeitos
  que permitem dano contra defesa.

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-combat-stat-layout.txt `
  -Addresses @(
    '005add60',
    '005addc0',
    '005be050',
    '0056d790',
    '0056d8d0',
    'ins:005b0f90:85'
  )
```

Depois disso, um **decodificador offline do evento
de 4 bytes** poderá reproduzir score e flags e
ser confrontado com traces passivos, sem ativar
o simulador nativo no duelo real. Nenhum hook
foi alterado neste estudo.
