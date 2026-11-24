# Piloto nativo P200/P192/P201 — janelas de resposta a ataque

Estado: **decks preparados e validados offline**, ainda **sem observação
real desta família no `joey_pc.exe`**. Esta é uma instrução para execução
manual no Windows, não autorização para executar o jogo em CI ou em segundo
plano. A main permanece intacta (branch empilhada em PR #20).

## Perguntas e hipótese por carta

| Efeito | Perfil / recurso / índice DAT | Hipótese a confrontar |
| --- | --- | --- |
| Magic Cylinder | P200 / `1214` / `894` | Cancela ataque declarado; causa ao jogador que atacou o ATK **atual** do atacante; ambos monstros permanecem e a Battle Phase pode prosseguir |
| Negate Attack | P192 / `960` / `282` | Cancela ataque declarado, sem dano, e **encerra** a Battle Phase do atacante |
| Mirror Force (controle) | P201 / `1056` / `467` | Cancela combate destruindo todos os monstros do lado atacante que estiverem em posição de ataque, sem cálculo de batalha |

Os três perfis/índices são mapeamentos do catálogo
`docs/evidence/card-callback-catalog.json`. São **hipóteses de
comportamento** de acordo com texto histórico/TCG, e não regras
capturadas do binário original.

Fontes de texto: Magic Cylinder na listagem contemporânea de Joey
https://gamefaqs.gamespot.com/pc/920746-yu-gi-oh-power-of-chaos-joey-the-passion/faqs/32648 ;
Negate Attack e Magic Cylinder nas páginas oficiais:
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4804&ope=2
e
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=5124&ope=2 .

## Decks (arquivos prontos)

Todos estão sob `native_hooks/decks/attack-responses/` e utilizam
**internalIndex uint16**, não resourceId ou passcode. Os decks de defesa
partem do `demo-A.json` e o adversário é uma cópia fiel do
`demo-B.json`, já utilizados em experimentos anteriores.

| Arquivo | Main Deck | Cartas de interesse |
| --- | ---: | --- |
| `defense-cylinder.json` | 40 | 3× Magic Cylinder |
| `defense-negate.json` | 40 | 3× Negate Attack |
| `defense-mixed.json` | 40 | 2× Magic Cylinder + 2× Negate Attack + 2× Mirror Force |
| `opponent-demo-B.json` | 43 | Deck histórico de adversário sem as três armadilhas |

`tests/test_attack_response_decks.py` protege formato, identidade
(resource ID ↔ internalIndex ↔ classe DAT 21 ↔ pack), máximo de três
cópias e as posições exatas alteradas no deck original.

**Atenção:** Magic Cylinder é do pack Joey, Negate Attack do Kaiba e
Mirror Force do Yugi. A injeção de decks no **modo demo 7** contorna
restrições normais de coleção/salvamento e não atesta legalidade ou
disponibilidade no deck editor. Não misturar esses experimentos com
partidas competitivas comuns.

## Execução Windows — piloto de 3 duelos por cenário

Pré-requisitos: checkout da branch `feature/p200-p192-attack-negation-v1`,
`.NET 10`, ambiente `native_hooks/.venv-frida`, executável original
`joey_pc.exe` x86 e módulo YugiLab configurado como no piloto anterior.

Na raiz da solução, primeiro validar os presets:

```powershell
python -m unittest discover -s tests -p "test_attack_response_decks.py" -v
Get-FileHash "C:\caminho\para\joey_pc.exe" -Algorithm SHA256
```

O EXE observado anteriormente tinha SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Se for diferente, **não inferir endereços/hook nem paridade** sem
verificar a variante binária.

Magic Cylinder:

```powershell
dotnet run --project .\src\Joey.Lab -- experiment `
  --exe "C:\caminho\para\joey_pc.exe" `
  --duels 3 --workers 1 --seed 12345 --speed-value 600 `
  --deck-0 ".\native_hooks\decks\attack-responses\defense-cylinder.json" `
  --deck-1 ".\native_hooks\decks\attack-responses\opponent-demo-B.json" `
  --trace-ai --python ".\native_hooks\.venv-frida\Scripts\python.exe"
```

Negate Attack: repetir o mesmo comando alterando
`--deck-0` para `defense-negate.json` e `--seed` para `12346`.

Controle misto: repetir com `defense-mixed.json` e
`--seed 12347`.

**Não aumentar duelos ou workers antes de validar o piloto de 3.**
O controlador da IA pode não Set/ativar essas cartas mesmo que estejam
no deck; o resultado `0 ativação` significa **ausência de observação**,
não validação nem falsificação da regra. Se houver poucos exemplos,
inverter `--deck-0`/ `--deck-1` com seed documentada pode ajudar.

## O que observar/guardar

- Pasta de saída completa do YugiLab, contendo `manifest.json`,
  `report.json`, JSONL nativos, footer e logs; arquivar em ZIP e
  compartilhar sem reformatar, perdendo campos ou editar o trace.
- Verificar `complete=true`, `capturesComplete=true`,
  `backendVerified=true`, `overwritten=0`, sequência/terminação
  válidas e `deck_override verified` **para ambos os lados**.
- Quando uma Trap for ativada, registrar turno/lado, card de defesa,
  quem declarou ataque, o ATK exibido do atacante, LP antes/depois,
  destino do atacante/defensor, se um **segundo monstro** conseguiu
  atacar depois e se o jogo continuou ou saiu da Battle Phase.
- Comparar Magic Cylinder contra Negate Attack, incluindo ataque
  direto e ataque contra monstro virado para baixo. Observar se o
  ATK com Field/Equip modifica o dano refletido.
- Guardar ao menos uma sequência de **passar em vez de ativar**,
  quando possível, para conferir que a declaração não acontece duas vezes.
- Capturas JSONL de comandos sem estado/causalidade completas são
  `Inconclusive`, mesmo que tenham IDs de carta próximos de comandos
  de LP. O replay `.ydr` pode complementar, mas não substitui
  observação de LP, fase e campos.

Se ocorrer crash, corrupção, divergência de deck, erro de
encerramento de PID ou perda de eventos, preservar o ZIP e interromper
a ampliação. O laboratório não deve modificar arquivos do jogo em
disco; o override dos decks é em memória.

## O que testar depois

Somente se os pilotos produzirem episódios com causalidade clara,
converter para fixtures `input/action/expected` e comparar
`Joey.Core`, classificando `Match/Mismatch/Inconclusive`.
**Não promover para `Verified` agora.**

Com múltiplas armadilhas simultaneamente prontas, a pergunta passa
a ser **chain building, negação e prioridade**. Esse é o sinal
para interromper a expansão horizontal de cartas reativas e consolidar
o modelo de corrente antes de implementar cartas como Seven Tools of
the Bandit, Magic Jammer ou mais Counter Traps.
