# YugiRE: prova de viabilidade da execução híbrida

Este experimento não é um recompilador e não substitui nenhuma regra. Ele verifica se o executável local corresponde ao build estudado e, opcionalmente, conta chamadas a três funções nativas por instrumentação no processo.

## 1. Preflight sem instalação adicional

No PowerShell, na raiz do repositório:

    py -3 experiments/hybrid_probe/pe_preflight.py "C:\CAMINHO\joey_pc.exe"

Resultado esperado: "passed": true. O script verifica SHA256 DB8FE7B7EA4B12F18A9BB1777B5F5B8EAB254011DB2A52571A0264725505B6DE, PE32 x86, base de imagem 0x00400000 e presença das funções 0x0059DEB0, 0x005B9170 e 0x0056DD30 na seção de código. Não altera o EXE. Se falhar, não execute o teste de instrumentação com estes endereços.

## 2. Observação experimental com Frida

Use uma cópia de testes do jogo e faça backup dos saves. A instalação fica isolada numa pasta temporária:

    py -3 -m venv .venv-frida
    .\.venv-frida\Scripts\python.exe -m pip install frida-tools

Inicie o Joey normalmente, obtenha o PID com:

    Get-Process joey_pc

Depois, substitua o número 1234 pelo PID real:

    .\.venv-frida\Scripts\frida.exe -p 1234 -l experiments/hybrid_probe/joey_calls.js

Faça um duelo curto, compre e invoque uma carta. Observe as mensagens ready, first_call e counts. O script contabiliza invocações de InvokePrimaryCardCallback (0x0059DEB0), QueueDuelCommand (0x005B9170) e CalculateCardAttributes (0x0056DD30), e registra registradores brutos nas primeiras três chamadas de cada função. Ele não supõe uma assinatura nem decodifica cartas. Encerre a ferramenta pelo terminal. A pasta .venv-frida pode ser excluída após o experimento.

### Limites e precauções

Frida Interceptor.attach modifica temporariamente instruções na memória do processo para instrumentação, mas não altera o arquivo executável em disco. Não é a mesma modalidade externa de leitura do JoeyTrace; pode causar falhas e alterar timing. Não instale hooks simultâneos do JoeyTrace/Ghidra na primeira tentativa; teste em uma cópia local e preserve os saves. O script não modifica parâmetros, valores de retorno ou regras deliberadamente. Endereços são ajustados à base real do módulo, após o preflight do SHA exato.

### Critérios de decisão

- Preflight falha: primeiro identificar a versão correta.
- Frida não anexa: resolver ambiente/arquitetura; não modificar EXE às cegas.
- Mensagem ready e contadores crescendo, jogo estável: podemos investigar contrato ABI de uma função isolada e preparar DLL x86 para substituição.
- Sem contadores: conferir se o cenário acionou os endereços e os xrefs no Ghidra.
- Crash ou congelamento: remover instrumentação, escolher alvo menos sensível.

Esse teste não demonstra que o motor inteiro pode ser recompilado nem que já é seguro substituir as funções. A etapa seguinte é recuperar calling convention, parâmetros, efeitos colaterais e estado compartilhado antes de experimentar uma substituição.

## O que compartilhar depois

Envie a saída JSON do preflight, as mensagens ready/counts do Frida e diga quais ações executou e se houve algum problema. Não é necessário enviar o executável. Para um estudo de recompilação automática, o próximo experimento deve testar lifting de uma função separadamente e avaliar as dependências.


## 3. Próximo experimento após o teste de chamadas (2026-11-03)

O operador confirmou preflight com SHA256 esperado, PE32 x86, base 0x00400000 e as três funções na seção .text. Frida 17.25.2 anexou em ia32 e registrou, ao longo da sessão, 123 chamadas ao callback primário, 690 chamadas ao enfileiramento de comandos e 128513 cálculos de atributos. Primeiras três chamadas ao callback primário retornaram de 0x005BBA7D; há evidência de um ponto comum de despacho, sem provar que cada invocação represente uma ativação de carta. Não foi relatada falha no log. A instrumentação (não a substituição) está validada nessa versão.

