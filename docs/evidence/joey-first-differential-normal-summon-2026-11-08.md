# Primeiro teste diferencial parcial: invocação normal Joey → Joey.Core

**Data:** 2026-11-08. **Status:** implementação e validação estática de fixtures concluídas. **A nova suíte .NET ainda precisa de execução no Windows.**

## Objetivo

Fazer a implementação própria `DemoDuel.Summon` executar uma
invocação normal a partir de um estado **parcialmente observado**
no executável original e confrontar somente os campos que
foram realmente capturados. Esse é um **primeiro teste
diferencial de uma ação isolada**, não uma reprodução automática
da sequência de comandos YDR/JSONL e nem equivalência do
estado inteiro do motor nativo.

O usuário informou **144 checks passed** no `Joey.Tests`
antes desta alteração (incluindo cinco novos checks do
`NativeDefaultQueuePacket`).

## Casos com evidência real

### Caso A — invocação na zona 0

Fonte primária resumida:
`docs/evidence/console-native-summon-live-2026-11-04.md`
(log de console/state fornecido pelo operador, não-atomicidade).

- Jogador: lado 1; LP observado: 8000 antes/depois.
- Mão antes (índices internos):
  `[895, 467, 894, 1016, 582, 221]` (6 cartas).
- Campo do jogador: 5 zonas de monstros vazias.
- CPU: carta interna 1052 no slot 0.
- Preflight nativo `summon check 1 5`:
  `levelClass=4`, `eligibility=0x11`, `selectedSlot=0`.
- Ação executada: `summon 1 5`.
- Estado posterior observado: mão 5, lado 1 slot 0 contém
  a carta interna **221**, LP permanecem 8000.

Fixture versionado:
`src/tests/Joey.Tests/Fixtures/console-normal-summon-side1-221.json`.

### Caso B — invocação na zona 1

Fonte:
`docs/evidence/windows-native-second-monster-slot-and-field-2026-11-04.md`
(watch 250 ms e comando nativo `0xCE` do mesmo intervalo).

- Jogador: lado 1; slot 0 já contém carta interna 221.
- Mão antes: `[272, 1016, 261, 795, 705]` (5 cartas).
- Ação: carta interna **705**, posição 4 da mão, slot 1.
- Comando `0xCE`: `ceNativeState.context=0x4103`
  (lado 1, mão índice 4, slot 1), `source=705`,
  `stage=37`, recurso `0x010C` (268).
- Observação posterior: mão
  `[272, 1016, 261, 795]` (4 cartas),
  carta interna 705 no slot 1, posição nativa `0x0202`.
- LP não foram fornecidos nesse trecho e **não são testados**.

Fixture versionado:
`src/tests/Joey.Tests/Fixtures/watch-normal-summon-side1-705-slot1.json`.

## Implementação

- `src/Joey.Core/Demo.cs` agora oferece
  `DemoDuel.FromPartialObservation(...)`.
  Essa rotina configura explicitamente: lado em turno,
  mão conhecida, cinco posições de monstro por lado
  e LP ativo quando observado. **Não é restore/savestate
  nativo.** Deck, outras mãos, flags, fase, cemitério
  e correntes ficam com valores padrão do demo.
- `src/tests/Joey.Tests/DifferentialNormalSummonTests.cs`
  lê os dois fixtures, garante consistência do evento
  com a mão inicial e do `CE context` opcional,
  chama efetivamente `DemoDuel.Summon` e testa:
  1. mão reduziu em uma carta;
  2. quantidade posterior corresponde à observada;
  3. índice da carta na posição de campo corresponde;
  4. LP coincide **somente** no caso que os observou.
  Inclui um controle negativo que altera a carta esperada;
  o comparador deve rejeitá-la.
- `Joey.Tests.csproj` copia `Fixtures/*.json` para
  o diretório de execução; `Program.cs` chama
  `DifferentialNormalSummonTests.Run(Check)`.

**Metadados não conhecidos:** os recursos e propriedades de
várias cartas não estavam presentes nas observações.
O harness usa instâncias `Card` com índice interno real,
IDs opacos `0xFFFF` quando desconhecidos e propriedades
sintéticas. Para o alvo 221 foi observada a classe nativa
4; para 705 a classe não aparece no registro e foi usada
uma aproximação de monstro comum nível 4 para permitir
a execução de `Summon`. **Este teste verifica a
movimentação observada no campo e na mão, não a legalidade
completa de nenhuma das duas invocações.**

Os métodos nativos `FUN_005AAB90`,
`FUN_005AD740` e `FUN_005ABD00` possuem regras
adicionais ainda não representadas por esse fixture.

## Validação local e próxima etapa

Após atualizar a branch:

```powershell
dotnet run --project .\src\tests\Joey.Tests\Joey.Tests.csproj
```

Os fixtures foram validados quanto à coerência de
`side/handIndex/sourceCardIndex/fieldSlot` e do
`0x4103` com o documento de evidência. **Não há
SDK .NET neste ambiente para executar/compilar a nova
suíte.** Não afirmar que os novos checks já passaram.

**Próximo incremento para automatizar a regressão:**
instrumentar uma ação de invocação no YugiLab e capturar
um `before/after` minimamente atômico (ou pausar/sincronizar
a thread do motor com segurança), contendo mão completa
antes/depois, slot, identidade interna da carta, lado,
LP, flags e comando. Então exportar o mesmo schema para
substituir os fixtures transcritos. A amostragem atual
pode perder transições intermediárias; não atribuir a ela
causalidade atômica.
