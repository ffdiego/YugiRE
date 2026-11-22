# YugiRE — protocolo de revisão visual com o Joey original

Data da rodada: **21/11/2026**, após PRs #31, #33, #34, #35, #36,
#37, #38, #39 e #40. O objetivo é comparar **comportamento
observável**, sem exigir equivalência da implementação nativa.

A cobertura declarada é **19/31 rotinas Draw com implementação
parcial ou concluída**, **0 Verified**: essa porcentagem não
é um índice de fidelidade em pixels. Não promover nenhuma
rotina a Verified só porque o DAT certo é usado.

## Como capturar e comparar no Windows

Execute na raiz do repositório, com o `data.dat` original:

```powershell
dotnet run --project src/Joey.App -- .\passion\data.dat --check-graphics
dotnet run --project src/Joey.App -- .\passion\data.dat --alpha
```

- **F3** mostra/oculta indicadores de debug. Capturar sem debug para
  a comparação final, e com debug quando for útil identificar o estado.
- **F6** pausa ou retoma o avanço do duelo no cliente. Quando pausado,
  comandos de jogo por mouse não são aceitos; o Core não é
  retrocedido e a função não permite navegar por estados anteriores.
- **F7** (quando F6 está pausado) aplica um **update nominal** do
  perfil de apresentação (Classic=30/s; Fast=60/s). É um recurso do
  **cliente**; a taxa nativa real e a ordem Update/Draw ainda não
  estão comprovadas. O modo `--instant` não tem updates para avançar.
- Quando uma invocação normal ocorre, F12 também registra o último
  **candidato** a cut-in (`lastSignatureCandidateResourceId` e
  `lastSignatureCandidateManifest`). Isso não significa que a cinemática
  tenha sido renderizada; `signatureCinematicRendered` permanece false
  até existir implementação sustentada pelo Draw do busto do Joey.
- **F12** grava o *render target* do YugiRE, sempre com **800×600
  pixels**, em `scratch/visual-review/yugire-<UTC>.png`.
  Ao lado, grava um JSON com turno, fase, lado, LP lógico e exibido,
  delta pendente, tipo de efeito e contadores de animação.
  O caminho absoluto aparece no terminal.
- Os arquivos de revisão ficam em `scratch/` e **não são
  versionados**. Nenhuma captura ou dado do jogo é enviada
  automaticamente a terceiros.

O screenshot do Joey original deve estar na **mesma fase,
perspectiva, zona, carta e update**. A captura de YugiRE
não prova por si só que o original executou o mesmo número de
Updates. Evitar comparar screenshots redimensionadas,
com borda de janela, tooltips, mouse ou FPS diferentes;
recortar primeiro a área original de 800×600.

## Levantamento de recursos do DAT antes da revisão

Para confirmar a ordem dos sprites e dimensões de atlases sem abrir o
Ghidra nem iniciar a janela raylib, use no mesmo ambiente Windows em que
está o `data.dat`:

```powershell
dotnet run --project src/Joey.App -- .\passion\data.dat --inspect-visual-dat
```

O comando examina os **57 manifests** catalogados em `j\duel\*.txt`
e também os **17 manifests** de `j\bust_up\*.txt` (74 no total,
incluindo subdiretórios). Registra metadados dos BMPs/YGAs das duas
árvores, incluindo 160 imagens do busto/reação do Joey. Grava
`scratch/visual-review/dat-manifests.json`, com conteúdo Latin-1 e
hexadecimal dos manifests, dimensões e SHA-256 de cada imagem. **Não**
exporta pixels ou BMP/YGA, não inicia a janela e não executa o EXE
nativo. Cada registro de imagem contém erro explícito caso o
decodificador não reconheça seu formato; nesse caso o comando devolve
código 2, mas mantém o relatório disponível para análise.

Na investigação, conferir primeiro os manifests `sacrifice.txt`,
`set.txt`, `sp_summon.txt`, `fusion.txt`, `break.txt`,
`shuffle.txt`, `coin.txt`, `dice.txt`,
`exclusion.txt`, `chain.txt`, `number.txt` e
`lifepoint.txt` antes de supor a ordem dos sprites. O JSON não substitui
a captura de frames, mas evita associar arbitrariamente um bitmap a
uma posição do atlas.

