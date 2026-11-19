# Estratégia de validação de cartas — decisão de novembro/2026

Desenvolver primeiro famílias reutilizáveis de efeitos em Joey.Core, com testes C#
determinísticos e CI de regressão por carta, sem exigir captura nativa para cada
efeito isolado. `Done (sandbox)` não significa paridade com Joey original.

Depois de termos variedade suficiente de efeitos que interagem (FLIP,
buscadores, recrutadores por batalha, magias, armadilhas e outros eventos),
montar decks controlados e realizar muitos duelos **IA vs IA no Joey
original** usando o laboratório YugiLab já existente.

Coletar localmente, por execução, fingerprint do EXE, decks, parâmetros
de RNG/seed, comandos/eventos nativos em JSONL, estados e deltas de
mão/deck/cemitério/campo/LP, marcadores e, quando disponível, replay YDR.
Os arquivos brutos permanecem sob `scratch/` (fora do Git).

Converter trechos úteis em fixtures diferenciais com:
- estado de entrada observado;
- ação ou evento observado;
- saída nativa com campos conhecidos e provenance;
- comparação contra Joey.Core: `Match`, `Mismatch` ou `Inconclusive`.

Replays YDR contêm comandos pós-resolução e não bastam para atestar causalidade,
efeitos ou decisões da IA. Polling de RAM não é atômico. Não classificar
ausência de observação ou empate de contagens como confirmação de regra.
Promover a `Verified` somente após captura diferencial suficientemente completa.

**Ainda não executar o corpus IA vs IA em massa.** O momento de começar será
decidido por diversidade das mecânicas, não apenas pelo total de cartas.