A etapa seguinte é observar os **contratos das funções**, antes de usar DLL ou Interceptor.replace. O novo joey_abi_probe.js registra as primeiras 48 chamadas ao callback primário e as primeiras 48 chamadas ao produtor de comandos. Não instala hook em CalculateCardAttributes, que recebe chamadas demais para um experimento detalhado sem necessidade.

No PowerShell, na pasta experiments/hybrid_probe, com a venv anterior já instalada e o jogo aberto:

    Get-Process joey_pc
    .\.venv-frida\Scripts\frida.exe -p 1234 -l .\joey_abi_probe.js

Troque 1234 pelo PID real. Faça algumas ações de duelo e salve a saída do terminal. O script registra quatro words no pedido a partir do ponteiro em [ESP+4] no callback (layout parcialmente identificado estaticamente em docs/13-parasite-creation.md), seu segundo argumento bruto, o endereço de retorno e EAX no fim. Para o produtor de comandos registra quatro words de argumentos, o opcode base mascarado e o bit alto, sem inferir efeitos de cartas. A leitura do pedido é protegida por verificação de faixa legível; falhas são registradas, não corrigidas.

A interpretação do primeiro word como índice de propriedades já está sustentada pela análise estática; identificar as outras words exige confrontar com o cenário e seguir os chamadores no Ghidra. Em especial, valores brutos de ECX e EDX da primeira prova não determinam a convenção de chamada.

**Não há recompilação nem substituição de função neste estágio.** Instrumentação pode influenciar timing e exige a mesma cópia descartável do jogo. Uma substituição só deve ser tentada após confirmar ABI, retorno, side effects e ponteiros compartilhados de uma função de baixo risco.


## 4. Observação dos callbacks específicos (opcional, após resultado da etapa 3)

O segundo log da sessão confirmou 34 chamadas ao wrapper e 263 enfileiramentos de comandos, com 48 registros de argumentos. O catálogo correlaciona os índices internos 972/Morphing Jar com 0x005860E0; 248/Sangan com 0x00583D20; 893/Magic Jammer com 0x00588D90; 457/Jam Breeding Machine com 0x00583F30. Detalhes e retornos observados: ABI_FINDINGS_2026-11-03.md.

Para verificar se o wrapper encaminha chamadas a esses handlers e se os 20 bytes do pedido mudam entre entrada e saída, o script joey_card_handlers.js monitora essas quatro funções mais o dispatcher. Faça um duelo curto na mesma cópia de testes; com a Frida venv instalada e o processo aberto:

    .\.venv-frida\Scripts\frida.exe -p 1234 -l .\joey_card_handlers.js

Substitua 1234 pelo PID atual. A captura registra no máximo 48 chamadas detalhadas por função, os valores brutos de arg0/arg1, ECX/EDX, EAX ao retornar e uma leitura protegida de 20 bytes em arg0. **Somente no dispatcher** foi confirmado que arg0 aponta para o pedido; nos callbacks específicos o campo maybeRequest20Hex ainda é hipótese e pode ser irrelevante. Ao terminar, compare as entradas e saídas por função/thread e posição temporal no log.

Esse script ainda não é uma DLL substituta. Logs podem conter estruturas internas e devem permanecer locais ou ser compartilhados conscientemente. Mais jogatinas não são necessárias para seguir o próximo passo estático: estudar 0x005860E0 no Ghidra, especialmente o avanço de estados e os efeitos colaterais das chamadas.


### Frida 17: erro `unable to intercept function at 00583F30`

Observado no Windows x86 com Frida 17.25.2 ao tentar instrumentar `JamBreedingMachinePrimary` (`0x00583F30`). O attach ao processo já estava funcionando; a falha específica veio de `Interceptor.attach` nesse endereço. O motivo exato (função curta, prólogo/relocação ou outra restrição) **ainda não foi determinado**.

