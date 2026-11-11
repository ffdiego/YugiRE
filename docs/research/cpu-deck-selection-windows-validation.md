# Validação Windows — seleção de decks normais

Protocolo utilizado em **duas capturas dinâmicas Windows em 2026-11-10**:
uma sem MT e outra com snapshot MT. Ambas deram `ExitCode=0`, sem
divergências; detalhes e hashes em
[checkpoint](cpu-deck-selection-checkpoint.md).
Usar o EXE original com SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Iniciar manualmente; anexar debugger/probe a um processo existente.
Não usar `native_rules.py --spawn`, aplicar patches ou editar perfil/save.
Os duelos e opções usados normalmente pelo operador podem salvar progresso;
a instrumentação proposta somente observa.

Todos os endereços são VA da base `00400000`. O observador automatizado
recusa outra base: este EXE não tem relocations utilizáveis. Os callbacks
leem no thread interceptado, sem pausar globalmente os demais threads;
snapshots não são atômicos em relação a consumidores concorrentes do MT.

## Captura automatizada pronta — attach passivo

Executar do diretório raiz do repositório no Windows, com Python + Frida
(venv já existente pode ser reutilizado) e .NET 10 para a comparação:

```powershell
py -3 -m venv native_hooks/.venv-frida
.\native_hooks\.venv-frida\Scripts\python.exe -m pip install frida
```

1. Inicie **manualmente** o `joey_pc.exe` original e aguarde o menu,
   de preferência com backup atualizado do save.
2. Confira o PID com `Get-Process joey_pc`. Se houver mais de um, selecione
   explicitamente o processo desejado. Anexe sem opções de spawn:

```powershell
.\native_hooks\.venv-frida\Scripts\python.exe .\native_hooks\native_rules.py --observe-only
```

Sem `--pid`, o launcher identifica `joey_pc.exe` automaticamente
quando há uma única instância. Se houver mais de uma, use
`--pid N` explicitamente. Não precisa compilar/carregar a DLL nativa.
A abertura do observador exige SHA do arquivo apontado pelo módulo carregado;
`trace deck on` verifica também base, bytes exatos dos cinco pontos e
permissões das regiões de memória antes de instalar qualquer hook do deck.
EXE correto no disco com hook/patch incompatível em memória será recusado.

No console do laboratório, um comando por linha:

```text
trace on 20000
trace deck on mt
trace mark menu-1-estrela-pool-desligado
```

`mt` é opcional: `trace deck on` captura perfil, roll, caminho e source
sem ler as 624 palavras do gerador. Não habilite `trace demo`: os conjuntos
são mutuamente exclusivos. Overrides de seed/deck/speed/mutex são recusados
junto ao observador de decks. Não há escrita em memória de dados nem chamadas
nativas pelo novo módulo; Frida instala e remove seus próprios trampolins.

3. Na janela original, entre em um **novo Single Duel** pelo menu e espere
   aparecer o campo/mão. Não é necessário concluir o duelo ou conseguir uma
   carta específica. Anote estrelas e opção de cartas permitidas.
4. No console:

```text
trace deck off
trace off
trace dump
stop
```

`trace off` também desativa o observador de decks, caso pule a primeira linha.
`trace dump` informa o arquivo exclusivo salvo em
`scratch/traces/commands-<UTC>-pid<PID>.jsonl`. `stop` desanexa do processo
iniciado manualmente. A instrumentação não acessa saves; o jogo pode salvar
seu progresso durante ações normais da UI.

Compare, ainda da raiz do repositório:

```powershell
dotnet run --project .\src\tools\Joey.DeckTrace -- ".\scratch\traces\commands-<arquivo>.jsonl" "..\passion\data.dat"
$LASTEXITCODE
```

Use o caminho real do DAT original. O comparador verifica seu SHA, lê os YDC
sem extraí-los para disco e resolve IDs físicos pelo catálogo do DAT.
O relatório JSON lista seleções normais e problemas específicos:

| Exit code | Interpretação |
| --- | --- |
| 0 | Uma ou mais cadeias normais completas concordam em perfil→roll→caminho→source; MT também concorda quando capturado. |
| 1 | Divergência encontrada: caminho, site RNG, saída MT ou listas/count/ordem do source. Veja `Issues`; não declarar refutação se também houver lacunas. |
| 2 | Evidência insuficiente/incompatível: hook tardio, erro de leitura, overflow, cadeia ambígua, load falhou, Demo apenas, arquivo truncado ou fingerprint incorreto. |

