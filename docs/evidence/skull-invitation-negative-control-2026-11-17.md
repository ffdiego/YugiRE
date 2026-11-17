# Skull Invitation — controle negativo sem a carta (16/11/2026 local)

**Status:** comparação observacional controlada por substituição no deck; não prova interna da implementação.

## Procedência
Relatórios enviados pelo operador:
- Com Skull: `duels-20261117-020520-485960-pid20416.jsonl` (hash SHA256 reportado: `e76d67678a3843d14541fed5ec5d94aabbca603a129554ab2f46f69e27919994`).
- Sem Skull: `duels-20261117-033024-668792-pid10160.jsonl` (hash SHA256 reportado: `4c66383b82b47cf247540d48ca34c4e5f45dd016e4e9bea3640ef24bba9ceecc`).

O JSONL bruto não foi compartilhado nesta revisão; fontes são os `state-report.json` / `state-summary.md` da primeira captura e `state-report(1).json` / `state-summary(1).md` da segunda.

| Observação | Com Skull | Sem Skull |
|---|---:|---:|
| Duelos íntegros | 1 | 1 |
| Amostras válidas de estado | 382 | 325 |
| Baselines / deltas | 1 / 108 | 1 / 91 |
| Erros de leitura | 0 | 0 |
| Entradas sem amostra de estado | 18 | 18 |
| Alterações individuais | 178 | 159 |
| Skull vista em qualquer slot de campo | Presente, inclusive no CF de Rain | **0 em 325 amostras** |
| Incrementos unitários de Cemitério | 13 | 12 |
| Quedas de LP exatamente -300 | 14 | **0** |
| Pares observacionais +1 Cemitério → -300 LP (mesmo lado, até 6 seq) | 13 | **0** |

A ausência observada em 325 amostras é forte evidência de que Skull não esteve em campo, mas não é observação atômica entre todos os instantes. No controle, houve 12 oportunidades distintas de aumentos de Cemitério sem qualquer queda de LP de 300.

## Mesmo efeito principal de Rain of Mercy nos dois casos
- Com Skull: Rain CF seq335; LP +1000/+1000 seq340; Rain índice969 sai do slot7 com +1 graveyard lado0 seq344; 300 LP comandado seq346 e delta real -300 lado0 seq348.
- Sem Skull: Rain CF seq302; LP +1000/+1000 seq307, lado0 7500→8500 e lado1 4200→5200; Rain sai do slot6 com +1 graveyard lado0 seq311; **nenhum comando 300 nem delta -300 na janela**.

O efeito principal de recuperação é observado em ambos. O efeito adicional de dano desaparece na ausência de Skull. Outros danos continuam ocorrendo no controle (ex.: -1300 lado0 seq105, -400 lado1 seq136), então a leitura de LP não ficou inoperante.

## Interpretação e decisão
- **Evidência forte e convergente de comportamento dependente de Skull Invitation** em dois duelos contrastantes; compatível com regra de 300 LP ao dono de cada carta enviada ao Cemitério.
- **Não prova causalidade completamente isolada:** trocar a carta muda o deck e pode mudar o fluxo da IA mesmo com a mesma seed; um duelo por condição; gatilhos e identidade real da carta no Cemitério ainda não mapeados; amostras de estado são não atômicas.
- A perda extra -300 do duelo positivo na seq455 lado1 permanece sem pareamento simples, e não deve ser inventado um gatilho.
- A coluna `readyForFullCsharpComparison` não deve passar para true só por esse contraste.

**Próxima meta:** validar a pipeline integrada de testes/CI; depois executar coortes de cartas mágicas por família em decks catalogados e com capturas até 3 duelos/1 worker no piloto de state trace. Separar descoberta de paridade de engine.
