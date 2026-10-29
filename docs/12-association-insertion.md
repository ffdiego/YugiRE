# Inserção de associações e resolução da chave

Nona rodada, 2026-10-29. Análise estática, alvo e convenções da
[visão geral](00-overview.md). Endereços VA. Nenhuma alteração persistida no
banco; os cinco arquivos originais continuam preservados.

## Conclusões e limite principal

**Alta:** a rotina 0x0056AB60 insere índice e metadados no fim de uma lista
selecionada por bloco/categoria e incrementa seu contador. O comando 0x86
chega a essa rotina. Categoria 11 usa as mesmas bases consultadas na busca
de Parasite Paracide.

**Alta:** a chave `((w >> 12) & 1) + 2 * ((w >> 24) & 0x7F)` também é usada
por 0x005687D0 para procurar uma palavra de estado em várias regiões.
Isso sustenta seu papel como vínculo para localizar uma ocorrência de carta;
não prova unicidade global, persistência ao longo do duelo nem o significado
isolado de seus campos de bits.

**Ainda aberto:** o produtor específico que cria uma entrada de Parasite
Paracide com nibble 13. Não atribuir esse significado aos produtores de tipo 3
observados nesta rodada. Ter encontrado a inserção genérica não estabelece
por si só quais argumentos chegam a ela em todo caminho de efeito.

## Inserção genérica

Para `s = arg1 & 1`, `c = (arg1 >> 8) & 0xFF` e
`delta = s * 0xD44 + c * 0x90`, **0x0056AB60** faz:

```text
n = uint16[0x00A55D7E + delta]
uint16[0x00A55D80 + delta + n * 2] = arg2
uint16[0x00A55DC0 + delta + n * 2] = arg3
uint16[0x00A55D7E + delta] = n + 1
```

Não há checagem explícita de capacidade nesse helper. Para categoria 11,
as bases tornam-se contador **0x00A563AE**, índice **0x00A563B0** e
metadados **0x00A563F0**. Diferentemente da limpeza por 0x4D, essa rotina
efetivamente incrementa o contador.

A tabela de normalização do dispatcher fornece:
`0x86 → índice 0x5C → stub 0x005B969A → handler 0x00571BC0`.
O handler passa as palavras do registro corrente nesta ordem:
`0x0056AB60(palavra_3, palavra_2, palavra_4)`.
Portanto, nesse comando, o destino bloco/categoria vem da **terceira palavra**,
não diretamente do bit alto da primeira palavra.

**0x005747E0** emite 0x86 com os outros três argumentos como payload.
**0x00579790** é um wrapper de três argumentos na pilha que força categoria
11 no destino: `(s & 0xFF) | 0x0B00`. O decompilador recuperou nele uma
assinatura `__thiscall`/quatro parâmetros ambígua; as instruções em
0x00579790–0x005797B0 mostram os três argumentos na pilha e sua transmissão.
Nos três caminhos amostrados em 0x005771F9, 0x005795C4 e 0x00577F90, o
produtor empacota **`3 | (chave << 8)`**. São exemplos de formato compartilhado;
não são criação comprovada do tipo 13.

Evidências: [inserção e instruções](evidence/association-slot-insertion.txt),
[handler](evidence/association-append-handler.txt),
[emissor de comandos](evidence/association-command-producers.txt),
[três produtores com tipo 3](evidence/association-type3-examples.txt),
[argumentos e wrappers](evidence/association-type13-producer.txt).
O último nome registra a hipótese procurada, não a confirmação de tipo 13.

## A chave localiza uma ocorrência

**0x005687D0(chave)** procura palavras de estado cuja extração da chave
coincida com o argumento. Percorre primeiro 11 registros de passo 0x90 em
cada um de dois blocos; depois outras regiões com palavras de quatro bytes
e contadores próprios. Retorna um localizador empacotado:

```text
byte baixo: seletor do bloco
byte seguinte: código de região ou posição de registro
16 bits altos: índice da posição em certas regiões
```

Por exemplo, a região baseada em **0x00A56574** retorna código 13 no segundo
byte e a posição nos 16 bits altos; a região **0x00A56434** retorna código 11.
Não confundir esses códigos de região com o nibble de tipo dos metadados da
associação: ambos usam números pequenos, mas estão em campos diferentes.
A falha de busca retorna 0x1000; há também tratamento especial para dois
registros globais temporários. Semântica completa de cada região pendente.

O mesmo relatório mostra 0x005690D0 removendo uma palavra da sequência em
0x00A56574 e 0x00568C00 inserindo uma palavra no início dessa sequência.
Esses acessos manipulam palavras de estado de quatro bytes, e não o array
paralelo de associações de duas palavras uint16. Foram distinguidos para
evitar concluir que qualquer movimento de carta crie metadados de tipo 13.

Evidência: [resolução da chave e operações adjacentes](evidence/association-key-resolution.txt).
Referências usadas para selecionar os escritores:
[bases específicas](evidence/association-writer-xrefs.txt) e
[bases genéricas](evidence/association-generic-writer-xrefs.txt).

## Validação e próximo passo

```bash
python3 YugiRE/scripts/validate_association_insertion.py
```

O script reexecuta a preservação/validação da rodada anterior e verifica seis
sequências de bytes e o despacho de 0x86 no alvo conhecido. Resultado esperado:
duas linhas `PASS`; [relatório](evidence/association-insertion-validation.json).

Trechos sem listing foram investigados apenas com desassemblagem transitória
e descartados pelo modo readOnly; tentativas e trilhas auxiliares ficam em
`scratch/association-round9/`, não versionadas. Nenhum nome ou tipo foi aplicado.

Próxima rodada: procurar escritores que formem explicitamente
`13 | (chave << 8)` para a lista de categoria 11. A conexão entre essa criação
e os ramos do ID 762 ainda precisa ser demonstrada. Não ampliar a investigação
para todas as cartas ou todos os consumidores de propriedades.
