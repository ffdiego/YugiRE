# Contrato DAT das listas nativas do Joey — 22/11/2026

Fonte: selection-manifests.json produzido no data.dat do usuário por Joey.App --inspect-visual-dat e scripts/summarize_native_selection_dat.py. Foram recuperados 15 manifests e 50 imagens, sem erros de decodificação nem recursos de seleção esperados ausentes. Não versionar o DAT, as imagens ou o texto completo dos manifests.

## Manifest pai: fundo e scroll

| Manifest | Fundo 0 | Fundo 1 | Submanifest | Inteiros em ordem |
| --- | --- | --- | --- | --- |
| selectlist1/2.txt | list_select0.bmp (451×191) | list_select1.bmp (451×191) | select_scroll.txt | 95, 83, 150 |
| gravelist1/2.txt | list_grave0/1.bmp (412×204) | mesmo BMP do lado (412×204) | grave_scroll1/2.txt | 102, 93, 153 |
| fusionlist1/2.txt | list_fusion0.bmp (422×223) | list_fusion1.bmp (422×244) | fusion_scroll.txt | 136, 136, 194 |
| excludelist1/2.txt | list_jyoga_jibu/aite.bmp (384×180) | variante *2.bmp (384×180) | exclude_scroll1/2.txt | 93, 74, 143 |

Os manifests 1/2 de seleção e fusão são iguais; os de cemitério e exclusão usam recursos distintos para cada lado. Os três inteiros do manifesto pai ainda não receberam significado geométrico comprovado.

## Inteiro composto do primeiro item de scroll

A função decompilada 0043BA30 divide o inteiro n conforme os campos do objeto:

    obj+0x40 = n / 10000000
    obj+0x44 = (n / 100000) % 100
    obj+0x48 = (n / 1000) % 100
    obj+0x4C = n % 1000

| Scroll | Bruto | +0x40 | +0x44 | +0x48 | +0x4C |
| --- | ---: | ---: | ---: | ---: | ---: |
| Seleção | 213839248 | 21 | 38 | 39 | 248 |
| Cemitério (dois lados) | 212617260 | 21 | 26 | 17 | 260 |
| Fusão | 242447336 | 24 | 24 | 47 | 336 |
| Exclusão (dois lados) | 224435224 | 22 | 44 | 35 | 224 |

**Confirmado:** a fórmula de separação. **Pendente:** a semântica de cada campo (não presumir x/y/largura/altura). Os números dos outros recursos, como 001000 e 002000, devem conservar o texto com zeros à esquerda e o valor inteiro.

## Recursos auxiliares

- Seleção: sele_box.bmp (41×12); seis setas sele_yaji_l/r0/1/2.bmp (40×21).
- Cemitério: grave_bar_jibu/aite.bmp (308×31), grave_box_jibu/aite.bmp (17×17); setas 26×26.
- Fusão: fusi_box.yga (47×32); setas 41×38.
- Exclusão: jyoga_box_jibu/aite.yga (38×23); setas esquerdas 32×24, direitas 46×24.
- Ícones: 11 BMPs 16×16; list_myself.bmp e list_oppo.bmp (50×6).
- null.bmp existe fisicamente no DAT, com 1×1. Não é ausência de imagem.

## Relação com o Draw

0043A060 usa âncora X=DAT_005DB570 e Y=300; 0043ABA0 posiciona as cinco cartas em centerX + 64 × (slot-2). O fundo 451×191 não é automaticamente um hitbox nem tamanho de carta. Rolagem, variantes, escala e recorte ainda precisam de comparação no Joey executando.

## SQLite: importação reproduzível

O script scripts/import_native_selection_dat.py acrescenta ao novo banco quatro tabelas (visual_dat_manifests, visual_dat_images, visual_dat_entries, visual_dat_scroll_fields) e a view visual_dat_entry_details. Na amostra: 15 manifests, 50 imagens, 85 referências ordenadas e seis descritores de scroll, nenhuma referência não resolvida. O banco de origem não é modificado; nenhuma imagem ou comentário integral é gravado.

Com ambos os arquivos em scratch/, execute:

    python3 scripts/import_native_selection_dat.py --source scratch/joey-knowledge-visual-decoded.sqlite --dat-selection scratch/visual-review/selection-manifests.json --output scratch/joey-knowledge-visual-dat.sqlite --summary scratch/visual-selection-dat-audit.json

Consulta sugerida:

    SELECT manifest_path,entry_index,resource_path,numeric_raw,width,height
    FROM visual_dat_entry_details
    WHERE manifest_path LIKE '%selectlist1.txt' ORDER BY entry_index;

SHA256 de joey_pc.exe para as decompilações: db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de. Não há hash do DAT neste relatório. Este lote não altera o badge 19/34, 0 Verified.
