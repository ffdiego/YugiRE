# Geometria dos algarismos dos indicadores de carta — 22/11/2026

## Fontes e evidências

Consulta às decompilações de `0041A070`, `0041A3B0`,
`00480690`, `004803C0`, `00480230` e `00417F40` do SQLite
`joey-knowledge-visual-decoded.sqlite`, que contém o EXE original com
SHA-256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Os metadados privados já extraídos do DAT (`life-font-evidence.json`)
confirmam o `number.txt` original: entrada índice 8
`duel_fontm_y.bmp`, **70 × 10 px, 10 colunas** — células **7 × 10 px**.

**Os indicadores A e B compartilham essa fonte amarela**. Não confundir
com os três atlas de ATK/DEF (normal, aumentado, reduzido), nem com
o banco de sprites da carta em `duel/card/card.txt`.

## A diferença nos dois Draws

- `0041A070` (indicador A) chama `00480690`. Essa rotina mede a
  largura da sequência numérica, desloca o limite direito por
  `textoWidth/2` e o topo por `fontHeight/2`; depois chama
  `004803C0` para desenhar os algarismos.
- `0041A3B0` (indicador B) chama `004803C0` diretamente, usando
  seu próprio **limite direito** da numeração.
- `004803C0` avança da direita para a esquerda: antes de cada desenho
  subtrai a largura de um glifo do X, usa `valor % 10` para escolher a
  coluna, e então divide `valor / 10`. Em `15`, desenha primeiro `5`,
  depois `1`. O zero produz um único glifo.

`NativeCardStatusDigits` codifica os dois modelos em `Joey.LookAndFeel`:
`Centered` e `RightAnchored`, retornando recortes e destinos sem
tocar no `Joey.Core`. `DatGraphics.CardStatusNumberFont` aponta para
o bitmap já carregado como `LifeNumberFallback`. Testes cobrem `15`,
`7`, `0`, recortes, dimensões e entradas inválidas.

## Expressões X recuperadas e lacuna do Ghidra

As decompilações contêm somente parte da equação do X:

| Draw | Equação parcial | O que está incerto |
| --- | --- | --- |
| `0041A070` | `objX - unaff_EBX/2 + iStack_6c + 6` | produtor de `EBX` e eventual escrita de `iStack_6c` pelo método virtual |
| `0041A3B0` | `objX + unaff_EBX/2 - 3` | produtor de `EBX` e qual textura determina sua largura |
| `00419B70` | variáveis `unaff_EBX`/`unaff_EDI`/`unaff_EBP` e coordenadas virtuais | ordem e proveniência dos operandos |

**Não concluir que EBX é uma largura específica** nem fixar uma
coordenada absoluta por suposição. A função auxiliar
`HorizontalFromDecompilerOperands` mantém os valores do decompilador
como entradas explícitas. Esse helper é uma forma de preservar a
álgebra, **não** uma autorização para desenhar o sprite.

O script pendente `scripts/pending/04-card-status-ghidra.sh` foi
expandido para os helpers numéricos e agora solicita **12 janelas
exatas de disassembly** nos endereços de retorno identificados na
decompilação. O exportador `--instruction-window VA:COUNT` imprime
marcadores início/fim e falha se o Ghidra iniciar a leitura numa
instrução diferente, se o intervalo estiver ausente/incompleto, ou
se houver saída duplicada. A extração ainda **não foi executada**
no projeto Ghidra local, e não contém o executável nem as texturas. Ao executá-lo no Fedora/Ghidra,
correlacionar instruções e produção dos registradores perto de
`0041A1E6`, `0041A1F5`, `0041A23D`,
`0041A520`, `0041A52E` e `0041A580`; depois cruzar as larguras
com o `card-status-manifests.json` do DAT original.

**Estado:** layout dos algarismos implementado e testado,
ícones e flashes gráficos ainda **Pending**. Não aumentar cobertura
visual: **19/34 Partial, 0 Verified**.

### Auditoria das evidências locais

A inspeção dos bundles privados disponíveis nesta sessão
(`ghidra-visual-batch.zip` e
`ghidra-visual-virtual-methods.zip`) não encontrou linhas
`INS` cobrindo `0041A1E6`, `0041A1F5`,
`0041A23D`, `0041A520`, `0041A52E` ou
`0041A580`. O SQLite possui **decompilações**, não uma
tabela de instruções dessas regiões. Portanto, ainda não é
possível fechar o X por leitura de assembly somente com
esse corpus.

O script `04-card-status-ghidra.sh` preserva todos os
endereços de retorno e captura o trecho na memória do
projeto Ghidra com `ins:VA:COUNT`, somente-leitura. A
próxima revisão deve examinar os blocos `INS_WINDOW_BEGIN`
e `INS_WINDOW_END` e vincular cada operando ao produtor
antes de autorizar um novo Draw no cliente.
