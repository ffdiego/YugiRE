# Inventário visual — Joey the Passion

Investigação de 2026-11-16, somente leitura offline. O [CSV completo](resource-inventory.csv)
contém **3.568 entradas**, nome, extensão, offset absoluto no DAT, tamanho lógico,
tamanho armazenado, assinatura inicial, dimensões, formato e finalidade provável
com confiança separada. Não contém imagens, descrições de cartas ou payloads.
A finalidade baseada no diretório é inferência; o formato é validado em bytes.

Originais localizados em `../passion/` nesta máquina, sem fixar esse caminho nas ferramentas:

- EXE SHA256: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- DAT SHA256: `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.

| Formato | Entradas | Bytes lógicos / armazenados no DAT | Identificação |
|---|---:|---:|---|
| BMP | 2.763 | 308.911.010 / 298.354.324 | Alta: `BM`, BI_RGB; 2.760 de 24 bits, dois de 1 bit, um de 8 bits |
| YGA | 537 | 18.408.948 / 18.408.948 | Alta: `yga\0`, raster BGRA32 com LZSS interno |
| TXT | 167 | 154.438 / 69.366 | Manifestos, parâmetros e listas; função depende do consumidor |
| BIN | 41 | 1.610.206 / 575.430 | Tabelas já investigadas; ver [formatos existentes](../../02-file-formats.md) |
| YDC | 58 | 6.752 / 6.752 | Decks, fora da investigação visual |
| SUE | 2 | 521.756 / 521.756 | `SUECAT 1A 00`; candidato a catálogo de miniaturas, confiança baixa quanto ao uso |

Há compressão **externa DAT** em 150 TXT, 39 BIN e 16 BMP. Nenhum YGA usa essa
camada no corpus, embora todos usem LZSS **interno**. O inventário descomprime
ambas conforme a camada, sem confundir os tamanhos.

## Famílias relevantes

| Família | Recursos e finalidade | Evidência / confiança |
|---|---|---|
| `card/`, `mini/` | 777 BMPs em cada: arte e miniaturas; mapas de 1.114 associações podem compartilhar imagens | Manifestos existentes; alta |
| `cursor/` | 61 YGAs; cursores e balões de ações | Manifestos e imagens; alta |
| `j/duel/effect/` | 61 YGAs: ativação, explosão, moeda, dado, fusão, exclusão, invocação especial, resultado | Manifestos + construtores nativos; alta para os vínculos rastreados |
| `j/duel/phase_name/` | 90 YGAs; sequências `nex_?_b/r[1–8]` de troca de turno | Manifesto; alta para lista, temporização ainda aberta |
| `j/duel/field/` | 54 YGAs; gráficos de campo | Diretório e dimensões; média para finalidade específica |
| `j/duel/help/`, `dialog/`, `get/` | 136 / 30 / 26 YGAs de interface | Diretórios; média, não são 192 animações distintas |
| `j/duel/exodia/`, `chain/`, `life/` | 11 / 6 / 2 YGAs | Famílias de apresentação; média |
| `j/bust_up/` | 21 YGAs em win/start/summon | Apresentação de personagem; média |
| `j/title/` | Duas máscaras YGA, 440×320 e 155×312 | Cabeçalhos + `title.txt`; alta para raster/máscara, blend específico aberto |

Spritesheets confirmados por parâmetros e geometria: `coin_sha.yga` 168×42 →
quatro regiões 42×42; `coin_kirari.yga` 56×14 → quatro 14×14;
`dice_shadow.yga` 288×48 → seis 48×48; `vija_smoke.yga` 400×80 → cinco 80×80;
`vija_moji_smo.yga` 256×37 → oito 32×37. Sequência e duração precisam do consumidor.
O TXT também enumera quadros em arquivos separados (`expl1`…`expl7`, `sp_sum_*`).
Ver [formatos de animação](animation-formats.md).

Os BMPs fora do subconjunto 24 bits do demo são `j/bust_up/dummy#.bmp` (800×600,
1 bit), `j/duel/detail/line.bmp` (177×1, 8 bits) e
`j/duel/dialog/3taku_mask.bmp` (306×20, 1 bit). Não se alterou o parser da aplicação.

Os SUE são `cursor/_thumbnl.sue` e `j/deck_c/card_koukan/_thumbnl.sue`;
não foi provado que o executável os consome. A assinatura e os nomes internos
sugerem cache de catálogo de imagens. Não há evidência para tratá-los como script
de animação; foram deixados com prioridade inferior aos controladores nativos.

Reprodução, limites e exportação local: [ferramentas](../../../tools/visual/README.md).
A [validação do corpus](corpus-validation.json) registra hashes antes/depois,
amostras com offsets e cinco verificações de divisões declaradas em manifestos.