Um `ExitCode=0` valida as escolhas registradas, não todas as dificuldades.
`NormalSelections[].MtChecked=false` significa que o MT não foi comparado.
Se um YDC externo sobrescrever o recurso do DAT sob o mesmo caminho, a
comparação do source apontará divergência; investigar a resolução do arquivo
antes de atribuir isso ao seletor. Não remover nem substituir arquivos para
este teste. Para novas capturas, iniciar `trace on` novamente e anexar o
JSONL inteiro, sem cortar a linha de metadata ou eventos de outra espécie.

### Correlação e limites

- `deck_selection`: enter (caller/thread/perfil/cena/MT opcional), rng
  (EAX uint32 e site), leave (string devolvida). `selectionId` identifica
  o sorteio em todos os estágios; `sceneId`/`loadId` identificam chamadas.
- `deck_scene`: caller conhecido + thread + ponteiro do char* + caminho
  vinculam a seleção externa à cena. O modo global na entrada do seletor
  pode ser da cena anterior; a classificação usa a cena solicitada.
- `deck_loader`: tipo e string por valor na entrada; retorno e leitura
  única de `E2` bytes do source1 na saída, antes de shuffle. O comparador
  normal exige retorno de chamada `0044C0E8`, tipo1 e resultado0.
- Demo7 tem seleção externa/interna e ordem de load invertida, preservadas
  por frames por thread; ele não conta como validação normal. Cena2 pode
  reutilizar o deck sem seleção: um trace só de rematch é inconclusivo.
- Eventos compartilham o buffer limitado do trace existente. `overwritten`
  ou sequências ausentes invalidam um resultado positivo. Erros de leitura
  são explícitos; não há substituição por arrays vazios ou RNG zero.
- MT é lido antes do sorteio, na entrada do seletor. Uma divergência isolada
  do MT também exige verificar consumo concorrente por outro thread.

### Validação local sem executar o Joey

```text
node native_hooks/tests/test_deck_trace.cjs
node native_hooks/tests/test_loader.cjs
node native_hooks/tests/test_demo_rng_seed.cjs
python -m unittest discover -s native_hooks/tests -p test_console_commands.py
python -m unittest discover -s native_hooks/tests -p test_deck_trace_preflight.py
dotnet build YugiRE.sln
dotnet run --project src/tests/Joey.Tests -- ../passion/data.dat ../passion/deck.ydc
```

Testes usam ABI/memória simuladas e fixtures sintéticas, além dos recursos
reais somente para leitura. O Joey não foi executado no Fedora; as duas execuções dinâmicas ocorreram
no Windows do operador após a correção dos probes internos de RNG.
Evidência da ABI: `docs/evidence/cpu-deck-observer-abi.txt`.


### Resultado real de 2026-11-10

O operador exportou duas capturas normais sem perda de eventos e verificou-as
no Windows, no DAT original: uma registrou 20 eventos e `RawRoll=1762370162`
sem snapshot MT; outra registrou 35 eventos e `RawRoll=538871612`
com `MtChecked=true`. Ambas escolheram `CPU_002.ydc`, perfil base1/pool0,
e as listas carregadas coincidiram em quantidade, ordem e índice com o DAT:
`main=43, second=0, third=4`. O JSON do comparador retornou
`ExitCode=0`, `Issues=[]` nas duas capturas. As capturas brutas não estão
versionadas; os hashes SHA256 estão no [checkpoint](cpu-deck-selection-checkpoint.md).

Os dois pontos de RNG são **instruções internas**. Depois de crashes Windows
`0xC0000005`, o observador passou a usar
`Interceptor.attach(site, function () { ... })` (probe de instrução), e não
`{onEnter(){...}}` (listener de entrada de função), nesses locais.
Os dois testes reais bem-sucedidos ocorreram após a mudança. Isso não é
promessa de estabilidade universal; para novos testes preserve backup e,
se houver crash, evite repetições e colete o contexto da exceção.

## A. Uma seleção normal inteira — prioridade máxima

1. No menu, registrar as estrelas estáveis e a opção de cartas permitidas.
2. Breakpoint em `0044BBE0` (`SelectCpuDeckResourcePath_0044BBE0`). Na entrada:
   - Retorno em `[ESP]` e thread do evento.
   - Bytes `A54DB1` inicializado, `A54DB2` anterior, `A54DB3` base,
     `A54DB4` pool; cena `005F3294` (DWORD).
   - Se desejar prever também o RNG: **624 DWORDs LE** de `005F25EC`
     (2496 bytes) e índice DWORD de `005F2FAC`. Não incluir o vtable
     em `005F25E8` como primeira palavra de estado.
   - Guardar o ponteiro de retorno da string em `[ESP+4]`.
