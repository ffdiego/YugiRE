# Enriquecimento visual do SQLite de Joey

A base de decomposições de 21/11/2026 tem 6.518 funções e 159.821 referências. O índice original contém ponteiros para apenas 10 dos 28 slots analisados nas sete vtables de seleção, diálogos, corrente, indicadores e layerfont. Os outros 18 estão **não exportados**, não necessariamente nulos.

Os scripts scripts/enrich_joey_sqlite.py e scripts/ExportVisualVtables.java permitem coletar e armazenar essa informação sem alterar o executável, o projeto Ghidra ou o SQLite de origem.

## Etapa 1: enriquecer a cópia existente

    python3 scripts/enrich_joey_sqlite.py --source scratch/joey-knowledge.sqlite --output scratch/joey-knowledge-visual.sqlite --summary scratch/visual-vtables-audit.json

O destino deve ser um arquivo novo. Cria visual_subsystems, visual_vtables, visual_virtual_methods, visual_analysis_notes e visual_unresolved_slots. Não reinterpreta os nomes de funções nativas.

## Etapa 2: recuperar os slots reais no Ghidra do Fedora

Executar a partir da raiz do repo com o projeto persistente ghidra-projects/Joey e o mesmo joey_pc.exe:

    mkdir -p scratch
    GHIDRA_INSTALL_DIR="${GHIDRA_INSTALL_DIR:-$HOME/tools/ghidra_12.2_PUBLIC}"
    "$GHIDRA_INSTALL_DIR/support/analyzeHeadless" "$PWD/ghidra-projects" Joey -process joey_pc.exe -noanalysis -readOnly -scriptPath "$PWD/scripts" -postScript ExportVisualVtables.java "$PWD/scratch/visual-vtables.tsv" -log "$PWD/scratch/visual-vtables-ghidra.log"

A rotina lê quatro bytes por entrada e informa as VAs de função quando conhecidas. Confirme o sucesso no log e o timestamp do TSV antes da próxima etapa. Sem o projeto Ghidra real, não é possível validar esses 18 DWORDs.

## Etapa 3: mesclar em nova base

    python3 scripts/enrich_joey_sqlite.py --source scratch/joey-knowledge.sqlite --output scratch/joey-knowledge-visual-ghidra.sqlite --ghidra-tsv scratch/visual-vtables.tsv --summary scratch/visual-vtables-ghidra-audit.json

O importador valida SHA256 do executável, formato TSV, posição dos slots, duplicatas e divergências com ponteiros já conhecidos. Um slot unreadable não substitui evidência existente.

## Consulta SQL

    SELECT v.subsystem,m.slot_index,m.role,m.slot_address,m.target_address,
           m.function_address,m.resolution,m.evidence_source
    FROM visual_virtual_methods m JOIN visual_vtables v
    ON v.address=m.vtable_address
    ORDER BY v.subsystem,m.slot_index;

O badge visual continua em **19/31**: as novas tabelas medem investigação, não implementação C# nem paridade com o Joey. O exportador Java ainda precisa ser executado na instalação local do Ghidra; os quatro testes Python passaram em fixture sem binários.

Não versionar SQLite, EXE nem DAT.

## Etapa 4: importar as decompilações transitórias de funções virtuais (22/11/2026)

O ZIP `ghidra-visual-virtual-methods.zip` fornecido pelo usuário contém 15 entrypoints distintos decompilados via `tempfunc:`, associados a 18 slots. O novo importador mantém o resultado em tabela separada e não promove funções transitórias para o índice persistente:

```bash
python3 scripts/import_visual_virtual_bundle.py \
  --source scratch/joey-knowledge-visual-ghidra.sqlite \
  --bundle scratch/ghidra-visual-virtual-methods.zip \
  --output scratch/joey-knowledge-visual-decoded.sqlite \
  --summary scratch/visual-transient-decomp-audit.json
```

Novo inventário de Draw identificados: **19/34 parcialmente implementados, 0 Verified**. Ver [relatório do lote](visual-virtual-methods-2026-11-22.md) para semântica dos slots; `+8`/`+C` não têm significado universal entre classes.
