# Evidência dinâmica: ABI do dispatcher e callbacks de cartas (2026-11-03)

Sessão fornecida pelo operador: Frida 17.25.2, processo PE32 ia32 com base real `0x00400000`, EXE previamente validado com hash `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. `joey_abi_probe.js` monitorou o wrapper `0x0059DEB0` e o produtor `0x005B9170`. Dados brutos da sessão não foram publicados no repositório.

## Amostragem e limites

- `ready_abi` emitido; 34 entradas e 34 saídas do wrapper com leitura bem-sucedida do ponteiro de pedido; contagem final **34 callbacks** e **263 chamadas ao produtor de comandos**.
- Apenas as primeiras **48** chamadas do produtor tiveram argumentos registrados; não inferir distribuição de todos os 263 comandos.
- Em todas as entradas documentadas do wrapper, `returnAddress = 0x005BBA7D`. Isso mostra o mesmo chamador direto nas amostras, não prova que seja o único chamador possível.
- O log não relata falha do Frida. Não houve substituição de código, validação de uma ABI completa ou recompilação.

## Identidade das cartas

Índices obtidos do primeiro `uint16` do pedido, cruzados com `docs/evidence/card-callback-catalog.json` (distintos de IDs de recurso):

| Índice interno | Carta | ID de recurso | Função principal da tabela | Ocorrências no wrapper |
|---:|---|---:|---|---:|
| 457 | Jam Breeding Machine | 1426 | `0x00583F30` | 1 |
| 972 | Morphing Jar | 590 | `0x005860E0` | 28 |
| 893 | Magic Jammer | 1029 | `0x00588D90` | 1 |
| 248 | Sangan | 47 | `0x00583D20` | 5 |

## Retornos repetidos com pedido aparentemente estável

**Morphing Jar, índice 972, primeira sequência**: `word1=0x9501`, `word2=0x03E1`. Retornos das 15 chamadas: `0x80 × 5 → 0x7F × 6 → 0x7E → 0x7D → 0x7C → 0x00`.

**Morphing Jar, segunda sequência**: `word1=0x9501`, `word2=0x0421`. Retornos das 13 chamadas: `0x80 × 5 → 0x7F × 3 → 0x7E → 0x7D → 0x7C → 0x00`.

**Sangan, índice 248**: `word1=0xA55D`, `word2=0x0361`; retornos `0x7F → 0x7E → 0x64 → 0x01 → 0x00` nas cinco chamadas amostradas.

**Jam Breeding Machine, índice 457**: `0x00`; **Magic Jammer, índice 893**: `0x00` — um único retorno amostrado por carta, sem inferir ausência de outras fases ou subcallbacks.

Esses padrões são **consistentes** com handlers/continuações em etapas e com o estudo estático de retorno `0x80/0x7F/0x7E` em outros callbacks. Não é válido nomear cada estado de Morphing Jar sem seguir `0x005860E0` e seus chamadores. Também não se pode contar uma chamada como uma ativação: Morphing Jar aparece 28 vezes na mesma sessão mas em duas sequências de pedido com valores distintos.

Os primeiros quatro `uint16` de cada pedido amostrado permaneceram iguais **dentro de cada sequência**, embora o retorno varie. Portanto, caso o estado de continuação seja persistido, ele pode estar em bytes posteriores do pedido, outros globais, ou resultar da evolução do estado do jogo. O experimento ainda não distingue esses casos.

## Pista do tamanho de 20 bytes

Para Magic Jammer, o pedido foi lido no endereço `0x00A55B54` e o segundo argumento bruto do wrapper era `0x00A55B40`. A diferença é `0x14` (=20), igual ao tamanho de pedido já documentado em `docs/cards/parasite-paracide.md`. É evidência consistente com dois registros adjacentes ou pedidos relacionados, não prova encadeamento sem seguir a lógica de quem constrói o segundo argumento.

## Comandos observados

Nos primeiros 48, aparecem `0x0061(..., 5, ...)` e `0x8061(..., 5, ...)` no início, depois `0x0061/0x8061(..., 1, ...)` no andamento; `0x02` antes de `0x03` nas transições de ambos os lados. Essas observações são compatíveis com `docs/21-native-card-draw.md` e `docs/19-temporary-state-cleanup.md`. A coleta observa a **emissão** dos comandos, não sua conclusão nem o instante de apresentação na tela.

## Próximo teste de baixo risco

Instrumentar `0x005860E0` e `0x00583D20` com `Interceptor.attach`, sem alterar seus retornos, e capturar uma amostra protegida dos **20 bytes** completos do pedido no começo e no fim de cada chamada. Correlacionar wrapper e handler pelo thread ID. Evitar instrumentar `0x0056DD30`, que teve mais de 128 mil chamadas na primeira prova.

Antes de substituir qualquer função, confirmar pelo assembly e rastros: convenção de chamada; origem/destino dos argumentos; forma de retorno; globais, flags e filas modificadas; efeitos indiretos. Nenhuma dessas condições é comprovada apenas pelos dados desta sessão.


## Terceira captura — hooks diretos e cobertura (2026-11-03)

`joey_card_handlers.js` conseguiu instalar hooks no wrapper, Morphing Jar, Sangan e Magic Jammer. Ao tentar `0x00583F30` (compartilhado com Jam Breeding Machine e DNA Surgery), o Frida declarou `unable to intercept`. A leitura de bytes `66 33 C0 C3 90...` corresponde a `xor ax, ax; ret; nop...`: apenas quatro bytes úteis no corpo, compatível com dificuldade de realocação do prólogo. A causa interna do Frida não foi isolada.

Na sessão, Morphing Jar teve 13 entradas no handler nativo `0x005860E0`; Sangan e Magic Jammer, zero. O wrapper, por sua vez, teve mais de 70 chamadas. **As primeiras 48 detalhadas** permitem mapear, pelo primeiro `uint16` e catálogo estático, Cyber Jar (índice 320, 31 chamadas), Giant Rat (218, 7), DNA Surgery (573, 2), Raigeki (345, 2), Harpie's Feather Duster (695, 2), Change of Heart (300, 1), Tribute to The Doomed (363, 1), Parasite Paracide (197, 1) e Magician of Faith (471, 1). Esses índices correspondem aos callbacks específicos do catálogo, mas não foram instrumentados individualmente.

Em Cyber Jar, os 20 bytes do pedido foram modificados entre entrada e saída em algumas chamadas. Um dos campos de 16 bits, no offset +6, foi observado progredindo de 0 para 1, 3, 7 e 15 em etapas sucessivas. Outros campos do pedido também mudaram. Essa progressão sugere informação acumulativa de andamento, mas **o significado do bitmask não está demonstrado**.

O último retorno de `MorphingJarPrimary` foi registrado como EAX bruto `0xFFFF0000`; o low16 é `0x0000`. Não interpretar o EAX de 32 bits como contrato do callback antes de conferir a assinatura e as instruções consumidoras do valor.

O log finaliza com `Process terminated`. Sem informação adicional, não se sabe se foi saída normal do jogo ou falha. O observador genérico `joey_all_primary_callbacks.js` foi criado para evitar a dependência de hooks em funções específicas e registrar índices de todos os pedidos que passarem pelo wrapper.


## Contexto funcional fornecido pelo operador — Jam Breeding Machine

O operador confirmou que **fechou deliberadamente o Joey** para encerrar a sessão anterior; portanto a mensagem `Process terminated` desse log não deve ser classificada como evidência de crash.

Também informou que **Jam Breeding Machine cria um token a cada Standby Phase aplicável**, enquanto permanece como carta contínua. Essa é uma descrição observacional fornecida pelo operador, ainda não uma reconstrução do mecanismo no binário.

O catálogo estático mostra que a entrada da carta é índice interno **457**, ID de recurso **1426 (0x592)**, perfil **P007**, com exatamente cinco slots `[0x00583F30, null, null, null, null]`. O mesmo perfil ocorre em **23 registros**, inclusive Gravity Bind, Messenger of Peace, Solemn Wishes, Anti-Spell Fragrance e The Dark Door. A função `0x00583F30` tem bytes iniciais `66 33 C0 C3` (`xor ax, ax; ret`): **não implementa emissão de comandos ou criação de token nesse corpo**. Isso não determina o mecanismo de outras rotinas do jogo.

Hipótese prioritária: regras contínuas/recorrentes podem ser verificadas fora da tabela de callbacks principais, em código global, processamento por fases ou funções que consultam IDs de carta. Não inferir que todas as 23 cartas compartilhem a mesma regra nem que o sistema seja completamente dirigido por dados sem inspecionar os caminhos consumidores. Próxima investigação estática: localizar consumidores da carta ID 0x592 (ou seu índice 457) no código de Standby e nas operações de criação de token, incluindo verificações de fase/campo. Próxima validação dinâmica: registrar as emissões de comando e alterações de contagem de tokens próximas à Standby, sem tentar hook em 0x00583F30.


## Quarta captura — dispatcher genérico (2026-11-03)

`joey_all_primary_callbacks.js` instrumentou apenas `0x0059DEB0` no PE32 ia32. Último resumo enviado: **169 chamadas**, **17 índices internos distintos** e **0 pedidos ilegíveis**. Houve 98 mensagens detalhadas (limite de 12 por índice); portanto os retornos individuais dos demais eventos constam apenas como histogramas agregados.

| Índice | Carta (catálogo estático) | Callback principal | Chamadas |
|---:|---|---|---:|
| 972 | Morphing Jar | 0x005860E0 | 37 |
| 320 | Cyber Jar | 0x0058A880 | 33 |
| 107 | Royal Decree | 0x005872B0 | 28 |
| 218 | Giant Rat | 0x0058AC10 | 21 |
| 453 | Nimble Momonga | 0x0058B040 | 12 |
| 22 | The Shallow Grave | 0x0058B400 | 12 |
| 248 | Sangan | 0x00583D20 | 5 |
| 851 | Backup Soldier | 0x0058BA80 | 4 |
| 695 | Harpie's Feather Duster | 0x00586850 | 4 |
| 1061 | Call Of The Haunted | 0x0058A4A0 | 3 |
| 802 | Dark Hole | 0x00584C80 | 2 |
| 359 | Monster Reborn | 0x005886C0 | 2 |
| 929 | Shield & Sword | 0x005881D0 | 2 |
| 467 | Mirror Force | 0x0058D660 | 1 |
| 893 | Magic Jammer | 0x00588D90 | 1 |
| 894 | Magic Cylinder | 0x0058D660 | 1 |
| 610 | Graceful Dice | 0x0058CA70 | 1 |

Todos os 17 índices aparecem no catálogo; não inferir quantidade de ativação por contagem de callbacks. Royal Decree foi consultada 28 vezes e apresentou retornos: `0x7F×2, 0x7E×12, 0x7D×12, 0x78×1, 0x00×1`. Essas sequências são compatíveis com processamento dividido em etapas; sem análise dos chamadores e da função específica não é válido nomear cada etapa. A sessão foi encerrada com o comando `exit`, sem mensagem de crash.

### Correção da identificação de nomes

Todos os 17 `card_seen` vieram com `name: null` porque o primeiro script tentou ler nomes a partir de `0x005F25A8 + índice×64`, endereço/layout **não demonstrado**. Não utilizar essa hipótese como fonte de nomes. O script foi corrigido para usar um dicionário estático derivado de `docs/evidence/card-callback-catalog.json`, para o SHA exato do executável, contendo **439 índices com nome** e seu `resourceId` e callback principal; três índices ligados a registros sem nome continuam sem rótulo. Isso não altera o motor nem exige novo dump de memória.

### Interface de chamada do callback principal identificada estaticamente

O listing de `docs/evidence/parasite-callback-dispatch.txt` mostra no dispatcher:

```asm
0059DEDD PUSH EDX        ; segundo argumento
0059DEDE PUSH ESI        ; ponteiro para pedido
0059DEDF CALL EAX        ; callback via tabela
0059DEE1 ADD ESP,0x8     ; chamador remove dois argumentos
0059DEE4 AND EAX,0xffff  ; wrapper mantém 16 bits baixos do retorno
```

Assim, **esse ponto de chamada** utiliza duas palavras/pointers de 32 bits na pilha, limpeza pelo chamador (compatível com `__cdecl` x86), retornando um valor efetivo de 16 bits no wrapper. É uma base suficiente para projetar uma função de ponteiro substituta de baixo risco, mas ainda não assegura compatibilidade com todos os outros slots, chamadas fora desse dispatcher ou semântica dos dois argumentos. Antes de substituir um ponteiro na tabela, planejar retorno/restauração no detach; caso contrário, o jogo pode chamar código descarregado.


## Quinta captura — Jam Breeding Machine no dispatcher principal (2026-11-04)

Nova sessão Frida do operador no processo `joey_pc.exe` PID 25580, base `0x00400000`, usando `joey_all_primary_callbacks.js` com catálogo de nomes. O log terminou com **21 chamadas ao dispatcher em 12 índices**; todos os pedidos relatados foram legíveis. Essas contagens são do intervalo instrumentado, não de toda a partida.

- **Jam Breeding Machine:** índice interno **457**, ID recurso **1426**; **1 chamada**, `returnLow16=0x0000`, sem alterações nos 20 bytes do pedido pelo wrapper. Bytes da entrada: `c9011380a1070000000000000000000000000000`.
- **Gravity Bind:** índice **244**, ID recurso **1323**; **1 chamada**, `returnLow16=0x0000` e pedido inalterado.
- **Aqua Chorus:** índice **11**, ID recurso **1178**; **1 chamada**, `returnLow16=0x0000` e pedido inalterado.
- Os três registros têm callback principal compartilhado **0x00583F30**, cujo corpo observado é apenas `xor ax, ax; ret`. Portanto **esse corpo específico não cria tokens nem agenda comandos por si só**.

Interpretação: a passagem de Jam Breeding Machine pelo dispatcher principal está **comprovada**. A emissão recorrente dos tokens deve passar por outra função ou mecanismo, ou ao menos não é implementada pelo corpo de quatro bytes de `0x00583F30`. O baseline externo anterior registrou 20 entradas distintas do índice de monstro 1114, mas essa captura do Frida não registrou diretamente tokens ou instantes de Standby; não alegar correlação temporal estrita entre eventos de sessões diferentes.

**Próxima investigação recomendada**: no Ghidra procurar xrefs de ID de recurso `1426 / 0x592`, índice interno `457 / 0x1C9` (cuidado: constantes podem ser genéricas), e o caminho de criação do token/índice `1114` durante o agendador de fases `docs/20-turn-scheduling.md`. Localizar o teste de espaço nas cinco zonas, o produtor de comandos e a função que itera cartas contínuas. Registrar evidência do fluxo e só então escolher esse código como alvo de substituição semântica.

**O protótipo `patch_jam_breeding.py` e `jam_primary_replacement.js` continua experimental e não está validado no Windows; não executar ainda.** Mesmo um patch equivalente no slot do callback principal somente demonstra redirecionamento técnico, não substituição do efeito recorrente.


## Sexta captura — primeiro redirecionamento executado (Sparks, 2026-11-04)

O operador executou `patch_sparks.py --pid 25580` no Windows com Frida e relatou o seguinte resultado real:

- SHA256 do executável original carregado: verificação passou.
- `rpc.install()`: `installed=true`, recurso **342** (Sparks), registro **66**, ponteiro principal original `0x00584E60`, ponteiro substituto `0x0B790000`.
- Durante o teste, `callbackCalls` evoluiu de **0 para 1**. Isso demonstra que a rotina substituta nativa executou pelo menos sua instrução de contagem.
- Após o comando `stop`, o host informou `Original callback pointer restored; callbackCalls=1`. Não houve erro reportado na restauração.
- O console mostrou `Unable to start idund: idund did not start listening` antes da instalação, mas a instalação e a contagem aconteceram. Causa desse aviso não investigada, sem evidência de impedimento do teste.

**Conclusão comprovada:** a técnica de modificar só o slot principal do registro da carta (sem alterar o endereço compartilhado `0x00584E60`) conseguiu redirecionar e executar código x86 inserido em memória, com restauração relatada pelo script. Esse é o primeiro checkpoint dinâmico de redirecionamento seletivo do YugiRE.

**Não comprovado:** o operador não informou nesta saída os LP do oponente antes/depois. `callbackCalls=1` não prova, sozinho, que o branch do stub chamou `0x005784E0`, que o comando 0x43 foi emitido, ou que o efeito de 200 LP foi aplicado. Confirmação funcional pendente: observar LP antes/depois e, idealmente, registrar o comando correspondente; um teste com dano temporário 300 LP só deve ser considerado após baseline funcional de 200 LP e revisão do procedimento.


## Sexta captura — confirmação funcional fornecida pelo operador (2026-11-04)

Após o primeiro redirecionamento de Sparks (recurso 342, linha 66), que já havia mostrado `callbackCalls=1` e restauração do ponteiro, o operador confirmou explicitamente: **o adversário perdeu 200 LP ao utilizar Sparks com o patch ativo**. Portanto temos a primeira validação funcional em duelo real para o callback principal seletivamente reimplementado (contexto normal, uma chamada observada).

Esse resultado é consistente com o stub nativo chamando `0x005784E0(destinatario, 200)`; junto com o contador e a checagem do slot, sustenta que o efeito passou pelo código substituto. Não prova equivalência completa em condições de negação, bloqueio, múltiplas instâncias ou todas as configurações de partida, nem se todo o estado/transcrição YDR corresponde bit a bit ao original.

Para uma prova controlada de capacidade de alterar a regra, foi acrescentada a opção **`--damage 300`** no host `patch_sparks.py`, que modifica **somente o imediato PUSH 200 -> PUSH 300** no script de agente carregado em memória, após conferir exatamente a sequência de bytes pretendida. O padrão permanece **200 LP** sem alterar o JavaScript validado na primeira captura. Essa variante de 300 LP **ainda não foi executada pelo operador**. Antes do teste, fazer backup de saves; executar duelo normal, capturar LP antes/depois e contador; digitar `stop` para restaurar a função original.


## Sétima captura — alteração funcional confirmada: Sparks 300 LP (2026-11-04)

O operador confirmou que a variante do patch `patch_sparks.py --damage 300`, adicionada após a captura funcional original de 200 LP, foi executada em duelo real e que **o adversário perdeu 300 LP**, conforme configurado. Isso é a primeira validação dinâmica de **modificação semântica da regra de uma carta**: o mesmo redirecionamento seletivo na tabela de callbacks (somente registro de Sparks, ID de recurso 342) permite produzir um dano diferente do original por uma rotina x86 própria e pelo helper nativo de dano.

Evidências acumuladas:

1. `--damage 200`: registro de instalação, `callbackCalls=1`, restauração de ponteiro e confirmação visual do operador de perda de 200 LP.
2. `--damage 300`: confirmação visual do operador de perda de **300 LP** na partida com a variante do patch.

A confirmação do teste de 300 LP foi relatada sem log adicional de contador, restauração ou estado de RAM/YDR nessa mensagem; não atribuir valores ou afirmar restauração verificada para essa segunda execução. A alteração afeta somente uma entrada da tabela, não o código compartilhado de `0x00584E60` nem o executável em disco por intenção. **Conclusão limitada:** capacidade demonstrada de substituir e alterar o efeito de Sparks em um duelo normal. Não equivale a recompilação matching, validação de todas as cartas nem demonstração de robustez ante múltiplos turnos, replay, cancelamento ou interações raras.

Próximo checkpoint sugerido: portar o stub para uma DLL x86 com ABI explícita e ponteiro restaurado no teardown, mantendo prova de equivalência 200 LP e alteração 300 LP como testes de regressão; documentar fronteiras de ownership de memória e funções auxiliares nativas antes de expandir para outros callbacks.


## Oitava captura — Sparks executado a partir de DLL C++ x86 (2026-11-04)

O operador compilou `joey_rules.dll` com Visual Studio 2026, configurado para Win32. Ao tentar carregar a DLL pela primeira vez, o agente Frida falhou com `invalid abi specified` (uso de `cdecl` em `NativeFunction` no carregador), após validar o fingerprint do EXE. A correção do agente substituiu a designação de ABI de `cdecl` por `mscdecl`; não houve necessidade de recompilar a DLL.

No teste subsequente, o operador confirmou explicitamente: **Sparks causou 300 LP de dano ao adversário usando a variante carregada pela DLL C++**. Isso demonstra em duelo real que o callback selecionado foi redirecionado com sucesso para a implementação compilada em Win32, que reutilizou o produtor de dano nativo do jogo.

Escopo validado: execução de efeito normal com dano modificado (300 LP) usando código compilado em C++ fora do executável original. Informações não fornecidas nesta captura: contador da DLL, status final da restauração, log das chamadas ao helper, nova captura de RAM e replay. Não alegar que estes elementos foram observados. A solução de DLL ainda não foi submetida a testes de várias cartas, negação, dupla ativação ou descarregamento com chamadas em andamento.

Próximo passo arquitetural sugerido: transformar o experimento Sparks em um módulo reproduzível (fonte C++, CMake, carregador Frida e teste) versionado na branch; centralizar validação do fingerprint, resolução de RVA e operações reversíveis de tabela num adaptador, e preservar a lógica das cartas em código C++ independente.


## Nona captura — Sparks e Raimei simultâneos em DLL C++ (2026-11-04)

O operador relatou ter executado com sucesso **ambas as cartas, Sparks e Raimei**, no experimento de substituição simultânea por funções C++ independentes após o commit `949c746`. A configuração sugerida no teste era `--sparks 300 --raimei 400`. O relato confirma que foi possível usar as duas cartas; a mensagem não especifica separadamente os LP exatos observados de cada uma, nem inclui log de contadores ou restauração, portanto não presumir esses detalhes.

A interface atual restringe explicitamente os danos a **Sparks 200/300** e **Raimei 300/400** em três camadas: argumentos do Python, validação do agente Frida e inicialização do módulo C++. A função nativa do jogo recebe quantidade numérica, mas limites altos, overflow, condições especiais e efeitos sobre a partida ainda não foram validados.


## Décima captura — composição de efeitos com compra de cinco cartas (2026-11-04)

O operador confirmou após executar no Joey a DLL C++ com os modos de efeito
introduzidos pelo commit `bb1dcc37c2fd1f4fa0b19f4784cce6edade2c981`:

> "funcionou perfeitamente, as duas cartas!"

A configuração publicada para o teste era:
- **Sparks** (`--sparks-mode draw`): substitui o dano original por compra de
  cinco cartas para o jogador que ativou a magia;
- **Raimei** (`--raimei-mode damage-draw --raimei 400`): solicita o comando
  nativo de dano de 400 LP seguido de um lote de cinco compras para o ator.

O sucesso relatado demonstra em duelo real a viabilidade de **trocar a
categoria de um efeito** e de **compor dois mecanismos do motor original**
(dano em `0x005784E0` e compra em `0x00578B60`) usando callbacks próprios
em C++ e redirecionamentos individuais por tabela. Os efeitos continuam
a ser consumidos pela fila original do Joey; a DLL não modifica as estruturas
de mão ou deck diretamente.

Esta captura é uma confirmação descritiva do operador, **não** inclui log
novo de `drawBatches`, `damageCommands`, contagens de mão/deck ou um número
de LP transcrito; evitar afirmar valores de contadores que não foram enviados.

Próximos testes possíveis: negadores e correntes, poucas cartas no deck,
ativação em lados diferentes e repetição de efeito. A confirmação de cenário
normal não prova equivalência geral, nem segurança de todos os casos-limite.
