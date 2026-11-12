# Investigação estática de ESI e proposta combat-context v3

Sol, 2026-11-12. Branch `research/esi-crash`, base `aeb3f92`.
Correção restrita: **`09b08a1`**, `fix(native): use instruction probes for interior combat listeners`.
Este relatório encerra a investigação estática; a confirmação dinâmica depende do Windows.

## Conclusão e limites

Há um defeito justificável no uso da API: os nove pontos internos usavam
`Interceptor.attach(address, {onEnter() {...}})`. No binding QuickJS inspecionado,
esse objeto cria um **listener de chamada**, com callback nativo de saída mesmo
sem `onLeave` JavaScript. Um probe de instrução é passado como **função direta**.
O primeiro formato pode substituir uma palavra da stack que não é endereço de
retorno. A correção muda somente esse formato nos pontos diagnósticos; mantém
o padrão v1 com dez entradas e o bloqueio dos nove pontos combinados no Windows.

Em ADD60, a palavra exposta é precisamente o ESI salvo da F40. Seu carregamento
posterior por `POP ESI` explica uma rota de corrupção coerente com o dump e com
o offset `+0x0B` de um trampoline de saída. Isso é a **hipótese principal**, não
prova da sequência executada pelo PID 23328. Faltam versão/runtime exatos do
Frida, bytes dos trampolines e observação da escrita na stack. Não há demonstração
de bug no relocator, nem validação Windows da correção. Contextos completos novos: **zero**.

## Fontes e reprodução offline

- **Captura observada anteriormente:** [incidente e análise do minidump](native-combat-context-v2-crash.md).
  Dump SHA256 `362f5f51a577783e4a64672e26c3fe0734a92a2c86f43905e6d22ab23f05476a`.
  O dump não foi reaberto nesta unidade; os registros e segmentos citados são
  evidência documentada, não uma nova extração.
- **Captura Windows auditada anteriormente:** [reteste v1](../../evidence/combat-context-v1-retest-2026-11-12.md),
  JSONL SHA256 `fc9daddea0116855ec2bc6b63a1966f645c294440fa76b1f2d2ec4b41fb1f77c`:
  seis F40 pareadas, 311 eventos, nenhuma perda/erro estrutural e duelo concluído.
  Isso apoia o isolamento dos pontos internos, sem identificar qual causou a falha.
- **Assembly novo:** GNU objdump `2.46.1-1.fc44` sobre o EXE local, SHA256
  recalculado `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- **Ghidra novo:** `../tools/ghidra_12.2_PUBLIC`, Java 25, projeto copiado de
  `/home/diego/prj/joeyre/yugire/ghidra-projects` para
  `/tmp/joey-esi-investigation/projects`. Consulta `-readOnly -noanalysis` com
  `QueryArchive.java`; transação do script revertida. O projeto persistente
  não foi aberto pelo headless. Listing/decompilação concordam com objdump.
  Relatório temporário `query.txt`, SHA256
  `2791fb69790139ae53933c232e3bd7772aaa1a90ed6eb9cfc8cbd00df4042624`.
  Nenhum dump de memória ou corpo completo decompilado é adicionado ao Git.
- O provedor REA respondeu `provider_unavailable/not_configured` (exige 12.1.x);
  nenhum alvo REA foi aberto. O resultado headless direto **não** é Evidence REA.

Comandos executados, além da cópia temporária do projeto:

```bash
sha256sum ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x5add60 --stop-address=0x5ade40 ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x5b0f40 --stop-address=0x5b1310 ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x56dd30 --stop-address=0x56dd80 ../passion/joey_pc.exe
XDG_CONFIG_HOME=/tmp/joey-esi-investigation/config \
GHIDRA_HEADLESS_JAVA_OPTIONS='-Dapplication.settingsdir=/tmp/joey-esi-investigation/settings -Dapplication.cachedir=/tmp/joey-esi-investigation/cache' \
../tools/ghidra_12.2_PUBLIC/support/analyzeHeadless \
  /tmp/joey-esi-investigation/projects Joey -process joey_pc.exe \
  -noanalysis -readOnly -max-cpu 2 -scriptPath "$PWD/scripts" \
  -postScript QueryArchive.java /tmp/joey-esi-investigation/query.txt \
  005ADD60 005ADDC0 005B0F40 ins:005ADD60:43 ins:005B0F40:26 ins:005B1184:12 \
  -log /tmp/joey-esi-investigation/headless.log
