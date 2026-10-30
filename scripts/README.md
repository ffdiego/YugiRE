# Reprodução

Tabela de Rituais:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s YugiRE/tests -p 'test_ritual_rules.py'
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/catalog_ritual_rules.py
```

Seis testes sem binários; o gerador imprime dois `PASS`, confere 16 pares,
callbacks e níveis base e exporta `docs/evidence/ritual-rules-catalog.json`.
Ver [layout e alcance](../docs/cards/ritual-bindings.md).

Limpeza dos vínculos de equipamento:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_equipment_cleanup.py`.
Esperado: treze linhas `PASS`; instruções, dispatch de tipos e nove cenários
de transcrição em `docs/evidence/equipment-cleanup-validation.json`. Não executa
o alvo. Ver [análise e limites](../docs/cards/equipment-cleanup.md).

Vínculo e bônus de Legendary Sword:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_legendary_sword.py`.
Esperado: doze linhas `PASS`; identidade, combinação de callbacks, dispatch
de 0xB2 e instruções de +300/+300 em `docs/evidence/legendary-sword-validation.json`.
Não executa o jogo. Ver [análise e pendências](../docs/cards/legendary-sword.md).

Catálogo completo de callbacks:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s YugiRE/tests -p 'test_card_callback_catalog.py'
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/catalog_card_callbacks.py
```

Oito testes sem binários; o gerador exige o alvo conhecido, imprime dois `PASS`
e o resumo (443 registros, 269 combinações, 219 principais distintos).
Exporta `docs/evidence/card-callback-catalog.{json,csv}` sem executar/decompilar
efeitos. Ver [fonte, limites e próximas amostras](../docs/17-card-callback-catalog.md).

Supressão FLIP por Royal Command:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_royal_command.py`.
Esperado: onze linhas `PASS`; identidade, registro, instruções e despacho
de 0xB1 em `docs/evidence/royal-command-validation.json`. Não executa o jogo.
Ver [análise e limites](../docs/cards/royal-command.md).

Classificação e produtor condicional de FLIP:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_flip_activation.py`.
Esperado: dez linhas `PASS`; tabelas/instruções e despacho de 0x80 em
`docs/evidence/flip-activation-validation.json`. Não executa o jogo. Ver
[alcance da validação e plano dinâmico](../docs/cards/man-eater-flip-activation.md).

Custo de descarte de Tribute e callback compartilhado:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_tribute_cost.py`.
Esperado: nove linhas `PASS`; registros, quantidades, seletores e modelo de
elegibilidade em `docs/evidence/tribute-cost-validation.json`. Ver
[análise de custo](../docs/cards/tribute-discard-cost.md).

Destruição e listas de destino:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_monster_destruction.py`.
Esperado: oito linhas `PASS`, preservação dos originais/banco, dispatcher,
identidades e modelo de 64 casos em `docs/evidence/monster-destruction-validation.json`.
Ver [continuação de Man-Eater Bug](../docs/cards/monster-destruction.md).

Seleção de alvo de Man-Eater Bug:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_monster_targets.py`.
Esperado: sete linhas `PASS`, identidades e modelo de revalidação em
`docs/evidence/monster-target-validation.json`. Ver
[análise de seleção](../docs/cards/man-eater-bug-targeting.md).

Comparação Sparks/Ookazi:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_simple_lp_cards.py`.
Esperado: sete linhas `PASS`, com metadados em
`docs/evidence/simple-lp-cards-validation.json`. Ver
[análise comparativa](../docs/cards/sparks-ookazi.md).

Caso consolidado de Parasite Paracide:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_parasite_lifecycle.py
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/inspect_parasite_resources.py
```

Executar da raiz do workspace. O primeiro imprime seis linhas `PASS` e
revalida originais/banco, instruções e modelo aritmético. O segundo valida
quatro recursos limitados em memória e imprime JSON com descrição e amostras;
não salva os payloads completos. A classificação de Insect combina os nomes
locais com a descrição da carta. Ver [caso consolidado](../docs/cards/parasite-paracide.md).

Validação da continuação de Parasite Paracide:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_parasite_continuation.py`
(a partir da raiz do workspace). Esperado: cinco linhas `PASS`. Ver
[análise dos estados](../docs/16-parasite-continuation.md).

