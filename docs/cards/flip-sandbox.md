# FLIP jogável — cinco monstros (incluindo Morphing Jar #2)

Escopo: engine C# do `DemoDuel` com teste sintético e interface hot-seat raylib.
**Não** equivale a callbacks nativos idênticos, nem modela correntes e negação.

| Carta | Resource ID | Efeito sandbox |
| --- | ---: | --- |
| Man-Eater Bug | 500 (pack Yugi) | FLIP: destrói um monstro selecionado |
| Morphing Jar | 590 (pack Kaiba) | FLIP: descarta as mãos dos dois jogadores e cada um compra 5 |
| Penguin Soldier | 601 (pack Joey) | FLIP: devolve **até dois** monstros à mão de seus donos |
| Hane-Hane | 540 (pack Yugi) | FLIP: devolve **exatamente um** monstro à mão do dono, se existir alvo válido |
| Morphing Jar #2 | 1163 (pack Joey) | FLIP: embaralha monstros nos Main Decks, escava até quota e coloca elegíveis face-down |

**Morphing Jar #2** (`1163`) é distinta de **Morphing Jar** (`590`); agora as duas têm resolvers separados no sandbox.

## Três fontes de revelação

- **Flip Summon:** mudar a posição de um monstro setado em um turno posterior; o efeito é resolvido após a revelação.
- **Ataque a monstro face-down:** revelar, **calcular dano e determinar quem será destruído**, então resolver FLIP, finalmente enviar ao cemitério os monstros ainda marcados para destruição no fim do Damage Step.
- **Revelação por efeito:** existe um hook `RevealMonsterByEffect(player,zone)` para futuros efeitos que virem monstros para cima; não é uma magia de revelação jogável ainda.

`DemoDuel.IsMarkedForBattleDestruction(player,zone)` permite ao resolver saber
se uma carta ainda está fisicamente na zona, mas não pode ser devolvida à mão.

### Penguin Soldier

O dono seleciona **zero, um ou dois** alvos em zonas ocupadas de qualquer
jogador. O efeito é opcional: usar **Done FLIP** após 0 ou 1 alvos (dois
alvos concluem automaticamente). A interface destaca os alvos e assinala em
vermelho as cartas marcadas para destruição. Se o Penguin Soldier for Flip
Summoned, pode escolher a si mesmo. Quando é atacado e **não** é destruído
por batalha, também pode retornar a si mesmo; danos de batalha já foram
calculados. Se foi atacado e **foi** marcado para destruição, não pode
escolher a si mesmo, mesmo estando na zona durante a seleção.

As cartas retornadas vão à mão do seu controlador no sandbox (controle de
monstros por outro jogador e regras do Extra Deck não existem aqui). Alvos
são únicos e validados no momento da resolução; equipamentos associados
vão ao cemitério do dono. Não é permitido retornar qualquer outro monstro
também marcado para destruição no mesmo combate.

### Hane-Hane

Hane-Hane (`resourceId=540`, perfil nativo `P080`, pack Yugi)
compartilha o mecanismo de retorno à mão com Penguin Soldier, mas exige
**um único alvo legal**, em vez de permitir 0–2. Ao selecioná-lo, a
resolução acontece automaticamente, sem botão Done. Se nenhum alvo legal
existir (por exemplo, todos os monstros restantes já estão marcados
para destruição por batalha), o FLIP não abre uma seleção impossível.

Pode devolver a si mesmo quando não está marcado para destruição,
inclusive após Flip Summon ou ao sobreviver a uma batalha; não pode
devolver a si mesmo quando vai ser destruído naquele combate.
A limpeza de equipamento e o envio à mão utilizam o mesmo caminho
da infraestrutura de Penguin Soldier. A verificação original de
`P080` no Windows ainda está pendente; `Done` mede somente o sandbox.

### Morphing Jar #2 (P182)

O novo resolver `DemoDuel.ResolveMorphingJar2` movimenta monstros entre
campo e deck, com quotas independentes para cada lado, sem reutilizar
`Morphing Jar`:

