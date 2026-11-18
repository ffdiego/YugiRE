# Joey.LookAndFeel — política de apresentação

Assembly .NET 10 **sem referência a Joey.Core ou raylib**: controla somente a duração
e a política de reprodução de cues visuais ou falas. O Core resolve as regras de
cartas/batalha imediatamente em um estado determinístico; a apresentação não deve
alterar resultados, disparar compras ou introduzir delays em testes automatizados.

## Componentes

A consolidação da análise estática da branch `analise-visual` foi organizada
em [**contrato visual nativo**](native-visual-contract.md). As fórmulas de mão,
centros do campo, contadores de ativação/explosão e despacho de objetos
agora residem em `Joey.LookAndFeel` e não em `Joey.Core`.
O port modela **updates e parâmetros**, sem atribuir FPS ou milissegundos
nativos ainda não medidos.


- `DuelPresentationCue`: `CardMove`, `FlipReveal`, `FlipEffect`, `Battle`,
  `LifePoints` e `JoeySpeech`. São **pontos de extensão**, não uma reprodução
  já comprovada dos tempos nativos.
- `DuelLookAndFeel`: classe base com métodos virtuais `BaseDuration(cue)`,
  `Duration(cue)` e propriedades de velocidade, animação e voz.
- `FastLookAndFeel`: 2x os tempos do preset clássico.
- `SilentLookAndFeel`: desativa cues de voz, mas mantém animação.
- `InstantLookAndFeel`: sem delays de animação nem de fala.
- `HandPresentation` e `NativeDuelLayout`: mão, movimento, posições e
  retângulos sem dependências da engine ou da raylib.
- `NativeActivationTimeline` / `NativeActivationFrame`: contadores e três
  passes, consumindo tabela de cosseno x16.16 fornecida pelo chamador.
- `NativeExplosionTimeline`: update e draw separados para preservar
  as janelas de desenho da análise estática.
- `NativeVisualDispatch`: máscaras e ordenação dos objetos nativos.
- `DuelPresentationClock`: relógio de frame não bloqueante com `Show`,
  `Advance`, `SetStyle` e `Clear`. Não chama `Sleep`, `Task.Delay` ou
  qualquer método de `Joey.Core`.

No `Joey.App` atual, os argumentos `--fast`, `--instant` e `--mute-joey`
selecionam o preset ao iniciar, e a janela mostra o nome. A duração do cue
controla apenas o realce do **texto de feedback**. O perfil também altera
a suavização de movimento da mão: Classic = 1 passo nativo por render,
Fast = 2, Instant = posicionamento imediato. Esta política por frame ainda
não corresponde a um FPS nativo confirmado. A camada de áudio/vozes do
Joey ainda **não existe**, portanto `--mute-joey` é uma política preparada
para futuras falas, **não** a desativação de um player de áudio já integrado.
Não confundir o timing inicial do demo com valores medidos no executável.

Exemplo de futura extensão sem editar a engine:

```csharp
public sealed class MyQuickJoey : FastLookAndFeel
{
    public override bool PlayJoeyVoice => false;
    protected override TimeSpan BaseDuration(DuelPresentationCue cue) =>
        cue == DuelPresentationCue.FlipEffect
            ? TimeSpan.FromMilliseconds(120)
            : base.BaseDuration(cue);
}
```

## Prévia de ativação com sprites do DAT

A primeira integração desenhável está documentada em
[`activation-preview.md`](activation-preview.md). `NativeYgaSprite`
decodifica pixels BGRA dos YGAs originais com LZSS fornecido pelo chamador;
`ActivationEffectPlayback` executa os 17 updates lógicos do nativo, usando
uma frequência de atualização **escolhida pelo cliente**. O app raylib
desenha os dois sprites e o scratch intermediário. A fidelidade de mistura,
tabela de cosseno e temporização com o original **não foi demonstrada**.
A animação fica congelada nesta fatia e a prioridade volta às cartas.

## Separação pretendida

```text
Joey.Core (estado e regras)
        |
        |  futuros eventos tipados de domínio
        v
Joey.LookAndFeel (política e agenda de apresentação)
        |
        |  cues de animação / fala, sem alterar o Core
        v
Joey.App / raylib (desenha, anima, reproduz áudio, recebe input)
```

A ligação por **eventos tipados** ainda é planejada; no primeiro incremento o
`Joey.App` infere o cue de mensagens de texto para realçar o feedback. Não usar
esse mecanismo provisório como fonte de verdade em automação ou testes de
regras. Eventos futuros devem trazer contexto (`cardId`, origem, destino,
proprietário, fase) e ser gerados após uma transação do Core.

## Regra específica de Penguin Soldier

Em batalha, dano e determinação da destruição acontecem **antes** de resolver
FLIP. Um monstro marcado para destruição permanece na zona até o fim dessa
etapa, mas não pode voltar à mão por Penguin Soldier. Se ele sobreviver ou
for Flip Summoned, pode devolver a si mesmo. Esta é uma regra do **Core**,
não uma opção ou atraso de `Joey.LookAndFeel`.

Referências:
- https://www.yugioh-card.com/ygo_cms/ygo/all/uploads/Rulebook_v9_en.pdf
- https://ygorganization.com/learnrulingspart9
- https://ftp.yugipedia.com/wiki/Card_Rulings:Penguin_Soldier
