# Callers e gates do construtor de summon/set

**Data:** 2026-11-04. **Base documental:** extração do Ghidra
`summon-builder-callsites.txt`, fornecida pelo operador. EXE SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

A evidência reforça o contrato de `FUN_005AD740(side, handIndex,
destinationSlot, tributeData, mode)`. **O construtor apenas prepara e
inicia a operação; não garante a legalidade da jogada.**

## Três chamadas originadas no controle do jogador

Todos os callsites são de `FUN_0059DF40`, mas pertencem a caminhos
diferentes do controlador. A reconstrução de argumentos usa a ordem
`cdecl` observada (último parâmetro empilhado primeiro), e não
nomes originais.

### 0x0059E203: slot calculado, sem tributo

- `side = (DAT_00A5780A >> 8) & 1`.
- `handIndex = DAT_00A5780C & 0xFF`.
- `destinationSlot = FUN_0056A030(side)` (**confirmado** no
  Ghidra). Os pushes anteriores de `mode` e `tributeData=0`
  permanecem na pilha até a posterior chamada ao construtor.
  Ver [estudo do seletor](summon-slot-selector-2026-11-04.md).
- `tributeData = 0`.
- `mode = valor lido de [ESP+0x248]` antes dos pushes.
- Depois do retorno do construtor, um valor de contexto da pilha é
  testado; dependendo dele pode emitir `FUN_00579930(side,0x11,1)`.
  Esse teste **não** é o retorno do construtor, que é `void`.

### 0x0059EAD1: entrada com dados auxiliares

- `side = (DAT_00A5780A >> 8) & 1`.
- `handIndex = EDX & 0xFF` (origem de EDX fora do recorte).
- `destinationSlot = valor de [ESP+0x248]`, capturado em ECX.
- `tributeData = ESI`.
- `mode = EAX` (ambos calculados fora da janela).
- Um caminho irmão próximo chama `FUN_005ADBF0`, que prepara
  outra família de operações. Precisamos observar os valores reais
  antes de afirmar a condição de escolha.

### 0x0059FBD4: modo de invocação constante

- Guarda anterior visível:
  `CMP word ptr [0x00A558B4],0`, com desvio se zero.
- `side = (DAT_00A5780A >> 8) & 1`.
- `handIndex = DAT_00A5780C & 0xFF`.
- `destinationSlot = FUN_0056A030(side)`. Os `PUSH 1` e
  `PUSH 0` fazem parte da preparação dos argumentos
  da chamada seguinte, não da função de seleção de slot.
- `tributeData = 0`.
- `mode = 1`, ou seja, ramo de summon de `FUN_005AD740`.
- Também existe a emissão subsequente de
  `FUN_00579930(side,0x11,1)` condicionada a estado da pilha.

O controlador utiliza as palavras de ação `DAT_00A5780A` e
`DAT_00A5780C` em vários caminhos. Entretanto, os recortes não
expõem toda a cadeia que define essas palavras ou valida ações.
**Não chamar `FUN_0059DF40` diretamente com argumentos inventados.**

## FUN_005B6FF0: caminho automatizado compatível com a IA

O decompilador expõe `FUN_005B6FF0(side, handIndex, param3)`.
Essa função é chamada nos endereços `0x005B73D2`,
`0x005B7534` e `0x005B766D` e executa:

1. Obtém a carta na mão usando `DAT_00A56434` e classifica
   `FUN_004026C0(resourceId)`.
2. Consulta `FUN_00570A70(side)`; quando retorna zero,
   força `param3=1`, cujo `mode=(param3==0)` posteriormente
   resulta em `mode=0` (set).
3. Para tipos `1..4`, usa `FUN_0056A030` para localizar o
   slot e `tributeData=0`.
4. Para tipos `5..6` e casos restantes, consulta
   `FUN_005AECE0` para selecionar alvos/tributos; pode executar
   `FUN_005AD960` em alternativas dependentes da situação.
