# Contrato visual estático de Joey — consolidação em Joey.LookAndFeel

**Alvo de engenharia reversa:** `joey_pc.exe`, SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`,
image base `0x00400000`. Endereços a seguir são VAs **dessa versão**.

**Fonte desta consolidação:** a branch
[`analise-visual`](https://github.com/ffdiego/YugiRE/tree/analise-visual),
com relatórios de análise estática
[`rendering-architecture.md`](https://github.com/ffdiego/YugiRE/blob/analise-visual/docs/research/visual/rendering-architecture.md),
[`animation-formats.md`](https://github.com/ffdiego/YugiRE/blob/analise-visual/docs/research/visual/animation-formats.md)
e
[`activation-effect.md`](https://github.com/ffdiego/YugiRE/blob/analise-visual/docs/research/visual/activation-effect.md).
A investigação anterior da mão está em
[`duel-hand-pause.md`](../visual/duel-hand-pause.md).
**Este commit extrai somente os contratos sustentados pela evidência**, não
importa automaticamente toda a branch de pesquisa nem dados do jogo.

## Separação de responsabilidades

```text
Joey.Core
  | alterações de estado, regras, dano, efeitos FLIP
  v
Joey.LookAndFeel
  | coordenadas, movimento, parâmetros dos passes, contadores,
  | máscaras de despacho, timing selecionado pelo usuário
  v
Joey.App (raylib)
    entrada, renderização, recursos DAT locais, relógio de quadros
