# SQLite cumulativo de evidências VISUALS

O importador original import_visual_virtual_bundle.py foi construído
para o primeiro bundle: só aceita retry com uma função, recria as
tabelas e exige backlinks de vtable. Não reutilizá-lo para os novos
bundles multi-função, nem sobrescrever a base atual.

Novo importador (sem alterar o anterior):

    python scripts/import_visual_evidence.py \
      --source scratch/joey-knowledge-visual-decoded.sqlite \
      --bundle scratch/ghidra-fusion-remaining-contract.zip \
      --bundle scratch/ghidra-coin-dice-break-contract.zip \
      --output scratch/joey-knowledge-visual-next.sqlite

Os nomes acima são exemplos dos roteiros pendentes, não afirmação de
que os ZIPs existam. Pode passar --bundle repetidamente ou importar
outro conjunto em uma nova cópia a partir da saída anterior.
Valida SHA256 do EXE no manifest e em cada relatório bem-sucedido,
limita tamanhos, não extrai arquivos do ZIP, recusa saída existente,
usa backup SQLite de origem read-only, integrity_check e
foreign_key_check. ZIP idêntico não duplica dados. Ausência ou
falha de decompilação permanece explícita; uma janela deslocada
no disassembly genérico fica apenas no relatório bruto, nunca
é indexada como se fosse o VA solicitado. Janelas dirigidas e
memória solicitada incompletas são rejeitadas.

Tabelas privadas: visual_evidence_bundles (manifest e hash),
visual_evidence_reports (texto original com proveniência),
visual_evidence_functions (batch/retry por VA e bundle),
visual_evidence_memory (bytes BLOB por endereço) e
visual_evidence_instructions (janelas exatas e ordinais).

Exemplos:

    SELECT address,provenance,bundle_sha256,source_entry
    FROM visual_evidence_functions WHERE address='004584C0';

    SELECT address,hex(bytes) FROM visual_evidence_memory
    WHERE address='005DB5B0';

    SELECT address,instruction FROM visual_evidence_instructions
    WHERE window_address='004584C0' ORDER BY bundle_sha256,ordinal;

As decompilações transientes novas também são adicionadas por
INSERT OR IGNORE à tabela histórica quando ela existe.
Versões antigas não são alteradas, nem se inventam funções
persistentes ou slots de vtable; versões alternativas ficam
na tabela nova. A tabela functions original permanece intacta.
Não versionar a base, os ZIPs, os relatórios ou os bytes lidos.
O código e as fixtures sintéticas são os únicos artefatos públicos.
Os próximos bundles ainda não foram recebidos nem importados.
