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
