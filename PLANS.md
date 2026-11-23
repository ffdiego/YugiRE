# PLANS.md — YugiRE jogável primeiro

> **Meta:** completar a experiência de *Joey the Passion* em C# com regras reutilizáveis e apresentação clássica, usando o original como **oráculo sob demanda**. Este plano substitui a antiga organização por trilhas de RE rigidamente dependentes. O código e a evidência já obtidos permanecem úteis.

## Regras de priorização

1. Priorizar o que o jogador consegue **fazer de verdade** no duelo; não medir progresso por número de callbacks mapeados.
2. Entregar fatias verticais pequenas: mecânica + teste + integração de ativação/turno + `CARDS.md`. Quando necessário, UI/feedback mínimo.
3. Para a maioria das cartas, começar pela descrição do DAT local e agrupamentos de perfil, não pela decompilação. Investigar nativamente apenas ambiguidades com consequência observável.
4. Testes de `Joey.Tests` liberam `Done` **no escopo descrito**. Traces nativos permitem `Verified` quando comparem o **mesmo comportamento**, não por mera presença de um opcode.
5. Manter `Joey.Core` independente de Frida e de tempo de animação; não apagar o laboratório legado.
6. Para **todos os elementos visuais**, a meta é reproduzir coordenadas, dimensões, orientação, recursos, composição, contadores e duração **do Joey original**, mesmo com código de renderização e scheduler C# diferentes. A análise estática recupera parâmetros; vídeo e capturas comparam frames. Consultar [meta visual em pixels](docs/architecture/visual-fidelity-goal.md). Uma aproximação é uma pendência, não o contrato final.

7. Os recursos de apresentação devem ser lidos do **`data.dat` original em runtime**; caches em memória são permitidos, mas a alpha fiel não deve usar sprites temporários/placeholders como aparência final. As animações devem preservar trocas de variante e quadros intermediários (ex.: espada inteira/espada aparentemente inserida na carta), não apenas posição inicial/final. Falta de recurso ou transição não investigada deve ser documentada e visível para revisão.

8. **Modo visual limpo obrigatório:** o modo fiel parte com overlays de diagnóstico desativados. Quando os overlays estiverem ativos, disponibilizar botão clicável `Ocultar debug`, atalho `F3` e reativação pelo menu; controles essenciais de jogo não desaparecem junto com o debug. Texto provisório, contadores, FPS e botões auxiliares devem estar em camada distinta, sem modificar a geometria/timeline da cena.
9. Revisar sobreposição e legibilidade da Alpha em 800×600 com capturas de modo limpo (sem debug) e modo diagnóstico; verificar HUD, mão, cartas, números, fases, ícones e cliques, sem cobrir sprites nativos por acidente.

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
- [x] Implementar Hane-Hane (540, pack Yugi): seleção obrigatória de um único alvo, fallback sem alvos, auto-retorno após Flip Summon/sobrevivência, e restrição na destruição de batalha; regressões e UI.
- [x] Morphing Jar #2 (1163, pack Joey) no sandbox: shuffle determinístico dos Main Decks, quota de escavação distinta por jogador, especiais face-down e descarte dos inválidos; defender já condenado em batalha fica no campo até o cemitério, não volta ao deck.
- [ ] Ampliar Morphing Jar #2 para Fusion/Extra Deck, tokens, controle de cartas alterado e seleção quando >5 monstros invocáveis; não declarar paridade nativa até medir callbacks P182 no Windows.
- [ ] **Próximo grupo — seleção de cartas pelo efeito (UI do Joey):** investigar a tela nativa de seleção e construir um seletor genérico de origem/destino/filtragem no Core + janela raylib. Começar por Magician of Faith (427) e Mask of Darkness (101), ambos P018, FLIP que recupera Magia/Armadilha do cemitério. Depois Sangan (47)/Witch of the Black Forest (573), ambos P006, que buscam monstros no deck ao serem enviados ao cemitério; e família Mystic Tomato (1123)/Giant Rat (1108)/Shining Angel (1117), P164, disparada após destruição em batalha. **Não rotular esses dois últimos grupos como FLIP.**
- [ ] Refinar resolução FLIP: janelas de resposta, negadores, deck-out nativo e mais gatilhos sob demanda.
- [ ] Gatilhos do cemitério/batalha, depois armadilhas comuns (Trap Hole, Mirror Force) e correntes/respostas, sem fingir que uma fila simples cobre todos os casos.

### Iteração C — rituais, fusões, casos especiais

