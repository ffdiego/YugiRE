# LP: separação da regra e do contador visual

Fontes: SQLite global privado `joey-knowledge.sqlite` (SHA256 do
`joey_pc.exe` `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`);
decompilações `0047C310`, `0047C4F0`, `0047C4D0`,
`0047C540`, `0047C550`, `0047C560` e `00573FB0`.
O Draw `0047C660` aparece como entrada transitória no
`retry-039-0047C660.txt` do ZIP de Ghidra. Não se precisou
solicitar nova análise.

## Fatos estáticos recuperados

- Construtor `0047C310` inicializa o valor visível `obj+0x24`
  em **8000**, o delta `obj+0x28` em 0, o contador
  `obj+0x34` em **256** e o contador de brilho `obj+0x3C` em 0.
- Preparação de mudança `0047C4F0`: escreve o delta assinado em
  `obj+0x28`, reinicia o contador `obj+0x34` em **0**,
  guarda outros dois parâmetros e ativa um som conforme o sinal.
  Há uma porta `obj+0x30` que pode suspender a contagem; ainda
  não reproduzida no cliente.
- Update `0047C560` acrescenta **8 por Update** ao contador
  até `256`. Portanto cada variação leva primeiro **32 Updates**
  de preparação, sem alterar o número visível.
- Após essa rampa e se não há bloqueio, consome **100**, **10**
  ou **1** unidade por Update, conforme o valor absoluto do delta
  restante, aplicando seu sinal ao valor visível e reduzindo o
  delta pendente. A ordem é `100` quando `abs >= 100`,
  `10` quando `10 <= abs < 100`, e `1` quando `0 < abs < 10`.
- O valor mostrado é limitado a `[0, 0xFFFF]`.
  A função também atualiza uma fase de 7 bits em `obj+0x38`
  e um contador de 31 passos em `obj+0x3C` quando o número muda.
  `00573FB0` parece atualizar outra contagem de LP por jogador;
  não assumimos que substituir isso no cliente seja necessário.

## Integração do YugiRE

`Joey.Core.DemoDuel.Life` permanece como **estado lógico único**.
Dano, cura, morte e condições de vitória não aguardam
animações, efeitos nem passos da HUD.

`Joey.LookAndFeel.NativeLifePointPresentation` mantém
`Displayed`, `PendingDelta`, `RampCounter`, `FlashCounter`
e `PulseCounter` por jogador. `Joey.App` observa o valor
lógico a cada frame e executa o relógio de apresentação,
sem acrescentar o LP a `MotionBusy`.

Se um segundo evento chegar durante a animação, a apresentação
retoma a partir do número já exibido e substitui seu objetivo
pelo valor lógico mais recente. **Essa política de retarget é
do cliente**, não uma reprodução verificada das filas nativas.
O perfil Instant pula direto ao novo valor sem esperar.

O HUD atual ainda desenha **texto simples** nas posições
`(74,9)` e `(74,557)`. Continua pendente integrar:
`j\duel\life\lifepoint.txt`, `j\duel\font\number.txt`,
dígitos, barras, efeitos de dano, flashes, textura, blend, som
e comparação de frames com o jogo original.

A função Draw `0047C660` usa recursos, fontes e layout mais
complexos; o pseudocódigo transitório contém registradores e
argumentos ambíguos. O inventário visual já contava essa família
como **Partial**, portanto **não aumentamos a badge** só por
portar seu contador.

## Testes

`LookAndFeelChecks` cobre: delay de 32 Updates; dano -250
em 100,100,10,10,10,10,10; cura +205 em 100,100,1,1,1,1,1;
retarget durante animação; clamp visual a 65535; Instant; e
sem interferência nas regras Core.


## Integração parcial de atlas e barras do DAT (21/11/2026)

O Draw transitório `0047C660` no ZIP visual contém a expressão
`iStack_38 = min(obj+0x24, 8000) * largura / 8000`. A largura e a
posição da barra chegam de outras estruturas e **não são recuperáveis
com segurança** apenas desse pseudocódigo (registradores reconstruídos
ambíguos). O cliente usa `NativeLifeHud.FilledBarPixels` com truncamento
inteiro, saturando a barra em 8.000 LP, mesmo que o contador numérico
possa alcançar 65.535.

O índice do `data.dat` contém os seguintes recursos originais:

- `j\duel\life\lp_jibu1.bmp` e `lp_jibu2.bmp` (painel local).
- `j\duel\life\lp_aite1.bmp` e `lp_aite2.bmp` (painel adversário).
- `j\duel\font\duel_fontm_y.bmp` e `number.txt`.
- `j\duel\font\damage00.bmp` e `damage01.bmp`,
  `j\duel\life\bomb_*.bmp` e `lp_explos.bmp` estão catalogados,
  mas **não** foram integrados nesta fatia.

O `Joey.App` agora renderiza as imagens 1/2 de cada lado como
base/preenchimento, **composição provisória**. A barra tem 137 × 14 px,
ancorada à coluna de detalhes já existente (não se trata de coordenadas
nativas confirmadas). A segunda imagem é recortada ao comprimento
derivado de `NativeLifeHud`; o LP e a animação continuaram isolados no
`Joey.LookAndFeel`.

