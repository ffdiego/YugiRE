# Criação do vínculo de Parasite Paracide

Décima rodada, 2026-10-29. Análise estática do alvo com SHA256 conhecido;
endereços VA, image base `0x00400000`. Originais e banco preservados.

## Conclusão

Foi ligado um produtor de metadados de **tipo 13** ao ID de recurso **762**,
Parasite Paracide. Uma tabela selecionada por ID fornece o callback
**0x005875A0**. No ramo examinado, esse callback insere uma associação na
categoria 11 do bloco oposto, usando o índice da carta e uma chave extraída
do pedido recebido. Confiança alta na ligação e na escrita resultante.

Isso confirma um mecanismo de callbacks selecionados por carta neste caminho.
Não prova que todos os efeitos tenham uma função exclusiva, nem exclui
tabelas adicionais, código compartilhado ou outros mecanismos.

## Registro e chamada por ID

**0x0059DBE0** recebe um índice de propriedades, converte-o por
0x004022E0 (`CARD_ID`) e busca o ID resultante numa tabela com base
**0x005ED0A8** e passo **0x18 (24) bytes**. As instruções usam busca binária,
com índice inicial 0xDD e limites iniciais 0/0x1BA.

Para o ID 762, a busca chega ao registro de índice **150**, em
**0x005EDEB8** (offset de arquivo **0x001EDEB8**):

| Offset no registro | DWORD observado | Interpretação estabelecida |
|---|---|---|
| +0 | `0x000002FA` | ID de recurso 762 |
| +4 | `0x005875A0` | Callback chamado por 0x0059DEB0 |
| +8 | zero | Papel não investigado |
| +12 | `0x005800A0` | Papel não investigado |
| +16 | `0x0057CCC0` | Papel não investigado |
| +20 | zero | Papel não investigado |

**0x0059DEB0** usa o primeiro uint16 do pedido como índice de propriedades,
chama a busca, lê o ponteiro em `base + registro * 24 + 4`, verifica se é
zero e faz uma chamada indireta com o pedido e um segundo argumento.
O índice 197 já foi associado ao ID 762 e ao nome Parasite Paracide nas
rodadas de IDs/nomes. A conclusão usa a busca em código e o registro real;
não depende apenas da proximidade de um número com um ponteiro no arquivo.

Evidências: [referências à base dos callbacks](evidence/card-handler-table-xrefs.txt),
[busca e instruções](evidence/card-handler-id-search.txt),
[wrapper de chamada](evidence/card-handler-lookup.txt),
[chamada indireta nas instruções](evidence/parasite-callback-dispatch.txt).

## O ramo que cria o tipo 13

O trecho **0x005875A0** não tinha função independente/listing completo no
banco salvo. Foi desassemblado transitoriamente, sem persistir mudanças.
Chamamos o ponteiro recebido de `pedido`, sem atribuir ainda uma estrutura C.
O ramo seguido exige `(uint16[pedido+2] & 0x3000) != 0x2000`.

Define:

```text
indice = uint16[pedido+0]
s = uint16[pedido+2] & 1
posicao = (uint16[pedido+2] >> 1) & 31
origem = 0x00A55D74 + s * 0xD44 + posicao * 0x90
```

Antes de emitir a inserção, verifica que `(uint32[origem] & 0xFFF) == indice`
e que o bit 1 do byte em `origem+6` está definido. O significado desse bit
e da região não foi nomeado nesta rodada.

Em **0x0058760B–0x00587623**, monta:

```text
chave = (uint16[pedido+4] >> 5) & 255
metadados = 13 | (chave << 8)
0x00579790(1 - s, indice, metadados)
```

O wrapper, já validado na rodada anterior, força categoria 11 e emite o
comando 0x86 via 0x005747E0. Seu handler 0x00571BC0 chama 0x0056AB60,
que grava no fim dos arrays paralelos e incrementa o contador:

```text
destino = (1 - s) * 0xD44
n = uint16[0x00A563AE + destino]
uint16[0x00A563B0 + destino + n*2] = indice
uint16[0x00A563F0 + destino + n*2] = 13 | (chave << 8)
uint16[0x00A563AE + destino] = n + 1
```

Assim, a entrada satisfaz os campos de índice/tipo que 0x00570BE0 procura.
Para que a busca a encontre para uma palavra de estado `w`, ainda é necessário
que `chave == ((w >> 12) & 1) + 2*((w >> 24) & 0x7F)`.
A origem desse valor em `pedido+4` precisa ser seguida até quem monta o pedido;
não presumir que todo pedido recebido satisfaça automaticamente essa igualdade.

Evidência: [callback e produtor](evidence/parasite-handler-registration.txt).
Esse relatório cobre o primeiro ramo; a alternativa que começa em
0x0058766E e os helpers chamados após a emissão não foram reconstruídos.

## Validação e próximo passo

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_parasite_creation.py
```

Esperado: três linhas `PASS`. O validador reconfirma preservação, o caminho
de inserção da rodada anterior, seis sequências de bytes desta rodada e o
registro completo de 24 bytes. Reproduz a busca pelo ID 762 nos bytes da
tabela e registra os índices visitados, sem executar o jogo.
[Relatório com bytes, offsets e busca](evidence/parasite-creation-validation.json).

Próximo incremento: seguir quem monta `pedido+4` no caminho de 0x0059DEB0
e comprovar a igualdade da chave com a palavra de estado posteriormente
consultada. Isso fechará o vínculo entre criação, busca e limpeza sem depender
de analogia com as regras conhecidas da carta.
