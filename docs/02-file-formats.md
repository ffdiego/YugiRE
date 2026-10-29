# Estruturas iniciais dos arquivos

Evidências: [hashes e primeiros 256 bytes](evidence/initial-inspection.json), [amostras adicionais](evidence/archive-samples.json). Todos os offsets abaixo são de arquivo. Não foi realizada extração de conteúdo em massa; leitura integral dos arquivos grandes ocorreu apenas para SHA256.

## data.dat e Voice.dat

| Campo observado | data.dat | Voice.dat |
|---|---|---|
| Tamanho | 318.892.812 | 145.041.724 |
| 0x00–0x07 ASCII | KCEJYUGI | KCEJYUGI |
| 0x08–0x0B, uint32 LE candidato | 3568 (0xDF0) | 132 (0x84) |
| 0x0C em diante | bytes não ASCII, depois zeros | bytes não ASCII, depois zeros |

### Layout confirmado na segunda rodada

Confiança alta: assinatura de oito bytes seguida por contador uint32 little endian em 0x08; índice começa em 0x0C e contém `count` registros de **0x10C (268) bytes**. A hipótese inicial de 0x110 foi refutada pelo código e pela contabilidade dos arquivos.

| Offset dentro do registro | Tamanho | Significado |
|---|---:|---|
| 0x000 | 256 | Nome terminado em zero, com nibbles trocados |
| 0x100 | 4 | Offset absoluto do conteúdo no arquivo, uint32 LE |
| 0x104 | 4 | Tamanho em memória após transformação, uint32 LE |
| 0x108 | 4 | Tamanho armazenado, uint32 LE |

`FUN_005428D0` lê assinatura e contador, aloca/lê `count * 0x10C` bytes, percorre registros com esse passo, chama `FUN_004A5EA0`, procura nomes por busca binária e transfere os campos 0x100/0x104/0x108 ao objeto de leitura. O offset é usado em chamada de seek absoluto. Evidência: [leitor](evidence/archive-reader-ghidra.txt).

`FUN_004A5EA0` troca nibbles em cada byte até zero: `decoded = (encoded >> 4) | ((encoded & 15) << 4)`. A transformação é reversível, não uma cifra forte. Xrefs de chamada: 0x00542E1A e 0x004A55F6. [Helper](evidence/archive-reader-helpers.txt).

`FUN_00543290` aloca `size1 + 1` bytes. Se size1 == size2, lê diretamente; senão lê size2 bytes em buffer temporário e chama `FUN_0054A000` com tamanho de saída size1. Interpretação alta para os dois tamanhos; a classificação e os parâmetros exatos do descompressor ainda precisam de inspeção, pois o pseudocódigo dessa função apresenta problemas de recuperação de pilha. Há também um caminho posterior que reconhece `YPK1` e transforma bytes por troca de nibbles e XOR 0xCC; não foi verificado numa amostra nesta rodada. [Carregamento](evidence/archive-resource-load.txt).

### Validação física e amostras

| Arquivo | Entradas | Fim do índice = primeiro conteúdo | Último conteúdo termina em | Tamanhos diferentes |
|---|---:|---:|---:|---:|
| data.dat | 3568 | 956236 (0xE974C) | 318892812 = EOF | 205 |
| Voice.dat | 132 | 35388 (0x8A3C) | 145041724 = EOF | 0 |

Todos os registros dos dois índices foram inspecionados, sem extrair payloads: limites válidos e contiguidade exata em todos os registros. Voice.dat contém uma entrada vazia; data.dat nenhuma. Evidência: [validação](evidence/archive-index-validation.json), [índice data.dat](evidence/data.dat-index.json), [índice Voice.dat](evidence/Voice.dat-index.json).

