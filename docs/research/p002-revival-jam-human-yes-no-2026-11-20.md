# P002 Revival Jam — experimento humano Sim/Não no Joey original (20/11/2026)

**Classificação:** observação dinâmica nativa + relato direto do operador. Evidência específica para o perfil P002; ainda **não** é prova de paridade entre Joey original e Joey.Core.

## Proveniência e integridade

O operador compartilhou captura de tela do prompt e ZIP privado de JoeyTrace, obtidos em dois duelos manuais sucessivos com **joey_pc.exe original**:

- EXE SHA256: `db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
- ZIP privado SHA256: `afddec1e54274831447ec9821fc6a4d2582b46e5fe3c745109dc11cf7664591b`.
- `state-changes.jsonl` SHA256: `cff105c6395a736138b98b264685c1ef333587e034d376161d6c1d08f5aff6b2` (201 eventos).
- `snapshots.jsonl` SHA256: `68d934f6262787a3b41019cbf2c202a6a1fead907f9b72f1dce5e7b61fe1afcc` (202 snapshots).
- Screenshot privado SHA256: `e38e365634e1045a25f175f6ac25a1ed84803e923d7f1bde31db55e9f4378ab4`.
- 267 segundos de captura; 2491 polls; 201 change batches; **0 erros de leitura**; 0 marcadores; 2 segmentos separados por 1 `possible_duel_reset` heurístico (seq 125).
- Coletor externo `read_only_external_polling` e não atômico; `native_command_hook_enabled=false`. O ZIP e a screenshot **não são versionados** e os caminhos/PIDs locais não são publicados.

O texto do prompt fotografado diz: **“Do you wish to pay 1000LP to Special-Summon Revival Jam in your next Standby Phase?”**, com escolhas **Yes/No**. O texto da carta na captura menciona invocação em **face-up Defense Position** e pagamento de 1.000 LP após ida ao cemitério por batalha.

### Relato humano (fonte independente do JSONL)

- **Duelo 1:** o operador escolheu **Sim em todas as oportunidades oferecidas**. Quando restavam menos de 1.000 LP, a pergunta deixou de aparecer.
- **Duelo 2:** o operador escolheu **Não na primeira oportunidade**.
- Não foram anotados timestamps/markers de cliques; portanto o JSONL não codifica diretamente a escolha nem o instante exato de exibição do modal.

## Duelo 1: três pagamentos de 1.000 LP e três retornos

Index DAT de Revival Jam: **1060**; resource ID **1405**. Na coleta, a carta aparece no **side 1, monster zone 0**.

| Episódio | Desaparecimento do campo | Diminuição de LP side 1 | Reaparecimento no campo |
| --- | --- | --- | --- |
| 1, Sim (relato) | seq **27** (42,09 s) | seq **29–30** (45,50–45,61 s): **8000→7000** | seq **37** (57,00 s), `position_word_hex=0203` |
| 2, Sim (relato) | seq **50** (76,41 s) | seq **52–53** (79,09–79,20 s): **7000→6000** | seq **63** (86,19 s), `0203` |
| 3, Sim (relato) | seq **77** (111,19 s) | seq **79–80** (113,81–113,89 s): **4000→3000** | seq **93** (123,00 s), `0203` |
| 4, sem prompt segundo relato | seq **100** (134,00 s) | side 1 permanece com **500 LP**; nenhum pagamento de 1.000 | **Nenhum** novo retorno observado até o fim do segmento |

O primeiro aparecimento observado de índice 1060 foi seq **13** (23,91 s; `position_word_hex=0201`). Antes da primeira destruição, na seq **26** (41,31 s), o mesmo `card_index` muda `occurrence_word=0→4` e `position_word_hex=0201→0203`. Os valores de `occurrence_word` nos reaparecimentos são **6**, **9** e **12**. **Não interpretar esse word como identidade estável de objeto entre GY e campo**: ele muda na transição. A correlação dos retornos é por índice de catálogo, lado, janela temporal e relato, não por um ID de ocorrência nativo validado.

A seq **93** já mostra o retorno com **500 LP** (o terceiro pagamento ocorreu anteriormente, quando havia 4.000). A seq **100** é, portanto, um teste de inelegibilidade após novo envio ao GY com LP já insuficientes. O terminal desse duelo mostra side 1 chegando a 0 LP na seq **124**.

A redução de LP é amostrada em várias pequenas alterações enquanto a UI anima o número, mas cada primeira janela soma precisamente **-1000**.

## Duelo 2: recusa

O reset heurístico ocorre na seq **125** (197,50 s). No segundo segmento:
- Seq **152** (211,70 s): `card_index=1060` entra em side 1 / zone 0 com `0201`.
- Seq **158** (219,50 s): mesmo índice muda o `position_word_hex` para `0203`.
- Seq **159** (220,41 s): Revival Jam sai do campo.
- **Nenhum retorno posterior de `card_index=1060` foi observado** até a conclusão (seq 201), consistente com a recusa relatada.
- Seq **162–165** (224,59–224,91 s) mostra side 1 perdendo **1.700 LP** (9000→7300). **Não** atribuir automaticamente esses 1.700 ao Revival Jam ou caracterizá-los como pagamento; o relato é de recusa e a captura não registra comandos nem causa do dano.

## Ordem nativa adicional: compra precede o retorno (impacto no Core)

O segundo exame da captura mostrou, em **três** episódios independentes,
`hand_cards_changed` com uma carta adicionada no side 1 e
`deck_count_changed` com uma carta removida **antes** da reaparição:

| Episódio | Compra (hand+1 / deck-1) | Retorno de `card_index=1060` |
| --- | --- | --- |
| 1 | seq **35** (55,80 s; deck 34→33, hand +383) | seq **37** (57,00 s) |
| 2 | seq **61** (85,00 s; deck 33→32, hand +244) | seq **63** (86,19 s) |
| 3 | seq **91** (121,80 s; deck 31→30, hand +1016) | seq **93** (123,00 s) |

A correspondência é muito forte para a ordem **Draw → efeito na
Standby** (a Standby vem após a Draw nas regras do jogo). O coletor
não registra o início/fim da fase em si, então a associação exata à
transição de fase ainda pode ser confirmada por instrumentação de comandos.

**Correção necessária:** o `DemoDuel` do PR #25 resolvia efeitos
diferidos dentro de `AdvanceTurn()`, **antes** de `Draw()`. O contrato
experimental de Revival Jam deve aplicar o efeito somente depois da
compra bem-sucedida do dono. Isso não requer uma fase Standby pública
para corrigir a ordem, mas exige testes de regressão específicos.

## O que foi observado, e o que ainda não foi provado

**Sustentado:** o Joey original apresenta o prompt Yes/No para o custo, menciona a **próxima Standby**, existe forte correlação entre aceitação e pagamento de 1.000 LP seguida de retorno; recusa se correlaciona com ausência de retorno; LP 500 se correlacionam com ausência da pergunta. Os retornos aparecem com word bruto `0203`, consistente com face-up defense descrito no texto da carta, mas o bitfield exato não foi decodificado aqui.

**Ainda pendente:** igualdade exata em **1000 LP** (relato apenas inferior a 1000); regra de owner != controller; negação/cancelamento e múltiplas cópias; o código nativo que decide abrir o modal e o que o modo demo/CPU decide em vez do humano; atraso entre clique e mudança de LP; correspondência rigorosa do turno/Standby via comandos; confirmação de ocorrência física pelo GY (coletor não lê identidade completa no GY).

**Hipótese útil a investigar no Ghidra:** perfil P002 (registro 365), `availability=0x00582990`, `preparation=0x0057DC40` (endereços do catálogo, não identificação do modal). Seguir os callers até a rotina que solicita uma opção Sim/Não e a política do modo demo, sem assumir que o prompt é meramente ocultado.

## Relação com a implementação atual

O sandbox C# introduzido no [PR #25](https://github.com/ffdiego/YugiRE/pull/25) já:
- solicita decisão após destruição por batalha, cobra 1.000 LP apenas quando a opção é aceita e agenda retorno no turno seguinte do dono;
- recusa criar a decisão com `Life <= 1000`; a fronteira de **1000 exatos** ainda precisa de teste humano;
- resolve por referência `Card` (política local C#), não pela `occurrence_word` nativa;
- representa Standby por hook provisório de início de turno (não fase nativa completa).

O experimento aproxima esse sandbox da observação real em ambos os ramos, mas **não** promove o perfil para `Verified`.

## Próximo teste econômico

1. **1000 LP exatos**: descobrir se o Joey oferece o prompt (e, caso sim, se autoriza um pagamento que zera LP).
2. **Modo demo / IA** com Revival Jam e uma captura de comandos nativos, para identificar política de decisão e se evento de escolha é registrado em algum caminho interno.
3. No Ghidra, conferir `0x00582990`, `0x0057DC40`, a função do modal, e a geração/agendamento do retorno.
