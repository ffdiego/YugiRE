# Chave no pedido do callback de Parasite Paracide

Décima primeira rodada, 2026-10-29. Análise estática e verificação aritmética;
VA com image base `0x00400000`. Alvo e preservação da rodada anterior
revalidados. Nenhum jogo executado nem alteração persistida no banco.

## Resultado e limite

O campo `pedido+4` contém a chave nos **bits 5–12**. A rotina que cria o
pedido grava os oito bits baixos de um argumento nessa posição; o callback
de Parasite Paracide recupera os mesmos oito bits. Isso confirma o transporte
da chave, com confiança alta.

Também foi identificado um chamador que fornece a chave calculada de uma
palavra de estado com a mesma fórmula de 0x00570BE0. Entretanto, seu código
de pedido, **0x365F**, seleciona o **outro ramo** de 0x005875A0. Portanto não
está demonstrado que esse chamador cause a inserção de tipo 13 do primeiro
ramo. A representação está ligada; o ciclo temporal completo continua aberto.

## Criação e disposição do pedido

**0x005BA310(modo, codigo, chave, contexto)** verifica se os 16 bits baixos
do código são não zero. No caminho local, escolhe um registro de **20 bytes**,
limpa-o e preenche, entre outros campos:

| Offset | Valor observado |
|---|---|
| +0 | 16 bits baixos de `codigo`, usados pelo dispatcher como índice de carta |
| +2 | Bits do código redistribuídos, incluindo seletor e discriminantes |
| +4, bits 5–12 | Oito bits baixos de `chave` |
| +16 | 16 bits baixos de `contexto` |
| +18 | 16 bits altos de `contexto` |

As posições não listadas não receberam semântica nesta rodada.
Em +4 a expressão observada é:

```text
novo = (antigo & 0xE01F) | ((chave & 255) << 5)
```

`modo == 0` escolhe **0x00A558C0 + contador_pendente * 20**, com contador
uint16 em 0x00A55C84. O outro caminho escolhe
**0x00A55B40 + contador_ativo * 20**, com contador uint16 em 0x00A55C80.
Há um caminho condicionado por flags que serializa 12 bytes via 0x005BD090
e retorna antes da escrita local; sua comunicação não foi investigada.

Evidências: [construtor](evidence/parasite-request-enqueue.txt),
[seleção das áreas](evidence/parasite-request-local-layout.txt),
[instruções de escrita](evidence/parasite-request-storage-xrefs.txt).

## Como o pedido chega ao callback

**0x005BBE40** possui caminhos para processar pedidos e para transferir
registros da área pendente para a ativa. No caminho de transferência, chama
**0x005BDE10(destino, origem, 20)**, copiando o registro completo.
As instruções confirmam origem `destino - 0x280`, isto é, a diferença entre
0x00A55B40 e 0x00A558C0. O helper copia DWORDs e bytes residuais; não decodifica
nem recompõe +4.

**0x005BB720** calcula o ponteiro que encaminha a 0x0059DEB0 como:

```text
pedido = 0x00A55B2C + contador_ativo * 20
       = 0x00A55B40 + (contador_ativo - 1) * 20
```

Para contador positivo, isso seleciona o último registro ativo. A chamada em
0x005BBA78 encaminha esse ponteiro. Assim, o formato de 20 bytes preenchido
por 0x005BA310 corresponde ao pedido recebido pelo callback. Isso não
classifica todos os estados da máquina nem prova ausência de outras escritas
nos pedidos entre sua criação e consumo.

Evidências: [transferência](evidence/parasite-request-promotion.txt),
[seleção do registro](evidence/parasite-request-selection.txt),
[chamada do callback](evidence/parasite-request-callsite.txt),
[helper de cópia](evidence/parasite-word-key-caller.txt).

## Igualdade da chave e distinção dos ramos

Para uma palavra de estado `w`, o chamador em **0x00578954–0x00578981** monta:

```text
k = ((w >> 12) & 1) + 2 * ((w >> 24) & 127)
codigo = (w & 0xFFF) | (s << 31) | 0x365F0000
0x005BA490(codigo, k, contexto)
```

0x005BA490 consulta 0x0057EE80 e, se permitido, chama
`0x005BA310(0, codigo, k, contexto)`. Logo, nesse caminho a construção local
do pedido conserva `k` em +4. A extração usada pelo primeiro ramo do callback
seria `(uint16[pedido+4] >> 5) & 255 == k`.

Mas o mesmo construtor também redistribui os bits do código para +2.
Aplicando a fórmula observada ao código 0x365F, para os dois seletores,
obtém-se **`uint16[pedido+2] & 0x3000 == 0x2000`**. A primeira condição
de 0x005875A0 encaminha esse caso para **0x0058766E**, fora do ramo de inserção
analisado na rodada anterior. Não ligar por engano a emissão posterior de
0x4D à criação anterior de tipo 13 como se fosse uma única chamada linear.

Evidências: [entrada de chave e argumentos](evidence/parasite-request-key-input.txt),
[extração da palavra de estado](evidence/parasite-word-key-caller.txt),
[condição e primeiro ramo](evidence/parasite-handler-registration.txt).

## Validação

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_parasite_request.py
```

Esperado: quatro linhas `PASS`. Além das verificações anteriores, o script
confere cinco sequências de instruções e **65.536 combinações** da codificação:
256 chaves × 256 padrões dos bits preservados de +4. Para cada caso, verifica
que a extração do callback e o byte de vínculo dos metadados reproduzem a
chave da palavra de estado. Também verifica a seleção do ramo para 0x365F
nos dois seletores. É um teste do modelo extraído das instruções, sem executar
as rotinas originais. [Relatório](evidence/parasite-request-validation.json).

Próximo passo: seguir o ramo 0x0058766E e seus argumentos para descobrir o
que faz quando essa associação já pode existir. Continua pendente localizar
um produtor inicial cujo código selecione o primeiro ramo para Parasite
Paracide, com vínculo correspondente à palavra de estado relevante.
