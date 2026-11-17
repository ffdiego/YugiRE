# Ferramentas visuais offline

Python 3, biblioteca padrão. Nenhum comando executa o jogo, hooks ou helpers nativos.
Forneça os caminhos dos originais explicitamente; não há busca nem download de dados do jogo.

```bash
python3 -m unittest discover -s tools/visual/tests -v
python3 tools/visual/joey_visual.py "$VISUAL_DAT" inventory --decode-yga --output scratch/visual/inventory.json --csv scratch/visual/inventory.csv
python3 tools/visual/validate_corpus.py "$VISUAL_DAT" --exe "$VISUAL_EXE" --output scratch/visual/validation.json
python3 tools/visual/joey_visual.py "$VISUAL_DAT" scene 'j\duel\effect\vija.txt' --output scratch/visual/vija.json
python3 tools/visual/joey_visual.py "$VISUAL_DAT" export-yga 'j\duel\effect\coin_sha.yga' --frames 4 --output-dir scratch/visual/coin-shadow-new
```

Crie `scratch/visual` antes dos relatórios. `inventory` lê os 3.568 membros em memória,
um de cada vez; seu JSON/CSV contém somente metadados. `--decode-yga` valida o
consumo integral do LZSS interno. Os BMPs têm geometria/limites validados; não se
interpreta sua apresentação nativa. O CSV classifica finalidade provável pelo
caminho, separada da certeza do formato. O campo `parameter` dos manifestos é
um inteiro bruto ou `null`, nunca coordenadas/padrões presumidos.

`export-yga` só grava PNGs em `scratch/` deste checkout, que é ignorado pelo Git;
recusa sobrescritas e saídas externas. `--frames` é divisão horizontal fornecida
pelo operador, não metadado embutido no YGA. Não gera animação temporizada.
Os canais BGRA são reordenados para RGBA sem ajustar alpha ou reproduzir blends.
Limite por recurso/saída decodificada: 16 MiB; dimensões até 4.096.
Modo 1 é LZSS; modo 0 é raster bruto e aceita `CompressedSize` zero ou igual ao
payload. Não há amostra modo 0 no DAT original: cobertura sintética e evidência estática.

## Referência externa e comparação

O projeto [derplayer/YuGiOh-PoC-ModTools](https://github.com/derplayer/YuGiOh-PoC-ModTools/tree/b689dfbfc593916e7e20ea4873ce92ef81c9fc0d)
já documentava YGA e implementava seu decoder. O parser Python foi iniciado a
partir do corpus e do loader local antes de recebermos essa referência; ela
corroborou cabeçalho, canais e LZSS e revelou a convenção `CompressedSize=0`
do escritor de modo bruto. Não reivindicamos descoberta inédita do formato.

Para repetir a comparação, obtenha **somente** `YuGiLZSS.cs`, `YuGiBuffer.cs` e
`YuGiCompressedWord.cs` de `YuGiOh_PoC_Patcher/YuGi/` naquele commit e seu
`LICENSE`, em `scratch/visual/upstream/`. Mantenha a licença MIT junto às cópias.
Não é necessário baixar, compilar ou iniciar o launcher Windows.
Copie `upstream_compare.cs` como `Program.cs` nessa pasta e crie `Compare.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework></PropertyGroup>
</Project>
```

```bash
dotnet run --project scratch/visual/upstream/Compare.csproj -- "$VISUAL_DAT" scratch/visual/inventory.json
```

Resultado da sessão: 537/537 hashes e comprimentos coincidem. Isso é comparação
entre duas implementações offline, **não execução do decoder do jogo**.
Os hashes dos fontes externos estão em `docs/research/visual/upstream-comparison.json`.

## Ghidra

Use uma cópia descartável do projeto existente, dentro de `scratch/`; nunca
passe o projeto persistente para `query.sh`. A ferramenta reutiliza
`scripts/QueryArchive.java`, sempre com `-noanalysis -readOnly` e sem jogos em execução.
Execute uma consulta de cada vez.

```bash
bash tools/visual/query.sh "$VISUAL_GHIDRA" "$PWD/scratch/visual/ghidra" "$PWD/scratch/visual/loader.txt" tempfunc:00497360 tempfunc:00496ab0 ins:00497400:65
bash tools/visual/query.sh "$VISUAL_GHIDRA" "$PWD/scratch/visual/ghidra" "$PWD/scratch/visual/animation.txt" tempfunc:00450590 tempfunc:004505d0 tempfunc:00456730 tempfunc:004514b0 tempfunc:005498b0 tempfunc:005605a0
bash tools/visual/query.sh "$VISUAL_GHIDRA" "$PWD/scratch/visual/ghidra" "$PWD/scratch/visual/dispatch.txt" tempfunc:0045cf80 tempfunc:0045cfe0 tempfunc:0045d040 tempfunc:0045d150 tempfunc:0045d170
```

Artefatos extraídos e fontes externos não entram no versionamento.

## Ativação azul: visualizador e captura preparados

A etapa seguinte está em [ActivationViewer/README.md](ActivationViewer/README.md):
executável raylib-cs isolado, composição CPU de três passes, avanço manual e
comparação de referência/diferença. A [análise específica](../../docs/research/visual/activation-effect.md)
fecha os oito argumentos de `00422C00`, as escalas/ângulos e as tabelas de alpha.
`capture/` prepara uma captura natural limitada no Windows, ainda não executada.
