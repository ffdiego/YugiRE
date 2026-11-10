# Checkpoint — seleção de decks da CPU

## Objetivo atual
Instrumentação passiva em desenvolvimento: attach manual `--observe-only`,
`trace deck on [mt]`, correlação seletor→cena→loader e comparador offline.
ABI e bytes confirmados em `../evidence/cpu-deck-observer-abi.txt`.
Não usar spawn nem carregar DLL; validação Windows ainda pendente.

A seleção normal foi especificada e transcrita em C#. Próxima etapa útil:
confrontar perfil→roll→caminho→source em um duelo normal no Windows, conforme
[protocolo](cpu-deck-selection-windows-validation.md). Não repetir a análise
estática inicial. Trabalho na `main`; gráficos continuam fora desta etapa.

## Descobertas confirmadas
- Alvo Joey SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
  Endereços VA, base `00400000`. Não executar o original no Fedora.
- Seletor: **0044BBE0**, agora `SelectCpuDeckResourcePath_0044BBE0`.
  Perfil: base byte `A54DB3`, pool booleano `A54DB4`.
  Pool0: clamp(base,1,7), cinco opções CPU por nível.
  Pool1: clamp(base+2,3,9), opções FUL por nível: 3,7,6,7,6,6,6.
- Tabelas: CPU `005DB158` (linha0 inválida), FUL contagens `005DB080`,
  matriz `005DB0A8`. **21 CPU + 37 FUL = 58 recursos e 58 mains distintos**.
  Todos existem e são válidos no DAT fixado. CPU contém somente pack4;
  FUL inclui cartas das edições anteriores. Não é geração procedural.
- Uma escolha consome um uint32 do **MT global 005F25E8**, `0055C140`,
  reduzido por módulo. É separado do RNG CRT do shuffle.
  Construtor `0040F150`: índice625; primeiro Next semeia4357 por `0055C0E0`,
  inicialização antiga LCG69069 duas vezes/palavra. UI e outros caminhos
  consomem o mesmo estado: não recriar o gerador a cada duelo.
- Fonte normal: caller seleciona caminho → configuração `0044BEE0` cena0/1
  → loader `0044A920` tipo1, caller/retorno `0044C0E3/0044C0E8`
  → parser `0044A690` → source1 `A54F42` → `00569380` → shuffle `00568D80`.
- Recurso virtual `data/J/file/...` corresponde a `j\file\...` no DAT.
  Resolvedor tenta caminho externo antes do fallback em archives. Overrides
  podem alterar bytes sob o mesmo nome, não o algoritmo de seleção.
- Estrelas estáveis: base+2*pool, limitado a9 na apresentação. DB2 é base
  anterior; UI reconhece mudança copiando DB3→DB2. Pool pode habilitar no
  primeiro perfil pela posse de cartas de edições anteriores (`005BEDD0`)
  e ser alterado pela opção da UI em `0048AA10`.
- Progresso: `00446DF0`; DAF=classe anterior, DB0=streak saturado255,
  DB1=inicializado. Resultado1 aumenta; demais entram na classe não vitória.
  Gates: cena3 retorna; flag40 bypass; match incompleto adia mudança.
  Limiares extraídos de `005DAF40/005DAF64`, documentados no relatório.
- Rematch cena2 reutiliza composição; novos duelos0/1 sorteiam. Demo7 faz
  duas escolhas: externo é sorteado antes, mas carregado depois do interno.
- Presets Demo A/B/C correspondem exatamente a FUL_0GE1/0VAL/0UMI, a linha
  FUL3, nas três listas e na ordem. Arquivo [main,list1,list2] vira source
  [main,second=list2,third=list1] por `00449EF0`.
- Implementações isoladas: `CpuDeckSelection.cs`, `NativeDeckRng.cs`,
  `CpuDifficultyProgression.cs`. Não foram ligadas ao demo nem à IA.
- 12 funções nomeadas no banco persistente; relatório e script versionados.
  Original EXE conferido novamente e intacto. Não reimportar/reanalisar.

## Hipóteses e lacunas restantes
- Comando40 → `00408410` zera LP e seta0x240. Provável desistência/aborto,
  mas falta identificar a ação da UI em captura para fechar esse nome.
- `0040F180` desserializa MT por método virtual; consumidor indireto não
  identificado. Isso não altera a especificação do seletor dado seu estado.
- A captura normal de uma estrela relatada pelo operador não contém aqui
  perfil/roll/caminho correlacionados. Main43 é compatível com CPU_002 na
  linhaCPU1, mas tamanho sozinho não identifica o deck.
- Frequências reais e igualdade com snapshots de um processo Windows não
  foram medidas. Viés residual do módulo foi preservado, sem inventar pesos.

