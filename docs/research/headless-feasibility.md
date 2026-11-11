# Viabilidade de executar o núcleo Joey sem interface

## Conclusão da investigação delimitada

O menor recorte interessante é o **confronto hipotético `005B0F40` com
atributos e serviços de efeitos explícitos**. O grafo estático permite
isolar essa fronteira, mas não fornece um executável independente dela.
Hoje, portar esse recorte para C# é menor e mais verificável que construir
um loader/harness x86 para o código original. Não há evidência de vantagem
suficiente para mudar a prioridade da reimplementação C#.

Ghidra Headless foi executado; **Joey headless não foi executado**. Não
houve Wine, VM, emulador, execução de código do alvo, patch, instalação de
dependências ou alteração do banco persistente. Todos os endereços são VA,
base `00400000`, EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Trabalho anterior auditado

Commit `2c3ee0c03f19152ef2e9aabda837dcf4e016feb0` acrescentou
`CoreBoundaryReach.java`, wrappers Linux/PowerShell e self-test sintético.
O script:

- percorre chamadas diretas e tail calls incondicionais que escapam do corpo;
- resolve imports diretos/thunks/IAT e propaga imports aos chamadores;
- aceita raízes, tabelas explicitamente fornecidas, cortes `stop:`, limite
  `depth:` e busca opcional de ponteiros de função por `addr:follow`;
- cria funções faltantes numa transação revertida; o wrapper usa
  `-process joey_pc.exe -noanalysis -readOnly`;
- preserva chamadas indiretas e fronteiras não expandidas como incerteza.

Isso é uma ferramenta de **dependências estáticas**, não análise de efeitos
colaterais, emulação, extração de uma ABI autocontida ou prova de ausência de
Win32. A classificação `RUNTIME_ONLY` inclui imports de memória/thread/misc;
ela não significa “somente CRT portátil”. Heap/TLS/critical sections também
são serviços do sistema operacional; `misc` exige revisão individual.
`NEEDS_STUB` tem precedência sobre `INDIRECT`/`OPEN`, então uma classe sozinha
não descreve todas as lacunas do nó.

### Limites concretos

1. `PURE` quer dizer “sem imports/indiretos abaixo na cobertura obtida”,
   não “função pura”. `005B08E0`/`005B0950` copiam globais e `0045E240`
   escreve feedback global `005EBEE4`, embora apareçam como `PURE`.
2. Tabelas são sementes explícitas; o script não deduz automaticamente todos
   os alvos de `CALL EAX`, vtables, COM ou callbacks de cartas. `addr:follow`
   pode incluir handlers de exceção/destruição e outros endereços tomados;
   não demonstra que esses callbacks rodem no caminho analisado.
3. `STOP` e `LIMIT` tornam ancestrais `OPEN`, salvo precedência de outras
   classes. Ausência de import num recorte aberto não é prova de isolamento.
4. Há dependência da qualidade/cobertura dos corpos reconhecidos no Ghidra.
   Cortes de função, métodos virtuais ou destinos desconhecidos limitam o grafo.
5. Com várias raízes, profundidade é a menor distância a qualquer raiz, não
   um orçamento independente por raiz. Não comparar contagens como árvores
   de chamadas dinâmicas nem como frequência de execução.
6. O default antigo `005B726C`, descrito no script como scheduler, fica dentro
   do executor `005B6FF0` (termina em `005B7283`, ver a evidência de instruções
   [S4/S5 do estudo da IA](ai-decision-engine.md)). `ensureFunction` aceita a
   função continente. Portanto usar as entradas exatas **`005B7B50` e
   `005BC700`**, como nas consultas abaixo, evita uma raiz default enganosa.
   O script original foi preservado nesta avaliação.

## Self-test: dependência ausente, não validado nesta máquina

`selftest-core-boundary.sh` compila um PE **sintético** via
`i686-w64-mingw32-gcc`/`i686-w64-mingw32-nm`, importa-o num projeto temporário
e verifica imports, indiretos, classificação e descoberta de callback.
Ele não executa esse PE nem o Joey. A exceção ao fingerprint existe só na
cópia temporária do script.

Nesta sessão, ambos os executáveis MinGW não estavam no PATH nem em
`/usr/bin`; `gcc` e `clang` também não foram encontrados. Ghidra12.2/Java25
estavam disponíveis. O self-test foi **não executado por falta de dependência**,
sem instalar compiladores. Isso limita a validação do próprio analisador;
as três consultas do projeto Joey abaixo concluíram normalmente.

