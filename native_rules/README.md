# YugiRE Native Rules

Implementação híbrida das regras de **Yu-Gi-Oh! Power of Chaos: Joey the Passion**.
Este diretório contém **todo o código necessário** para os experimentos ativos
com callbacks em DLL C++ e o console interativo de duelo, além dos testes.
Não depende da pasta antiga `experiments/hybrid_probe`.

## Organização

```text
native_rules/
  CMakeLists.txt                    # build Windows Win32 / x86
  include/yugire/                   # CardRequest, EffectContext, API do motor
  src/engine.cpp                    # funções nativas de dano e compra do Joey
  src/effect_context.cpp            # composição de efeitos C++
  src/cards/{sparks,raimei}.cpp     # callbacks individuais
  src/exports.cpp                   # ABI pública da DLL (versão 6)
  yugire_rules.def                  # exports sem decoração
  patch_dual_rules.py               # host Python e console
  pe_preflight.py                   # validação PE + SHA-256 do EXE
  yugire_dual_loader.js             # agente Frida que roda dentro do Joey
  tests/                             # regressões Python / JS / C++
```

**O papel de cada parte:**
1. **Python** conecta à instância do Joey por Frida, valida o fingerprint
   do executável, mantém a sessão ativa e implementa o prompt `draw 5`.
2. **JavaScript/Frida**, já dentro do Joey, usa `LoadLibraryW` para carregar
   `yugire_rules.dll`, localiza os exports e substitui os dois ponteiros
   de callback individuais. O comando do console é apenas *solicitado*
   por RPC: um hook no despachante do duelo agenda a compra na thread do jogo,
   não diretamente na thread RPC.
3. **DLL C++** executa os efeitos e chama produtores de comandos do Joey:
   `0x005784E0` para dano e `0x00578B60` para compra. O motor original
   continua responsável por processar a fila, o deck, a mão e as animações.

O código do executável em disco e os outros callbacks não são modificados.
A ABI atual é v6; apenas os snapshots de leitura foram ampliados.

## Compilar

Use **Developer PowerShell for Visual Studio 2026** na raiz do repositório:

```powershell
cd Z:\YugiRE\yugire
git pull
cmake -S native_rules -B native_rules\build-vs2026 -G "Visual Studio 18 2026" -A Win32
cmake --build native_rules\build-vs2026 --config Release
ctest --test-dir native_rules\build-vs2026 -C Release --output-on-failure
```

A DLL será gerada em
`native_rules\build-vs2026\Release\yugire_rules.dll`.
Essa DLL deve ser **x86**, não x64.

### Python/Frida

O código Python precisa apenas do pacote `frida` instalado em um ambiente
Python. Se você já tem um virtualenv funcional na pasta antiga, ele pode ser
usado temporariamente apontando para o novo `patch_dual_rules.py`.
Para deixar tudo autônomo e poder remover a pasta antiga por completo:

```powershell
cd Z:\YugiRE\yugire\native_rules
py -3 -m venv .venv-frida
.\.venv-frida\Scripts\python.exe -m pip install frida
```

A pasta `.venv-frida` e os binários de compilação não são versionados.

## Executar em um duelo normal

Reinicie o Joey antes de testar uma DLL recém-compilada e aguarde a
inicialização de um duelo. No diretório `native_rules`, basta:

```powershell
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py
```

Ou `python .\patch_dual_rules.py` se o Python ativo já tiver Frida.
O script localiza automaticamente **um único processo `joey_pc.exe`**.
Se Joey não estiver aberto, retorna uma mensagem clara. Se houver mais
de uma instância, não escolhe aleatoriamente: lista os PIDs e solicita
`--pid <número>` para desambiguar.

Sem parâmetros, os efeitos padrão são **Sparks = comprar 5**, sem dano,
e **Raimei = 400 LP de dano + comprar 5**. O lado padrão de `draw 5`
é 1 (jogador, no duelo CPU testado). As opções anteriores continuam
disponíveis para experimentos futuros: `--sparks`, `--raimei`,
`--sparks-mode`, `--raimei-mode`, `--console-side`, `--pid`
e `--dll`. Os danos permitidos permanecem de 1 a 9999 LP.

| Carta | Exemplo de efeito |
|---|---|
| Sparks (`resource_id=342`, linha 66) | comprar 5, sem dano |
| Raimei (`resource_id=1007`, linha 172) | dano de 400 LP e comprar 5 |

As duas alterações de efeito já foram validadas pelo operador em um duelo
normal, assim como o console de compras.

### Console interativo

