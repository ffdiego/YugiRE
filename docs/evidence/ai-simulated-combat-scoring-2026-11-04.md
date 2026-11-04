# IA do Joey: pontuação e ordenação de confrontos simulados

Data: 2026-11-04. Relatório Ghidra fornecido pelo operador:
`ai-simulated-combat-scoring.txt`, 284 linhas no arquivo local.
Alvo: EXE SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Análise estática, sem execução ou injeção de funções novas.
Complementa [o ciclo de save/restore](ai-simulation-state-lifecycle-2026-11-04.md)
e [os seletores da mão](ai-hand-selection-2026-11-04.md).

## Descoberta central: baseline e hipótese usam a mesma função de score

`FUN_005B08B0(side)` é um wrapper:

```cpp
int EvaluateBaseline(uint32_t side) {
    FUN_005B08E0(side); // snapshot dos dois campos de monstro
    FUN_005B09C0(side); // prepara flags do lado atual
    int score = FUN_005B0A50(side);
    FUN_005B0950(side); // restaura campos, conserva resultados auxiliares
    return score;
}
```

O seletor `FUN_005B1360` já documentado inicializa
`local_1c = FUN_005B08B0(side)`. Depois, para cada
carta candidata legal, faz snapshot, prepara a invocação
hipotética via `FUN_005B1610`, calcula
`iVar6 = FUN_005B0A50(side)` e restaura o campo.
O seletor compara `iVar6` com `local_1c` e,
nos ramos de melhoria/empate, atualiza o índice da
mão selecionado e o limiar de score.

**Conclusão:** há comparação de pontuação **do
estado atual versus estados após jogadas hipotéticas**,
com critérios adicionais de desempate. Não se trata
apenas de escolher maior ATK ou carta aleatória.
Isso **não** demonstra busca minimax, múltiplos turnos
ou que a pontuação inteira seja dano em LP.

## FUN_005B0B90(side): seleção ordenada de possíveis ações

1. Itera slots 0..4 do lado fornecido e usa
   `FUN_0056B730(side,slot,1)` para montar uma
   lista dos participantes elegíveis (regra exata
   de elegibilidade ainda não decompilada).
2. Faz uma ordenação por seleção `selection sort`
   dos slots listados. A chave é:

```cpp
uint32_t key = (priorityClass << 16) | (calculatedATK & 0xFFFF);
```

O valor de ATK vem de `FUN_0056F690(side,slot)`,
documentada no projeto como ATK calculado. As classes são:

| Classe | Recurso da carta | Observação |
| --- | --- | --- |
| `1` | `0x5F3` | Primeiro grupo |
| `2` | Demais recursos | Grupo padrão |
| `3` | `0x2F9`, `0x226`, `0x2E8` | Último grupo |

A comparação usa `key < bestKey`, portanto as
classes menores são consideradas antes; entre
integrantes da mesma classe, o ATK nos **16 bits
baixos** influencia a ordem de forma crescente.
Os recursos especiais não estão mapeados no
catálogo parcial de callbacks; **não atribuir
nomes sem outra fonte**.

3. Na ordem construída, chama
   `FUN_005B0D90(side,slot,1,&event)`.
   Se retorna não zero, copia quatro bytes de
   `event` para `DAT_00A579A6`, marca bit
   `0x04` e retorna `1`. Caso não encontre
   ação, retorna `0`.

A presença de ordem de consideração **não prova
que seja a ordem definitiva de ataque no duelo**.
`FUN_005B0D90` ainda precisa de decompilação:
não sabemos como escolhe alvo, verifica restrições,
ou calcula `DAT_00A579A8`.

## FUN_005B0A50(side): acumulação de pontuação

A análise anterior já mostrou a rotina:

```text
zerar bits auxiliares e bitmask de ações
if (!FUN_005A0DD0(side)) return 0;
while (FUN_005B0B90(side)) {
    registrar slot envolvido (DAT_00A579A6)
    se bit 0x400: limpar identidade do monstro próprio
    se bit 0x800: limpar identidade do monstro rival
    total += DAT_00A579A8
}
return total;
```

Aqui há uma **sequência de possíveis confrontos
dentro de uma única avaliação hipotética**, pois
os bits `0x400/0x800` removem identidades
de monstros para as próximas iterações.
Isso é compatível com análise de trocas;
não se sabe ainda se `DAT_00A579A8` é
dano, diferença estratégica, pontuação arbitrária
ou outra métrica.

