# YGA, spritesheets e controle de animação

**Conclusão:** os 537 YGAs deste DAT são rasters BGRA de 32 bits comprimidos.
O formato não contém uma linha do tempo: quadros, recortes e evolução temporal
ficam em manifestos e código nativo. Isso não exclui variantes fora deste corpus.
O formato já era conhecido no projeto externo indicado pelo usuário; ver
[proveniência e comparação](upstream-comparison.json).

## Cabeçalho e payload

Offsets relativos ao início do recurso, inteiros little endian:

| Offset | Bytes | Campo e evidência |
|---|---:|---|
| `0x00` | 4 | Assinatura `79 67 61 00` (`yga\0`), presente em 537/537 |
| `0x04` | 4 | Largura; enviada a `00496AB0` pelo loader |
| `0x08` | 4 | Altura; enviada a `00496AB0` pelo loader |
| `0x0C` | 4 | Modo: zero copia raster; não zero entra na descompressão nativa; corpus contém somente 1 |
| `0x10` | 4 | Bytes descomprimidos; em todo o corpus `largura × altura × 4` |
| `0x14` | 4 | Comprimento comprimido; em modo 1 coincide com tamanho do recurso menos 24 |
| `0x18` | variável | Raster bruto (modo 0) ou LZSS (modo 1) |

`00497360` obtém o buffer por `00542220`, aloca a superfície por `00496AB0`,
copia `+0x10` bytes do payload no modo zero, ou chama `0054A000`.
As instruções `00497407–00497411`, `00497463–00497487` e
`004974BA–004974D7` confirmam os offsets mesmo onde o decompilador perde a pilha.
[Evidência estática](evidence/native-yga-loader.txt).

O LZSS usa dicionário circular de 4.096 bytes inicialmente zero, escrita em `0xFEE`,
flags LSB primeiro (1 literal), referência de 12 bits absoluta e comprimento
`(segundo_byte & 15) + 3`. O parser limita a saída e exige consumo integral.
Não foram encontrados bytes adicionais com tabelas de quadros nos 537 casos.

`00496AB0` cria alternativamente DIB BI_RGB de 32 bits com altura negativa,
ou buffer alinhado a oito bytes; stride `largura × 4` e ponteiros de linhas
avançando. Isso sustenta orientação de cima para baixo.
A ordem B,G,R,A e o cabeçalho coincidem com
[YGAFile.cs](https://github.com/derplayer/YuGiOh-PoC-ModTools/blob/b689dfbfc593916e7e20ea4873ce92ef81c9fc0d/YuGiOh_PoC_Patcher/YuGi/Filetypes/YGAFile.cs)
e [YGAHeader.cs](https://github.com/derplayer/YuGiOh-PoC-ModTools/blob/b689dfbfc593916e7e20ea4873ce92ef81c9fc0d/YuGiOh_PoC_Patcher/YuGi/Headers/YGAHeader.cs).

O escritor externo de modo 0 coloca comprimento comprimido zero. O parser foi
ajustado para essa convenção, com teste sintético; não há exemplo bruto no DAT.
Não houve execução de arquivo produzido por esse escritor no jogo.
Modo não zero diferente de 1 permanece `Unsupported` na ferramenta por falta de amostras.

## Evidência nas amostras

| Recurso | Offset DAT decimal | Tamanho | Dimensões | Payload comprimido |
|---|---:|---:|---:|---:|
| `cursor/cursor_e_summon.yga` | 137341554 | 4632 | 64×64 | 4608 |
| `j/duel/effect/coin_sha.yga` | 213789198 | 3523 | 168×42 | 3499 |
| `j/duel/effect/dice_shadow.yga` | 214356680 | 7685 | 288×48 | 7661 |
| `j/duel/effect/expl1.yga` | 216774575 | 19846 | 164×164 | 19822 |
| `j/duel/effect/kouka_ue_b.yga` | 217399920 | 39739 | 164×164 | 39715 |
| `j/duel/effect/vija_smoke.yga` | 217665464 | 25044 | 400×80 | 25020 |

Hashes dos recursos e dos rasters em [corpus-validation.json](corpus-validation.json).
Os três fontes LZSS externos foram compilados isoladamente com .NET 10:
**537/537 hashes dos pixels e 537/537 comprimentos coincidem** com o Python.
Esse diferencial é entre implementações offline; não atesta equivalência dinâmica
com o executável. O decoder externo é mais permissivo com truncamento/padding;
a ferramenta local rejeita esses casos.

## Manifestos e parâmetros

`005498B0` faz duas passagens nas linhas, conta entradas, armazena nome e inteiro
opcional. `00560700` lê o token e `005605A0` o inteiro decimal com sinal.
O inteiro é um parâmetro para o consumidor; seu significado não é global.
A ferramenta preserva ausência como `null`, embora o parser nativo use zero
quando não consegue ler o parâmetro. Não confundir isso com coordenadas (0,0).

`coin.txt` declara 14 padrões de `coin_rotate.bmp`, quatro de `coin_sha.yga`
e quatro de `coin_kirari.yga`; o construtor `004571D0` divide a largura pelo
parâmetro. Em `vija.txt`, um comentário japonês declara número de padrões × 1000
mais deslocamento Y; `5004` e `8031` dão cinco/Y=4 e oito/Y=31.
`004580F0` divide pela parte `/1000`; o desenho `004584C0` usa também `%1000`.
Essa semântica é específica desse efeito. Não aplicá-la a todos os inteiros TXT.

## Sequências e relógios nativos

[Evidência selecionada](evidence/native-animation.txt); VA do EXE com SHA fixado.

| Apresentação | Construtor / recursos | Atualização / desenho |
|---|---|---|
| Ativação | `00450260`, `duel/effect/activate.txt`; pares `kouka_ue/sita_b/y` | Vtable `005DB2F4`: update `00450590`, desenho `004505D0` |
| Explosão | `004511D0`, `duel/effect/explosion.txt`, sete YGAs | Vtable `005DB324`: update compartilhado `00456730`, desenho `004514B0` |
| Moeda | `004571D0`, `duel/effect/coin.txt` | Vtable `005DB404`: update `00457500`, desenho `00457520` |
| Vija | `004580F0`, `duel/effect/vija.txt` | Vtable `005DB414`: condição `004584A0`, desenho `004584C0`; update no slot +8 é vazio |

Na ativação, contador em objeto `+0x10` aumenta em 16; flag ativa em `+4`
é mantida enquanto o novo valor é menor que `0x101`. No primeiro update com
contador zero há chamada `005C03D0(0x1000000D,0)`, cujo serviço não foi investigado.
O desenho consulta tabela `00A52EE8` e compõe dois sprites com transformações.

Na explosão, contador aumenta em 8. O desenho seleciona estágios com limites
`0x20`, `0x80`, `0x90`, `0xD0`, usa os recursos separados e desativa o objeto
quando o contador passa de `0xCF`. A renderização também pode alterar o estado;
não presumir um update completamente separado de draw.

Na moeda, update aumenta em 8 e compara com `(campo +0x14 + 4) × 0x80`.
O desenho contém seleção de regiões, composição e interpolação. Seu pseudocódigo
tem propagação de tipos ruim: não foi portado como especificação completa.

**Aberto:** frequência dos despachantes, pausas, skipped draws, blends por canal,
seleção exata de todos os padrões e sincronização com áudio. Nenhuma contagem
foi convertida em segundos ou milissegundos.
