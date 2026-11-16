# Descobertas visuais e estado da investigação

Marco offline de 2026-11-16, branch `analise-visual`.

| Pergunta | Resultado | Tipo de evidência / limite |
|---|---|---|
| O que é YGA? | Raster BGRA32 de cabeçalho 24 bytes; modo 1 LZSS | Corpus inteiro + loader estático + implementação pública |
| O parser interpreta recursos reais? | 537/537 YGAs; saída e consumo integral | Processamento offline, não execução nativa |
| Há segunda implementação para comparar? | 537/537 hashes e comprimentos iguais ao LZSS do derplayer | Fontes externos isolados, compilados com .NET 10 |
| Onde ficam os quadros? | Em spritesheets e arquivos separados listados em TXT | Cinco divisões verificadas; nenhum timeline embutido nos YGAs do corpus |
| Quem controla duração? | Contadores e branches em classes nativas de efeitos | Ativação/explosão/moeda rastreadas; frequência real desconhecida |
| Há mecanismo reutilizado? | Registro global, máscaras, sort por +0x0C, update/draw virtuais | Funções comuns a dezenas de construtores |
| Relação com o duelo? | `00406B20` cria e aguarda objetos de ativação em estados específicos | Fluxo estático, não captura de sincronismo real |
| Como o resultado final chega à tela? | Parte de superfícies/DIB/raster conhecida | Backend final e cadência ainda não fechados |

## Artefatos e reprodução

- [Inventário](resource-inventory.md) e [CSV](resource-inventory.csv): 3.568 membros, dimensões e classificação.
- [Formatos](animation-formats.md): cabeçalho, compressão, divisões, sequências e contadores.
- [Arquitetura](rendering-architecture.md): loader, superfícies, ciclo de objetos e ponte com o duelo.
- [Ferramentas e comandos](../../../tools/visual/README.md): inventário, inspeção TXT, decoder e PNG opcional em `scratch/`.
- [Corpus](corpus-validation.json): hashes antes/depois e amostras reproduzíveis.
- [Comparação externa](upstream-comparison.json): commit e hashes dos três fontes usados.
- [Evidência Ghidra](evidence/): funções selecionadas, sem dump do executável.

Foram executados dez testes Python sintéticos: literais, sobreposição, retorno
circular, truncamento, excesso de bytes, limites de arquivo, geometria BMP/YGA,
modo bruto, parâmetro ausente, recorte horizontal e PNG/canais/CRC/orientação.
O corpus validou os 2.763 cabeçalhos BMP e decodificou os 537 YGAs. Os casos
sintéticos não são apresentados como execução nativa ou oráculo independente.
Uma exportação local do cursor Summon foi inspecionada; aparência legível não
substitui comparação visual com captura do jogo.

Ferramentas disponíveis: Python 3, .NET **10.0.113**, Ghidra Headless **12.2**,
Java OpenJDK **25.0.4.1**. Não foi necessária instalação. O projeto Ghidra
persistente recusou a abertura no ambiente restrito; uma cópia de cerca de 39 MiB
foi usada em `scratch/`, com uma única instância por vez, `-noanalysis -readOnly`.
Consultas com endereços inválidos de função foram corrigidas; não geraram evidência
usada como resultado. Nenhum helper nativo do jogo foi chamado.

## Conhecimento externo e contribuição local

O usuário indicou [YuGiOh-PoC-ModTools](https://github.com/derplayer/YuGiOh-PoC-ModTools/tree/b689dfbfc593916e7e20ea4873ce92ef81c9fc0d)
durante a investigação. Esse projeto já tinha cabeçalho e decoder YGA, LZSS e
mapas de valores visuais; o formato não é uma descoberta inédita da comunidade.
O trabalho local acrescenta ferramenta sem dependência de Windows, validação
estrita no corpus específico, comparação reproduzível e vínculo entre recursos,
controladores e despachantes no EXE de SHA fixado. Nenhum launcher/patcher externo
foi executado, nem suas dependências de hooks/Windows foram importadas.
A consulta aos fontes também identificou `CompressedSize=0` no escritor de modo
bruto, coberto pelo parser e teste sem extrapolar confirmação dinâmica.

## Lacunas e próximo experimento

1. Fechar wrapper de imagem e seleção de blend/alpha para um efeito, antes de
   implementar composição no raylib. A imagem PNG sozinha não determina o pass.
2. Rastrear cadência do chamador de `0045D040/0045D170`, incluindo pausas e
   caminhos que desenham várias vezes por update. Não inferir duração por nomes `ani`.
3. Confirmar em Windows um efeito de ativação e uma explosão: quantidade de chamadas,
   contador, chave de ordenação, slot, recurso e operação de desenho, sem alterar
   estado nem chamar helpers artificialmente. Exige captura natural ainda ausente.
4. Verificar recortes de Vija/moeda em assembly e capturas se for necessário um
   replay fiel: pseudocódigos extensos desses desenhos têm tipos/pilha incertos.
5. SUECAT permanece classificado com baixa prioridade; investigar xrefs somente se
   houver motivo para suspeitar de uso em runtime. A assinatura não prova um player.

Esta entrega fecha um marco de formatos e arquitetura parcial. Não declara
reprodução completa das animações nem equivalência visual dinâmica. A aplicação,
engine e hooks não receberam implementação desta investigação. As fontes
proprietárias e PNGs permanecem fora do Git, com hashes preservados.

## Integração solicitada durante a missão

`origin/main` em `b89f228` foi integrada sem conflitos pelo merge local `c717130`,
após salvar a investigação em `54fd13e`. A solução compilou sem avisos/erros e
passou em 1.051 verificações C#. Passaram ainda 87 testes dos hooks, 50 do
laboratório Python e o teste JS do loader. A suíte Python geral listou 176 testes,
com nove skips e dois erros de import por arquivos ausentes em
`experiments/hybrid_probe/`; ela **não está integralmente verde**.
Detalhes e comandos no [handoff](../handoffs/native-visual-yga.md).
