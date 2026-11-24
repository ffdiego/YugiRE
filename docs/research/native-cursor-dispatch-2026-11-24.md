# V09/V10 — seleção de ações e pedidos de cursor

Fonte privada SQLite v2, SHA256 80daa5c6563f4be5da0ed742517514a4eaada7ec7b70440bdc29f00ccea04da4,
quick_check=ok; fingerprint EXE db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta decompilations, refs, pointers, strings, visual_evidence_memory e
visual_evidence_instructions. Os alvos do roteiro 26 não têm assembly
indexado de entrada; fórmulas abaixo são leitura de decompilação, não
assembly confirmado.

## Produtor de opções

0044DBC0 zera 005F2FDC/005F2FE0, obtém coordenadas pelo objeto
de input e usa hit-test 0041E3D0. 005BEEE0 lê a palavra contextual
via 00418B50, ou usa 0045C3F0 quando não há carta sob o cursor.
Publica lado/zona/índice em 00A55044/48/4C e reúne bits de entrada.
005A0CD0 cancela ao falhar 005BEED0 ou entrada bit 2, reinicia
ao mudar o trio (005B88D0), recalcula por 005A0090→005A00C0→
005A0140→005A0150. Zona 0 (monstros) chama 005A0530.
005A00C0 escreve máscara em 00A57806 (metade alta de 00A57804).

005A0530 diferencia lados, fase e flags do ocupante. Caminhos
de fase 2/4 consultam 0056B440/0056B5A0 e flags 00A55D7A
(com stride 0x90/0xD44). Se flag 2 não está setada, o ramo
pode ofertar 0x20 e 2; se está, retorna 0x20 ou 0x40
dependendo do bit 1, antes da opção adicional 8.
Fase 3 consulta máscara 00A55D70 e 0056B730: opção 4.
0059FF00 executa 00575210(...,0,0,1) para action 5 ou 6.
Isto liga as duas opções de mudança de posição à lógica
de ações do campo, mas não prova qual imagem corresponde
a cada code. Não duplicar estes checks no Core.

## Ciclo e pedido visual

005B8910: se máscara=0 cancela e chama 005BF4B0; na entrada
procura o primeiro bit permitido seguindo tabela de 13 dwords
005DD980..005DD9B3. Seleção fica nos bits 3..6 de 00A57804.
Enquanto ativa, entrada 0x400 (inclui remapeamento de 0x100
quando 005BEEB0 retorna não zero) percorre a tabela circular,
pulando opções indisponíveis. 005AA480 é consultado para
confirmar a ação. Não identificar 0x400 exclusivamente
como clique direito sem rastrear input.
Se action=12 usa reset; outras chamam 005BF490(action).
005BF490 escreve action+1 em [005F33E4]+108h e byte 1 em
+10Ch; 005BF4B0 escreve zero e byte 1. Xref 0040F777:
0040F670 inicializa 005F33E4 com objeto +27Ch e vtable
005DA594. Não confundir campo de pedido com índice de textura.
A string 005EB6C0 (data/cursor/cursor_?.txt) existe em strings
mas não tem xref direto no índice de refs. Ainda falta localizar
o consumidor de +108h, hot spot e a associação ao manifesto.

NativeCursorActionSelector modela somente o ciclo e pedido
raw, com tabela injetada; testes usam ordem sintética.
A exigência de permutação e recuperação de seleção inválida
são salvaguardas do adaptador, não uma confirmação da tabela
real. Não ligar ao dispatcher de App enquanto faltarem a
tabela, a ABI, o vínculo code→asset e os offsets. O cursor
atual segue inalterado e usa a autorização do Core.

## Próxima evidência

Roteiro 26 (Ghidra): assembly dos cinco métodos, 52 bytes da
tabela 005DD980 e 20 bytes da vtable 005DA594. Verificar
alvos da vtable primeiro no SQLite após importar. Manifesto
cursor_e.txt local, ordem, campos e dimensões sem pixels.
Nenhuma nova rotina Draw contabilizada. V09/V10 Partial:
investigação/modelo, não integração ou Verified.
