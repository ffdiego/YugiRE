# V31.9 — contrato do busto de abertura

Fonte: SQLite cumulativo v14, leitura somente, PRAGMA quick_check=ok;
bundle aa3d7227… batch-000, janelas 00415580 e 00415840.
Capturas Proton descritas em V31.7 (roteiro 67).

- Update 00415580 pede 0x10000025 + variante quando o contador de entrada é 128.
  004157EA–00415830: consulta áudio se 384 < t < 65536; se aceita,
  grava 65536 e retorna sem somar 4. Do contrário soma 4.
- Draw 00415AC2–00415ACB limpa ativo em 0x10100. A amostra sem
  áudio teve 162 pares; a variante 1 com áudio saltou de 1148 e teve 352.
- 004158D3–00415914: x = posição X do personagem + (min(t-64,32)*largura >> 5)
  - largura; min apenas no limite superior. Largura 672 no DAT.
- 0041591A–00415975 desenha personagem enquanto t <= 128.
  Update com entrada 0 compõe fundo; entrada 128 recompõe com sombra,
  personagem e texto selecionado (linha 7 + variante).
- 00415975–00415A14 usa linha 3 exatamente em 128 e linha 4 depois;
  ambas têm parâmetro zero no manifesto (null.bmp), então não desenha.
- 00415A14–00415ABE lê (t >> 3)&3; não desenha boca na fase zero
  nem em t >= 0x10000. Demais fases consultam 005DA798[phase].
  A tabela não está nos blocos exportados; não presumir ordem.
- O manifesto inglês tem fundo 800x600, sombra 684x553 em (0,47),
  personagem 672x600 em (0,0), duas bocas 94x92 em (206,208),
  três textos 480x283 em (320,22). O banco valida as dez linhas,
  incluindo as duas nulas, e oito recursos presentes.
- Descritores Internal 0x25..0x27 apontam para 005F0798, 005F078C,
  005F0780; strings não recuperadas no SQLite. Roteiro 71 coleta
  apenas tabela e nomes faltantes.

## Revisão

dotnet run --project src/Joey.App -- /path/to/data.dat --review-reaction=start:1 --no-audio

F5 = reproduzir/pausar a 30 FPS; F6 = reproduzir/pausar a 60 FPS (-speedy);
F7 pausa e avança um Update/Draw; F8 reinicia pausado; F12 exporta PNG/JSON.
F5/F6 reiniciam automaticamente se a animação terminou. O relógio não pula
amostras em um host lento e não modifica os incrementos nativos.
--reaction-wait-audio mantém o predicado falso até F9 após pedir fala;
sem essa opção, o predicado já é verdadeiro (simulação de áudio desligado).
Os mesmos controles atendem reaction07. A fala real continua pendente.
--opening-mouth-table=K1,K2,K3 aceita somente chaves 5/6 fornecidas
a partir de uma captura real da tabela. Sem a opção omite boca e
registra a lacuna no JSON. Não usar valores de teste como evidência.

Compositor Raylib de camadas, não equivalência dos blits da superfície
temporária. Revisão pós-step omite objeto inativo no frame terminal.
Sem áudio real, sem RNG de regras, sem ligação à fila ou à abertura
jogável. O inventário permanece Pending. Testes sintéticos adicionados;
build/CI e DAT real pendentes nesta sessão.

## Ajuste após captura do cliente (28/11)

A captura do mantenedor mostrou o verde puro opaco na frente do fundo,
na composição do personagem e do balão. O decoder BMP existente já
possui substituição RGB(0,255,0) por transparente. O review passa a usar
esse caminho somente para as seis imagens de primeiro plano do start.txt;
o fundo statobg e a sombra YGA não passam por essa substituição.
É uma correção diagnóstica baseada na captura do cliente, não prova
de equivalência ao compositor DirectDraw original. Nova comparação
visual e CI pendentes. Nenhuma mudança de cobertura.
