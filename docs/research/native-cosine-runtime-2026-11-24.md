# V01 — tabela de cosseno x16.16

A inicialização em `0055DCF0` escreve 512 inteiros em `00A52EE8` e
marca `00A536E8` ao concluir. O assembly do EXE com SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`
confirma a sequência `FILD`, dois multiplicadores, `FCOS`, um
multiplicador e a conversão inteira em `005C3E40`.

O snapshot privado v3 registra os 512 índices consecutivos na
`visual_runtime_cosine` para `source_id = 'cosine-20261124'`.
A fórmula `trunc(cos(i * π / 256) * 65536)` coincide em todos os
índices com a captura. Os valores assinados serializados como
Int32 little-endian em ordem de índice têm SHA-256
`c3c61d7556976734a882e61e82fe27a7e76fb897a9b7e2a1908ed07d21f3c1e9`.
O teste C# usa esse hash como referência.

O cliente gera a tabela ao iniciar. Em F12, `cosineSource` informa
`native-equivalent-generated` por padrão ou `native-runtime-capture`
com `--native-cosine=`. A comparação da tabela não valida os pixels, a
ordem de Draw ou a cadência dos efeitos que a consomem.
