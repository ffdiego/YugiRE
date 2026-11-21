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
