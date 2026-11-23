# P085/P255 — Greenkappa e Tornado Bird: seleção de duas S/T

Status: **Done no escopo sandbox**. A solução .NET 10 compilou e
**3.242 verificações sintéticas passaram** no CI do PR #55, incluindo
14 cenários novos das duas cartas. **Não há paridade nativa verificada**.

## Evidências

- Greenkappa: `resourceId=585`, `P085`; callbacks catalogados
  `0057FA90`, `0057AC40`, `00593F80`, `00586040` ainda não decompilados.
  O texto original descreve destruir exatamente duas S/T face-down;
  o projeto documenta isso em
  `docs/research/card-static-sweep/07-selecao-vinculos-e-substituicao.jsonl`.
- Tornado Bird: `resourceId=1521`, `P255`; disponibilidade
  `00583120` decompilada verifica mais de uma S/T ocupada entre
  dois lados, `0056A320` cobre as zonas 5..10, incluindo FIELD.
  Candidatos `0057B530`, preparação `00597270`, primário
  `005909A0` ainda ausentes do SQLite.
- `00576940` encaminha operação 0x0E para `00576960` (destruição);
  `00577A40` encaminha 0x0B (retorno). As associações diretas desses
  helpers aos callbacks ainda dependem das análises anteriores e de
  exportação adicional Ghidra.
- Ambas pertencem ao pack Joey (packMask 4). Estas notas distinguem
  assembly disponível, interpretação e comportamento de sandbox.

## Contrato C#

`FlipSpellTrapSelectionRules` seleciona S/T nas cinco posições comuns
e na zona FIELD, dos dois lados. `DemoDuel.PendingFlipSpellTrapTargets`
exige duas ocorrências distintas e suspende outras ações e o combate.
`SelectFlipSpellTrapTarget` permite escolher/desmarcar e revalida
identidade por `ReferenceEquals`, posição e face antes de aplicar
qualquer movimento. Não move a primeira carta antes da segunda escolha.

Greenkappa destrói as duas selecionadas reutilizando a limpeza de
`DestroySpellTrapCard`. Tornado Bird devolve as duas aos controladores
(sandbox: controlador=dono), com remoção especial do vínculo de Equip
sem enviar a carta ao cemitério, além da limpeza da zona FIELD. Para
Destiny Board face-up, preserva a limpeza de mensagens implementada no
sandbox para MST; prioridade e ordem de chains ficam pendentes.

O dispatcher de FLIP usa os caminhos já existentes de Flip Summon,
revelação em combate com dano calculado antes da escolha, e revelação
externa. A CPU decide um alvo por Step, priorizando S/T adversárias.
A UI de campo usa o quadro original para candidatos e selecionados,
com prompt provisório (não visualmente Verified).

## Cenários

`FlipSpellTrapSelectionChecks.cs` inclui: dois próprios ou de ambos os
lados, Set/face-up, FIELD, equipamento, contagem de zero/um, seleção
repetida, ocorrência substituída, escolha CPU, Flip Summon, combate
diferido e revelação externa.

Limitações: não implementa cadeias, supressão Royal Command,
responsabilidade dono/controlador distintos, proteção/negação de alvo,
geração durável de instância ao substituir publicamente o mesmo objeto
`Card`, nem ordenação/timing nativo dos movimentos.

Ghidra pendente: `scripts/pending/04-flip-spelltrap-targets-ghidra.sh`
versionado neste PR; **não executado** neste ambiente. Produz ZIP privado
em `scratch/ghidra-flip-spelltrap-two-targets.zip` na máquina com Ghidra.
