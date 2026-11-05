# IA: layout dos atributos, monstros ocultos e vínculos (2026-11-04)

Fonte: `ai-combat-stat-layout.txt`, relatório Ghidra fornecido pelo
operador (292 linhas). EXE validado SHA256:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Somente análise estática, sem nova execução do Joey.

## Descoberta principal: avaliação com informação parcial

`FUN_005B0F40` extrai um contexto do atacante por
`FUN_005ADD60(side,slot,out)` e um do alvo por
`FUN_005ADDC0(opponent,slot,out)`.

O contexto aparentemente contém 12 bytes:
`+0..1` ID interno, `+2` e `+3` metadados de tipo,
`+4..7` e `+8..11` dois atributos numéricos.
A interpretação precisa de cada atributo depende
de `FUN_0056DD30`.

**FUN_005ADD60:** se a flag `positionFlags & 0x02`
não está marcada, marca temporariamente, chama
`FUN_0056DD30` para calcular os valores reais
e remove novamente esse bit. Caso já estivesse
marcada, chama `FUN_0056DD30` diretamente.

**FUN_005ADDC0:** se a flag `0x02` está marcada,
calcula os atributos normalmente. Caso contrário,
consulta `FUN_004026C0(internalCard)` e preenche
um contexto substituto, sem revelar atributos reais:

| Classe da carta oculta | Valores gravados em +4 e +8 |
| --- | ---: |
| `<= 4` | 1000, 1000 |
| `> 4` | 1900, 1900 (`0x76C`) |

Os metadados nos offsets 0..3 são zerados.
É uma heurística específica para um adversário
com informação oculta, **não uma regra de combate**
universal nem prova de que todas as rotinas da
IA tratem cartas viradas para baixo do mesmo modo.
O avaliador ainda conhece a classe interna.

## FUN_005BE050: metade arredondada

A decompilação é:

```cpp
int FUN_005BE050(int value) {
    return (value * 5 + 5) / 10;
}
```

Para valor não negativo e sem overflow,
equivale a `ceil(value/2)`. Não assumir a
mesma equivalência para negativos: divisão
inteira de C trunca em direção a zero.

## FUN_0056D790: contagem de equipamentos/vínculos

`FUN_0056D790(side,monsterSlot,resourceId)`
retorna uma **contagem**, não apenas verdadeiro/falso.
Percorre vínculos do monstro (quantidade no offset
`0xA55D7E`, lista no offset `0xA55DC0`);
aceita códigos de tipo `1,2,6,7,8`;
identifica side/slot da carta associada;
exige ID de recurso igual, bit de desativação
`0x20` ausente e `FUN_0056B440(...)=0`.
A fórmula de `FUN_005B0F40` usa
`FUN_0056D790(...,0x291)` para
**Metalmorph** e adiciona
`count * FUN_005BE050(stat)`.
O campo exato de `stat` ainda depende
da inspeção do produtor `FUN_0056DD30`.

## FUN_0056D8D0: contagem contextual de efeitos

`FUN_0056D8D0(side,monsterSlot,resourceId,targetSide)`
percorre vínculos do mesmo conjunto de tipos e
consulta tanto o ID do efeito quanto o lado
de origem e flags. Há um filtro antecipado via
`FUN_00402460`, `FUN_004025D0` e
`FUN_0056C460`. Seus chamadores em
`FUN_005B0F40` consultam recursos
`0x521` (**Fairy Meteor Crush**) e
`0x604` (**Cyclon Laser**) na avaliação
de combate contra defesa. Não basta contar
todas as cartas equipadas pelo nome.

## Decodificador offline de eventos incluído

`scripts/decode_ai_combat_event.py` interpreta
o descritor **de quatro bytes little-endian**
produzido pelo avaliador `FUN_005B0F40` e
selecionado por `FUN_005B0D90`.

```powershell
python .\scripts\decode_ai_combat_event.py A8096400 200406FF
python .\scripts\decode_ai_combat_event.py --json 08080000
python -m unittest discover -s tests -p test_decode_ai_combat_event.py -v
```

Campos: slots 4..6 e 7..9, score assinado
na segunda palavra, bits `0x02` rota especial,
`0x04` seleção pelo loop externo,
`0x08` utilidade, `0x400` remoção hipotética
do atacante, `0x800` remoção do defensor e
demais bits sem nome. A condição base de
utilidade é reconstruída, mas não aplicada
à rota especial (`0x02`) com construtor
distinto.

**Os três eventos hex de exemplo são sintéticos,
não capturas do Joey.** Esses eventos não são
comandos `.ydr` (o replay tem registros
de oito bytes); o console `state`/`diff`
não os fornece. Para comparação dinâmica,
precisaremos capturar `DAT_00A579A6` num
contexto de simulação via hook passivo, sem
chamar os helpers mutantes.

## Próxima extração

Decompilar `FUN_0056DD30` para mapear
os campos reais e `FUN_0056B440` para
entender as condições dos vínculos:

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-combat-computed-stats.txt `
  -Addresses @(
    '0056dd30',
    '0056b440',
    'ins:0056dd30:85',
    'ins:005b10a0:90'
  )
```

O decodificador possui testes offline e
não modifica o executor, a DLL ou o agente Frida.
