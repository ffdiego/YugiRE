# Seletor de slots e flags pós-invocação (Ghidra, 2026-11-04)

Fonte fornecida pelo operador: `summon-slot-selector.txt`, SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## 1. FUN_0056A030: escolher uma zona aceita pelo motor

Decompilação: `uint FUN_0056A030(uint side)`.

A função percorre os cinco slots `0..4` e usa **sempre**
`FUN_00569F70(side, slot)` como predicado:

- Quando `DAT_00A577EC & 1` está ativo, faz primeiro uma
  varredura que evita determinadas zonas referenciadas por
  `DAT_00A57840`, `DAT_00A57864` e `DAT_00A578F4`.
  Só esse ramo adicional diferencia os dois lados e checa
  identidades nos registros de referência.
- Caso não encontre um slot nesse primeiro percurso, faz a
  varredura básica de `0` a `4`, retornando o primeiro
  `slot` cujo predicado tenha retorno diferente de zero.
- Se nenhum slot for aceito, retorna `0xFFFFFFFF`
  (equivalente a `-1` quando interpretado como inteiro).

O corpo **não verifica diretamente** o efeito Ground Collapse;
o predicado `FUN_00569F70` delega a `FUN_00569E10`,
cuja análise posterior confirmou a comparação explícita
com Ground Collapse (resource ID `0x528`). Ver
[`ground-collapse-zone-predicate-2026-11-04.md`](ground-collapse-zone-predicate-2026-11-04.md).

A captura anterior de Ground Collapse mostrou o mesmo caller
`0x0059E203` entregando slot 2 e depois slot 3 ao construtor.
Essa sequência é coerente com o primeiro slot aceito e com a
ocupação posterior do slot 2, mas, sem instrumentar o predicado,
não demonstra *isoladamente* por que 0 e 1 foram rejeitados.

### Correção da interpretação dos argumentos no callsite do jogador

A extração `ins:0059e1c8:55` mostra:

```asm
PUSH mode
PUSH 0              ; tributeData futuro
PUSH side
CALL 0x0056A030      ; lê side (um argumento)
ADD ESP,0x4          ; remove só side
PUSH EAX             ; destinationSlot recebido
PUSH handIndex
PUSH side
CALL 0x005AD740
ADD ESP,0x14
```

**Os dois primeiros `PUSH` não são parâmetros de
`FUN_0056A030`**. Permanecem na pilha como parâmetros
da chamada seguinte `FUN_005AD740`. Isso invalida
a leitura anterior de que `FUN_0056A030` recebe 3
argumentos em `0x0059E1E0`; o contrato Ghidra para
`FUN_0056A030` é **um argumento (`side`)**.

No callsite, o descritor recebe
`FUN_005AD740(side, handIndex, selectedSlot, 0, mode)`.
A preparação das flags e stage continua dentro do construtor.

## 2. FUN_00579930: flag por jogador + notificação 0x0D

Contrato observado:

```cpp
void SetPlayerFlag(uint32_t side, uint32_t bit, uint32_t value) {
    if (((flags[side] >> (bit & 31)) & 1) != value) {
        FUN_005706B0(side, bit, value);
        FUN_005B9170((side ? 0x8000 : 0) | 0x0D,
                     bit, value, 0);
    }
}
```

O endereço da palavra de flags é
`0x00A56430 + (side & 1) * 0xD44`.
`FUN_005706B0` é o caminho para mudar o estado;
`0x005B9170` emite a mudança `0x0D` caso haja diferença.
O callsite posterior a `FUN_005AD740` passa
`(side, 0x11, 1)`, isto é, **bit 17 = `0x20000`**,
associado em outras análises ao uso da invocação normal
no turno. O produtor não é só um evento da interface.

Os traces anteriores registraram emissões `0x0D`
com `arg1=0x11` antes do primeiro despacho da invocação.
Há ainda calls a `FUN_00579930` no inicializador
`FUN_005AD8C0` e em outras rotinas, portanto não
atribuir toda emissão exclusivamente à linha posterior
do controlador.

### Condicional importante no caller

Em `0x0059E208`, depois do `void FUN_005AD740`,
o código lê uma **variável da pilha** em
`[ESP+0x264]` (com a pilha ainda contendo os argumentos),
`ADD ESP,0x14` e testa esse valor para decidir
se chama `FUN_00579930(side,0x11,1)`.
**Não é valor de retorno do construtor**. A condição
e o significado dessa variável ainda devem ser mapeados.

## 3. Estado da pesquisa

O caminho básico mais próximo do original é:

```text
UI/IA valida ação e modo
  → FUN_0056A030(side)
    → FUN_00569F70(side,slot) para cada candidato
  → FUN_005AD740(side,handIndex,slot,tributeData,mode)
    → FUN_0059D040(side)
    → FUN_005AD8C0()
  → despachante FUN_005AB820 e colocação FUN_005ABD00
  → atualização/sincronização de flags FUN_00579930
```

A posição exata da última atualização varia conforme o caller
e o estado em jogo, portanto a última seta é *conceitual*,
não uma ordem universal de execução.

**Não chamar ainda o construtor externamente**. Permanecem
pendentes autorização de turno/efeitos, confirmação do predicado
de zona, parâmetros de tributos e rechecagens na thread do duelo.

## 4. Resultado da investigação da legalidade de slots

`FUN_00569F70(side,slot)` só aceita a zona quando o
low12 do registro do slot é zero (vazia) **e**
`FUN_00569E10(side,slot)` retorna valor não zero.
`FUN_0056A000(side)` conta quantas zonas satisfazem o
mesmo predicado; `FUN_005706B0` altera bits de flags
diretamente em memória. A análise posterior demonstrou que a restrição de Ground
Collapse está explicitamente codificada em `FUN_00569E10`,
junto de outro mecanismo de bloqueio por entradas `'@'`.
Ver [relatório da regra](ground-collapse-zone-predicate-2026-11-04.md). Ver
[`summon-slot-legality-2026-11-04.md`](summon-slot-legality-2026-11-04.md).

## 4. Consulta original no Ghidra (concluída)

```powershell
.\scripts\Run-ArchiveQuery.ps1 -Report summon-slot-legality.txt -Addresses @(
  '00569f70',
  '005706b0',
  '0056a000',
  'ins:0059e0a0:60',
  'ins:0059e1b0:45'
)
```

Queremos identificar o que `FUN_00569F70` testa
(ocupação, bloqueios contínuos, máscaras de efeito)
e obter o contrato de atualização da flag por
`FUN_005706B0`, antes de qualquer executor ativo.
`FUN_0056A000` **conta as zonas aceitas** por
`FUN_00569F70`; isso está agora confirmado na
decompilação subsequente.

Essa consulta é estática e pode ser executada no projeto
Ghidra existente do Windows; não é necessário rodar
novamente o jogo.
