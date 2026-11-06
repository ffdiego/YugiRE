# Baseline temporal das demos com `-speedy` — 2026-11-06

## Correção de contexto

**Todas as capturas feitas pelo operador até este checkpoint utilizavam
`-speedy`.** Em particular, os dois JSONL comparados aqui são **ambos
execuções com `-speedy`**. A segunda captura foi produzida usando o novo
`native_rules.py --spawn`, mas isso é uma mudança no momento de instrumentação,
não um experimento *sem* `-speedy`.

**Não há baseline sem `-speedy` nos arquivos analisados.** Portanto não atribuir
à opção qualquer ganho ou perda de velocidade com base nessa comparação.

## Observações dos arquivos

| Arquivo | Linhas (inclui metadata) | Eventos efetivos | Inícios de turno `0x0001` | Intervalos `0x0001 → 0x0050` (ms) | Mediana excluindo primeiro (ms) |
|---|---:|---:|---:|---|---:|
| `commands-20261106-044114-605159-pid12180.jsonl` | 230 | 229 | 6 | 34, 1083, 1080, 1079, 1091, 1082 | **1082** |
| `commands-20261106-050849-199955-pid6248.jsonl` | 274 | 273 | 10 | 32, 1077, 1081, 1082, 1081, 1087, 1071, 1084, 1092 | **1081,5** |

- Os metadados das duas sessões indicam `overwritten=0`.
- A medição é o delta de `timestampMs` de dois eventos do produtor de
  comandos, dentro do mesmo turno, **não** um contador de frames nem uma
  medição do tempo gasto numa chamada específica de `Sleep`.
- O primeiro intervalo é curto nas duas capturas; precisa ser analisado
  separadamente antes de interpretar o restante como espera obrigatória.
- A segunda gravação capturou eventos anteriores à demo, incluindo
  carregamento de recursos na inicialização, graças à instrumentação
  pré-`resume`. Isso não demonstra alteração da velocidade de execução.

Os tempos podem ser recalculados pelo script de análise:

```powershell
python scripts/analyze_demo_timing.py `
  scratch/traces/commands-20261106-044114-605159-pid12180.jsonl `
  scratch/traces/commands-20261106-050849-199955-pid6248.jsonl
```

Os caminhos são exemplos: os arquivos JSONL precisam estar disponíveis nesses locais.

## Investigação seguinte (somente observação)

1. **Localizar o parser de `-speedy`** no EXE, verificando referências
   de strings e o caminho de inicialização. Não assumir a função exata do
   argumento antes de observar os campos/branches que ele modifica.
2. **Rastrear o intervalo entre os comandos** nos handlers associados aos
   endereços `0x005A59B0` e `0x005A41C0`, conforme
   [agendamento de turnos](../20-turn-scheduling.md).
   Descobrir qual condição ou temporizador posterga `0x0050`.
3. **Produzir um baseline controlado sem `-speedy`**, quando o operador
   puder executar uma demonstração comparável, com os mesmos hooks e
   critérios de coleta. Comparar *o mesmo tipo de evento*, condições de
   teste, duração, turnos e resultado; RNG/decks podem tornar as execuções
   não idênticas.
4. **Somente após localizar as esperas**, testar aceleração opt-in da demo.
   Não multiplicar indiscriminadamente o tempo global do Windows, nem
   pular estados lógicos do agendador; verificar comandos emitidos e
   conclusão sem travamentos antes de aumentar a velocidade.

A hipótese de que o intervalo ~1,08 s seja uma espera visual/temporal
controlada **permanece hipótese**, mesmo com a consistência das medidas.
