# Royal Command — supressão FLIP em Joey.Core (sandbox)

**Escopo inicial:** Partial. Diferenciar presença de efetividade, impedir
o efeito FLIP e revalidar escolhas pendentes sem impedir a revelação.

## Evidência nativa
- `0x5FA` = Royal Command (`1530`), armadilha contínua, classe DAT `21`.
  `0x2EF` = Jinzo (`751`); `0x409` = Royal Decree (`1033`).
- `00598D70` consulta Jinzo efetivo / presença de Royal Decree
  antes de calcular a máscara FLIP `0x20` de Royal Command.
- `00598C40` aplica `0x20` ao monstro FLIP, com filtro `00567620`.
- `0057EAC0` nega a admissibilidade de FLIP mediante contagem
  efetiva de Royal Command; `0057EE80` consulta o bit por ocorrência.
- `005BB720`, estado 4, revalida a solicitação antes de chamar
  o primário `0059DEB0` nos estados seguintes.
- `00588D90`, callback primário de Royal Command, reconhece outro
  pedido FLIP e marca o outro pedido com `0x0E`, sem destruir a
  ocorrência do monstro; confirmar janelas reais via captura.
- A interpretação completa da máquina de estados e prioridades
  está em `docs/cards/royal-command.md`.

## Contrato implementado
`FlipEffectSuppressionRules` consulta as ocorrências **face-up**
de Royal Command, Royal Decree e Jinzo. No sandbox restrito,
Royal Command suprime FLIP quando há uma cópia face-up e
**não** há Jinzo/Royal Decree face-up. Não é uma implementação
universal dos bits globais nativos.

`DemoDuel.TriggerFlip` impede criação e execução imediata de
efeitos FLIP enquanto a supressão está efetiva. A revelação,
Flip Summon, dano calculado e remoção de batalha seguem seu
caminho habitual.

Efeitos que já criaram um pedido também são revalidados no
momento da escolha: `PendingFlipTarget`, seleções FLIP
`PendingCardSelection` e opção de compra de Skelengel
(`PendingEffectDecision`). A supressão encerra o pedido sem
executar o efeito e retoma o combate diferido. Uma carta
não FLIP usando o mesmo seletor não deve ser negada.

A ativação por `ActivateSetSpell` ocorre na Main Phase do
controlador somente em turno posterior ao Set, espelhando o
modelo restrito já usado para Destiny Board. Não existe
ativação reativa no turno adversário ou cadeia completa.

## Resultado dos testes sintéticos

No CI inicial do PR #56, a solução .NET 10 compilou e a suíte
\`Joey.Tests\` aprovou **3.254 checks**, incluindo 22 novos checks
de Royal Command. O job Python legado falhou porque espera
322 IDs, embora o manifesto desse commit tenha 391.
O teste sintético de não-FLIP e cópias múltiplas foi acrescentado
em commit posterior e exige nova verificação pelo CI.

## Testes
`src/tests/Joey.Tests/RoyalCommandSuppressionChecks.cs`:
- armadilha setada vs ativa; turno de Set;
- Royal Command própria/adversária e múltiplas fontes;
- Jinzo face-up/oculto; Royal Decree face-up/oculto;
- Man-Eater Bug, Morphing Jar, Magician of Faith, Skelengel;
- revalidação de pedidos pendentes e controle sem supressão;
- combate com dano/destruição mantidos e remoção de Royal Command.

## Limitações
Não confundir `Done` neste recorte com `Verified`.
Sem replay nativo, prioridade de traps, ativação durante o turno
adversário, reinicialização global de `00598D70`, supressão
de outras categorias, Royal Command sobre novos seletores FLIP
ainda não integrados, ou representação completa de
`DAT_00A577EC`/`slot+0x8C`.
