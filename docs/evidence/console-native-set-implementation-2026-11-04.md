# Implementação experimental de set nativo (2026-11-04)

Branch: `experiment/hybrid-native-probe`; EXE SHA256:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.

## Contrato reutilizado

A análise de Ghidra e os traces reais anteriores de set
demonstraram que `FUN_005AD740(side,handIndex,slot,0,mode)`
inicia a colocação de monstros sem tributo, com
`mode=1` para summon face-up e `mode=0` para set face-down.
O retorno de `FUN_005AAB90` usa máscaras separadas
(candidatas corroboradas pelo pseudocódigo):
`0x01` autoriza summon normal; `0x10` autoriza
set de monstro. A lógica do corpo do Ghidra inicializa
`uVar4=1`, `uVar5=0x10` e pode modificar apenas um
dos valores sob efeitos específicos.

`set check 1 N` valida sem chamar o construtor;
`set 1 N` passa pelas mesmas condições de preflight
antes da chamada `FUN_005AD740(1,N,slot,0,0)`.
O slot é escolhido/revalidado pelo próprio motor
(`FUN_0056A030` e `FUN_00569F70`) para respeitar
Ground Collapse, ocupação e outras restrições.
Todo processamento ocorre no callback da atualização
original do duelo (`FUN_005BC700`), nunca na thread RPC.
A mão é revalidada no tick contra a identidade capturada.

As duas operações consomem `FUN_00579930(side,0x11,1)`,
marcando a flag compartilhada `0x20000`. A recusa
por turno consumido agora inclui `normalSummonUsed:true`
e `playerFlags` em `console.last`, tornando o motivo
verificável no diagnóstico do console. O operador havia relatado
que uma segunda tentativa de summon no mesmo turno foi
barrada no Joey real, mas sem motivo registrado. Uma
captura posterior confirmou explicitamente a flag
`0x20000` após `set`, como detalhado abaixo.

## Testes offline

O suite do agente Frida simulado cobre:
- `set check` e `set`, máscara de elegibilidade
  exclusivamente `0x10`, slot 3 e builder modo 0;
- ausência de mutação/consumo da flag durante o `check`;
- `summon` bloqueado após `set`, e `set` bloqueado
  após `summon`, ambos por `0x20000`;
- recusa de set com apenas bit `0x01` (summon-only),
  e vice-versa;
- rejeições de fase/turno, não-monstro, classe >4,
  ausência de slot, slot bloqueado e identidade da
  mão alterada enquanto a solicitação aguardava;
- cancelamento quando outra operação de colocação
  está ativa; sem invocar o construtor após cancelamento.

Os testes do parser Python cobrem ambos os comandos e
argumentos inválidos.

## Primeiro teste funcional real de set

Fonte: console Python/Frida fornecido pelo operador
em 2026-11-04, `joey_pc.exe` PID 21052;
fingerprint do EXE confirmado pelo loader.

- Mão inicial de side 1: seis cartas, índices
  `[705,221,206,273,369,795]`; cinco zonas de
  monstro inicialmente vazias.
- `set check 1 3` para a carta interna `273`
  retornou `allowed=true`, `eligibility=17`
  (`0x11`), `eligibilityMask=16` (`0x10`),
  `selectedSlot=0`, `levelClass=4` e
  `playerFlags=0`.
- `set 1 3` foi agendado. `diff` confirmou:
  `handCount: 6 → 5`; slot 0
  `- → 273`; palavra de posição
  `0x0000 → 0x0201`. A lista de mão
  passou a `[705,221,206,369,795]`.
- `set check 1 0`, depois da colocação,
  retornou `allowed=false`,
  `normalSummonUsed=true` e
  `playerFlags=131072` (`0x20000`),
  com `detail='Normal summon/set already used
  (player flag 0x20000)'`.

**Conclusão:** o primeiro `set` executado
externamente teve resultado concreto no campo, e
a recusa posterior ocorreu explicitamente por
`0x20000`. A palavra `0x0201` é compatível
com a posição de set esperada, sem exigir
decodificar todos os bits de posição.
Não foi capturado o `console.last` do request
ativo de set, mas a mudança da mão e do campo
confirma a conclusão da operação.

Este teste não valida ainda todas as combinações
de efeitos, tributos, zonas bloqueadas, mudança
de fase ou o modo demo.

## Sequência sugerida para validação

```text
hand 1
state
set check 1 4
# Enter: console.last.allowed, eligibilityMask=16, selectedSlot
set 1 4
# Enter: console.last.state=submitted
diff
state
# Ainda no mesmo turno, após término da colocação:
summon check 1 <outro indice>
# Enter: normalSummonUsed=true / player flag 0x20000
```

Usar monstro conhecido de nível 1–4 no jogador 1,
duelo descartável. A mudança não requer recompilar a DLL.
O comando não funciona para set de magias/armadilhas,
nem para set por tributo ou invocação especial.
