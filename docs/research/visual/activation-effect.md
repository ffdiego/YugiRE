# Uma ativação azul — composição e visualizador isolado

Estado de 2026-11-17: **contrato estático reconstruído e visualizador funcional em
modo offline; fidelidade visual/temporal com o original ainda não validada**.
O usuário escolheu preparar a captura Windows para depois. Não há captura natural
nesta entrega. Não se retomou o layout da mão nem a implementação em `Joey.App`.

Alvo: EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
[Evidência selecionada](evidence/activation-composition.txt); VAs com base `00400000`.
O caminho é `004505D0 → 00422C00 → 00422840/00422E30`.
**A assinatura e alguns parâmetros do pseudocódigo anterior estavam incorretos**
por perda de pilha: os oito argumentos abaixo foram reconstruídos das instruções.

## Oito argumentos do helper transformado

`00422C00` é cdecl:

```text
(destino, fonte, centroX, centroY, angulo, escalaX16_16, escalaY16_16, opacidade)
```

As chamadas estão em `00450687` e `0045071D`, com `ADD ESP,0x20`.
Opacidade >=256 encaminha a `00422840`, sem atenuação global; até 255 usa
`00422E30`. A ativação base limita sua opacidade a 255, portanto percorre o
segundo caminho, mesmo no pico. Não confundir opacidade global com alpha do YGA.

## Três passes, não apenas duas texturas

Seja `t = objeto+0x10`, `centro = (+0x14,+0x18)` e `C` a tabela em `00A52EE8`.
`0055DCF0` calcula a tabela com x87 `FCOS`, constantes π (`005DCCF8`), 1/256
(`005DCCB0`) e 65536 (`005DCD00`), seguida de conversão truncada para inteiro.
A reconstrução usa `C[k] = trunc(65536 × cos(πk/256))`. O replay usa a tabela
**lida no processo**, evitando pressupor igualdade bit a bit entre Math.Cos e x87.

```text
s = C[(t - 128) & 511]          // escala 16.16
opacidade = min(s >> 8, 255)
```

| Ordem | Fonte | Rotação | Escala | Opacidade |
|---|---|---|---|---|
| 1 | índice 1: `kouka_sita_b.yga` | `trunc(t/2)` | `s,s` | `min(s>>8,255)` |
| 2 | scratch preto do tamanho da carta | sem rotação/escala | dimensões reais do scratch | `s>>9` |
| 3 | índice 0: `kouka_ue_b.yga` | `t` | `s,s` | `min(s>>8,255)` |

O manifesto `j/duel/effect/activate.txt` fornece esses dois primeiros recursos e,
depois, o par amarelo. O byte categoria em `+0x1C` escolhe os índices `2*categoria+1`
e `2*categoria`. A ferramenta cobre somente categoria zero/azul.

O passe do meio é importante: `00450260` cria a superfície `objeto+0xAC`, define
**color key branco** em `00450409–00450417`, e a preenche de preto em `0045041C–00450420`.
`004505D0` desenha-a centrada, usando suas dimensões reais. Não é uma terceira
imagem extraída do DAT, nem uma máscara de alpha equivalente às duas estrelas.
No pico `s=65536`, o passe preto usa 128/255, não exatamente 1/2.
As dimensões dependem de região/postura; não foram preenchidas a partir do demo.

## Geometria

`00422C00` obtém largura/altura pelo método virtual `+0x38` e calcula:

```text
W = MulDiv(largura - 1, escalaX, 65536)
H = MulDiv(altura - 1, escalaY, 65536)
left = -trunc(W/2); right = trunc((W+1)/2)
top  = -trunc(H/2); bottom = trunc((H+1)/2)
x' = centroX + ((x*C[angulo] - y*C[angulo-128]) >> 16)
y' = centroY + ((y*C[angulo] + x*C[angulo-128]) >> 16)
```

MulDiv arredonda ao inteiro mais próximo, com empate afastando de zero. `>>16`
é deslocamento aritmético. Ângulo 128 é um quarto de volta; em coordenadas de
tela cresce no sentido horário. W/H zero evita chamar o rasterizador.
UVs usam os limites da fonte com right/bottom menos um.
O campo inteiro sem escala tem 164×164 pixels, não 163×163: a extensão é 163,
os extremos de rasterização são inclusivos.

`00422E30` escolhe o kernel conforme os formatos em `fonte/destino +0x74`.
No caminho destino 7 / fonte 12, o kernel é `004276F0`. Ele percorre arestas e
scanlines com **20 bits fracionários**, offset inicial `0x80000`, truncamento
nas divisões e amostragem inteira. O visualizador implementa essas regras com
limites explícitos. A equivalência de empates de aresta, clipping e amostragem
precisa da comparação com pixels reais; não foi inferida dos testes sintéticos.

## Alpha e mistura de canais

`00546D70` constrói as tabelas usadas em `004276F0`:

```text
effective = floor(alphaDoPixel × opacidadeGlobal / 255)
out = destino + trunc((fonte - destino) × effective / 255)
```

Há **duas divisões inteiras**. O delta de canal negativo trunca em direção a zero.
Pixels de color key são preservados, não misturados. Para os YGAs do contrato, o
wrapper usa color key RGB zero; o observador/analisador verifica esse campo em
vez de presumir que todo YGA do jogo o use. Alpha zero preserva o destino.
O scratch tem color key branco e fonte preta: aplica a segunda fórmula com a
opacidade global `s>>9`, sem o alpha de um YGA. A rota passa por `0041AB90`,
`0041ADA0`, `004C8500` e, em formato 7, `004D71C0`.

A apresentação raylib recebe pixels já compostos em CPU com alpha final 255;
não se delega essa aritmética à mistura padrão da GPU. O uso de RGBA na ferramenta
é uma conversão do BGRA do arquivo, não uma mudança na fórmula nativa.

## Contador e duração

`00450590` incrementa t em 16 e mantém ativo enquanto o resultado for menor que
257. Uma instância iniciada em zero passa por 17 updates até 272, quando é
desativada. Isso **não prova 17 quadros visíveis**, nem uma duração em segundos:
a ordem update/draw, draws repetidos e cadência precisam ser observados.
O visualizador manual oferece contadores 0,16,…,256, sem reprodução automática.
Seu replay usa os draws e timestamps QPC medidos, inclusive contadores repetidos.

## Ferramentas e validação

- [Visualizador .NET/raylib-cs e comandos](../../../tools/visual/ActivationViewer/README.md).
- [Protocolo de captura natural Windows](activation-windows-capture.md).
- Coletor/analisador em `tools/visual/capture/`, com hashes, sequência, limites e desmontagem.

Nesta sessão: build do executável sem avisos/erros; **14 checks C# sintéticos**;
**15 testes Python** (dez de recursos, cinco de integridade da captura); mocks JS
para seleção/ABI de leitura/rollback/limite; renderização offline dos 17 contadores
usando os dois YGAs reais; PNG inspecionado e CRC/inflação verificados por Python.
Uma comparação da reconstrução com ela mesma deu diferença zero — **somente teste
da comparação**, não evidência de fidelidade com o original.

Pendente: executar o coletor no Windows, conferir tipos/dimensões/ordem reais,
comparar os vértices capturados com o modelo, obter referências visuais alinhadas
e explicar diferenças em crescimento/pico/contração. A janela interativa também
não foi testada neste host sem DISPLAY. Até esse gate, não há alegação de efeito
reproduzido com fidelidade comprovada.