A função `FUN_005A0DD0(side)` impõe gates:
- duelo ativo: `DAT_00A577F8 != 0`;
- `playerFlags[side] & 0x40000 == 0`;
- chama `FUN_005A0D60(&DAT_00A55D64,side,0,1)`;
- exige valor não zero em um campo auxiliar
  de `DAT_00A55D6C + side*0xD44 + 2`.

**Importante:** `0x40000` é uma flag distinta
de `0x20000` usada para consumo de summon/set.
O gate não é leitura pura: chama outra função.
Não nomear `0x40000` como ataque usado ou
fim de turno sem evidência do seu significado.

## Função de cópia FUN_005BDE10

A decompilação confirma um `memcpy(dst,src,size)`
elementar em blocos de 4 bytes e bytes residuais.
Isso reforça o entendimento de snapshots
`0x2D0` bytes por lado, embora continue
não havendo transação completa de todas as
globais durante a simulação.

## Filtro final do seletor de cartas (assembly)

Em `FUN_005B1360`, após
`CALL FUN_005B0950` (`0x005B153F`),
o caller:

1. Lê um DWORD em `[ESP+0x60]` e exige
   `>= 0x384` (**900**) para prosseguir.
   O significado exato dessa saída ainda
   depende do layout de pilha de
   `FUN_0056DD30`; não identificar
   automaticamente como ATK.
2. Lê a metade baixa de `DAT_00A579AC`
   e testa `1 << selectedSlot`, usando
   `EDI` como slot vindo de
   `FUN_005B1610`.
3. Compara o score de `FUN_005B0A50`
   (preservado em `EBP`) com o melhor score
   anterior em `[ESP+0x18]`.
4. Inclui desempate por bits do estado auxiliar
   `DAT_00A579AA` e por uma posição de
   campo anteriormente selecionada.
5. Se aceita, escreve o índice da mão em
   `[ESP+0x24]` e armazena novos parâmetros
   de score/desempate.

A checagem é **multicritério**: pontuação,
threshold 900, bitmask de slot e desempates.
Portanto `bestScore = max(scores)` é apenas
uma abstração, não a especificação integral.

## Evento de combate explicado em extração posterior

O relatório de `FUN_005B0D90` comprovou:
- rota especial com destino numérico `5` (possível
  ataque direto, ainda sem confirmação);
- rota de teste de cada slot adversário com
  `FUN_0056BD80` e `FUN_005B05A0`;
- `FUN_005B0F40` preenchendo descritor
  com flags e valor de 16 bits;
- comparação dos resultados assinados para
  escolher melhor evento e desempate por DEF;
- `FUN_0056B730` filtrando monstros por
  posição, máscara e gates de regras;
- `FUN_005A0D60` construindo o bitmap dos
  slots elegíveis.

`DAT_00A579A8` é a **segunda palavra** do
descritor de quatro bytes a partir de
`DAT_00A579A6`. Isso confirma a origem
estrutural do valor acumulado; seu significado
estratégico exato depende de `FUN_005B0F40`.
Ver [análise de alvos e evento](ai-combat-event-producer-2026-11-04.md).

## Próxima investigação estática recomendada

**Prioridade 1: `FUN_005B0F40`** para entender
como o valor de 16 bits e as flags são calculados.

**Prioridade 2: `FUN_005B05A0`** para confirmar
restrições de combate e o destino `5`.

**Prioridade 3: `FUN_0056BD80`,
`FUN_0056BF20`, `FUN_0056BFD0`**
para mapear o alvo e as condições da rota especial.

```powershell
.\scripts\Run-ArchiveQuery.ps1 `
  -Report ai-combat-outcome-scoring.txt `
  -Addresses @(
    '005b0f40',
    '005b05a0',
    '0056bd80',
    '0056bf20',
    '0056bfd0',
    'ins:005b0d90:85'
  )
```

O último recorte permite conferir a seleção e
a ordenação em assembly, caso a saída decompilada
fique ambígua.

**Não habilitar chamadas ativas desses helpers via
Frida**: usam buffers globais e alteram estados
de campo hipotéticos; a observação passiva ainda
precisa distinguir janela de simulação do estado real.