3. Capturar `EAX` **antes da instrução** em `0044BDA5` no ramo CPU, ou
   `0044BC84` no FUL: é a saída MT uint32 antes da redução por módulo.
4. Ao retornar do seletor, ler o char* em `[objeto_string+4]`; o objeto
   é o ponteiro guardado em `[ESP+4]` na entrada. Registrar a string ASCII.
5. Em `0044A920`, filtrar a chamada cujo retorno é `0044C0E8`.
   Na entrada, `[ESP+4]` deve ser 1 (tipo CPU), `ECX` é o objeto do loader.
   Na saída em `0044C0E8`, `EAX==0` significa sucesso. Ler source1:
   - Main: índices internos uint16 a partir de `A54F42`.
   - Quantidade main: uint16 em `A5501E` (offset `DC`).
   - Para preservar tudo, dump de `E2` bytes a partir de `A54F42`.
6. Comparar o caminho com `CpuDeckSelection.Select(base,pool,roll)` e a
   lista com o YDC do DAT, resolvido por `DeckFile.Read(...).Resolve(0,catalog)`.
   Não comparar índices internos diretamente com IDs físicos do arquivo.

Com snapshot de MT, a previsão independente em C# é:

```csharp
var rng = NativeDeckRng.FromSnapshot(words, index);
uint predictedRoll = rng.Next32();
string path = CpuDeckSelection.Select(baseDifficulty, pool, predictedRoll);
```

**Falsificadores:** bytes/estado coerentes mas roll diferente; mais de uma
saída MT consumida pelo seletor; caminho fora da linha prevista; main que
não corresponde ao caminho em instalação original. Não contar como refutação
snapshot não atômico, recurso externo sobrescrito ou captura de Demo7.

Se UI estável mostra uma estrela e bytes são base1/pool0, a linha esperada é
CPU_000..004. Se CPU tem 43 cartas, CPU_002 é o único tamanho compatível nessa
linha, mas ainda exige comparação das cartas/caminho. Capturar os bytes é mais
forte que deduzir o pool pela instalação das outras versões do jogo.

## B. Pool, estrelas e escolhas repetidas

Quando já houver cartas de edições anteriores no perfil, observar a opção
normal da interface que altera pool. Registrar os bytes e UI antes/depois;
o setter em `0048AA10` escreve 1 quando o item recebido é 7, senão zero.
Sem alterar base, a interpretação estática prevê mudança de dois no nível
efetivo e troca CPU↔FUL. Não editar bytes para criar esse cenário.

Em novos duelos com a mesma configuração, cada chamada ao seletor deve
consumir uma saída do mesmo objeto global. Repetir deck é permitido; não há
regra de evitar o anterior. Não tentar validar uniformidade com poucos duelos:
um único roll e caminho já testa o lookup exato.

## C. Match e progresso

- Em modo match (cena1), observar uma transição de rodada pela cena2 em
  `0044BEE0`. Previsão: source1 preservado, nenhuma nova chamada ao seletor
  nem a `0044A920` nessa rota. Novo shuffle não significa deck novo.
- Em resultados naturais, capturar `A54DAF`, `A54DB0`, `DB2`, `DB3`, `DB4`,
  resultado bruto de `A577FA>>6`, modeword `A54E5C` e DWORD `005F3254`.
- Pausar em `00446F3B` antes do cálculo de dificuldade e em `00446FCE`
  antes da persistência. Previsão: limiares do relatório, streak saturado,
  mudança apenas com resultado agregado não zero e reset após mudança.
- Se o operador obtiver três vitórias consecutivas no nível1/pool0, a
  terceira deve levar base1→2. Derrotas do nível2 exigem três da classe
  não vitória para voltar. Um empate também pertence à classe não vitória
  no trecho nativo; a integração desse caso merece captura específica.
- Se houver ação normal da UI que acione comando40, observar `00408410`
  e o bit40 no modeword. Confirmar se é desistência/aborto e a exceção de
  progressão; não invocar o comando manualmente para forçar o resultado.

## D. Opcional: memória do estado antes de escolher

Não basta reusar a seed CRT do embaralhamento. O objeto MT também é
consumido por cartas/UI/efeitos. Para previsão exata entre capturas distantes,
é necessário preservar ou observar todos esses avanços. `0040F180` é um
desserializador virtual de MT, mas sua participação em caminhos de replay
ou outros contextos não foi demonstrada. Registrar eventual chamada é útil;
ausência em um duelo não prova que nunca seja usada.

Guardar: fingerprint, opção/estrelas, caller/thread, bytes do perfil,
roll uint32, caminho, main ou source dump, e instante preciso de cada captura.
Não são necessários logs de callbacks nem um duelo completo para o teste A.
