# Seleção de decks da CPU — especificação estática em construção

Alvo Joey, SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços VA, base `0x00400000`. Sem executar o jogo ou alterar originais/saves.
Estado operacional e prioridades: [checkpoint](cpu-deck-selection-checkpoint.md).

## Seleção precede o carregamento

`0x0044BBE0` monta o caminho. Chamadores passam esse caminho à configuração
de cena `0x0044BEE0`. Nas cenas 0/1, ela carrega o deck do jogador com
`0x0044AA00` e então chama `0x0044A920(1, caminho)` em `0x0044C0E3`, retorno
`0x0044C0E8`. O tipo 1 seleciona `0x0044A690`, que lê o YDC no source slot 1
`0x00A54F42`. `0x00569380` transfere depois a composição para o duelo;
`0x00568D80` embaralha. Não confundir esses estágios com a escolha do recurso.

## Algoritmo do caminho — comprovado

`base` é o byte **sem sinal** em `0x00A54DB3`; `pool` é o byte em
`0x00A54DB4`. São campos distintos. A semântica de progressão será fechada
separadamente; não chamar `base` sozinho de quantidade de estrelas universal.

```text
if pool == 0:
    level = clamp(base, 1, 7)
    roll = RNG_0055C140(global_005F25E8)
    number = CPU_TABLE[level * 5 + (roll % 5)]
    return "data/J/file/CPU_" + decimal(number, width=3) + ".ydc"
else:
    level = clamp(base + 2 * pool, 3, 9)
    count = FUL_COUNTS[level]
    slot = count == 0 ? 0 : RNG_0055C140(global_005F25E8) % count
    return "data/J/file/FUL_" + FUL_TABLE[(level - 3) * 7 + slot] + ".ydc"
```

As sete contagens FUL reais são não nulas, logo toda seleção válida consome
um valor do gerador. O módulo é divisão **sem sinal de 32 bits**, conforme
`DIV` em `0x0044BC86` e o ramo CPU. Não há rejeição de resultado repetido
nem pesos adicionais dentro desta função. O módulo introduz o viés residual
usual se a distribuição de entrada for uniforme e sua cardinalidade não
for divisível pela quantidade de opções; uniformidade de execução não foi medida.

## Opções por nível

Tabelas CPU `0x005DB158`, contagens FUL `0x005DB080`, nomes FUL `0x005DB0A8`.
Não interpretar a linha zero CPU nem os índices FUL inferiores a três:
estão fora do domínio usado pelo seletor e incluem dados adjacentes.

| Nível efetivo | CPU, `pool=0` (sufixos) | FUL, `pool!=0` (sufixos) |
|---|---|---|
| 1 | 000,001,002,003,004 | — |
| 2 | 003,004,010,011,012 | — |
| 3 | 010,011,012,013,014 | 0GE1,0UMI,0VAL |
| 4 | 013,014,015,020,021 | 1DEC,1RED,1TOO,1INS,1UIJ,1UMI,1VAL |
| 5 | 020,021,022,023,024 | 1FUS,1GAM,1GE1,1GE2,1HIK,1MIX |
| 6 | 023,024,025,026,027 | 2DEC,2VAL,2TOO,2GE1,2HIK,2INS,2UIJ |
| 7 | 025,026,027,028,029 | 2FUS,2GAM,2GE2,2MIX,2RED,2UMI |
| 8 | — | 3ALL,3DEC,3YAM,3FUS,3TOO,3HND |
| 9 | — | 3ALL,3DEC,3EXO,3FUS,3GRV,3HND |

Há 35 posições CPU e 41 posições FUL, mas **21 e 37 recursos distintos**,
respectivamente. Sobreposição de opções entre níveis é deliberada nas tabelas;
subir de nível não força um deck diferente. Níveis FUL 8 e 9 compartilham quatro
das seis opções; os sufixos não constituem prova dos efeitos/estratégias do deck.

