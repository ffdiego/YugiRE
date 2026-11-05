# Auditoria de IA/autoplay e limiar de ATK — 2026-11-05

Fonte primária: `ai-selection-score-offsets.txt`, 413 linhas de assembly/decompilação Ghidra para EXE SHA256 `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. As sugestões de laboratório vieram de uma revisão externa compartilhada pelo operador. **Análise estática; sem novo experimento no jogo.**

## Prova do gate de 900 ATK

Seja `S` o valor do `ESP` antes de `0x005B14C4`. As seis instruções PUSH até `0x005B14DA` deixam `ESP=S-0x18`. Portanto, em `0x005B14E1` a expressão `LEA EAX,[ESP+0x40]` produz `out=S+0x28`. O ponteiro é passado para `FUN_0056DD30` em `0x005B14E8`. Outros sete PUSH deixam `ESP=S-0x34` quando `0x005B1544` executa `MOV EAX,[ESP+0x60]`. Essa leitura usa `S+0x2C = out+4`, precisamente **ATK calculado**, conforme o struct de 12 bytes já estabelecido. `CMP EAX,0x384` e `JL 0x005B15B3` **rejeitam o candidato se o ATK calculado for inferior a 900**. É uma condição necessária, não suficiente para ser escolhido.

## FUN_00567620: classificador de cartas especiais

A função transforma o índice interno em ID de recurso com `FUN_004022E0` e compara uma whitelist de constantes; `resource=0x280` tem uma exceção dependente do segundo argumento. Para as chamadas `FUN_00567620(index,-1)` de `FUN_005AE220`, **Mystic Tomato** (índice 221, recurso `0x463`) e **Wall of Illusion** (índice 273, recurso `0x419`) retornam **false**. Também não são os recursos especiais `0x4DB`, `0x2DF`, `0x492` ou `0x585` do wrapper. Assim **`FUN_005AE220` retorna 0 para ambas** e não explica a recusa observada. `FUN_005AE3E0(side,2,resource)` também não os bloqueia no caminho padrão da extração anterior.

O caso real com ambos os passes retornando `-1` apesar de `summon check` permitir Mystic Tomato continua sem causa individual demonstrada. Agora priorizar: `FUN_005AAB90` com identidade mascarada, ATK efetivo na simulação, máscara de slot `DAT_00A579AC` e score/desempate.

## Avaliação das três ideias para o laboratório

**1. Auditar snapshot/restore antes de escalar — recomendação forte.** `FUN_005B08E0` e `FUN_005B0950` cobrem 5 registros de 0x90 bytes por lado (`0x2D0` cada), não necessariamente todas as globais. Capturar antes/depois *na mesma thread* ao redor de `ai check`, inicialmente apenas leitura. Primeiro comparar bytes das regiões de monstros `0x00A55D74` e `0x00A55D74+0xD44` (0x2D0 bytes cada); depois examinar separadamente máscaras/score `DAT_00A579AA/AC`, filas, flags, mão e RNG com baseline de controle. Relatar offsets modificados e hashes, repetir com estado equivalente. **Diferença não prova vazamento:** pode ser contador/global auxiliar esperado; igualdade não prova ausência de outros efeitos. Não restaurar memória à força. Caso as regiões de monstro sejam alteradas inexplicavelmente, bloquear testes automatizados de alto volume.

**2. Reaproveitar o escalonador original — melhor aposta arquitetural.** A IA nativa já selecionou e invocou Mystic Tomato no lado 1 com `ai step 1`, mas o protótipo só submete summon/set de classe 1–4, sem tributos, mágicas, ataques ou fim de turno. O `FUN_005BC700` e os chamadores da execução automática são melhores candidatos para turnos inteiros que reconstruir cada ação individualmente. Porém não há evidência de existir **um único bit 'lado controlado pela CPU'**: rastrear primeiro o dispatcher, dono do turno, tratamento de UI e branch de decisão. Não alterar um bit hipotético nem emitir `0x03` diretamente. Neste projeto `side=0` é CPU e `side=1` é humano; não existe `side=2` documentado.

**3. Testar a demo — barato, mas não garantido.** A demonstração começa após o menu ficar ocioso, mas pode ser IA×IA, replay ou roteiro. Observar chamadas passivas a `FUN_005B1360`, `FUN_005B16C0`, **`FUN_005B6FF0`** e ao produtor `FUN_005B9170`, com lado e sequência. O `trace ai on` atual **não** observa `FUN_005B6FF0` diretamente, exigindo hook adicional. A presença de executores/comandos isolados não prova origem em IA; comparar decisões e duas execuções completas.

## Prioridade sugerida

Fazer a **observação passiva do modo demo primeiro** (baixo risco). Antes de qualquer autoplay em lote, implementar o **audit pré/pós de estado** com resultados classificados. Depois investigar o escalonador da CPU e só então avaliar troca forçada de decks, aceleração e duelos IA×IA. Não ampliar automaticamente os comandos nativos até os pressupostos de isolamento e transição de turno serem validados.

Referências internas: [seleção e gates](ai-selection-rejection-gates-2026-11-05.md), [caso real com Mystic Tomato](ai-native-selection-rejected-legal-mystic-tomato-2026-11-05.md), [simulação/restore](ai-simulation-state-lifecycle-2026-11-04.md), [auditoria do autoplay](offline-ai-autoplay-audit-2026-11-04.md).