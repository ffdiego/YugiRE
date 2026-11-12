# Contrato de observação de FUN_005B0F40 — v1

Trilha S, 2026-11-11. Contrato estático para um piloto; **não validado no
Windows**. Não autoriza executar helpers artificialmente. Desconhecido é
`null`/`Incomplete`/`Unsupported`; ausência de chamada não é retorno zero.

## Identidade e evidência

EXE `joey_pc.exe`, PE32 x86, base preferida `00400000`, SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Digest recalculado no artefato local com `sha256sum ../passion/joey_pc.exe`.
VA neste documento deve ser relocada por `module.base + VA - 00400000`.
O hash da captura precisa de atestado próprio; o hash deste documento não o substitui.

Fontes primárias versionadas: [corpo decompilado](../evidence/ai-decision-combat-deep.txt)
(F40 e wrappers ADD60/ADDC0), [assembly](../evidence/ai-combat-outcome-arithmetic.txt),
[dispatch/scratch](../evidence/ai-decision-dispatch-and-scratch.txt).
Fontes secundárias: [layout](../evidence/ai-combat-computed-stats-2026-11-04.md),
[vínculos](../evidence/ai-combat-stat-layout-2026-11-04.md),
[ciclo hipotético](../evidence/ai-simulation-state-lifecycle-2026-11-04.md).
Alguns dumps originais citados por esses relatórios não estão neste checkout;
não tratá-los como nova extração realizada nesta sessão.

Conferência adicional local, somente leitura, via GNU objdump (não é Evidence REA):

```bash
objdump -d -Mintel --start-address=0x5b0f40 --stop-address=0x5b1309 ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x5699a0 --stop-address=0x5699b5 ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x56b220 --stop-address=0x56b241 ../passion/joey_pc.exe
objdump -d -Mintel --start-address=0x56d790 --stop-address=0x56d9f2 ../passion/joey_pc.exe
```

O REA informou Ghidra `not_configured`; nenhum alvo foi aberto. Não há projeto
Ghidra persistente neste worktree. Não houve execução do jogo nem alteração do EXE.

## ABI e correlação

`005B0F40(side, attackerSlot, targetSlot, outEvent)` usa quatro palavras de
32 bits na stack: entrada ESP+4/+8/+12/+16. Evidência assembly: prologue
subtrai 0x20 e salva quatro registradores; side em `005B0F47 [ESP+34]`,
target em `005B0F50 [ESP+3C]`, out em `005B107C [ESP+50]` após pushes
adicionais. `RET` simples em `005B1308`; caller B0D90 limpa 0x10 em
`005B0E20`. Convenção compatível com cdecl, sem argumento implícito ECX.
Retorno EAX não é resultado semântico; resultado é o buffer de quatro bytes.

Preservar `sideRaw`, aceitar side lógico somente se 0 ou 1. Os slots naturais
do loop B0D90 são 0..4; destino especial 5 pertence a outra rota, não deve ser
lido como monstro. Validar antes de calcular qualquer endereço.

Cada registro deve conter versão, sessão, sequência, timestamp, threadId,
callId, selectorCallId (ou null), combatCallId (nos filhos), VA e caller.
IDs são locais à sessão. Usar stack **por thread**, não proximidade de eventos.
Aninhamento/reentrância e chamadas entre threads continuam pendentes de piloto.
Um enter sem leave, filho órfão, perda ou readError torna o confronto incompleto.

## Campos e instante de leitura

`P(s)=s*0xD44`, `M(s,z)=00A55D74+P(s)+z*0x90`.
Todos os campos abaixo são transitórios; ler sincronicamente no thread
interceptado. Isso não prova atomicidade contra outros threads.