```text
[dual-rules] > draw 5
[console] Request 1 pending for side 1.
[dual-rules] > status
[dual-rules] > draw 5 0
[dual-rules] > cancel
[dual-rules] > stop
```

- `draw 5` envia uma compra de cinco cartas ao **jogador humano**, lado 1,
  no duelo local testado.
- `draw 5 0` compra para a **CPU**, lado 0.
- `status` ou Enter consulta os contadores, último pedido e fila.
- `cancel` cancela um pedido que ainda não entrou na fila.
- `stop` restaura os ponteiros de callbacks e remove o hook.

O carregador intencionalmente mantém **no máximo um pedido externo pendente**;
a mensagem `A console draw request is already pending` é esperada se o
comando anterior ainda não foi aceito. `draw 6` e `draw 30` são
proibidos nesta versão. O comando manual recusa a compra se o lado escolhido
tiver menos de cinco cartas no deck.

O mapeamento 0=CPU / 1=humano foi confirmado em duelo normal contra CPU.
Não assumir que ele é idêntico em outros modos sem observação.

## Duel Lab: dano externo e inspeção de estado (ABI v6)

A partir desta versão, o console do Native Rules tem **dois novos comandos**
que ajudam a investigar o motor original sem depender de cartas específicas:

```text
[dual-rules] > state
[state] Side 0 (CPU): LP=8000 hand=4 deck=27 grave=1 banished=0 ...
[state] Side 1 (Player): LP=7600 hand=6 deck=22 grave=2 banished=1 ...

[dual-rules] > damage 1200 0
[console] Damage request 1 pending: 1200 LP to side 0.

[dual-rules] > diff
[diff] side 0 lifePoints: 8000 -> 6800
```

`state` chama a nova exportação nativa de leitura
`YugiRE_ReadDuelState` (DLL C++). Os dados já estavam documentados
em [JoeyTrace](../docs/30-joeytrace.md), mas agora ficam disponíveis
diretamente no nosso console:

- LP, mão, deck, cemitério e cartas banidas de ambos os lados;
- bits de condição terminal (deck-out 0x100, Exodia 0x200,
  Destiny Board 0x400);
- ocupação dos cinco slots de monstro, por **índice interno de carta**,
  não por resource ID;
- observação dos seis slots físicos adicionais (5..10), a palavra de
  posição/face (`+6`) e índices internos das cartas da mão (até 80 por lado).

`diff` compara o estado atual com o último `state` ou `diff`
capturado, atualizando a referência. Use `state` antes de um efeito
e `diff` depois para verificar exatamente o que mudou. **A leitura
não é atômica:** se o jogo estiver movendo cartas ao mesmo tempo,
alguns campos podem representar momentos ligeiramente diferentes.

O comando `damage <quantidade> <lado>` solicita dano de **1 a 9999 LP**
ao alvo explicitamente indicado. No duelo local testado, `0 = CPU` e
`1 = jogador`. Exemplo: `damage 1200 0` tenta causar 1200 LP
à CPU. O alvo é sempre obrigatório para evitar atingir o lado errado.

Assim como `draw 5`, o dano é **agendado pelo hook de
`0x005BC700` e executado na thread do duelo**, usando a função nativa
`0x005784E0`. O console permite somente **um comando de alteração
pendente** (`draw` ou `damage`) por vez; `cancel` descarta o
pedido que ainda não entrou na fila original. `state` e `diff`
são apenas leitura e não ocupam esse slot de comando.

```text
state
damage 200 0
diff
draw 5 1
diff
```

**O console de compra já foi testado em duelo real; os comandos
`state`, `diff`, `damage` e `destroy` ainda exigem validação específica
dentro do Joey.** Primeiro verifique `state` com o duelo parado,
depois use `damage 100 0` e confira os LP pelo jogo e por `diff`.
Não extrapole para multiplayer ou replay.

O parser de `DuelSnapshot` fica em `src/duel_state.cpp`, testável
independentemente, e os exports agora exigem ABI **v6**. Para evitar
reutilizar a DLL antiga, feche o Joey antes de recompilar e reinicie
o processo após a build.

## Experimento: destruir monstro em slot (iniciado na ABI v5)

O comando `destroy <lado> <slot>` tenta destruir um monstro usando a
rotina nativa de **destruição por efeito** `FUN_00576960`, que produz
comandos `0x79`. O lado é obrigatório: `0` = CPU, `1` = humano
(no duelo local validado). O slot é **zero-based, de 0 a 4**, exclusivamente
na zona de monstros; mágicas/armadilhas e outros campos ficam para outra etapa.

