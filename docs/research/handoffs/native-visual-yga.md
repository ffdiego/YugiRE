# Handoff — recursos visuais, YGA e despacho nativo

Data: 2026-11-16. Trabalho pontual autorizado em `analise-visual`, sem assumir S/T
do roadmap nem editar PLANS/AGENTS. Objetivo: investigar animações e recursos offline,
com ferramentas de inspeção sem alterar a aplicação.

## Commits e sincronização

- `54fd13e`: parser/decoder, inventário de 3.568 membros, dez testes, quatro documentos e evidência nativa selecionada.
- `c717130`: integração solicitada pelo usuário de `origin/main` (`b89f228`), sem conflitos.
- Este handoff e a normalização dos hashes dos fontes externos ficam no commit que contém este arquivo.

Foi feito apenas fetch e merge local na branch `analise-visual`; nenhum push ou
alteração de ponteiro das outras branches. A integração trouxe mudanças da main
em Core/App/Lab; nenhum arquivo desses foi editado pela investigação visual.
A worktree estava limpa no início. Os novos arquivos pertencem a esta missão.

## Fatos novos e contribuição

- 537 YGAs: raster BGRA32; cabeçalho de 24 bytes; 537 streams LZSS internos consumidos integralmente.
- Inventário completo, inclusive BMPs de 1/8 bits fora do subconjunto do demo, manifestos, spritesheets e SUECAT ainda sem papel de runtime provado.
- Cinco divisões horizontais verificadas contra parâmetros dos manifestos de moeda/dado/Vija.
- Loader `00497360`, superfície `00496AB0`, destruição `00497550` e instruções dos offsets preservados.
- Contadores e seleção de estágios de ativação/explosão; registro global `005F3318`, máscaras, flag ativa e ordenação por `+0x0C` nos despachantes `0045D040/0045D170`.
- Ponte estática `005B9470 → 00406B20 → 00450260/00450730`, com estados que aguardam apresentação.

O usuário indicou `derplayer/YuGiOh-PoC-ModTools` durante o trabalho. A referência
já possui YGAHeader/YGAFile/LZSS: **não se trata de formato inédito na comunidade**.
Seu commit `b689dfbfc593916e7e20ea4873ce92ef81c9fc0d` foi fixado. Três classes LZSS
originais foram compiladas isoladamente e comparadas: **537/537 hashes e comprimentos
iguais**. Os Git blob hashes das cópias usadas conferem com a árvore remota;
SHA256 registrado em `visual/upstream-comparison.json`. A licença MIT permanece
junto às cópias ignoradas. Não executamos o launcher, patcher ou jogo.

## Validação realmente executada

Após o merge:

| Comando | Resultado |
|---|---|
| `python3 -m unittest discover -s tools/visual/tests -v` | 10 testes PASS |
| `python3 tools/visual/validate_corpus.py <DAT> --exe <EXE> --output scratch/visual/post-merge-validation.json` | 3.568 membros; 537 YGAs; cinco checks de divisão; hashes originais antes/depois iguais |
| Harness `tools/visual/upstream_compare.cs` com três fontes externos | 537 matches, 537 comprimentos exatos; sem execução nativa |
| `dotnet build YugiRE.sln --no-restore -m:1` | PASS; zero avisos/erros |
| `dotnet build src/Joey.Coverage/Joey.Coverage.csproj --no-restore -m:1` | PASS; zero avisos/erros |
| `dotnet run --project src/tests/Joey.Tests --no-build -- <DAT> <YDC>` | 1.051 verificações PASS |
| `python3 -m unittest discover -s native_hooks/tests` | 87 testes PASS, mocks offline |
| `python3 -m unittest discover -s yugilab/tests` | 50 testes PASS, mocks offline |
| `node native_hooks/tests/test_loader.cjs` | PASS |
| `python3 -m unittest discover -s tests` | 176 testes listados, dois erros de import e nove skips; **não PASS global** |
| `python3 -m py_compile ...`, `bash -n tools/visual/query.sh`, links locais e `git diff --check` | PASS |

O build inicial sem restore encontrou `NETSDK1004`: worktree nova sem assets.
Restore foi feito exclusivamente de cache/pasta de fonte local vazia, com auditoria
NuGet desabilitada para esse comando; nenhuma dependência baixada ou configuração
persistente alterada. O primeiro teste de Coverage precisava do binário; após
compilar, esse erro sumiu. Restam testes que importam
`experiments/hybrid_probe/compare_ydr.py` e `pe_preflight.py`, ausentes. Os dois testes
não mudaram entre `e0ae044` e o HEAD integrado e o diretório também não está na
árvore inicial. Não foram inventados substitutos nem alterados testes de outra trilha.

EXE: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
DAT: `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.

## Limites e continuidade

Nenhuma equivalência dinâmica foi alegada. Não houve janela do jogo, Wine,
captura Windows, patch de artefatos, hook, alteração da mão ou layout local.
PNG é inspeção do raster; não reproduz blend, transformações ou duração.
SUECAT e backend final de apresentação permanecem abertos.

Próxima unidade estática útil: rastrear wrapper de imagem e seleção de blend
para **um** efeito de ativação, a partir de `004505D0 → 00422C00`, sem implementar
na App. Depois medir a cadência e o estado de um efeito natural no Windows antes
de transformar contadores em tempo ou afirmar fidelidade visual.
Comando de partida, sobre a cópia descartável já existente:

```bash
bash tools/visual/query.sh "$VISUAL_GHIDRA" "$PWD/scratch/visual/ghidra" "$PWD/scratch/visual/activation-blend.txt" tempfunc:004505d0 tempfunc:00422c00 tempfunc:00422840 tempfunc:00422e30
```

Leia primeiro [findings.md](../visual/findings.md), [formatos](../visual/animation-formats.md)
e [arquitetura](../visual/rendering-architecture.md), evitando repetir o corpus ou
as consultas concluídas. Os dados extraídos/fontes externos/logs ficam em `scratch/`
e não devem ser commitados. A decisão de reproduzir efeitos na App continua fora
desta missão. Este turno entrega um marco verificável, sem prometer continuação automática.