- [ ] Aproveitar o catálogo local `docs/evidence/ritual-rules-catalog.json` como ponto de partida para `P269`; executar pelo menos um cenário jogável ponta a ponta antes de ampliar.
- [ ] Fusões com receitas, materiais e zona apropriada; conferir restrições da versão Joey.
- [ ] Casos emblemáticos: Exodia, Relinquished, Destiny Board, Scapegoat e condições especiais.

### Iteração D — aparência e experiência

**Método de trabalho para esta trilha (decisão 22/11/2026): assembly-first.** O mantenedor aponta diferenças visuais, e o agente realiza a investigação de `joey_pc.exe` antes de escolher sprites, coordenadas, cores ou timing. Capturas são oráculos de validação, não substitutos da análise da rotina. Registrar endereços, ramificações de estado, argumentos, recursos, grau de certeza e cenários de teste; deixar `Pending` quando o assembly não estiver esclarecido. Detalhes: [pendências assembly-first](docs/research/visual-assembly-first-backlog-2026-11-22.md) e [instruções aos agentes](AGENTS.md#política-obrigatória-engenharia-reversa-primeiro-para-apresentação).

**Pendências visuais novas ou refinadas, em ordem sugerida:**

- [ ] **Janela de seleção e fusão, foco de cobertura:** modelo `NativeSelectionWindow` isolado no LookAndFeel para os cinco slots, rolagem e contadores de entrada/saída comprovados por `0043A060 / 0043ABA0 / 0043B0F0 / 0043B280 / 0043E280`; resta ligar ao overlay, extrair a âncora e as constantes do gerador x87 com --memory; recuperar a tabela de cossenos gerada em runtime por 0055DCF0 (Ghidra estático não fornece a tabela pronta), e validar os passes Draw/frames (o índice de cosseno, a interpolação de centro e a escala X/Y já foram corrigidos por assembly). A fusão tem **duas Draws distintas** `004524F0` e `004877F0`, associadas aos construtores `004520E0` e `004874C0`; precisamos exportá-las para distinguir efeito geral e efeito vinculado à carta. Ver [investigação seleção e fusão](docs/research/native-selection-window-fusion-2026-11-22.md). **Não atualizar badge 19/34 só pelo modelo.**
- [ ] **LP flutuante por origem do dano/cura:** remover a escolha universal de ponto médio entre as fileiras. Rastrear `00414110` (combate), `00408470` (caso geral), `0047C4F0` (registro do delta/identificador), `0047C660` (Draw) e `004806F0` (números) até a passagem real de X/Y/slot. Comparar dano de batalha sobre monstro, dano direto/cura sobre a região de magias, ambos os lados, ataque direto e alvo face-down. **Nada de ajustar pela captura sem entender a seleção nativa de âncora.**
- [ ] **Aviso de mudança de jogador/turno:** investigar os recursos `j/duel/phase_name/nex_e_b*.yga` / `nex_e_r*.yga`, transição e ordem relativas aos banners de fase. Seguir `00480B40`/`00480BC0`, `00480C20`, `00480C60` e seus chamadores antes de implementar o texto e a timeline no cliente.
- [ ] **ATK/DEF em monstros no campo:** os três BMPs originais, o glifo separador, variações de atributo e intensidades `0xA0/0x100` já estão ligados ao Draw. O tween `7/8` agora acompanha a implementação com estado por carta/slot em `NativeFieldAttributeTimeline`: `00418B10` inicializa com atributos básicos e `004194D0` interpola os valores exibidos; relógio de apresentação respeita Classic/Fast/Instant e pausa F6/F7. Monstros próprios face-down mostram ATK/DEF, oponentes face-down permanecem ocultos. **Partial, não Verified**: pendem coordenadas absolutas, flags `0x8000`, blends e comparação de pixels/timing com Joey. Veja [investigação ATK/DEF](docs/research/native-field-attributes-2026-11-22.md) e `scripts/pending/03-field-attributes-ghidra.sh`.
- [ ] **Cursor `Attack` na Battle Phase:** a nuvem de ataque original já aparece no hover do nosso monstro quando `Joey.Core.CanAttack(zone)` autoriza, com testes de estados negativos e sem segundo cursor; **resta validar no dispatcher original** a precedência, o hotspot e os casos de seleção de alvo. Não confundir o cursor com a espada no campo.
- [ ] **Cursores `SetAttack`/`SetDefense`:** recuperar as condições/alternância nativas de mudança de posição e as variantes `cursor_e_setatk.yga`/`cursor_e_setdef.yga`; não exibir ação ilegal nem assumir que os nomes do DAT provam o fluxo.
- [ ] **Indicador de corrente desativada `004531C0`:** contrato temporal `NativeDisabledChainTimeline` implementado no LookAndFeel (24 updates, dois ciclos dos seis quadros, bit de composição `0x40`) e testes sintéticos; **Draw ainda Pending**. Falta mapear recursos reais de `j/duel/chain/disable.txt`, coordenadas e passes de `0045C140`, condições de `00408730`, renderização raylib e validação no Joey. Gerar `scratch/visual-review/chain-manifests.json` com `scripts/summarize_native_chain_dat.py` e o ZIP do Ghidra com `scripts/pending/02-chain-disabled-ghidra.sh`. Ver [contrato parcial](docs/research/native-chain-disabled-2026-11-22.md).

**Critério de conclusão visual:** fidelidade observável de **posição/tamanho por pixel e tempo por update** em 800×600, cobrindo progressivamente todos os elementos (carta, mão, texto, HUD, LP, fases, efeitos e transições). Não é necessário reproduzir GDI/opcodes/estrutura interna, mas deve-se demonstrar de onde vieram os valores. Conferir o [contrato de fidelidade visual](docs/architecture/visual-fidelity-goal.md) e documentar divergências mensuradas.

- [x] Criar `Joey.LookAndFeel` independente do Core e raylib, com perfis Classic/Fast/Silent/Instant, clock não bloqueante, overrides e integração inicial de realce de texto no `Joey.App`.
- [x] Consolidar os achados estáticos da `analise-visual` em `Joey.LookAndFeel`: mão/hover, posições espelhadas, update de ativação e explosão, passes/alpha e critérios de despacho, com testes e ligação parcial à raylib.
- [x] Prévia visual de ativação jogável em raylib com os quatro YGAs originais carregados do DAT local, modelo nativo de três passes, clock de protótipo e perfis Classic/Fast/Instant; testes C# e verificação opcional de assets.
- [x] **Pausa histórica** após a prévia de ativação para priorizar Iteração B; **superada pela meta atual de alpha com fidelidade visual**, sem abandonar as regras.
- [ ] Recuperar e aplicar o contrato nativo de renderização da carta: ambos os lados, rotação (ataque/defesa e face-up/down), dimensões/pivô, e posição legível do ATK/DEF **acima no adversário e abaixo no próprio**.
- [ ] Substituir placeholders remanescentes do fluxo visual padrão por **sprites originais carregados do DAT local**, usando manifests e seleção de variantes para cada estado; não versionar os assets. Testar aviso explícito de asset faltante e evitar substituição silenciosa.
- [ ] Reconstituir estaticamente e validar a **troca de sprite da espada de ataque** ao atingir a carta, investigando os BMPs `j\\duel\\effect\\atk_icon.bmp` e `atk_icon2.bmp`, o frame/estado de transição, clipping, pivô e camada; preservar a ilusão de penetração relatada no vídeo, sem declarar seus detalhes como comprovados antes da análise.
- [ ] Implementar `DebugOverlayEnabled` (ou equivalente) na UI: default OFF em modo fiel, botão `Ocultar debug` enquanto ativo, atalho F3 e toggle em menu para restaurar. Centralizar Draw e HitTest do overlay, sem interferir no duelo; testes do estado/toggle.
- [ ] Fazer auditoria visual da Alpha em 800×600, separando **controles do jogo** de **diagnósticos**: corrigir layout provisório que cobre cartas, sprites e textos, e comparar capturas sem debug. A imagem recebida de 21/11/2026 mostra textos e controles sobrepostos ao tabuleiro; ela não determina, isoladamente, quais componentes são debug.
- [ ] Criar matriz de comparação por elemento visual e estados/quadros-chave contra Joey original: coordenadas/dimensões/ângulos e contagem de updates; registrar valores ainda provisórios em vez de arredondar silenciosamente.
- [x] **Ferramenta de comparação visual 800×600:** `scripts/compare_visual_frames.py` compara PNGs alinhados sem redimensionar, gera diferença, máscara, overlay e métricas por painel lateral/tabuleiro; testes sintéticos em `scripts/tests/test_compare_visual_frames.py`. `VisualReviewScreen` registra a ação do mesmo sprite de cursor selecionado (incluindo `Attack`). Não implica paridade nativa dos elementos. Ver [protocolo de comparação](docs/research/visual-frame-comparison-2026-11-22.md).
- [ ] Expandir sistematicamente a fidelidade de todas as telas/efeitos visuais, depois da fatia carta/ATK/DEF, sem congelar o progresso funcional da alpha.
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