```text
state
destroy 0 2
status
diff
```

Antes de agendar, o adaptador recusa slots vazios e armazena índice interno,
dono e chave de ocorrência. No tick o adaptador e a DLL verificam novamente
a identidade, pois o slot pode ter sido reutilizado. A operação só acontece
na thread do duelo e compartilha o único pedido pendente de `draw`/`damage`.
`status` informa `submitted` quando o helper foi *invocado*, não quando
a destruição realmente ocorreu. Consulte `state` e `diff` após a resolução.

O operador relatou destruição bem-sucedida em teste local e rejeição
correta de alvo vazio. Ainda faltam capturas isoladas de causa/efeito.

**Limitação experimental importante:** a DLL cria um pedido temporário
de 20 bytes com índice interno 753 (Man-Eater Bug) para chamar
`FUN_00576960(pedido, máscara, 14, 2)`. O caminho é fundamentado nos
estudos estáticos em [destruição](../docs/cards/monster-destruction.md)
e [seleção](../docs/cards/man-eater-bug-targeting.md), mas **não foi validado
em execução real**. Filtros nativos podem rejeitar o alvo e interações de
cartas, vínculos e animações ainda precisam ser verificadas. Não usar em
duelo remoto/replay. Reinicie Joey antes de testar a nova DLL/ABI.

## Inspeção da mão, zonas adicionais e watch (ABI v6)

```text
state
hand
hand 0
watch on 250
destroy 1 0
watch off
diff
```

`state` apresenta uma linha `[field]` por lado, contendo os slots físicos
5..9 e 10 e a palavra de posição/face em hexadecimal para cada carta.
A interpretação **5..9=mágicas/armadilhas e 10=Field Zone é provisória**:
o arranjo de onze registros de campo é documentado, mas a correspondência
com os slots da interface ainda não foi testada diretamente. Zero é vazio.
Índices mostrados não são resource IDs.

`hand` lê os índices internos da mão do jogador 1; `hand 0` mostra os
da CPU, inclusive cartas ocultas. Não publique esses dados inadvertidamente.
`diff` agora informa mudanças de slot físico, posição/face e mão.

`watch on [ms]` inicia uma thread Python de leitura, com intervalo de
100 a 5000 ms (padrão 250). `watch off` interrompe e `watch status`
consulta o estado. O observador não gera comandos, não pausa o jogo e
pode perder mudanças rápidas: o snapshot não é atômico e não registra
cada etapa da fila. É encerrado antes de restaurar os hooks.

`summon`, `set` e `discard` ainda não são comandos externos: os
produtores nativos encontrados dependem de contexto de turno, seleção,
destino e rotinas auxiliares. Precisam de validação antes de invocação
pela DLL. A visualização de mão/campo desta etapa prepara esses testes.

**Após a build da ABI v6, reinicie Joey para carregar a nova DLL.**

## Experimento de IA nativa no lado do jogador (opt-in)

O objetivo é aproveitar a IA original do Joey para
jogar no **lado 1**, em vez de reimplementar toda a
escolha de cartas fora do processo. Os primeiros
comandos são deliberadamente limitados:

```text
state
ai check 1
# Pressione Enter para ver console.last
ai step 1
# Pressione Enter; após a resolução:
diff
state
```

`ai check 1` reproduz os dois passes de seleção
do callsite `0x005B73D2`: chama primeiro
`FUN_005B1360(1,0)` e, somente se retornar
`-1`, tenta `FUN_005B1360(1,-1)`.
**Ambos são executados na thread do duelo**, após
fila/posicionamento ociosos, durante o turno do
jogador em fase principal e com invocação normal
ainda disponível.
O seletor utiliza snapshots do campo e pode
alterar globais temporários: não é uma função
puramente read-only, embora o comando não envie
uma colocação ao motor.

O índice retornado é verificado novamente pelas
regras de colocação. No **caminho específico
`FUN_005B1360`**, o chamador original passa
`modeSelector=0` (normal summon) e o executor
pode forçar set quando `FUN_00570A70(side)==0`.
`FUN_005AE270` pertence a **outros caminhos**
que selecionam via `FUN_005B16C0`; não
misturamos mais as duas políticas.

O console agora mostra `normalPass0` e
`tributeFallbackPassMinus1` (nulo quando
não houve fallback), além de `selectedKind`,
`allowed`, `levelClass` e motivos de
recusa. Se ambos os passes retornarem índice
negativo, mostra `handCandidates`, uma
lista de classificação das cartas da mão
**sem afirmar que sejam legalmente jogáveis**.
Um `-1` não prova que a mão não contém
monstros aptos a invocar.

