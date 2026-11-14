# Gerador de inventário v1

Requer .NET 10 SDK, Python 3 e Git com o commit de fontes
`7e5ca132f7185bd2c3df4c9782e080fb95a135f5` disponível. Não consulta rede,
executa o jogo ou importa anotações semânticas. O baseline fixa o Core auditado;
o snapshot não atribui implementação ao baseline. Nenhuma mudança no Core.

Na raiz do worktree:

```bash
dotnet restore src/Joey.Coverage
dotnet build src/Joey.Coverage --no-restore
python3 scripts/generate_card_coverage.py --dat ../passion/data.dat --exe ../passion/joey_pc.exe --output /tmp/yugire-coverage
python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -v
YUGIRE_COVERAGE_DAT=../passion/data.dat YUGIRE_COVERAGE_EXE=../passion/joey_pc.exe python3 -m unittest discover -s tests -p 'test_card_coverage*.py' -v
```

Restore não tem pacotes externos; após o build, o gerador usa `--no-build`.
Recompile após qualquer alteração da ponte C#. Fontes Core diferentes do commit
pinado são rejeitadas. Para outro baseline/fingerprint, revisar fontes e atualizar
o pin explicitamente. O EXE é aberto apenas para SHA256, comparado ao catálogo
estático pinado. Os hashes de ID/nomes/propriedades também precisam coincidir.

Saídas UTF-8/LF, sem BOM, caminhos de entrada ou horário:

- `inventory.json`: snapshot normativo conforme schema v1; todas as linhas não
  sentinela de `Catalog.Load`, callbacks completos inclusive órfãos, aliases
  numéricos e do helper, ledger imutável. Quatro eixos explicitamente Unknown,
  inventários Open, zero mecânicas/vínculos. Busca negativa na tabela significa
  somente AbsentInTable.
- `attributes.json`: formato auxiliar separado, pois v1 proíbe atributos extras.
  `cardRef` liga propriedades brutas/derivadas e uint16 card_pack às identidades
  do snapshot. Inclui SHA256 de card_pack, fingerprints, refs ao ledger e resumo.
  Bits 1/2/4 registram disponibilidade Yugi/Kaiba/Joey; bits restantes são
  preservados no uint16. O resumo mantém denominadores separados.
- `inventory.csv`: projeção com colunas fixas, nomes escapados pelo módulo csv,
  disponibilidade explícita true/false, quatro estados Unknown. Ponteiros null
  explícitos permanecem no JSON normativo, não são projetados no CSV.

O relatório Markdown fica para a terceira entrega. Não versionar saídas reais
em massa; somente os golden sintéticos entram no repositório. As descrições
consumidas por Catalog.Load nunca saem do processo C#.

A biblioteca padrão Python `jsonschema` é apropriada como verificação adicional
Draft 2020-12. `--standard-schema` usa Draft202012Validator quando previamente
instalado; sem o pacote a opção falha, sem instalar ou buscar nada. Não foi
possível exercitar essa opção neste ambiente (pacote ausente). A validação padrão
reutiliza o verificador restrito da primeira entrega, agora em
`scripts/coverage/validation.py`: rejeita keywords desconhecidas e verifica
referências, denominadores e gates de proveniência. Não é um validador genérico.
Nenhuma biblioteca estrutural substitui a revisão científica das evidências.

Os testes golden comparam bytes e SHA256, incluindo nome vazio, acento, vírgula,
aspas e newline. Testes sintéticos preservam sentinelas, índices descontínuos,
ID zero, duplicatas e variantes +2000; rejeitam entradas inválidas e referências
soltas. O teste local opcional gera duas vezes e compara todos os bytes.
