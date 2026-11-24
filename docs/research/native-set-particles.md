# Set: geometria das partículas — evidência do SQLite global

## Fontes

- `joey-knowledge.sqlite`, enviado pelo usuário em 21/11/2026: funções
  `0044EFD0` (construtor), `00402690` (classificação),
  `0045C140` (centro), `0055C140` (gerador MT), referências e
  chamadas `004059C0`/`00405CB0`.
- Lote de Ghidra de 21/11/2026: `retry-011-0044F390.txt` (Draw);
  `retry-010-0044F370.txt` (Update). Os dois entrypoints transitórios
  **não existem como funções separadas** no SQLite atual.
- SHA-256 nativo (mesmo alvo em ambos):
  `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Contrato recuperado do construtor 0044EFD0

O construtor obtém o centro da carta por `0045C140` e o armazena em
`obj+0x14` (X) e `obj+0x18` (Y). Em seguida prepara **oito pares
de inteiros** em `obj+0x104,0x108`, com stride de 8 bytes:

```text
para i = 0..7:
    R = 16 + (NextUiDeckMtUInt32_0055C140() & 31)
    theta = i * 64                 # unidades da tabela 0..511
    X[i] = centerX + ((C[theta] * R) >> 16)
    Y[i] = centerY + ((C[(theta-128)&511] * R) >> 16)
```

A tabela `C = DAT_00A52EE8` usa ponto fixo x16.16. Por isso os
centros das partículas ficam em oito direções ao redor da carta,
com raios inteiros de **16 a 47 pixels**. A ordem original é
direita, diagonal inferior direita, baixo, diagonal inferior esquerda,
esquerda, diagonal superior esquerda, cima, diagonal superior direita.

A chamada de RNG usa o gerador Mersenne Twister compartilhado do
jogo, estudado em `0055C140`. A Alpha preserva a distribuição dos
cinco bits inferiores, mas usa `Random.Shared` separadamente da
lógica de duelo. Isso **não** reproduz o estado exato do RNG nativo
nem a realização concreta dos oito raios de um duelo original.

## Desenho recuperado de 0044F390 (ZIP Ghidra)

- Contador `0,12,…,252`: 22 amostras de Draw; a próxima atualização
  desativa o efeito.
- Para cada partícula `i`, o lançamento é atrasado em `16*i`.
  `age = trunc((counter - 16*i) / 8)`, renderizado só com idade
  não negativa.
- `variant = age & 1`; a partícula troca entre duas variantes BMP.
- A posição Y é `Y[i] - floor(age²/16)`. X permanece `X[i]`.
  Os pixels da imagem são centralizados por metade da largura/altura.
- O índice de sprite da partícula é
  `(age&1) + 2 + (flag==0 ? 2 : 0)`. A reconstrução da associação
  dos arquivos depende da ordem de `j\duel\effect\set.txt`,
  que não consta no SQLite (ele contém referências a strings,
  não o conteúdo do manifesto DAT).
- `flag` fica em `obj+0x1E`, preenchido via `00402690`.
  A função testa se o tipo da carta é de `1..20`, portanto a
  distinção é de **tipo monstro vs. magia/armadilha**, não de lado.
  A família de BMPs escolhida pelo cliente segue a hipótese baseada
  em seus nomes e índices consecutivos.

O cliente agora renderiza os oito BMPs de estrelas conforme as
amostras de tempo e as origens provenientes da fórmula do construtor,
além do brilho central já implementado.

## O que falta validar visualmente

- A ordem real dos arquivos em `set.txt` no `data.dat`, a
  seleção exata de cores/colorkey e a composição dos passes.
- A tabela x16.16 real (a Alpha ainda usa cosseno calculado).
- O estado MT nativo (não é necessário copiar o código do gerador
  para obter comportamento **observável** equivalente).
- Capturas pareadas de Set de monstro e magia/armadilha nos dois lados
  do campo, em updates 0, 11 e 21.

**Status:** Set continua `Partial` na badge. O denominador e a
quantidade coberta não mudaram; nenhuma equivalência pixel-perfect foi
declarada.

**Observação de processo:** o banco global continha o construtor que
parecia faltar no ZIP; não solicitar outra exportação Ghidra desse
endereço. Consultar primeiro o SQLite e então os entrypoints
transitórios do lote quando não existirem na tabela `functions`.