```

Os três arquivos `HandPresentation.cs`, `NativeAnimation.cs` e
`DuelLookAndFeel.cs` ficam em `src/Joey.LookAndFeel`. O Core **não** depende
do assembly de apresentação. O método `MoveCardAxis` é virtual: Classic aplica
um passo nativo por *chamada do cliente*, Fast aplica dois, Instant salta à
posição final. Isso altera **só a apresentação**, não o número de turnos,
compras ou respostas lógicas. O FPS do programa original ainda não foi medido.

## Geometria e mão

| Função/variável original | Fato sustentado | Tipo |
| --- | --- | --- |
| `0x0045C140` / `0x0045C280` | Localiza carta por lado, região e slot | Estático |
| `0x0045C590` / `0x0045C600` | Posições e largura da mão; limita largura a 512 | Estático |
| `0x0045C8B0` | Suaviza movimento do hover e eleva alvo em 32 | Estático |
| `0x005DB560` | Centro dos monstros, X 369/447/525/603/681, Y próprio 352, adversário 248 | Estático |
| `0x00418B60` | Interpola posição, rotação e escala do objeto visual | Estático |

A função de aproximação implementada é
`target + trunc((current - target) * 3 / 4)` **por update**.
`HandPresentation.CenterX` mantém as fórmulas anteriores, agora fora do
`Joey.Core`. Os `108×153` pixels das cartas na mão, o tamanho
`66×88` dos slots e a posição das zonas de magias são **escolhas do cliente**
ainda não confirmadas como dimensões nativas universais. Os centros e a ordem
espelhada dos monstros vêm da evidência, os retângulos são a nossa UI.

O `Joey.App` usa `NativeDuelLayout.MonsterBounds` para zonas de monstros,
`HandPresentation.CenterX` para a mão e
`presentation.Style.MoveCardAxis` para o movimento de hover.

## Objeto visual e despacho

A investigação de `0x0045D040` (update),
`0x0045D170` (draw) e `0x0045D150` (comparador) sustenta:

- byte `obj+0x04`: objeto ativo;
- DWORD `obj+0x08`: máscara que precisa cruzar a máscara solicitada;
- int `obj+0x0C`: chave de ordenação **crescente**;
- vtable `+8`: update; `+0x0C`: desenho, para os objetos investigados.

`NativeVisualDispatch.Participates` e `CompareLayer` modelam o filtro e a
ordenação. Empates de chave, máscaras exatas de cada tela e ordem de desenho
sob múltiplos passes ainda precisam de verificação.

## Animação da ativação

Funções `0x00450260` (construtor), `0x00450590` (update),
`0x004505D0` (draw) e `0x00422C00` (transformação).

- O contador `t` cresce em **16 por atualização**; após a 17ª atualização
  sai de 256 para 272 e desativa a animação (`t < 257`).
- `NativeActivationTimeline` expõe o contador em **updates**, sem converter
  para frames exibidos ou milissegundos.
- `NativeActivationFrame.FromCounter` produz três passes, nesta ordem:
  **sprite inferior, scratch preto gerado, sprite superior**.
- A escala vem de uma tabela nativa `cos` em ponto fixo
  `C[(t - 128) & 511]`; a função exige as **512 entradas fornecidas pelo
  chamador**, pois `Math.Cos` não comprova paridade bit a bit com a tabela x87
  do processo. Não há tabela inventada no repositório.
- Opacidade dos sprites `min(s >> 8, 255)`; do scratch `s >> 9`.
  Rotações `t/2` (sprite inferior) e `t` (superior).
- `ResourceIndex` usa o par `2*categoria+1` / `2*categoria`
  para categorias azul/amarela do `activate.txt`.

**Isso é um contrato de sequência e de parâmetros, não uma reprodução completa
do rasterizador.** A mistura de canais, a color key e o tamanho real do scratch
dependem de recursos, superfícies e captura ainda não integrados à raylib.
A branch `analise-visual` já contém um visualizador isolado para investigar
esses passes; não foi copiado para o `Joey.App` como se fosse validado.

## Explosão e relógios

`NativeExplosionTimeline` representa `0x00456730` (contador +8 por update)
e as janelas de desenho em `0x004514B0`:

| Pass | Janela de contador |
| --- | --- |
| Initial | `t < 0x80` |
| Flare | `0x1F < t < 0x90` |
| Fragments | `0x7F < t < 0xD0` |
| Desativação | no desenho quando `t > 0xCF` |

A função `AfterNativeDraw` é separada de `AdvanceNativeUpdate` porque a
evidência mostra que **draw também altera estado** para essa classe.
Não se afirmou duração de animação em segundos, FPS de 60 do nativo nem
contagem fixa de imagens exibidas.

## Primeira prévia em raylib (posterior à análise estática)

A aplicação agora carrega os sprites de ativação `kouka_ue/sita_b/y.yga`
do DAT original por `NativeYgaSprite` e desenha os três passes via
`DuelActivationScreen.cs` quando uma magia suportada é realmente ativada.
A política temporal `ActivationEffectPlayback` segue os 17 updates do
contador, mas usa **60 atualizações/s como escolha do cliente**, não
medição do EXE. As transformações usam tabela gerada com `Math.Cos`
e GPU blending da raylib; isto é uma **prévia**, não replay pixel-perfect.
[Instruções, limitações e pausa da trilha](activation-preview.md).

## Validação, limites e próximos trabalhos

`NativePresentationChecks` valida fórmulas e contadores sintéticos,
posicionamento espelhado, perfis e fases de explosão. A build e testes C# são
executados pela CI; a UI ainda requer comparação manual com Joey no Windows.

1. Confirmar cadência e ordem `update → draw` com instrumentação observacional
   em `0x0045D040`/`0x0045D170`; coletar valores `t` e timestamps.
2. Medir posições da mão ao longo do tempo e conferir interpolação e dimensões.
3. Validar o replay de ativação com a tabela de cosseno **extraída do
   processo**, comparando frames/alfa/transformações contra capturas nativas.
4. Só depois criar timeline em tempo real, sprites raylib e áudio/voz original
   e concluir a ligação por eventos tipados de domínio. `--fast` e
   `--mute-joey` continuam políticas de apresentação, nunca regras.

Os perfis de milissegundos existentes em `DuelLookAndFeel.cs` são ainda
**tempos do protótipo** e não devem ser anunciados como originais do Joey.