O script `joey_card_handlers.js` foi atualizado para envolver **cada** instalação de hook com `try/catch`, emitir `hook_skipped` com o erro e até 16 bytes iniciais da função e continuar com os demais. A mensagem `ready_handlers.targets` agora distingue `attached` e `skipped`. Não inferir que um hook pulado foi observado. Isso não substitui a investigação no Ghidra sobre o prólogo de `0x00583F30`.

Após `git pull` na branch experimental, no mesmo diretório com a venv existente:

    .\Scripts\frida.exe -p 3664 -l ..\joey_card_handlers.js

Troque 3664 pelo PID atual. Mesmo que a função `0x00583F30` continue impossível de interceptar, os hooks de Morphing Jar, Sangan, Magic Jammer e dispatcher poderão registrar chamadas. Esse ajuste ainda requer teste no processo real.


## 5. Observador genérico de todas as cartas que chegam ao dispatcher

No terceiro experimento de 2026-11-03, Frida anexou os hooks do dispatcher, Morphing Jar, Sangan e Magic Jammer. O hook de \`0x00583F30\` não pôde ser instalado: seus bytes iniciais \`66 33 C0 C3\` representam \`xor ax, ax; ret\`, uma função de apenas quatro bytes. Durante o teste, o Morphing Jar recebeu 13 chamadas no handler específico; Sangan e Magic Jammer não receberam chamadas. O dispatcher geral, porém, viu outras cartas: Cyber Jar 31, Giant Rat 7, DNA Surgery 2, Raigeki 2, Harpie's Feather Duster 2, e outras (apenas 48 registros detalhados do wrapper). Esse é o motivo pelo qual monitorar exclusivamente quatro handlers dá uma visão incompleta.

Para observar qualquer carta cujo **callback principal** seja invocado, sem instalar hooks individualmente nos handlers, use o novo \`joey_all_primary_callbacks.js\`. O script instala um único hook no dispatcher \`0x0059DEB0\`, captura o índice interno do pedido e tenta obter o nome do buffer \`CARD_Name\` de 64 bytes por índice. Se o nome não puder ser lido ou parecer inválido, o índice é preservado sem nome. Registra no máximo 12 eventos detalhados por índice e faz resumo com contadores a cada 15 segundos quando há novas chamadas. Não usa hooks nos callbacks individuais.

No diretório \`experiments/hybrid_probe/.venv-frida\`:

    .\Scripts\frida.exe -p 1234 -l ..\joey_all_primary_callbacks.js

Substitua o PID atual do Joey. Salve o texto de \`ready_all_cards\`, \`card_seen\`, \`card_callback\` e \`summary\` para comparação com o catálogo do repositório. O script ainda necessita validação de execução no Windows.

Este observador **não vê monstros normais, nem cartas cujos efeitos sejam tratados por código global sem passar pelo callback principal**, e não demonstra que todo nome carregado na RAM corresponde a um nome válido. Também não resolve a hipótese de recompilação automática ou substituição seletiva; seu papel é facilitar a identificação do contrato do dispatcher e escolher a próxima função pequena a investigar.


## Resultado da quarta captura e nomes das cartas (2026-11-03)

O observador genérico registrou **169 chamadas ao dispatcher**, distribuídas entre **17 índices internos**, todos reconhecidos pelo catálogo estático. Entretanto, o código que tentava ler o nome de uma região presumida da RAM retornou `None` em todas as cartas. Esse endereço/layout não foi validado e foi **removido** do script.

O `joey_all_primary_callbacks.js` atualizado agora contém um mapa estático para **439 índices com nomes**, derivado de `docs/evidence/card-callback-catalog.json`, incluindo nome, ID de recurso e endereço do callback principal. Isso evita depender de uma hipótese de endereço em memória e preserva índices ainda sem nome. Depois de atualizar a branch com `git pull`, as novas mensagens `card_seen` e `summary` devem exibir nomes das cartas presentes no catálogo. Não precisamos repetir longas sessões para validar o funcionamento do dispatcher; esse ajuste melhora apenas os rótulos.

Também foi confirmado pelo listing do próprio repositório que o wrapper `0x0059DEB0` chama os callbacks via dois argumentos na pilha e limpa oito bytes após a chamada, preservando 16 bits baixos do retorno. Ver `ABI_FINDINGS_2026-11-03.md` para a evidência e os cuidados com experimentos de substituição.


## 6. Replays .ydr como teste de regressão

O formato YDR documentado em docs/29-replay-ydr.md contém listas iniciais de cartas e comandos de oito bytes. Já foi identificado um caminho nativo de reapresentação dos comandos (docs/11-command-4d.md). Isso sugere que reproduzir um replay gravado pode **não chamar os callbacks que produziram os comandos**. Não tratar um replay histórico funcionando como prova de equivalência de uma substituição de regra.

Adicionado compare_ydr.py, que utiliza o parser scripts/inspect_joey_replay.py para comparar listas iniciais de cartas e a sequência ordenada de quatro palavras de 16 bits por comando. Ignora campos de identificação/timestamp do cabeçalho. Mostra o primeiro registro divergente, os tamanhos das sequências e histogramas de opcodes. Retorna 0 se decks e comandos são iguais, 1 se diferentes e 2 em caso de erro.

Da raiz do repositório:

    py -3 experiments/hybrid_probe/compare_ydr.py "scratch/baseline.ydr" "scratch/patched.ydr" --json "scratch/ydr-diff.json"
    py -3 -m unittest discover -s tests -p "test_joey_compare_ydr.py" -v

Plano experimental para Jam Breeding Machine:

1. Em uma cópia do jogo, duelar normalmente sem patch, colocar a carta em campo, avançar por algumas Standby Phases, anotar se um token surge em cada fase aplicável e salvar YDR. Captura com JoeyTrace --card-events pode complementar observações; preserve saves.
2. Duelo normal com o **patch do ponteiro principal apenas dessa carta**, registrando chamadas reais à função substituta. Observar os tokens durante as Standby Phases.
3. Comparar os YDRs. Igualdade exata é evidência forte sobre comandos gravados, mas não prova equivalência de todo o estado; divergência também pode ser causada por decisões da IA, embaralhamento, RNG ou jogadas distintas. Não esperar igualdade bit a bit em duelos não determinísticos.
4. Como teste complementar, abrir o YDR antigo enquanto o patch está ativo e medir se a função substituta é chamada. Caso o contador fique zero, esse replay testa principalmente o consumidor de comandos, não a lógica substituída.

Por isso o melhor teste de funções produtoras de efeitos é duelo normal controlado + contador do patch + estado de campo + sequência YDR. Para substituições do consumidor/apresentação, um replay antigo pode ser um teste mais direto. A estrutura de reexecução do YDR ainda precisa ser confirmada dinamicamente.


## 7. Primeira substituição funcional — Sparks (experimental)

A carta **Sparks** é mais adequada que Jam Breeding Machine para demonstrar uma substituição funcional: seu efeito normal de dano é imediato, quantificável e já foi acompanhado até o comando 0x43 em docs/cards/sparks-ookazi.md.

- Índice interno **755**, recurso ID **342 / 0x156**, linha **66** da tabela de 443 registros.
- Callback compartilhado original **0x00584E60**. Sparks e Ookazi usam o mesmo endereço, então **não modificar diretamente as instruções dessa função**.
- Slot primário específico de Sparks: 0x005ED0A8 + 66*24 + 4 = 0x005ED6DC na imagem base 0x00400000.
- Slot de preparo de alvo +20 continua apontando para 0x00593CC0. Esse slot resolve o oponente no pedido.
- O código substituto, escrito como stub nativo x86 e **não** em JS no caminho crítico, incrementa um contador, checa pedido[4] & 4, lê pedido[6] & 1, chama 0x005784E0(lado, 200) e retorna limpando apenas AX. As chamadas a esse helper são responsáveis por emitir o mesmo comando de dano usado pela implementação original.
- O programa Python valida o **hash do EXE realmente carregado** antes de alterar a RAM e confirma os cinco campos do registro da carta. O patch é somente em memória; não altera EXE, saves ou outros callbacks por intenção.
- O patch restaura o ponteiro original quando o operador digita stop ou pressiona Ctrl+C e deixa a memória nativa alocada até o encerramento do processo, para não criar um endereço inválido no teardown. Se o terminal ou Frida cair abruptamente, feche o Joey normalmente antes de qualquer outro experimento.

**ATENÇÃO:** o stub foi inspecionado com desmontagem x86 local, e a sintaxe JS validada estaticamente, porém **ainda não foi testado dentro do Joey no Windows**. Use backup dos saves, uma cópia descartável do jogo e faça primeiro um duelo curto.

Para testar na branch experimental, inicie o Joey e, em um PowerShell na raiz do projeto:

    git pull
    cd experiments\hybrid_probe
    $joeyPid = (Get-Process joey_pc).Id
    .\.venv-frida\Scripts\python.exe .\patch_sparks.py --pid $joeyPid

Deixe o terminal aberto. Em um duelo **normal** (não replay), use Sparks. Pressione Enter no terminal para ler callbackCalls; se o efeito funcionar, o oponente perderá **200 LP**. Ao terminar, digite stop para confirmar a restauração do ponteiro. Se não houver perda de 200 LP, qualquer erro ou encerramento inesperado, interrompa o experimento; não tente repetir comandos de patch no mesmo processo.

Para diagnóstico inicial de instalação, copiar as mensagens [sparks] do host e relatar LP do oponente antes/depois. O teste **não prova ainda** equivalência completa em todos os contextos de negação e múltiplas ativações.

Esta prova demonstra reimplementação seletiva de um efeito usando os helpers nativos do motor; não confundir com injeção no callback de Jam Breeding Machine (corpo vazio) ou com implementação do sistema de turnos. Não rodar ao mesmo tempo que joey_all_primary_callbacks.js, joey_abi_probe.js ou outro hook na tabela. O modo anterior de Jam Breeding Machine em patch_jam_breeding.py permanece pendente de revisão e não é recomendado.


### Confirmação real do primeiro efeito reimplementado

O operador confirmou que, no primeiro teste com `patch_sparks.py` padrão, o contador do callback foi de 0 para 1, o oponente perdeu **200 LP**, e o ponteiro foi restaurado pelo comando `stop`. Este é um teste dinâmico funcional da substituição individual de Sparks, não uma validação de todos os contextos da carta.

A próxima experiência opcional muda apenas o dano causado pelo nosso stub, de **200 para 300 LP**, para provar controle semântico. Na pasta `experiments/hybrid_probe` e com Joey reiniciado:

    $joeyPid = (Get-Process joey_pc).Id
    .\.venv-frida\Scripts\python.exe .\patch_sparks.py --pid $joeyPid --damage 300

O agente JS em disco permanece equivalente ao original de 200 LP. O host modifica apenas os quatro bytes imediatos da instrução `push 200` na fonte do agente **em memória antes da injeção**, após verificar que o padrão a substituir aparece exatamente uma vez. O argumento `--damage` só aceita `200` ou `300`; sem argumento, permanece `200`. Em duelo normal, verificar LP do adversário antes/depois de uma única Sparks e `callbackCalls=1`. Digitar `stop` para restaurar a entrada da tabela. O efeito de 300 LP ainda NÃO foi testado no Joey real. Backups dos saves e cópia do jogo continuam recomendados.
