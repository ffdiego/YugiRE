# Experimento sintético de ESI e Frida (Windows x86)

Objetivo: distinguir os dois overloads de Interceptor.attach em uma instrução
interna, sem abrir o Joey nem carregar arquivos do jogo. A hipótese original está
em docs/research/handoffs/native-esi-crash-static-analysis.md.

A função sintética executa push esi / mov esi,0D44h / push esi e a janela
de seis bytes A8 02 75 1B 0C 02 (TEST / JNE / OR). Após o ramo, restaura
ESI para uma estrutura de saída; ela nunca desreferencia o ESI corrompido.

## Compilar

No Windows, abra o Developer PowerShell ou Developer Command Prompt do Visual Studio.
O `build.cmd` agora tenta inicializar o toolchain MSVC com alvo **x86**
automaticamente, inclusive se o terminal atual for x64, por meio de
`VsDevCmd.bat -arch=x86 -host_arch=x64`. A troca ocorre somente dentro do
script; não altera a configuração permanente do terminal. Em instalações
sem o componente MSVC x86, o script falha com mensagem explícita.
Dentro desta pasta, execute:

~~~cmd
build.cmd
~~~

Usa o cl.exe do Visual Studio já instalado. Não usa Joey, DAT, Ghidra ou
bibliotecas baixadas. Só compila para x86 e produz esi_fixture_x86.exe.

## Executar

No PowerShell, na raiz do repositório, usando o ambiente Python/Frida existente:

~~~powershell
$py = '.\native_hooks\.venv-frida\Scripts\python.exe'
$script = '.\native_hooks\experiments\esi_stack_probe\run_fixture.py'
$exe = '.\native_hooks\experiments\esi_stack_probe\esi_fixture_x86.exe'

& $py $script --exe $exe --mode baseline --al 0
& $py $script --exe $exe --mode baseline --al 2
& $py $script --exe $exe --mode probe --al 0
& $py $script --exe $exe --mode probe --al 2
~~~

Pare imediatamente se baseline ou probe retornar fail ou inconclusive_*.

**Somente depois**, execute o controle negativo com o objeto Frida,
limitado ao executável sintético. Ele **pode corromper ESI ou encerrar o
processo descartável**:

~~~powershell
& $py $script --exe $exe --mode object --al 0 --allow-unsafe-object-control
& $py $script --exe $exe --mode object --al 2 --allow-unsafe-object-control
~~~

Cada comando inicia um processo **novo e único**, lê a confirmação READY com PID
e endereço do probe, conecta o Frida apenas ao PID criado e envia GO após
confirmar a instalação. O programa verifica nome, cabeçalho PE x86 e marcador
do próprio fixture; nunca se conecta a um PID arbitrário.

Relatórios locais: scratch/esi_stack_probe/esi-*.json. Registram SHA256 do
executável e agente, versão do Python/Frida, runtime do script, registradores,
resultado, código de saída e erros. Não versionar dumps, binários nem JSON bruto.

## Critérios

- baseline e probe: pass; ESI=0x00000D44; rota 1 para AL=0, rota 0 para AL=2.
- object_overwrote_saved_esi: mecanismo reproduzido nessa versão do Frida.
- object_not_reproduced: hipótese não reproduzida; não significa refutação
  da ocorrência histórica.
- inconclusive_* / ausência de RESULT / crash: interromper e analisar o
  relatório antes de nova tentativa.

O teste sintético nunca confirma, sozinho, a causa exata do incidente histórico
nem autoriza reativar os nove hooks simultaneamente no Joey. Os testes Python,
Node e a compilação estática de um objeto x86 **não substituem** o piloto real
com MSVC e Frida no Windows.