```

O headless retornou 0, reportou processamento read-only e produziu as três
decompilações e o listing solicitado. Nenhum jogo/helper do jogo foi executado.

## ADD60: ESI, stack e retornos

ABI: `ADD60(side, slot, outStats)`, palavras na stack, retorno simples.
Se `S` é ESP na entrada, `[S]` é o retorno real para `005B0F97`.

| VA | Assembly relevante | Consequência |
| --- | --- | --- |
| `005ADD60` | `mov edx,[esp+4]` | Side antes do save |
| `005ADD64` | `push esi` | ESP=S-4; **[ESP]=ESI da F40**; retorno real em [ESP+4] |
| `005ADD65..005ADD83` | máscara e aritmética | ESI vira `00A55D7A+(side&1)*D44+slot*90` |
| `005ADD8A` | `mov ax,[esi]` | Posição u16; EAX alto não é zerado |
| `005ADD8D` | `A8 02`: `test al,2` | Hook suspeito; ESI vivo aponta para posição, save contém offset rival |
| `005ADD8F` | `75 1B`: `jne 005ADDAC` | Visível: não força o bit |
| `005ADD91..005ADD93` | `or al,2`; `mov [esi],ax` | Oculto: força visibilidade temporariamente |
| `005ADD9D` | `call 0056DD30` | Caminho oculto; três argumentos, limpa 0xC depois |
| `005ADDA5..005ADDAB` | `and word [esi],FFFD`; `pop esi`; `ret` | Limpa bit temporário; restaura ESI de [S-4] |
| `005ADDB3..005ADDBC` | `call DD30`; `add esp,C`; `pop esi`; `ret` | Caminho visível também restaura ESI de [S-4] |

O corpo DD30 salva ESI em `0056DD39`; isso é evidência de save no prólogo,
não prova de integridade dinâmica de todos os seus caminhos/callees.
Se somente o ESI vivo fosse alterado pelo callback em ADD8D, haveria risco de
acesso errado dentro do wrapper, mas o `POP ESI` deveria recuperar o offset da
F40. A hipótese de escrita no **save** explica a persistência após o retorno.

ADDC0 salva ESI em `005ADDC6`, calcula o offset e o restaura já em `005ADDE6`,
antes do TEST de visibilidade em `005ADDE7`. O ramo oculto retorna em `005ADE2B`;
o revelado chama DD30 em `005ADE33` e retorna em `005ADE3B`. O retorno direto à
F40 é `005B0FAE`. Seus hooks internos também não estão em entradas de função.

## F40: vida completa de ESI

F40 salva o ESI de seu caller em `005B0F45`; EDI recebe side em `005B0F47`.
`005B0F4B..005B0F54` calcula `r=(-1-side)&1`. A sequência de LEA/SHL/SUB/ADD
`005B0F5C..005B0F6E` produz `ESI=r*0xD44` (3r → 27r → 54r → 53r →
848r → 849r → 3396r). Para side válido, esse offset é 0 ou D44.

| Instante | Estado/uso de ESI |
| --- | --- |
| `005B0F74` | Consome offset para postura rival: `[slot*90+esi+A55D7A]` |
| `005B0F92 → ADD60 → 005B0F97` | ESI precisa atravessar o wrapper preservado |
| `005B0FA9 → ADDC0 → 005B0FAE` | O wrapper salva/restaura o offset recebido |
| `005B0FB3..005B116A` | Chamadas naturais a 4022E0, 5BE050, D790, D8D0, conforme ramos; dependem da preservação pelo callee |
| `005B1195` | `8B 86 30 64 A5 00`: `mov eax,[esi+A56430]` |
| `005B12FE` | `pop esi`: restaura ESI do caller da F40, não o offset rival |
| `005B1308` | `ret` |

Após `005B0F6E`, o corpo original da F40 não atribui outro valor a ESI até
`005B12FE`. Há vários ramos que convergem em `005B1195`; não se pode escolher
um deles somente pelo EIP final. O dump documentado demonstra
`0871120F+A56430=0916763F`, exatamente o endereço READ inválido. Com EDI=0,
o valor necessário era D44. Isso prova o operando inválido, não seu produtor.

## API Frida, relocação e hipóteses concorrentes

No [binding QuickJS 17.0.0](https://github.com/frida/frida-gum/blob/17.0.0/bindings/gumjs/gumquickinterceptor.c),
`gumjs_interceptor_attach` distingue função direta (`JSProbeListener`) de objeto
(`JSCallListener`). As interfaces atribuem `on_leave=NULL` ao probe e um callback
nativo ao call listener. Isso continua presente no código `main` consultado.
Portanto, omitir o método JS `onLeave` não basta para obter semântica de probe.

No [núcleo Gum 17.0.0](https://github.com/frida/frida-gum/blob/17.0.0/gum/guminterceptor.c),
`gum_function_context_add_listener` deriva `has_on_leave_listener` da interface
nativa. `_gum_function_context_begin_invocation` arma o retorno e substitui
`*caller_ret_addr` por `on_leave_trampoline`. O callback JS retornar cedo não
impede essa escrita. Também há bookkeeping de invocações; este relatório não
simula sua interação completa com hooks de saída dos wrappers. Versões diferentes
podem tratar a stack de invocações de formas distintas.

O [backend x86 consultado em main](https://github.com/frida/frida-gum/blob/main/gum/backend-x86/guminterceptor-x86.c)
emite ponteiro de contexto, entrada, saída e trecho relocado. Condicionalmente
ao layout sem CET e ao JMP de cinco bytes, a saída fica 11 bytes após a entrada.
O [writer x86](https://github.com/frida/frida-gum/blob/main/gum/arch-x86/gumx86writer.c)
emite PUSH indireto de seis bytes e pode emitir JMP relativo de cinco.
Assim, entrada **inferida** `08711204` → saída candidata `0871120F`, valor do
ESI do dump. Não é desmontagem do trampoline original; versão, CET, layout e
ordem de alocação do incidente permanecem sem confirmação.

Há um risco separado de relocação: a janela `005ADD8D..005ADD92` cobre
TEST/JNE/OR, seis bytes inteiros para um redirect de pelo menos cinco.
O JNE original usa deslocamento +1B a partir de ADD91 e deve continuar atingindo
ADDAC; o caminho não tomado deve executar OR e continuar em ADD93. Copiar
`75 1B` literalmente para outra VA seria errado. O
[relocator x86 consultado em main](https://github.com/frida/frida-gum/blob/main/gum/arch-x86/gumx86relocator.c)
tem reescrita explícita para Jcc e destinos externos ao trecho movido. Logo,
a presença de JNE é um requisito de validação, não evidência de falha do relocator.
O preflight de bytes não valida destinos emitidos, flags, stack ou registradores.

| Hipótese | Evidência favorável e lacuna discriminante |
| --- | --- |
| Listener de chamada interno sobrescreveu ESI salvo | API e stack demonstram mecanismo; candidato on-leave coincide com ESI. Falta observar a escrita com o Frida exato. Prioridade alta. |
| Relocação errada do JNE | Há Jcc na janela, mas nenhum byte relocado preservado. Não explica sozinha por que o save seria um endereço de saída. |
| JS escreveu contexto/args/retval por acidente | O bloco interno inspecionado lê `this.context`; não atribui registros, argumentos ou retval. Escrita nativa indireta da API independe disso. |
| Outro ponto interno corrompeu stack/código | Todos usavam o mesmo formato. Em F40/0F7B, [ESP] é o ponteiro outStats já empilhado; em 119B, é EDI salvo após a limpeza dos argumentos. Há riscos adicionais. |
| Callee/engine, outra instrumentação, race ou memória prévia | Não excluídos por um minidump. O reteste v1 é um controle favorável, não reprodução do mesmo caminho/estado. |

Ordem de instalação não é ordem de execução: F40/0F7B precede a chamada ADD60.
Um hook instalado também afeta execuções fora do seletor e após o limite,
mesmo quando seu callback descarta o evento. Não atribuir o incidente inteiro
exclusivamente a ADD8D nem considerar seguro um ponto só porque ele não emitiu JSONL.

## Alternativa v3 mínima: fronteiras de função

Proposta, **não implementada como novo modo/schema nesta unidade**. Primeiro
estágio: cinco listeners de entrada/saída — seletores B1360/B16C0, F40,
ADD60 e ADDC0 — sem hook de instrução e sem DD30 adicional. Depois, se houver
necessidade concreta, incorporar as cinco entradas de helpers do v1 (dez no
total), uma por etapa. O modo atual `combat-context` permanece v1, sem renomear
amostras antigas ou declarar que a v3 já está disponível.

| Dado | Fronteira possível | Limite preservado |
| --- | --- | --- |
| Lados, slots, identidade e postura | enter F40 | Postura é amostra anterior ao consumo 0F74; não `Consumed` |
| Seed do descritor | enter F40, quatro bytes | Leitura anterior à instrução 1086; saída não prova seed consumida |
| Visibilidade própria/rival | enter/leave ADD60/ADDC0 | Amostra e restauração; não testemunho do ramo interno |
| Stats e estimativa oculta | leave ADD60/ADDC0 | Outputs nativos; não atributos calculados independentemente |
| Flags de jogador | enter/leave F40 e wrappers | Amostras limitadas; igualdade das pontas não exclui mudança intermediária |
| Predicados e recurso efetivo | enter/leave dos helpers naturais diretos, estágio posterior | Filtrar return VA/thread/frame; ausência permanece desconhecida |
| ESI diagnóstico | enter/leave dos wrappers diretos | Deve ser `((1-side)&1)*D44`; comparação read-only, sem reparar registro |

ESI em enter/leave **F40** pertence ao caller em ambos os instantes; não comparar
esse valor ao offset rival. Nos wrappers, registrar valor raw, ESP raw e caller
relocado, sem ler stacks ilimitadas. Mismatch invalida a avaliação e pede parada;
não mascarar o erro impondo ESI=D44. Validar argumentos antes de qualquer endereço.

Manter stacks por thread, geração/callId, filtragem dos retornos diretos F40,
limite de 64 avaliações e 24 filhos, rollback de instalação, zero chamadas
artificiais e nenhuma escrita no estado/retorno. Instalar/remover somente em
janela ociosa controlada; não tentar detach dentro de callback em voo.
Snapshots/saídas podem ser suficientes para diagnóstico parcial da fórmula.
Não substituem testemunhos de consumo ou manifesto independente de modificadores:
estes campos ficam `null`/`Unsupported`, e a avaliação permanece incompleta.

## Correção entregue e testes reais

`09b08a1` altera o segundo argumento dos pontos internos para `function () {...}`.
Não adiciona hooks, não muda endereços/assinaturas, não reativa os nove no Windows,
não muda o esquema padrão. Um teste modela explicitamente a escrita de retorno
do listener antigo (controle negativo); com os probes reais, preserva o save de
ESI e o retorno em stack sintética, para AL=0 e AL=2 e também fora de um frame.
O objeto de contexto é congelado. Isso testa a escolha da API e a ausência de
escrita no mock, **não executa TEST/JNE/OR, Gum, x86 ou instruções do Joey**.

| Comando efetivamente executado | Resultado |
| --- | --- |
| `python3 -m unittest discover -s native_hooks/tests` | **89 testes, OK**, incluindo preflight no PE local; sem skips reportados |
| `python3 -m unittest discover -s yugilab/tests` | **50 testes, OK**; workers e duelos dessas fixtures são mocks |
| `node native_hooks/tests/test_loader.cjs` | **PASS**, incluindo controle negativo, probe, limites, rollback, correlação e bloqueio Windows |
| `node native_hooks/tests/test_deck_trace.cjs` | **PASS** |
| `node native_hooks/tests/test_demo_rng_seed.cjs` | **PASS** |
| `node --check native_hooks/yugire_dual_loader.js` | **Exit 0** |
| `git diff --check` | **Exit 0** |

Mensagens de erro de argparse/exportação nas suítes Python são controles
negativos esperados; o resultado agregado foi OK. .NET/build C++ e Windows/Frida
não foram executados; não há mudança em Joey.Core/Joey.Lab nem necessidade de
atribuir validação dessas camadas a esta correção.

## Próximo experimento Windows e critérios de interrupção

**Primeiro, discriminar a hipótese em um alvo x86 sintético descartável, usando
o mesmo ambiente Frida do incidente, sem abrir o Joey.** O protocolo abaixo é
especificação do experimento; não há harness Windows novo validado neste commit.

1. Registrar `frida.__version__`, `Frida.version`, `Script.runtime`, arch,
   versão Windows e hash do agente. O host atual usa `create_script(source)`
   sem runtime explícito; não deduzir o runtime do incidente desse default.
2. Em uma função de fixture própria, salvar o ESI original; definir ESI=D44;
   fazer um segundo `push esi`; executar uma janela TEST/JNE/OR de seis bytes
   com destino fora dela; nos dois caminhos, `pop esi`, copiar esse ESI para
   uma saída própria, restaurar o ESI original e retornar. AL=0 e AL=2 exercitam
   os dois ramos. Não usar EXE/DAT ou funções do jogo. Antes de instrumentar,
   ambos os caminhos devem observar D44 e ESP balanceado.
3. Em processos separados, comparar três condições: sem hook; hook objeto
   `{onEnter(){}}`; hook função direta `function(){}`. Um hit por processo é
   suficiente inicialmente. Começar pelo probe; a condição objeto é o controle
   potencialmente corruptor, restrito à fixture. Não desreferenciar o ESI que
   sair da fixture. Conservar snapshots mínimos antes/depois e saída raw.
4. Usar breakpoints de hardware no alvo sintético para observar a escrita no
   endereço do segundo save de ESI e desmontar o trampoline de entrada/saída
   e a cópia relocalizada do JNE. Não adicionar outro Interceptor para medir
   esse save. Comparar a palavra alterada ao endereço **medido** de on-leave,
   sem extrapolar a regra de alocação 0x200. Repetir com wrapper de entrada/saída
   natural para revelar interferência do bookkeeping, se o teste mínimo for íntegro.
5. Resultado discriminante: somente o objeto altera o save para on-leave →
   mecanismo reproduzido naquela versão; probe preserva save mas erra ramo →
   investigar relocator separadamente; ambos íntegros → hipótese ainda pendente,
   revisar versão/runtime/layout em vez de forçar conclusão. Nenhum desses
   resultados reconstitui sozinho a execução histórica do PID 23328.

**Depois**, o controle funcional disponível agora no Joey é apenas v1 nesta
branch, uma sessão nova e um duelo natural a velocidade normal:

```powershell
# Em native_hooks; Joey original já aberto pelo operador, sem outros agentes:
.\.venv-frida\Scripts\python.exe .\patch_dual_rules.py --observe-only --pid <PID>
```

```text
trace on 20000
trace ai on combat-context
trace mark esi-entry-only-control
trace status
# Operador joga um duelo; NÃO digitar modos combat-context-point.
trace off
trace status
trace dump
stop
```

Exigir SHA atestado, dez listeners AI, `schemaVersion=1`,
`combatReadPointsArmed=[]`, pares F40/wrappers, seq contínua, overwritten=0,
read/correlation errors=0 e nenhum overflow. Esse controle revalida a branch,
não testa a correção dos probes, pois não os instala. A v3 com amostragem de ESI
exige implementação/revisão própria antes de seu piloto de cinco listeners;
com os gates satisfeitos, ampliar de um a no máximo três duelos, uma mudança
de cada vez. Não reativar a combinação v2 para reproduzir o incidente.

Interromper no primeiro crash, travamento, comportamento anormal, mismatch de
ESI/save/ESP na fixture, falha de assinatura/hash, perda, erro de leitura ou
correlação. Preservar privadamente JSONL/dump e hashes; em falha real, guardar
dump com páginas executáveis privadas dos trampolines, contexto e módulos
(`.ecxr`, `r`, `kv`, `!analyze -v`, bytes originais/relocados). Não continuar a
sessão nem trocar hooks após falha de integridade. Não publicar memória bruta.

Ponto de parada: a análise estática e a correção demonstrável da API terminaram.
Faltam medição Windows com o Frida exato e, para captura v3, implementação do
modo de fronteiras proposto. Nenhuma execução futura foi agendada.
