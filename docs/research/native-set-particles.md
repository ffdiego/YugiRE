# Set: geometria das partículas — investigação direcionada

Revisão do renderer: \`0044F370\` (Update), \`0044F390\` (Draw),
\`0044EFD0\` (construtor), vtable \`005DB2C4\`.
Alvo: \`joey_pc.exe\` SHA-256
\`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de\`.

## O que já está integrado

A Alpha já executa o Set do Core e a transferência mão→zona. Após o voo,
\`Joey.App\` desenha o BMP de Set do \`data.dat\` usando amostras do
\`NativeSetFrame\`. O jogador confirmou visualmente que a última mudança
funcionou no seu ambiente. Isso **não** equivale a comparação de
pixels/frames pareados com o executável original.

## O que o lote Ghidra de 21/11 oferece

- \`0044F370\`: \`obj+0x10 += 0x0C\` por Update; flag ativa enquanto
  \`counter < 0x101\`; são 22 amostras \`0,12,...,252\`.
- \`0044F390\`: o laço itera oito vezes, incrementando o deslocamento de
  lançamento em 16 unidades (\`0x00..0x70\`). Lançamentos posteriores
  entram quando o contador alcança seu deslocamento.
- Quando \`age = trunc((counter - launchOffset) / 8)\` é não negativo,
  a variante é \`age & 1\` e o deslocamento vertical derivado é
  \`trunc(age * age / 16)\`. O Draw subtrai metade das dimensões para
  centralizar a imagem, **mas não determina o centro original**.
- O Draw escolhe quatro índices de partículas segundo o estado de cor.
  O mapeamento exato desses índices aos BMPs deve ser conferido com
  \`j\duel\effect\set.txt\` do DAT local.
- O pseudocódigo sugere um bloco de origens de partículas próximo de
  \`obj+0x104\`. Registradores e tipos reconstruídos pelo Ghidra estão
  ambíguos nesse trecho: verificar as instruções antes de fixar a
  estrutura e o deslocamento exatos.
- O construtor \`0044EFD0\` **não integra os 87 endereços** selecionados
  no ZIP anterior. Portanto os X/Y iniciais e a associação por lado
  ainda não foram extraídos. Não inventar posições ou dizer que as
  partículas já aparecem no cliente.

## Próxima extração pequena no Fedora

Na raiz do repositório (onde já existe o projeto Ghidra local):

\`\`\`bash
git pull --ff-only
python3 scripts/export_ghidra_visual_bundle.py \
  --targets 0044EFD0 0044F370 0044F390 \
  --disassemble --retry-missing \
  --output scratch/ghidra-set-constructor.zip
\`\`\`

O gerador executa Ghidra com \`-readOnly -noanalysis\`; o ZIP de
\`scratch/\` reúne pseudocódigo, **até 100 instruções por endereço**,
manifesto, logs e retries. A seleção \`--targets\` evita refazer as
demais 84 funções. Não versionar esse ZIP: anexá-lo ao chat para
inspeção. Não é necessário enviar EXE nem DAT.

Ao analisar a saída, identificar especificamente:

1. O preenchimento de cada origem em \`obj+0x104\` ou estrutura
   equivalente: base, oito deslocamentos, coordendas absolutas ou
   relativas, dependência de lado/região/slot e eventuais valores
   aleatórios.
2. Quais campos do construtor alimentam as coordenadas \`obj+0x14/0x18\`
   e a seleção de cor \`obj+0x1E\`; rastrear eventual chamada a
   \`0045C140\`.
3. A organização e dimensões dos BMPs em \`set.txt\`, além da
   transparência/composição nos dispatches de \`0044F390\`.
4. Com evidência suficiente, passar as origens reais ao modelo de
   partículas, desenhar os quadros em raylib e testar ambas as
   perspectivas. Até lá o Set permanece \`Partial\`, sem nova promoção
   de cobertura.

Esta tarefa **não** altera regras de \`Joey.Core\`, nem a animação já
integrada de Set, explosão ou espada.
