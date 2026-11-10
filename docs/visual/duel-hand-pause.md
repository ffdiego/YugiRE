# Duelo e animação da mão — ponto de retomada

**Pausado a pedido do usuário em 2026-11-09.** Registrar e preservar o trabalho
da sessão anterior; não iniciar novas consultas visuais nesta etapa. A investigação
realizada na interface web e publicada na `main` deve servir de base para a retomada.

## Entregue no protótipo

- `src/Joey.App/DuelScreen.cs` concentra a tela de duelo, antes em `Program.cs`.
  Slots alinhados, jogador ativo embaixo, ordem espelhada do adversário.
- Mão sobreposta na parte inferior, carta em foco desenhada por último e elevada.
  Balão Summon/Activate; botão direito alterna para Set; clique confirma.
- Invocação usa a primeira zona livre à esquerda, considerando tributos liberados.
  A seleção de tributos permanece explícita. Legendary Sword ainda pede um alvo.
- Set distingue cartas viradas para baixo de efeitos ativos. Somente os efeitos
  já suportados pelo demo podem ser ativados; demais cartas podem ser colocadas
  sem inventar seus efeitos. Não há correntes nem efeitos de flip implementados.
- O núcleo de colocação foi commitado anteriormente em `5b1559c`. O restante da
  apresentação e suas evidências são preservados no commit desta nota.

## Mapeamento estático já obtido

Alvo **Joey**, SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Todos os endereços são VA, image base `0x00400000`; não extrapolar para outros EXEs.

| VA | Evidência e papel |
|---|---|
| `0x004182E0` | Constrói objeto visual e referencia `duel/card/card.txt`. |
| `0x0041E3D0` | Consulta objetos visuais por hit test e atualiza o foco `0x005F2FDC`; percorre a lista de mão de trás para frente. |
| `0x0044DBC0` | Atualiza seleção; chamado pelo controlador `0x0044DD00`. |
| `0x0045C600` | Calcula largura da mão: `min((largura/2 + 8) * quantidade, 512) - largura/2`. |
| `0x0045C590` | Distribui posições horizontais conforme lado e quantidade. |
| `0x0045C8B0` | Lê foco, identifica região `0xB` no descritor, desloca alvo vertical em 32 pixels e suaviza coordenadas. |
| `0x0045C140` / `0x0045C280` | Resolvem posição por lado/região/índice; mão usa tabela dinâmica. |
| `0x004189D0` / `0x00418A80` | Atualizam descritor e inicializam posição do objeto visual. |
| `0x00418B60` | Atualiza movimento geral de carta, incluindo interpolação, rotação e escala. |

Na mão, `atual = alvo + trunc((atual - alvo) * 3 / 4)` por atualização.
O controlador `0x0044DD00` chama seleção e depois `0x0045C8B0` em um ramo
condicional. Isso não estabelece a frequência em milissegundos.

Tabela estática em `0x005DB608`: centros `(525,600)` e `(525,0)`.
Em `0x005DB560`: monstros próprios em X `369,447,525,603,681`, Y `352`,
e adversários na ordem inversa, Y `248`. O BMP original de campo mede 570×600
e é desenhado a partir de X=230 no protótipo.

`HandPresentation.cs` usa as fórmulas e elevação já identificadas; dimensões
108×153 foram escolhidas a partir do recurso `card_waku#l.bmp`. A ligação
exata entre essas dimensões e os globais de tamanho do renderer ainda não foi
fechada. Frequência de atualização, paginação em seis cartas, desenho do balão,
verso provisório e detalhes de interação são políticas do demo.

Relatórios preservados em `../evidence/hand-*.txt` e
`../evidence/card-coordinate-animation.txt`. Reutilizá-los antes de repetir
consultas ao Ghidra. `0x0041EA20` apareceu no rastreamento, mas é um helper
de inserção em contêiner; não o confundir com a animação propriamente dita.

## Retomar depois

1. Ler esta nota e as mudanças mais recentes da `main`, especialmente a
   investigação nativa/diferencial feita na interface web.
2. Validar no Windows alinhamento, ordem da mão, sobreposição e estabilidade
   do hover ao atravessar carta/balão e alternar Set com botão direito.
3. Conferir tributos em campo cheio, paginação, descarte que troca o jogador,
   Reset, ativação suportada e recusa explícita de efeitos ausentes.
4. Confirmar dimensões efetivas do renderer e frequência do update; comparar
   o movimento com o original antes de alegar reprodução visual exata.
5. Inspecionar os recursos de cursor `cursor_e_summon.yga` e `cursor_e_set.yga`
   se formos reproduzir os balões originais. O decoder YGA não foi implementado.

Compilação e testes C# disponíveis foram executados no fechamento; o modo
`--check-graphics` decodificou 793 BMPs e 1114 associações de arte.
**Sem validação visual nesta sessão SSH**; não foi executado jogo original
nem usada execução dinâmica para comprovar o movimento. Os gráficos e a
animação ficam pausados até nova solicitação.