1. Durante o Damage Step, o cálculo de dano determina quais combatentes
   já estão **marcados para destruição**. Esses não voltam ao deck.
2. Os demais monstros são removidos do campo e devolvidos ao **Main Deck**
   daquele jogador, com embaralhamento sintético reproduzível por seed.
3. Cada jogador escava cartas do topo até revelar tantas **cartas de
   monstro** quanto a quantidade que devolveu ao Main Deck. Isto não é
   uma compra; cartas de magia/armadilha encontradas não contam para a quota.
4. Todos os monstros elegíveis de nível 4 ou inferior e sem exigência
   de outra invocação são colocados em defesa face-down. Monstros
   de nível superior, restritos e demais cartas escavadas vão para o
   cemitério. Encontrar um monstro de nível 7 conta para a quota, mesmo
   quando ele não é colocado no campo.
5. Os combatentes ainda marcados são enviados ao cemitério ao encerrar
   a fase de dano simplificada.

Os resultados auditáveis ficam em `DemoDuel.LastMorphingJar2` por jogador,
e `DeckSnapshot(player)` permite à suíte inspecionar as cartas remanescentes
sem alterá-las. A UI raylib aciona o efeito automaticamente quando revelado;
não há janela de seleção de cartas nessa escavação.

**Limites:** sem Extra Deck/Fusion, tokens, mudança de controlador/dono,
cadeia de respostas, efeitos contínuos ou escolha de invocações quando mais
de cinco cartas forem elegíveis. O embaralhamento e a seed são uma política
do sandbox, não um algoritmo recuperado do Joey original. O cenário de
monstro destruído em combate segue o entendimento histórico de 2003;
não declarar `Verified` sem um replay nativo equivalente.

Fontes:
- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4969&ope=2
- https://www.goatformat.com/rulings5.html
- https://edisonrul.ing/cards/m/morphing-jar-2.html

### Man-Eater Bug e Morphing Jar

Man-Eater Bug requer um alvo ocupado. Mesmo que seja destruído em batalha,
seu FLIP ainda pode destruir outro monstro, **mas isso não desfaz a
destruição por batalha que já havia sido determinada**. O duelo fica
bloqueado para novas ações até concluir a seleção.

Morphing Jar resolve a troca de mãos no momento do FLIP. Em caso de deck
insuficiente, há uma política simplificada de deck-out; dois lados
insuficientes geram `IsDraw`. Não afirmar paridade histórica dessa política.

## Motor e limites

- `FlipEffectRules`: dispatch por resource ID exato, não por nome/índice.
- `DemoDuel.Attack`: `CalculateBattle` calcula o dano e os marcadores de
  destruição; `TriggerFlip` emite efeito; `FinishDeferredBattle` envia
  os combatentes marcados ao cemitério se ainda ocuparem a mesma zona.
- `ResolveFlipTarget` seleciona monstros; `FinishFlipTargets` conclui
  Penguin com 0 ou 1 escolhido; não duplica eventos.
- `MorphingJar2Checks`: deck shuffle, quotas, dano de batalha, deck-out, restrições, determinismo e conservação de cartas.
- `FlipMonsterChecks`: cenários de Flip Summon, por ataque, por efeito externo,
  mãos, cemitério, targets inválidos, dano antecipado, auto-retorno permitido e
  negado e consumo único. A interface visual precisa de teste manual no Windows.
- `Joey.LookAndFeel` organiza **somente** os tempos/políticas de apresentação;
  não altera a ordem de cálculo de dano ou a elegibilidade dos alvos.

### Pendências

Correntes, efeitos que negam FLIP, controle de monstros, cartas de fusão
retornando ao Extra Deck, substituição de destruição e validação de timing
contra o executável original. Os cinco recursos são `Done` apenas no escopo
de sandbox; nenhum vira `Verified`.

Referências:
- https://www.db.yugioh-card.com/yugiohdb/card_search.action?cid=4608&ope=2
- https://ftp.yugipedia.com/wiki/Card_Rulings:Penguin_Soldier
- https://ygorganization.com/learnrulingspart9