O helper numérico `004803C0` seleciona os algarismos por
`(valor % 10) * (larguraAtlas / numeroDeColunas)`, desenhando da direita
para a esquerda; `00480200` consulta a quantidade de glifos carregada
no manifesto. Como o conteúdo real do `number.txt` não está neste
ambiente, o cliente tenta extrair esse número **do DAT na execução**,
aceitando atlas com 10 a 12 glifos. Se não identificar a descrição,
continua com o texto funcional anterior. Isto evita fixar um número
inventado de colunas. O arquivo `duel_fontm_y.bmp` foi escolhido como
primeira variante, mas sua correspondência à escolha dinâmica de
`0047C660` ainda **não foi confirmada**; a renderização usa posições
aproximadas, sem considerar o kernel de rasterização nativo.

A função `DatGraphics.Validate` passa a decodificar os cinco BMPs
utilizados pelo HUD. Falhas ou divergências de layout do atlas
precisarão ser verificadas com o DAT original e os quadros
capturados do jogo. O conteúdo do DAT e o ZIP SQLite não são
incorporados ao repositório.

### Próxima validação visual, sem urgência para o desenvolvimento

Comparar, para cada lado, o número e a barra com 8.000, 4.000 e
0 LP, observando se `lp_*1/2` são realmente base/preenchimento,
a seleção de cor da fonte, os recortes verdes e a posição. Depois
investigar as janelas de `0047C660` que chamam
`004806F0` para os dígitos flutuantes de dano/cura e as imagens
`bomb_*.bmp`.

O estado continua **Partial**, e a cobertura de funções Draw
permanece **13/31**: esta fatia melhora uma função já contada.

## Revisão dos sprites de LP após comparação do original (22/11/2026)

A imagem pareada mostrou o número permanente do LP significativamente maior que a versão RE, mesmo após o recorte 1:1 do atlas. A fonte original é um **atlas de dígitos BMP**, não uma fonte alfabética do sistema. O cliente agora tenta usar `duel_fontl.bmp` como fonte principal, consulta a quantidade de colunas do `number.txt` real e só volta a `duel_fontm_y.bmp` quando o manifesto não identifica o atlas grande.

Os números do LP são desenhados com recorte correto do atlas, ampliados para 32 px de altura, alinhados à direita em X=195, com top Y=3 e bottom Y=536. Esses valores são **estimativas da captura**, não prova do código de `0047C660`; não promovemos cobertura visual com essa mudança.

O texto flutuante de perda/cura de LP já passa por `damage00.bmp` / `damage01.bmp` e pelo sampler `NativeFloatingLifeDigits`, reconstruído a partir de `004806F0`. Portanto ele também é composto de sprites; a animação usa cossenos aproximados e posições iniciais provisórias. **Não** converter esse efeito para GDI/Raylib Text.

O manifesto `j\duel\font\number.txt` no DAT real é a próxima evidência para conferir quais colunas/variante foram selecionadas e se o atlas grande corresponde ao LP permanente em todos os estados.


## Diagnóstico reproduzível de `number.txt` (22/11/2026)

A extração previamente enviada ao chat contém `selection-manifests.json`, mas **não** o `number.txt` completo. O arquivo completo `scratch/visual-review/dat-manifests.json` existe na instalação Windows do usuário após `--inspect-visual-dat`; seu conteúdo numérico ainda não foi verificado aqui. Não apresentar atlas, número de colunas ou tamanho de glifo como confirmados antes de executar o diagnóstico sobre essa fonte.

```powershell
git pull
python scripts/inspect_native_life_dat.py
```

O script lê o relatório completo já criado em `scratch/visual-review/dat-manifests.json` e gera `scratch/visual-review/life-font-evidence.json`, contendo `number.txt` original, argumentos de cada linha, dimensões dos BMPs relevantes e a interpretação **atual do cliente** sobre as colunas e o atlas selecionado. Nenhum pixel, hash de imagem nem BMP é exportado.

Os quatro alvos são `duel_fontl.bmp`, `duel_fontm_y.bmp`, `damage00.bmp` e `damage01.bmp`. `0047C310` carrega `j/duel/font/number.txt`, `004803C0` indexa o atlas por dígito e `004806F0` gera o movimento dos números flutuantes. `NativeLifeHud.TryFontColumns` assume que o segundo token contém de 10 a 12 colunas; o script mostra quando essa heurística falha. Altura atual de 32px e âncoras do LP ainda são estimativas de captura e não devem ser declaradas como parity.

**Próxima decisão:** usar o JSON resultante para conferir qual atlas o executável seleciona por condição, número de glifos e escala, e só depois substituir as constantes provisórias da apresentação do LP. Se o script indicar ausência do atlas ou largura incompatível, registrar falha sem retornar silenciosamente à fonte da raylib.


## Atlas comprovados pelo DAT e nitidez das cartas (22/11/2026)

