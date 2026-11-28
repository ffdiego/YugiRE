# scripts/regression — verificações opcionais

Execute os comandos da raiz do repositório, com as dependências e os dados locais de cada verificação.

### 14 — C01: taxonomia de efeitos pelo DAT local

- Ambiente: Windows (Git Bash) ou Fedora, Python 3 e .NET 10, DAT original local.
- Comando: `bash scripts/regression/14-card-effect-taxonomy.sh /caminho/para/data.dat`.
- Saída privada: `scratch/card-taxonomy.json` (IDs, fingerprints, famílias, rotas e status atuais; sem descrições).
- Pergunta: quais cartas dos packs são candidatas a lotes triviais e quais exigem revisão/continuações/respostas?
- Encerramento: conferir as 771 entradas por ID, máscara e categoria e validar o fingerprint com `docs/cards/pack-index.json`. O SQLite v2 contém 442 identidades. O DAT local fornece as descrições para a triagem.
- Estado: execução concluída em Fedora/.NET 10. Rotas validadas no lote local: 264 sem efeito aplicável, 433 fila/resposta, 72 revisão e duas candidatas triviais. A compra já consta como Done e a cura permanece Pending. Mantenha a saída `--taxonomy-input` no pipe privado do classificador. A classificação preserva os status do manifesto e a saída em `scratch/` contém somente metadados.

## A03/A04 — 22-actions-console-visual-smoke.sh (piloto validado no Windows)

Execute `bash scripts/regression/22-actions-console-visual-smoke.sh /caminho/para/data.dat` para a regressão local; consulte os comentários do script para as opções.

Roteiro 22 executado via PowerShell, headless e gráfico: 18 Applied e 106 linhas relevantes idênticas após fff5e6e. Novo roteiro `scripts/fixtures/duel-console-two-sides-smoke.txt` executou 29 Applied, sem espera >=5s; operador confirmou compras animadas de ambos lados. CI 45328386043. Manter script como regressão opcional. Não implica paridade visual nativa; A05/A07 pendentes.


## A09 — actions-a09-combat-smoke.py (smoke gráfico com DAT local)

- Requisitos: Python 3.10+, .NET 10, DAT privado e sessão gráfica. Não requer
  joey_pc.exe nem modifica saves. Rodar a partir da raiz:
  `python scripts/regression/actions-a09-combat-smoke.py --dat ../passion/data.dat`.
- Roteiro: `scripts/fixtures/duel-console-combat-smoke.txt`; direct lado 1,
  combate com alvo lado 0, decks não vazios, setup explícito por spawn.
  A hipótese de ID 1351 com ATK positivo é validada pelo resultado do Lab,
  não pela fixture sintética do CI.
- Fechar Classic e Instant depois de Script completed. Logs em
  `scratch/actions-combat/smoke-*/` (diretório único). Enviar os três .txt e
  registrar se a espada aparece antes do dano/remoção; não versionar o DAT.
- Critério: mesmo trace/state headless e visual nos dois perfis, fila livre,
  nenhuma espera >=5s, ordenação Started/Completed serial. A comparação
  não mede pixels nem paridade com o original. A captura nativa equivalente
  de A09 continua pendente; este roteiro não a substitui.

V20.2 — revisão de moeda (Windows/Fedora desktop): `dotnet run --project src/Joey.App -- /path/to/data.dat --review-coin=3,5,400,100,1`. F7 avança, F8 reinicia, F12 captura PNG/JSON. Verificar uma/três moedas e ambos os bits; recortes/halo e flags de reflexão no JSON. Reflexão/brilhos não implementados; não repetir coleta 16. Validação manual pendente, sem dados privados versionados.

V20.3: o review de moeda agora inclui brilho key5 e JSON schema 2 com o plano de reflexão. A reflexão continua omitida até resolver +94/+A0. Roteiro 24 já abrange esses offsets da vtable; não repetir a coleta 16.

### 41 — V17, formato da superfície da cena em Wine

O [procedimento para preparar Proton, Xvfb e a cópia do original](../../docs/research/joey-proton-validation.md) descreve a abertura do duelo, a captura e a identificação do processo.