`scripts/extract_cpu_deck_selection.py` confere fingerprints EXE/DAT, lê as
tabelas e valida cada YDC diretamente no arquivo original, sem exportar cartas.
As 58 referências existem, todas sem compressão nessas entradas, todas com
listas válidas e consumo integral do recurso. Lista principal entre 40 e 45
cartas; contagens das demais listas e hashes em
`../evidence/cpu-deck-selection-tables.json`. A natureza completa das listas
adicionais permanece fora do escopo deste passo.

Reprodução:

```sh
python scripts/extract_cpu_deck_selection.py ../passion/joey_pc.exe ../passion/data.dat
python -m unittest discover -s tests -p test_cpu_deck_tables.py
```

A captura descrita de CPU com 43 cartas e perfil de uma estrela é compatível
com `CPU_002.ydc`, a única opção com 43 cartas na linha CPU 1. **Isso é só
compatibilidade por tamanho**, não identificação sem comparar caminho ou composição.

## Perfil e opções — evidências iniciais

`0x005BEDD0` percorre tipos possuídos. Retorna 1 ao encontrar carta elegível
pela máscara acumulada de edições (`0x005BEA60`) e ausente na máscara corrente
(`0x005BEA40`). No Joey, são máscaras 7 e 4. É detecção de posse de carta de
edição anterior; não consulta diretamente a presença dos outros executáveis.

`0x005BEE30`, chamado por `0x004834A0`, inicializa o perfil quando `A54DB1==0`.
Ele escreve dificuldade anterior/atual e depois define o byte de pool pelo
resultado de `0x005BEDD0`. A ordem usa o **valor anterior** de `A54DB4` no cálculo
inicial `1 - 2*pool`, com truncamento para byte; não simplificar antes de fechar
as pré-condições de inicialização. `0x0048AA10` também escreve o pool com um
booleano (`opção == 7`); não confundir auto-habilitação e opção selecionada.

`0x00446DF0` modifica `A54DB3` com base em resultados e sequência consecutiva,
com tabelas de limiares. O gráfico de estrelas em `0x00489490` usa
`A54DB3 + 2*A54DB4` e uma animação transitória em relação a `A54DB2`.
O mapeamento dos gates e sequência está na seção de progressão abaixo.

## Evidências

- `../evidence/cpu-deck-normal-entry.txt`
- `../evidence/cpu-deck-stream-and-callsite.txt`
- `../evidence/cpu-deck-profile-and-rng.txt`
- `../evidence/cpu-deck-pool-progression.txt`
- `../evidence/cpu-deck-pool-options-and-result.txt`
- `../evidence/cpu-deck-selection-tables.json`

## RNG da seleção e implementação C#

`src/Joey.Core/CpuDeckSelection.cs` transcreve o seletor, separado do duelo
de demonstração. `Select(base,pool,uint)` reproduz a seleção a partir de um
valor já observado; `SelectNext` consome exatamente uma saída do gerador
fornecido pelo chamador. Isso permite testar o lookup sem fingir que possuímos
o histórico inteiro de um processo original.

`src/Joey.Core/NativeDeckRng.cs` transcreve `0x0055C140`, objeto global em
`0x005F25E8`: 624 palavras de estado, índice em `+0x9C4`, twist com deslocamento
397 e máscara `0x9908B0DF`, tempering shifts 11/7/15/18. Os masks anteriores
aos shifts observados equivalem, em uint32, aos masks MT `9D2C5680/EFC60000`.
Isso é uma instância MT com inicialização antiga, **não** o `NativeRng` CRT.

`0x0040F150`, alcançado pela entrada de inicialização estática `0x005EB03C`
→ `0x0040F140`, configura o vtable e o índice 625. `0x0055C140`, ao encontrar
625, chama `0x0055C0E0(4357)`. A inicialização de cada palavra faz:

```text
high = seed & 0xFFFF0000
seed = uint32(seed * 69069 + 1)
state[i] = high | (seed >> 16)
seed = uint32(seed * 69069 + 1)
```

