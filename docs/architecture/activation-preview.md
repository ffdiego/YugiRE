# Prévia de ativação de magia — raylib + Joey.LookAndFeel

**Situação:** fatia de apresentação **implementada**, não é reprodução
fiel do executável. A trilha de animações fica **pausada** após esta entrega;
a prioridade seguinte volta aos comportamentos de cartas no `Joey.Core`.

## O que pode ser experimentado

Na raiz do projeto, no Windows, com o `data.dat` original disponível:

```powershell
dotnet run --project src/Joey.App -- ..\passion\data.dat
# Perfis de apresentação (sem mudar o duelo):
dotnet run --project src/Joey.App -- ..\passion\data.dat --fast
dotnet run --project src/Joey.App -- ..\passion\data.dat --instant
# Confere os quatro YGAs de ativação e os BMPs existentes sem abrir a janela:
dotnet run --project src/Joey.App -- ..\passion\data.dat --check-graphics
```

No duelo demo, **ative uma magia jogável da mão, uma magia previamente setada,
uma magia de campo setada, ou equipe um monstro**. O renderizador agora reproduz
uma composição de ativação na região correspondente. Apenas ações **aceitas**
pela engine disparam a apresentação; tentativas inválidas não animam. Resetar
o duelo cancela a animação anterior.

## Fonte dos sprites

Os quatro recursos são decodificados **diretamente do DAT local**, sem
exportá-los nem copiá-los ao repositório:

| Índice nativo | Recurso | Uso |
| ---: | --- | --- |
| 0 | `j\duel\effect\kouka_ue_b.yga` | Superior azul |
| 1 | `j\duel\effect\kouka_sita_b.yga` | Inferior azul |
| 2 | `j\duel\effect\kouka_ue_y.yga` | Superior amarelo |
| 3 | `j\duel\effect\kouka_sita_y.yga` | Inferior amarelo |

`NativeYgaSprite.Decode` valida assinatura, dimensões, modo e tamanhos,
reutiliza `Archive.Decode` como dependência injetada para LZSS, converte
BGRA → RGBA e aplica a color key RGB preta. Assim
`Joey.LookAndFeel` **não referencia** `Joey.Core` ou raylib.
`DatGraphics` carrega o raster RGBA na GPU apenas quando necessário.
`--check-graphics` verifica os quatro YGAs e suas dimensões 164×164.

## Como os parâmetros são gerados

`ActivationEffectPlayback` consome `NativeActivationTimeline` e
`NativeActivationFrame`, recuperados do Ghidra no ciclo anterior:

1. A ativação começa em `t=0`.
2. Cada *update lógico* acrescenta 16; último contador ativo `t=256`.
3. Após o 17º update, `t=272`, a apresentação termina.
4. Por amostra, o efeito desenha inferior → retângulo preto → superior.
5. Escala, ângulos e opacidades são obtidos do descritor de três passes;
   a categoria azul é utilizada na integração atual, com a amarela
   disponível nos recursos e no modelo.

Classic executa os 17 updates a 30 passos/s e Fast a 60, conforme a
cadência nominal recuperada do assembly. O FPS de renderização pode
variar. Instant ignora a prévia. O relógio de mensagens em
`DuelPresentationClock` permanece separado. [Contadores e endereços](native-visual-contract.md#contadores-do-executável-e-efeito-de--speedy)

### Limitações que ainda impedem declarar fidelidade

- A tabela x16.16 é hoje uma **aproximação com `Math.Cos`**, não os
  512 valores originais lidos de `DAT_00A52EE8` no processo.
- A composição usa **alpha blend da raylib/GPU**, que não é bit a bit
  equivalente às duas divisões inteiras, clipping, color key e scanlines
  de `0x004276F0`. O visualizador experimental da
  [`analise-visual`](https://github.com/ffdiego/YugiRE/tree/analise-visual)
  contém um rasterizador de referência para futuros testes diferenciais.
- O brilho é centralizado sobre a zona de ativação da carta. Para magias
  ativadas da mão, o demo usa a primeira zona livre ou a zona de campo.
  Uma carta que já foi ao cemitério permanece visível como cópia de
  apresentação até o brilho acabar. As dimensões do scratch seguem as
  dimensões de zona do cliente.
- A apresentação recebe a carta ativada e sua zona após uma ação bem-sucedida
  do humano ou da CPU. A destruição usa um evento tipado do Core e aguarda o
  brilho de ativação acabar. As partículas são geradas pelo cliente nas
  janelas do contador de destruição recuperado. O cemitério apresenta uma
  pilha virada para baixo e atualiza a contagem ao fim da animação.
- Sem captura da versão Windows do original, não há afirmação de
  temporização precisa, sincronia com vozes ou paridade de pixels.
- O jogo não implementa ainda o fluxo geral de armadilhas ativadas;
  a amarela está preparada mas não é aplicada automaticamente a toda
  armadilha só por seu tipo.

## Testes

`ActivationEffectChecks` cobre YGA bruto/comprimido com amostras
sintéticas, dimensões inválidas, color key, ordem dos quatro recursos,
contador de 17 passos, perfis de velocidade, posicionamento e cancelamento.
A verificação completa dos quatro YGAs **reais** fica em
`--check-graphics`, executável apenas com o DAT do usuário.

## Ponto de retomada, se voltarmos ao visual

Conferir timings e pixels capturados no Windows pelo protocolo
[`activation-windows-capture.md`](https://github.com/ffdiego/YugiRE/blob/analise-visual/docs/research/visual/activation-windows-capture.md).
Depois recuperar a tabela de cossenos e medir a ordem exata update/draw;
só então considerar rasterização bit a bit, áudio e timeline de eventos.

**Não expandir a camada de animação agora.** Próxima prioridade:
`PLANS.md` → Iteração B, cartas FLIP e seus comportamentos (e.g.
Hane-Hane, Morphing Jar #2).
