# VISUALS — detalhes dos itens

Histórico e contratos movidos de PLANS.md na revisão de 24/11/2026 (base `42f7813`). [Plano vivo e status atuais](../../PLANS.md). Registrar novos relatos (PR, CI, evidência, limitações) no bloco do item; não duplicar relatos no plano. Os relatos anteriores são históricos, não certificam o estado atual de uma branch.

> **Método (decisão 22/11/2026): assembly-first.** O mantenedor aponta diferenças visuais; o agente investiga `joey_pc.exe` antes de escolher sprites, coordenadas, cores ou timing. Capturas são oráculos de validação, não substitutos da análise da rotina. Registrar endereços, ramificações de estado, argumentos, recursos, grau de certeza e cenários de teste; deixar `Pending` quando o assembly não estiver esclarecido. Detalhes: [pendências assembly-first](../../docs/research/visual-assembly-first-backlog-2026-11-22.md) e [instruções aos agentes](../../AGENTS.md#política-obrigatória-engenharia-reversa-primeiro-para-apresentação).
>
> **Apresentações de ações do duelo** devem se ligar à fila em `A03`, respeitando os gates e a ordem nativa; nem toda rotina Draw representa um comando (HUD, hover e objetos persistentes podem atualizar/desenhar independentemente). VISUALS pode implementar contratos e modelos isolados antes de A03, sem introduzir uma segunda fonte de ordem de regras.

**Critério de conclusão visual:** fidelidade observável de **posição/tamanho por pixel e tempo por update** em 800×600, cobrindo progressivamente todos os elementos (carta, mão, texto, HUD, LP, fases, efeitos e transições). Não é necessário reproduzir GDI/opcodes/estrutura interna, mas deve-se demonstrar de onde vieram os valores. Conferir o [contrato de fidelidade visual](../../docs/architecture/visual-fidelity-goal.md) e documentar divergências mensuradas.

## Histórico concluído (sem ID)

- [x] `Joey.LookAndFeel` independente do Core e do raylib, com perfis Classic/Fast/Silent/Instant, clock não bloqueante, overrides e integração inicial de realce de texto no `Joey.App`.
- [x] Achados estáticos da `analise-visual` consolidados em `Joey.LookAndFeel`: mão/hover, posições espelhadas, update de ativação e explosão, passes/alpha e critérios de despacho, com testes e ligação parcial à raylib.
- [x] Prévia visual de ativação jogável em raylib com os quatro YGAs originais do DAT local, modelo nativo de três passes, clock de protótipo e perfis Classic/Fast/Instant.
- [x] Pausa histórica após a prévia de ativação para priorizar regras; **superada** pela meta atual de alpha com fidelidade visual.
- [x] `scripts/compare_visual_frames.py`: compara PNGs alinhados 800×600 sem redimensionar (diferença, máscara, overlay, métricas por painel). `VisualReviewScreen` registra a ação do mesmo sprite de cursor selecionado. Não implica paridade nativa. Ver [protocolo](../../docs/research/visual-frame-comparison-2026-11-22.md).

## V01 — Instrumentação Frida: tabela de cosseno e log de Draw

<a id="v01"></a>

O snapshot privado v3 contém os 512 valores x16.16 de `00A52EE8`, capturados após a inicialização em `0055DCF0`. A geração por truncamento reproduz todos os valores e é o padrão do cliente. O loader `--native-cosine=` continua disponível para comparação, e F12 registra a origem usada. [Evidência e teste](../../docs/research/native-cosine-runtime-2026-11-24.md).
  Falta registrar o primitivo de blit comum por frame de duelo (rotina Draw, recurso, x, y, alpha/blend, ângulo/escala, quadro) para comparar as rotinas do inventário com o original.

## V02 — Contrato nativo de renderização da carta

<a id="v02"></a>

Ambos os lados, rotação (ataque/defesa e face-up/down), dimensões/pivô, e posição legível do ATK/DEF **acima no adversário e abaixo no próprio**.

## V03 — Sprites originais no lugar de placeholders

<a id="v03"></a>

Substituir remanescentes do fluxo visual padrão por sprites carregados do DAT local, usando manifests e seleção de variantes por estado; não versionar os assets. Aviso explícito de asset faltante; nada de substituição silenciosa.
  V03.1: preflight de mappings grandes/mini para cartas jogáveis, validação de mini BMPs e verso original, sem substituição geométrica/nome no duelo; diagnóstico de registro não jogável só com --include-unused fora do duelo. Tema explícito preservado. Controles transitórios de seleção/descarte e validação DAT real ainda pendentes. [Evidência](../../docs/24-dat-graphics.md#v031-assets-obrigatórios-e-fallback-explícito).

## V04 — Troca de sprite da espada de ataque

<a id="v04"></a>

Roteiro 21 recebido: Draw 004142C0 confirma chaves 0/2/1 e ângulo recalculado no voo; sampler ajustado. V04.2: NativeRotatedQuad porta geometria inteira e pivô de 00422840/00422C00 confirmados pelo assembly no SQLite v3 (bundle de fusão); âncora de ataque direto 525,64/536 confirmada. Testes de vértices adicionados; renderer normal preservado, faltam x87, compositor final e comparação. V04.3: NativeSwordAngle porta operandos FPATAN, perspectiva e truncamento da interpolação (sete checks); constantes e __ftol não constam no v3, roteiro 32 direcionado. CI do PR #151 verde. Pivô/compositor e constantes x87 pendentes; [contrato](../../docs/research/native-attack-life-routing-2026-11-24.md).
  Reconstituir estaticamente e validar a troca ao atingir a carta (`j\duel\effect\atk_icon.bmp` e `atk_icon2.bmp`): frame/estado de transição, clipping, pivô e camada. Preservar a ilusão de penetração observada no vídeo, sem declarar seus detalhes como comprovados antes da análise. Rotina Draw `004142C0` (Partial).

## V05 — ATK/DEF em monstros no campo

<a id="v05"></a>

Já ligados: BMPs originais, separador, variantes normal/aumentado/reduzido, `0xA0/0x100`, tween 7/8, gate nativo de `00418EF0 → 004194D0` (movimento `0x100`, saída estado 5, zona, lado, face-up `0x8000`, identidade do ocupante `0056DD30`); renderer e JSON F12 compartilham a visibilidade, evitando revelar atributos da CPU antes do contato da espada. V05.1: gate distingue preservar/resetar/avançar; DuelScreen mantém tween durante slot ocupado em movimento. DAT confirma glifos 7x10, roteiro 33 recebido e importado no v4-visuals: confirma todos os centros e Y 398/202, sem deslocamento novo; 50 checks extras. CI do PR #152 verde. Faltam blending, comparação gráfica/cadência e sprites dos indicadores adicionais (overlays `00419B70/0041A3B0/0041A070` não são números). **Partial, não Verified.** Ver [pesquisa](../../docs/research/native-field-attributes-2026-11-22.md) ; bundle 03 recebido e absorvido em 23/11, não repetir coleta. Ver [handoff dos bundles](../../docs/research/visual-contract-bundles-2026-11-23.md).

## V06 — LP flutuante por origem do dano/cura

<a id="v06"></a>

NativeLifeDeltaRequest seleciona slot do lado afetado ou -1 (direto/flags 8 e 10h); entrada opt-in RequestNativeChange preserva hold de consumo, segundo pedido libera sem substituir delta e slot. A03 possui fornecimento de dados e integração, Roteiro 21 recebido: Draw confirma slot -1 → S/T central e centralização do número; renderer atualizado. V06.1: coleta 33 confirma centros absolutos; assembly integral de 004806F0 recuperado das janelas existentes, corrige entrada phase=0 por glifo e despacho estático >=256, com rampa completa 0..512 e 50 checks adicionais. CI do PR #153 verde; fornecimento do slot de combate por A03, composição e comparação gráfica/cadência pendentes. [Contrato](../../docs/research/native-attack-life-routing-2026-11-24.md).
  Remover a escolha universal de ponto médio entre as fileiras. Rastrear `00414110` (combate), `00408470` (caso geral), `0047C4F0` (registro do delta/identificador), `0047C660` (Draw) e `004806F0` (números) até a passagem real de X/Y/slot. Comparar dano de batalha sobre monstro, dano direto/cura sobre a região de magias, ambos os lados, ataque direto e alvo face-down. **Nada de ajustar pela captura sem entender a seleção nativa de âncora.**

## V07 — Aviso de mudança de jogador/turno

<a id="v07"></a>

NativeTurnNoticePlan: aviso tipo 6 inicia junto ao giro dos botões; fase posterior começa ao atingir 512/64 updates, exceto fase -1. Preloads relativos à perspectiva e parser NativePhaseNoticeResources. Roteiro 23 recebido: Draw/Update e chaves/ângulo confirmados, DrawAtCounter implementado; NativeNoticeProjection recupera geometria candidata de 004223F0 (decompilação, ABI EBX/EBP pendente); rasterizador 0051EBD0 ainda não fechado. V07.1: predicado virtual +4 (004812B0) confirmado nas janelas da coleta 23: +150<512 ou +14C<128; NativePhasePanelState porta contadores e início de fase, gates 16/24 updates após virada de 64. Integração do predicado permanece com A03/A07; sem alterar PhaseBusy. Roteiro 25. Texto integrado ao cliente: manifest v3 validado (46 registros), sampler DrawAtCounter sobre turnFlipElapsed existente, 48/64 updates concorrentes. Projeção candidata renderizada por colunas raylib, compositor ainda não nativo; sem novo gate ou crédito Verified, smoke desktop pendente. [Contrato](../../docs/research/native-turn-notice-contract-2026-11-24.md).
  Investigar `j/duel/phase_name/nex_e_b*.yga` / `nex_e_r*.yga`, transição e ordem relativas aos banners de fase. Seguir `00480B40`/`00480BC0`, `00480C20`, `00480C60` e seus chamadores antes de implementar texto e timeline no cliente.

V07.2: NativePhaseBannerDraw recupera os passes +10/+8/+B0 dos tipos 0..5 e chaves dos sete frames Battle; +B0 conserva apenas AX confirmado, sem inventar blend. Roteiro 24 já abrange offset +B0 da vtable candidata; confirmar receptor. Renderer e gate legado preservados. CI do PR #154 verde após corrigir a asserção de exceção.

## V08 — Ícones e indicadores sobre cartas (`00419B70`, `0041A070`, `0041A3B0`)

<a id="v08"></a>

Bundle 04 recebido: 17/17 funções e 12/12 janelas; `NativeStatusDrawLayout` implementa a geometria de sprites 4..9 (miniatura, quadros do atlas, âncoras A/B, espelhamento tipo/atributo e flashes 10..13); `NativeCardStatusDigits` usa atlas amarelo original. Preservar seletores, comandos `0x5F/0x60/0x69` e countdown 512→0 em 32 updates já isolados. Compositor ordenado e modo `--review-card-status=` usam os divisores do `card.txt` local e os contratos existentes; os dados sintéticos são exclusivos da revisão. V08.1: v4-visuals confirma card.txt 14 registros e dimensões 4..13, A=ampulheta/B=caveira (ícones, não semântica completa). Review valida banco/atlas; gate de região+slot<=10 e identidade ordinária região<5 corrigidos. CI do PR #156 verde. **Pending no duelo:** faltam smoke DAT em runtime, semântica A/B, compositor nativo e integração normal. São subpasses da Draw 004194D0 já contabilizada; não somar três novas Draws. [Contrato](../../docs/research/native-card-status-digits-2026-11-22.md); [pesquisa](../../docs/research/native-card-status-overlays-2026-11-22.md). Não repetir coleta 04.

## V09 — Cursor `Attack` na Battle Phase

<a id="v09"></a>

A nuvem de ataque aparece no hover quando `Joey.Core.CanAttack(zone)` autoriza (testes negativos, sem segundo cursor). **Resta validar no dispatcher original** a precedência, o hotspot e os casos de seleção de alvo. Não confundir o cursor com a espada no campo.

## V10 — Cursores `SetAttack`/`SetDefense`

<a id="v10"></a>

Recuperar condições/alternância nativas de mudança de posição e as variantes `cursor_e_setatk.yga`/`cursor_e_setdef.yga`; não exibir ação ilegal nem assumir que os nomes do DAT provam o fluxo.
  NativeCursorActionSelector isola ciclo de 005B8910 (máscara, ordem injetada, pedido raw 005BF490/4B0). Roteiro 26 citado como recebido, porém ausente no v4-visuals consultado; localizar coleta antes de repetir. V09/V10.1: manifesto inglês e 16 dimensões confirmados; Flip/SetAttack/SetDefense no hover do duelo pelo gate CanChangePosition do Core. Parser de manifesto isolado; prioridade code→asset/hotspot, smoke e integração do preflight pendentes. [Contrato](../../docs/research/native-cursor-dispatch-2026-11-24.md).

## V11 — Janela de seleção e fusão (foco de cobertura)

<a id="v11"></a>

`NativeSelectionWindow` já contém cinco slots/scroll, entrada e saída, índice correto e interpolação confirmados. Bundle 01 recebido: âncora 525, gerador x87 (pi/256 e multiplicação final ainda não medida), Update/Draw das duas fusões, fases, recursos e clocks 4/8 com espera no segundo. **Coleta concluída**; a tabela C runtime foi confirmada em V01. Faltam constantes/tabela de seletores, integração e validação. As duas Draws `004524F0` e `004877F0` devem permanecer distintas. `NativeFusionTimeline` porta os clocks e fórmulas dos passes principais em modelos separados (ângulo mutado no Draw, espera em 128 na fusão vinculada). A seleção estável `0043B280` agora é Partial no duelo: usa fundo do manifesto, variante por total>5, slots de 64px e scroll de cinco cartas; preserva decisões de busca/fusão no Core. Controles ainda provisórios, sem transição/ícones/scroll nativos completos. Fallback `--legacy-selection`. As duas Draws de fusão continuam Pending. Banco fusion.txt e revisão isolada vinculada com flag explícita e cosseno validado; F7 Update+Draw, F9 Draw independente. Roteiro 13 pede apenas dados/compositor/liberação ausentes; não inserir pausa no scheduler por hipótese. Ver [contrato](../../docs/research/native-selection-window-fusion-2026-11-22.md) e [handoff](../../docs/research/visual-contract-bundles-2026-11-23.md). Não alterar badge só pelo conhecimento.

## V12 — Indicador de corrente desativada (`004531C0`)

<a id="v12"></a>

Bundle 02 concluído: opcode 0x72, gates, descritor, rotação 0x4000, 24 updates (dois ciclos de seis quadros), bit 0x40 e seis texturas inteiras documentados. `NativeDisabledChainTimeline` tem testes sintéticos. **Draw Pending no duelo:** modo de revisão isolada `--review-disabled-chain=` agora carrega as seis entradas de `disable.txt` em runtime, usa descritor/centro/rotação e captura F12. Falta testar com DAT real, confirmar compositor e mapear o gatilho do comando para a fila; não tratar 0x72 como negação genérica. Ver [contrato](../../docs/research/native-chain-disabled-2026-11-22.md). Não repetir coleta 02.

## V13 — `DebugOverlayEnabled` na UI

<a id="v13"></a>

Default OFF em modo fiel, botão `Ocultar debug` enquanto ativo, atalho F3 e toggle em menu para restaurar. Centralizar Draw e HitTest do overlay, sem interferir no duelo; testes do estado/toggle.
  Estado App-local `DebugOverlayState`: default OFF, F3/menu, hitboxes compartilhados, captura antes de cliques do duelo e hide no mesmo frame. Testes sintéticos do estado; comparação gráfica ON/OFF e smoke real pendentes (V14/V15).

## V14 — Auditoria visual da Alpha em 800×600

<a id="v14"></a>

Separar **controles do jogo** de **diagnósticos**; corrigir layout provisório que cobre cartas, sprites e textos; comparar capturas sem debug. A imagem de 21/11/2026 mostra textos e controles sobrepostos ao tabuleiro; ela não determina, isoladamente, quais componentes são debug.

## V15 — Matriz de comparação por elemento visual

<a id="v15"></a>

Coordenadas/dimensões/ângulos e contagem de updates por estado/quadro-chave contra o Joey original; registrar valores ainda provisórios em vez de arredondar silenciosamente. Usar `compare_visual_frames.py` e o log de `V01`.
  O comparador aceita regiões nomeadas e registra seus retângulos de divergência em coordenadas do canvas. A matriz de pares agrega diferenças de pixels e contadores fornecidos pelo operador, com limites opcionais e diagnósticos por quadro-chave. Testes com PNGs sintéticos incluídos no CI. Comparações pareadas com o executável continuam pendentes. [Protocolo](../../docs/research/visual-frame-comparison-2026-11-22.md).

## V16 — Demais Draws do inventário

<a id="v16"></a>

Seleção nativa (`0043B280`, ver V11), ajuda `0045E4A0`, efeito de estado persistente `0040E260`, quebra/remoção alternativa `00450FD0`, ativação alternativa `00450AD0`, corrente `0041F870`, remoção/exclusão `0043DBE0`, moeda `00457520`, dado `00456B90`, Vija `004584C0`, Layerfont A/B `00455FF0`/`00456360`. Cada rotina vira um item novo (`V20`, `V21`...) quando começar. Estado: [cobertura visual](../../docs/architecture/VISUAL_COVERAGE.md).

## V17 — Cadência nativa de update/draw e blending

<a id="v17"></a>

NativeSurfaceCopy porta cópia opaca/escala/clipping e quatro formatos de surface+74h (004AD9C0/00547180), sem assumir blend ou máscara. Roteiro 24 lê vtable 005DC6EC e alvos ainda desconhecidos; confirmar assembly das fronteiras. Roteiro 25 separa geometria de quadrilátero de 0051EBD0 e família de intensidade 00422E30 (<256); NativeQuadRasterDispatch porta a fronteira. NativeIntensityPixel porta mistura por delta signed /255 (BGR24/BGRX32) e soma de escalas packed /15 (565/555), chaves e byte X, a partir do inicializador 00546D70; NativeSourceAlphaPixel porta kernels de mesma cor 12/13, modulação alpha/global e diferença do copy >=256; NativeSurfaceColorLayout porta máscaras DirectDraw, COLORREF→chave e upload alpha 10/11 (packed + nibble)/12 BGRA/13 RGBA; seleção +E0 e vínculo YGA ainda pendentes. NativePackedAlphaPixel porta 10/11 same-format (produtos /255, alpha/storage preservados) e 10->3/11->4 (q/15), base same-format copia três bytes. NativeQuadScanlines porta candidato de scanline 20 bits (0052CF10/00433150), incluindo arredondamentos, flag de divisor e clipping. NativeBgr24QuadRaster combina esse sampler com cópia/chave/mistura em CPU; domínio convexo não degenerado. ABI, assembly e comparação pendentes; demais formatos e máscaras não ligados. Roteiro 25 inclui inicializador e janelas dos kernels para confirmação assembly. [Contrato](../../docs/research/native-quad-raster-contract-2026-11-24.md). [Contrato](../../docs/research/native-surface-copy-contract-2026-11-24.md).
  Confirmar cadência e pixel blending com capturas Windows; não traduzir contadores estáticos em milissegundos sem medição.

## V18 — Áudio e vozes

<a id="v18"></a>

Integração gráfica inicial: DuelAudioPlayer abre Voice.dat adjacente ou --voice-dat, recursos originais por nome, cues de apresentação, música menu/duel1. --no-audio/--no-music. Associações provisórias, tabela de descritores ausente; vozes, música dinâmica e validação desktop pendentes.
  Reproduzir vozes/áudio a partir dos arquivos locais do usuário (a opção `--mute-joey` só configura a política; ainda não existe player), preservando a apresentação clássica. NativeJoeyReaction porta índice de descritor e parâmetro de duração (decompilação), sem player; tabela de nomes e x87 no roteiro 27. V18.1: NativeAudioRouting distingue Internal/Stream, volume e transição; NativeWaveInfo valida RIFF. --inspect-voice-dat e --review-voice= permitem inspeção e reprodução isolada por nome real, sem mapear IDs ou bloquear duelo. Roteiro 28 retornou 129 rejeições RIFF, reproduzidas no Voice.dat local. A inspeção e o player isolado reconhecem a variante PCM observada por tamanho e estrutura, com 106/131 entradas aceitas. Raylib decodificou joey_01.wav usando ALSA nulo. Restam 24 WAVs com quadro estéreo incompleto e uma entrada acima do limite do Archive. Escuta real pendente. V18.2: NativeDuelAudioCues porta requests de ativação, invocação especial, break/explosion, início, aviso de turno e Vija; seleção de Stream por diferença de LP em 0044C320. Sem player automático nem nomes presumidos. [Contrato](../../docs/research/native-joey-reaction-audio-2026-11-24.md).

## V19 — Fidelidade sistemática das demais telas/efeitos

<a id="v19"></a>

Depois da fatia carta/ATK/DEF, sem congelar o progresso funcional da alpha.

## V20 — Banco coin (00457520)

<a id="v20"></a>

Bundle 16 recebido; NativeCoinTimeline porta relógio 96/128, bits/defasagem, centros e escala. NativeCoinPassLayout recupera keys 0/1/2, recortes de giro/sombra, âncoras, halo e fade global, sinaliza ramo scratch/reflexo; SQLite v3 confirma coin.txt (14/4 divisores) e dimensões dos seis assets. NativeCoinResources e --review-coin=COUNT,BITS,X,Y,HIGHLIGHT renderizam passes 0/1/2 isoladamente com captura F12; NativeCoinExtraLayout confirma brilho key5 (cinco fases) e plano de scratch 3/4; review desenha brilho, exporta plano de reflexo sem inventar máscara. Remap, +94/+A0, composição nativa e integração A03 pendentes. Não contar revisão como Draw no duelo. [Draws](../../docs/research/native-bank-effect-draws-2026-11-24.md).
  Rotas 8E/8F, camada e início recuperados; nome do banco não comprova semântica de RNG. Roteiro 16; [contrato](../../docs/research/native-coin-dice-break-contract-2026-11-23.md).

## V21 — Banco dice (00456B90)

<a id="v21"></a>

Bundle 16 recebido; NativeDiceTimeline porta 128 updates, mutação no Draw, bancos, recortes, sombra e fade; --review-dice exige DAT. Remap/blend e integração A03 pendentes; não contar review como gameplay. [Draws](../../docs/research/native-bank-effect-draws-2026-11-24.md).
  Rotas 90/91/92, âncora e gate recuperados; Draw/Update ausentes. Roteiro 16, mesmo contrato.

## V22 — Remoção break (00450FD0)

<a id="v22"></a>

Bundle 16 recebido; NativeBreakTimeline porta cinco partículas, 16 updates, orientação e argumento de distorção. NativeBreakScatter reconstrói tabela 00A52EB4 via CRT srand(0)/rand, 256 bytes; porta blocos 4x2 e fases de dispersão, projeção com divisor injetado. Roteiro 29 confirma ABI/x87 de 00558FB0 e gerador; compositor restante pendente. [Draws](../../docs/research/native-bank-effect-draws-2026-11-24.md).
  Opcode 4F, camadas e separação de explosão identificados; esperar a coleta 16 antes de portar a timeline. Não alterar regras.

## V23 — Corrente (0041F870)

<a id="v23"></a>

NativeChainTimeline porta estados e seletores de 0041F810/830/870; comandos 08/09 e espera 128 não equivalem a bloqueio contínuo. Roteiro 17 e manifests v3 confirmam lista de sprites, recortes e geometria dos dígitos. Revisão isolada `--review-chain=` usa DAT, contagem manual e compositor diagnóstico. Integração à fila e comparação de pixels pendentes. [Contrato](../../docs/research/native-chain-affect-contract-2026-11-24.md).

## V24 — Destaque contextual affect (0040E260)

<a id="v24"></a>

NativeAffectTimeline porta fase, reset por identidade visual e ramo recuperável de vínculos. Foco por hit-test e comandos; sem recalcular regras. O roteiro 17 e o DAT confirmam scratch 50×50, recortes verticais com wrap e recomposição por troca de seletor. O PR #139 porta o plano de composição e testa a ordem dos passes. Faltam ramos de vínculos, compositor e validação em tela. [Evidência](../research/native-affect-scratch-2026-11-24.md).

## V25 — Ativação alternativa (00450AD0)

<a id="v25"></a>

Bundle 18 recebido: NativeAlternativeActivationTimeline.Sample porta carta grande, escala/fade e glow herdado; revisão --review-alternative=ID,CATEGORY com DAT. Integração A03 e comparação pendentes.
  NativeAlternativeActivationTimeline: -128, passo 16, 33 Updates, som em entrada zero, centro 525/300, camada 0x110; rotas 48/49/18. Draw ausente; roteiro 18. [Contrato](../../docs/research/native-alternative-activation-exclusion-2026-11-24.md).

## V26 — Exclusão (0043DBE0)

<a id="v26"></a>

Bundle 18 recebido: NativeExclusionTimeline porta 24 Draws, ordem mini/feixes/partículas/halo, fim e mascaramento; revisão --review-exclusion=ID,HEX (oculto exige ID 0). Remap/blend e integração A03 pendentes.
  NativeExclusionStart: descritor, argumento mascarado face-up, contador 64 e camada 0x120. Update vazio, Draw e produtor não mapeados. Não inventar duração nem disparar para todo banimento. Roteiro 18; mesmo contrato.

## V27 — Layerfont A (00455FF0)

<a id="v27"></a>

Modelo Draw +4 módulo 256, modo 5/7, âncora 788/588, intensidade por C; revisão isolada com DAT. Compositor e integração em modo nativo ainda pendentes; roteiro 19. [Contrato](../../docs/research/native-layerfont-help-2026-11-24.md).

## V28 — Layerfont B (00456360)

<a id="v28"></a>

Modelo Draw de entrada/saída condicionado a 005BDAD0/0044BA10, modo 3; revisão isolada, dimensão/compositor provisórios. Roteiro 19, mesmo contrato.

## V29 — Ajuda (0045E4A0)

<a id="v29"></a>

Roteiro 19 recebido no SQLite v2: NativeHelpTimeline porta request por update, gates, identidade 0x3FFF, escala vertical em 4 updates, clamps e âncora contextual. NativeHelpMailbox e NativeHelpProducers portam last-write-wins e precedência de requests de posição, batalha, hover, invocação normal e custo (decompilação dos produtores; não duplicam regras). Faltam textos/remap dos IDs, manifest help.txt, resolução/medidas DAT, compositor +4C e integração de apresentação. Não repetir coleta 19; não contar modelo isolado como Draw. [Contrato](../../docs/research/native-layerfont-help-2026-11-24.md).

## V30 — Vija / Destiny Board (004584C0)

<a id="v30"></a>

NativeVijaContract porta inicialização e predicado virtual +4 para ambos os modos; NativeVijaResources preserva manifesto e parâmetros de atlas. Roteiro 20 recebido: NativeVijaTimeline porta ordem dos passes, centros, fade, scratch e deformação por linha, 97/177 Draws. Máscara +98/remap e renderer ainda pendentes; não bloquear A03. [Contrato](../../docs/research/native-vija-selection-origins-2026-11-24.md). Roteiro 20 recebido: centros estáticos V11 em NativeSelectionOrigin; descritor D requer ajuste X vivo, sem assumir zero. Ver [handoff](../../docs/research/pending-bundles-handoff-2026-11-24.md).

## V31 — Bust-up de reações do Joey

<a id="v31"></a>

Modelo de roteamento de dano 00403900, distinção som-only/bust-up 00402E00, duração raw 00416B80. Roteiro 27 recupera tabela de severidade→manifesto e vtable 005DA840; posição, compositor, Update/Draw e gate ainda ausentes. NativeJoeyCinematicRouting porta 00403310 (ATK original, lado, códigos de evento e duas falas), 004152D0 e 00415C90 (start/result e variantes RNG), sem integração; roteiro 27 ampliado para vtables destas cenas. [Contrato](../../docs/research/native-joey-reaction-audio-2026-11-24.md).
