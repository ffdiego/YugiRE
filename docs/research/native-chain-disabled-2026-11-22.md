# Indicador de corrente desativada — contrato parcial (22/11/2026)

**Alvo:** Joey the Passion Win32, `joey_pc.exe` SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. Fontes: inventário de vtables e decompilações transitórias documentadas em [visual-virtual-methods](visual-virtual-methods-2026-11-22.md). **Sem nova captura do original nesta etapa.**

## Caminho nativo conhecido

| Elemento | Evidência de análise estática |
| --- | --- |
| Disparo candidato | `00408730`: verifica condições de apresentação e cria o objeto |
| Construtor | `00452F20`, vtable `005DB374`, camada `0x111` no inventário |
| Update | `004531A0`: incrementa contador +1, desativa ao alcançar `0x18` |
| Draw | `004531C0`: índice `(contador/2)%6`, ramificação de composição dependente do bit `0x40` do descritor |
| Recurso | Manifest `j\duel\chain\disable.txt`; ordem e dimensões ainda não verificadas aqui |
| Geometria | Consumidor `0045C140` ainda precisa de assembly e argumentos |

`24 Updates` não significa 24 frames de tela nem uma duração em segundos. O Draw pode ser chamado em cadência distinta. Com contador inicial 0, os índices para `0..23` são `0,0,1,1,...,5,5` duas vezes; depois o objeto se torna inativo. Isso é **um contrato de índice**, não uma animação completa.

## Implementação nesta etapa

- `src/Joey.LookAndFeel/NativeDisabledChainFrame.cs`: contador, amostragem de índice e flag `0x40`, sem acesso a raylib, DAT, Core ou FPS. Valida intervalos e interrompe amostragem depois do update 24.
- `NativePresentationChecks.cs`: testes sintéticos de dois ciclos completos, flag separada do índice, limites, desativação e reset.
- `scripts/summarize_native_chain_dat.py`: extrai do relatório de `--inspect-visual-dat` somente manifests e metadados de imagens sob `j\duel\chain\`, apontando manifests esperados ausentes. Não recupera imagens.
- O inventário **continua com `004531C0` como `Pending`**: nenhum Draw novo está integrado ao `Joey.App`; o total permanece 19/34 (0 Verified).

## Coleta independente pendente

Na máquina com o DAT original:

```bash
dotnet run --project src/Joey.App -- /caminho/para/data.dat --inspect-visual-dat
python3 scripts/summarize_native_chain_dat.py
```

Saída: `scratch/visual-review/chain-manifests.json` (inspecionar e compartilhar privadamente, sem commitar).

Na máquina com Ghidra:

```bash
bash scripts/pending/02-chain-disabled-ghidra.sh
```

Saída: `scratch/ghidra-chain-disabled-contract.zip` (não versionar). Relacionar a ordem dos recursos do manifesto com o Draw real; rastrear o fluxo `00408730 -> 00452F20` e os argumentos de `0045C140`, o pass condicionado pelo bit `0x40`, pivô, escala, blend e orientação. Só então integrar os YGAs/BMPs ao cliente e comparar com o executável em 800×600.

**Limite:** o modelo não dispara o indicador em um duelo nem bloqueia entrada. Conectar agora sem evidência de gatilho/composição seria simulação, não fidelidade.

