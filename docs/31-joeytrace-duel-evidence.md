# JoeyTrace: evidencias de cinco duelos reais

Analise observacional de cinco sessoes fornecidas pelo operador em 2026-11-03. Os traces brutos e replays YDR NAO foram versionados. EXE alvo SHA-256 db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de.

## Inventario e cuidados metodologicos

As cinco sessoes tem 957 lotes de mudancas na RAM, 18 marcadores e nenhum erro de leitura. Quatro replays somam 2383 registros: capturas 010701 (798 comandos, 40 turnos inferidos), 012243 (228, 7), 012851 (295, 9) e 013432/Exodia (1062, 52). A captura 010023 tem 78 alteracoes, mas nao possui YDR.

Nas capturas 012243, 012851 e 013432 o coletor foi armado enquanto ainda havia estado residual do duelo anterior. A reinicializacao do estado e observada apos 34,889 s, 9,500 s e 8,000 s, respectivamente. Nao atribuir os dados anteriores aos replays anexados.

Os snapshots foram obtidos por leitura nao atomica, a intervalos de aproximadamente 100 ms. Os YDRs nao tem timestamps por comando; e incorreto afirmar alinhamento temporal exato entre comando e alteracao RAM. Todos os cinco manifests registram unknown_only_enabled=false e raw_blocks_enabled=false. Marcadores e nomes das jogadas sao relatos do operador, nao inferencias confirmadas pelo binario.

## Exodia: evidencia de condicao terminal especial

No replay 013432, os comandos finais do turno 52 sao:

- Registro 1057: 0x8001 (0,0,0), inicio de turno inferido.
- Registro 1058: 0x8050 (0,0,0), etapa de fase.
- Registro 1059: 0x8061 (1,1,0), compra de carta documentada em docs/21-native-card-draw.md.
- Registro 1060: 0x8005 (0,0,0), unico opcode BASE 0x05 nos quatro replays.
- Registro 1061: 0x8004 (0,0,0), encerramento provavel.

A ultima alteracao RAM registrada, t=1670,000 s / seq=374, mostra lado 1 com mao aumentando de 5 para 6. LP finais: lado 0=7350, lado 1=2200. Isso corrobora a vitoria por Exodia sem dano fatal relatada pelo operador. Nao prova que o opcode 0x05, isoladamente, determine a regra: pode representar sua notificacao ou apresentacao.

A evidencia estatica em docs/23-terminal-conditions.md identifica 0x005BBF70: verifica na mao IDs de recurso 16, 17, 18, 19, 20 e alimenta o bit 0x200 no estado de cada lado, consultado por 0x005BC050. Os cinco IDs estao na lista inicial de deck do lado 1 desse replay. O JoeyTrace v1 nao extrai os IDs reais da mao para provar qual carta foi comprada por ultimo.

### Resultado do comando 0x8004

Nos quatro YDRs, o ultimo opcode e sempre 0x8004. Seu primeiro argumento esta correlacionado aos resultados na perspectiva do lado 1:

| Captura | LP finais lado 0 / lado 1 | 0x8004 arg1 | Resultado observado |
|---|---|---|---|
| 010701 | 0 / 8850 | 0 | Vitoria do lado 1 por LP |
| 012243 | 8000 / 0 | 1 | Derrota do lado 1 por LP |
| 012851 | 9000 / 0 | 1 | Derrota do lado 1 por LP |
| 013432 | 7350 / 2200 | 0 | Vitoria especial do lado 1 (Exodia) |

Hipotese: arg1=0 significa vitoria local e arg1=1 derrota local. Nao foi testado empate, deck-out nem outras modalidades. O bit alto do opcode nao deve ser interpretado automaticamente como lado vencedor.

## Efeitos comparados com as anotacoes

### Messenger of Peace e Nimble Momonga (captura 010023, sem YDR)

- Marcador 1 em t=43,500 s: lado 1 perdeu 100 LP (8000->7900, seq=12 em 42,906 s); coerente com o custo descrito. Copycat/Gearfried foi citado no mesmo relato, mas nao isolado nos dados.
- Nimble Momonga: indice interno 453/ocorrencia 12 desaparece da zona 2 do lado 1 em t=124,000 (seq=36). LP sobe 6300->6900->7300 entre 125,796 e 125,890 (seq=38/39), ganho total de 1000. Duas novas ocorrencias do indice 453 entram nas zonas 2 e 3 em t=128,110 e 130,406 (seq=41 e 43), ambas com position_word=0201. Os incrementos 600+400 de LP podem ser estados de uma animacao, nao efeitos separados.

### Witch of the Black Forest / Spear Cretin (captura 012243)