`ai step 1` usa a mesma lógica mas **submete
no máximo uma invocação/colocação** (monstro
comum classe 1..4, sem tributos). Mesmo quando
o fallback encontra um monstro de tributo,
o preflight o rejeita por enquanto. Utiliza
`FUN_005AD740` e a flag `0x20000`.
Nenhuma mudança da DLL C++ é necessária.
O primeiro teste no Windows deve ser
`ai check 1`, em duelo descartável; só
tente `ai step 1` se o estado permanecer
coerente e a seleção parecer plausível.

**Ainda não é autoplay completo:** não
joga magias/armadilhas, não realiza batalhas,
não passa fase/turno, não troca decks e
não reinicia duelos. Essas etapas precisam
respeitar o fluxo original e serão introduzidas
depois de validar a decisão no lado 1.

**Captura confirmada (2026-11-05 UTC):** em um
`ai check 1` após a invocação normal já ter
sido usada, o novo trace direto gravou
**2 eventos `ai_console`** (início e recusa)
e `overwritten=0`. A recusa ocorreu antes
de `FUN_005B1360`, pela flag `0x20000`,
portanto não há `select_enter/leave`
nem dados de pontuação. Essa captura
valida o trace **direto**, mas não prova
que os hooks passivos funcionem.
Ver [captura e próximo teste](../docs/evidence/ai-console-gate-capture-2026-11-05.md).

### Observar quais critérios a IA utilizou

Em um duelo descartável, execute:

```text
trace on 12000
trace ai on
ai check 1
# Aguarde a resposta e consulte o console.last com Enter
trace status
trace off
trace dump
```

`trace ai on` instala hooks passivos em
`FUN_005B1360`, `FUN_005B16C0`,
`FUN_005B0F40` e `FUN_0056DD30`.
**Validação dinâmica 2026-11-04:** o usuário confirmou
que `ai step 1` conseguiu invocar Mystic Tomato
no lado 1. O `ai check 1` anterior retornou
`normalPass0=1`, `handIndex=1`, carta interna
221, `allowed=True` e `selectedKind=summon`.
Entretanto, com os hooks ativos, `trace status`
continuou `seen=0` mesmo depois da decisão.
A observação confirma a execução do seletor, mas
**não valida os hooks aninhados**.

Para não depender deles, o código agora escreve
`kind=ai_console` **diretamente no buffer de
trace**, sem novas chamadas nativas:
`start`, `select_enter` e `select_leave`
para cada passe, e `decision` com
`allowed`, `handIndex`, `internalIndex`,
`selectedKind`, `selectedSlot` e
`state` (`checked` ou `submitted`).
Essas linhas trazem `origin=yugire_console_native_call`,
`requestId`, `threadId` e são geradas
**somente** quando `trace on` e `trace ai on`
estão ativos. `state=submitted` não prova
sozinho que o campo foi alterado: verifique
`diff`/`state`.

Os hooks `kind=ai_probe` continuam instalados
para observar chamadas vindas do próprio jogo.
Dependendo do contexto do Frida, as chamadas
nativas iniciadas pelo console podem não
produzir essas notificações. **Não confundir
`ai_console` com `ai_probe`**: os primeiros
confirmam os valores retornados pelo console,
mas não trazem buffers de combate/stats de
quatro/doze bytes. Estes continuam a depender
da observação passiva funcionando.
Os dois seletores registram entrada e retorno,
e os demais registram a saída hexadecimal
de 4 bytes do evento e de 12 bytes de stats,
**apenas enquanto houver um seletor ativo
na mesma thread**, reduzindo eventos
não relacionados do renderizador.

O arquivo em `scratch/traces/commands-*.jsonl`
inclui eventos `kind=ai_probe`, função,
`callId`, `side`, `threadId`, `outputHex`
ou `readError`, e
`selectedHandIndex` no retorno do seletor.
Para decodificar cada `outputHex`, use
`scripts/decode_ai_combat_event.py` e
`scripts/decode_ai_combat_stats.py`. Esses
dados podem representar **estados de campo
hipotéticos**, não jogadas resolvidas.

`trace ai off` interrompe apenas os hooks de IA.
`trace off` também interrompe todos os
observadores auxiliares; `trace dump`
exporta eventos preservados em JSONL.
A capacidade é limitada e o hook tem custo
de execução, então faça testes curtos.
A análise **não está validada dinamicamente**
no Joey após a instalação desses hooks.

