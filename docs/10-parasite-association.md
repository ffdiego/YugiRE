# Parasite Paracide — consulta de associação

Sétima rodada, 2026-10-29. Análise estática de poucas funções no Ghidra 12.2,
sem executar o jogo. Endereços abaixo são VA; image base `0x00400000`.
SHA256 do alvo e verificações de preservação constam em
[parasite-static-validation.json](evidence/parasite-static-validation.json).

## Conclusão comprovada

**0x00570BE0** procura uma entrada de Parasite Paracide associada a uma
palavra de estado recebida por ponteiro. Retorna **posição + 1**, ou zero
quando não encontra. Sugestão documental: `buscar_associacao_parasite`;
nenhuma renomeação foi persistida no banco. Confiança alta no mecanismo;
o significado completo da associação permanece aberto.

Para `s = argumento_1 & 1` e `w = *argumento_2`:

```text
chave = ((w >> 12) & 1) + 2 * ((w >> 24) & 0x7f)
quantidade = uint16[VA 0x00A563AE + s * 0xD44]
para i em [0, quantidade):
    indice = uint16[VA 0x00A563B0 + s * 0xD44 + i * 2]
    flags  = uint8 [VA 0x00A563F0 + s * 0xD44 + i * 2]
    vinculo= uint8 [VA 0x00A563F1 + s * 0xD44 + i * 2]
    se (flags & 15) == 13 e indice == CARD_IntID[762] e vinculo == chave:
        retornar i + 1
retornar 0
```

São acessos paralelos com passo de dois bytes, separados por `0x40` entre
o índice e os metadados. Isso **não** estabelece a capacidade dos arrays;
não há limite superior explícito além do contador nessa função. A tabela
de nomes já validada associa ID 762 ao índice 197 e a Parasite Paracide.
O argumento não é simplesmente um ID: o helper lê a palavra inteira e
extrai bits de associação. O papel de bit 12 e bits 24–30 ainda não foi
confirmado como proprietário, controlador ou identidade de instância.

Evidência: [helper, instruções e três chamadores](evidence/parasite-callers.txt).

## Como os chamadores usam o resultado

**0x00570420**, caso `param_2 == 13`, retorna diretamente a consulta sobre
`uint32[0x00A56574 + s * 0xD44 + param_3 * 4]`. Outros caminhos dessa
função retornam flags ou constantes; portanto não tratar seu retorno geral
como booleano estrito: esse caso pode retornar posições maiores que um.
Confiança alta no fluxo; semântica de cada código de localização pendente.

**0x0057F130** percorre palavras de quatro bytes a partir de
`0x00A56434 + s * 0xD44`, com contador byte em
`0x00A55D68 + s * 0xD44`. Extrai índice com máscara `0xFFF`, aplica
filtros de propriedades e só decrementa a quantidade requerida quando
`buscar_associacao_parasite(...) == 0`. Retorna se a quantidade foi
satisfeita. Confiança alta para a exclusão da contagem; isso não basta
para nomear a ação como tributo, descarte ou custo.

**0x00578860** percorre outra sequência de palavras de quatro bytes,
a partir de `0x00A56574 + s * 0xD44`. Para uma carta cujo ID é 762 e cuja
associação existe, chama **0x005BA490** com código composto contendo
`0x365F0000` e a mesma chave. Se essa chamada retorna zero, consulta
novamente a associação, subtrai um e chama **0x00574960(s, 11, posição)**.
Esse wrapper envia a **0x005B9170** o código `0x4D` (com bit de seleção
`0x8000`) e empacota os bytes 11 e posição no argumento seguinte.
Isso confirma uso da posição encontrada; não comprova remoção imediata de
uma entrada, pois o consumidor desse comando não foi seguido.

O caminho alternativo passa, sob condições adicionais, por **0x005759A0**
e **0x00575AF0**. O segundo também extrai a mesma chave e emite chamadas
de comando/notificação; não foi encontrado nele um deslocamento direto
dos arrays que prove seu significado. O nome do relatório
`parasite-list-mutation.txt` identifica uma hipótese investigada, não uma
mutação demonstrada.

Evidências adicionais:
[wrappers e instruções do chamador](evidence/parasite-list-mutation.txt),
[helper alternativo e conversão para posição zero-based](evidence/parasite-transfer-helper.txt).

## Validação e limites

Além de confrontar o pseudocódigo com instruções, três sequências foram
conferidas diretamente no EXE, traduzindo VA para offset pelos cabeçalhos PE:

- `0x00570C2F`: `68 fa 02 00 00`, constante 762;
- `0x00570C3C`: `66 39 47 c0`, comparação do índice em metadados menos `0x40`;
- `0x00578994`: `48 50 6a 0b 53`, decremento e preparação dos argumentos.

Os cinco originais mantêm os hashes iniciais. O arquivo de banco do programa
também coincide com a cópia anterior às consultas. As consultas não refizeram
autoanálise nem persistiram alterações. Ainda não há prova de arquitetura
geral de efeitos, de estruturas completas de jogadores ou de IA.

Próximo passo pequeno: seguir o processamento do comando `0x4D` em
`0x005B9170`, distinguindo enfileiramento/apresentação de mudança de estado;
alternativamente, examinar quem escreve os metadados com nibble 13.