## Caminhos descartados
- Três presets Demo não limitam o catálogo normal; agora sua origem é FUL3.
- Seguir só loader ou shuffle não revela seleção: caminho vem antes.
- Não chamar DB3 sozinho de estrelas em ambos os pools; somar offset do pool.
- Não usar a seed CRT do shuffle para prever o MT de seleção.
- Não tratar linhas inválidas das tabelas como opções ou ponteiros válidos.
- Não inferir nomes modernos das duas listas adicionais apenas por tamanho.

## Próximos passos priorizados
1. Operador Windows: teste A do protocolo, processo manual + attach; observar
   `0044BBE0`, `0044BDA5` ou `0044BC84`, retorno do loader `0044C0E8`.
2. Comparar snapshot MT e uint32 com `NativeDeckRng.FromSnapshot`, e caminho
   com `CpuDeckSelection.Select`; conferir main pelos IDs do DAT.
3. Se houver divergência, seguir o primeiro elo divergente (perfil, RNG,
   caminho, resolução do recurso ou parser), sem repetir inventários.
4. Testar pool, rematch, progressão e comando40 nas condições do protocolo.
   Não usar spawn, editar saves, aplicar patches ou forçar comandos.
5. Somente depois integrar o modelo ao futuro motor/orquestrador, se solicitado.

## Última ação realizada
Build sem warnings/erros e **341 verificações C# passaram**, incluindo DAT,
58 decks, três presets completos, RNG e limiares. **4 testes Python novos
passaram**. Suíte Python geral:104 casos,2 erros de import legados,1 skip:
`tests/test_joey_compare_ydr.py` espera `experiments/hybrid_probe/compare_ydr.py`,
`tests/test_joey_hybrid_preflight.py` espera `experiments/hybrid_probe/pe_preflight.py`.
Arquivos foram removidos/movidos em trabalho anterior da main; não restaurados
ou alterados nesta etapa. Não confundir essa pendência com falha do seletor.
`RenameCpuDeckSelection.java` aplicou nomes com Save succeeded; consulta
`cpu-deck-alternate-scene-calls.txt` confirmou o símbolo persistente.
Protocolo Windows preparado; nenhum jogo foi executado nesta investigação.

## Commits relevantes
- `e1bd36c`: cadeia normal e checkpoint inicial.
- `5458908`: tabelas e recursos CPU/FUL; extrator reproduzível.
- `c560b9e`: seletor C# e MT legado, referência independente.
- `36399ce`: progressão, match, flag40 e consulta writers:VA.
- `716802c`: presets completos, pools e caminhos alternativos.
- `432b661`: nomes persistentes, mains distintos, limiares como oráculo.
- Fechamento documental: protocolo e checkpoint consolidado nesta versão.

## Bloqueios / preservação
Falta ambiente Windows para corroboração dinâmica; não bloqueia o modelo
estático já entregue. Nenhuma decisão do operador é necessária para entender
as tabelas. Preservar alterações locais anteriores em docs/15,
scripts/README, wrapper e evidências toolchain: não pertencem a esta pesquisa.
Não houve push dos commits desta etapa.

## Documentos de entrada
- [Especificação, endereços, tabelas e referências de evidência](cpu-deck-selection.md).
- [Experimentos Windows precisos](cpu-deck-selection-windows-validation.md).
- `../evidence/cpu-deck-selection-tables.json`: hashes, contagens e limiares.
- `../evidence/cpu-deck-ghidra-symbols.txt`: aplicação dos nomes; script Java
  reproduzível `scripts/RenameCpuDeckSelection.java`.
- `../evidence/joey-star-level-vs-demo-deck-selection-2026-11-09.md`: captura
  histórica, agora ligada aos resultados posteriores.

## Incremento — observador passivo
- `native_rules/deck_trace.js`: cinco hooks, perfil, EAX uint32, resultado
  std::string, source1 completo e MT opcional; IDs por sessão/thread.
- Correlação externa exige caller conhecido, thread, ponteiro e caminho;
  Demo interno é associado à cena ativa, sem confundir ordem dos dois loads.
- Host verifica SHA do EXE antes de autorizar o observador; runtime exige
  base fixa, bytes exatos e regiões legíveis/executáveis antes dos hooks.
- Mutadores Demo/speed/mutex são incompatíveis; observer não escreve nem
  chama funções nativas. Buffer/export JSONL existentes preservados.
- Testes Node simulam ABI, uint32, MT, correlação, thread, count inválido,
  preflight, rollback e stop; testes console Python incluem novos comandos.
- Próxima ação: implementar comparador C# usando CpuDeckSelection,
  NativeDeckRng.FromSnapshot e DeckFile/Catalog, depois protocolo Windows.
