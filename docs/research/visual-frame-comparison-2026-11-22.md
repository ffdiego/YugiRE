# Comparação de frames 800×600 — YugiRE vs. Joey (22/11/2026)

A captura F12 já grava um PNG do canvas do YugiRE e um JSON de estado em
`scratch/visual-review/`. O utilitário `scripts/compare_visual_frames.py`
compara **dois PNGs alinhados de 800×600** sem alterar os originais nem
inventar coordenadas. É uma ferramenta de *diagnóstico*, não um verificador
automático de paridade nativa.

## Uso

O primeiro PNG deve ser um recorte **exato do canvas** de 800×600 do Joey
original; o segundo, o PNG produzido pela tecla F12 no YugiRE. Os dois devem
estar no mesmo estado lógico, perspectiva e ponto da animação. A comparação
não redimensiona, não registra deslocamentos nem tenta adivinhar estados.

Requer Python 3 e Pillow (`python -m pip install Pillow` no ambiente Python
selecionado). Na raiz do projeto:

```bash
python3 scripts/compare_visual_frames.py \
  scratch/visual-review/joey-original.png \
  scratch/visual-review/yugire-exemplo.png \
  --output scratch/visual-review/comparison
```

A opção `--threshold N` ignora na **máscara** diferenças de canal menores
ou iguais a N (0 a 255). Com threshold 0, qualquer canal RGB distinto conta
como pixel divergente. A métrica de erro médio, por sua vez, mantém todos os
erros RGB, inclusive os abaixo do threshold.

### Saídas (sempre locais, em `scratch/`)

| Arquivo | Significado |
| --- | --- |
| `report.json` | Total e porcentagem de pixels divergentes, erro médio RGB, maior diferença de canal e retângulo de diferenças (limite direito/inferior exclusivo) |
| `difference.png` | Diferença absoluta RGB por canal, preto quando idêntico |
| `mismatch-mask.png` | Vermelho para pixels diferentes acima do limiar, preto caso contrário |
| `overlay.png` | Mistura visual 50% / 50% dos dois frames, útil para observar deslocamentos |

O relatório separa **sidebar** `x=[0,230)` do **board** `x=[230,800)`.
Essa divisão vem de `DuelSurfaceLayout.Board`, e não foi inferida das
capturas. Não é ainda um detector de cartas, sprites nem regiões dinâmicas.
O utilitário rejeita entradas de tamanho errado, pixels transparentes ou
um caminho de saída que sobrescreveria as imagens de entrada.

## Limites e prioridades

- Um número alto de pixels divergentes **não prova** erro de renderização se
  os estados, fontes, idioma, frames, cartões, posição do mouse ou update não
  coincidirem. Conferir primeiro os metadados F12.
- O PNG F12 captura o **canvas antes do cursor global**, que é desenhado
  sobre a janela escalonada. Portanto, esta comparação **não** avalia o cursor
  ou seu hotspot; para isso, comparar capturas da janela completa.
- Ao capturar o original, remover bordas/letterbox sem redimensionar. Não
  deixar o ponteiro do mouse interferir na área comparada.
- `mismatch-mask.png` é imagem diagnóstica, não textura do jogo.
- Não promover funções Draw a `Verified` com um único score: a prova exige
  frames correspondentes e revisão visual por elemento.
- Comparações e imagens proprietárias continuam privadas em `scratch/`.

## Correção associada: metadados de cursor

A captura registrava `actionCloud` derivando somente de `CurrentHandAction`,
mesmo quando `ActiveWindowCursorSprite` já mostrava `Attack` ao passar sobre
um monstro próprio durante a Battle Phase. Agora `actionCloud` é obtido do
**mesmo sprite efetivamente escolhido** para a janela, por uma inversão
pura do mapa de YGAs em `NativeDuelCursor.ActionForSprite`. Essa mudança não
altera nem reposiciona pixels do cursor nem afirma paridade com o EXE.

## Testes

```bash
python3 -m unittest scripts/tests/test_compare_visual_frames.py -v
```

Testes sintéticos cobrem frames iguais, diferenças na fronteira x=229/230,
threshold, limites de região, opacidade e bloqueio contra sobrescrita.
