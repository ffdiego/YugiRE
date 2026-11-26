# P201 — Mirror Force: janela de declaração de ataque (sandbox)

**Status:** Implementação C# com testes sintéticos, **sem comparação
diferencial com `joey_pc.exe`**. Branch empilhada em #19/P196+P038; não altera
`main`.

## Identidade, texto e contexto

`docs/evidence/card-callback-catalog.json` registra Mirror Force como
`resourceId=1056`, `internalIndex=467`, callback nativo P201. O
`data.dat` classifica a carta como Trap (classe DAT 21).
Texto de referência:

https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4887&ope=2

Regra no sandbox: **quando um monstro do adversário declara um ataque**,
o defensor pode ativar Mirror Force previamente Set. Destrói **todos
os monstros em posição de ataque do atacante**, incluindo aqueles que
não estão participando desse combate. Não destrói monstros em defesa,
face-down nem os monstros do controlador da Trap.

## Separação de estados: antes da Damage Step

O antigo `Attack` avançava diretamente para cálculo de dano, revelação
e efeitos FLIP. Agora há duas etapas:

1. `Attack` valida o atacante, alvo ou ataque direto e **consome a
   declaração de ataque uma vez**. Se existir Trap elegível, cria
   `PendingAttackResponse` antes de qualquer dano ou revelação.
2. `PassAttackResponse` limpa a decisão e chama
   `ResolveDeclaredAttack` diretamente — não faz uma segunda
   declaração. Se não havia Trap, a continuação é imediata, mantendo
   os cenários anteriores.
3. `ActivateAttackTrap` valida ocorrência da Trap na zona, classe DAT,
   turno de colocação e atacante original. Consome a Trap e usa
   `MonsterWipeRules.AttackPositionTargets` + `ResolveMonsterWipe`
   para destruir os monstros em ataque. O ataque não alcança dano,
   batalha ou revelação do defensor; nenhuma nova janela é aberta.

A destruição por Mirror Force é **por efeito**, não por batalha;
recrutadores P164/P168 não disparam. O wipe retira todas as
ocorrências do campo antes de abrir efeitos de envio ao GY (p.ex.
Sangan e Witch). `LastMonsterWipe` e `LastAttackTrap` expõem
contagens auditáveis, não um trace nativo.

O mesmo evento oferece Mirror Force se a declaração for de ataque
**direto**: a defesa ocorre antes da aplicação dos LP. Uma Trap recém
Set no mesmo turno ainda não é elegível. Trap Hole e Torrential Tribute
continuam utilizando a janela de **invocação**, que não foi confundida
com a declaração do ataque.

## Por que isso não é ainda uma engine de correntes?

A máquina de estados precisa reconhecer o evento e pausar o combate,
mas uma *chain* real acrescenta:

- alternância de prioridade e oportunidade de acrescentar Chain Links;
- resolução LIFO (último link ativado resolve primeiro);
- efeitos disparados e decisões após o término da corrente;
- negação, alterações de alvo/campo durante a corrente, timing de
  ativação e possíveis replays do Battle Step.

Neste recorte há **uma resposta opcional sem chain**. A interface
`AttackResponseScreen.cs` é provisória, não o layout do jogo
original, e não deve ser considerada prova de timing nativo.
O contrato `DemoAttackDeclaration` conserva identidade do atacante,
zona/alvo e se a declaração foi direta; essas informações ajudarão
na evolução para um resolvedor de eventos e correntes.

## Regressões

`src/tests/Joey.Tests/AttackTrapChecks.cs`: identidade P201, tipo DAT,
elegibilidade adversária, declaração antes da Damage Step, destruição
de todos os ATK (não apenas atacante) e preservação de DEF/face-down,
ataque direto ativado ou passado, continuação única com FLIP de
Mask of Darkness, conservação de Trap no Pass, interações com
Sangan/Witch, ausência de efeito de batalha para recrutadores e
ausência de Trap mantendo o combate anterior.

**Condição para `Verified`:** traços nativos suficientes para
diferenciar declaração, decisão, destruição, gatilhos pós-corrente
e estado final. O texto atualizado das cartas não substitui
verificação das diferenças do Joey original.


No piloto direto da fila, App, CPU e console usam a mesma resposta.
Mirror Force reutiliza o lote de destruição. A armadilha Set é gasta
antes da primeira destruição. Cada monstro em ataque aberto recebe um
comando próprio, e o resultado aguarda o último gate. O piloto aceita
aliados normais já cobertos (IDs 24 e 1351) e preserva os monstros em
defesa. A janela também atende ataques com alvo dentro do piloto de combate,
antes de revelar o defensor. Efeitos de campo fora do piloto mantêm
a resolução legada.
