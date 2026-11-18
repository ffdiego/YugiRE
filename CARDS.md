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
| Magias de dano direto `P045` (5 IDs) | **Done** (sandbox) | `DirectDamageRules.cs`, `LifePointSpellRules.cs`, `DemoDuel` e testes de ativação da mão/set | Sem correntes, negação e validação de timing nativo integral |
| Magias de recuperação de LP (5 IDs) | **Done** (sandbox) | Red Medicine, Goblin's Secret Remedy, Soul of the Pure, Dian Keto e Rain of Mercy usam o mesmo fluxo de ativação/LP/cemitério | Valores do texto das cartas; interações/negação nativa não implementadas |
| Equipamentos `P025` | **Partial** (3 IDs) | `EquipmentRules.cs`, `NativeAiAttributes.cs`, vínculo/limpeza básica no demo | Faltam demais famílias, supressões e interações |
| Magias de campo `P036` | **Partial** (Sogen Warrior jogável) | `FieldSpellRules.cs`, zona exclusiva, ativação da mão/set, substituição, batalha, UI e `FieldSpellChecks.cs` | Beast-Warrior e outras magias `P036` ainda não implementados; timing/negação sem paridade nativa |
| Infinite Cards (`0x593`) | **Done** (sandbox) | `Demo.cs`, `InfiniteCardsTests.cs` | Controle de limite de mão, sem generalizar para engine de correntes |
| Armadilhas, monstros de efeito e rituais | **Pending** no motor genérico | Análises e catálogos em `docs/` | Falta ciclo de resolução/gatilhos |

**Não usar os totais estimados do rascunho (~650 normais, ~180 magias, ~225 outras) como contagens verificadas.** Para totais precisos, consultar o DAT legítimo local; para `Done`, exigir comportamento/testes.

## Magias de LP jogáveis no sandbox

| Resource ID | Carta | Status | Referência / escopo |
| ---: | --- | --- | --- |
| 342 | Sparks | Done (sandbox) | `P045`, 200 de dano direto na rota simples |
| 343 | Hinotama | Done (sandbox) | `P045`, 500 de dano direto |
| 344 | Final Flame | Done (sandbox) | `P045`, 600 de dano direto |
| 345 | Ookazi | Done (sandbox) | `P045`, 800 de dano direto |
| 1007 | Raimei | Done (sandbox) | `P045`, 300 de dano direto |
| 338 | Red Medicine | Done (sandbox) | Recupera 500 LP do ativador |
| 339 | Goblin's Secret Remedy | Done (sandbox) | Recupera 600 LP do ativador |
| 340 | Soul of the Pure | Done (sandbox) | Recupera 800 LP do ativador |
| 341 | Dian Keto the Cure Master | Done (sandbox) | Recupera 1000 LP do ativador |
| 1315 | Rain of Mercy | Done (sandbox) | Recupera 1000 LP dos dois duelistas |
| 332 | Sogen | Done (sandbox, Warrior somente) | Zona de campo, +200/+200 para classe 15 em ambos os lados, retirada/substituição dinâmica; Beast-Warrior pendente |
| 300 | Legendary Sword | Partial | `P025`, Warrior tipo 15, +300/+300 sob condições suportadas |
| 301 | Sword of Dark Destruction | Partial | `P025`, atributo DARK, +400/-200 sob condições suportadas |
| 307 | Beast Fangs | Partial | `P025`, Beast tipo 11, +300/+300 sob condições suportadas |
| 1427 (`0x593`) | Infinite Cards | Done (sandbox) | Desabilita limite de 6 cartas na End Phase enquanto ativa |

## Evidências nativas (não confundir com paridade C# completa)

| Resource ID | Carta / família | Estado do conhecimento |
| ---: | --- | --- |
| 332 | Sogen | 69 snapshots de atributos de batalha no piloto; 6 ocorrências classe15 com +200/+200. Agora jogável **para Warrior** no sandbox, sem controle causal isolado, classe Beast-Warrior ou equivalência nativa geral |
| 1315 | Rain of Mercy | Aumento de 1000 LP observado em captura do original; efeito simples agora jogável no sandbox, sem paridade completa |
| 1306 | Skull Invitation | Correlação de perdas de 300 LP e controle negativo observacional |
| 1353 | Burning Land | Ticks de 500 LP relatados em evidência, ainda sem regra C# completa |
| — | Rituais `P269` | Dados de rituais em `docs/evidence/ritual-rules-catalog.json`; falta jogabilidade |

## Primeiro cenário de magia de campo

A zona de campo é independente dos cinco slots de magia/armadilha. O efeito de Sogen é calculado sob demanda para ambos os lados, sem modificar ATK/DEF-base, e combina com equipamentos antes da saturação final. Cartas setadas não aplicam bônus; ativar uma nova magia de campo remove a anterior para o cemitério de seu dono. O jogador pode ativar o Sogen setado clicando na zona **FIELD** na interface local. Essa é uma política de sandbox com suporte explícito a **Warrior / classCode 15**; ainda faltam Beast-Warrior, outras magias de campo, gatilhos e supressões.

## Próxima lacuna concreta

A carta **Mooyan Curry (resource ID 337)** exige escolher quem receberá 200 LP, portanto permanece fora da tabela de resolução automática. Adicionar seleção de jogador na interface/ações antes de considerá-la jogável. Famílias com efeito de resposta/negação continuam pendentes.

## Como atualizar

Ao implementar uma família: registrar os resource IDs **reais**, mecânica acionável, testes que cobrem ativação/estado/custos, limites conhecidos e (se existir) a captura nativa. Não promover `Partial` a `Done` por testes de helper isolado; não promover a `Verified` por texto da carta ou por suposição.
