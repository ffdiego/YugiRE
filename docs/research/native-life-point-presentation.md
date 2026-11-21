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
