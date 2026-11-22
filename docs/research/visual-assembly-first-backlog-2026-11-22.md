# Fila visual assembly-first — observações e hipóteses (22/11/2026)

**Objetivo:** transformar diferenças relatadas pelo mantenedor em perguntas verificáveis para o assembly de `joey_pc.exe` e seus assets `data.dat`, evitando novos deslocamentos, tempos e filtros escolhidos por tentativa e erro.

**Situação:** investigação aberta; as evidências disponíveis permitem localizar os candidatos abaixo, **não** afirmam que suas condições de Draw foram inteiramente reconstruídas. O código atual pode conter aproximações para manter a alpha jogável; elas não devem ser promovidas a comportamento nativo sem comprovação.

## Processo de aceite por item

| Etapa | Evidência mínima |
| --- | --- |
| Localização | VA da rotina, caller/callee e contexto (lado, estado, zona, fase) |
| Recurso | Caminho no `data.dat`, recorte e variante condicionada por estado |
| Geometria | Expressão/argumentos nativos para X/Y, escala, pivô, orientação; conversão para coordenadas lógicas |
| Timeline | Contador nativo, Update/Draw, ordem com eventos e gates de execução |
| Implementação | Código C# e testes para estados positivos/negativos, sem dependência de timing do `Joey.Core` |
| Verificação | Captura/janela de updates do executável original, mesmo estado no YugiRE; diferenças residuais explícitas |

Preferir consultar o SQLite/decompilações e o ZIP visual já extraído, inclusive versões transitórias (por exemplo `retry-039-0047C660.txt`), antes de pedir nova exportação. Quando um dado depender de disassembly/VA ausente, executar `scripts/export_ghidra_visual_bundle.py --targets ... --disassemble --retry-missing` na máquina com o projeto Ghidra; não preencher a lacuna com uma constante de screenshot.

## V01 — Posição dos números flutuantes de LP

**Observação nativa do usuário:** perda de LP por cartas/efeitos é exibida na região da fileira de magias; perda de LP por batalha aparece sobre o monstro envolvido. Uma âncora global `NativeLifeHud.FloatingNumberAnchor` entre as duas fileiras **não cobre ambos os casos**.

**Candidatos de fluxo:** `00414110` para resolução de combate, `00408470` para mudanças gerais, `0047C4F0` para configuração do delta, `0047C560` para a rampa, `0047C660` para o Draw, `004806F0` para a composição dos algarismos. A interpretação de identificadores de zona e a origem efetiva de X/Y devem ser confirmadas no assembly e callers, incluindo casos de ataque direto. O documento `native-life-point-presentation.md` já registra um Draw transitório de `0047C660`, embora a decompilação tenha argumentos ambíguos.

**Matriz de validação:** dano de magia e cura, batalha monstro vs monstro, batalha contra face-down, dano direto sem defensor, lado do jogador/CPU, alvo removido ou preservado. Medir âncora, texto, animação, z-order e ordenação após ativação/espada. **Não substituir o ponto provisório por outro ponto estimado.**

## V02 — Banner/texto de troca de jogador

**Observação:** na passagem de turno existe texto/efeito equivalente, mas distinto, do banner de troca de fase.

**Fonte identificada:** `j/duel/phase_name/nex_e_b.yga`, `nex_e_b1..8.yga`, `nex_e_r.yga`, `nex_e_r1..8.yga`; os nomes por si só não estabelecem o estado de seleção nem o timing. Na família de avisos, os endereços candidatos são `00480B40`, `00480BC0` (inicialização), `00480C20` (Update) e `00480C60` (Draw). Confirmar via Xrefs qual ramo usa os recursos `nex_` e em que ordem ele ocorre relativamente ao encerramento de fase, animação de virada do botão de turno e início do novo turno.

**Situação do cliente:** `DuelPhaseScreen.DrawPhaseNotice` desenha os banners de fase e da Battle Phase, mas não compõe o anúncio `nex_` do próximo jogador. A referência espacial é 800×600, sem pressupor o mesmo centro/timeline dos avisos de fase.

## V03 — ATK/DEF em todos os monstros revelados, inclusive defesa

**Observação:** ambos os valores continuam visíveis em carta face-up no modo de defesa, mas o valor **não usado** fica cinza; o caso especificamente observado é ATK cinza quando a carta está em defesa.

**Candidatos:** `004194D0 → 00480500 / 00480300`, atlas `j/duel/font/number.txt`, flags de face/posição/lado e a geometria de `00419612..00419630`. Confirmar no assembly se ATK/DEF usam chamadas separadas, tonalização ou outra variante, e os critérios exatos de cinza/realce e orientação.

**Estado atual:** `DuelCardLayout.OnBoard` já calcula `AttributesCenter` para monstros face-up mesmo em defesa; `DrawDuelField` renderiza `"ATK / DEF"` com `Label` monocolorido. A lacuna verificada é o desenho/cor dos atributos, não necessariamente a mera visibilidade em defesa. Testar ataque/defesa, ambos os lados, face-down/face-up, modificadores de atributos e atacante/defensor.

## V04 — Cursor de ataque na Battle Phase

**Observação:** balão **Attack** acompanha o cursor sobre monstro apto a atacar. Não deve aparecer se o ataque não é permitido.

**Recurso identificado:** `cursor/cursor_e_atk.yga`, já mapeado em `NativeDuelCursor.ActionSprite(NativeCursorAction.Attack)`. A fonte da legalidade jogável atual é `DemoDuel.CanAttack(zone)`; isso **não comprova** o gate visual nativo, sua precedência sobre outras opções ou o hotspot. Rastrear o despacho do cursor e a ligação às flags/estados da fase de batalha no assembly. Não confundir com `j/duel/effect/atk_icon.bmp` (ícone/espada desenhado no campo).

**Estado atual:** `GlobalCursorScreen.ActiveWindowCursorSprite` seleciona balões da mão, mas não o `Attack` da carta no campo. O item é pendente, não testado.

## V05 — Cursores de mudança de posição

**Observação anterior:** `Attack Position` / `Defense Position` aparecem somente quando a mudança é legal. O cursor contém a opção permitida e não deve mostrar uma ação impossível.

**Recursos candidatos:** `cursor/cursor_e_setatk.yga`, `cursor/cursor_e_setdef.yga`. Mapear a seleção, o ciclo de escolhas e os gates nativos, então usar o Core para impedir ações ilegais. Não inferir o significado exato de cada sufixo sem calls ao recurso e comparação.

## Fronteiras e acompanhamento

- **Problemas de engine/regras:** continuam no plano de jogabilidade; não exigir assembly para implementar uma regra simples do Core que já tenha base textual e testes.
- **Problemas de apresentação:** uso de sprites, cor, posição, duração, ordem e cursor seguem a política assembly-first em `AGENTS.md`.
- As capturas do usuário são evidência das **diferenças observadas**, não prova isolada de constantes ou fórmulas de Draw. Atualizar este documento conforme cada VA for confirmado.
- O quinto item de uma lista recente veio sem descrição; não presumir qual seria. Incluir quando houver uma observação concreta.
