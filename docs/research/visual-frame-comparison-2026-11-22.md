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

Para inspecionar elementos pontuais, repita `--region NOME X0 Y0 X1 Y1`.
O canto inferior direito é exclusivo e os retângulos podem se sobrepor. As
regiões adicionais mantêm as métricas globais e as duas regiões padrão.
O relatório registra o retângulo de cada região e os limites de divergência
em coordenadas do canvas.

Em uma regressão com frames alinhados, `--max-mismatched-pixels 0` exige
igualdade dentro do limiar RGB selecionado. O processo retorna 1 quando
ultrapassa o limite e ainda grava todos os diagnósticos. Sem esse argumento,
a contagem de diferenças é informativa e o processo retorna 0.

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
caminhos de saída que apontam para uma imagem de entrada ou para a mesma saída, inclusive com links simbólicos e hard links.

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
limiares RGB e de regressão, regiões sobrepostas, opacidade e proteção dos
arquivos de entrada.

## Matriz de quadros-chave

Salve a lista de pares em `scratch/visual-review/pairs.json`. Os caminhos
relativos apontam para arquivos a partir da pasta do manifesto. Cada `id`
identifica o cenário e o quadro-chave no relatório.

```json
{
  "schemaVersion": 1,
  "pairs": [
    {
      "id": "example-impact",
      "reference": "reference-impact.png",
      "candidate": "candidate-impact.png",
      "referenceUpdate": 16,
      "candidateUpdate": 17,
      "threshold": 0,
      "maxMismatchedPixels": 12,
      "maxUpdateDelta": 1,
      "regions": {
        "impact": {
          "boundsExclusive": [400, 210, 520, 340],
          "maxMismatchedPixels": 0
        }
      }
    }
  ]
}
```

```bash
python scripts/compare_visual_frame_matrix.py scratch/visual-review/pairs.json \
  --output scratch/visual-review/matrix
```

O `matrix-report.json` agrega os resultados e aponta para os diagnósticos
gravados na subpasta de cada par. `updateDelta` é `candidateUpdate - referenceUpdate`.
Use contadores aferidos desde o mesmo evento de início. A ferramenta registra
os valores fornecidos. A correspondência de estado e a origem de cada contador
dependem das capturas. Os campos `threshold`, `maxMismatchedPixels` e
`maxUpdateDelta` são opcionais. O limiar RGB padrão é 0 e o limite de updates
usa o valor absoluto da diferença. O comando processa todos os pares válidos e
retorna 1 quando algum excede um limite declarado.

Cada par aceita regiões opcionais com limites direito e inferior
exclusivos. Os retângulos podem se sobrepor. O relatório resume a contagem
e os limites da divergência de cada região em coordenadas do canvas.
O limite de pixels da região é opcional e usa o mesmo limiar RGB do par.
Uma região que excede seu limite faz o par falhar mesmo quando a diferença
total está dentro do limite. Os retângulos no exemplo são ilustrativos.

Quando as capturas tiverem JSONs de estado, o manifesto pode apontar para
eles com `referenceMetadata` e `candidateMetadata`. Cada contador
aceita um inteiro declarado ou o nome de um campo no JSON daquela captura.
Os nomes em `stateFields` identificam os campos do nível superior, na
ordem referência e candidato.

```json
{
  "id": "example-impact-with-metadata",
  "reference": "reference-impact.png",
  "candidate": "candidate-impact.png",
  "referenceMetadata": "reference-impact.json",
  "candidateMetadata": "candidate-impact.json",
  "referenceUpdate": "elapsedUpdates",
  "candidateUpdate": "cardEffectUpdate",
  "stateFields": {
    "phase": ["duelPhase", "phase"],
    "perspective": ["perspective", "playerPerspective"]
  }
}
```

Se os campos declarados divergem, o relatório indica quais deram
diferença, deixa o par sem pontuação e segue com os outros pares.
O processo retorna 1 quando houver um par nesse estado. Campo
ausente ou contador inválido impede a comparação antes da primeira
gravação. Use as mesmas unidades e a mesma origem para os contadores.
A seleção dos campos e o vínculo de cada JSON com seu PNG continuam
sob responsabilidade de quem monta o par.

```bash
python -m unittest discover -s scripts/tests -p 'test_compare_visual_frame*.py' -v
```