Abra um duelo na sua cópia de teste com GDB e Wine/Proton disponíveis.
Confira o SHA-256 do executável, o PID da cópia que você iniciou e o
mapeamento do PE em `00400000`. O fingerprint é o da coleta 34.
O roteiro usa breakpoint de hardware, lê três entradas de `0044DD00`
e desanexa. Os campos de formato e flag são lidos na vtable conhecida.

```bash
timeout --signal=INT 15s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/41-wine-scene-surface.gdb > scratch/scene-surface-wine.log 2>&1
```

O piloto em Proton 9.0, Xvfb a 800×600 e llvmpipe retornou
`0x005dc6ec format=3 flag78=0` nas três amostras do duelo de
demonstração. Para conferir a negociação, inicie o jogo com
`WINEDEBUG=-all,+ddraw` e procure o ponteiro `ddraw` do roteiro
em `ddraw_surface1_GetSurfaceDesc` no `game.log` da mesma execução.
Confira o pedido em `CreateSurface` e as máscaras devolvidas.
Os breakpoints e o trace interferem no tempo de execução.
A coleta 34 em Windows continua sendo a referência para cadência e
para a passagem de composição na seleção.

### 42 — V11/V17, compositor do cemitério adversário em Proton

Use a cópia i386 do executável e o display privado do roteiro 41.
No duelo individual, deixe o adversário com pelo menos uma carta
no cemitério. Anexe ao processo de teste e clique na pilha do
cemitério adversário. O roteiro usa quatro breakpoints de hardware
e desanexa após a primeira passagem estável.

```bash
timeout --signal=INT 75s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/42-wine-selection-compositor.gdb > scratch/selection-compositor-wine.log 2>&1
```

O recorte registra vtables, formatos, origem da amostra, chave,
retorno do blit e a consulta do ramo por cor-chave RGB565. O piloto
observou a chave `0x07E0` em `004B2D29` com retorno 0 da chamada.
Para verificar a transparência na mesma cena, capture o canvas
antes e depois de abrir a lista e confronte os pixels do canto e
do interior. A coleta 34 continua disponível para o cenário Windows.

### 43 — V31, quadrilátero base da reação em Proton

Use o [prefixo e display privados do piloto](../../docs/research/joey-proton-validation.md).
Confirme o fingerprint, o PID da cópia e o mapeamento do PE. Anexe no
menu e aguarde a demonstração automática. O roteiro usa dois
breakpoints de hardware e desanexa quando o objeto observado termina.

```bash
timeout --signal=INT 115s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/43-wine-reaction-geometry.gdb > scratch/reaction-geometry-wine.log 2>&1
```

Compare os quatro vértices de origem e os quatro de destino com
`bustUpReactionGeometry` no JSON do inspetor DAT. O piloto encontrou
uma correspondência única com `reaction07.txt`, formato 3 em ambas as
superfícies e 63 chamadas base com contador de 8 a 504, em passos de
8. O log deve terminar com `reaction-end` e a confirmação de
desanexação. O manifesto encontrado e o contador final podem variar
conforme a reação. O recorte mede argumentos e contadores do processo.

### 44 — V31, camadas da reação em Proton

Use o mesmo cenário e a mesma identificação de PID do roteiro 43,
em uma nova execução da demonstração. O roteiro seleciona uma reação
cujo primeiro cabeçalho é 1 no contador 8. Ele registra os dois
despachos, suas variantes, as posições nos slots `+0C` e `+4C` e o
término. Usa quatro breakpoints de hardware.

```bash
timeout --signal=INT 115s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/44-wine-reaction-overlays.gdb > scratch/reaction-overlays-wine.log 2>&1
```

O piloto capturou as duas variantes da boca e dos olhos de
`reaction07.txt` no slot `+0C`. Confira que o primeiro despacho
precede o segundo no mesmo contador e que cada linha `layer` tem
um despacho correspondente. Use o registro `selected` e a origem
do manifesto para conferir a posição final. As omissões dependem
dos bits de estado. Confira o encerramento em `reaction-end` e a
desanexação no log.

