# PLANS.md — YugiRE jogável primeiro

> **Meta:** completar a experiência de *Joey the Passion* em C# com regras reutilizáveis e apresentação clássica, usando o original como **oráculo sob demanda**. Este plano substitui a antiga organização por trilhas de RE rigidamente dependentes. O código e a evidência já obtidos permanecem úteis.

## Regras de priorização

1. Priorizar o que o jogador consegue **fazer de verdade** no duelo; não medir progresso por número de callbacks mapeados.
2. Entregar fatias verticais pequenas: mecânica + teste + integração de ativação/turno + `CARDS.md`. Quando necessário, UI/feedback mínimo.
3. Para a maioria das cartas, começar pela descrição do DAT local e agrupamentos de perfil, não pela decompilação. Investigar nativamente apenas ambiguidades com consequência observável.
4. Testes de `Joey.Tests` liberam `Done` **no escopo descrito**. Traces nativos permitem `Verified` quando comparem o **mesmo comportamento**, não por mera presença de um opcode.
5. Manter `Joey.Core` independente de Frida e de tempo de animação; não apagar o laboratório legado.

## Arquitetura-alvo (propostas, não funcionalidades já existentes)

- **Cálculo de atributos efetivos** a partir do estado: bases, magia de campo, equipamento, efeitos contínuos e temporários. Evitar aplicar bônus permanentes ao valor base.
- **Efeitos declarativos simples** para dano/cura, compra/descarte, busca, destruição, retorno e mudanças de posição.
- **Eventos de duelo/janelas de resposta** (futuros): summon, flip, ataque, dano, destruição, cemitério e transição de fase. Separar gatilho, condição, custo, alvo e resolução.
- **Eventos de apresentação** (futuros): `Joey.Core` emite consequências de estado; `Joey.App` anima sem alterar a regra.
- **Testes por cenário**: fixtures C# baratos; quando houver divergência real, executar uma captura curta com decks e seed reproduzíveis.

## Entregas sugeridas

### Inventário e porcentagem de jogabilidade

- [x] Criar manifest por resource ID com status e famílias de efeito, separado do registry legado de RE.
- [x] Criar `scripts/card_playability.py` para ler `data.dat` com Joey.Coverage, filtrar as 771 cartas dos três packs ou 350 de Joey, separar Monster/Spell/Trap e produzir CSV/JSON/Markdown com porcentagens.
- [x] Executar no DAT original local: 771 registros nos packs, 350 apenas Joey. Snapshot de grupos e badges sem texto/arte original versionados.
- [x] Classificar os 499 monstros por correspondência de nomes públicos e aliases históricos: 264 Normal, 193 Effect, 34 Fusion, 8 Ritual. Fonte não substitui validação da categoria do DAT.
- [x] Publicar badge da trilogia e do Joey no README e relatório de porcentagens atualizados pelo GitHub Actions após edições do manifest.
- [x] Resolver os 87 nomes antes não classificados usando tipos públicos, renomeações documentadas e revisão de conflitos; manter validação de categoria com DAT como etapa adicional.
- [x] Cobrir 264 resource IDs Normal com a regra genérica em C# (invocação, tributos, set, ataque), cenários sintéticos por ID e teste opcional com registros reais do DAT.
- [ ] Rodar `dotnet run --project src/tests/Joey.Tests -- ../passion/data.dat` no Windows para conferir todos os 264 registros Normal usando as propriedades nativas. Divergências de categoria devem ser tratadas antes de elevar paridade.
- [ ] Iniciar efeitos FLIP reutilizáveis para os 193 monstros de efeito (ex.: Man-Eater Bug, Morphing Jar).
- [ ] Expandir a taxonomia para Normal/Effect/Fusion/Ritual, compra, destruição, FLIP, cemitério, negação e afins a partir dos textos locais, sem marcar como Done por inferência.
- [ ] Atualizar o manifest após cada implementação, com evidência e testes de cenário.

### Iteração A — tornar magias e equipamentos jogáveis

- [x] Integrar os cinco IDs de dano direto (`P045`) e cinco magias de recuperação de LP (338–341, 1315) em um fluxo único de ativação da mão/set; testes sintéticos para ambos os lados, LP, descarte e falhas atômicas. Escopo: **sandbox C#**, sem correntes/negação nativa.
- [x] Introduzir zona FIELD e Sogen para Warrior (classe15), depois estender a Beast-Warrior (classe12, hipótese textual), com ativação mão/set, troca de campo, stats dinâmicos, equipamento, UI e testes. Paridade nativa geral permanece pendente.
- [x] Expandir `P036` com as seis magias por atributo (Gaia Power, Umiiruka, Molten Destruction, Rising Air Current, Luminous Spark, Mystic Plasma Zone), compartilhando a regra declarativa +500 ATK/-400 DEF, integração na zona FIELD e testes de combate/equipamento.
- [x] Completar Forest, Wasteland, Mountain, Umi, Yami e Chorus of Sanctuary em P036, com tabela por tipos (incluindo bônus negativos), regra por posição e testes de duelos; seis nomes/typeCode documentados. Sogen agora contempla Beast-Warrior no sandbox.
- [ ] Confirmar Sea Serpent classCode=5 com o DAT completo e testar manualmente a interface/um duelo real no Windows. Não promover P036 a Verified sem comparação nativa.
- [ ] Expandir `EquipmentRules`/`DemoDuel` por famílias de filtros e bônus, além dos três recursos atualmente modelados. O perfil `P025` tem 41 callbacks catalogados; **não assumir** que 41 equipamentos já tenham a mesma regra.
- [ ] Incorporar curas/danos adicionais em tabelas de efeitos, sem prometer tamanho de lote antes de confirmar o catálogo original.

