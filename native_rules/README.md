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
