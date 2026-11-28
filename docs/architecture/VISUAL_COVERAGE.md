# Cobertura de Draw nativas — YugiRE

Denominador: **rotinas Draw nativas distintas identificadas** em
[native-renderer-inventory.md](native-renderer-inventory.md). Uma rotina
compartilhada por várias famílias conta uma vez. Funções Draw ainda
desconhecidas não entram no denominador, que poderá aumentar.

**Implementadas (inclusive parciais): 29/39 (74.36%).**
**Validadas no Joey original: 0/39.**

Partial indica um componente visual C# relacionado, mas divergências
conhecidas. Done exige escopo visual concluído em C#; Verified exige
comparação com o executável original. Esta badge mede implementação,
**não fidelidade pixel-perfect comprovada**.

**Não cobre toda a UI:** seleções, diálogos e outros objetos com Draw
desconhecido ainda não entram neste denominador. Ver
[famílias visuais em investigação](../research/visual-unmapped-families-2026-11-21.md).

| VA Draw | Família | Estado | Implementação C# |
| --- | --- | --- | --- |
| `00449850` | Campo e troca de cenário | Partial | `src/Joey.App/DuelScreen.cs` |
| `0046F7A0` | Painel de detalhes | Partial | `src/Joey.App/DuelDetailScreen.cs` |
| `00418EF0` | Cartas e mão | Partial | `src/Joey.App/DuelScreen.cs` |
| `0043C980` | Deck, cemitério e pilhas | Partial | `src/Joey.App/DuelScreen.cs` |
| `004194D0` | Atributos ATK/DEF | Partial | `src/Joey.App/DuelScreen.cs` |
| `0047C660` | LP, dano e placar | Partial | `src/Joey.App/DuelDetailScreen.cs` |
| `00481420` | Fases, botões e troca de cor | Partial | `src/Joey.App/DuelPhaseScreen.cs` |
| `00480C60` | Avisos de fase e novo jogador | Partial | `src/Joey.App/DuelPhaseScreen.cs` |
| `0043B280` | Seleção nativa de cartas | Partial | `src/Joey.App/NativeSelectionListScreen.cs` |
| `0045E4A0` | Ajuda | Partial | `src/Joey.App/DuelHelpPresentation.cs` |
| `0040E260` | Efeito de estado persistente | Partial | `src/Joey.App/DuelAffectPresentation.cs` |
| `004142C0` | Combate declarado e espada | Partial | `src/Joey.App/AlphaDuelScreen.cs` |
| `004514B0` | Explosão de destruição | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `00450FD0` | Quebra / remoção alternativa | Partial | `src/Joey.App/DuelBreakPresentation.cs` |
| `0044F390` | Set | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `0044FA10` | Invocação normal | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `00450100` | Invocação especial | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `004505D0` | Ativação no campo | Partial | `src/Joey.App/DuelActivationScreen.cs` |
| `00450AD0` | Ativação alternativa | Pending | — |
| `00451980` | Abertura | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `00451E60` | Resultado do duelo | Partial | `src/Joey.App/DuelResultScreen.cs` |
| `004524F0` | Fusão | Partial | `src/Joey.App/DuelFusionPresentation.cs` |
| `004877F0` | Fusão | Pending | — |
| `00453560` | Embaralhamento | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `0041F870` | Corrente | Pending | — |
| `004531C0` | Indicador de corrente desativada | Partial | `src/Joey.App/DuelDisabledChainPresentation.cs` |
| `0043DBE0` | Remoção / exclusão | Partial | `src/Joey.App/DuelExclusionPresentation.cs` |
| `004870B0` | Sacrifício | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `00457520` | Moeda | Pending | — |
| `00456B90` | Dado | Pending | — |
| `00454010` | Exodia | Partial | `src/Joey.App/DuelCardEffectsScreen.cs` |
| `004584C0` | Vija | Pending | — |
| `00455FF0` | Layerfont A | Pending | — |
| `00456360` | Layerfont B | Pending | — |
| `00414DC0` | Invocação signature | Pending | — |
| `00415840` | Busto de abertura | Partial | `src/Joey.App/LiveOpeningBust.cs` |
| `00416260` | Busto de resultado | Partial | `src/Joey.App/LiveResultBust.cs` |
| `00416BC0` | Busto de reação | Partial | `src/Joey.App/LiveActivationReaction.cs` |
| `00444490` | Saída do banner de resultado | Pending | — |

O conjunto de endereços deve corresponder ao inventário.
Diálogos e ícones com Draw desconhecido permanecem listados no
inventário, mas não inflam a porcentagem.
