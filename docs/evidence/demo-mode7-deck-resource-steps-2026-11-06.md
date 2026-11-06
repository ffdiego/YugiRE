# Demo modo 7: dois carregamentos de deck, uma cópia e sorteios

Evidência estática: uploads `demo-menu-mode-writer.txt` e
`demo-source-deck-writers.txt`, hash do executável
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Sequência observada em FUN_0044BEE0: case 7

`FUN_0044BEE0` grava o parâmetro de modo em
`DAT_005F3294` no endereço `0x0044BF0C`.
A ramificação `case 7`:

1. Chama `FUN_0044BBE0` (ainda sem contrato definido).
2. Chama **`FUN_0044A920(1)`** em
   `0x0044BF75` (return address `0x0044BF7A`).
   Se retorno diferente de zero, sai antecipadamente.
3. No caminho de sucesso, cópia de **`0xE2` bytes**:
   `REP MOVSD` com `ECX=0x38` copia 224 bytes,
   e `MOVSW` copia mais 2 bytes:
   origem `0x00A54F42` → destino `0x00A54E60`.
4. Chama **`FUN_0044A920(1)`** de novo
   em `0x0044BFD8`
   (return address `0x0044BFDD`).
   Retorno diferente de zero também pode
   abortar a continuação.
5. No caminho de sucesso, chama
   `FUN_005BDDB0(2)` duas vezes,
   preenchendo `DAT_005F3288` e
   `DAT_005F328C` com valores do
   intervalo esperado `0..1`
   (limites/rng do helper devem ser
   mantidos como contrato sob investigação).
   Chama `FUN_005C40B7` e grava
   `DAT_005F3290`.
6. Continua a transição de cena via
   `FUN_00477EA0`.

**Hipótese de trabalho:** a primeira chamada
carrega/seleciona uma lista persistente no
buffer 1; ela é copiada para o buffer 0;
a segunda chamada substitui o buffer 1
por outro deck, potencialmente adversário.
Esse padrão é compatível com as capturas
reais anteriores (deck de 43 cartas
recorrente no buffer 0 e decks de 40/41
no buffer 1), mas **a semântica de
`FUN_0044A920` ainda não foi decompilada
nem validada na transição real**.
Não afirmar que o deck persistente é
do usuário ou que o adversário é aleatório.

## Outras rotas que escrevem decks

`FUN_0044AD50` chama duas vezes
`FUN_00449EF0` com destinos
`0x00A54E60 + (DAT_005F3288 != 0)*0xE2`
e `0x00A54E60 + (DAT_005F3288 != 1)*0xE2`.
Ela parece desserializar listas de um
fluxo de entrada, mas a presente extração
não confirma que essas chamadas participem
do `case 7`; os callers diretos são
`FUN_0044AFF0` e `FUN_0044B100`.
Não confundir os fluxos de carregamento.

As referências da região ainda mostram
`FUN_0044A550`, `FUN_0044A690`,
`FUN_005BD2D0` e outras funções
como possíveis escritores. A função
`FUN_00569380`, comprovadamente,
**apenas transfere** as listas de origem
para as estruturas de duelo.

## Como localizar o disparo da demonstração

`FUN_0044BEE0` tem chamadas diretas
em `0x004764EE`, `0x0048E389`,
`0x0042155B`, `0x0048A2E1`,
`0x0048A3C0`, `0x0048A550`,
`0x0048AD6C`, `0x0048AE28`,
`0x0048B1FC`.

Ainda não sabemos **qual caller passa
o valor 7 depois do timeout do menu**.
O hook passivo `scene_config` agora
registra `requestedSceneMode` e
`caller` para identificar o endereço
real, sem especular a partir da lista.

## Novo experimento pronto (ainda não validado no jogo real)

`trace demo on` observa agora
`FUN_0044A920` com
`kind=demo_init` e
`function=deck_resource_step`.
Registra o endereço de retorno
(`0x0044BF7A`/ `0x0044BFDD`
esperados para `case 7`),
`argument0Raw` e snapshots
dos dois `sourceDecks` **na
entrada e saída** de cada chamada.

Também registra snapshots antes/depois
de `scene_config` e valor bruto de
`requestedSceneMode`.

Teste simulado confirmou:
oito hooks opt-in; primeira chamada
pode substituir o conteúdo do buffer 1;
o `0xE2` copia para o buffer 0;
segunda chamada pode alterar novamente
o buffer 1; `trace off` detach todos.
Isso **valida a instrumentação com dados
sintéticos, não o comportamento do executável**.

Para capturar, iniciar no menu antes do
timeout com `--observe-only`:

```text
trace on 20000
trace demo on
trace mark esperando-demo
```

Deixar uma demo iniciar; depois
`trace off` e `trace dump`.
Para os decks o `trace ai on` não é
necessário: ativá-lo apenas se for
preciso comparar decisões da IA.

## Próxima extração estática

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report demo-two-deck-loads.txt `
  -Addresses @(
    '0044a920',
    '0044bbe0',
    'ref:0044a920',
    'ins:0044bf5f:90',
    '00449ef0'
  )
```

Prioridade seguinte: usando o `caller`
real da demo, extrair a função
que invoca `FUN_0044BEE0(7,...)` e
o contador de inatividade do menu.

Não modificar buffers de cartas
nem iniciar a cena por chamada isolada
antes de observar as pré-condições.