### 45 — V31, reação com duas falas em Proton

Use o prefixo e o PID confirmados no roteiro 43. Anexe no menu e
deixe a demonstração progredir. A reação com dois IDs apareceu
depois de várias reações com um só ID no piloto. O roteiro usa
quatro breakpoints de hardware e desanexa ao término do primeiro
objeto observado com ID secundário diferente de zero.

```bash
timeout --signal=INT 190s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/45-wine-reaction-two-voices.gdb > scratch/reaction-two-voices-wine.log 2>&1
```

Confira que `first` solicita o primário no contador 8 e que
`second` ocorre no mesmo contador do primeiro `primary-status`
diferente de zero. O novo primário, o pedido e o secundário
guardado em `expected` devem coincidir. O secundário deve estar
zerado e o objeto ainda ativo nesse ponto. Confira uma promoção,
objeto inativo em `reaction-end` e a confirmação de desanexação.
Se o prazo expirar após registrar só reações sem segundo ID, a
transição ficou sem amostra nesse duelo.

### 46 — V17, despacho da cena em Proton

Abra duas sessões de teste seguindo o
[guia de Proton](../../docs/research/joey-proton-validation.md), uma
no modo padrão e outra com `-speedy`. Para cada sessão, confirme
o PID e anexe no menu antes de começar a demonstração. Use GDB
com Python e um nome de log diferente por sessão.

```bash
preset=standard  # Use speedy para a segunda sessão.
timeout --signal=INT 105s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/46-wine-scene-cadence.gdb > "scratch/scene-cadence-$preset.log" 2>&1
```

O roteiro usa quatro breakpoints de hardware. Depois de 360 entradas
completas na região de despacho, ele escreve uma linha `scene-cadence`
com JSON e desanexa. Compare o valor `nominal` com o preset e confira
`dispatches` por entrada. Uma lista vazia registra que a cena passou
pela convergência sem chamar os dois dispatchers observados. Nos pares,
confira Update→Draw, ramo e máscara iguais. Use timestamps de entradas
consecutivas no mesmo thread e com a mesma sequência de despacho para
comparar os intervalos. O relógio é monotônico e os valores estão em
nanossegundos. O log fica em `scratch/`.

### 47 — V17, entrega do buffer em Proton

Use as duas sessões indicadas no roteiro 46. Deixe a demonstração
chegar ao tabuleiro e aguarde as animações de abertura passarem
antes de anexar. Use GDB com Python e arquivos separados para os
dois presets.

```bash
preset=standard  # Use speedy para a segunda sessão.
timeout --signal=INT 45s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/47-wine-scene-present.gdb > "scratch/scene-present-$preset.log" 2>&1
```

O roteiro usa quatro breakpoints de hardware e grava uma linha
`scene-present` com JSON para 120 intervalos completos entre
entradas da cena. No recorte regular do piloto, cada entrada
teve uma chamada do laço e uma chamada ao `BltFast` do DirectDraw,
em ordem. Confira o módulo `ddraw.dll`, `xy=[0,0]`, a origem
`[0,0,800,600]`, flags `0x10` e retorno zero. Compare as medianas
entre `callNs` consecutivos no mesmo thread. O relógio monotônico
do host informa nanossegundos sob GDB.

### 48 — V17, notificações de desenho em Xvfb

Use o display Xvfb isolado do [guia de Proton](../../docs/research/joey-proton-validation.md),
com `XAUTHORITY` apontando para a autoridade da sessão. Rode uma
vez no modo padrão e outra com `-speedy`, com a demonstração já
no tabuleiro e sem debugger anexado. O observador usa Python 3.8+
e as bibliotecas `libX11.so.6` e `libXdamage.so.1`.

```bash
preset=standard  # Use speedy para a segunda sessão.
python scripts/regression/48-x11-drawable-cadence.py --display "$DISPLAY" --seconds 10 \
  > "scratch/x11-damage-$preset.json"
```

