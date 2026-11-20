# mass96 C11 — controle sem Gravity Bind, decisões opcionais e Revival Jam

**Fonte:** captura nativa privada `mass96-c11-no-gravity(1).zip` recebida do operador em 20/11/2026.
SHA256 ZIP: `14f012a6d12e8a03f9c35a306082a57faca43d42c0cbbc0ff538d5cdb4536566`.
SHA256 JSONL: `6c7647f4c59113dd71d80e98cbee232a795ba92fb9286281bf59a6dfea0bf5f3`.
O ZIP não é versionado; `seq` abaixo aponta para o JSONL do operador.
Comparação histórica: [mass96-gravity-stall-and-p168-differential.md](mass96-gravity-stall-and-p168-differential.md).
**Classificação:** observação nativa correlacionada; não é prova de paridade Core/nativo ou causalidade exclusiva.

## Integridade da captura e comparação A/B

- 3/3 duelos concluídos; 1.598 eventos, 1.388 comandos, 1.328 amostras de estado, três baselines e 380 deltas. Erros de leitura 0; `overwritten=0`.
- 6/6 verificações de deck override conferidas, 40 cartas por jogador; speed 600; seed base 21361661.
- Substituição **somente** das três ocorrências de Gravity Bind (índice DAT 244, resource 1323) em cada deck pelos controles 151, 241 e 525, conservando os outros sete alvos em 3x por lado. Sem `0x0072(resourceId=1323)`.
- Original C11, com Gravity Bind: 3/3 duelos encerrados com ambos os decks vazios e LP positivos; novo controle: 3/3 com LP lado 0 zerado e decks não vazios. A configuração de seed é pareada, mas substituições permitem trajetórias diferentes de draws, RNG e IA.

| Duelo controle | Seq terminal | LP lado 0 / lado 1 | Deck lado 0 / lado 1 |
| --- | ---: | ---: | ---: |
| 1 | 601 | 0 / 3600 | 28 / 27 |
| 2 | 1276 | 0 / 6100 | 26 / 26 |
| 3 | 1596 | 0 / 5700 | 31 / 31 |

O contraste reforça um viés de stall causado ou favorecido por Gravity Bind. Não elevar a *proof of exclusive causality* nem decodificar `terminalFlags` a partir deste A/B.

## Revival Jam (P002, resourceId 1405, internalIndex 1060)

Quatro episódios de custo de 1000 LP temporalmente consistente com `0x0072(1405)` e `0x0043(1000)`:
- Duelo 2: `1044–1047`, lado 0 LP 5200→4200; `1114–1117`, 2700→1700; `1205–1208`, 1600→600.
- Duelo 3: `1585–1588`, LP 3500→2500.

Três transferências cemitério→campo com índice 1060, duelo 2:
- `1067–1071`, grave 9→8, slot 0, `positionWord=515`.
- `1130–1134`, grave 9→8, slot 0.
- `1220–1224`, grave 11→10, slot 0.

Esses episódios sustentam custo e retorno postergado; **não** registram explicitamente a tela de prompt, o clique Sim/Não, nem a política de decisão da CPU em modo demo. Não afirmar que houve supressão sistemática ou aceitação automática só pelo custo registrado.

**Hipótese de modelagem para testar no jogador humano:** quando a ocorrência é destruída por batalha e colocada no GY, o dono pode optar por pagar 1000 LP. Se aceita, debitar imediatamente e agendar tentativa de Special Summon na sua próxima Standby; se recusa, não agendar. Rulings históricos reforçam pagamento na ida ao GY, não na Standby. A execução no Joey 2004 precisa ser observada separadamente. Identidade de *ocorrência* do monstro, ownership vs controller, 0 LP/LP insuficiente, movimentação do GY antes da Standby, zonas cheias e condição de posição continuam pendentes. Não decodificar `positionWord=515` sem evidência visual/estática.

### Proposta arquitetural: escolha e política desacopladas

`DemoDuel` atualmente usa `PendingCardSelection`, `PendingFlipTarget`, `PendingSummonResponse` e `PendingAttackResponse`, mas não uma escolha genérica de custo Sim/Não. `DemoPhase` só expõe Main/Battle/End e `SandboxDuelRunner` é uma política muito limitada, não a IA nativa.

Sugestão (não implementada por este relatório):
1. Emitir uma `PendingEffectDecision` tipada e imutável (`decisionId`, `controller/owner`, `sourceOccurrence`, `reason`, `kind=PayLifePoints`, `amount=1000`, `choices=Accept/Decline`).
2. O Core valida e executa a resposta sem depender de interface; o aplicativo raylib apenas pergunta e encaminha a opção. Um driver de IA/replay resolve o mesmo contrato por política explícita e rastreável (não assumir Sim em todas).
3. Registrar evento de Standby mesmo se o loop visual ainda não possuir uma fase Standby; manter `PendingDelayedEffect` com a ocorrência do GY e turno/side devido. Revalidar estado na resolução. Não pagar novamente.
4. Produzir logs `decision_requested`, `decision_resolved` e `delayed_effect_resolved` para testes diferenciais. Políticas de autoplay devem ser explícitas e não bloquear o loop.
5. Validar por testes: Accept/Decline, 999/1000 LP, várias ocorrências, transferência do GY, campo cheio, decisão em ambos os lados, reentrada no GY, distinção de turnos e cancelamentos; comparar com execução humana original antes de declarar `Verified`.

## Equipamentos e janela de resposta

- Mage Power (1450): seq. 666–671, associação ao Goblin Attack Force índice 307, lado 1.
- United We Stand (1449): seq. 820–825, associação ao índice 307 lado 0; seq. 1481–1485, associação ao índice 885 lado 1.
- Offerings to the Doomed (1451) e Magic Drain (1321): janelas seq. 713–728 e 868–883. Há ordem de comandos sugerindo resposta, não prova completa de resolução de Chain LIFO nem descarte.
- Mudanças de ATK/DEF efetivo dos equipamentos e avanço forçado da próxima compra em Offerings ainda não foram demonstrados por estes deltas.

## Próximo corte

1. Preparar um duelo com um humano controlando Revival Jam; capturar a pergunta e escolher **Não**, depois **Sim** em outra tentativa, com mesma configuração, observando `0x0072`, `0x0043`, entrada/saída do cemitério e próximo Standby.
2. Analisar no Ghidra o callback P002, o gerador do prompt e o ramo CPU/demo (incluindo se há um único caminho de prompt suprimido por política).
3. Construir o modelo genérico de decisão/custos e de efeitos diferidos em branch nova, com fixtures sintéticas e nativas delimitadas.
4. Manter este resultado em documentação/fixtures de pesquisa e separar os níveis de evidência de qualquer promoção a `Verified`.
