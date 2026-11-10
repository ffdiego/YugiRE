# Validação Windows — seleção de decks normais

Complemento dinâmico **ainda não executado nesta investigação**.
Usar o EXE original com SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Iniciar manualmente; anexar debugger/probe a um processo existente.
Não usar `native_rules.py --spawn`, aplicar patches ou editar perfil/save.
Os duelos e opções usados normalmente pelo operador podem salvar progresso;
a instrumentação proposta somente observa.

Todos os endereços são VA da base `00400000`; se a base observada diferir,
usar `base_real + (VA - 00400000)`. Capturas de vários endereços precisam
ser feitas com o processo pausado para evitar misturar atualizações.

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
