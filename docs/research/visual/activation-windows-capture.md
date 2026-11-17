# Captura Windows de uma ativação natural — preparada, não executada

O usuário escolheu preparar esta etapa para execução posterior. Não há host Windows
conectado nesta sessão. Este documento não relata uma observação já realizada.
O observador está em `tools/visual/capture/`, sem alterar o backend `native_hooks/`.

## Alvo, entrada e parada

- Alvo: processo Joey **já aberto**, Windows x86, EXE de SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- DAT fornecido ao host: SHA256 `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`; o caminho/hash declarado não é uma observação de abertura desse DAT pelo processo.
- Entrada: **uma magia/armadilha azul ativada normalmente pelo operador**, sem forçar comando, seed, carta ou callback.
- Seleção: primeira chamada natural de `00450590` com vtable exatamente `005DB2F4`, contador zero e categoria azul. Subclasses e efeitos amarelos não são capturados como equivalentes.
- Saída esperada: updates antes/depois, draws e seus passes, QPC/frequência, tabela cosseno, geometrias e formatos.
- Falha: SHA/arquitetura divergente, leitura inválida, perda/overflow, contexto inesperado, objeto não encontrado, timeout ou teardown incompleto.
- Parada: primeira instância desativada, limite de eventos ou até 120 segundos; Ctrl+C encerra e desarma. O processo permanece aberto.

## Pré-requisitos e comando

Use o ambiente Python/Frida Windows **já configurado** do projeto e uma cópia desta
branch. Não instalar Wine nem iniciar o EXE no Linux. Não é necessário modificar
o jogo, decks, saves ou opções de velocidade. Evite outros hooks ativos durante a
medição, para não misturar overheads.

Da raiz do checkout no PowerShell, substituindo os caminhos e o PID reais:

```powershell
python tools\visual\capture\capture_activation.py --pid 1234 --exe 'C:\jogo\joey_pc.exe' --dat 'C:\jogo\data.dat' --seconds 60 --max-events 1024 --output-dir scratch\visual\activation-windows
```

O host confere o SHA antes de armar, confirma caminho/arquitetura do módulo do PID,
instala somente interceptadores de observação e imprime o nome do JSONL ao terminar.
Não faz spawn, input sintético, patch, override de RNG/velocidade, chamada artificial
a funções do jogo nem alteração de argumentos/retornos/memória do jogo.
Frida instala trampolines e aloca memória de instrumentação: “observação” não significa
ausência de overhead ou de instrumentação no processo.
As únicas `NativeFunction` chamadas são as APIs Windows QueryPerformanceCounter e
QueryPerformanceFrequency, para leitura do relógio. Não são helpers da engine.

Se o efeito escolhido usar outra classe, o coletor pode terminar sem seleção. Isso
é uma captura incompleta, não motivo para invocar a classe artificialmente ou
relaxar a validação. O analisador recusa renderização 16 bits e formatos inesperados;
registre a limitação em vez de converter metadados para fazê-los passar.

## Registros e análise

O JSONL contém metadata, sequência contínua, eventos com ticks QPC em string
inteira e footer. Contém caminhos e ponteiros locais; mantenha-o ignorado em
`scratch/`, sem publicar o trace bruto.

| Evento | Campos observados |
|---|---|
| `start` | Objeto, categoria, centro, scratch width/height, tabela cosseno de 512 inteiros, frequência QPC |
| `update_enter/leave` | Contador e ativo antes/depois de `00450590` |
| `draw_enter/leave` | Estado natural em `004505D0` |
| `transform` | Oito argumentos cdecl em `00422C00`, metadados fonte/destino |
| `polygon` | Opacidade, UVs e vértices efetivos em `00422E30` |
| `blit` | Superfície central e argumentos de `0041AB90` |
| `complete` | Desativação observada após update |

O footer registra contagem emitida, overflow, erros de leitura/host, resultado do
desarmamento e hashes dos arquivos antes/depois. O analisador exige entrada completa,
17 pares de updates 0→16 … 256→272, eventos correlacionados por objeto/thread,
ordem dos três passes, dois transformados, geometria/alpha/fontes esperados.
Essa expectativa vem da análise estática; uma divergência deve ser investigada,
nunca escondida ajustando a captura.

```powershell
python tools\visual\capture\analyze_activation.py scratch\visual\activation-windows\activation-NOME-REAL.jsonl --output scratch\visual\activation-windows\replay.json
```

O replay contém intervalos entre updates em segundos obtidos de **QPC observado**,
mínimo/mediana/máximo e todos os draws, preservando contadores repetidos. Nenhum
FPS é preenchido por padrão. São intervalos **instrumentados**, sujeitos ao custo
dos interceptadores, sends, escrita do host e ao escalonamento do Windows.
Uma única captura não prova que a cadência nominal seja constante.

## Referência visual

O coletor **não tira screenshots dentro dos hooks**: isso alteraria fortemente o
trabalho medido. Registre a apresentação original separadamente, usando a ferramenta
de captura já disponível no Windows; mantenha o tamanho cliente original, sem escala.
Guarde uma imagem do fundo imediatamente antes do efeito e quadros do efeito.
Não há sincronização automática entre vídeo externo e QPC; associe um quadro ao
contador usando a geometria/ordem observada e registre a incerteza de alinhamento.
Se o vídeo não discriminar quadros, não alegue paridade temporal/pixel a pixel.

A ferramenta de comparação aceita fundo e referência 800×600 e oferece diferença
RGB por região. [Comandos do visualizador](../../../tools/visual/ActivationViewer/README.md).
Critério de fechamento: captura íntegra real, geometrias iguais, dimensões reais,
alinhamento de quadro verificável e diferença de pixels explicada ou zerada em
mais de um estágio (crescimento, pico e contração). Até isso ocorrer, fidelidade
visual permanece **não validada**.