| Arquivo/índice | Nome decodificado | Offset | Em memória | Armazenado |
|---|---|---:|---:|---:|
| data.dat / 0 | bin#\\card_desceng.bin | 956236 | 125132 | 49690 |
| data.dat / 1 | bin#\\card_descfra.bin | 1005926 | 136832 | 54998 |
| Voice.dat / 0 | joey\\english\\dummy.txt | 35388 | 0 | 0 |
| Voice.dat / 1 | joey\\english\\internal\\attack.wav | 35388 | 124550 | 124550 |

Foram lidos até 32 bytes por entrada nas duas primeiras entradas. A entrada dummy é vazia: bytes no seu offset pertencem à entrada seguinte, não a dummy.txt. O relatório preliminar `archive-two-records.json` leu bytes nesse endereço sem limitar pelo tamanho; a evidência corrigida e autoritativa é `archive-index-validation.json`, com amostra vazia para dummy.

attack.wav começa com RIFF/WAVE; o chunk RIFF declara 124550 bytes incluindo o cabeçalho de oito bytes, coincidindo com o índice. A amostra de card_desceng contém texto intercalado com bytes de controle; não tratar como texto descomprimido. A estrutura interna de descrições/cartas e os arquivos gráficos YGA ainda não foram analisados.

## Atualização: compressão LZSS confirmada

Confiança **alta** para a variante usada por `FUN_0054A000` (VA 0x0054A000), com evidência de [pseudocódigo e callers](evidence/lzss-ghidra.txt) e [teste real controlado](evidence/lzss-small-validation.json). Esta atualização resolve a hipótese de compressão acima; não valida todos os formatos de payload.

- Dicionário circular de 4096 bytes, inicialmente zerado, posição de escrita 0xFEE.
- Flags: deslocar à direita; recarregar com `0xFF00 | próximo_byte` quando bit 0x100 acaba. Processar do bit menos significativo ao mais significativo.
- Bit 1: um byte literal, copiado à saída e ao dicionário.
- Bit 0: dois bytes `a,b`; posição absoluta no dicionário `a | ((b & 0xF0) << 4)`, comprimento `(b & 0x0F) + 3`, de 3 a 18 bytes. Leitura/escrita avançam com máscara 0xFFF, permitindo referências sobrepostas.
- Parada pelo tamanho de saída esperado, não por marcador de fim. A função retorna assim que completa a saída, inclusive no meio de uma referência. Não recebe tamanho de entrada no pseudocódigo recuperado.
- O contexto `this` guarda o dicionário a partir de +4. Há opção de alocar saída; o parâmetro de saída é indireto. Tipos/signatura recuperados são provisórios; não foram usados para chamar código original.