O primeiro valor de um gerador recém-construído é `2867219139`; isso não
afirma que seja o valor consumido ao iniciar um duelo real. UI, seleção de
cartas e outras rotas também o consomem. Não foi visto reset de RNG dentro
do seletor. O método virtual `0x0040F180` também pode desserializar as 624
palavras mais o índice; seus consumidores indiretos ainda precisam ser fechados.
Portanto a seed padrão explica inicialização limpa, não todos os estados possíveis.

Testes C#: 14 linhas comparadas com JSON extraído do EXE, todos os slots
atingíveis, clamps de bytes, uint máximo, consumo de uma saída e sobreposição
de recursos. Sequências de 1250 saídas para seeds 0,1,4357,FFFFFFFF cruzam
vários twists e conferem com CPython `random.setstate`/`getrandbits(32)`, usando
as palavras de inicialização construídas pela recorrência nativa acima.
O seed do `random.Random` do Python não é usado como equivalência de seeding.
Snapshot/restauração também conferidos. **Não houve execução nativa do Joey**;
esses testes validam a transcrição matemática e as tabelas.

Build da solução passou, 221 verificações C# com DAT/deck local passaram
após a adição dessa unidade. Fixtures dinâmicos opcionais de outras sessões
dependem das variáveis de ambiente e não são presumidos presentes.

Evidência adicional: `cpu-deck-rng-initialization.txt`,
`cpu-deck-rng-vtable-and-progression-gates.txt`, `cpu-deck-rng-seed-method.txt`,
`cpu-deck-result-counters-and-state.txt` em `../evidence/`.

## Estrelas, persistência e progressão

| Campo VA | Papel confirmado |
|---|---|
| `A54DAF` | Classe do resultado anterior: 1 se resultado nativo=1, senão 2. |
| `A54DB0` | Comprimento da sequência da mesma classe; satura em 255. |
| `A54DB1` | Indicador de inicialização do perfil. |
| `A54DB2` | Dificuldade anterior reconhecida pela apresentação. |
| `A54DB3` | Dificuldade base corrente usada pelo seletor. |
| `A54DB4` | Pool booleano; soma dois ao nível efetivo quando habilitado. |

Todos pertencem à imagem de save `0x00A53CC0`, protegida pelo checksum já
descrito em [save e coleção](../27-save-and-collection.md). `0x004834A0`
carrega essa imagem e chama o inicializador. A alteração da dificuldade em
`0x00446FC2` é seguida de escrita via thunk `0x00483490`; não é estado apenas
volátil do menu. Em `0x0048A767..0x0048A773`, a apresentação reconhece a
mudança copiando `DB3` para `DB2` e chama o caminho de persistência.

O desenho `0x00489490` usa `DB3 + 2*DB4`, limitado superiormente a 9;
durante a animação da estrela nova pode desenhar uma a menos. No estado
normal de perfil (base1..7, pool0/1), isso corresponde a estrelas 1..7 no
pool Joey e 3..9 no pool completo. Mudar a opção do pool altera em dois
o nível efetivo sem mudar a base. O seletor faz seus próprios clamps.

`0x005BE160` zera a imagem, incluindo pool e indicador inicializado.
Assim, em um perfil realmente novo, `0x005BEE30` inicialmente cria base=1;
depois a posse de cartas antigas pode habilitar pool=1, resultando em
nível efetivo 3. Não aplicar esse raciocínio a um perfil artificialmente
desinicializado com `pool` anterior não zero: a subtração tem wrap de byte.

### Atualização em `0x00446DF0`

Depois do carregamento do perfil:

1. Cena 3 retorna antes de atualizar perfil (gate `0x0044B980`).
2. Com flag `modeword & 0x40`, não conta resultado nem muda dificuldade;
   apenas zera sequência se a classe anterior era vitória. O escritor
   `0x00408410` zera LP do lado indicado pelo descritor e ORa `0x240` no
   modeword, através do comando **0x40** no dispatcher `0x005B9470`.
   Isso é compatível com desistência/aborto, mas o botão de UI produtor
   ainda precisa ser identificado antes de dar esse nome definitivo.
