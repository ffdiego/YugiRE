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

## V03 — ATK/DEF do próprio monstro face-down e cores por posição

**Correção explícita do mantenedor:** os dois valores de ATK/DEF **também aparecem abaixo dos nossos monstros face-down**, inclusive quando estão em posição de defesa. O verso da carta permanece visível. O monstro do oponente face-down **não** divulga os atributos. Na defesa, ATK (o atributo não utilizado) aparece em cinza, conforme observação visual anterior; confirmar a coloração inversa/variações por estado no original.

**Evidência estática disponível:** `004194D0` consulta o lado, a região, flags de exibição e valores de ATK/DEF; chama `00480500` para os dois números e `00480300` para o separador. A decompilação possui variáveis de registradores ambíguas e não prova, isoladamente, o significado exato dos bits de face-down/lado. Revisar assembly, saltos, referências a `DAT_00A54E5C`, bit `0x8000`, escolha das variantes do atlas e o posicionamento `00419612..00419630`.

**Correção funcional no cliente:** `DuelCardLayout.OnBoard` agora expõe `AttributesCenter` para monstros do lado próprio mesmo quando face-down, e para os monstros face-up de ambos os lados. Continua oculto para o adversário face-down. `DrawDuelField` usa a geometria resultante e não elimina informações de um monstro do próprio jogador antes do contato da espada. Testes C# verificam ambos os lados e ataque/defesa.

**Avanço implementado (22/11):** três atlas BMP originais com seleção normal/aumentado/reduzido, glifo separador e parâmetros de composição `0xA0/0x100` para ATK/DEF em ataque e defesa. Passes e testes isolados em `NativeFieldAttributeFrame` com ligação ao `DuelScreen`. [Detalhes técnicos](native-field-attributes-2026-11-22.md).

**Avanço de visibilidade/camadas:** `00418EF0` só alcança `004194D0` para carta estabilizada (`obj+0x14 == 0x100`, estado !=5). `004194D0` exige região de monstros, perspectiva própria ou bit `0x8000`, e identidade atual no campo confirmada por `0056DD30`. O gate está em `NativeFieldAttributeVisibility`, com testes e integração entre renderer/F12. O F12 deixa de vazar números da CPU na janela entre revelação lógica e contato da espada. Tween 7/8 já ligado ao relógio de apresentação. Status `00419B70/0041A3B0/0041A070` é overlay independente da família numérica. [Rastreio](native-field-attributes-2026-11-22.md).

**Ainda pendente de paridade nativa:** âncora de `0045C140`, modos de blend, gates de efeitos ainda não correlacionados e capturas pareadas. Não marcar Verified.

## V04 — Cursor de ataque na Battle Phase

**Observação:** balão **Attack** acompanha o cursor sobre monstro apto a atacar. Não deve aparecer se o ataque não é permitido.

**Recurso identificado:** `cursor/cursor_e_atk.yga`, já mapeado em `NativeDuelCursor.ActionSprite(NativeCursorAction.Attack)`. A fonte da legalidade jogável atual é `DemoDuel.CanAttack(zone)`; isso **não comprova** o gate visual nativo, sua precedência sobre outras opções ou o hotspot. Rastrear o despacho do cursor e a ligação às flags/estados da fase de batalha no assembly. Não confundir com `j/duel/effect/atk_icon.bmp` (ícone/espada desenhado no campo).

**Estado atual (22/11):** `GlobalCursorScreen.ActiveWindowCursorSprite` agora seleciona o recurso original `cursor_e_atk.yga` para hover de monstro próprio com `DemoDuel.CanAttack(zone)` verdadeiro, exceto quando o jogador já está selecionando alvo. Testes sintéticos cobrem a autorização e negação do balão. **Pendente:** comparar a precedência e o hotspot reais do dispatcher Win32 no assembly; não chamar esse cursor de Verified.

## V05 — Cursores de mudança de posição

**Observação anterior:** `Attack Position` / `Defense Position` aparecem somente quando a mudança é legal. O cursor contém a opção permitida e não deve mostrar uma ação impossível.

**Recursos candidatos:** `cursor/cursor_e_setatk.yga`, `cursor/cursor_e_setdef.yga`. Mapear a seleção, o ciclo de escolhas e os gates nativos, então usar o Core para impedir ações ilegais. Não inferir o significado exato de cada sufixo sem calls ao recurso e comparação.

## V06 — Indicadores sobre os monstros e flashes de atributos

**Localização recuperada:** após desenhar os números, `004194D0` executa três subpasses independentes `00419B70`, `0041A3B0` e `0041A070` quando o descritor possui `0x8000` e a região é elegível. O construtor `004182E0` carrega **`duel/card/card.txt`**, não confundir com `duel/card/icon.txt` de `0041A800` (outra coleção).

**Família identificada:** índices 4/5 são alterações de atributo/tipo; 6/7 e 8/9, duas máquinas de estado; 10..13, flashes de ATK/DEF. Os comandos originais `0x5F`, `0x60` e `0x69` alteram os campos de indicador (escrita direta, adição saturada em 15, mínimo + máscara em 7). `00418B60` faz a animação **regressiva de 512 até 0 em 32 updates**, somente quando o objeto está estabilizado. `NativeCardStatusGeometry` deriva as âncoras de Y da **altura da miniatura DAT** e calcula recortes horizontais com número de colunas explícito. O X absoluto segue pendente devido a registradores ambíguos do decompilador. Modelo e testes no `Joey.LookAndFeel`. `scripts/summarize_native_card_status_dat.py` extrai nomes/dimensões a partir do JSON de `--inspect-visual-dat`.

**Estado:** *Pending* para Draw de efeitos (não conta como novo renderizador, não altera 19/34). A extração de disassembly específica está em `scripts/completed/04-card-status-ghidra.sh`; documentação em [status de cartas](native-card-status-overlays-2026-11-22.md). Só integrar os overlays ao cliente após mapear as texturas, recortes, âncoras e filtros de estados. Não inventar sprites a partir do prefixo do arquivo.

## Fronteiras e acompanhamento

- **Problemas de engine/regras:** continuam no plano de jogabilidade; não exigir assembly para implementar uma regra simples do Core que já tenha base textual e testes.
- **Problemas de apresentação:** uso de sprites, cor, posição, duração, ordem e cursor seguem a política assembly-first em `AGENTS.md`.
- As capturas do usuário são evidência das **diferenças observadas**, não prova isolada de constantes ou fórmulas de Draw. Atualizar este documento conforme cada VA for confirmado.
- O quinto item de uma lista recente veio sem descrição; não presumir qual seria. Incluir quando houver uma observação concreta.
