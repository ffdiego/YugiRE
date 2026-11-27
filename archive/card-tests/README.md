# Histórico de testes de cartas

Baseline integral: [295d0eb5](https://github.com/ffdiego/YugiRE/tree/295d0eb5fe3df2791851a426800bc8215ca55430/src/tests/Joey.Tests). Consultar somente por divergência concreta; não manter compatibilidade por obrigação.

Primeira fatia: chamadas individuais desativadas por padrão (opt-in local temporário YUGIRE_HISTORICAL_CARD_TESTS=1); fontes ainda compilam até separação física. Próxima fatia: retirar fontes históricas da compilação e runner, inclusive comparadores embutidos, preservando infraestrutura, scheduler, visual e cenários úteis. CI não deve ativar a variável.

A primeira extração de arquivos moveu DirectDamageChecks, LifePointSpellChecks e DrawSpellChecks para esta pasta, fora dos projetos ativos. Os demais casos permanecem desativados no runner até separar dependências.
