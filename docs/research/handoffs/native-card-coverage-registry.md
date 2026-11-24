# Handoff — Card Coverage Registry, primeira entrega

2026-11-14. Branch `feature/card-coverage-registry`, worktree
`/home/diego/prj/joeyre/YugiRE-card-coverage`. Baseline da main:
`e0ae044bbd9994c33c66630e4a74f8c95ef15831`.
Commit do contrato, schema, exemplos e testes: `bba167f`.

## Entrega e decisões

- [Contrato técnico](../card-coverage-registry-contract.md).
- [Schema JSON v1](../../coverage/card-coverage.schema.json).
- [Cinco exemplos manuais](../../coverage/card-coverage.examples.json).
- [Testes de contrato](../../../tests/test_card_coverage_contract.py).

Identificação, compreensão, implementação e validação são avaliações
independentes, com Unknown por padrão e escopo explícito. Cartas se identificam
pelo índice original dentro do fingerprint; recursos, aliases, callbacks e
ocorrências não se confundem. A associação carta–mecânica tem cobertura própria.
Callbacks não determinam equivalência, normalidade ou ausência de efeitos.

Axe Raider mantém apenas match condicional do cálculo fechado de atributos;
Sparks e Legendary Sword têm implementação parcial; Witch tem observação sem
causalidade fechada; Zombyra mantém investigação aberta fora da tabela. Todas
as cinco cartas têm inventário Open e validação integral Unknown.

`research/esi-crash` foi consultada em
`a7de8c016af3d4c9c857b4be8c8bb6a6b1af9bfb`, sem merge nem cherry-pick.
Sua expansão de política normal não foi atribuída ao baseline da main. Os
relatos da captura v3 continuam condicionais e não são novas execuções aqui.

## Verificação executada

Na raiz do worktree:

```bash
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_card_coverage_contract.py' -v
PYTHONDONTWRITEBYTECODE=1 python3 -m unittest discover -s tests -p 'test_card_callback_catalog.py' -v
git diff --cached --check
sha256sum ../passion/joey_pc.exe ../passion/data.dat
```

Resultados: **14 testes de contrato e 8 testes existentes de callbacks
passaram**; sem erros de whitespace. Conferidos também os SHA256 dos 15 blobs
de evidência via `git show <commit>:<path>`, inclusive as duas referências
externas. Os testes permanentes requerem Git e o commit baseline, mas não
buscam a branch research na rede.

Hashes EXE/DAT antes e depois permanecem, respectivamente:
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de` e
`7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.
CARD_ID, nomes e propriedades foram decodificados em memória, com os hashes
esperados do contrato; os cinco pares índice/ID foram conferidos. Nenhum
recurso bruto ou descrição integral foi exportado.

Não executados: testes .NET, jogo/Windows, capturas nativas ou gerador de
relatórios. Esta entrega não modifica código de produção. Os resultados
históricos referenciados pelos exemplos são distintos destes 22 testes offline.

## Limites, riscos e aceite

O pacote `jsonschema` não estava instalado; a tentativa em venv temporário
falhou por indisponibilidade de DNS. Os testes usam um helper restrito às
keywords utilizadas, com rejeição de qualquer keyword não suportada. Isso
não é certificação genérica Draft 2020-12 nem implementação do leitor futuro.
Referências e critérios de promoção têm verificações semânticas separadas.

O schema não comprova causalidade, honestidade de atestados nem completude de
uma matriz. Escopos textuais exigem revisão. Hashes incompletos de capturas
históricas não foram preenchidos a partir de documentos ou fixtures.

Aceite: quatro eixos conservadores; identidades/fingerprints preservados;
cinco exemplos pinados sem validação integral indevida; gates para matriz,
contexto independente, testes, revisão e fechamento; contrato de relatórios
com ordenação, denominadores e serialização determinísticos; testes positivos
e negativos passando. Para validar uma carta, satisfazer todos os sete
critérios do contrato, incluindo todos os vínculos e dependências aplicáveis.

Ponto de parada: primeira entrega concluída. A implementação do gerador só
pertence a uma próxima tarefa. Não houve hooks, alteração de DLL/Core, merge
da pesquisa, push ou publicação de dados privados. O checkout original e seus
três arquivos não rastreados continuam preservados.
