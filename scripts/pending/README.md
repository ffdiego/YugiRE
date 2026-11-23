# scripts/pending — investigações nativas aguardando execução

Esta pasta guarda **roteiros de coleta de evidência, não implementações incompletas**. Eles não são invocados por `build`, `CI`, testes ou pelo loop normal de desenvolvimento. Cada script é autocontido, roda a partir de qualquer diretório, usa o projeto Ghidra **local** e escreve somente sob `scratch/` (ignorado pelo Git). **Não versionar o ZIP, o executável, o DAT nem dumps de memória.**

## Fila

| Roteiro | Pré-requisito | Produz | Estado |
| --- | --- | --- | --- |
| `01-selection-fusion-ghidra.sh` | Fedora/Linux com Ghidra e `ghidra-projects/Joey.gpr` | `scratch/ghidra-selection-fusion-contract.zip` | **Aguardando execução** |
| `02-chain-disabled-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-chain-disabled-contract.zip` | **Aguardando execução** |
| `03-field-attributes-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-field-attributes-contract.zip` | **Aguardando execução** |
| `04-flip-spelltrap-targets-ghidra.sh` | Mesmo ambiente Ghidra | `scratch/ghidra-flip-spelltrap-two-targets.zip` | **Aguardando execução** |

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

### FLIP com duas magias/armadilhas — callbacks que faltam

O quarto roteiro investiga Greenkappa (`585`, P085) e Tornado Bird (`1521`, P255).
Os oito slots catalogados de callback têm apenas uma disponibilidade decompilada
(`00583120`, Tornado Bird) no SQLite atual. O ZIP deverá conter assembly e
status de resolução para os sete endereços ausentes, seus helpers de seleção,
modos `0x0B/0x0E` e gate de IA. Confirmar filtros face-down, zona FIELD,
resolução de dois alvos e efeito de negação antes de reivindicar paridade
nativa. **O script registrado não foi executado.** Ver
[`P085/P255`](../../docs/cards/p085-p255-two-spelltrap-flip-sandbox.md).

## Uso

Depois de atualizar a `main` na máquina com Ghidra, executar apenas o roteiro pertinente, por exemplo:

```bash
bash scripts/pending/01-selection-fusion-ghidra.sh
```

O exportador `scripts/export_ghidra_visual_bundle.py` valida o SHA-256 de `joey_pc.exe`, executa o Ghidra em modo `-readOnly` com rollback e inclui decompilações/disassembly em um ZIP privado. Se `GHIDRA_INSTALL_DIR` não aponta à instalação correta, ajustá-la antes de executar. Fazer upload **somente do ZIP gerado** na conversa apropriada.

## Protocolo para novas pendências

Cada novo roteiro deve especificar: hipótese/objetivo, endereços-alvo, dependências, comando reprodutível, artefato esperado e critério de encerramento. Depois de processar o ZIP e registrar conclusões em `docs/research/`, atualizar esta fila para **concluído** ou remover o roteiro de pending em commit separado; não abandonar scripts silenciosamente. Scripts de uso geral permanecem em `scripts/`.
