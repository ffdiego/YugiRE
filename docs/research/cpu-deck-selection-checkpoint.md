# Checkpoint — seleção de decks da CPU

## Objetivo atual
Observador passivo implementado; preparar a **primeira captura Windows** para
validar a especificação estática. Trabalhar somente na `main`. Não executar
Joey no Fedora, usar spawn, DLL, patches de jogo ou alterações de saves.
O [protocolo](cpu-deck-selection-windows-validation.md) contém comandos exatos.

## Descobertas confirmadas
- EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`;
  DAT `7e8841ceb015861dacf898d4a62a909e399da14bb7d69c40de3afa3a4610e1ed`.
  Endereços VA/base00400000; EXE sem relocations utilizáveis.
- `0044BBE0`: seletor. DB1..DB4 em `A54DB1`: inicializado, base anterior,
  base atual, pool. Pool0: clamp(base,1,7), cinco opções CPU por linha.
  Pool não zero: clamp(base+2*pool,3,9), opções FUL:3,7,6,7,6,6,6.
- CPU tabela005DB158; FUL contagens005DB080/matriz005DB0A8. São **58 YDC
  e 58 mains distintos**, todos válidos no DAT, sem geração procedural.
- MT005F25E8/Next0055C140 é compartilhado com UI e separado do CRT shuffle.
  Palavras005F25EC (624 DWORD), índice005F2FAC (0..625); inicialização
  legada/LCG69069 e lazy seed4357 reproduzidas em `NativeDeckRng`.
- ABI: hidden std::string output em `[ESP+4]`, objeto16 bytes com char*+4,
  length+8/capacity+12. EAX uint32 nos sites0044BDA5/0044BC84 antes do módulo.
- Seleção externa→cena0044BEE0(requested0/1)→loader0044A920(type1), retorno
  **0044C0E8**. Loader this=ECX, type=[ESP+4], std::string por valor=[ESP+8].
  Source1A54F42:226 bytes, arrays80/15/15, countsDC/DE/E0. Source listas
  [main,second,third] correspondem a grupos YDC[0,2,1].
- Modo global005F3294 na entrada do seletor pode ser da cena anterior.
  Correlação segura usa caller conhecido, thread, ponteiro e caminho na
  cena solicitada. Demo7 seleciona externo/interno, mas carrega interno
  primeiro; rematch2 reutiliza composição sem nova seleção/load.
- Resolver tenta recurso externo antes do DAT: conteúdo de um override
  pode divergir sob o mesmo caminho, sem refutar o algoritmo de seleção.
- Progresso00446DF0, limiares005DAF40/64, stars e gates documentados na
  [especificação](cpu-deck-selection.md). Não repetir investigação inicial.

## Implementação e evidências
- `native_rules/deck_trace.js`: cinco hooks observacionais, IDs selection/
  scene/load, perfil, roll, string, source1 e MT opcional. Sem write APIs,
  NativeFunction, alterações de args/retval ou acesso a saves.
- `native_rules.py --observe-only --pid N` anexa processo manual. `trace on
  20000`, `trace deck on [mt]`, novo duelo, `trace off`, `trace dump`.
- Host exige SHA do módulo carregado; módulo exige base fixa, bytes exatos
  e regiões válidas antes de hooks. Mutadores Demo/speed/mutex incompatíveis;
  falha de instalação desfaz hooks; falha de detach mantém handle para retry.
- JSONL usa buffer limitado existente. Overwrite/drain/truncamento, erro de
  leitura, associação ambígua ou etapas ausentes impedem resultado positivo.
- `CpuDeckTraceVerifier` + CLI `src/tools/Joey.DeckTrace`: compara seletor C#,
  MT opcional e todas as listas do DAT (IDs físicos→índices internos/ordem).
  Exit0=concordância capturada;1=divergência;2=incompleto/incompatível.
- [ABI e bytes obtidos no Ghidra Headless](../evidence/cpu-deck-observer-abi.txt).
- [Tabelas, recursos e limiares](../evidence/cpu-deck-selection-tables.json).
- [Nomes persistentes no Ghidra](../evidence/cpu-deck-ghidra-symbols.txt).

## Incidente Windows — crashes no observador (2026-11-10)

- Uma captura real por attach `--observe-only`, `trace on 20000`,
  `trace deck on mt` instalou cinco hooks (`deckActive=True`), mas o Joey
  sofreu acesso inválido `0xC0000005` ao iniciar o duelo. O problema não é
  o antigo pré-teste de região, que foi ultrapassado.
- Event Viewer registrou EIP `0048ADBD` (duas vezes; `MOV EAX,[ECX]`)
  e `00477F0B` (uma vez; `MOV EAX,[EDI]`). Consulta headless do operador
  `duel-crash-sites.txt` confirmou as instruções; são acessos indiretos
  posteriores aos hooks, **não** prova direta da causa.
- Revisão encontrou erro concreto de API Frida em `deck_trace.js`:
  `Interceptor.attach(site, {onEnter(){...}})` era usado em `0044BDA5`
  e `0044BC84`, que são instruções internas, não entradas de função.
  Frida documenta a forma de *instruction probe* com callback direto;
  um caso semelhante de corrupção de pilha foi relatado em
  https://github.com/frida/frida/issues/2944.
- Corrigido para `Interceptor.attach(site, function () {...})` sem onLeave,
  mantendo os listeners em entradas reais de funções. O teste de simulação
  agora exige essa distinção. **Não testado ainda no Windows**: tratar como
  hipótese corretiva, não correção validada.
- Não repetir duelos com a versão anterior. Após pull e testes locais,
  testar apenas com backup do save e processo manual; se o crash persistir,
  suspender os probes internos e reduzir para hooks de entrada/saída
  previamente conhecidos, isolando a origem com dump/registradores.

## Hipóteses / questões abertas
- Nenhum JSONL dinâmico do novo observador foi capturado no Windows ainda.
  Snapshot MT deve prever roll; concorrência entre threads pode interferir.
- Captura histórica de uma estrela/main43 é compatível com CPU_002, mas
  sem perfil+roll+caminho não demonstra a cadeia completa.
- Comando40 provavelmente desistência/aborto; ação da UI não confirmada.
- Desserialização MT0040F180 existe, consumidor indireto ainda não identificado.
  Não impede validar seleção dado snapshot/perfil.

## Caminhos descartados
- Presets Demo A/B/C não são catálogo normal; equivalem a FUL_0GE1/0VAL/0UMI.
- Loader/shuffle, tamanho43 ou stars sem pool não identificam escolha.
- Seed CRT não prevê MT; não reinicializar MT por duelo.
- Não ligar cenas só pelo nome do recurso: Demo pode repetir caminhos e
  inverter ordem de carga. Não usar modo global antigo para classificar.
- Não inferir semântica moderna de listas adicionais só pelo tamanho.

## Próximos passos priorizados
1. Windows: seguir protocolo automatizado no menu, habilitar deck trace
   antes de um novo Single Duel, exportar JSONL inteiro e rodar comparador.
2. Se incompleto, seguir `Issues`: fingerprint/bytes, hook tardio, caller
   não mapeado, leitura ou perda de buffer. Não tratar como refutação.
3. Se divergente, investigar primeiro elo: perfil/site/roll/MT/caminho ou
   recurso externo/parser. Preservar logs e instalação original.
4. Capturar pool ligado/desligado, várias dificuldades e modo match; rematch
   sozinho deve ser inconclusivo para nova seleção. Não editar perfil.
5. Validar comando40/progresso depois, pelos experimentos manuais do protocolo.

## Última ação / validação
Build solução sem warnings/erros. **437 checks C# passaram**, incluindo
76 escolhas sintéticas cobrindo58 recursos reais do DAT e77 novos checks de
integração além dos19 casos negativos/MT iniciais. Fixtures sintéticas não
são evidência dinâmica. CLI testada separadamente com DAT real e duas cadeias
sintéticas: exit0; roll alterado:exit1; arquivo truncado:exit2.
Node: módulo de ABI, loader/RPC e Demo RNG passaram; inclui threads,
reentrância, Demo com mesmo caminho e loads invertidos, rollback/teardown.
Console Python:33 testes passaram. Teste PE real:5 assinaturas conferidas.
Suíte native_rules completa:79 testes,
1 erro legado `test_capture_rotation` exige detach antes de kill; reproduzido
com fonte anterior `e9d0a4b`, sem executar nenhum processo do jogo.
Suíte Python raiz já tinha2 imports legados (compare_ydr e pe_preflight em
experiments/hybrid_probe), registrados na investigação estática anterior.
Último incremento endurece diagnóstico de contagens e eventos órfãos; build
e os437 checks foram repetidos com sucesso após a mudança. Originais EXE/DAT mantêm SHA acima.

## Commits relevantes
- `5222a50`: observador passivo, ABI, console e primeiras simulações.
- `2e14eab`: comparador C#/CLI e testes de divergências/incompletude/MT.
- `89e1a6f`: Demo/reentrância/detach retry, correlação e cobertura58 DAT.
- Base estática: `e1bd36c`, `5458908`, `c560b9e`, `36399ce`, `716802c`,
  `432b661`; consolidação `e9d0a4b` já estava no origin/main.
- Novos commits desta tarefa ainda não enviados ao remoto.

## Bloqueios / preservação
Não há mais validação dinâmica factível neste Fedora sem contrariar o escopo;
próxima evidência precisa do operador Windows. Instrumentação está pronta
para esse teste, não declarada validada no jogo. Banco existente preservado,
sem reimport/reanalysis. Alterações locais anteriores docs/15, scripts/README,
wrapper e evidências toolchain não pertencem à tarefa e não foram commitadas.