- Marcadores anotados em t=95,203 e t=156,906.
- Indice 261 sai da zona 0 do lado 1 em t=85,703 (seq=35); mao 5->6 em t=94,389 (seq=43).
- Indice 516 sai da zona 0 em t=138,093 (seq=52); indice 261 entra em t=145,593 (seq=55), sai em t=148,593 (seq=58); mao 5->6 em t=153,108 (seq=59), depois 6->7 em t=160,093 (seq=69).
- E compativel com as jogadas descritas, mas os increments da mao ainda podem envolver compra de turno: identificar IDs de cartas e manipulacao de deck antes de atribuir causalidade.
- O replay contem 0x80AD no registro 127, arg1=1116, apos cadeia de combate/retirada. Comparar com 0xAD de outras cartas sem atribuir semantica exclusiva.

### Wall of Illusion (captura 013432)

- Marcador em t=65,296. Em t=61,906 / seq=33, lado 0 remove monstro indice 384 ocorrencia 3 da zona 1 E a mao do mesmo lado cresce de 5 para 6 na mesma leitura. Evidencia forte para retorno do atacante a mao, ao inves de destruicao.
- O replay tem 0x80AD com arg1=83 no registro 256 apos outra cadeia de combate. Relacao causal especifica com o marcador nao esta provada; 0xAD tambem ocorre com arg1=1116 em 012243.

### Raigeki (captura 013432)

- Mao do lado 1 diminui uma carta em t=1281,000.
- Cinco zonas de monstro do lado 0 sao esvaziadas em t=1282,406 (seq=136), 1284,000 (seq=137), 1285,609 (seq=139), 1285,703 (seq=140), 1285,796 (seq=141).
- A remocao e observada ao longo de ~3,39 s, nao numa amostra atomica. Fortemente compativel com a anotacao do operador.

### Trap Hole (captura 013432)

- Indice 24 entra na zona 0 do lado 0 em t=1338,906 (seq=166), concomitante a mao -1, e desaparece em t=1343,609 (seq=168), cerca de 4,7 s depois. Compativel com a destruicao de monstro recem-invocado descrita no marcador t=1349,796.
- Outras alteracoes proximas nao devem ser atribuidas automaticamente a esse efeito.

### Dark Hole, Spellbinding Circle e Monster Reborn (captura 013432)

- Apos a acao descrita como Dark Hole, tres monstros do lado 0 desaparecem das zonas 0, 1, 2 em t=1471,891, 1473,391, 1475,094 (seq=270/271/273). O lado 1 nao tinha monstros no instante, portanto este teste nao prova eliminacao bilateral.
- O operador relatou destruicao de Spellbinding Circle por estar vinculada a Red-Eyes. Essa relacao nao e observavel no JoeyTrace v1: nenhuma zona de spell/trap ou associacao de equipamento e decodificada. Existem 2 ocorrencias do opcode 0xB2 no replay, mas a correspondencia temporal com Spellbinding Circle e desconhecida.
- Indice 1082/ocorrencia 33 e removido da zona 1 do lado 0 em t=1473,391 (seq=271). Depois, indice 1082/ocorrencia 67 entra na zona 1 do lado 1 em t=1571,906 (seq=321) apos Monster Reborn relatado. Isso corrobora a reentrada do mesmo tipo de carta do outro lado, com nova palavra de ocorrencia. Nao significa que indice sozinho identifique uma instancia fisica.

## Proximos passos mais produtivos

1. **Ghidra / condicoes terminais:** localizar produtor/consumidor de 0x05 e 0x04 no dispatcher; correlacionar 0x05 com o predicado de Exodia e o bit 0x200. Confirmar semantica do resultado em 0x04 usando os quatro finais.
2. **JoeyTrace / inicio de duelo:** corrigir a heuristica de armamento para nao misturar dados residuais de outras partidas. Preferir sinal nativo, ou ao menos detectar limpeza do campo e distribuicao inicial.
3. **JoeyTrace / cartas da mao:** documentacao estatica ja localiza o array em 0x00A56434 e contador em 0x00A55D68 (docs/21). Adicionar IDs/indices e marca terminal 0x00A55D6C, com cuidados de privacidade.
4. **Efeitos:** investigar 0xAD (args 1116 vs 83), fluxo de vida +1000 e novas ocorrencias de Nimble Momonga, e a cadeia de busca do deck em Witch/Spear.
5. **Associacoes de cartas:** mapear spell/trap zones e vinculos antes de afirmar como Spellbinding Circle e equipamentos sao removidos junto com monstros. Estudar 0xB2 no contexto das rotinas ja analisadas.

Os traces brutos foram intencionalmente mantidos fora deste commit; nomes e dados pessoais de paths/saves nao foram publicados. A separacao entre OBSERVACAO, RELATO DO OPERADOR e HIPOTESE deve ser preservada nas proximas rodadas.
