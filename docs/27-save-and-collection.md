# Save comum, registro e coleção

EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços abaixo são VA, image base `0x00400000`. Investigação estática no
Ghidra existente; nenhum executável original foi executado ou alterado.

## Localização comprovada no Joey

`0x00410B50` procura `CommonDir` na seção `system` sob
`HKLM\SOFTWARE\KONAMI\Yu-Gi-Oh! Power Of Chaos`. O wrapper de abertura
`0x00481FF0` usa `RegCreateKeyExA` com HKEY `0x80000002` (HKLM).
A leitura de strings passa por `0x00482810` / `RegQueryValueExA`.
O caminho formado acrescenta `\system.dat` a esse diretório.

Quando não consegue recuperar o diretório comum, o fluxo consulta
`InstallDirJ` e contém uma alternativa com o nome
`Yu-Gi-Oh! Power of Chaos Common`, gravando `CommonDir`. A composição completa
da alternativa ainda requer conferir o helper que escolhe o diretório base;
não presumir que seja sempre uma subpasta da instalação.

O relato do operador de que há uma pasta Common compartilhada pelos três
jogos é compatível com esse caminho. Não examinamos aqui os executáveis do
Yugi e do Kaiba para comprovar como cada um atualiza o arquivo compartilhado.

## Carregamento e integridade

`0x00483440` encaminha a leitura a `0x004832C0`. Este aplica os filtros de
cabeçalho e de conteúdo e copia `0x462` DWORDs (`0x1188` bytes) para
`0x00A53CC0`. **Esses são offsets da imagem já decodificada em memória,
não offsets diretamente utilizáveis no arquivo.**

O filtro `0x0045CCF0` processa e remove oito bytes de cabeçalho. O valor
binário `system\flcrc`, lido por `0x0045CA80`, contém oito bytes codificados
em 13 bytes no registro e participa desse mecanismo.
`0x00439FF0` verifica o conteúdo com `0x00439EF0` e chama
`0x00439D80` com parâmetros `0x35` e `299`; este extrai grupos de nove bits e
chama `0x00439C00`. A decodificação foi posteriormente validada na amostra
do operador, conforme a seção abaixo.

`0x005BE0C0` exige a palavra `0xFBA5` em `0x00A54E42` e compara a palavra
`0x00A54E44` com o complemento de dois da soma de `0x8C2` palavras iniciada
em `0x00A53CC0`. `0x005BE160` inicializa a imagem e escreve `YUGIOH01` em
`0x00A54E3A`. Falha de carga/integridade leva ao caminho de inicialização;
portanto uma cópia isolada de bytes não deve ser tratada como save válido
sem verificar os filtros e a associação com o cabeçalho.

## Quantas cartas o jogador possui

A tabela em `0x00A53CCC` tem `1115` entradas de 16 bits; o índice zero é
reservado. O **byte inferior** representa a quantidade de cópias. O byte
superior também contém flags e não deve ser contado como quantidade.
`0x005BE830` incrementa a quantidade, preservando os demais bits, e atualiza
o contador de cópias em `0x00A53CCA`.

`0x0047DBB0`, construtor da lista, percorre índices 1–1114. Primeiro conta as
cartas elegíveis pelo filtro de edição; depois conta um tipo possuído para
cada entrada elegível cujo byte inferior seja diferente de zero:

```text
tipos_possuídos = soma((coleção[i] & 0xFF) != 0
                      para i = 1..1114 se (card_pack[i] & máscara) != 0)
```

A máscara corrente do Joey é `4`; a máscara acumulada é `7`. Os totais de
catálogo já identificados, 350 e 771, não são a quantidade desbloqueada de
um save específico. Tipos possuídos também diferem do total de cópias.
Na investigação inicial não havia `system.dat` disponível em `passion/`.
Uma amostra foi depois enviada para `passion/saves/` e examinada abaixo.
A reimplementação continua usando seu catálogo
sem restrição de save, comportamento temporário já adotado para demonstração.

## O SetupReg.exe desta cópia

SHA256 `27267e43bb4e184b17e3c6ae5cd55dd0e3059ad6f448b6faf1ab2f4a26a2fc88`,
7962 bytes. Sua seção `.reg` (file offset `0x1E00`, VA `0x0040D000`)
contém cinco registros. Quatro têm nome e conteúdo vazios nas chaves raiz
e `system`; o quinto define `system\InstallDirJ`, tipo `REG_SZ`, como
`%cd%`. Não há nessa tabela `CommonDir`, `flcrc` nem coleção de cartas.

