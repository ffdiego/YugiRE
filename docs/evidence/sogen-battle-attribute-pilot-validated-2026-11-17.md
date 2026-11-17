# Piloto nativo Sogen: ATK/DEF efetivos em 69 comandos 0x31

**Data:** 17/11/2026. **Origem entregue pelo operador:** ZIP `sogen-piloto.zip` contendo `duels-20261117-225913-550178-pid3540.jsonl`, `battle-attributes.json` e `battle-attributes.md`. A análise cruzou os eventos JSONL brutos e os resultados do analisador `yugilab.battle_attribute_audit`. Não adicionamos nenhuma rodada de duelo nesta etapa.

**SHA256 conferido diretamente nos bytes JSONL do ZIP:** `01053d79243d5a086447f201f66f698a6d8ba259f4d9de1fe8fad00d96c3cf2b`. SHA256 do arquivo local `attributes.json`, registrado no audit: `decac745e39a46c00b15118472cb03b70fbe5b6f96694535fcaba8c63845e6fe`; bytes deste attrs não acompanharam o ZIP, portanto o hash é **reportado**, não recalculado aqui.

## Integridade

- 3/3 duelos completados, 2637 eventos, RNG controlado aplicado três vezes, 0 eventos descartados.
- `stateTrace`: **2301** amostras, 3 baselines, **639** deltas, **0** erros de leitura.
- `battleAttributesTrace`: **69** snapshots válidos em **69** comandos `0x0031`; **0** erros de leitura. A comparação dos blocos produz índices plausíveis, estatísticas compatíveis com atributos-base e danos observados (o layout lido na entrada de `0x31` é útil e não retorna lixo nesta captura).
- Os valores continuam snapshots na **entrada do produtor** e não representam diretamente todas as etapas do resolvedor ou causa de modificadores.

## Sogen e a assinatura +200/+200

Sogen **internalIndex=490**, `resourceId=332`, CF observado nas seq **421** (duelo 1) e **1256** (duelo 3); entrou no campo **lado0/slot10** nas seq **422** e **1257**, respectivamente. Não há CF/entrada de Sogen no duelo 2.

**Quatro identidades de monstro distintas**, todas com `baseClassCode=15`, tiveram exatamente +200 ATK e +200 DEF enquanto Sogen permanecia observada no slot 10. São **6 snapshots**, pois 147 e 151 aparecem duas vezes:

| InternalIndex | ClassCode nativo | Base ATK/DEF | Calculado ATK/DEF | Seq `0x31` | Duelo |
|---:|---:|---:|---:|---|---|
| 147 | 15 | 800/800 | **1000/1000** | 507 | 1 |
| 147 | 15 | 800/800 | **1000/1000** | 1337 | 3 |
| 1052 | 15 | 500/1200 | **700/1400** | 1735 | 3 |
| 151 | 15 | 700/1000 | **900/1200** | 1876, 1884 | 3 |
| 24 | 15 | 1700/1150 | **1900/1350** | 2632 | 3 |

**Controle interno relevante:** monstro **147** no **duelo 2, seq943**, quando Sogen **não** foi observada no campo, tem **800/800** (sem delta). Assim, para a mesma identidade, os snapshots mostram 800/800 sem campo versus 1000/1000 com campo, com +200 nas duas dimensões.

**Tabela agregada por tipo/campo, sem duplicar snapshots:** classe15 com Sogen **6/6 snapshots com +200/+200**; classe15 sem Sogen **1/1 sem alteração**; nos **demais 94 snapshots de monstro** enquanto Sogen esteve presente, o delta ATK/DEF é **0/0**, inclusive múltiplas classes não elegíveis. Essa segunda contagem deve ser revalidada mecanicamente se for usada como métrica formal; o resultado básico (todos os não-15 com campo sem delta) foi calculado no relatório. Não afirmamos ainda que o código 15 mapeia exaustivamente todos os tipos elegíveis a Sogen. A documentação já associa o tipo15 à elegibilidade para Legendary Sword (Warrior), enquanto o texto da carta Sogen também contempla Beast-Warrior. **Não foi isolado um Beast-Warrior nesta campanha.**

**Associações brutas:** nas seq **507** e **1337**, há `referenceWord=0x0A00` com `metadataWord=8`, porém nos monstros **550** e **886**, respectivamente, e **não** nos monstros beneficiados 147. Nos outros quatro snapshots +200/+200 não aparece nenhuma associação de campo candidata `0x0A00/0x0A01` nos monstros presentes. Logo, **não é necessário observar essa associação no beneficiado para medir +200/+200 no cálculo**. Não concluir que os marcadores de campo não participam de alguma etapa; seu significado ainda precisa de RE.

## Cinco danos de 200 LP: explicação independente de Sogen

No terceiro duelo, os cinco eventos `-200 LP` identificados anteriormente aparecem num pipeline explícito de combate `0x35→0x30→0x31→0x3C→0x43→0x7C`. Os snapshots dos pares combatentes no `0x31` **não têm +200** em ATK nem DEF (ambos os deltas são zero). As diferenças entre ATKs calculados em cada par são exatamente 200:

| Seq 0x31 | Seq perda LP | Bloco A ATK | Bloco B ATK | Diferença absoluta |
|---:|---:|---:|---:|---:|
| 1573 | 1576 (lado1) | índice124: 1400 | índice135: 1200 | 200 |
| 2076 | 2079 (lado0) | índice1038: 1400 | índice384: 1600 | 200 |
| 2258 | 2261 (lado1) | índice887: 1700 | índice982: 1500 | 200 |
| 2456 | 2459 (lado0) | índice939: 1200 | índice966: 1400 | 200 |
| 2559 | 2562 (lado1) | índice887: 1700 | índice169: 1500 | 200 |

Todos os cinco incluem `0x31` com valor 200, `0x43` emitindo 200 e LP do lado adequado caindo 200 no próximo evento amostrado. Essa evidência **explica os cinco danos de 200 como diferenças de ATK no combate**, sem requerer a aplicação de Sogen nesses ataques. Nem todo dano de 200 temporalmente próximo a magia de campo é dado pelo efeito; os agregadores antigos foram corretamente conservadores.

## Conclusões em camadas

**Observado diretamente:** a leitura opt-in de ambos os blocos de 16 bytes é funcional, o pareamento cardIndex/ATK/DEF com o catálogo tem consistência, e o bônus exato +200/+200 está presente para a classe15 em duas batalhas/duelos diferentes (6 snapshots), ausente para o índice147 sem Sogen. Os cinco danos antigos passam por batalhas com ATK diferença200 sem buffs observados nos respectivos pares.

**Hipótese fortemente apoiada:** o bônus dinâmico de Sogen para monstros elegíveis está sendo aplicado pelo cálculo de atributos de batalha, mesmo quando não há `0x0A00` associado diretamente ao monstro beneficiado.

**Ainda não demonstrado:** (i) relação causal isolada com carta via controle negativo em deck semelhante; (ii) classe/casos de Beast-Warrior; (iii) ordem com equipamentos, Shield & Sword e outros modificadores; (iv) timepoint da modificação fora do pipeline de `0x31`; (v) interpretação de todos bits do descriptor e `referenceWord=0x0A00`. Não promover à paridade C# completa.

**Próxima etapa:** preparar piloto de controle sem Sogen mantendo demais condições registradas. Comparar classe15 em estados semelhantes, não exigir idêntica sequência de jogadas após mudar deck. Para melhor identificação do bônus, um teste forçado com Warrior elegível e outro Beast-Warrior, com o mesmo monstro observado antes/depois da ativação, será mais discriminativo.