O caminho de `FUN_00543290` chama o descompressor em VA **0x00543404**. Outro caller identificado em **0x004974D7**, dentro de 0x00497360, ainda não investigado. A estrutura de flags, posição inicial e referências concordam com o [Unpacker externo](https://github.com/bitemydusto/YGO-PoC-Toolset/blob/main/src/Unpacker/Program.cs); a conclusão local usa também o alvo e o teste, não apenas essa referência.

### Experimento único

Entrada 1368 de data.dat: `j\\deck_c\\line.txt`, offset absoluto **197189114**, tamanho armazenado **23**, tamanho em memória **34**. Leitura do payload limitada a exatamente 23 bytes; índice lido apenas para localizar a entrada. Sem análise dinâmica ou execução de ferramenta externa.

Entrada comprimida (hex): `DF6C696E652FEEF15F78BF2E626D700D0AEEF77900F9F3`.

Resultado: `line/line_x.bmp\r\nline/line_y.bmp\r\n`, com CRLF e sem terminador adicional. Não há prefixo YPK1 nesta amostra. Não confundir o NUL extra alocado pelo carregador com o conteúdo declarado no índice.

Consumidos **23/23** bytes, produzidos **34/34**, nenhum byte residual; 14 literais e 3 referências produzindo 20 bytes. SHA256 da saída: `AC15544AFBF72F8E5F00EE3CC91DE91383B0079266066E3FF01AC8DFD3723D5F`. Artefato: [34 bytes descomprimidos](evidence/lzss-line-output.bin).

SHA256 de data.dat antes/depois: `7E8841CEB015861DACF898D4A62A909E399DA14BB7D69C40DE3AFA3A4610E1ED`. Nenhuma extração em massa.

O decoder próprio rejeita entrada truncada e limita alocação a 16 MiB; essas são salvaguardas da ferramenta, não comportamentos atribuídos ao jogo. A atualização do dicionário após o último byte na ferramenta não é observável, porque o jogo retorna antes e o contexto é reinicializado em cada chamada.

## CARD_Prop.bin — quarta rodada

Entrada 22 de data.dat, nome `bin#\\card_prop.bin`, offset absoluto **1415235**, armazenado **3872** bytes, saída **4460** bytes. LZSS consumiu exatamente 3872/produziu 4460, sem sobra e sem prefixo YPK1. São **1115 palavras uint32 little endian**, sem cabeçalho identificado. A palavra de índice zero é zero. Não concluir que 1115 é a quantidade de cartas jogáveis.

SHA256 do conteúdo descomprimido, mantido em memória: `1A772642A04D86A2CFCEBBA2D0F4FF40A1737761296D0EA6A2645F5ED2BF0B9E`. Apenas estatísticas, os primeiros 64 bytes e 16 registros foram exportados no [relatório](evidence/card-property-validation.json); não foi salvo o recurso inteiro.

A função **0x004025F0** inicializa o recurso sob demanda via **0x00402340**, referenciando `data/bin#/CARD_Prop.bin` em VA **0x005EB540**, xref **0x004025FE**. Obtém o buffer via 0x00542220 e consulta `uint32[(argumento & 0x7FF)]`. Evidência: [loader e acessores](evidence/card-resource-loader.txt), [campos](evidence/card-property-consumers.txt).

| Bits da palavra | Extração base | Consumidor VA | Interpretação/confiança |
|---|---|---|---|
| 0–8 | `(word & 0x1FF) * 10` | 0x00402800 | DEF, alta |
| 9–17 | `((word >> 9) & 0x1FF) * 10` | 0x004027B0 | ATK, alta |
| 18–19 | `(word >> 18) & 3` | 0x00402740 | Categoria ainda sem rótulo, alta para máscara |
| 20–24 | `(word >> 20) & 31` | 0x004025D0 | Discriminante de classe/tipo, média para semântica |
| 25–28 | `(word >> 25) & 15` | 0x004026C0 | Nível candidato, média/baixa; máscara alta |
| 29–31 | `word >> 29` | 0x00402650 | Atributo candidato, média/baixa; máscara alta |

As funções retornam valores derivados, não apenas os bits: para classe 21/22/23, ATK/DEF retornam zero; classe 24 retorna 4000; demais classes usam a escala 10. 0x004026C0 retorna zero para classes 21–23 e 10 para 24; 0x00402650 retorna 9 para classe 21 e 8 para 22. Não nomear esses códigos de classe como magia/armadilha/monstro sem confirmação adicional.

0x00402700, apenas para classes 21/22, extrai bits 17–19; portanto a disposição dos bits tem interpretação dependente da classe, sobrepondo o campo numérico geral. 0x00402740 também usa classes especiais e consulta CARD_ID para os valores 0x776–0x778. Não tratar cada campo como universal para toda carta.

### Evidência de ATK/DEF

0x0041B3E0 monta texto para a interface com string VA **0x005EB914**, `ATK/%4d DEF/%4d`. Na [sequência de instruções](evidence/card-property-arguments.txt), primeiro chama DEF em 0x0041B47F e empilha o resultado; depois ATK em 0x0041B48E e empilha o resultado antes da string. A ordem dos argumentos na pilha confirma ATK primeiro no formato e DEF segundo. O [pseudocódigo da interface](evidence/card-property-ui.txt) recupera a chamada variádica de forma ambígua; as instruções resolvem a dúvida.

Exemplo índice 1: palavra `0xA83064C8`, ATK **500**, DEF **2000**, classe 3, campo 25–28 = 4 e campo 29–31 = 5. A identidade dessa carta não foi estabelecida.

### Limites e identificadores

CARD_ID e CARD_IntID foram identificados por strings e acessores, mas seus payloads não foram extraídos nesta rodada. 0x004022E0 consulta uint16 de CARD_ID para argumento menor que **0x45C (1116)** e retorna 0xFFFF fora dessa condição. 0x00402460 consulta CARD_IntID em `(argumento % 2000) * 2`, acrescentando 1 quando argumento >= 2000, se argumento < 4000. São domínios de identificação distintos; a direção e a identidade dos IDs ainda precisam de comparação das tabelas.

O acesso a CARD_Prop mascara 11 bits, podendo representar 2048 índices, mas o recurso tem só 1115 registros; não há limite de 1115 explícito nesse acessor. Isso não prova um bug no jogo: os chamadores podem restringir os argumentos. A diferença entre o limite 1116 de CARD_ID e o tamanho físico observado também permanece aberta. Não sondar índices inválidos por execução.

## CARD_ID.bin e CARD_IntID.bin — quinta rodada

Evidências locais: [validação das tabelas](evidence/card-id-validation.json), [instruções dos acessores e dois consumidores](evidence/card-id-instructions.txt). Confiança alta nos layouts e relações numéricas abaixo; nomes de cartas e associação a números oficiais continuam desconhecidos.

| Recurso | Entrada DAT | Offset absoluto | Armazenado | Em memória | Elementos uint16 LE |
|---|---:|---:|---:|---:|---:|
| bin#\\card_id.bin | 6 | 1273896 | 2230 | 2230 | 1115 |
| bin#\\card_intid.bin | 13 | 1301435 | 2750 | 4096 | 2048 |

CARD_ID foi lido diretamente; CARD_IntID foi descomprimido por LZSS, consumindo todos os 2750 bytes e produzindo os 4096 declarados. Nenhum dos dois tem prefixo YPK1; não há cabeçalho interno antes dos elementos consultados pelos acessores. Os recursos completos ficaram apenas em memória; foram exportados hashes, estatísticas e amostras.

### Dois domínios e conversão

Adotamos nomes documentais: **índice de propriedades** para `i` e **ID de recurso** para `d`. Este último rótulo não significa número oficial, senha da carta nem necessariamente formato do deck.

`F(i) = CARD_ID[i]`, consultado em VA **0x004022E0**. `R(d) = CARD_IntID[d % 2000] + (d >= 2000 ? 1 : 0)`, consultado em VA **0x00402460** para `d < 4000`. As instruções mostram leitura de palavras uint16 e retorno significativo em AX. Fora da faixa, R retorna AX=0; os bits altos de EAX não são contrato verificado. F retorna AX=0xFFFF fora da condição de limite descrita adiante.

- CARD_ID contém 1115 valores todos distintos. Índice 0 → **0xFFFF**, sentinela. Índices **1–1114** têm IDs válidos abaixo de 4000 e todos satisfazem **R(F(i)) = i**.
- Há 1105 IDs base abaixo de 2000 e nove IDs acrescidos de 2000. Os 1105 elementos não zero de CARD_IntID entre posições 0–1999 satisfazem **F(CARD_IntID[d]) = d**.
- As outras 895 posições de CARD_IntID entre 0–1999 são zero; as posições 2000–2047 também são zero e não são alcançadas pelo módulo 2000.
- **ID zero é válido:** F(816)=0 e R(0)=816. Ele não equivale ao índice sentinela zero. F(817)=2000 e R(2000)=817.
- Exemplo comum: F(1)=1504, R(1504)=1; F(2)=614, R(614)=2; F(3)=50, R(50)=3.

### Nove pares acrescidos de 2000

| ID base | Índice base | ID acrescido | Índice acrescido |
|---:|---:|---:|---:|
| 37 | 31 | 2037 | 32 |
| 40 | 97 | 2040 | 98 |
| 68 | 327 | 2068 | 328 |
| 63 | 525 | 2063 | 526 |
| 389 | 574 | 2389 | 575 |
| 34 | 803 | 2034 | 804 |
| 387 | 809 | 2387 | 810 |
| 0 | 816 | 2000 | 817 |
| 14 | 858 | 2014 | 859 |

Todos os pares apontam para índices adjacentes, como prevê a soma de 1 no acessor. Isso não comprova arte alternativa, estado de efeito, fusão ou outra semântica. Identificar os nomes e comparar conteúdo dos pares é uma investigação futura.

### Limites e sentinelas

A comparação em **0x00402315** é `CMP SI,0x45B`, seguida por `JBE` em **0x0040231A**. Portanto F aceita **0–1115**; contudo, o arquivo contém somente **0–1114**. O limite do pseudocódigo anterior (`<1116`) estava matematicamente correto; as instruções confirmam a diferença com o arquivo físico. Não se executou o índice 1115 e não foi classificada essa diferença como falha explorável.

R compara SI com 4000 em **0x00402495**, retorna AX=0 se fora da faixa em **0x0040249C**, usa divisor 2000 em **0x004024B5** e soma o indicador de segundo intervalo em **0x004024C6–0x004024C9**. Conversão não é validação de associação: uma posição ausente com valor zero vira índice 1 se usada com +2000. Só os IDs presentes em CARD_ID foram comprovados como associações válidas.

A máscara 0x7FF de CARD_Prop é maior que sua quantidade física de registros, mas todos os **1114 IDs presentes** retornam índices dentro do tamanho de propriedades conhecido. Isso esclarece o domínio válido observado, sem explicar todos os argumentos possíveis dos chamadores.

SHA256 de CARD_ID: `A36CDE2192FA85D9CCBCDB0DDB8BA8494A5199EA31D11A50A70B5FD32BF5879C`; CARD_IntID descomprimido: `CF0E1E662B71D3EFD1644F59C94A262281DE4B9A7B212D8F94996208BDAC6E09`.

## deck.ydc

### Layout confirmado — sexta rodada

Evidências: [entrada e referências ao nome do arquivo](evidence/deck-name-entrypoints.txt), [caminho de leitura](evidence/deck-payload-reader.txt), [serializador de listas](evidence/deck-id-codec.txt), [amostra interpretada](evidence/deck-name-validation.json). Endereços das funções são VA; offsets abaixo são de arquivo.

O caminho 0x0044AA00 → 0x0044A920 → 0x0044A550 lê oito bytes de cabeçalho e depois chama **0x00449EF0**. No ramo de leitura, essa função lê três listas de contagem uint16 LE seguida de IDs uint16 LE, convertendo cada ID por **0x00402460** antes de gravar o índice na estrutura em memória. No ramo de escrita, usa **0x004022E0** para converter os índices em IDs. Isso confirma com confiança alta que **deck.ydc armazena IDs de recurso**, e não índices de CARD_Prop.

| Offset na amostra | Tamanho | Conteúdo |
|---|---:|---|
| 0x00 | 8 | Cabeçalho `01 00 00 00 91 B6 B1 54` |
| 0x08 | 2 | Quantidade lista 0: 42 |
| 0x0A | 84 | 42 IDs de recurso |
| 0x5E | 2 | Quantidade lista 1: 1 |
| 0x60 | 2 | ID 812 → índice 651 → Alligator's Sword Dragon |
| 0x62 | 2 | Quantidade lista 2: 0 |

Todos os 100 bytes são consumidos por esse layout; os 43 IDs têm roundtrip válido nas tabelas. Primeiras entradas da lista 0: ID 528 → índice 823 → `Flame Cerebrus` (grafia do arquivo), ID 722 → índice 459 → `Slot Machine`, ID 531 → índice 550 → `Gemini Elf`, repetido duas vezes.

Capacidades observadas em 0x00449EF0: 80 para lista 0 e 15 para cada outra. Em memória, relativamente ao destino da função, IDs convertidos da lista 0 começam em +0x00 e contador em +0xDC; lista 1 começa em +0xBE e contador em +0xE0; lista 2 começa em +0xA0 e contador em +0xDE. Portanto a ordem das listas no arquivo difere de sua ordem física no objeto. O layout interno ocupa ao menos 0xE2 bytes.

0x0044AA00 exige contador principal entre 40 e 80 e os dois outros até 15. Lista 0 como deck principal tem confiança alta pelo contexto e limites. Lista 1 como fusões é hipótese média, coerente com a única carta desta amostra; papel da lista 2 não foi confirmado. Não impor nomenclatura moderna de extra deck/side deck por analogia.

0x0044A550 pode validar o cabeçalho por **0x0045CD20**, que compara somente o primeiro byte e o DWORD em +4 com valores do objeto chamador. A origem/semântica desses valores não foi rastreada; bytes 1–3 também não foram explicados. Não rotular +4 como checksum nem data. O parser auxiliar preserva o cabeçalho sem atribuir semântica e rejeita limites/truncamento em vez de reproduzir os clamps do jogo.

### Nomes de cartas

`bin#\\card_nameeng.bin`, entrada DAT 14, offset 1304185: **17402 → 71360 bytes**, LZSS consumido integralmente, sem YPK1. Contém **1115 registros de 64 bytes**, com strings terminadas em zero. A amostra inglesa usa bytes ASCII nos nomes exportados; isso não especifica codificação de todos os idiomas.

**0x00402290** retorna o buffer do objeto global **0x005F25A8** acrescido de `(argumento & 0xFFFF) * 64`. O carregamento por tabela de caminhos é observado em 0x00401F77, usando a tabela VA 0x005EB444, cuja primeira entrada aponta para `data/bin#/CARD_Name` em VA 0x005EB4C0. [Contexto do acessor](evidence/deck-reader-name-context.txt), [carga](evidence/deck-payload-reader.txt) e [inicialização](evidence/card-name-loader-prologue.txt).

| Índice | ID | Nome em inglês |
|---:|---:|---|
| 1 | 1504 | Earthbound Spirit |
| 2 | 614 | Armored Starfish |
| 3 | 50 | Armored Lizard |
| 31 / 32 | 37 / 2037 | Gaia The Fierce Knight em ambos |
| 97 / 98 | 40 / 2040 | Celtic Guardian em ambos |
| 816 / 817 | 0 / 2000 | Blue-Eyes White Dragon em ambos |

Nomes iguais nesses três pares reforçam a hipótese de variantes da mesma carta, mas não explicam a diferença entre elas. Não foi comprovado que são artes alternativas.

IDs constantes dos dois consumidores da rodada anterior: **762 (0x2FA) → índice 197 → Parasite Paracide**; **1091 (0x443) → índice 703 → Graverobber**. A associação dos nomes tem confiança alta; os comportamentos dessas funções continuam pendentes.

SHA256 dos nomes descomprimidos: `74848D0B2CD7D5FAC7A767075BD5B01E0366183CE934AFA6070CE24394A24032`. Os recursos foram mantidos em memória; somente metadados, exemplos de nomes e as cartas do deck fornecido foram exportados.

## Region.dat

Um byte `0x11` (alta). A função desse valor permanece desconhecida; o nome não basta para inferir idioma/região. String homônima existe no executável em offset 2012736, VA 0x005EB640. A função 0x0040F510 referencia essa string e, no caminho de retorno zero de 0x00543170, obtém um ponteiro por 0x00542220 e calcula `(primeiro_byte >> 4) % 6`, passado a 0x005BE140. Para 0x11 o resultado seria 1; na ausência desse caminho o valor padrão é 1. Fluxo observado com confiança alta no pseudocódigo; vínculo exato dos wrappers com leitura de arquivo ainda tem confiança média. O significado de 1 não foi determinado. Ver [Ghidra](evidence/ghidra-recon.txt).
