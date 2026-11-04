# Contrato observado do construtor normal de summon/set

Data: 2026-11-04. Fonte primária fornecida pelo operador:
`summon-descriptor-builders.txt`, extração de pseudocódigo e
callers pelo Ghidra, SHA-256 do EXE:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Resultado decisivo: FUN_005AD740

O corpo da função fornece o seguinte contrato **candidato**:

```cpp
// Nome e parâmetros sugeridos; são inferências, não símbolos originais.
void PrepareNormalPlacement(
    uint32_t side,
    uint32_t handIndex,
    uint32_t destinationSlot,
    uint32_t tributeData,
    int32_t mode
);
```

Evidências explícitas do corpo do Ghidra:

- `param_3` é testado como inteiro com sinal, e o cálculo usa
  `param_3 & 0x1F`; nas capturas normais 0 e 1 corresponderam a
  slots físicos de monstros.
- `param_2 & 0xFF` entra nos bits 6..13 do contexto e seleciona
  o índice da carta em `(&DAT_00A56434)[param_2 + (side & 1)*0x351]`.
- `param_1 & 1` escolhe o lado. `param_5==0` seleciona
  `0x8000` (set); caso contrário seleciona `0x4000` (summon).
  Um efeito consultado por `FUN_005699C0(0x47F)` pode
  acrescentar `0x4000` mesmo quando `mode=0`; portanto,
  as flags finais dependem de regras adicionais.
- `param_4` incorpora seletores de tributo/efeitos nos bits altos,
  quando não zero, usando uma codificação ainda não validada.
- Inicializa `DAT_00A5508C=2` e modo 1 em
  `DAT_00A5508E = (stage & 0xFFE7) | 4`, preservando alguns
  bits pré-existentes.
- Copia a identidade da carta de `0x00A56434` para
  `DAT_00A55084` (12 bits baixos), chama `FUN_0059D040(side)`,
  e depois `FUN_005AD8C0()`.
- `FUN_005AD8C0` habilita o estágio
  `stage = (stage & 0xF01D) | 1`, opcionalmente produz comandos
  auxiliares ou prepara comunicação. Junto com o bit de modo
  previamente definido, isso explica `stage=0x0005`
  observado no início de ambos os traces de preparação.

O produtor final `FUN_005ABD00` é acionado em ticks posteriores
por `FUN_005AB820`, **sem precisar de chamada direta** para emitir
`0xCE` nem de reconstrução manual do ciclo de estados.

### Quem chama o construtor?

`FUN_005AD740` possui quatro callsites reportados no Ghidra:

| Callsite | Função dona |
| --- | --- |
| `0x0059E203` | `FUN_0059DF40` |
| `0x0059EAD1` | `FUN_0059DF40` |
| `0x0059FBD4` | `FUN_0059DF40` |
| `0x005B7267` | `FUN_005B6FF0` |

`FUN_0059DF40` é o caminho de interação/controle de ações,
e `FUN_005B6FF0` é consistente com o caminho de IA, mas
essa atribuição de responsabilidades precisa de confirmação
da lógica exata dos callsites.
Os chamadores, não o construtor, são os primeiros candidatos
a conter os gates de legalidade de turno, tributos, espaços
livres, escolha da carta e condições especiais.

### Callers e gates: estudo subsequente

O relatório adicional `summon-builder-callsites.txt` identificou
a montagem dos cinco parâmetros em três ramos de
`FUN_0059DF40` e a seleção de slot/tributos em
`FUN_005B6FF0`. O trecho de controle automatizado inclui
o teste `playerFlags & 0x20000`, mas **não permite garantir
que todas as rotas sejam protegidas pelo mesmo gate**.
Veja [estudo dos callsites](summon-builder-callsites-2026-11-04.md).
Não é seguro habilitar a chamada externa ainda.

### Construtores distintos

`FUN_005ADBF0` também obtém uma carta da mão, mas constrói
o descritor com modo `0x18` e `auxFlags=5`, apontando para
outra família de operação (ainda não classificada).
`FUN_005AD960` usa uma carta do **campo** como origem e
prepara modo `0x0C`, não uma invocação da mão.
`FUN_005ADA20`/`FUN_005ADB10` recebem ponteiro para
registro de carta e usam `FUN_005675C0`, com modos
`0x10`/`0x14`. São construtores de outros caminhos,
não substitutos equivalentes a `FUN_005AD740`.

`FUN_005AD8C0` é o iniciador compartilhado de operação
por essas famílias. Chamá-lo isoladamente sem montar
o descritor correto seria arriscado.

## Próxima prova dinâmica

O probe `trace prep on` agora registra
`FUN_005AD740` em `kind=summon_prep`,
`function=normal_builder`, com cinco `argsRaw`,
`builderIntent` e snapshots de estado antes/depois.
O objetivo é confirmar os argumentos reais em summon, set e
uma invocação por tributo. Não altera as regras.

Para estudar os **gates nos chamadores**, use o script
`QueryArchive` já presente no repositório, sem pedir uma
decompilação integral do controlador maior:

```powershell
.\scripts\Run-ArchiveQuery.ps1 -Report summon-builder-callsites.txt -Addresses @(
  'ins:0059e1c0:60',
  'ins:0059ea80:60',
  'ins:0059fb80:70',
  'ins:005b7230:70',
  '005b6ff0'
)
```

O comando exporta instruções no entorno de cada callsite e
decompila o caminho de IA. Caso a decompilação seja longa,
pode-se reduzir a consulta aos argumentos `ins:...`
e examinar a IA separadamente.

## Limites para invocação pelo console

A rotina nativa **não é por si uma API segura**: não verifica
legalidade de turno, classe da carta, ocupação do slot,
posição corrente na mão, uso da normal summon e possíveis
substituições de tributo. Antes de uma chamada externa:
- usar a função de elegibilidade somente no game thread;
- identificar os gates superiores e suas máscaras de retorno;
- revalidar identidade da carta, slot escolhido e estado do
  turno no momento da execução;
- começar com um experimento controlado em duelo local,
  sem mexer na fila de comandos a partir da thread RPC.

Não habilitamos ainda `summon` ou `set` no console.