O relatório `life-font-evidence.json` do usuário corresponde ao `j/duel/font/number.txt` real. `duel_fontl.bmp` contém 10 glifos em 80x12, ou seja, cada glifo mede 8x12; `duel_fontm_y.bmp` tem 10 glifos em 70x10 (7x10 por glifo); `damage00.bmp` e `damage01.bmp` têm 12 glifos em 192x40 (16x40 por glifo). As alturas finais dos dígitos permanentes (32px) e sua posição ainda são estimativas visuais.

A renderização de dano/cura não deve comprimir arbitrariamente os sprites de 40px para 32px. O cliente agora usa sua dimensão de origem e conserva a animação de `NativeFloatingLifeDigits`. A posição e curva nativas exatas continuam pendentes.

As capturas pareadas mostram mais blocagem de pixels no Morphing Jar nativo do que no campo do RE, mas também há perda de nitidez em parte do fundo do RE. `DatGraphics.Texture` já utiliza filtro Point; foi fixado explicitamente `TextureFilter.Point` para o render target 800x600 e a geometria dos BMPs das cartas foi arredondada para tamanhos inteiros: por exemplo, 200x290 reduzido ao slot de campo resulta em 50x72, e não 49,655x72. Isso não prova equivalência ao rasterizador nativo: para validar, comparar F12 (canvas 800x600) com o jogo original na mesma resolução, sem passar por escala do Paint.

O deslocamento horizontal restante da carta do detalhe foi informado como aproximadamente 2px pelo usuário. Não ajustamos novamente o `PreviewBounds.X` sem determinar a direção no par de capturas alinhado.

## Apresentação integer-scale dos LP (22/11/2026)

O DAT original enviado pelo usuário contém `duel_fontl.bmp` com dez glifos
de **8×12 px** (bitmap de 80×12) e `duel_fontm_y.bmp` com dez glifos de
7×10 (bitmap de 70×10). A captura do Joey original mostra algarismos
grandes, blocados e aproximadamente três vezes maiores que a célula
`duel_fontl`; o antigo desenho RE ampliava a célula de 8×12 para uma
retícula fracionária de 21,333×32, que pode borrar ou produzir largura
não uniforme sob `TextureFilter.Point`.

`NativeLifeHud` agora concentra `DisplayGlyphScale=3`, e o cliente
desenha `duel_fontl` em **24×36 px por glifo**, sem alteração de textura
nem substituição por font TTF. A origem Y foi deslocada para manter
aproximadamente as mesmas bordas inferiores das duas regiões de LP
até que uma captura pareada de mesmo estado valide o alinhamento.
O atlas alternativo `duel_fontm_y` permanece compatível, mas seu
tamanho resultante também respeita o multiplicador de inteiros.

**Nível de confiança:** o tamanho da célula de origem é comprovado
pelo manifesto e pelas dimensões do DAT. O fator 3, X/Y e a escolha
da variante em momentos de flash são **estimativas visuais**, ainda
sem prova estática do Draw `0047C660`. Não considerar este passo
a validação da família de LP. Os `damage00/01` não foram alterados.

## Ordem dos efeitos: ativação, ataque e LP (22/11/2026)

Observações de execução fornecidas pelo usuário: dano/cura de uma magia só
começam **após** sua ativação; em combate, a espada sai do atacante e
atinge o alvo antes de revelar monstros face-down; o dano começa depois
do movimento de ataque. Os números flutuantes aparecem no **campo** entre
fileiras de monstros e magias/armadilhas, não no painel lateral.

O cliente passa a separar `CardMotionBusy` (voo, animação de carta,
ativação e espada) e `LifePresentationBusy` (valor numérico exibido
diferente do Core ou animação/rampa ainda ativa). O `MotionBusy` global
bloqueia novas jogadas enquanto qualquer uma das duas famílias estiver
ativa. `AdvanceLifePresentation` não retargeta nem avança LP enquanto
`CardMotionBusy`, mantendo `DemoDuel.Life` como valor lógico imediato.
Somente depois inicia a rampa nativa de +8 por update até 256 e a
contagem de +/-100/10/1. Isto preserva a ordenação mesmo se o Core
resolver dano já no clique da magia/ataque.

O efeito de ataque usa `NativeAttackFrame.ImpactStartUpdate=16`
(`t=0x100`, metade da trajetória de 32 updates). Quando o alvo estava
face-down, uma cópia visual do verso é capturada **antes** do Core
resolver o combate e coberta no campo até o contato; inclusive se o Core
já retirou o monstro do slot. O flip visual no contato **ainda não**
interpola quadros de giro da carta e o caminho de resposta/chain pode
exigir sequência mais fina; esta é uma barreira de revelação, não
afirmação de paridade nativa.

O desenho flutuante `damage00/01.bmp` deixa de usar X=209 e Y=45/536
(painel). A âncora passa a ser obtida da geometria do campo:
(525,406) jogador e (525,194) CPU, entre as duas fileiras. Essas
coordenadas são **provisórias** e derivadas dos centros atuais, não
extraídas de `0045C140`. Os recursos originais 16x40 e as curvas
anteriores permanecem, sem migrar para fonte do sistema.

A implementação de LP permanece **Partial**, sem alteração de badge.