5. Se tiver parâmetros suficientes, chama
   `FUN_005AD740(side,handIndex,slot,tributeData,mode)`,
   seguido de `FUN_00579930(side,0x11,1)`.

Esse encadeamento é consistente com o controle de jogadas da IA,
mas a **autorização da normal summon não é comprovada dentro
dessa função**. Algumas validações podem estar nos chamadores.

Um recorte próximo ao caminho automatizado, em `0x005B72E0`,
consulta `playerFlags & 0x20000` e desvia quando o bit está
ligado. Na sequência há chamadas a `FUN_005AE390(side)` e
`FUN_00570A70(side)`, com desvios baseados nos retornos.
É evidência de um gate antes de parte das operações, **não**
prova de que todos os três callsites de `FUN_005B6FF0`
sejam protegidos por esses mesmos testes.

## Relevância para nossa implementação externa

Temos três peças diferentes:

- **Elegibilidade:** `FUN_005AAB90` foi observada antes de
  summon/set nos logs, retornando `0x11` nas duas capturas.
  Ainda não sabemos interpretar todos os bits de retorno.
- **Construtor:** `FUN_005AD740` recebe lado, índice de mão,
  slot, tributo e modo; a implementação lê esses índices sem
  validar todos os limites e estados do turno. O construtor
  é o lugar certo para **iniciar a operação depois de validada**.
- **Status pós-seleção:** `FUN_00579930(side,0x11,1)`
  aparece depois da chamada no jogador e no caminho automatizado;
  seu efeito exato ainda precisa ser confirmado.

Não podemos ainda expor comandos `summon`/`set` seguros
apenas porque `FUN_005AD740` foi localizada: isso poderia
burlar o limite de uma invocação por turno ou ler um índice
inválido da mão. A primeira versão deve limitar-se a
monstros sem tributos, escolha automática de slot desocupado,
validação de identidade da carta na thread do duelo e estado
sem outra operação ativa. A autorização do turno precisa ser
confirmada antes de qualquer execução experimental.

## Experimento com Ground Collapse: slot 2 e 3

Com duas zonas de monstro indisponíveis devido a Ground Collapse,
o caller do jogador `0x0059E208` foi observado invocando
`FUN_005AD740(1,5,2,0,1)` para Ancient Brain e
`FUN_005AD740(1,4,3,0,0)` para baixar Wall of Illusion.
O `0xCE` reproduziu o contexto/slot dos dois casos.
Essa evidência indica que a escolha de zona ocorre **antes**
do construtor; não basta procurar o primeiro slot vazio
em um executor externo. Ver
[`ground-collapse-summon-set-slots-2026-11-04.md`](ground-collapse-summon-set-slots-2026-11-04.md).
O candidato prioritário para estudar a escolha é
`FUN_0056A030` no callsite `0x0059E203`.

## Experimento recomendado

O probe **já implementado** `trace prep on` registra
`normal_builder` com os cinco argumentos e return address.
Assim não precisamos instalar novo hook para o próximo teste:

```text
trace on
trace prep on
trace mark antes summon
# Invocar uma carta pela interface do Joey
trace mark depois summon
trace off
trace dump
```

Repetir em outra captura com um set. Conferir:
`builderIntent`, `caller`, `state` antes/depois e o
`0xCE` subsequente. Os endereços de retorno esperados
para as rotas de jogador são `0x0059E208`,
`0x0059EAD6` ou `0x0059FBD9`; para a rotina
automatizada, `0x005B726C`. Isso distinguirá empiricamente
os ramos do controlador para jogar a carta.

**Próxima análise estática:** examinar `FUN_00579930`
e a origem dos valores `0x00A5780A/0C`, sobretudo as
comparações de elegibilidade anteriores a
`0x0059E203`, `0x0059EAD1` e `0x0059FBD4`.
Não decompilar indiscriminadamente `FUN_0059DF40`
inteira antes de localizar o bloco de validação.
