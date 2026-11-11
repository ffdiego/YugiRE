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
  agora exige essa distinção. **Validado depois em duas capturas Windows
  sem crash**, uma com snapshot MT, mas não estabelecido como correção
  universal de qualquer falha futura.
- Não repetir duelos com a versão anterior. Manter backup de save e
  processo manual para novas capturas; se o crash reaparecer, isolar probes
  e recolher dump/registradores.

## Validação dinâmica Windows concluída — 2026-11-10

**Status: duas cadeias normais completas observadas, verificadas contra o DAT e
reproduzidas pelo C#.** O problema de crash não voltou nas duas capturas após
a correção do tipo de callback dos probes internos (uma execução sem MT, outra
com MT). Isso é evidência favorável para a correção, **não** demonstra ausência
de crashes em outros fluxos nem cobertura de todos os níveis/pools.

Ambas foram capturadas após abrir o Joey manualmente, com
`native_rules.py --observe-only`, sem carregar DLL ou substituir callbacks.
O EXE observado tem SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`
e o comparador usou `../passion/data.dat` com fingerprint já fixado pelo
projeto. Os **JSONL brutos não são versionados**: permanecem com o operador
Windows; abaixo, identificadores, hashes e resumo verificável.

| Captura local | Eventos | Roll nativo | MT | Escolha | Verificador |
|---|---:|---:|---|---|---|
| `commands-20261110-044529-224808-pid10332.jsonl` | 20/20, 0 perdas | 1762370162 | não | CPU_002.ydc | ExitCode 0, Issues [] |
| `commands-20261110-045059-240787-pid9816.jsonl` | 35/35, 0 perdas | 538871612 | 624 palavras, índice 624 | CPU_002.ydc | ExitCode 0, Issues [], MtChecked true |

Hashes SHA256 dos arquivos JSONL, para conferir o material original sem o
publicar (não são fingerprints do EXE ou DAT):

- Sem MT: `e1fc32f4b16654165a5c4c7de35c74d4e3f6570b87e8a9fa8cf80c05911959f6`.
- Com MT: `9a4bcf283062d83f78dee4cae243e164a2671c3f8423f7b95780be16d861e6c6`.

Em ambos: perfil `initialized=1`, `baseDifficulty=1`, `pool=0`, cena
solicitada `0`, site RNG `0x0044BDA5`. A função retorna
`data/J/file/CPU_002.ydc`: os dois rolls dão resto 2 na divisão sem sinal
por 5. O loader normal (`caller=0x0044C0E8`, tipo 1) retorna zero e o source
tem 43 índices no main, 0 no second, 4 no third, todos **na ordem** esperada
pela resolução do DAT. Há ligação inequívoca entre seleção, cena e loader
por thread, caller, ponteiro e caminho. O jogador carrega `deck.ydc`
separadamente (tipo 0).

A segunda captura inclui `mtBefore` (624 uint32, índice 624). O verificador
`CpuDeckTraceVerifier` construiu `NativeDeckRng.FromSnapshot` e
`Next32()` produziu a mesma saída observada, 538871612.
O console Windows registrou:

```json
{
  "ExitCode": 0,
  "NormalSelections": [
    {
      "SelectionId": "1:selection:1",
      "ResourcePath": "data/J/file/CPU_002.ydc",
      "RawRoll": 538871612,
      "MainCount": 43,
      "MtChecked": true
    }
  ],
  "Issues": []
}
```

**Escopo rigoroso:** duas seleções de cena0, base1/pool0 e o mesmo recurso;
uma só previsão de MT contra o jogo; não provar pools FUL, demais
dificuldades, rematch, progressão, distribuição estatística ou todos os
caminhos de carregamento. Também não chamar dois sucessos de prova
universal da estabilidade do Frida.

## Hipóteses / questões abertas
- Agora há duas validações dinâmicas em Windows, sendo uma com MT; isso
  não elimina a ressalva geral sobre snapshots concorrentes entre threads.
- O antigo indício por tamanho43 foi superado pelas capturas novas com
  perfil, roll, caminho e composição na ordem exata.
- Comando40 provavelmente desistência/aborto; ação da UI não confirmada.
- Desserialização MT0040F180 existe, consumidor indireto ainda não identificado.
  Não impede validar seleção dado snapshot/perfil.

## Seguimento do laboratório — LP e seleção da IA (2026-11-11 UTC)

Captura real de um duelo completo:
`commands-20261111-022940-355228-pid36348.jsonl`,
SHA256 `dd2029e8397d22401b7790ce3f007c9353cef94d5e6a9c14dbe1690d20206a90`.
Foram **1.120 eventos íntegros**, 29 leituras reais de LP,
14/14 danos `0x0043` conferidos com a leitura do comando seguinte,
CPU terminando com LP=0 e jogador com LP=300.
A CPU executou 23 seletores (13 normal, 10 priority) e
**12/12** comandos `0x00CE` do lado CPU tiveram `source`
igual à carta escolhida pelo último seletor válido anterior.
Essas correspondências **não provam** que cada ação se resolveu
nem explicam a pontuação de candidatos.

O novo trace passivo de LP está, portanto, **validado em um duelo
real**; próxima etapa é investigar os critérios de
`select_normal`/`select_priority`, com estado e escolha concretos.
[Evidência evento a evento](../evidence/normal-duel-lp-ai-live-validation-2026-11-11.md).

## Caminhos descartados
- Presets Demo A/B/C não são catálogo normal; equivalem a FUL_0GE1/0VAL/0UMI.
- Loader/shuffle, tamanho43 ou stars sem pool não identificam escolha.
- Seed CRT não prevê MT; não reinicializar MT por duelo.
- Não ligar cenas só pelo nome do recurso: Demo pode repetir caminhos e
  inverter ordem de carga. Não usar modo global antigo para classificar.
- Não inferir semântica moderna de listas adicionais só pelo tamanho.

## Nova evidência de IA/LP (2026-11-10)

O operador capturou um duelo normal inteiro em
`commands-20261111-015433-637065-pid41940.jsonl` (SHA256
`b24e67fccbb79e23df446eb3f36e24c3d57107003691e6ae4ea89561f260bba7`):
1.583 eventos íntegros, com probes IA, 496 comandos e um terminal
`0x8004`. O jogador informou vitória. Nove comandos `0x0043`
permitem reconstruir uma curva hipotética de dano consistente com
CPU chegando a zero, mas **não eram leituras reais de LP**.

Para fechar essa lacuna, o hook existente `trace on` agora mede LP
uint16 em `A55D64 + side*0xD44` quando a primeira amostra aparece,
os LP mudam, há comando `0x0043` ou terminal `0x0004`.
A leitura é no produtor **antes da fila**, e a alteração pode
aparecer somente no próximo comando. Sem novo hook/patch, sem dados
escritos. O teste simulado do agente verificou transições de LP,
encerramento, reset e falhas de leitura; **validação Windows pendente**.
[Detalhes e protocolo](duel-life-points-capture.md).

## Próximos passos priorizados — retomada

1. **Próximo foco: IA jogando duelos normais.** Em sessão *separada* do
   observador de decks, anexar `--observe-only`, executar `trace on 20000`,
   `trace ai on`, `trace mark inicio-investigacao-ia`, observar dois ou três
   turnos da CPU, depois `trace ai off`, `trace off`, `trace dump`, `stop`.
   Validar estabilidade desses hooks no Windows; não ativar comandos mutadores.
   Cruzar eventos de candidatos/decisão com comandos emitidos e registrar
   quais observações ainda são hipotéticas.
2. Objetivo da próxima etapa: explicar **uma decisão concreta da CPU**, desde
   o estado conhecido até a ação realmente enfileirada, antes de implementar
   um seletor de ações em C# ou acelerar partidas.
3. Posteriormente expandir a seleção de decks para base/pool distintos,
   FUL, rematch/match, progressão e modo40, com capturas próprias. Não editar
   o perfil nem confundir ausência de nova seleção em rematch com erro.
4. Persistindo divergências futuras, analisar primeiro perfil/site/roll/MT,
   caminho e resolução do recurso; preservar `Issues` e JSONL íntegros.

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
- `2e62e66`, `64e6ee5`: validação de intervalos legíveis atravessando
  regiões de memória e regressão correspondente.
- `e7668d4`, `57bb834`: troca dos probes de RNG no interior da função
  para callbacks diretos e proteção por teste; observados em duas capturas
  Windows sem crash.
- `8f39ccd`: diagnóstico inicial dos crashes Windows; este checkpoint
  complementa-o com o resultado das capturas reais.

## Bloqueios / preservação
A validação dinâmica do seletor foi feita pelo operador no Windows.
No Fedora, testes e consultas estáticas não substituem execução do Joey.
Banco Ghidra existente preservado, sem reimport/reanalysis. Nenhum save,
DAT, EXE ou JSONL bruto é publicado neste checkpoint; observar também
que a investigação da IA ainda está pendente.