Se o compilador já estiver disponível num ambiente posterior:

```bash
GHIDRA_INSTALL_DIR=/home/diego/apps/ghidra_12.2_PUBLIC \
  bash scripts/selftest-core-boundary.sh
```

## Consultas realizadas e resultados observados

| Consulta | Nós | Classes | Fronteiras não resolvidas |
| --- | ---: | --- | --- |
| Oito raízes de combate/atributos, depth5 | 118 | 67 INDIRECT;22 LIMIT;13 OPEN;14 PURE;2 RUNTIME_ONLY | 4 sites indiretos;10 endereços tomados fora do grafo. |
| Update, dispatcher e oito etapas explícitas, depth3 | 409 | 14 INDIRECT;238 LIMIT;139 OPEN;18 PURE | 7 sites indiretos;4 endereços tomados fora do grafo. |
| Confronto com sete serviços cortados, depth3 | 9 | 1 OPEN;7 STOP;1 PURE | Nenhum indireto observado dentro do recorte; serviços deliberadamente abertos. |

Foram criadas zero,67 e zero funções **transitórias**, respectivamente;
nenhuma foi salva no projeto. Relatórios compactos e CSVs:
[avaliadores](../evidence/ai-combat-boundary-evaluators.txt),
[dispatchers](../evidence/ai-combat-boundary-dispatchers.txt),
[fronteira explícita](../evidence/ai-combat-boundary-explicit-services.txt).
Cada `.txt.functions.csv` contém os nós e sua classificação sem decompilação
redundante ou payloads do jogo.

### Dependências efetivamente referenciadas no recorte de avaliadores

As oito raízes `005B0F40`, `005B0D90`, `005B0B90`, `005B0A50`, `005B08B0`,
`005ADD60`, `005ADDC0`, `0056DD30` ficaram `INDIRECT`. O recorte alcançou:

| Import Win32 | Categoria do script | Cadeia estática exemplificativa |
| --- | --- | --- |
| `KERNEL32!HeapFree` | memory | B0F40 → 004022E0 → 00402340 → 0054A2D0 → 005C50AA |
| `KERNEL32!GlobalAlloc` | memory | B0F40 → 004022E0 → 00402340 → 00543170 → 00543290 |
| `KERNEL32!HeapAlloc`, `HeapReAlloc` | memory | B0F40 → 004022E0 → 005C387C → 005C37FE → 005C7124 |
| `KERNEL32!HeapSize` | memory | mesma cadeia até 005C725C |
| `EnterCriticalSection`, `LeaveCriticalSection` | thread | B0F40 → 004022E0 → 00402340 → 0054A2D0 → 00496670/80 |

São referências estáticas a APIs reais do Windows, não chamadas observadas
num confronto. Parte pertence ao suporte C++/alocação inicial, não à fórmula
numérica em si. Não foram alcançados imports USER32/GDI/WINMM neste limite;
**não** concluir que o transitive closure completo nunca os alcança.

O caminho pelo getter de ID não é incidental: a decompilação já preservada
em [card-resource-loader.txt](../evidence/card-resource-loader.txt), função
`004022E0`, testa `005F2478.bit0`, carrega `data/bin#/CARD_ID.bin` por
`00402340` e registra destrutor por `005C387C` no primeiro acesso. Depois
consulta a tabela inicializada. Logo “só resolve um ID” pode incluir
inicialização lazy, arquivo/recurso, heap e lifetime C++ antes do cálculo.
Os quatro calls indiretos deste recorte ficam em `00543290`, por slots
`[EDX+0xC]`/`[EDX+0x44]`. Seu serviço concreto não foi substituído nem
classificado como arquivo/Win32 sem resolver a implementação virtual.

`_strlen`, `_malloc` e `__CxxThrowException@8` aparecem como nós de runtime
ou fronteiras limitadas, não como evidência de uma API gráfica. Código CRT
interno pode, por sua vez, alcançar Win32; o nome CRT não encerra a análise.

### Dispatchers e tabelas

`005BC700` contém `CALL [ECX*4+005EFBB0]`; `005B7B50` contém `CALL EAX`
para o estágio em `005EFB50`. A consulta semeou as oito entradas conhecidas
dessa segunda tabela, pois os calls indiretos isolados não as descobrem.
Outros indiretos do recorte aparecem em `005B5E00`, `0040CA70` e `005A4090`.
Os 238 LIMIT tornam esse recorte uma localização de fronteiras, não uma
lista completa de serviços. Não apareceu import direto dentro de depth3;
essa ausência com tanto código não expandido não autoriza chamar o
escalonador de independente do Windows.