**Nova família a observar (ainda não implementada):** reações do Joey,
invocação de Red-Eyes B. Dragon e outras cenas signature em `j\bust_up\`,
além do efeito Vija da vitória F-I-N-A-L em `duel/effect/vija.txt`.
A cadeia de construtores, as esperas nativas e as cinco cartas da condição
foram investigadas em [bust-up e Destiny Board](bust-up-cinematics-and-destiny-board.md).
Nenhuma dessas observações deve elevar a badge até haver Draw ativo.

## Cursor e balões de ação sobre a carta

O balão original de Summon/Set/Activate agora vem de
`cursor/cursor_e_*.yga`, e o ponteiro próprio usa `cursor00.yga`
no canvas 800×600. O inspetor `--inspect-visual-dat` também
exporta metadados dos 68 registros `cursor/` do DAT.
Comparar com o original a ponta da nuvem, o pivô do cursor,
a transição do botão direito (Summon → Set) e transparência;
F12 registra a posição do cursor e o nome da ação selecionada.
Esses elementos **não contam** nas 31 Draw inventariadas.
Ver [pesquisa de cursor](native-cursor-and-action-clouds.md).

## Matriz de comparação

| Fatia (fontes nativas) | Provocar no cliente | Estados/quadros sugeridos | Divergência a procurar |
| --- | --- | --- | --- |
| Abertura `00451980` | Iniciar Alpha e duelo local | logo t=0/64/256/312 | escala, alfa, centro, background e ordem relativa à distribuição |
| Mãos, carta `00418EF0` | Distribuição inicial, compra e descarte | primeira/última carta, dois lados | face, orientação, tamanho, interpolação, ordem |
| Set `0044F390` | Baixar monstro e magia/armadilha, inclusive CPU | t=0/132/252; 8 estrelas | fonte e cor, posições, alfa, clipping, ordem dos 6 BMP |
| Sacrifício `004870B0` | Invocar monstro nível 5–8 tributando 1 ou 2 monstros | t=0/128 (hold)/136/256/384; primeiro e último tributo | ordem das remoções antes do voo, 8 estrelas, textura, rotação, som, `sacrifice.txt` |
| Invocação normal `0044FA10` | Invocar face-up dos dois lados | t=0/64/72/256 | associação de miniatura, segundo passe, fade e escala |
| Invocação especial `00450100` | Recrutadores do deck, Morphing Jar #2 | t=0/72/184/192/312 | a/d conforme posição, quatro YGA, overlay da carta e camadas |
| Ataque `004142C0` | Ataque direto e a monstro | mira, voo, contato, impacto | troca espada inteira/parcial, centro de contato, ângulo |
| Destruição `004514B0` | Combate e destruição por efeito | t=0/32/128/160/184 | passes e seleção dos 7 YGA, instante de sumiço da carta |
| LP `0047C660` | Dano direto, cura, dano letal | antes e depois de 32 updates, decrementos 100/10/1 | fonte, barra, deslocamento, limite 8000 e sincronização com Core |
| LP flutuante `004806F0` | Dano/cura com valores de 1 a 4 dígitos | fase 0/64/128/192/256 | atraso por glifo, sinal, atlas damage00/01, origem e cor |
| Shuffle `00453560` | Buscas e Morphing Jar #2 | t=0/56/64/256/264/320 | centro, tamanho do BMP, moldura, sentido e som |
| Fase `00481420`, aviso `00480C60` | Mudar para Battle e End Phase | 7 quadros de Battle (2 updates cada) | botão, animação de giro, cor, fade e ordenação |
| Resultado `00451E60` | Vitória, derrota e empate | t=0/64/128/512 | sprite, centragem, fade, eventual saída/som |
| Exodia `00454010` | Concluir as cinco peças na mão por compra, Sangan ou Hane-Hane | primeiro ramo t=0/16/32/64/80/312 | ordem das cinco miniaturas, ângulos, alfa, etapa posterior de montagem e áudio |

Quando houver divergência, anexar **par** de PNGs (original
e YugiRE) e o JSON do YugiRE, informando sprite/tipo,
update, lado, tamanho do canvas e se era Classic/Fast.
Nome sugerido para registro:
`<familia>-<lado>-<contador>-<original|yugire>.png`.

## Pendências que não devem ser tratadas como implementadas

As **12 rotinas Draw** abaixo constam `Pending`
no inventário após PR #40. Seus entrypoints e alguma análise
estática existem, mas faltam evidências de evento, atlas,
protocolo de jogo ou composição para uma fatia vertical segura.

| Entry point | Família | Bloqueio principal antes da implementação |
| --- | --- | --- |
| `0045E4A0` | Ajuda | tela/caminho de abertura e asset por idioma |
| `0040E260` | Estado persistente | tradução dos estados reais em ícones de campo e flags |
| `00450FD0` | Quebra | distinguir comando/evento nativo de destruição padrão |
| `00450AD0` | Ativação alternativa | gatilho e tipos de efeito que usam essa variante |
| `004524F0`, `004877F0` | Fusão | fluxo de seleção/materialização e dois renderers distintos |
| `0041F870` | Corrente | fluxo de cadeia/ordem/respostas ausente no Core |
| `004531C0` | Corrente desativada | condição do indicador durante as correntes |
| `0043DBE0` | Exclusão | evento nativo de banimento e efeito visual correto |
| `00457520` | Moeda | ação e decisão por efeito, distribuição de quadros/resultado |
| `00456B90` | Dado | ação e decisão por efeito, quadros/resultado |
| `004584C0` | Vija | efeito de carta específico e máscara/recursos nativos |

Priorizar trabalhos subsequentes por **efeitos acionáveis no Core
e evidência concreta suficiente**, não por subir a porcentagem
da badge. Usar primeiro `joey-knowledge.sqlite` (6.518 funções)
e o ZIP Ghidra visual já fornecido. Nova extração Ghidra
apenas se essas fontes não atenderem à pergunta.

## Validações sem abrir o original

O CI valida build .NET 10, `Joey.Tests` e os testes
Python/Node. O comando `--check-graphics` valida
decodificação dos recursos DAT que já foram conectados.
**Nenhuma dessas verificações demonstra equivalência visual
com a versão Windows.** Para declarar Verified, capturar
pares nos estados equivalentes, registrar a geometria/timing
e demonstrar tolerâncias de comparação por pixel.
