# P196/P038 — armadilhas de resposta à invocação (sandbox)

Estado: **implementadas e testadas em Joey.Core**, sem prova de equivalência
dinâmica ao `joey_pc.exe`. Esta branch depende do PR #17 (P037/P039) e
não modifica `main`.

## Identidades / callback

O arquivo `docs/evidence/card-callback-catalog.json` registra:

| Carta | Recurso | DAT | Callback | Gatilho / resolução |
| --- | ---: | ---: | --- | --- |
| Trap Hole | 1002 | 120 | P196 | Após Normal/Flip Summon adversária, se ATK atual >=1000, destrói o monstro invocado |
| Torrential Tribute | 1425 | 272 | P038 | Após uma invocação, destrói todos os monstros no campo |

Os IDs foram catalogados no EXE; os predicados de ativação vêm dos textos
históricos da carta, **não** de uma implementação de cadeia descoberta no
callback nativo. Spell/Trap usa `ClassCode=21` para Trap e `22` para
Spell no DAT original.

Referência oficial de Trap Hole:
https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4836&ope=2

Rulings sobre Torrential, incluindo resposta a invocação própria e limites
na Damage Step:
https://www.goatformat.com/rulings7.html

## Implementação C#

- `SummonTrapRules`: predicados fechados dos dois resource IDs e
  `DemoSummonEvent` com `Normal` / `Flip`. Trap Hole exige que o
  monstro seja do adversário e tenha ATK atual (com Field/equipamento
  modelado) de pelo menos 1000. Torrential aceita invocação de ambos
  os jogadores neste recorte.
- `DemoDuel.PendingSummonResponse`: janela explícita após invocação
  bem-sucedida. Exibe somente armadilhas previamente viradas para baixo
  e colocadas em turno anterior; bloqueia Draw/EndTurn/ações normais.
  Opções são indexadas pela ocorrência da carta em sua zona de Spell/Trap
  e revalidadas ao ativar. `PassSummonResponse` preserva as cartas.
- `ActivateSummonTrap(owner,zone)`: consome a Trap uma vez e envia
  ao GY do proprietário. Trap Hole envia o monstro invocado ao GY;
  Torrential reutiliza `ResolveMonsterWipe` para ambos os campos.
- O motor espera o pagamento de tributos e a colocação do novo
  monstro antes de abrir o prompt. Eventos P006/Sangan/Witch e
  P165/Sonic Bird/Senju continuam na fila; a escolha não se perde
  quando a resposta destrói imediatamente o monstro recém-invocado.
  Em Torrential, **todos os monstros saem do campo antes de começar
  a resolver as escolhas**; FLIPs face-down não são disparados só por
  serem destruídos.
- A UI `SummonResponseScreen.cs` oferece botões para cada trap e
  `Pass response`. É um overlay provisório, não uma reprodução visual
  ou temporal do jogo original.
- `LastSummonTrap` e `LastMonsterWipe` oferecem auditoria de
  qual carta foi ativada, do jogador, evento e número de removidos.
  Não são capturas JSONL nativas.

### Resposta após Normal Summon enfileirada

App, CPU e console admitem a resposta pelo mesmo produtor após a colocação
da fila. O passe reabre a próxima busca sem gastar a armadilha. Na
ativação, o gate da apresentação precede o gasto. A fila aplica cada
destruição e vínculo separadamente antes de abrir uma busca. O caminho de
resposta após Flip Summon continua legado.

## Cobertura

`src/tests/Joey.Tests/SummonTrapChecks.cs` testa:
- 1000 ATK inclusivo, 900 ATK inválido, buffs de Sogen acima do limiar;
- ativação em resposta à Normal/Flip Summon adversária, não Set ou
  revelação de carta face-down por ataque/efeito externo;
- no máximo uma Trap ativada por janela; recusa sem gastar carta;
- impossibilidade de ativação no mesmo turno em que foi Set;
- Torrential contra Normal Summon própria ou adversária;
- destruição dos dois lados, manutenção de triggers de Sangan/Witch;
- resposta a Senju: busca Ritual pendente tanto após passar quanto
  depois de Trap Hole destruir o monstro recém-invocado;
- recrutamento P164 na Damage Step **sem** abrir falsamente
  uma janela de Normal/Flip Summon.

## Limites a manter visíveis

O programa **não implementa o ciclo completo de correntes, prioridade
ou SEGOC**. Cada Summon possui uma única resposta opcional, sem
contra-respostas, negação e sem Trap imunity (p.ex. Jinzo).
Não oferece Torrential Tribute em resposta a todos os Special Summons,
nem às invocações realizadas durante resolução de chain, e não
implementa janela durante a Damage Step. Também faltam efeitos contínuos,
troca de controle e verificações profundas de timing para FLIPs com
outros gatilhos simultâneos. Direcionar essas evoluções para o futuro
sistema de eventos; não fingir que `Done (sandbox)` equivale a
`Verified` contra o executável original.


Após uma Flip Summon enfileirada sem outra seleção imediata, App, CPU e
console usam o mesmo comando de resposta da Normal Summon. Efeitos FLIP
que já emitem um lote mantêm sua finalização antes da janela. A compra
opcional de Skelengel aguarda o passe ou o último gate da armadilha e
continua disponível se o monstro for destruído. A recuperação do cemitério de
Magician of Faith e Mask of Darkness também aguarda a armadilha antes
de reapresentar os candidatos originais por ocorrência. As combinações
em que o FLIP abre seleção de alvos no campo permanecem no fluxo legado.
