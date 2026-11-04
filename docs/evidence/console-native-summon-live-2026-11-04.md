# Primeiro summon externo validado no Joey real (2026-11-04)

**Fonte:** log do console Python fornecido pelo operador, execução no
Windows com `joey_pc.exe`, PID 9040. O loader informou que o
SHA-256 do executável carregado coincide com o fingerprint documentado.
A build de `yugire_rules.dll` manteve ABI 6. Não houve neste teste
um dump JSONL de comandos ou resultado de `console.last` após
`summon 1 5`: a conclusão da execução usa os snapshots de duelo.

## Estado inicial do jogador 1

- LP 8000, mão com 6 cartas, deck 34;
- mão (índices internos): `[895,467,894,1016,582,221]`;
- zonas de monstros `[-,-,-,-,-]`;
- CPU com monstro interno `1052` no slot 0.

## Preflight em execução real

- `summon check 1 4`: carta interna `582`, recusada antes de
  consultar a elegibilidade final, com
  `allowed=false` e mensagem
  `Only ordinary level 1..4 monsters without tributes are supported`.
  Isto prova apenas que a classe retornada não estava na faixa
  1..4 suportada pelo executor; não identifica isoladamente
  se era de nível alto ou classe especial.
- `summon check 1 5`: carta interna `221`,
  `expectedWord=134222045`, `allowed=true`,
  `eligibility=17 (0x11)`, `levelClass=4`,
  `selectedSlot=0`, `state=checked`.
  A solicitação foi atendida, sem uma alteração observada de
  quantidade de cartas ou monstros atribuível ao diagnóstico.

## Execução ativa

Operador emitiu `summon 1 5`, que o console aceitou como
solicitação pendente (request ID 3).
No `state` subsequente:

| Campo | Antes | Depois |
| --- | --- | --- |
| Mão do jogador 1 | 6 | 5 |
| Monstro do jogador no slot 0 | vazio | 221 |
| Monstros nos slots 1..4 | vazios | vazios |
| LP do jogador | 8000 | 8000 |

A coincidência do índice interno 221 e a mudança de mão/campo
**confirmam o efeito concreto de uma invocação externa
bem-sucedida** em um duelo real do Joey; superam a mera
observação de um `state=submitted`.

O operador executou `state` antes de `diff`; no console
existente, `state` **atualiza a baseline de `diff`**.
Portanto `[diff] No changes in the captured fields` não
significa falha: o baseline já era o snapshot pós-summon.

O warning `Unable to start idund: idund did not start listening`
não bloqueou esta sessão: houve attach, fingerprint
confirmado, instalação e execução funcional. Sua causa
não foi investigada.

## Validação restante

- Não foi capturado o `console.last` da request ID 3;
  é recomendado confirmar o status `submitted` e o estado
  da flag `0x20000` em próximo teste.
- Repetir tentativa de summon no mesmo turno para confirmar
  rejeição por `Normal summon already used`.
- Testar `summon check` com Ground Collapse e slots 0/1
  proibidos, assegurando seleção de uma zona disponível,
  e depois executar uma invocação em duelo descartável.
- Repetir com trace ativo para correlacionar o construtor
  e a produção `0xCE`, se surgir discrepância.

Nenhuma necessidade de ampliar o executor para tributos,
efeitos especiais, CPU ou autoplay com base só nesta captura.
A mudança foi validada funcionalmente **em um caso**.
