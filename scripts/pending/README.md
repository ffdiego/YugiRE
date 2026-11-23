# scripts/pending — investigações nativas aguardando execução

Esta pasta guarda **roteiros de coleta de evidência, não implementações incompletas**. Eles não são invocados por `build`, `CI`, testes ou pelo loop normal de desenvolvimento. Cada script é autocontido, roda a partir de qualquer diretório, usa o projeto Ghidra **local** e escreve somente sob `scratch/` (ignorado pelo Git). **Não versionar o ZIP, o executável, o DAT nem dumps de memória.**

## Fila

| Roteiro | Pré-requisito | Produz | Estado |
| --- | --- | --- | --- |
| `01-selection-fusion-ghidra.sh` | Fedora/Linux com Ghidra e `ghidra-projects/Joey.gpr` | `scratch/ghidra-selection-fusion-contract.zip` | **Aguardando execução** |
| `02-chain-disabled-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-chain-disabled-contract.zip` | **Aguardando execução** |
| `03-field-attributes-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-field-attributes-contract.zip` | **Aguardando execução** |
| `04-card-status-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-card-status-contract.zip` | **Aguardando execução** |

### Seleção/fusão: o que já sabemos e o que NÃO sabemos

O primeiro roteiro obtém assembly de `0043B280`, `0055DCF0`, `004524E0/004524F0`, `004877B0/004877F0` e bytes estáticos da âncora e constantes do gerador x87. **Importante:** `00A52EE8` é uma tabela trigonométrica gerada em tempo de execução por `0055DCF0`; não espere obter seus 512 valores definitivos lendo apenas o PE no Ghidra. Depois da extração, recuperar os valores da tabela **após sua inicialização** com uma captura runtime ou reconstrução x87 comprovada. Consultar `docs/research/native-selection-window-fusion-2026-11-22.md`.

### Corrente desativada: próxima família visual independente

O segundo roteiro traz construtor, caminho de disparo e assembly de Update/Draw do objeto `005DB374`, que usa `j/duel/chain/disable.txt`. A decompilação já confirma `004531A0`: contador +1 por Update, desativa após 24; `004531C0`: quadro `(contador/2)%6`, com modo de composição alternativo por bit 0x40 do descritor. Ainda **não** conhecemos o índice do sprite no manifesto, as coordenadas efetivas de `0045C140`, o ponto exato de disparo e a rotação, portanto não inventar a apresentação. Essa é uma candidata a uma nova Draw no badge, **somente** após integração C# efetiva.

### Metadados do DAT para corrente desativada (sem Ghidra)

O escopo do inspetor `--inspect-visual-dat` já inclui `j\duel\chain\`. Na máquina com o `data.dat` original, executar:

```bash
dotnet run --project src/Joey.App -- /caminho/para/data.dat --inspect-visual-dat
python3 scripts/summarize_native_chain_dat.py
```

O segundo comando cria `scratch/visual-review/chain-manifests.json`, filtrando apenas o texto dos manifests e as dimensões das imagens (sem pixels, hashes de imagem ou arte gráfica). Verificar seu conteúdo e compartilhar este JSON **privadamente**, junto do ZIP de Ghidra. Não versionar nenhum dos dois. A extração dos metadados **não** identifica automaticamente o papel/ordem dos sprites: isso depende de `004531C0`, `0045C140` e do manifesto real.

### ATK/DEF — conferir a composição nativa antes da validação visual

O terceiro roteiro exporta assembly de `00418A80`, `00418EF0`, `004194D0`, `00417F40`, `00480200`, `00480230`, `00480300` e `00480500`. No cliente já estão integrados os três bitmaps originais `duel_fontm` e o split de opacidades 160/256; faltam comparar coordenadas absolutas, mascaramento, modo de blend e ordem Update/Draw. **Não** interpretar a existência desse roteiro como extração executada. O manifesto e dimensões dos BMPs podem ser inspecionados separadamente com `--inspect-visual-dat`. Ver [`native-field-attributes`](../../docs/research/native-field-attributes-2026-11-22.md).

### Indicadores de estado de carta e flashes de ATK/DEF

**Parte resolvida sem novo DAT:** `0041A070` e `0041A3B0` usam a entrada 8 do `font/number.txt` (`duel_fontm_y.bmp`, 70×10 pixels, glifos 7×10). O primeiro centraliza via `00480690`, o segundo desenha à direita via `004803C0`; `NativeCardStatusDigits` já implementa ambos os cálculos. O **próximo bundle é especificamente para descobrir o produtor de EBX/EDI e `iStack_6c`** nas chamadas virtuais e as dimensões/nomes reais dos ícones; não para redescobrir os algarismos. Ver [evidência da numeração](../../docs/research/native-card-status-digits-2026-11-22.md).


O quarto roteiro exporta assembly dos subpasses `00419B70`, `0041A070`,
`0041A3B0`, a animação `00418B60`, os comandos nativos `0040B270` e
`0040B370`, o dispatcher `005B9470`, o carregador da miniatura
`00418560` e funções do banco de sprites `duel/card/card.txt`. É
independente do roteiro 03 (glifos ATK/DEF). No Windows, `--inspect-visual-dat`
já exporta os metadados de `j\\duel\\card\\`; em seguida rode:

```bash
python3 scripts/summarize_native_card_status_dat.py
```

Ele cria `scratch/visual-review/card-status-manifests.json`, com índices
0..13, caminhos e dimensões sem pixels. A leitura falha se as quatro
âncoras conhecidas do manifesto não coincidirem com o contrato de
`00418EF0`; não tentar corrigir índices por estimativa. Os sprites
desenhados e sua semântica visual ainda exigem disassembly e inspeção DAT.
Ver [pesquisa dos indicadores](../../docs/research/native-card-status-overlays-2026-11-22.md).

## Uso

Depois de atualizar a `main` na máquina com Ghidra, executar apenas o roteiro pertinente, por exemplo:

```bash
bash scripts/pending/01-selection-fusion-ghidra.sh
```

O exportador `scripts/export_ghidra_visual_bundle.py` valida o SHA-256 de `joey_pc.exe`, executa o Ghidra em modo `-readOnly` com rollback e inclui decompilações/disassembly em um ZIP privado. Se `GHIDRA_INSTALL_DIR` não aponta à instalação correta, ajustá-la antes de executar. Fazer upload **somente do ZIP gerado** na conversa apropriada.

## Protocolo para novas pendências

Cada novo roteiro deve especificar: hipótese/objetivo, endereços-alvo, dependências, comando reprodutível, artefato esperado e critério de encerramento. Depois de processar o ZIP e registrar conclusões em `docs/research/`, atualizar esta fila para **concluído** ou remover o roteiro de pending em commit separado; não abandonar scripts silenciosamente. Scripts de uso geral permanecem em `scripts/`.