## Consultas no Fedora

Com JDK 25 completo e Ghidra 12.2 extraído em
`../../tools/ghidra_12.2_PUBLIC/` (relativamente a este diretório), executar
a partir da raiz do workspace:

```bash
bash YugiRE/scripts/run-archive-query.sh parasite-callers.txt 00570be0 00570420 0057f130 00578860 ins:00570be0:65
```

O wrapper Linux usa o projeto persistente com `-noanalysis -readOnly`.
O seletor `xref:VA` exporta somente referências, sem selecionar as funções
donas para descompilação; `ref:VA` mantém o comportamento anterior.
Define `XDG_CONFIG_HOME` para que o launcher também salve sua seleção de JDK
em `scratch/`; configurações, cache e logs de cada consulta ficam em um
diretório temporário próprio nesse local. `GHIDRA_INSTALL_DIR` permite
escolher outra instalação. O primeiro argumento é um nome de relatório `.txt`
sem diretórios, destinado a `docs/evidence/`; relatórios existentes com esse
nome são substituídos após a consulta ter sucesso. O wrapper verifica erros
no console porque o headless pode retornar zero mesmo após falha de abertura.
Não executa o jogo. Os comandos PowerShell abaixo documentam o fluxo Windows.

Validação estática do comando 0x4D: `python3 YugiRE/scripts/validate_command_4d.py`
(a partir da raiz do workspace). Exige o alvo conhecido e o banco preservado
da rodada anterior; saída esperada `PASS`, metadados em
`docs/evidence/command-4d-validation.json`. Ver
[análise do comando](../docs/11-command-4d.md).

Para a inserção genérica: `python3 YugiRE/scripts/validate_association_insertion.py`.
Revalida a rodada anterior e o despacho/instruções de 0x86, gerando
`docs/evidence/association-insertion-validation.json`. Ver
[inserção e resolução da chave](../docs/12-association-insertion.md).

