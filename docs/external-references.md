# Referências externas indicadas pelo usuário

Consulta: 2026-10-29. Nenhum executável externo foi baixado ou executado. Links abaixo apontam branches móveis; não foram fixados commits nesta consulta inicial. As observações locais anteriores foram obtidas independentemente; concordância externa não substitui xrefs no alvo.

## bitemydusto / YGO-PoC-Toolset

[README](https://github.com/bitemydusto/YGO-PoC-Toolset/blob/main/README.md) apresenta unpacker, ferramentas de cartas e mod loader; declara foco em executável No-CD/Fixed. Não assumir equivalência de endereços nem procedência de nosso alvo.

[Unpacker/Program.cs](https://github.com/bitemydusto/YGO-PoC-Toolset/blob/main/src/Unpacker/Program.cs), GetFileInfos, concorda com nosso cabeçalho, contador, nome de 256 bytes, três uint32 e troca de nibbles. Decompress descreve LZSS, dicionário de 4096 bytes, posição inicial 0xFEE e referências com comprimento +3. É hipótese concreta para confrontar com 0x0054A000. DecodeYPK1 concorda com o ramo visto em 0x00543290. UnpackFiles lê FileSize, embora compare CompressedFileSize; nosso inspetor usa o tamanho armazenado como limite físico. Não reproduzir essa leitura excedente.

Confiança alta na concordância do layout; LZSS ainda não validado localmente nesta rodada. Tentativa de consultar FileData.cs recebeu HTTP 429.

## derplayer / YuGiOh-PoC-ModTools

[README](https://github.com/derplayer/YuGiOh-PoC-ModTools/blob/master/README.md) anuncia ferramentas DAT, YGA e LZSS.

[YuGiData.cs](https://github.com/derplayer/YuGiOh-PoC-ModTools/blob/master/YuGiOh_PoC_Patcher/YuGi/YuGiData.cs), LoadFileList, também reconhece assinatura, contador e registros de 268 bytes.

[YGAFile.cs](https://github.com/derplayer/YuGiOh-PoC-ModTools/blob/master/YuGiOh_PoC_Patcher/YuGi/Filetypes/YGAFile.cs) usa cabeçalho de 24 bytes, descompressão LZSS quando Mode == 1 e pixels BGRA de quatro bytes. Isso fornece pistas para futura inspeção de um YGA; nenhum layout de YGA foi validado contra nosso jogo ainda. Conferir limites com amostras próprias antes de adotar código.

## Consequência para o plano

Próxima rodada: comparar a função 0x0054A000 com a variante LZSS descrita, validar um único recurso pequeno e só depois avançar para tabela de cartas/IDs. YGA é uma trilha auxiliar para renderização, sem prioridade sobre efeitos de cartas.

Atualização: comparação com 0x0054A000 e teste de um recurso concluídos na terceira rodada; parâmetros LZSS coincidem e a amostra consumiu/produziu exatamente os tamanhos do índice. Detalhes em [formatos](02-file-formats.md).