3. Sem a flag, resultado nativo `0x005BBF60()==1` pertence à classe crescente;
   todos os demais resultados pertencem à outra classe. Incrementa a
   sequência saturada ou reinicia em 1 quando troca de classe.
4. Só avalia mudança de dificuldade se resultado agregado `0x005F3254 != 0`.
5. Vitória: sobe uma base se sequência atinge o limiar e nível efetivo está
   abaixo de 7 (pool0) ou 9 (pool1). Não vitória: desce se acima de
   `1+2*pool` e sequência atinge seu limiar. Mudar nível zera a sequência;
   atingir teto/piso sem mudança não a zera.

| Nível efetivo | Sequência para subir | Sequência não vitória para descer |
|---|---:|---:|
| 1 | 3 | — |
| 2 | 4 | 3 |
| 3 | 5 | 4 (pool0); piso no pool1 |
| 4 | 5 | 4 |
| 5 | 5 | 4 |
| 6 | 6 | 4 |
| 7 | 6 (pool1); teto no pool0 | 3 |
| 8 | 7 | 2 |
| 9 | teto | 2 |

Tabelas em `0x005DAF40` e `0x005DAF64`; os índices não usados não devem
ser tratados como limiares válidos. Resultado 3 também entra na classe
não vitória: o código não contém exceção para empate nessa parte.
`0x005BC050` produz códigos em bits 6–7 de `A577FA`: 1 favorece o lado
`modeword&1`, 2 o outro, 3 pode representar empate; no normal source0 é
o deck do jogador. A transcrição C# aceita o **código nativo bruto**.

`0x0044BAF0` só agrega resultados nas cenas 0,1,3. Na cena0, grava o
resultado diretamente em `5F3254`. Na cena1 (match), conta resultados1/2;
diferença ±2 encerra antes de três duelos; com três duelos, diferença±1
ou zero também encerra. Um match ainda não concluído conserva zero,
adiando a alteração da base, embora a sequência do perfil já seja atualizada
por duelo. Empates permanecem na sequência não vitória nesse caminho.

`CpuDifficultyProgression.cs` transcreve apenas esse trecho sobre um perfil
já carregado, sem simular I/O ou contadores estatísticos. Limita entradas aos
estados entendidos (base0..7, pool0/1, resultado0..3). Os testes exercitam todos
os limiares válidos, troca de classe, empate, match incompleto, flag0x40,
modo3, saturação e pisos/tetos. Build e **255 verificações C#** passaram.

### Consulta direcionada de escritores

`QueryArchive.java` agora aceita `writers:VA`, que retorna somente referências
classificadas como escrita/RMW, com instrução e função. É uma observação de
endereços exatos; não prova ausência de escrita por alias ou ponteiro.
O relatório `cpu-deck-modeword-writers.txt` encontrou 13 escritores do modeword,
dois da base e dois do pool. Isso isolou o caminho especial 0x40 sem repetir
decompilações de todos os leitores do estado de duelo.

Evidências desta unidade: `cpu-deck-modeword-writers.txt`,
`cpu-deck-normal-result-context.txt`, `cpu-deck-resource-backend.txt`,
`cpu-deck-resource-open-and-state.txt`, `cpu-deck-surrender-and-resource.txt`.

## Corroboração com capturas existentes e composição dos pools

`CpuDeckSelectionTests.RunWithArchive` lê as 58 referências do DAT original,
resolve todos os IDs principais e confirma que todos pertencem ao catálogo
jogável. Nas 21 composições CPU, **nenhuma carta principal está fora do pack4**.
Nas 37 composições FUL, somam-se **401 cópias principais** que não pertencem
ao pack4. Isso corrobora concretamente a interpretação dos dois pools.
As contagens somam recursos distintos, não frequências de sorteio.