## Observação passiva da demonstração do menu

A captura real de 2026-11-05
[`demo-native-ai-both-sides`](../docs/evidence/demo-native-ai-both-sides-2026-11-05.md)
mostrou seletores de IA e execução de colocações
**para ambos os lados**, além de transições automáticas
de turno. O teste **apenas observa**, sem chamar
funções da IA, alterar flags ou enfileirar ações.
Isso não revela ainda qual flag/dispatcher ativa
o controle em um duelo normal.

`trace demo on` instala um hook em
`FUN_005B6FF0(side,handIndex,modeSelector)`,
o executor de *summon/set*, não um controlador de
todos os tipos de ação. Registra `kind=demo_executor`,
`phase=enter|leave`, `callId`, `threadId`,
`caller`, `side`, `handIndex`, `modeSelector`
e, se existir duelo ativo e índice válido,
`handInternalIndex`. O `returnRaw` é opaco:
não prova que o summon/set foi resolvido.

Com o jogo **ainda no menu principal**, inicie o
console Python em outro terminal com **modo somente
observação**, sem carregar DLL nem alterar callbacks:

```powershell
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --observe-only
```

Nesse modo, o EXE é validado como antes,
mas `LoadLibrary` não é chamado,
Sparks/Raimei permanecem originais e
nenhuma ação manual `draw/damage/destroy/summon/set`
ou `ai check/step` é permitida.
Somente os hooks de observação
`trace on`/`trace ai on`/`trace demo on`
são instalados, quando solicitados.

Depois execute:

```text
trace on 20000
trace ai on
trace demo on
trace mark esperando-demo
```

Deixe o menu inativo até iniciar a demonstração,
observe algumas jogadas, sem executar
`ai check`/`ai step`, e encerre:

```text
trace status
trace off
trace dump
```

O JSONL fica em `scratch/traces/commands-*.jsonl`.
O console resume as entradas do executor por
`side0/enter` e `side1/enter`. O `trace ai on`
já observa os seletores `FUN_005B1360` e
`FUN_005B16C0`; `trace on` observa o
produtor de comandos `FUN_005B9170`.
Se `overwritten > 0`, repita com uma
captura menor. O limite do buffer é 20.000.

**Interpretação após captura real:** os hooks
passivos registraram chamadas de **seletores**
e de **executor** para os dois lados, com
correlação de callsites. Isso confirma
uso efetivo de decisões nativas de IA no
modo demonstração, mas **não prova que
todo o escalonador seja independente de
scripts ou replay**, nem identifica o
modo de ativação no duelo normal.
Se não houver eventos, tampouco prova que
é replay: outro executor pode ser usado.
O modo `trace demo` só coleta chamadas
nativas; não altera o escalonador do jogo.
Os hooks são opt-in e são removidos
automaticamente por `trace off`.

## Native Command Trace (experimental, passivo)

Esta instrumentação usa Frida `Interceptor.attach` em
`FUN_005B9170` (VA original `0x005B9170`), o produtor comum de comandos.
**Não substitui argumentos, retorno ou código de regras**, mas instalar o
hook modifica temporariamente o prólogo em memória, como qualquer Interceptor.
Valide exclusivamente o executável com fingerprint conhecido e em duelo local.

```text
trace on
trace mark invocacao normal
trace mark monstro baixado
trace mark ativacao de armadilha
trace status
trace off
trace dump
```

- `trace on [capacidade]`: inicia uma nova sessão, com até 8192 eventos por
  padrão; mínimo 16, máximo 20000. Uma nova sessão **descarta o buffer anterior**
  (use `trace dump` antes). O hook lê quatro argumentos de 32 bits, endereço
  de retorno/chamador, ID da thread, timestamp em milissegundos e sequência.
- Para o produtor `0xCE` especificamente **a partir de `0x005ABEAB`**,
  `trace dump` também informa `ceContextLow17Hex` e
  `ceResourceIdLow16`. A análise das duas capturas de 2026-11-04
  mostrou contexto `0x04001` (summon de Mask of Darkness, ID 101) e
  `0x08101` (set de Wall of Illusion, ID 1049). São campos observados
  neste caminho, **não** uma ABI universal nem uma prova de legalidade.
  O produtor é chamado com apenas dois argumentos nesse trecho;
  `args[2]` e `args[3]` do trace podem ser leituras residuais da pilha.