| Campo | VA/offset e tamanho | Ponto | Evidência e limite |
| --- | --- | --- | --- |
| Lado, slots | stack F40, três u32 | enter F40 | ABI acima; não normalizar side inválido silenciosamente |
| Identidade dos dois slots | M+0, u32; índice = raw & 0xFFF | enter F40 | Layout estático; preservar raw e slot separadamente |
| Recurso usado pelo avaliador | retorno low16 de 004022E0(index) | leave de chamada natural direta da F40 | CALLs 005B0FB3, 005B1004, 005B10F8, 005B124C; pode repetir; não converter índice por tabela presumida |
| Postura do alvo | M(opponent,target)+6, u8 & 1 | enter F40 | Leitura efetiva 005B0F74, antes de ADD60; bit 0 seleciona DEF quando 1, ATK quando 0 |
| Visibilidade | M+6, u16 & 2 | enter F40 e enter wrappers | ADD60 força bit 2 temporariamente; DD30 interno pode ver valor diferente do inicial |
| Flags do monstro | M+0x8C = 00A55E00+P+z*90, u32 raw | enter F40 | Referência D790 a bit 0x20; demais bits não são automaticamente efeitos resolvidos |
| Flags de jogador | 00A56430+P, u32 raw | enter F40 | F40 lê bit 0x400000 em 005B1195/005B11C6; equivalência enter/instante de uso exige piloto |
| LP | 00A55D64+P, u16 | enter F40 | Layout do backend duel_state.cpp; contexto adicional, F40 não lê LP diretamente neste corpo |
| Seed do descritor | outEvent+0, quatro bytes raw | enter F40 | Primeira palavra é entrada: preserva máscara F005; segunda palavra será zerada e recalculada |
| Resultado do descritor | mesmo ponteiro, quatro bytes raw | leave F40 | Flags u16 e score int16 LE; não é opcode de replay nem dano já aplicado |
| Stats próprios | arg2 de 005ADD60(side,slot,out), 12 bytes | leave wrapper direto | CALL 005B0F92, return VA 005B0F97; captura depois de restaurar bit 2 |
| Stats rival | arg2 de 005ADDC0(side,slot,out), 12 bytes | leave wrapper direto | CALL 005B0FA9, return VA 005B0FAE; ocultos usam estimativa, sem DD30 |
| Vínculos brutos | quantidade u16 M+0xA; lista em M+0x4C | somente se houver decoder limitado validado | D790/D8D0 usam base 00A55D7E/00A55DC0; não copiar lista ilimitada; não deduzir atividade por nome |

Stats LE: +0 u16 índice interno, +2 byte tipo, +3 byte atributo,
+4 int32 ATK, +8 int32 DEF. Para ocultos, ADDC0 zera +0..3 e usa
1000 ou 1900 nos dois atributos; zero nesse buffer **não prova zona vazia**.
Observar DD30 isoladamente perde esse caminho. F40 depois aplica modificadores
contextuais próprios aos buffers locais; capturar os wrappers antes deles.

## Predicados consultados naturalmente

Hooks na entrada das funções e saída normal, sem invocação adicional. Argumentos
são palavras de stack; conservar retorno EAX raw e int32. Os pontos abaixo são
**return VAs**, CALL+5, verificados no assembly local. Filtrar por eles e pelo
combatCallId ativo no mesmo thread; um DD30 aninhado pode chamar helpers iguais.

| Função | Argumentos | Return VAs diretos da F40 | Significado permitido |
| --- | --- | --- | --- |
| 0056D790 | side,slot,resource | 005B1060 | Contagem filtrada Metalmorph (291), não booleano |
| 0056D8D0 | side,slot,resource,targetSide | 005B1117,005B112F,005B1153,005B116F | Contagens contextuais 521/604; curto circuito pode omitir chamada |
| 005699A0 | side,resource | 005B11F4,005B1227 | Consulta 58F; wrapper encaminha side,resource,-1 a 00569820 |
| 0056B220 | resource | 005B1205,005B1238 | Consulta 14D; compara 00569660(resource) com low nibble de 00A577F4; EAX 0/1 |
| 004022E0 | internalIndex | 005B0FB8,005B1009,005B10FD,005B1251 | Recurso low16; preservar input para identificar origem |

Não chamar helpers para completar ramos não tomados. Uma chamada não observada
fica ausente, com razão desconhecida até reconstruir o controle de fluxo a partir
das entradas. Essas saídas nativas permitem testar a fórmula escalar, mas **não**
provam cálculo independente dos efeitos nem equivalência do motor completo.

## Estado hipotético e ponto de parada

Registrar `stateKind=selector-window` quando F40 estiver aninhada em B1360/B16C0;
isso delimita uma janela de avaliação, sem afirmar que cada byte é estado real.
B08E0 salva apenas cinco registros de 0x90 por lado; B0950 restaura esses arrays
e auxiliares, não toda a engine. B09C0 altera posturas; B1610 remove tributos e
insere carta hipotética; B0A50 remove identidades. A postura 304/319 fora dessa
janela não pode ser usada como observação dentro dela.

O piloto deve validar pares enter/leave, duas saídas dos wrappers, seed inicial,
posturas e flags em chamadas naturais, caller relocation, threads e ausência de
readError/perda. Sem isso, contagem de confrontos completos observados neste
marco é **0**. Não há evidência nova discriminante para Goblin Attack Force.
S2 pode preparar esses pontos offline; S3 depende do JSONL Windows original com
atestado de hash e integridade. S4 fica bloqueada até observação discriminante.

## Revisão S3 — primeira captura, 2026-11-12

