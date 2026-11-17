# Handoff — reconstrução de uma ativação azul e preparação Windows

Data: 2026-11-17. Branch `analise-visual`. Tarefa pontual do usuário: rastrear
composição, observar uma ativação natural e criar visualizador isolado.
**O usuário escolheu expressamente preparar a captura Windows para depois.**
Não há host Windows conectado, nem captura natural nova nesta entrega.

## Arquivos e commits

- `0badee4`: contrato estático, evidência, visualizador independente, observador Windows limitado e testes.
- O commit deste handoff acrescenta a validação de centro parcialmente fora da tela (como mão em Y=600), ajusta o diagnóstico de região de comparação vazia e registra os resultados finais.
- Ferramenta: `tools/visual/ActivationViewer/` (.NET 10/raylib-cs 7.0.2).
- Captura/análise: `tools/visual/capture/`; não se alterou `native_hooks/`.
- Contrato: [activation-effect.md](../visual/activation-effect.md).
- Coleta futura: [activation-windows-capture.md](../visual/activation-windows-capture.md).

Nenhuma edição na aplicação/engine/layout ou em outro checkout; nenhum push,
merge novo, dependência grande ou instalação de ferramentas. O projeto do
visualizador não foi adicionado à solução principal. Ele referencia Core somente
para usar `Archive`; não usa regras ou estado de duelo para preencher observações.
Restore usou exclusivamente pacotes já em cache e fonte local vazia.

## Fatos estáticos fechados

1. `00422C00` recebe oito argumentos cdecl: destino, fonte, centro X/Y, ângulo,
   escala X/Y 16.16 e opacidade. O pseudocódigo sem inspeção de pilha não bastava.
2. Há três passes: `kouka_sita_b.yga` com ângulo t/2, scratch preto da carta sem
   transformação, `kouka_ue_b.yga` com ângulo t. O scratch usa color key branco.
3. Escala `C[(t-128)&511]`, opacidade dos YGAs `min(escala>>8,255)`, scratch
   `escala>>9`. C é cosseno ×65536, 512 posições; geometria usa largura/altura
   menos um, MulDiv e inteiros após rotação. Não são sprites escalados apenas por
   `DrawTexturePro` nem composição padrão alpha da GPU.
4. `00546D70` e `004276F0` sustentam duas divisões: alpha efetivo por produto/255,
   depois delta de canal por /255 com truncamento em direção a zero. Sampling
   poligonal tem 20 bits fracionários. O passe preto percorre `004D71C0`/LUT.
5. O observador seleciona somente a classe base azul em contador zero, registra
   chamadas naturais, parâmetros, QPC, source/destination formats e vértices;
   não chama artificialmente funções do jogo. As duas NativeFunction são APIs
   Windows de leitura do relógio. Interceptadores, alocações e envio têm overhead.

## O que foi construído

Visualizador com passo manual, replay de draws/timestamps capturados, composição
CPU, fundo de referência, comparação lado a lado e diferença RGB/ROI, exportação
PNG/JSON restrita a scratch. Exige dimensões declaradas ou capturadas; não fixa
FPS original. Com replay, rejeita geometria que difira dos vértices registrados.
A camada interativa usa 60 FPS apenas como refresh da UI.

Coletor de uma ativação natural, PID já aberto, Windows x86, hashes antes/depois,
limite de 120 segundos/4096 eventos, rollback de attach e desarmamento. Analisador
recusa sequência perdida, footer incompleto, overflow, erros, formato inesperado,
17 pares de updates incompletos e divergência dos três passes. Não há screenshots
dentro dos hooks; referência de vídeo precisa ser alinhada separadamente.

## Validação executada

- Build do projeto isolado: PASS, zero avisos/erros.
- `--self-test`: **15 verificações C# sintéticas**, incluindo MulDiv, rotações,
  alpha/LUT, pixels opacos, contadores inicial/final, efeito morto e clipping com
  centro Y=600. Não são oráculo nativo.
- `--dat <original> --card-size 54,78 --check`: dois YGAs reais e 17 contadores
  renderizados; 54×78 é contexto declarado para teste, não dimensão observada.
- Exportação PNG no pico t=128, visualmente inspecionada; CRCs e tamanho inflado
  verificados independentemente em Python.
- Referência igual à própria reconstrução: zero diferenças, **somente teste da
  infraestrutura de comparação**, não paridade com o original.
- `python3 -m unittest discover -s tools/visual/tests -v`: **15 testes PASS**,
  incluindo rejeição de capturas incompletas, relógio/tipo/thread/ordem incorretos.
- `node tools/visual/tests/test_activation_observer.cjs`: PASS com mocks de Frida;
  seleção, leitura de argumentos, QPC, rollback, gate Windows e limite.
- `node --check`, `py_compile`, links locais e `git diff --check`: PASS.

Não executados: original no Windows, coleta Frida real, interação da janela
(sem DISPLAY), comparação de quadros nativos, equivalência de sampling/clipping.
Não se repetiram regressões completas da engine, pois não houve mudanças nela.

Hashes dos originais conferidos e preservados:
EXE `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`;
DAT `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.
PNGs, logs Ghidra e traces reais/sintéticos não são versionados.

## Ponto de parada e próximo experimento

A parte dependente de Windows foi adiada por escolha explícita do usuário. Falta
captura real para medir cadência, validar ABI/formatos/dimensões, aceitar o replay
e comparar pixels. Não se afirma objetivo de fidelidade alcançado.

No Windows, da raiz deste checkout, com processo já aberto e substituindo PID e
caminhos reais:

```powershell
python tools\visual\capture\capture_activation.py --pid 1234 --exe 'C:\jogo\joey_pc.exe' --dat 'C:\jogo\data.dat' --seconds 60 --max-events 1024 --output-dir scratch\visual\activation-windows
```

Ativar uma magia/armadilha normalmente. Se timeout, erro ou formato não suportado,
preservar o arquivo e investigar; não forçar callbacks nem relaxar a validação.
Depois executar o analisador e o visualizador conforme os READMEs, com imagens
originais cliente 800×600 alinhadas em crescimento, pico e contração.
O scanline implementado continua candidato à equivalência, até comparação com
os pixels originais. Nenhuma continuação automática é prometida após este turno.
