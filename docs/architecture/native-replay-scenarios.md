# A05.1 — Cenários a partir de YDR

O formato existente [YDR](../29-replay-ydr.md) guarda três listas de resource IDs por lado e registros de quatro words. Não é por si só uma lista de decisões legais. Um replay de operações resolvidas é oráculo para A05, mas não prova a regra que o produziu.

`scripts/export_joey_replay_scenario.py` reusa o leitor existente e gera **o mesmo roteiro textual do console A04**, com índice/offset/opcode nativo e fingerprint do arquivo, sem identificador do save. Não cria novo formato de cenário. Modo estrito: qualquer registro não suportado aborta o intervalo inteiro, antes de abrir o arquivo de saída; não sobrescreve arquivos. O estado anterior ao intervalo é fornecido em um roteiro de checkpoint; não afirmar que o checkpoint coincide com a memória nativa. Executar em duelo vazio com DAT. Exemplo:

```sh
python scripts/export_joey_replay_scenario.py scratch/replays/duel.ydr --start 1060 --count 1 --checkpoint scratch/replays/before-1060.txt --output scratch/replays/1060-console.txt
dotnet run --project src/Joey.Lab -- duel-console --dat /path/data.dat scratch/replays/1060-console.txt
```

O exemplo 1060 refere-se à amostra Relinquished já documentada; o checkpoint é privado, não fornecido pelo YDR. Comandos draw e LP são a primeira fatia. Opcode 0x61: docs/21-native-card-draw.md, 005B9F50/00578860/00571E30; somente (0,5,0) e (1,1,0). 0x43: 005784E0; 0x42: 005787F0, magnitude >0, args (magnitude,0,1), bit15 lado. SQLite privado quick_check OK; 00408470 confirma sinal por variante e flag final. Rejeitar variantes com bit12–14, p3=0, zero e quaisquer outros opcodes. Não inferir terminal nativo em deck-out simultâneo/curto. Para início, `--opening-order scratch/order.json --start 2 --count 2` valida prefixo 14/10 e dois draws de cinco; exige ordem **observada independentemente após shuffle**, arrays resource ID por lado {"0":[...],"1":[...]}, inventário exato com multiplicidade, listas 1/2 vazias. Não confundir ordem serializada com ordem embaralhada; seed/cabeçalho e fusão ainda precisam ser confirmados. Primeira amostra com Fusion Deck não é suportada no modo opening-order.

## Próximos passos

1. Obter YDR e captura correlacionada antes de uma operação de interesse (estado completo, posição das ocorrências, decks em ordem, LP, turno/fase, flags/links/decisões; não publicar bytes privados). Identificar serialização de seed e listas no carregador nativo. Ver docs/evidence/joey-native-shuffle-matching-2026-11-08.md: reconstrução high-mode confirmada com seed e palavras de deck observadas, mas não prova que o cabeçalho YDR forneça essas entradas.
2. Expandir projeção CE/CF/8E/79 somente com decodificação por variante e identidade; distinguir movimento gravado de decisão de jogador. Não aplicar snapshot ou comandos resolvidos como prova de paridade das regras.
3. Validar o mesmo roteiro headless e no App, relacionando os registros nativos aos comandos C# e suas continuidades. A05 fica parcial até comparador e critérios em PLANS.

Testes Python sintéticos incluem flags, limites, duplicatas, privacidade, rejeição de opcode e saída atômica quanto à validação. CI pendente. Nenhum YDR privado versionado.