### Iteração B — eventos e interações

- [ ] Primitivos de compra, descarte, destruição e movimentação entre zonas.
- [x] Primeira família `OnFlip` jogável: Man-Eater Bug e Morphing Jar, com flip summon, revelação durante batalha, seleção de alvo, suspensão/continuação de combate, descarte e compra das duas mãos; testes + UI local.
- [x] Implementar Penguin Soldier (601, pack Joey): 0–2 alvos, auto-retorno apenas se não marcado para destruição em batalha, danos antes do FLIP, hook de revelação por efeito, interface e testes.
- [ ] Expandir FLIP com Morphing Jar #2, Hane-Hane e mais cartas; manter gatilhos e tipos específicos.
- [ ] Refinar resolução FLIP: janelas de resposta, negadores, deck-out nativo e mais gatilhos sob demanda.
- [ ] Gatilhos do cemitério/batalha, depois armadilhas comuns (Trap Hole, Mirror Force) e correntes/respostas, sem fingir que uma fila simples cobre todos os casos.

### Iteração C — rituais, fusões, casos especiais

- [ ] Aproveitar o catálogo local `docs/evidence/ritual-rules-catalog.json` como ponto de partida para `P269`; executar pelo menos um cenário jogável ponta a ponta antes de ampliar.
- [ ] Fusões com receitas, materiais e zona apropriada; conferir restrições da versão Joey.
- [ ] Casos emblemáticos: Exodia, Relinquished, Destiny Board, Scapegoat e condições especiais.

### Iteração D — aparência e experiência

- [x] Criar `Joey.LookAndFeel` independente do Core e raylib, com perfis Classic/Fast/Silent/Instant, clock não bloqueante, overrides e integração inicial de realce de texto no `Joey.App`.
- [x] Consolidar os achados estáticos da `analise-visual` em `Joey.LookAndFeel`: mão/hover, posições espelhadas, update de ativação e explosão, passes/alpha e critérios de despacho, com testes e ligação parcial à raylib.
- [ ] Confirmar cadência nativa de update/draw, tabela de cossenos x16.16 e pixel blending com capturas Windows; não traduzir contadores estáticos em milissegundos sem medição.
- [ ] Adicionar eventos de apresentação tipados do Core, timeline de animações de cartas, transições e LP animado, sem acoplar regras ao tempo de tela.
- [ ] Reproduzir vozes/áudio a partir dos arquivos locais do usuário (a opção `--mute-joey` só configura a política, pois ainda não existe player de voz), preservando a apresentação clássica.

## Como validar com pouco atrito

### Teste local rápido de desenvolvimento

```powershell
dotnet build YugiRE.sln
dotnet run --project src/tests/Joey.Tests
```

### Captura nativa curta de estados (Windows)

Na raiz do repositório, com Python e o Frida da venv já preparados:

```powershell
python scripts/duelos.py
# ou escolha decks específicos:
python scripts/duelos.py .\scratch\batch-A.json .\scratch\batch-B.json --battle-attributes
```

Por padrão o atalho utiliza o original em `../passion/joey_pc.exe`, os decks `native_hooks/decks/demo-A.json` e `demo-B.json`, 3 duelos/1 worker, seed 12345, velocidade 2400 e `--trace-state`. O atalho aceita `--exe`, `--duels`, `--seed`, `--speed-value`, `--output-dir`, `--workers` e `--dry-run`; detecta a venv Frida no Windows.

O limite atual com estado é **até 3 duelos e 1 worker, sem --trace-ai**. Para volume sem state trace:

```powershell
python scripts/duelos.py --duels 10 --workers 2 --no-state
```

A captura nativa usa padrão `deck0_deck1_HHMMSSffffff-YYYYMMDD_v2.jsonl` (horário local, microssegundos para unicidade). `v2` se refere ao `metadata.formatVersion` do envelope JSONL, **não** às versões internas dos schemas de observação. O PID continua em `metadata.processId`. Capturas antigas mantêm seus nomes válidos.

Use `Joey.Lab summarize` ou ferramentas `yugilab` somente quando houver uma pergunta; não exigir uma campanha de captura antes de implementar uma carta simples.

## Colaboração

Uma sessão escritora atualiza e publica em `main` após testes. Outra máquina continua com `git pull --ff-only`. Se houver múltiplos agentes escritores ao mesmo tempo, isolar branches/worktrees e integrar sequencialmente. Marcar `CARDS.md` de acordo com implementação atual, **não** com intenção, texto de carta ou trace de outra implementação.
