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