Os presets observados anteriores são comparados com os recursos por índice,
na ordem original, sem supor que tamanho do deck determine a identidade:

| Captura | Recurso exato | Contagens no arquivo YDC (ordem física) |
|---|---|---|
| Demo A | `j\file\ful_0ge1.ydc` | 40 / 9 / 0 |
| Demo B | `j\file\ful_0val.ydc` | 43 / 3 / 6 |
| Demo C | `j\file\ful_0umi.ydc` | 41 / 3 / 6 |

As três listas de cada captura conferem. O leitor `0x00449EF0` troca a
ordem das listas adicionais ao preencher o source: arquivo `[main,list1,list2]`
vira `[main,second=list2,third=list1]`, isto é, list1 em `+0xBE`, count `+0xE0`,
e list2 em `+0xA0`, count `+0xDE`. Não dar nomes modernos às listas só por isso.
A documentação anterior preservava as listas físicas sem atribuir papéis;
a comparação dinâmica torna explícita essa correspondência de offsets.

Esses três recursos constituem exatamente a linha FUL3. Os 20 duelos da
captura anterior são, portanto, compatíveis com seleção restrita a essa linha,
e não com um catálogo global de apenas três decks. Não recuperamos os bytes
brutos de perfil/RNG daquela captura só a partir das composições.

Os testes opcionais contra DAT usam os mesmos presets já versionados em
`native_rules/decks/`; não exportam recursos originais nem alteram essas capturas.
Com DAT e deck local, **341 verificações C#** passaram após essa correlação.

## Resolução do recurso e condições que não devem ser confundidas

O nome retornado começa com `data/J/file/`. A entrada correspondente no DAT
é `j\file\...`; o resolvedor nativo trata caminhos e arquivos/archives, não um
array C# de composições embutidas. A cadeia do loader é `0055DA10 → 00543BE0`
→ virtual `00543170` → `00543290` → `005428D0`.

`005428D0` tenta abrir o caminho via CRT (`005C5AEB`) antes do ramo de procura
em contêineres. Portanto um recurso externo ou uma configuração distinta de
busca pode alterar **os bytes carregados pelo mesmo nome** sem alterar o
algoritmo do seletor. A relação de prioridade completa de todos os mounts
e overlays não foi reimplementada nesta investigação. Os 58 recursos aqui
conferidos são os do DAT com fingerprint fixado, em instalação original.

`0044A690` não filtra a lista principal por cartas possuídas pelo jogador;
o principal vem da composição do recurso. Após ler o source, pode retirar
entradas das listas adicionais cujo `005BE190` resulte em zero, dependendo
das regras/restrições atuais. Isso não sorteia outra composição principal.

Nos rematches, cena2 em `0044BEE0` muda para cena1, atualiza estado/tempo e
continua sem chamar novamente o seletor nem o loader CPU. Logo as rodadas
de um mesmo match reutilizam a composição, ainda que o shuffle mude a ordem.
Novos duelos/matches iniciados pelos callers `0048AD4B`/`0048AE05` sorteiam
novamente, antes de configurar cenas0/1 em `0048AD6C`/`0048AE28`.

No Demo7 há **duas escolhas**: o chamador externo escolhe o caminho que será
usado no segundo carregamento; dentro da configuração, `0044BF4B` chama o
seletor novamente para o primeiro carregamento (`0044BF75`), copia source1
para source0 e carrega o caminho externo em `0044BFD8`. A ordem de sorteio
é, portanto, inversa à ordem dos dois carregamentos. O segundo parâmetro
da configuração de cena não é usado no primeiro carregamento do Demo.
O mesmo pool/dificuldade alimenta ambos; isso é demonstrado pela cadeia de
chamadas, não pela quantidade de presets observados.

As 58 composições principais também são distintas como **multiconjuntos de
IDs**, não apenas por hash de arquivo. O extrator registra hashes canônicos
dos mains (IDs uint16 ordenados, little-endian), o total distinto e limiares
de progressão lidos dos offsets nativos. Os testes de progressão usam esses
limiares extraídos como oráculo, preservando multiplicidades das cartas.

