# Viagem de carta e sequência de batalha

O recorte estático usa o SQLite v15, as coletas 76–78 e o disassembly
do PE de SHA-256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
A amostra de Change of Heart e tributo vem da
[captura Windows V18.6](native-basic-placement-cadence-2026-11-28.md#v186--change-of-heart-da-mão-e-tribute-summon).

## Controle e cemitério

| Família | Origem do pedido ou consumidor | Evidência e lacuna |
| --- | --- | --- |
| Controle campo a campo | `83 → 59 → 00571670` pelo stub `005B968B`. Em `00576B30`, o ramo `00576D97–00576D9F` seleciona 83 quando o destino é uma posição de campo, após um pedido 8D. | O subestado 1 monta descritores de origem e destino e chama `0040CDB0` em `0057185C`. A confirmação do comando passa por outra chamada do consumidor, que copia o registro e limpa busy. A amostra Change of Heart mostra giro de 180°, face revelada e 8 updates de viagem com confirmação no nono. Falta observar retorno, equipamento, defesa e oculto. |
| Magia resolvida ao GY | `8F → 63 → 00409240` pelo stub `005B96BD`. O subestado 0 remove/insere pelas rotinas `00570790/005708D0` e pede viagem em `004096C7`; o subestado 1 limpa busy. | Na captura de Change of Heart há nova viagem de 8 updates com confirmação e som `1000000F` (`card_move.wav`), após a transferência do monstro. Confirmar casos de magia contínua, equipamento e resolução cancelada. |
| Destruição em batalha | `00577CF0` produz `7C` com argumentos do lado afetado; consumidor `00405190`, Explosion Draw `004514B0`. | Levar a passagem até o destino final. Verificar desvios de dependências, faces e sobreposição da remoção com a cauda visual. |
| Destruição por efeito | `00576B30` tem ramos para 8F e 79. `79 → 00404B40` usa Break. | A causa de remoção é necessária para identificar a sequência completa. Ver o [contrato Break](native-break-field-review-2026-11-25.md). |
| Tributo | A gravação V18.6 registrou `95` e som `1000000C`. O despacho de 95 aponta para `004074F0`. | Correlacionar o próprio efeito do tributo, o eventual pedido `0040CDB0` e a confirmação do destino. |
| Descarte da mão | `00575970 → 005759A0 → 00575AF0` produz `8E` em `00575B60`, com rota alternativa em `00575ADF`. Despacho `005B96B8 → 00408EE0`, com pedido de viagem `00409211`. | Estrutura de pedido comum com 83 e 8F. Faltam amostras equivalentes dos dois lados, som, face e destino na etapa de descarte. |

`0040CDB0` recebe índice interno da carta e os dois descritores. Os
descritores carregam lado físico, zona/posição e bits de apresentação.
O lado de destino e o proprietário devem permanecer dados independentes
na investigação. O exemplo de Change of Heart fixa uma rota do
controlador atual para outro lado, com a propriedade da carta preservada
no duelo. O caminho de retorno ainda precisa de amostra equivalente.

### Tomada e retorno do controle

O perfil P133 do SQLite liga Change of Heart (resource ID 1027,
índice interno 300) a `00589200`. No assembly, `0058929D`
chama `00574900`, que pede 87 em `0057494F`. A mesma
rotina de efeito chama `005747E0` em `005892BF`, que pede 86.
Esses dois pedidos manipulam associações. A viagem observada
chega por 83.

O segundo produtor de 83 é `00575750`. Em `005757DB`,
só enfileira quando a origem está ocupada e o destino está livre.
O SQLite v16 mostra a chamada `005BA060 → 005743F0 → 00575750`.
`005743F0` consulta `0056C210` para escolher o lado da carta e
`0056A030` para achar espaço. Se faltar espaço, chama `00577840`
em vez de pedir viagem. A escolha de lado em `0056C210` usa o
campo do proprietário com um bit de estado como base e pode ser
sobrescrita por associações. O ramo do tipo 3 compara o ID 1027
em `0056C333`. Isso oferece o mesmo caminho de viagem para o
lado escolhido após uma associação mudar. A expiração no fim
do turno ainda precisa ser correlacionada com o 83 de retorno.

Para Snatch Steal, `005899A0 → 00574990 → 005749B0` chega ao
pedido B2 de equipamento, com ramos preparatórios 88 e 8A.
`0056C210` reconhece o resource ID 1068 por associação de
equipamento do tipo 1. O perfil activation registra 86, 87,
88, 8A e B2 ao lado de 83 e dos dois descritores. Isso permite
verificar a ordem em campo cheio, troca em defesa e remoção
do equipamento sem deduzir timing dos pedidos de associação.
Em `00571829–0057183C`, o destino de 83 herda os dois bits
superiores do descritor de origem e pode limpar o bit 14
conforme o slot. A apresentação de ocultos durante a viagem
requer um trace pareado.

## Batalha

O [contrato de ataque e LP](native-attack-life-routing-2026-11-24.md)
mostra três pontos de sincronização: a seleção orienta a pose do objeto,
`004142C0` escolhe voo/impacto/lâmina parcial a partir do estado e
contador, e o Update `00414110` pede alteração de LP em
`0047C4F0` na entrada do estado 7 quando há delta. O resultado
lógico usa comandos próprios para revelação e remoção. A rota
`005B9470` permite distinguir a execução de um handler do término
do comando pelo bit 14 de `00A558AC`. A presença de Draw após uma
mudança de estado da fila, observada para Break, torna necessária a
correlação entre a atividade do objeto e busy.

A auditoria do encadeamento completo segue aberta. Registrar a seleção
do alvo, a primeira chegada ao impacto, o início e commit da revelação
em alvo oculto, pedido/Draw de LP, primeiro Draw de destruição,
confirmação de cemitério e o primeiro input subsequente que o jogo
aceita. Comparar com ataque direto e alvo já revelado.
[Coleta Windows](../../scripts/pending/README.md#v09v10-v18v31-e-v32--validação-em-execução).