- Nas capturas seguintes, os bits `context >> 6 & 0xFF` seguiram
  a posição aparente da carta na mão (0, 4 para summon; 4, 1 para set),
  enquanto `context >> 1 & 0x1F` permaneceu 0 (zona de monstro 0).
  Em novos JSONL, o exportador registra `ceHandIndexCandidate` e
  `ceFieldSlotCandidate`, mais flags brutas `ceFlag4000`/
  `ceFlag8000`, `ceSourceIndexCandidate` e `ceStageGroup`.
  Esses campos são **candidatos para validação**, não controles de invocação.
  A emissão `0xCF` no callsite `0x0057977F` também ganha
  `cfResourceIdCandidate` (baixo 16 bits de arg1): o primeiro teste
  registrou ID 1547, Fusion Gate. Veja
  [comparação de quatro ações](../docs/evidence/windows-native-ancient-brain-fusion-gate-2026-11-04.md).
- Para `0xCE` no mesmo callsite, agora o trace também registra, no evento
  `ceNativeState`, os valores **brutos e completos** dos globais
  `0x00A55080` (`context`), `0x00A55084` (`source`) e
  `0x00A5508E` (`stage`). Isso é apenas leitura no instante em que
  `0xCE` entra no produtor, e pode ser comparado com o contexto truncado
  `ceContextLow17Hex` durante `trace dump`. Esses globais ainda não
  são uma API segura de invocação.
- `trace mark <texto>` acrescenta uma anotação temporal emitida pela
  **thread RPC**, não pelo game thread. A hora da anotação é aproximada;
  **não prova** que um comando já terminou.
- `trace status` mostra vistos, retidos e sobrescritos. Quando a capacidade
  acaba, o buffer circular substitui os eventos mais antigos em O(1).
- `trace off` remove o hook e preserva os eventos. `trace dump` interrompe
  a captura, exporta em lotes de até 256 por RPC e grava JSONL em
  `scratch/traces/commands-<UTC>-pid<PID>.jsonl`; esse diretório já é
  ignorado pelo Git. O primeiro registro traz metadados/contadores. Cada
  evento mantém os quatro argumentos brutos e recebe formatos hexadecimais
  e opcode mascarado (bit 15 separado para análise). Também mostra a
  frequência dos 12 códigos mais comuns.
- `trace dump` confirma a exportação após gravar o arquivo completo e
  preserva o buffer. Ao sair do console, eventos ainda **não exportados**
  são salvos automaticamente. Nenhum comando é emitido pelo trace.
- O hook tem custo de execução: prefira capturas curtas e separadas por
  ação, especialmente para comparar invocação, set, descarte, ativação e
  batalha. `trace` e `watch` são observadores complementares; nenhum
  demonstra sozinho a causalidade completa.

O contrato de quatro argumentos está fundamentado nos caminhos já estudados
e na captura dinâmica anterior (ver
[ABI findings](../docs/evidence/hybrid-native-abi-findings.md)).
Não trate o código `0xCE`, `0x79` ou `0x80` como o único efeito de
uma ação: o jogo pode emitir múltiplos comandos e efeitos em etapas.
Ainda precisamos testar a captura em execução real para guiar a futura
implementação de `summon`, `set` e `discard`.

### Summon e set experimentais: monstros nível 1–4, sem tributos

O índice da mão é **zero-based**. Exemplo de teste:

```text
hand 1
state
summon check 1 4
# Pressione Enter para ler console.last: checked, allowed, selectedSlot
summon 1 4
# OU, em outro turno, para colocar o monstro virado para baixo:
set check 1 4
set 1 4
# Pressione Enter para ler console.last e então faça:
diff
state
```

**Observação:** `state` atualiza a baseline usada por `diff`.
Para ver a mudança, capture `state` antes do summon e
execute `diff` **antes de um novo `state`** após a jogada.

Os quatro comandos (incluindo os modos `check`) são agendados
no `0x005BC700` e atendidos
**somente na thread do duelo**, jamais na thread RPC.
Verificações: duelo ativo, jogador 1 na vez, Main Phase 1 ou 2,
flag `0x20000` livre, fila/descritor inativos, identidade e
tamanho da mão inalterados, carta de monstro nível 1–4,
máscara de `FUN_005AAB90` (**`0x01` para summon**;
**`0x10` para set**), seleção de slot por
`FUN_0056A030` e confirmação por `FUN_00569F70`.
Assim preservamos as restrições de Ground Collapse.

`summon check` e `set check` retornam `state=checked`,
sem chamar o construtor nem consumir a invocação normal.
A função original de elegibilidade pode gerar mensagens de UI
em casos inválidos; o diagnóstico não promete ausência
total de efeitos de interface.