Isso é corroborado pelo consumidor nativo: `0x0040138C` percorre a tabela,
`0x004013CD` lê o tipo e `0x00401437` chama o gravador `0x00401646`, que
usa `RegCreateKeyExA` e `RegSetValueExA`. A tabela é apontada pelo DWORD em
`0x0040228B`. A evidência sustenta um configurador de instalação; não
identificamos um desbloqueio direto de todas as cartas. A expansão exata de
`%cd%` e todos os outros caminhos do utilitário não foram fechados aqui.

## Evidências, validação e próximo teste

Os relatórios `evidence/save-*.txt` preservam as funções e referências
consultadas. `evidence/setupreg-records.json` registra os cinco valores,
offsets e fingerprint; `evidence/setupreg-disassembly.txt` permite conferir
o consumidor. Reprodução do disassembly: `objdump -d ../passion/SetupReg.exe`
a partir da raiz do repositório.

Foram conferidos limites da seção, os cinco registros REG_SZ, o terminador
final e o ponteiro nativo da tabela. A suíte existente passou com
`python -m unittest discover -s tests` (25 testes). Não foi feita validação
dinâmica no Windows nessa rodada inicial.

O próximo passo útil é comparar uma cópia do `system.dat` da pasta indicada
por `CommonDir` com a imagem decodificada `0x00A53CC0..0x00A54E47` de um
processo carregado. Pelo Ghidra também podemos ler a tabela de coleção e
comparar os tipos possuídos com o contador exibido na Card List. Não é
necessário executar o SetupReg para essa verificação.

## Amostra do operador e função de flcrc

O operador copiou `system.dat` e `replay.ydr` da pasta Common no Windows
para `passion/saves/`. O primeiro tem 5062 bytes e SHA256
`98e2ea327a7603a782f0899c413bab73d6dc8fee62b7fd3f5277b7ce0cb1f558`.
Os arquivos permanecem fora do repositório; o leitor não os modifica.

O formato externo tem quatro bytes de checksum seguidos de símbolos de
nove bits. `0x00439C00` calcula exponenciação modular; a escrita usa
expoente 5, módulo 299, e a leitura usa expoente 53, módulo 299. A amostra
decodifica para 4496 bytes: cabeçalho de oito bytes e imagem de 4488 bytes.
O checksum externo usa sementes `7D 13 8A BB` e soma quatro faixas
intercaladas **até o tamanho decodificado**, não até o fim do arquivo
codificado. Esse limite vem dos helpers nativos, embora pareça incomum.

Os checksums externo e interno, a assinatura `YUGIOH01` e o marcador
`0xFBA5` conferem. A coleção possui **38 entradas não nulas e 44 cópias**;
o contador interno também vale 44. A contagem de 38 ainda não aplica os
filtros de edição usados na tela. Cabeçalho decodificado:
`01 00 00 00 8E 79 E5 6A`.

`flcrc` não é a quantidade de cartas nem o checksum da coleção. É um valor
binário no registro com um pequeno bloco codificado pelo mesmo mecanismo.
`0x0045CA80` lê até 13 bytes, decodifica oito e, quando precisa criá-lo,
define o primeiro byte como 1 e o DWORD final a partir do helper de data/hora
`0x005C40B7`. O mesmo tipo de cabeçalho acompanha os dados persistidos.
`0x0045CD20` compara o byte inicial e o DWORD em +4 com os valores do objeto
de cabeçalho em memória. Isso comprova uma associação por identificador;
não estabelece que todos os consumidores rejeitem igualmente uma divergência.
Bytes +1..+3 não são comparados por esse helper. O significado expandido
da abreviação `flcrc` não foi estabelecido.

Não recebemos o valor do registro do operador, portanto ainda não
comparamos seu `flcrc` com o cabeçalho do arquivo. Também não foi determinado
quais campos dessa imagem guardam opções de áudio/vídeo. Não é necessário
alterar `flcrc` para ler e contar a coleção estaticamente.

Leitor somente de leitura, sem dependências externas:

```sh
python scripts/inspect_system_save.py ../passion/saves/system.dat
python -m unittest discover -s tests
```

Resultado da amostra em `evidence/system-save-2026-10-31.json`; código nativo
em `evidence/save-header-codec.txt` e `evidence/save-encoded-layout.txt`.
Os 31 testes passaram, incluindo seis novos casos com fixtures sintéticos
para domínio completo de bytes, bloco pequeno, contagem e corrupção.
