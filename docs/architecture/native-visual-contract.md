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
do assembly de apresentação. O método `MoveCardAxis` aplica um passo nativo. O relógio
de apresentação o agenda a 30 passos/s em Classic e 60 em Fast. Instant
salta à posição final. Isso altera **só a apresentação**, não o número de turnos,
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

## Contadores do executável e efeito de `-speedy`

Análise estática em Ghidra com `-noanalysis -readOnly`, conferida com
`objdump` no EXE do SHA acima. O DWORD em `005EB618` começa em **30** na
seção `.data`. O parser em `00411CA2` grava **60** com `-speedy`.
A leitura em `00411101` passa o valor a `005456E0`, que calcula
`(1000 << 16) / frequência`. O laço chama `00545780` em `004112D4` e
essa rotina usa o intervalo acumulado e timestamps em milissegundos.
Isso estabelece a cadência **nominal do laço** de 30 ou 60 iterações/s.
O relatório anterior de xrefs não mostrou a leitura em `00411101`.

| Efeito | Contador estático | Tempo nominal a 30 / 60 updates/s |
| --- | --- | --- |
| Movimento da carta (`00418B60`) | De `0` até `0x100` em passos de `0x20`: 8 updates de movimento | 267 / 133 ms |
| Brilho de ativação (`00450590`) | `+0x10`, desativa no 17.º update | 567 / 283 ms |
| Aviso padrão de fase (`00480B40`, `00480C20`, `00480C60`) | Começa em `0`, soma `8`, encerra no draw em `0x100`: 32 updates. O índice de fase `3` soma `4` de `0x40` a `0x80` e leva 40 updates. | 1.067 / 533 ms. Índice `3`: 1.333 / 667 ms. |
| Aviso de troca de jogador (`00480BC0`, `00480C20`, `00480C60`) | Índice `6`, `+8` até `0x180`: 48 updates | 1.600 / 800 ms |
| Valor flutuante de LP (`0047C4F0`, `0047C560`) | Contador `0 → 0x100` em 32 updates. Depois, enquanto a flag em `+0x30` estiver livre, consome um passo por update de `100`, `10` ou `1`, conforme o valor restante. Um delta de `1.000` requer mais 10 updates. | Espera: 1.067 / 533 ms. Consumo de 1.000: mais 333 / 167 ms. |

As conversões assumem um update do objeto por iteração. Os valores não
incluem esperas por resposta, sobreposições, pausas impostas por flags,
nem a cadência efetiva de draw. A gravação feita com `-speedy`, a 30 fps,
mostra a ordem dos efeitos, mas amostra menos quadros que a cadência nominal
de update. Ela não mede o preset sem a flag. Ainda falta fechar a lógica de
seleção e movimento da espada, que não deve herdar os tempos de uma carta.

## Primeira prévia em raylib (posterior à análise estática)

A aplicação agora carrega os sprites de ativação `kouka_ue/sita_b/y.yga`
do DAT original por `NativeYgaSprite` e desenha os três passes via
`DuelActivationScreen.cs` quando uma magia suportada é realmente ativada.
A política temporal `ActivationEffectPlayback` segue os 17 updates do
contador com 30 atualizações/s em Classic e 60 em Fast. Essas são as
cadências nominais do laço recuperadas estaticamente. As transformações usam tabela gerada com `Math.Cos`
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

As durações dos demais cues em `DuelLookAndFeel.cs` continuam sendo
políticas do protótipo. A trilha de ataque ainda usa uma duração provisória.