A execução ativa usa `FUN_005AD740(1,handIndex,slot,0,mode)`:
`mode=1` para invocação face para cima; `mode=0` para
baixar o monstro em defesa, virado para baixo. Ambas
chamam `FUN_00579930(1,0x11,1)`, sincronizando o
consumo da **mesma oportunidade de invocação normal**.
Uma operação bloqueia a outra até o próximo turno.

Se a flag `0x20000` estiver marcada, `console.last`
inclui `normalSummonUsed: true`, `playerFlags` e
`detail: 'Normal summon/set already used (player flag 0x20000)'`.
Isso distingue esse bloqueio de falhas de fase, classe
da carta ou zona indisponível.

`state=submitted` **não garante a colocação concluída**:
confira `state`/`diff`. `cancel` descarta um pedido
ainda pendente.

Escopo intencional: **apenas side 1 em duelo local**,
monstros comuns nível 1–4, sem tributos, magias/armadilhas,
invocações especiais ou exceções. O guard pode rejeitar
conservadoramente jogadas legais. O `summon` **já foi validado
em um duelo real**: `summon 1 5` colocou a carta interna
221 no slot 0, reduzindo a mão de 6 para 5. Veja
[`validação ao vivo`](../docs/evidence/console-native-summon-live-2026-11-04.md).
O **`set` também foi validado no Joey real**:
`set 1 3` colocou a carta interna 273 no slot 0
(`position=0x0201`) e reduziu a mão de 6 para 5.
Em seguida, `set check 1 0` confirmou
`normalSummonUsed=true`, com
`playerFlags=0x20000`. Os demais cenários
continuam experimentais; use duelos descartáveis.

Ver [relatório do novo `set`](../docs/evidence/console-native-set-implementation-2026-11-04.md).
A ABI da DLL continua v6; **não recompilar a DLL**.

### Probe passivo da preparação de summon/set

Após `trace on`, use `trace prep on` para ativar **cinco** hooks
adicionais de **leitura**: `0x005AAB90` (elegibilidade),
`0x005AD740` (**construtor da invocação normal/set**),
`0x005AB820` (despacho), `0x005ABD00` (colocação)
e `0x005AC870` (ajuste de contexto). Nenhum altera o jogo.

```text
trace on
trace prep on
hand 1
state
trace mark antes de invocar
# Invoque um monstro ou faça set pela interface do Joey
trace mark depois de invocar
trace prep off
trace off
trace dump
```

O JSONL recebe eventos `kind=summon_prep` com `phase=enter/leave`,
`callId`, nome da função, thread, retorno bruto, e snapshots dos
globais `0x00A55080/84/8E`, fase, turno, tamanho da fila e flags
dos dois jogadores (`0x00A56430 + lado*0xD44`). Para `0x005AAB90` são lidos dois argumentos brutos; para o
construtor `0x005AD740`, os **cinco argumentos brutos**, mais
`builderIntent` com `side, handIndex, fieldSlot, tributeData,
modeRaw, modeCandidate`. O modo é `0=set` e `!=0=summon`
nos caminhos normais identificados; efeitos podem sobrescrever
flags. `state.auxFlags` registra `DAT_00A5508C` (16 bits).
O evento de saída permite comparar o estado antes/depois
da preparação sem alterar nada. Valores de retorno de rotinas
`void` não devem ser interpretados como sucesso/falha.

O hook do despachante é filtrado: registra apenas estágio ativo no
modo 1, evitando centenas de amostras de ticks vazios. Os demais
hooks observam todas as suas chamadas enquanto ativos. Os eventos
entram no mesmo buffer circular de `trace on`, então `trace off`,
`trace dump` e `stop` removem os hooks de preparação antes da exportação.
Falha de instalação remove os hooks parcialmente instalados.

**Limite:** `0x005AD740` monta o descritor e inicia a operação,
mas **não verifica sozinho** se a invocação é legal. Nenhum
`summon`/`set` ativo foi adicionado. Já identificamos seu chamador
no controlador de jogada `0x0059DF40` e no caminho de IA
`0x005B6FF0`; a validação dessas chamadas continua pendente.
Veja [análise do construtor](../docs/evidence/summon-builder-contract-2026-11-04.md).

### Verificação de legalidade de cada zona