## Anotações no banco persistente

`scripts/RenameCpuDeckSelection.java` nomeou 12 entradas comprovadas no
projeto `ghidra-projects/Joey.gpr`, sem reimportação ou autoanálise.
Relatório `../evidence/cpu-deck-ghidra-symbols.txt`; log confirmou
`Save succeeded for processed file`. O script protege o fingerprint e preserva
nomes primários manuais preexistentes, criando alias quando necessário.
Nomes incluem a VA original, como `SelectCpuDeckResourcePath_0044BBE0`
e `NextUiDeckMtUInt32_0055C140`.

O banco permanece ignorado pelo Git; o script e o relatório permitem repetir
essas anotações em outra cópia do mesmo programa. Não reaplicar para obter
pseudocódigo: o wrapper de consultas permanece em modo readOnly.
`../evidence/cpu-deck-alternate-scene-calls.txt` confirmou depois o símbolo
persistido e os argumentos de rotas alternativas: rematch2 em `004764EE`,
cena6 em `0048E389`, cena3 em `0042155B` e cena4 em `0048B1FC`.
Não são chamadas substitutas dos novos duelos normais em modos0/1.

## Cobertura das perguntas e limites finais

| Pergunta | Resposta estática / limite |
|---|---|
| Onde fica a dificuldade? | Base em A54DB3, pool em A54DB4, ambos persistidos na imagem de system.dat; UI usa soma com offset do pool. |
| Como vira opções? | Clamp da base/nível efetivo e lookup das tabelas CPU/FUL, conforme matriz acima. |
| Quantos decks por nível? | CPU:5 por linha1..7; FUL:3,7,6,7,6,6,6 nas linhas3..9. Total58 mains distintos. |
| Como escolhe? | Uma saída uint32 de MT compartilhado, módulo do número de opções; não há rejeição de repetidos na função. |
| O que altera a escolha? | Base/progresso, pool, estado MT e novo duelo versus rematch; override de recurso pode alterar o conteúdo carregado. |
| Como reproduzir? | CpuDeckSelection + NativeDeckRng + CpuDifficultyProgression + leitor DeckFile/Archive existentes. |
| O que falta observar? | Mesma captura normal contendo perfil, MT/roll, caminho e source; integrações de progresso/empate e identificação da ação que gera comando40. |

Não instanciar um MT novo por duelo para alegar paridade com o original:
seu objeto é global e outros consumidores avançam a sequência. Uma reprodução
independente pode escolher sua política de RNG, mas essa política deve ser
identificada como tal. Nenhuma das novas classes foi ligada ao demo/IA
automaticamente nesta etapa.

O próximo experimento com poder de refutação está em
[validação Windows](cpu-deck-selection-windows-validation.md). O caminho
normal e as tabelas não dependem de capturar 58 duelos: roll e estado coerentes
numa única chamada permitem confrontar cada elo da previsão. Frequências,
timing do processo e consumidores indiretos do snapshot MT permanecem limites
de observação; não foram convertidos em certezas a partir de decompilação.

### Verificações no fechamento

- Build da solução: passou, sem warnings/erros; 341 verificações C# com DAT
  e deck local passaram. Sem fixtures extras por variável de ambiente.
- Quatro testes Python novos do extrator: passaram; os 58 recursos e
  fingerprints foram conferidos novamente. EXE original intacto.
- Suíte Python geral:104 testes,2 erros de importação,1 skip. Dois testes
  legados ainda apontam para `experiments/hybrid_probe/compare_ydr.py` e
  `experiments/hybrid_probe/pe_preflight.py`, removidos/movidos anteriormente
  na main. Não foram restaurados nem alterados nesta investigação.
- Nenhuma execução do Joey, alteração de save, patch ou novo teste visual.
  Banco Ghidra recebeu apenas as anotações explicitadas acima.
