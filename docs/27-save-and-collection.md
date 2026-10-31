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
binário de oito bytes `system\flcrc`, lido por `0x0045CA80`, participa desse
mecanismo. `0x00439FF0` verifica o conteúdo com `0x00439EF0` e chama
`0x00439D80` com parâmetros `0x35` e `299`; este extrai grupos de nove bits e
chama `0x00439C00`. A decodificação completa do arquivo fica pendente.

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
Não há `system.dat` disponível em `passion/`, logo não calculamos uma
coleção real nesta etapa. A reimplementação continua usando seu catálogo
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
dinâmica no Windows nem validado um decodificador de system.dat.

O próximo passo útil é comparar uma cópia do `system.dat` da pasta indicada
por `CommonDir` com a imagem decodificada `0x00A53CC0..0x00A54E47` de um
processo carregado. Pelo Ghidra também podemos ler a tabela de coleção e
comparar os tipos possuídos com o contador exibido na Card List. Não é
necessário executar o SetupReg para essa verificação.