Para o produtor de Parasite Paracide:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_parasite_creation.py`.
Esperado: três linhas `PASS`. O script valida também o registro de ID 762,
a busca e a chamada indireta; gera `docs/evidence/parasite-creation-validation.json`.
Ver [callback e produtor](../docs/13-parasite-creation.md).

Para o transporte da chave:
`PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_parasite_request.py`.
Esperado: quatro linhas `PASS`, incluindo 65.536 casos de codificação e
a distinção do ramo selecionado por 0x365F. Relatório em
`docs/evidence/parasite-request-validation.json`; análise em
[chave do pedido](../docs/14-parasite-request-key.md).

Executar em PowerShell 7, a partir de `Z:\YugiRE`:

```powershell
.\YugiRE\scripts\Inspect-Initial.ps1
.\YugiRE\scripts\Inspect-Samples.ps1
.\YugiRE\scripts\Run-InitialRecon.ps1
```

Requer Java 25 para Ghidra 12.2. Nenhum jogo é executado. Após autorização do usuário para modificar o banco, `Run-InitialRecon.ps1` cria e salva `ghidra-projects/Joey.gpr` na primeira execução. Nas seguintes, usa `-process joey_pc.exe -noanalysis -readOnly` para consultar a análise salva sem refazê-la. Para futuras alterações persistentes do banco, omitir `-readOnly` no comando correspondente. O script não sobrescreve importações existentes. Se a importação inicial falhar, verificar o log e o conteúdo do projeto antes de reutilizá-lo.

O script Java exporta strings selecionadas, até 20 referências por string e no máximo sete funções descompiladas. Nesta rodada foram duas. A análise automática padrão do Ghidra é mais abrangente que essa seleção; não equivale a inspeção manual de todas as funções.

Os dois scripts PowerShell leem os arquivos em modo somente leitura; a inspeção inicial usa SHA256 integral, strings ASCII do EXE e cabeçalhos. Amostras adicionais: cinco blocos de 96 bytes por arquivo grande. Não há extrator de payload nesta etapa. O parser PE é específico para reconhecimento do PE32 observado e não um parser genérico de arquivos hostis.

Validação realizada em 2026-10-29: tamanhos dos cinco arquivos conferem com os fornecidos; arquitetura/base/hash do PE conferem com Ghidra; headless terminou com `Analysis succeeded`, `Post-analysis succeeded` e `Discarded file import due to readOnly option`. Comparação dos cinco SHA256 ao final em `docs/evidence/preservation-check.json`. Não há testes de extrator porque nenhum foi implementado.

## Índices KCEJYUGI — segunda rodada

```powershell
.\YugiRE\scripts\Run-ArchiveQuery.ps1
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report archive-reader-helpers.txt -Addresses 004a5ea0,00543170,005c59a3,005c5767
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report archive-resource-load.txt -Addresses 00543290,00543760,00543b00
.\YugiRE\tests\Test-ArchiveIndex.ps1
.\YugiRE\scripts\Inspect-ArchiveIndex.ps1
```

QueryArchive usa a análise salva, sem autoanálise; sem endereços procura assinatura e *.dat; com endereços consulta só as funções indicadas. Há limite de 12 funções por exportação. A consulta de 0x00543B00 retornou a função contenedora 0x00543960, cujo corpo não foi usado para conclusões desta rodada. O relatório de helpers originalmente incluiu também funções da busca da assinatura; o script foi ajustado para evitar repetição em consultas posteriores.

ArchiveIndex.psm1 lê somente cabeçalho e índice e verifica assinatura, limites do índice, terminação do nome e faixas de payload. Os nomes ASCII são suficientes para as amostras atuais; codificação de nomes não ASCII ainda não especificada. `size1`/`size2` preservam a ordem dos campos: tamanho em memória e armazenado, respectivamente. Inspect-ArchiveIndex lê no máximo 32 bytes nas duas primeiras entradas, respeitando tamanho zero, e exporta metadados, sem extrair conteúdo.

Testes sintéticos passaram para nome conhecido, campos do registro, assinatura inválida, índice truncado, offset sobre o índice, payload além de EOF e nome sem terminador. Validação real: 3568/132 entradas; todas as faixas válidas, contíguas e terminando exatamente em EOF. Não há descompressor implementado.

## LZSS — terceira rodada

```powershell
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report lzss-ghidra.txt -Addresses 0054a000
.\YugiRE\tests\Test-PocLzss.ps1
.\YugiRE\scripts\Test-SmallArchiveResource.ps1
```

`PocLzss.psm1` implementa o fluxo confirmado em 0x0054A000, com checagem de entrada truncada, alocação limitada a 16 MiB e contabilidade de bytes. Não remove YPK1. Testes sintéticos: literais, referência sobreposta, dicionário zerado, parada no meio de referência, recarga de flags, entrada truncada e bytes residuais. Resultado esperado: linha PASS.

Experimento real fixo: entrada `j\deck_c\line.txt`, no máximo 4096 bytes de entrada/saída por salvaguarda. Esperado: 23 consumidos, 34 produzidos, zero residuais; texto `line/line_x.bmp` e `line/line_y.bmp`, ambos com CRLF. Exporta somente os 34 bytes e relatório em docs/evidence. SHA256 antes/depois de data.dat deve coincidir. Nenhum código do jogo é executado.

## Propriedades de cartas — quarta rodada

```powershell
.\YugiRE\scripts\Inspect-CardProperties.ps1
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report card-resource-loader.txt -Addresses text:data/bin#/CARD_ID.bin,text:data/bin#/CARD_IntID.bin,text:data/bin#/CARD_Prop.bin
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report card-property-consumers.txt -Addresses 004025d0,00402650,004026c0,00402700,00402740,004027b0,00402800,00402340
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report card-property-ui.txt -Addresses 00418b10,0041b3e0
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report card-property-arguments.txt -Addresses ins:0041b470:25
```

Esperado no relatório de propriedades: entrada 22, 3872 consumidos, 4460 produzidos, 1115 palavras, sem YPK1; hash descomprimido `1A772642A04D86A2CFCEBBA2D0F4FF40A1737761296D0EA6A2645F5ED2BF0B9E`. Apenas um payload é lido e descomprimido, com limite de 8192 bytes; exporta somente amostra/estatísticas. Os campos do relatório são nomeados pelos bits, sem presumir a semântica dos não confirmados.

QueryArchive aceita também `text:` para localizar strings e decompilar seus consumidores, `ref:` para consultar referências e `ins:VA:quantidade` para exportar até 100 instruções a partir da instrução contenedora. A consulta mantém limite de 12 funções descompiladas; lista de callers pode ser grande, sem decompilar esses callers.

## Conversões de IDs — quinta rodada

```powershell
.\YugiRE\scripts\Inspect-CardIds.ps1
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report card-id-instructions.txt -Addresses ins:004022e0:38,ins:00402460:45,00402740,00570be0,00570c70
```

O inspetor lê apenas dois payloads pequenos (2230 bytes diretos e 2750 bytes comprimidos → 4096 bytes), mais o índice DAT. Confere tamanho, ausência de YPK1, terminação da descompressão e hash antes/depois do arquivo original. Exporta amostras, hashes e relações numéricas, sem salvar payloads completos.

Validação sem execução do jogo: 1115 palavras em CARD_ID, 2048 em CARD_IntID; 1114 IDs válidos satisfazem retorno ao índice original. Os 1105 elementos não zero das primeiras 2000 posições do reverso retornam ao ID de origem; nove pares com +2000 têm índices adjacentes. O script rejeita inconsistências nessas relações e sentinela inicial diferente de 0xFFFF. Relatório `card-id-validation.json`. São verificações cruzadas das tabelas reais, não apenas exemplos da fórmula. Não implementa parser de deck nem identifica nome de carta.

## Nomes e deck — sexta rodada

```powershell
.\YugiRE\tests\Test-DeckYdc.ps1
.\YugiRE\scripts\Inspect-DeckNames.ps1
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report deck-name-entrypoints.txt -Addresses text:deck.ydc,text:data/bin#/CARD_Name,00402290,004022b0
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report deck-reader-name-context.txt -Addresses 0044aa00,004834a0,ins:00402290:12,ref:005eb444
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report deck-payload-reader.txt -Addresses 0044a550,0044a690,ins:00401f77:90
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report deck-id-codec.txt -Addresses 00449ef0,0045cd20,ins:004021a7:70
.\YugiRE\scripts\Run-ArchiveQuery.ps1 -Report card-name-loader-prologue.txt -Addresses decode:00401f20:24
```

`DeckYdc.psm1` é parser estrutural das três listas observadas, com cabeçalho opaco de 8 bytes. Rejeita truncamento, capacidades acima de 80/15/15 e bytes residuais. Não verifica o significado do cabeçalho, legalidade de cartas ou mínimo 40; não reproduz clamps do original. Testes cobrem ID zero, ID acrescido de 2000, offsets de listas, lista vazia, truncamento, capacidade e bytes adicionais.

O inspetor carrega somente CARD_ID, CARD_IntID e um idioma de CARD_Name (17402 bytes comprimidos → 71360 bytes), mantendo dados em memória. Lê também os 100 bytes do deck e consulta um ponteiro no executável após verificar seu hash. Exporta nomes amostrados e a lista de cartas do deck. Resultado esperado: 1115 registros de 64 bytes, listas 42/1/0 e 43 associações ID→índice→nome com roundtrip.

`decode:VA:quantidade` desassembla estaticamente a partir do endereço em memória do Ghidra; no wrapper atual, alterações são transitórias e descartadas por `-readOnly`. Não executa instruções do alvo. Usado para preencher a lacuna de listing no prologue 0x00401F20. `ins:` agora começa pela próxima instrução definida quando o endereço não contém instrução e registra aviso; relatórios antigos podem simplesmente não conter saída nesse caso.

## Retirada em contexto de batalha — 0x7C

`python3 scripts/validate_battle_command.py` verifica o hash do EXE, o
despacho 0x7C → 0x00405190 e quatro alvos de CALL. Não executa o original.
[Contexto e limites](../docs/18-battle-snapshot-removal.md).

## Modelo estático de seleção de tributos de Ritual

`PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_ritual_selection_model.py'`
compara a transcrição de 0x00599570/parte de 0x00599140 com enumeração
independente de subconjuntos. Quatro testes; não executa código nativo.
[Critério, pagamento e limites](../docs/cards/ritual-bindings.md).
