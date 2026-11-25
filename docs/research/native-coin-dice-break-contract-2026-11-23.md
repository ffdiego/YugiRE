# V20/V21/V22 — bancos coin/dice e remoção break

> **Errata (25/11):** a [auditoria do despacho](native-visual-dispatch-opcode-audit-2026-11-25.md) confirma que a coluna histórica abaixo mistura índices do switch e opcodes brutos. Os opcodes reais são E0/E1 para coin, E2/E3/E4 para dice, 79 para break e 7A/7C para explosion. Os produtores de 8E/8F/90/91/92 não provam as famílias coin/dice; não usar aquelas associações na integração A03.

Fonte: cópia privada joey-knowledge-visual-decoded.sqlite, quick_check ok,
SHA256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Consulta read-only às tabelas decompilations, refs, strings, pointers,
visual_virtual_methods e visual_transient_decompilations. Não houve novo
dump do EXE, captura do duelo ou inspeção do DAT nesta rodada.

## Rotas (decompilação e Xrefs; confirmar assembly)

| Opcode do dispatcher 005B9470 | Handler | Construtor | Banco | Alocação |
|---|---|---|---|---|
| 8E | 0040A7C0 | 004571D0 | coin.txt | 0x134 |
| 8F | 0040A900 | 004571D0 | coin.txt | 0x134 |
| 90, 91, 92 | 0040A6A0 (direto ou thunk 0040A690) | 004568C0 | dice.txt | 0xAC |
| 4F | 00404B40 | 00450C30 | break.txt | 0x104 |

O rótulo do banco **não define a semântica das regras**. Por exemplo,
005758A0, 00575900, 00575A80, 00575AF0, 00575C40, 00575D50, 00575FE0,
00576050, 00578D00 e 00579020 enfileiram 8E por 005B9170;
00575AF0 é caminho de movimentação/chegada ao cemitério já identificado
por CARDS. 00576110 empacota 90 com diversos bytes; ainda não atribuir
resultado de dado a um desses bytes. Não gerar RNG no renderer. Não
concluir que toda movimentação 8E desenha moeda: é preciso verificar
o ramo/seleção do objeto e a ABI de 004571D0.

Os três handlers de coin/dice criam o objeto somente quando
(DAT_00A558AC & 0x7F)==0, incrementam a fase baixa, adicionam o objeto
ao contêiner 005F3280+132C/1330 e retornam enquanto houver objeto ativo.
Quando todos liberam, limpam 0x4000 de DAT_00A558AC. Esse é um gate
real de apresentação, não uma duração fixa. A04/A03 deverão
reutilizar a fila; não criar uma segunda fonte de mutação.

O descritor de âncora é o ushort truncado de
((0xC000 | region)<<1) | side, portanto 0x8000|(region<<1)|side.
side = bit 15 da palavra A em 00A550A0; region = 5 bits do byte
baixo de 00A550A4 no dado, ou byte em 00A550A6 (baixo da palavra D)
na moeda. Corrigido com assembly recebido: 0040A809 / 0040A92E leem
A550A6, 0040A6BB lê A550A4. O byte A550A5 não é a região da moeda. A posição final é retornada por 0045C140, não assumir
centro da seleção (525,300). Coin passa valores derivados de
comparações com zero e 1/3; dice passa um valor de 16 bits.
A pilha dos construtores é mal reconstruída; não tipar ainda os
parâmetros como contagem de lançamentos ou faces.

## V20 coin

004571D0 instala vtable 005DB404, camada 0x110, +10=-128.
A branch analise-visual já documenta 00457500: incremento 8 e
comparação com (+14+4)*128. Fonte secundária:
docs/research/visual/animation-formats.md na revisão
30e63ac875556c551e2c43bab3c5c989bb63a91e.
Não portar um relógio terminal sem a instrução de comparação,
semântica +14 e gate de Draw. Recursos históricos: coin_rotate.bmp
14 padrões, coin_sha.yga 168x42 / 4, coin_kirari.yga 56x14 / 4.
Nenhum frame ou duração absoluta é deduzido da largura.

## V21 dice

004568C0 instala vtable 005DB3F4, camada 0x110, +10=-256.
A mesma branch confirma dice_shadow.yga 288x48 / 6 regiões 48x48.
Isso não prova seleção de face ou sequência do atlas principal.

## V22 break

00404B40 (4F) no estado 1 consulta 00570230 e constrói 00450C30
apenas se há ocupante; no estado 2 ocorre atualização de flags,
remoção e transferência; estado 3 pode notificar; estado 4 aguarda
objetos ativos antes de liberar 0x4000. O construtor instala
005DB314, começa contador +10=0, guarda descritor +1C e centro
retornado por 0045C140 em +14/+18. Se (descriptor&0x3E)==0x16,
a chave de camada = 0x201+((descriptor>>6)&255)*16, caso contrário
0x110. Carrega break.txt, chama 0041F0C0 com gate face-up e pede
som 0x10000008. Explosão 004511D0/004514B0 tem outro banco, camada
fixa 0x110 e som 0x10000007. Não substituir todo Destroy por break
nem confundir 4F com 50.

## Limite do snapshot e próxima coleta

As seis Update/Draw do inventário (00457500/00457520,
00456B70/00456B90, 00450FB0/00450FD0) não existem nas duas tabelas
de decompilação do SQLite. Os ponteiros dos slots 0 das três
vtables estão indexados (destrutores 004573B0/00456A30/00450E60);
demais slots ausentes no índice esparso, não presumir zero.
O roteiro 16-coin-dice-break-ghidra.sh exporta somente estas seis rotinas, assembly
dos construtores e handlers (ABI), três janelas de 16 bytes.
A coleta deve validar os slots +8/+C e as fórmulas de recorte,
contadores, gate e camada. Reaproveitar --inspect-visual-dat
para os três manifests e dimensões, sem enviar pixels/EXE/DAT.
Preferir importar as novas decompilações em cópia cumulativa do SQLite,
com provenance transitória e fingerprint, após receber ZIP.
Não repetir os bundles 01–04.

NativeEffectCommandRouting porta apenas a decisão de família,
descritor e camada conhecidos; testes sintéticos não são prova
de paridade. Sem nova Draw integrada, badge permanece 20/34, 0 Verified.