Ver [auditoria S3](../evidence/combat-context-pilot-2026-11-12.md). O piloto
forneceu 64 pares estruturalmente íntegros e 64 correspondências diagnósticas da
fórmula usando outputs nativos como inputs. Isso não elimina as lacunas temporais
ou valida o cálculo independente de atributos descritos acima.

Correções de interpretação da v1:

- A ABI semântica de 004022E0 é `ushort`; a stack contém o DWORD completo do
  cabeçalho de stats, e somente low16 de EAX é recurso. Não decodificar `argsRaw`
  inteiro como índice. O recurso efetivo FFFF do rival oculto não identifica sua carta.
- `resourceId:null` no snapshot e um retorno `resource` observado têm proveniências
  diferentes; não sobrescrever o recurso real do slot com o recurso efetivo estimado.
- Ausência de predicado exige reconstrução do caminho; registrar `NotEvaluated`
  com razão. Uma expressão composta pode estar determinada sem todos os seus
  termos, mas os termos ausentes continuam desconhecidos.
- Flags de jogador em enter F40 não são uma observação das leituras em 005B1195/
  005B11C6. Estabilidade ainda precisa de prova; saída coincidente não a fornece.
- Adaptação escalar de stats `AfterADDC0` não pode usar classe/Level default para
  reestimar ocultos. Separar atributos reais, estimativa observada e input efetivo.
- O primeiro JSONL tem `exeSha256:null`; a correção do exportador preserva atestado
  em capturas futuras, sem preencher o original. Exigir proveniência externa para
  qualificá-lo como build atestado.

## Revisão v2 — pontos de consumo e fronteira de independência

Esta revisão substitui os pontos de amostragem da v1 para os campos abaixo.
`combat_context.schemaVersion=2`; os campos anteriores são preservados. Não é
uma captura nova. Evidência estática: objdump do EXE local com SHA256 conferido
novamente; teste `test_combat_context_preflight.py` confronta as nove assinaturas
com o PE original. Não houve nova análise Ghidra nem execução Windows.

Os registros de ponto têm `phase=leave` por compatibilidade com serviços do
leitor: são **observações instantâneas**, não retornos de função. `functionVa`
identifica o ponto de observação; o consumo pode estar na instrução anterior.
`valueRaw:null` é intencional nos testemunhos de ramo. Não decodificar como zero.

| Registro / VA do hook | Valor e consumo comprovado estaticamente |
| --- | --- |
| `target_position_consumed` / 005B0F7B | low8 ECX contém o byte carregado em 005B0F74; bit 0 escolhe ATK/DEF. É a postura hipotética efetivamente usada, mesmo se o slot mudar depois. |
| `own_visibility_consumed` / 005ADD8D | low16 EAX contém posição carregada em 005ADD8A, antes de TEST AL,2. Somente dentro do wrapper direto correlacionado. |
| `opponent_visible_taken` / 005ADE2C | ramo revelado do TEST de 005ADDE7, `faceUpConsumed=true`. |
| `opponent_hidden_taken` / 005ADDF0 | ramo oculto do mesmo TEST, `faceUpConsumed=false`; não promover estimativa a atributo real. |
| `descriptor_seed_consumed` / 005B108A | low16 EDX é a palavra realmente lida de outEvent em 005B1086; `preservedMaskRaw=valueRaw & F005`. Conservar também os quatro bytes iniciais, sem substituir pela palavra consumida. |
| `defender_flags_consumed` / 005B119B | EAX DWORD já carregado em 005B1195; ESI é offset do rival. O TEST 005B11A0 usa esse registro, sem nova leitura de memória. |
| `attacker_flags_sample` / 005B11C6 | ECX*4 é offset próprio; DWORD amostrado antes do TEST de memória, **não** atestado de consumo do DWORD inteiro. |
| `attacker_protection_taken` / 005B11CF | ramo bit 400000 presente; marca somente o frame atual. |
| `attacker_flags_consumed` / 005B11DD | junção: exige amostra TEST anterior no mesmo frame; `protectionConsumed` é o bit consumido, verdadeiro se passou por 11CF, falso se saltou diretamente de 11CD. Sem amostra: null + correlationError. |

Offsets de jogador devem corresponder ao lado/stride do frame. Não existe
registro com o DWORD inteiro do atacante exatamente consumido: TEST não o
carrega em registrador. Para F40, o único bit desse DWORD usado é 400000;
outros bits amostrados não ganham proveniência de consumo. Perda de evento,
readError, filho órfão, childOverflow ou ausência da junção invalida o testemunho.
A ausência de `attacker_protection_taken` **isoladamente** nunca significa false.