A análise dos dispatchers já existente em
[ai-decision-engine.md](ai-decision-engine.md) continua sendo a fonte para
ordem de estágios, buffers, side ativo e transições. O grafo acrescenta a
cobertura de chamadas e explicita o que não consegue resolver; não executa
uma decisão, não fornece snapshots e não calcula scores.

## Menor fronteira candidata

A terceira consulta deixa `005B0F40` aberto em sete serviços:

| Serviço cortado | Estado/valor que um harness teria de fornecer |
| --- | --- |
| `005ADD60`, `005ADDC0` | Stats próprios e adversários, com política de ocultos e metadados. |
| `004022E0` | Tradução índice interno→ID de recurso já inicializada. |
| `0056D790`, `0056D8D0` | Contagens de equipamentos/vínculos e contexto do lado de origem. |
| `005699A0` | Presença/contagem contextual de efeitos no campo. |
| `0056B220` | Gate global de regras/efeitos consultado no cálculo. |

Só o helper aritmético `005BE050` ficou como `PURE` abaixo do confronto.
Isso **não é** uma extração executável validada: ainda há acessos diretos a
posição do alvo e flags de jogador em endereços absolutos, ABI x86 de stack,
representação/truncamento de palavras e buffers de saída. O harness precisa
mapear ou adaptar esses acessos e oferecer exatamente os sete contratos.
Trocar os serviços por funções vazias/zero falsificaria o confronto.

Subir para `005B0A50` acrescenta os campos completos de cinco monstros por
lado, elegibilidade de ataque, máscara de ações, remoções, contadores,
atributos que mudam após cada remoção e o save/restore global. Subir ao
seletor acrescenta mão, tributos, políticas e escalonamento; ao update inteiro,
filas/UI e dispatchers. O grafo de imports não captura esse custo de estado.

## Harness nativo versus C#

| Opção | Trabalho verificável mínimo | Avaliação atual |
| --- | --- | --- |
| Executar bytes x86 originais com stubs | Mapear código/dados, ABI32, recursos lazy, heap/TLS/C++ lifetime e indiretos; reconstruir snapshots e efeitos colaterais antes de executar. | Maior que o recorte C#; não implementado nem autorizado como atalho sem nova análise de efeitos. |
| Recompilar pseudocódigo C | Corrigir tipos/layout, helpers e runtime, depois validar contra os mesmos eventos. | Mantém quase todas as dependências sem a precisão automática do binário; não há compilador funcional gerado. |
| Portar confronto para C# com contexto explícito | Fórmula, flags, int16 e serviços de efeito tipados; falhar como unsupported quando faltam condições. | Menor fronteira prática e compatível com os testes/corpus existentes. |
| Portar sequência local em C# | Ordenação, filtros, estado hipotético e agregação; atributos por contratos validados. | Progressão recomendada após o confronto, sem globals mutáveis desnecessários. |

Não há um protótipo nativo menor claramente mais vantajoso demonstrado.
A utilidade imediata do trabalho do Claude é orientar as interfaces da
reimplementação: metadata/atributos, efeitos/vínculos e estado hipotético
explícitos. A investigação principal deve continuar no motor C#.

## Comandos exatos desta sessão

Da raiz do repositório; todos executam apenas Ghidra:

```bash
export GHIDRA_INSTALL_DIR=/home/diego/apps/ghidra_12.2_PUBLIC
bash scripts/run-core-boundary.sh ai-combat-boundary-evaluators.txt \
  005B0F40 005B0D90 005B0B90 005B0A50 005B08B0 005ADD60 005ADDC0 0056DD30 depth:5
bash scripts/run-core-boundary.sh ai-combat-boundary-dispatchers.txt \
  005BC700 005B7B50 table:005EFB50:8 depth:3
bash scripts/run-core-boundary.sh ai-combat-boundary-explicit-services.txt \
  005B0F40 stop:005ADD60 stop:005ADDC0 stop:004022E0 stop:0056D790 \
  stop:0056D8D0 stop:005699A0 stop:0056B220 depth:3
```

Nenhum `addr:follow` foi usado para evitar expandir callbacks por mera tomada
de endereço. Nenhuma fronteira aberta foi marcada como funcionalmente isolada.
