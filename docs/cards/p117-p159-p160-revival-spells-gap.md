# P117/P159/P160 — ressurreição: lacuna anterior à migração

Consulta somente leitura, 2026-11-24, SHA-256 do EXE
db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.
Ambos joey-knowledge-visual-decoded.sqlite e visual-cumulative.sqlite:
card_callback_slots catalogam os endereços abaixo; decompilations e
visual_transient_decompilations não retornam os sete endereços distintos.
O cumulativo passa quick_check. Não é prova de ausência no executável.

| ID | Perfil | Disponibilidade | Preparação | Primário | Alvo |
| --- | --- | --- | --- | --- | --- |
| 1008 Monster Reborn | P117 | 00580710 | — | 005886C0 | 00593530 |
| 1095 Call of the Haunted | P159 | 005816E0 | — | 0058A4A0 | 00593530 |
| 1160 Premature Burial | P160 | 00581C90 | 0057C5C0 | 0058A4A0 | 00593530 |

Nenhuma das três consta em grupos do manifesto de jogabilidade atual;
não há resolvedor específico em DemoDuel. Portanto não são uma
migração C12 de cartas Done. Não habilitar por analogia a Spear Cretin.

Problema estrutural: CardTarget.Side localiza a ocorrência, mas não
representa proprietário original independentemente do controlador.
RemoveMonster envia a graves[p]; ReturnMonsterToHand usa hands[player]
ou fusionDecks[player]. Simplesmente permitir Side de origem diferente
do destino em SpecialSummonCommand tornaria saídas futuras incorretas
para Monster Reborn adversário. Coordenar A06/A08 (e mudança de controle
de C17) antes de habilitar essa primitiva cruzada. Não criar uma segunda
tabela paralela de propriedade ou fila.

Premature Burial e Call precisam esclarecer regras de custo,
seleção/legitimidade da invocação e vínculo persistente bidirecional:
destruição vs retorno/banimento/tributo, negação e remoção do próprio
vínculo. Não confundir com equipamento de atributos. O primário e a
preparação de alvo comuns não demonstram equivalência dos efeitos.
Roteiro 16 coleta os sete VAs e quatro helpers já relevantes para
remoção/ligação, com assembly e retry-missing. Próxima evidência:
importar bundle privado em cópia cumulativa e verificar chamadas/opcodes,
filtros, origem/destino, ordem de custo e limpeza. Se lacuna persistir,
documentar cenário de trace local, sem afirmar paridade.
