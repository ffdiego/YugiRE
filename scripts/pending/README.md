# scripts/pending — fila de coleta de evidência nativa

Os quatro roteiros de 22/11 foram executados e seus ZIPs privados
recebidos em 23/11/2026. **Coleta concluída, conhecimento consolidado.**
Os scripts são preservados para reproduzir evidências quando
necessário; não são executados por build, CI ou desenvolvimento
normal. Não versionar ZIPs, executável, DAT ou dumps de memória.

| Roteiro | Bundle recebido | Estado da coleta |
| --- | --- | --- |
| `01-selection-fusion-ghidra.sh` | `ghidra-selection-fusion-contract.zip` | Concluída (6/6, 3 reads) |
| `02-chain-disabled-ghidra.sh` | `ghidra-chain-disabled-contract.zip` | Concluída (5/5) |
| `03-field-attributes-ghidra.sh` | `ghidra-field-attributes-contract.zip` | Concluída (8/8) |
| `04-card-status-ghidra.sh` | `ghidra-card-status-contract.zip` | Concluída (17/17, 12 janelas) |

Manifestos confirmam o fingerprint original e missing=[].
Para os limites do disassembly e contratos extraídos,
ver [handoff dos quatro bundles](../../docs/research/visual-contract-bundles-2026-11-23.md).
**Não confundir coleta concluída com Draw implementado/Verified.**

## Dependências restantes, não repetir os quatro scripts

- Seleção/fusão: tabela runtime `00A52EE8` depois de
  `00A536E8=1`; constantes de centro, tabela de seletores e
  manifesto fusion/list; flag de espera. O ZIP não contém
  o valor de `005DCD00` nem a conversão `005C3E40`.
- Corrente desativada: `disable.txt` original, semântica do
  comando 0x72, integração da rotação e validação.
- ATK/DEF: comparação pareada em 800×600 e blend/cadência.
- Status: `card.txt` original incluindo o inteiro auxiliar de
  cada entrada (divisor de colunas); semântica de flags A/B,
  ramo especial e composição. Geometria ordinária já resolvida.

## Metadados DAT — coleta separada, quando necessário

Na máquina com o DAT original, o relatório integral já pode
ser extraído sem Ghidra:

```bash
dotnet run --project src/Joey.App -- /caminho/para/data.dat --inspect-visual-dat
python3 scripts/summarize_native_chain_dat.py
python3 scripts/summarize_native_card_status_dat.py
```

As saídas em `scratch/visual-review/` são privadas.
O relatório integral de manifests preserva os inteiros
auxiliares de cada entrada; o resumo de card-status ainda
não registra as colunas. Não deduzi-las só pela dimensão
do bitmap. Inspecionar o texto antes da integração.

## Protocolo de novas coletas

Cada novo roteiro deve especificar hipótese, endereços,
dependências, comando, artefato e critério de encerramento.
O exportador valida SHA-256, usa Ghidra -readOnly com
rollback e grava sob scratch/. Depois de consumir um bundle,
registrar conclusões em docs/research e marcar coleta como
concluída nesta fila. Novas pendências devem corresponder
a evidências específicas, não reexecutar coletas completas.