Os wrappers incluem `positionBeforeRaw`/`positionAfterRaw` e `wrapperArgsRaw`
nos pontos internos. ADD60 força visibilidade somente no caminho oculto e
restaura bit 2 ao retornar (ADD93/ADDA5); isso é comportamento da engine,
não escrita do observador. Não reclassificar o monstro inicial como revelado.
O caminho rival revelado chama DD30 em ADE33; o oculto calcula estimativa.
Esses pontos não provam atomicidade de todo o estado DD30 contra outros threads.

O hook usa nove pontos internos além das dez entradas já existentes. Assinaturas
cobrem instruções inteiras de no mínimo cinco bytes e são verificadas antes de
instalar qualquer listener; falha de instalação desfaz os listeners desta tentativa.
Os pontos internos não declaravam onLeave JavaScript, mas usavam objetos
`{onEnter}`: isso pode armar retorno nativo e sobrescrever a stack. A
[investigação de ESI](handoffs/native-esi-crash-static-analysis.md) registra o
mecanismo, a correção para probes de função direta e os limites da conclusão.
Não há chamadas NativeFunction nesses callbacks nem atribuição explícita a
contexto, registro, buffer do duelo ou arquivo do jogo. Interceptor altera o código em memória
para interceptar e pode afetar timing: relocação x86 e retorno normal exigem piloto.
Limites continuam 64 avaliações por ativação e 24 filhos por avaliação (incluindo
pontos); overflow é explícito. Não aumentar limites para perseguir casos incompletos.

### Atributos: duas proveniências obrigatórias

`own_stats`/`opponent_stats.outputHex` são **outputs nativos observados**. Os
ajustes contextuais posteriores da F40 devem ser calculados independentemente;
esses buffers não são uma fonte independente para Core. Para Axe Raider e Gemini
Elf, `FromPlainCard` deriva ATK/DEF/tipo/atributo do DAT local identificado por hash.
Preservar índice interno, recurso e slot separadamente; validar o mapeamento contra
as chamadas naturais de 4022E0. O recurso efetivo do rival oculto continua separado.

Cada replay independente precisa de manifesto de modificadores com proveniência:
equipamentos elegíveis, supressão, paridade Shield & Sword, multiplicidade Mirror
Wall e sinal de deltas, incluindo efeitos de campo relevantes. `linkCountRaw` em
M+0A é agora amostrado para cada participante; zero é evidência sobre essa lista
na entrada, **não** prova ausência de todos os efeitos globais/temporários. Não
invocar D740/DAA0/B440 para preencher lacunas. O decoder de vínculos/manifesto
completo continua pendente; não foi inferido a partir da igualdade de stats.

### Checklist estrito de contexto completo (uma avaliação F40)

1. Metadata vinculada à sessão/PID/caminho do módulo, SHA256 esperado atestado
   pelo host após conferir o módulo carregado; hash do JSONL original e log de
   preflight preservados. Hash passado ao CLI do leitor sozinho não atesta o EXE.
2. Enter/leave F40 únicos, slots/lados válidos, IDs internos e recursos distintos,
   pai seletor e filhos do mesmo thread/callId/sessão; sequência sem perda,
   overwrite, erro, overflow ou chamada cortada ao desligar.
3. Quatro bytes do descritor inicial e final; palavra consumida em 1086 e máscara
   F005 explícitas. Se divergir da entrada, preservar ambas e investigar aliasing.
4. Postura consumida do alvo e visibilidade consumida dos dois wrappers, snapshots
   pré/pós, natureza `selector-window` preservada; nunca importar postura pós-restore.
5. Flags rival em EAX e bit próprio consumido comprovado pelo caminho TEST→junção.
   LP/flags adicionais exigidos pelo adaptador também conhecidos, sem defaults.
6. Dois buffers nativos diretos, identidades reveladas suportadas e atributos
   reconstruídos do catálogo independente com manifesto completo de efeitos.
   Comparar os atributos reconstruídos com os nativos, antes de avaliar o descritor.
7. Predicados F40 relevantes reconstruídos independentemente do estado/manifesto;
   chamadas naturais são testemunhos para comparação. Termos não tomados ficam
   `NotEvaluated` com justificativa pelo controle de fluxo, nunca false automático.
8. Core recebe somente inputs independentes mais observações externas necessárias
   (slots, postura, seed, flags), e produz os quatro bytes sem ajuste pela saída.

Se falta dado: `Incomplete` + nome do campo. Se identidade/efeito conhecido está
fora do suporte: `Unsupported`. Somente após cumprir 1–7 executar comparação;
`Match`/`Mismatch` em 8 não é uma maneira de resolver dados ausentes. O leitor C#
atual suporta v1 e mantém bloqueios conservadores; v2 será Unsupported até Terra
implementar a interface. **Contextos completos novos nesta entrega: zero.**
