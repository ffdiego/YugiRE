# CARDS.md — estado de cartas no YugiRE

Este é o inventário **funcional** de `Joey.Core`/`DemoDuel`, não uma contagem de cartas do DAT nem uma lista de callbacks decompilados. Atualize após implementar e testar, com escopo explícito.

## Status

- **Pending:** não implementado no motor jogável.
- **Partial:** existe um primitivo, filtro, trace ou integração incompleta.
- **Done:** funcional **no escopo indicado**, acionável em um duelo C# e coberto por testes; não exige RE exaustiva.
- **Verified:** `Done` e resultado relevante comparado com execução observada do Joey original. Traces de uma regra ainda não implementada em C# **não** tornam a carta Verified.

## Inventário atual (sem extrapolar números do DAT)

| Família / mecânica | Estado atual | Evidência de implementação | Limitação principal |
| --- | --- | --- | --- |
| Monstros normais — mecânicas genéricas | **Partial** (suporte de base) | `Demo.cs`: invocação comum, tributos por nível, set, mudança de posição e confronto básico | Não representa ~650 cartas individualmente validadas; faltam interações, exceções e cobertura por identidade |
| Magias de dano direto `P045` (5 IDs) | **Partial** | `DirectDamageRules.cs` e ativação básica em `DemoDuel` | Integração/condições do ciclo de efeitos e interações ainda limitadas |
| Equipamentos `P025` | **Partial** (3 IDs) | `EquipmentRules.cs`, `NativeAiAttributes.cs`, vínculo/limpeza básica no demo | Faltam demais famílias, supressões e interações |
| Magias de campo `P036` | **Pending** em C# | Evidência nativa para Sogen em `docs/evidence/` | Zona/cálculo de campo ainda inexistentes no demo |
| Infinite Cards (`0x593`) | **Done** (sandbox) | `Demo.cs`, `InfiniteCardsTests.cs` | Controle de limite de mão, sem generalizar para engine de correntes |
| Armadilhas, monstros de efeito e rituais | **Pending** no motor genérico | Análises e catálogos em `docs/` | Falta ciclo de resolução/gatilhos |

**Não usar os totais estimados do rascunho (~650 normais, ~180 magias, ~225 outras) como contagens verificadas.** Para totais precisos, consultar o DAT legítimo local; para `Done`, exigir comportamento/testes.

## Cartas com código parcial

| Resource ID | Carta | Status | Referência / escopo |
| ---: | --- | --- | --- |
| 342 | Sparks | Partial | `P045`, 200 de dano direto na rota simples |
| 343 | Hinotama | Partial | `P045`, 500 de dano direto |
| 344 | Final Flame | Partial | `P045`, 600 de dano direto |
| 345 | Ookazi | Partial | `P045`, 800 de dano direto |
| 1007 | Raimei | Partial | `P045`, 300 de dano direto |
| 300 | Legendary Sword | Partial | `P025`, Warrior tipo 15, +300/+300 sob condições suportadas |
| 301 | Sword of Dark Destruction | Partial | `P025`, atributo DARK, +400/-200 sob condições suportadas |
| 307 | Beast Fangs | Partial | `P025`, Beast tipo 11, +300/+300 sob condições suportadas |
| 1427 (`0x593`) | Infinite Cards | Done (sandbox) | Desabilita limite de 6 cartas na End Phase enquanto ativa |

## Evidências nativas que **ainda não** equivalem a regras C# prontas

| Resource ID | Carta / família | Estado do conhecimento |
| ---: | --- | --- |
| 332 | Sogen | 69 snapshots de atributos de batalha no piloto; 6 ocorrências classe15 com +200/+200, sem controle causal isolado e sem Beast-Warrior |
| 1315 | Rain of Mercy | Aumento de 1000 LP observado em captura do original |
| 1306 | Skull Invitation | Correlação de perdas de 300 LP e controle negativo observacional |
| 1353 | Burning Land | Ticks de 500 LP relatados em evidência, ainda sem regra C# completa |
| — | Rituais `P269` | Dados de rituais em `docs/evidence/ritual-rules-catalog.json`; falta jogabilidade |

## Como atualizar

Ao implementar uma família: registrar os resource IDs **reais**, mecânica acionável, testes que cobrem ativação/estado/custos, limites conhecidos e (se existir) a captura nativa. Não promover `Partial` a `Done` por testes de helper isolado; não promover a `Verified` por texto da carta ou por suposição.
