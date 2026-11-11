# Observação passiva dos pontos de vida em duelos normais

Implementado em 2026-11-10; **validado dinamicamente no Windows**
num duelo inteiro capturado em 2026-11-11 UTC.
Resultado: 29 snapshots reais de LP, zero erros, 14/14 transições
de dano conferindo com o estado observado e CPU terminando com 0 LP
contra 300 do jogador. [Evidência e tabela completa](../evidence/normal-duel-lp-ai-live-validation-2026-11-11.md).
O procedimento abaixo continua útil para novas capturas.
O agente mantém o mesmo hook passivo de `FUN_005B9170`
(`trace on`), sem novos trampolins, alterações de memória, DLL nativa ou
chamadas a funções do jogo. A captura funciona com ou sem `trace ai on`.

## Evidência que motivou a mudança

O JSONL real `commands-20261111-015433-637065-pid41940.jsonl`,
SHA256
`b24e67fccbb79e23df446eb3f36e24c3d57107003691e6ae4ea89561f260bba7`,
contém 1.583 eventos (zero perdas), com nove comandos mascarados
`0x0043` e um terminal `0x8004` no evento 1583.
O operador confirmou ter **vencido** o duelo.

Na amostra, `args[1]` nos nove comandos `0x0043` corresponde a
`1900, 2950, 1900, 1700, 400, 1700, 1300, 800, 1500`;
o bit 15 de `args[0]` distingue lado 1 e lado 0 conforme esperado.
A soma do dano enfileirado é 5.500 para o jogador/lado1 e
8.650 para a CPU/lado0. Partindo hipoteticamente de 8.000 LP
e supondo que todos os comandos foram aplicados como dano,
o resultado seria jogador2.500 e CPU0 (com saturação em zero).
**Isso é reconstrução pelos comandos, não histórico observado dos LP reais.**
O comando `0x0043` é conhecido como mudança de LP; outras fontes de
alteração, cancelamento, cura e timing da resolução impedem tratar a
reconstrução como fato observado.

## Novo contrato de captura

Em cada entrada do produtor, durante um duelo ativo, o observador lê
`duelRunningWord` em `0x00A577F8` e os dois LP uint16:

- `side 0` (CPU): `0x00A55D64`.
- `side 1` (jogador): `0x00A55D64 + 0xD44`.

O campo `lifePointsObserved` é adicionado ao **mesmo evento** `command`
quando há a primeira leitura do duelo, mudança em qualquer LP,
comando de LP `0x0043` ou terminal `0x0004` / `0x8004`.
Um comando comum sem mudança não duplica o snapshot.
Exemplo esquemático:

```json
{
  "kind": "command",
  "opcodeMasked": "0x0043",
  "lifePointsObserved": {
    "lifePoints": [8000, 6100],
    "duelRunningWord": 1,
    "samplePoint": "command_producer_onEnter",
    "reason": "lp_command"
  }
}
```

O `reason` pode ser `first_active_sample`, `changed`,
`lp_command` ou `terminal`. No terminal nativo conhecido
(`caller=0x005BC545`), `terminalNativeState.lifePoints`
recebe a mesma amostra ou `null` em caso de falha.
Falhas de leitura durante `0x0043` ou terminal emitem
`lifePointsReadError` e não impedem a execução do jogo.
O cache de comparação é reiniciado ao iniciar um trace, ao sair do
estado ativo e depois do terminal.

**Temporalidade obrigatória:** o sample ocorre em `onEnter` do
produtor, **antes de enfileirar o comando atual**. O LP pode continuar
inalterado no evento `0x0043`; a alteração aplicada deve aparecer
no **próximo** comando produzido depois da execução do efeito.
Esse mecanismo não registra mudanças entre comandos quando nenhum
outro comando é produzido, nem mede todos os frames do duelo.
Evitar rotular `lifePointsObserved` como resultado do comando atual
ou inferir vencedor apenas de `0x8004`.

Não há alteração no número total de eventos: o snapshot está embutido
nos eventos `command` já existentes, preservando a sequência JSONL.
Observações continuam sujeitas ao buffer limitado e às ressalvas de
leituras não atômicas do agente.

## Protocolo Windows

Manter backup do save. Executar o Joey **manualmente** e anexar na raiz do
repo, sem `--pid` se houver uma única instância:

```powershell
git pull --ff-only
node .\native_rules\tests\test_loader.cjs
.\native_rules\.venv-frida\Scripts\python.exe .\native_rules\native_rules.py --observe-only
```

No console do observador:

```text
trace on 20000
trace ai on
trace mark lp-ia-duelo-normal
```

Jogar um duelo até o fim. Depois:

```text
trace ai off
trace off
trace dump
stop
```

O `trace ai on` é opcional para observar LP, mas útil para cruzar
as decisões da IA com os LP e os comandos do mesmo duelo.
Não ativar o observador de deck nessa sessão, para manter a
instrumentação simples.

Na captura, conferir:
1. `metadata.overwritten == 0`, `seen == retained` e eventos sequenciais.
2. Um primeiro snapshot de LP, um `0x0043` com LP antes da resolução
   e a primeira mudança subsequente (se houver próximo comando).
3. O terminal `0x0004`, seu `terminalNativeState.lifePoints`
   e os LP observados. Comparar com o vencedor conhecido.
4. Se houver divergência com a soma de dano, **investigar**: cura,
   mudanças que não geram `0x0043`, saturação, LP inicial distinto,
   efeitos atrasados ou argumento do produtor que ainda não entendemos.

## Resultado do primeiro teste real

O operador executou o protocolo e enviou
`commands-20261111-022940-355228-pid36348.jsonl`.
O arquivo tem 1.120 eventos, sem perda, 649 comandos e
470 eventos de `ai_probe`. As leituras de LP reais concordam
em 14/14 pares de alteração de dano, inclusive a saturação no
golpe final (CPU500 -> 0 diante de comando de dano1000).
Os 12 comandos `0x00CE` do lado CPU também concordam com o
índice de carta do último seletor válido observado.

A descrição completa, os números por evento, SHA256 do JSONL
e ressalvas estão na
[evidência de validação](../evidence/normal-duel-lp-ai-live-validation-2026-11-11.md).

## Próxima investigação

Após validar LP no Windows, identificar o resultado nativo do duelo em
`0x005BC545` comparando ao menos uma vitória e uma derrota conhecidas,
com snapshots terminais. Em seguida, correlacionar LP e campo com
`select_normal` / `select_priority` para começar a explicar
**uma decisão concreta da IA**. Não deduzir toda a política da IA a
partir de nove comandos de dano.
