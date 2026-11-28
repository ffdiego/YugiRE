# Definição prática de pronto

> Status, manifesto e métricas de cartas. Carregue sob demanda.

## Definição prática de pronto

- **Pending:** sem regra implementada. **Partial:** primitivo ou integração parcial. **Done:** comportamento completo **no escopo anunciado** em Joey.Cards/Core, exercitável no duelo, sem exigir teste por carta. **Verified:** adicionalmente comparado com observação do Joey original. Um teste sintético não vira `Verified`. Headless isolado não prova UI/CPU: declarar o recorte testado e manter Partial quando a integração jogável necessária estiver incompleta; não promover cobertura por catálogo.
- Marque as limitações de escopo (por exemplo, ausência de correntes, efeitos globais ou interação com outras cartas); não declare todo o catálogo de monstros pronto apenas porque a invocação e o combate básicos funcionam.
- Prefira fatias menores que deixem o jogo melhor no fim da sessão. Um número grande de cartas listadas sem ativação jogável não é progresso funcional.
- A cada carta nova (inclusive procedimento opt-in incompleto, como Partial), atualizar `CARDS.md` e `docs/cards/playability-manifest.json` com resource ID exato, família, status, evidência de implementação e limitações. A badge CARDS e o relatório `docs/cards/PLAYABILITY.md` são gerados por `python scripts/publish_playability.py`. O workflow `playability-badges.yml` recalcula as três badges em pushes na `main`. Regenerar os arquivos afetados antes do commit para o CI; nunca editar manualmente os percentuais.
- Os 264 recursos classificados como `Normal` são `Done` apenas para a mecânica básica de invocação/set/tributos/batalha testada em C#. Isso **não** torna efeito, fusão ou ritual completos nem confere `Verified`. Se o DAT real divergir da classificação de referência, revisar o inventário antes de confiar na badge. Não declarar Done por catálogo/callback ou invocação genérica de monstros.
- Para medir porcentagem: `python scripts/card_playability.py --dat ../passion/data.dat`. Denominador é a união de packs (771 registros) ou Joey (350), não os 1.114 registros nem o número de callbacks. Sem DAT local, não inventar percentuais.
- Antes de reimplementar uma família, conferir o manifesto, o código atual e os relatos em [CARDS](../../docs/plans/CARDS.md) / [consolidação dos lotes 6–14](../../docs/research/card-rules-consolidation-lotes-6-14.md).
