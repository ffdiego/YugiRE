# Joey.LookAndFeel — política de apresentação

Assembly .NET 10 **sem referência a Joey.Core ou raylib**: controla somente a duração
e a política de reprodução de cues visuais ou falas. O Core resolve as regras de
cartas/batalha imediatamente em um estado determinístico; a apresentação não deve
alterar resultados, disparar compras ou introduzir delays em testes automatizados.

## Resultado visual como contrato

O objetivo do `Joey.LookAndFeel` é a **fidelidade de pixels e de tempo** do `joey_pc.exe` em seu canvas lógico de **800×600** — não equivalência de código, funções ou algoritmo de rasterização. Implementações em C#/raylib podem divergir internamente, desde que obtenham as **mesmas posições, dimensões, pivôs, orientações, escalas, recortes, ordem de composição, sprites e valores por update/frame** observáveis. Para a alpha, começar por cartas, ATK/DEF e ações visíveis; **a meta continua abrangendo todos os elementos visuais**.

Contadores nativos, cadência nominal (Classic/Speedy), tempos efetivos e espera por decisões são medidas diferentes. Manter velocidade Classic como alvo do executável quando conhecida, sem converter contadores em milissegundos arbitrários; `Fast` e `Instant` são perfis opcionais deliberadamente não idênticos. Registrar os dados como confirmado por Ghidra, observado no vídeo/original, derivado ou provisório. Testar estados e quadros equivalentes e comparar resultados em pixels/updates. [Contrato completo de fidelidade](visual-fidelity-goal.md).

## Recursos visuais: DAT como fonte de verdade

Para o modo fiel, todos os sprites e variantes **existentes no original** devem vir do `data.dat` local em tempo de execução pelo leitor de arquivos do projeto. Carregar sob demanda e manter em cache texturas/frames decodificados em memória ou GPU é permitido; `sempre ler do DAT` significa **não depender de placeholders ou arquivos externos extraídos/substitutos como fonte visual padrão**, e não executar leitura física repetida em cada desenho.

A apresentação deve selecionar **a variante correta em cada estado e update**, mantendo detalhes aparentemente pequenos: imagens de entrada/impacto/saída, cortes da espada, sobreposição atrás da carta, sprites parcialmente ocultos, opacidade e ordem de desenho. Para ataque, o DAT já contém `j\\duel\\effect\\atk_icon.bmp` e `atk_icon2.bmp`; o vídeo sugere a troca ao atingir a carta, mas o momento e o mecanismo devem ser confirmados pela análise de Update/Draw no Ghidra.

Fallbacks geométricos genéricos ficam restritos a testes, ferramentas e modo de diagnóstico explicitamente indicado. Se um asset nativo não for encontrado/decodificado, registrar a falta com seu caminho e bloquear a declaração de fidelidade daquele elemento, em vez de substituir silenciosamente. Temas visuais customizados podem continuar como uma opção separada, que não comprova fidelidade ao Joey.

## Componentes

A consolidação da análise estática da branch `analise-visual` foi organizada
em [**contrato visual nativo**](native-visual-contract.md). As fórmulas de mão,
centros do campo, contadores de ativação/explosão e despacho de objetos
agora residem em `Joey.LookAndFeel` e não em `Joey.Core`.
O port modela **updates e parâmetros** com base nos dados existentes, distinguindo
cadência nominal recuperada, comportamento observado e timings ainda não medidos.


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

No `Joey.App`, `PresentationTickClock` converte o tempo entre renders em
passos inteiros e guarda a fração para o próximo quadro. Classic aplica 30
passos/s e Fast aplica 60 no hover da mão, no deslocamento da carta e no
brilho de ativação. Os três usam os mesmos contadores nos dois presets.
Instant ignora essas transições. O intervalo entre ações da CPU e os
outros cues continuam sob política do cliente. `--mute-joey` configura
a preferência de voz, ainda sem player de áudio.

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
a cadência nominal selecionada no preset. O app raylib
desenha os dois sprites e o scratch intermediário. A fidelidade de mistura,
tabela de cosseno e temporização com o original **não foi demonstrada**.

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
