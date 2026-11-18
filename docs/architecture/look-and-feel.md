# Joey.LookAndFeel — política de apresentação

Assembly .NET 10 **sem referência a Joey.Core ou raylib**: controla somente a duração
e a política de reprodução de cues visuais ou falas. O Core resolve as regras de
cartas/batalha imediatamente em um estado determinístico; a apresentação não deve
alterar resultados, disparar compras ou introduzir delays em testes automatizados.

## Componentes

- `DuelPresentationCue`: `CardMove`, `FlipReveal`, `FlipEffect`, `Battle`,
  `LifePoints` e `JoeySpeech`. São **pontos de extensão**, não uma reprodução
  já comprovada dos tempos nativos.
- `DuelLookAndFeel`: classe base com métodos virtuais `BaseDuration(cue)`,
  `Duration(cue)` e propriedades de velocidade, animação e voz.
- `FastLookAndFeel`: 2x os tempos do preset clássico.
- `SilentLookAndFeel`: desativa cues de voz, mas mantém animação.
- `InstantLookAndFeel`: sem delays de animação nem de fala.
- `DuelPresentationClock`: relógio de frame não bloqueante com `Show`,
  `Advance`, `SetStyle` e `Clear`. Não chama `Sleep`, `Task.Delay` ou
  qualquer método de `Joey.Core`.

No `Joey.App` atual, os argumentos `--fast`, `--instant` e `--mute-joey`
selecionam o preset ao iniciar, e a janela mostra o nome. A duração do cue
controla apenas o realce do **texto de feedback**. A camada de áudio/vozes do
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
