# Evidência dinâmica: ABI do dispatcher e callbacks de cartas (2026-11-03)

Sessão fornecida pelo operador: Frida 17.25.2, processo PE32 ia32 com base real `0x00400000`, EXE previamente validado com hash `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`. `joey_abi_probe.js` monitorou o wrapper `0x0059DEB0` e o produtor `0x005B9170`. Dados brutos da sessão não foram publicados no repositório.

## Amostragem e limites

- `ready_abi` emitido; 34 entradas e 34 saídas do wrapper com leitura bem-sucedida do ponteiro de pedido; contagem final **34 callbacks** e **263 chamadas ao produtor de comandos**.
- Apenas as primeiras **48** chamadas do produtor tiveram argumentos registrados; não inferir distribuição de todos os 263 comandos.
- Em todas as entradas documentadas do wrapper, `returnAddress = 0x005BBA7D`. Isso mostra o mesmo chamador direto nas amostras, não prova que seja o único chamador possível.
- O log não relata falha do Frida. Não houve substituição de código, validação de uma ABI completa ou recompilação.

## Identidade das cartas

Índices obtidos do primeiro `uint16` do pedido, cruzados com `docs/evidence/card-callback-catalog.json` (distintos de IDs de recurso):

| Índice interno | Carta | ID de recurso | Função principal da tabela | Ocorrências no wrapper |
|---:|---|---:|---|---:|
| 457 | Jam Breeding Machine | 1426 | `0x00583F30` | 1 |
| 972 | Morphing Jar | 590 | `0x005860E0` | 28 |
| 893 | Magic Jammer | 1029 | `0x00588D90` | 1 |
| 248 | Sangan | 47 | `0x00583D20` | 5 |

## Retornos repetidos com pedido aparentemente estável

**Morphing Jar, índice 972, primeira sequência**: `word1=0x9501`, `word2=0x03E1`. Retornos das 15 chamadas: `0x80 × 5 → 0x7F × 6 → 0x7E → 0x7D → 0x7C → 0x00`.

**Morphing Jar, segunda sequência**: `word1=0x9501`, `word2=0x0421`. Retornos das 13 chamadas: `0x80 × 5 → 0x7F × 3 → 0x7E → 0x7D → 0x7C → 0x00`.

**Sangan, índice 248**: `word1=0xA55D`, `word2=0x0361`; retornos `0x7F → 0x7E → 0x64 → 0x01 → 0x00` nas cinco chamadas amostradas.

**Jam Breeding Machine, índice 457**: `0x00`; **Magic Jammer, índice 893**: `0x00` — um único retorno amostrado por carta, sem inferir ausência de outras fases ou subcallbacks.

Esses padrões são **consistentes** com handlers/continuações em etapas e com o estudo estático de retorno `0x80/0x7F/0x7E` em outros callbacks. Não é válido nomear cada estado de Morphing Jar sem seguir `0x005860E0` e seus chamadores. Também não se pode contar uma chamada como uma ativação: Morphing Jar aparece 28 vezes na mesma sessão mas em duas sequências de pedido com valores distintos.

Os primeiros quatro `uint16` de cada pedido amostrado permaneceram iguais **dentro de cada sequência**, embora o retorno varie. Portanto, caso o estado de continuação seja persistido, ele pode estar em bytes posteriores do pedido, outros globais, ou resultar da evolução do estado do jogo. O experimento ainda não distingue esses casos.

## Pista do tamanho de 20 bytes

Para Magic Jammer, o pedido foi lido no endereço `0x00A55B54` e o segundo argumento bruto do wrapper era `0x00A55B40`. A diferença é `0x14` (=20), igual ao tamanho de pedido já documentado em `docs/cards/parasite-paracide.md`. É evidência consistente com dois registros adjacentes ou pedidos relacionados, não prova encadeamento sem seguir a lógica de quem constrói o segundo argumento.

## Comandos observados

Nos primeiros 48, aparecem `0x0061(..., 5, ...)` e `0x8061(..., 5, ...)` no início, depois `0x0061/0x8061(..., 1, ...)` no andamento; `0x02` antes de `0x03` nas transições de ambos os lados. Essas observações são compatíveis com `docs/21-native-card-draw.md` e `docs/19-temporary-state-cleanup.md`. A coleta observa a **emissão** dos comandos, não sua conclusão nem o instante de apresentação na tela.

## Próximo teste de baixo risco

Instrumentar `0x005860E0` e `0x00583D20` com `Interceptor.attach`, sem alterar seus retornos, e capturar uma amostra protegida dos **20 bytes** completos do pedido no começo e no fim de cada chamada. Correlacionar wrapper e handler pelo thread ID. Evitar instrumentar `0x0056DD30`, que teve mais de 128 mil chamadas na primeira prova.

Antes de substituir qualquer função, confirmar pelo assembly e rastros: convenção de chamada; origem/destino dos argumentos; forma de retorno; globais, flags e filas modificadas; efeitos indiretos. Nenhuma dessas condições é comprovada apenas pelos dados desta sessão.


## Terceira captura — hooks diretos e cobertura (2026-11-03)

`joey_card_handlers.js` conseguiu instalar hooks no wrapper, Morphing Jar, Sangan e Magic Jammer. Ao tentar `0x00583F30` (compartilhado com Jam Breeding Machine e DNA Surgery), o Frida declarou `unable to intercept`. A leitura de bytes `66 33 C0 C3 90...` corresponde a `xor ax, ax; ret; nop...`: apenas quatro bytes úteis no corpo, compatível com dificuldade de realocação do prólogo. A causa interna do Frida não foi isolada.

Na sessão, Morphing Jar teve 13 entradas no handler nativo `0x005860E0`; Sangan e Magic Jammer, zero. O wrapper, por sua vez, teve mais de 70 chamadas. **As primeiras 48 detalhadas** permitem mapear, pelo primeiro `uint16` e catálogo estático, Cyber Jar (índice 320, 31 chamadas), Giant Rat (218, 7), DNA Surgery (573, 2), Raigeki (345, 2), Harpie's Feather Duster (695, 2), Change of Heart (300, 1), Tribute to The Doomed (363, 1), Parasite Paracide (197, 1) e Magician of Faith (471, 1). Esses índices correspondem aos callbacks específicos do catálogo, mas não foram instrumentados individualmente.

Em Cyber Jar, os 20 bytes do pedido foram modificados entre entrada e saída em algumas chamadas. Um dos campos de 16 bits, no offset +6, foi observado progredindo de 0 para 1, 3, 7 e 15 em etapas sucessivas. Outros campos do pedido também mudaram. Essa progressão sugere informação acumulativa de andamento, mas **o significado do bitmask não está demonstrado**.

O último retorno de `MorphingJarPrimary` foi registrado como EAX bruto `0xFFFF0000`; o low16 é `0x0000`. Não interpretar o EAX de 32 bits como contrato do callback antes de conferir a assinatura e as instruções consumidoras do valor.

O log finaliza com `Process terminated`. Sem informação adicional, não se sabe se foi saída normal do jogo ou falha. O observador genérico `joey_all_primary_callbacks.js` foi criado para evitar a dependência de hooks em funções específicas e registrar índices de todos os pedidos que passarem pelo wrapper.