O Ghidra confirmou que `FUN_00569F70(side,slot)`
testa duas condições: ausência de carta no slot e
`FUN_00569E10(side,slot) != 0`.
`FUN_0056A000(side)` conta as zonas aceitas por essa
mesma função. Por isso, um slot vazio não pode ser
assumido como elegível, especialmente com Ground Collapse.
A decompilação posterior mostrou **Ground Collapse**
(`resourceId=0x528`) dentro de `FUN_00569E10`,
além de bloqueios codificados na tabela `'@'`.
Veja [relatório de legalidade](../docs/evidence/summon-slot-legality-2026-11-04.md)
e [regra Ground Collapse](../docs/evidence/ground-collapse-zone-predicate-2026-11-04.md).

### Confirmação estática do seletor de zonas

O helper `FUN_0056A030(side)` é o **seletor do primeiro slot
considerado permitido pelo motor**, usando
`FUN_00569F70(side,slot)` para testar as cinco posições.
Possui ramo adicional que evita posições selecionadas em
estado de duelo especial e retorna `0xFFFFFFFF` se
não encontrar destino. `FUN_00579930(side,0x11,1)`
altera a flag de bit 17 (`0x20000`) e emite `0x0D`
se necessário. Consulte
[`relatório estático`](../docs/evidence/summon-slot-selector-2026-11-04.md).
O predicado nativo de bloqueio Ground Collapse já foi identificado,
mas ainda faltam gates de turno/carta e preflight seguro na thread
do duelo antes de habilitar summon/set nativos.

### Ground Collapse e seleção de zonas disponíveis

Uma captura do construtor com duas zonas de monstro indisponíveis
devido a Ground Collapse registrou `normal_builder` com slot 2
para summon e slot 3 para set. Os dois saíram do mesmo callsite
`0x0059E208`. Isso prova que o destino é decidido antes do
construtor; uma zona vazia **não** é necessariamente jogável.
Veja [evidência de zonas](../docs/evidence/ground-collapse-summon-set-slots-2026-11-04.md).

### Próximo teste dinâmico do construtor normal

O `trace prep on` existente registra `normal_builder`
(`0x005AD740`), os cinco parâmetros e a `caller` de cada
invocação; não é necessária outra alteração de código.
Uma captura de summon e outra de set permitem confirmar
qual dos três callsites de `FUN_0059DF40` é usado em cada
ação. Para o estudo das condições de autorização, veja
[análise dos chamadores](../docs/evidence/summon-builder-callsites-2026-11-04.md).

### Validação em duelo: segundo slot e Field Zone

Em nova sessão de 52 comandos, o trace capturou
`CE context=0x4103, source=705` e o `watch` confirmou
que a carta 705 saiu do índice 4 da mão e ocupou o **slot 1**
do campo (lado 1). Também capturou Umi (índice 86) no **slot físico
10**, associado a `0xCF` com ID de recurso 333. Esses fatos
corroboram a decodificação candidata e a hipótese de Field Zone,
sem validar execução direta de comandos. Ver
[`relatório da sessão`](../docs/evidence/windows-native-second-monster-slot-and-field-2026-11-04.md).

## Testes offline

Execute a partir de `native_rules`:

```powershell
python .\tests\test_catalog.py
python .\tests\test_damage_limits.py
python .\tests\test_console_commands.py
python .\tests\test_process_selection.py
node .\tests\test_loader.cjs
ctest --test-dir .\build-vs2026 -C Release --output-on-failure
```

Os testes de Python, JS e C++ não substituem testes reais dentro do jogo.
O último cenário validado incluiu várias compras pelo console enquanto a
CPU jogava; não inclui verificação completa de todos os casos-limite.

## Segurança e escopo

- O carregador só aceita o `joey_pc.exe` com SHA-256
  `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`
  (PE32/x86).
- O DLL e o hook ficam ativos até `stop`. A DLL permanece carregada até
  fechar o processo do Joey; reinicie o jogo antes de recompilar/retestar.
- Não interrompa a sessão durante a resolução de efeitos, nem teste
  injeções em replay, duelos remotos ou menus sem validação específica.
- Frida é uma dependência **apenas do carregador**, não da lógica de
  regras em C++; uma futura injeção própria pode substituí-la.
- Endereços de funções são específicos dessa versão do EXE; nenhuma
  compatibilidade automática com outras versões foi demonstrada.

## Documentação de engenharia reversa

A documentação e as evidências estáticas continuam no diretório `docs/`,
independentemente deste projeto compilável. O histórico dos protótipos
removidos pode ser recuperado no Git. Arquivos históricos relevantes:
[ABI e testes híbridos](../docs/evidence/hybrid-native-abi-findings.md) e
[baseline do Jam Breeding](../docs/evidence/hybrid-jam-baseline.md).