Confira os retângulos `area` e `geometry` antes de comparar
intervalos entre `serverMs` consecutivos, com wrap de 32 bits.
Em um display dedicado, o piloto encontrou apenas notificações
de 800×600 durante a amostra. `receivedNs` é o instante local do
recebimento relativo ao início da coleta. O modo de retângulos
brutos pode emitir mais de um evento por operação e também pode
cobrir pixels que conservaram o valor anterior.
[Protocolo DAMAGE da X.Org](https://www.x.org/releases/current/doc/damageproto/damageproto.txt).

### 49 e 50 — V05, atributos no campo em Proton

No prefixo e display isolados do
[guia de Proton](../../docs/research/joey-proton-validation.md),
confirme o fingerprint e o PID. Anexe o 49 no menu ou no
tabuleiro. Ele agrupa entradas com os mesmos argumentos a
partir da primeira entrada de `004194D0` e se desanexa ao
completar o recorte de 55 segundos. O 50 é uma segunda coleta
para um ATK com intensidade 256 e registra o formato, o
ponto de amostragem e a chave da superfície do atlas.

```bash
timeout --signal=INT 135s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/49-wine-field-attributes.gdb > scratch/field-attributes.log 2>&1

timeout --signal=INT 20s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/50-wine-field-atlas-key.gdb > scratch/field-atlas-key.log 2>&1
```

Confira `passes` por descritor e perspectiva. A ordem é
DEF, separador e ATK. Defensores adversários ocultos no
piloto tiveram lista vazia. Compare o atlas com a
variação em relação ao valor de catálogo. O campo
`right` é a âncora direita do helper, em pixels lógicos.
Para a prova de pixels, use uma captura com um valor em
intensidade 256 e confronte os pixels do glifo, fora da
cor-chave, com o DAT em RGB565.

### 51 — V31, reação com áudio desativado em Proton

No [guia de Proton](../../docs/research/joey-proton-validation.md),
inicie a sessão com `joey_args=(-nosound)`. Confirme o PID, anexe
no menu e deixe a demonstração progredir.

```bash
timeout --signal=INT 197s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/51-wine-reaction-nosound.gdb > scratch/reaction-nosound.log 2>&1
```

Confira `audio-enabled=0`, o primeiro pedido no contador 8
e a desativação no contador 256. O roteiro separa a primeira
reação de uma fala da primeira com duas e desanexa após
amostrar as duas. Para a segunda, confira o retorno do
predicado e o contador da troca. Se o prazo terminar com
apenas `single-end`, a amostra cobre só o caso de uma fala.

### 52 — V05, composição dos atributos em Proton

Confirme o fingerprint e o PID no
[prefixo isolado](../../docs/research/joey-proton-validation.md).
Anexe no menu e aguarde a demonstração. O roteiro se desanexa
após capturar DEF, separador e ATK em intensidade 160.

```bash
timeout --signal=INT 95s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/52-wine-field-attribute-blend.gdb > scratch/field-attribute-blend.log 2>&1
```

Os códigos de `glyph pass` 1, 2 e 4 identificam DEF,
separador e ATK. Confira formato 3 nas duas superfícies,
chave `0x8d85`, `src-weight=160` e `dst-weight=0`.
Na captura, compare os pixels do glifo após escalar os
canais RGB565 por 10/15. Nas posições da chave, compare
com uma captura do mesmo fundo sem números.

### 53 — V05, transição dos atributos em Proton

Use o [prefixo isolado](../../docs/research/joey-proton-validation.md)
e o PID confirmado do original. Anexe ao duelo de demonstração
ou provoque um efeito de ATK/DEF sobre um monstro que permaneça no
campo. O roteiro usa GDB com Python e três breakpoints de hardware.

```bash
timeout --signal=INT 213s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/53-wine-field-attribute-tween.gdb > scratch/field-attribute-tween.log 2>&1
```

A linha `attribute-change` identifica o primeiro objeto com
valor exibido diferente do alvo. A linha `attribute-tween`
lista seus Draws até os dois números chegarem ao alvo ou até
o limite de 128 amostras. Confira a continuidade dos valores,
a fórmula e o atlas escolhido em relação ao valor de catálogo.
Confira o contador do laço separadamente. Um prazo sem a
linha de conclusão deixa a transição sem amostra completa.

### 54 — V04, despacho da espada em Proton

No [prefixo isolado](../../docs/research/joey-proton-validation.md),
confirme o fingerprint e o PID. Anexe antes de um ataque da
demonstração. O roteiro usa GDB com Python e quatro breakpoints
de hardware. Desanexa ao ver a lâmina parcial no contador 496.

```bash
timeout --signal=INT 125s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/54-wine-attack-quad-dispatch.gdb > scratch/attack-quad-dispatch.log 2>&1
```

Confira o JSON em `attack-quad-dispatch`. `bankMatch` indica que a
origem corresponde ao índice registrado do banco. Para cada par,
confira o pedido de impacto seguido da lâmina parcial e a escala
zero no primeiro pedido de impacto. Os compositores base e por
intensidade devem aparecer dos dois lados da transição de 384
para 400. Use `targetSlot=-1` para identificar ataque direto.

Quando o compositor recebe quatro vértices, o roteiro inclui
`sourceQuad` e `destQuad` na ordem da chamada, com endpoints
inclusivos. `coefficients` guarda os dois valores 16.16 para
cada ângulo observado, indexado por `angle & 511`. Use as
dimensões do DAT e esses valores na chamada de
`NativeRotatedQuad.Project` para comparar os quatro pontos.
O pedido de escala zero não chega ao compositor.

Para amostrar só ataques diretos, acrescente
`-ex 'set $attack_direct = 1'` antes de `-x`. O filtro
ignora os pedidos com alvo em um slot de monstro.
Em um ataque direto do lado próprio, os dois últimos
quadriláteros do impacto chegaram com Y negativo, antes
do clipping interno.

### 59 — V04/V17, recorte do impacto em Proton

Use o [prefixo isolado](../../docs/research/joey-proton-validation.md)
e o PID confirmado do original. Anexe antes da demonstração ou
provoque um ataque direto. O roteiro procura uma chamada em
intensidade 32 de origem 10 para destino 3 com vértices além
da borda vertical. Use GDB com Python.

```bash
timeout --signal=INT 183s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/59-wine-attack-impact-clip.gdb > scratch/attack-impact-clip.log 2>&1
```

Confira `attack-impact-clip` e a desanexação. Para cada faixa,
compare `y` com o recorte e `offset` com `y*stride+x*2`.
`changed` conta as posições cujo RGB565 mudou entre a entrada
e a saída da faixa. O piloto do recorte inferior chegou a Y=599,
com 138 faixas e 6.022 alterações. A amostra pode variar conforme
a borda do impacto.

### 60 — V17, pixels do compositor 10 para 3 em Proton

Confirme o fingerprint e o PID no
[prefixo isolado](../../docs/research/joey-proton-validation.md).
Anexe antes de um ataque. Use GDB com Python e .NET 10.
A sonda procura o impacto em intensidade 224 e se desanexa
ao reunir 144 amostras antes e depois do helper por pixel.

```bash
timeout --signal=INT 115s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/60-wine-packed-alpha-pixels.gdb > scratch/attack-packed-pixels.log 2>&1
dotnet run --file scripts/regression/60-compare-packed-alpha-pixels.cs -- scratch/attack-packed-pixels.log
```

Confira quantas amostras foram comparadas, quantas passaram
pela cor-chave e quantas mudaram de valor. O comparador informa
a primeira divergência em RGB565. O piloto cobriu 144 pontos,
com 71 amostras de cor-chave e 57 mudanças. Os pixels lidos
permanecem no log privado.

O padrão é 224. Para selecionar outro passo do impacto,
acrescente `-ex 'set $packed_intensity = 32'` antes de
`-x`, com um nome de log específico. Os passos possíveis
vão de 32 a 224 em múltiplos de 32.

### 61 — V17, pixels do compositor base 10 para 3

Com os mesmos requisitos do roteiro 60, anexe antes de
um ataque. O roteiro seleciona intensidade 256 e coleta
144 amostras no helper base. O comparador do roteiro 60
reconhece o cabeçalho `attack-packed-base-pixels`.

```bash
timeout --signal=INT 105s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/61-wine-packed-alpha-base-pixels.gdb > scratch/attack-packed-base-pixels.log 2>&1
dotnet run --file scripts/regression/60-compare-packed-alpha-pixels.cs -- scratch/attack-packed-base-pixels.log
```

### 62 — V17, seleção de texels no impacto em Proton

No mesmo prefixo isolado do roteiro 60, anexe antes de um ataque.
A sonda lê os quatro vértices de origem e destino e amostra o
endereço do texel antes de o compositor ler sua cor. O comparador
usa as coordenadas e a flag de divisor da chamada original.

```bash
timeout --signal=INT 115s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/62-wine-attack-texels.gdb > scratch/attack-texels.log 2>&1
dotnet run --file scripts/regression/62-compare-attack-texels.cs -- scratch/attack-texels.log
```

O padrão é intensidade 224. Para repetir em outra escala, passe
`-ex 'set $packed_intensity = 32'` antes de `-x`. Confira as
144 amostras e a mensagem de desanexação no log privado.
Para medir o compositor base, use `-ex 'set $packed_intensity = 256'`
e salve a captura em outro log. O mesmo comparador lê os dois caminhos.

Para medir o recorte inferior a 800×600, passe
`-ex 'set $packed_bottom_clip = 1'`. Esse modo seleciona
intensidade 32 e coleta as sete últimas linhas visíveis.
No piloto em duelo individual com `-speedy`, encerrar o turno
com campo vazio permitiu o ataque direto adversário. Confira os
144 pares U/V e a presença de amostras em Y=599.

### 63 — V17, seleção de texels em quadrilátero inclinado

Anexe antes do voo da espada na demonstração, com os mesmos
requisitos do roteiro 62. A sonda entra apenas em chamadas
RGB565→RGB565 com um quadrilátero inclinado. O comparador
verifica cada endereço de amostragem em coordenadas de texel,
a preservação do destino na cor-chave e a cópia RGB565 nos
demais pontos.

```bash
timeout --signal=INT 130s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/63-wine-rgb565-rotated-texels.gdb > scratch/rotated-texels.log 2>&1
dotnet run --file scripts/regression/62-compare-attack-texels.cs -- scratch/rotated-texels.log
```

Confira as 144 amostras e a mensagem de desanexação. Uma passagem
curta pode produzir menos amostras. Nesse caso, a sonda descarta
essa passagem e aguarda a próxima.

O padrão 256 mede o compositor base. Para medir o compositor
de intensidade reduzida, acrescente `-ex 'set $rotated_intensity = 224'`
antes de `-x` e use outro log. Os passos disponíveis vão de
32 a 224 em múltiplos de 32.

### 64 — V16, avanço da cena de resultado

Anexe à cópia de teste durante um duelo em uma partida
melhor de três. No cenário observado, o primeiro duelo
terminou por LP zero. Na tela de resultado, recuse salvar
o replay e confirme o placar intermediário.

```bash
timeout --signal=INT 120s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/64-wine-result-exit-gate.gdb > scratch/result-exit-gate.log 2>&1
```

O registro inclui fase, modo, estado do contador e a
desativação do objeto. Para o caso acima, confira 16
entradas de Draw com `running=true` e uma desativação
antes de a cena sair. O controle em duelo individual
perdido mantém o contador de saída desabilitado.
A comparação no Windows permanece no roteiro 34.

### 65 — V26, associação das zonas de exclusão

No prefixo isolado, anexe antes de entrar em um duelo ou
de a demonstração começar. O roteiro desanexa após ler
a associação da zona `0x0F` para os dois lados.

```bash
timeout --signal=INT 90s gdb -nx -q -batch -p "$joey_test_pid" \
  -x scripts/regression/65-wine-exclusion-zone-binding.gdb > scratch/exclusion-zone-binding.log 2>&1
```

Compare os offsets `+3A0h` e `+800h` e o destino do slot
`+18h` para ambos. O roteiro cobre a ligação na abertura do
duelo. O cenário de banimento do roteiro 34 cobre o disparo.
