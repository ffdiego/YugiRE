# Handoff: coletas pendentes recebidas — 24/11/2026

Arquivo privado pending-bundles.zip recebido do mantenedor. Os 21 ZIPs
internos foram conferidos contra o SHA-256 original
db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Todos os manifests têm missing=[]; todos os reads/janelas solicitados
nos manifests novos foram validados. Isso é aquisição de evidência,
não verificação de comportamento em execução.

Snapshot privado joey-knowledge-visual-decoded.sqlite → nova cópia
joey-knowledge-visual-cumulative.sqlite; ZIP de handoff inclui auditoria
visual-evidence-import-audit.json. Schema cumulativo compatível com
import_visual_evidence.py; importação adaptada para manter também
os relatórios históricos sem marcadores INS_WINDOW_BEGIN/END,
sem indexá-los como janelas exatas. PRAGMA integrity_check e
foreign_key_check passaram. Base original preservada.

| Tabela | Registros |
| --- | ---: |
| functions | 6518 |
| visual_transient_decompilations | 93 |
| visual_evidence_bundles | 21 |
| visual_evidence_reports | 175 |
| visual_evidence_functions | 281 (215 endereços distintos) |
| visual_evidence_instructions | 167494 |
| visual_evidence_memory | 24 |

Inclui coletas 01–13 (Ghidra), 16, 18–21 e 23, mais os dois
bundles históricos visual-batch e visual-virtual-methods.
O ZIP esperado do roteiro 17 (ghidra-chain-affect-assembly.zip)
não está presente. Há decompilações históricas de chain/affect;
não declarar seus novos operandos resolvidos. Roteiros 14/15/22
não são bundles Ghidra e não estão neste handoff.
ACTIONS e CARDS devem consumir o mesmo SQLite; não interpretar
a coleta como conclusão dos IDs dessas trilhas.

## V07: 00480C20/00480C60, evidência recebida

00480F35..00480F95 confirma chave inicial 36-10*relative,
relative = (side == ((~A54E5C)&1)). Para t<64, chave inicial,
ângulo 2*(64-t). De 64 a 320 inclusive: chave inicial+1+
min((t-64)/16,8), ângulo zero. De 321 a 383: chave inicial+9,
ângulo 2*(320-t). 0048100F..00481024: t>=384 desativa tipo
(A0=-1) e solicita limpeza. Se chave > inicial, virtual +C
recebe chave-1; isso não significa frame desenhado.
00480FA8..00480FFD passa centro (525,300) e ângulo para
004223F0. O compositor é um quad com perspectiva, não
simples rotação 2D. A base de preload (27+10*relative)
e a base de Draw (36-10*relative) realmente diferem.
NativeTurnNoticePlan.DrawAtCounter porta os argumentos exatos;
não presume índice físico nem troca cor por nome de arquivo.

Vtables lidas: 005DBB1C slots 0..3 = 00480B20,00480B10,
00480C20,00480C60; 005DBB2C = 004811D0,004812B0,
00481370,00481420. Os predicados 00480B10 e 004812B0
ainda precisam resolução; não confundir com contadores de Draw.
Sem novo gate. Corrigido CS1673 na fixture anterior (captura de
membro do struct dentro de lambda).

## V11: origens 005DB5B0:80

Conforme 0045C280, os pares int32 little-endian são:

| Descritor | Lado próprio | Lado oposto |
| --- | --- | --- |
| C | 284,460 | 766,140 |
| D | 760,460 | 290,140 |
| E | 760,366 | 290,234 |
| 10 (decimal) | 286,353 | 766,236 |
| F | 762,308 | 292,292 |

Para D, 0045C140 com slot=0 usa ajuste X vivo
005F32D8[side], somado no próprio e subtraído no adversário.
Não supor zero sem evidência. NativeSelectionOrigin exige esse
argumento explicitamente, porta os pares estáticos, não cria
fila nem abre/fecha a janela. O modelo ainda exige cosseno
capturado e composição do Draw. Cobertura 20/34; não promover
Partial/Verified só pela aquisição.
