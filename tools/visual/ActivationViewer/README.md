# Visualizador isolado de uma ativação azul

Executável de investigação em .NET 10/raylib-cs 7.0.2. Não integra `Joey.App`,
não avalia regras da engine e não modifica Core, hooks ou originais. Usa somente
o leitor `Archive` já existente de Core para obter dois YGAs. Renderiza em CPU,
em seguida usa raylib para apresentar os pixels, evitando substituir a mistura
inteira nativa pela mistura padrão da GPU.

**Estado: reconstrução estática testada; fidelidade visual nativa ainda não validada.**
O usuário optou por preparar a captura Windows para depois. Nenhuma duração
original está embutida. A janela abre pausada; sem replay só há passos manuais.

## Teste sem janela

Da raiz do checkout, com dependências já disponíveis:

```bash
dotnet restore tools/visual/ActivationViewer/ActivationViewer.csproj
dotnet build tools/visual/ActivationViewer/ActivationViewer.csproj --no-restore -m:1
dotnet run --no-build --project tools/visual/ActivationViewer -- --self-test
dotnet run --no-build --project tools/visual/ActivationViewer -- --dat "$VISUAL_DAT" --card-size 54,78 --check
```

`--self-test` executa 15 verificações sintéticas sem DAT/raylib/janela.
`--check` lê os dois YGAs do DAT de SHA fixado e renderiza os 17 contadores
0,16,…,256. O tamanho 54×78 acima é **contexto declarado para teste**, não medição
Windows. O visualizador exige tamanho declarado ou replay; não preenche um
campo observado ausente com esse tamanho.

## Inspeção e exportação

```bash
dotnet run --no-build --project tools/visual/ActivationViewer -- --dat "$VISUAL_DAT" --card-size 54,78 --position 525,352 --counter 128

dotnet run --no-build --project tools/visual/ActivationViewer -- --dat "$VISUAL_DAT" --card-size 54,78 --counter 128 --headless --export-dir scratch/visual/activation-frame-new
```

Setas avançam/recuam o contador; R volta ao início; Escape fecha. A atualização
visual da janela usa 60 FPS, exclusivamente para interação — **não é a cadência
do efeito original**. Espaço só reproduz quando há timestamps capturados.
PNG/JSON só são exportados dentro de `scratch/`, sem sobrescrever arquivos.
O JSON distingue reconstrução de estado declarado e replay de timeline capturada;
`windowsPixelParityVerified` permanece falso.

## Comparação com captura

Primeiro execute o [protocolo Windows](../../../docs/research/visual/activation-windows-capture.md)
e analise o JSONL. Depois:

```bash
python3 tools/visual/capture/analyze_activation.py scratch/visual/activation-windows/capture.jsonl --output scratch/visual/activation-windows/replay.json

dotnet run --no-build --project tools/visual/ActivationViewer -- --dat "$VISUAL_DAT" --replay scratch/visual/activation-windows/replay.json --counter 128 --background scratch/visual/before.png --reference scratch/visual/original-128.png --reference-counter 128
```

O caminho exato do JSONL é impresso pelo coletor, não se chama automaticamente
`capture.jsonl`. O replay fornece tabela cosseno observada, dimensões da superfície
central, posição, contadores e timestamps QPC. O visualizador recusa playback se
seus quatro vértices não coincidirem com os vértices registrados dos dois passes.
Isso verifica **geometria**, não sampling/blend/pixels finais.

Use screenshots **da área cliente 800×600, sem redimensionamento**, da mesma cena,
e associe explicitamente `--reference-counter`. O fundo anterior deve corresponder
à cena por baixo do efeito. Sem fundo correspondente, mudanças de cartas/seleção/UI
entram na diferença e impedem atribuí-la ao raster do efeito.

A imagem reconstruída aparece à esquerda; a referência à direita. D alterna
referência/diferença RGB absoluta ×4. Métricas são retidas somente quando o contador
declarado da referência coincide com o quadro atual: média absoluta RGB, máximo e
pixels diferentes numa região de até 255×255 ao redor do efeito. Não há score de
“fidelidade” automático. Repetidos draws do mesmo contador ainda precisam do
alinhamento temporal e de uma referência visual correspondente.

`--headless --export-dir ...` com uma referência correspondente também exporta a
imagem da diferença e seu resumo no JSON. A referência pode ser lida sem abrir janela.

## Contrato e limites

Implementa a ordem `kouka_sita_b.yga` → superfície preta da carta →
`kouka_ue_b.yga`; escala 16.16, ângulos de 512 unidades/volta, MulDiv, vértices
inteiros e alpha por duas divisões truncadas. O scanline CPU segue as regras
20.20 observadas, com validação de limites em todas as leituras. Empates nas
arestas/clipping/arredondamentos e bit depth continuam dependendo de diferencial
com imagem original. Ver [evidências e fórmulas](../../../docs/research/visual/activation-effect.md).

Somente a ativação azul da classe base `005DB2F4` é coberta. Outros efeitos,
subclasses, efeitos amarelos de monstro e renderização 16 bits são recusados ou
ficam fora do contrato. A comparação Windows, interação da janela e equivalência
pixel a pixel ainda não foram executadas neste Linux sem DISPLAY.
