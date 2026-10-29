# Sparks e Ookazi — comparação de efeitos simples

Análise estática em 2026-10-29, no mesmo alvo fingerprintado de
[Parasite Paracide](parasite-paracide.md). Todos os endereços são VA,
image base `0x00400000`. Nenhum jogo executado nem alteração de banco persistida.

## Resultado

As duas cartas compartilham **o mesmo callback de resolução** e **o mesmo
callback de preenchimento de destinatário**. Dentro do callback compartilhado,
o ID seleciona constantes diferentes. Isso demonstra reutilização de código
entre cartas; não há uma função exclusiva para cada uma dessas duas cartas.

| Carta | Índice | ID de recurso | Registro na tabela | Dano |
|---|---:|---:|---:|---:|
| Sparks | 755 | 342 / 0x156 | 66 | 200 |
| Ookazi | 557 | 345 / 0x159 | 69 | 800 |

Os dois registros de 24 bytes contêm o ID em +0, **0x00584E60 em +4**,
zeros em +8/+12/+16 e **0x00593CC0 em +20**. Nomes, IDs e descrições foram
conferidos nos recursos locais. As descrições especificam dano direto ao
oponente, concordando com o fluxo abaixo.

## Destinatário e resolução

0x0059DE70 resolve o registro por índice→ID e chama o ponteiro em +20.
Para ambas as cartas, **0x00593CC0** calcula
`oponente = 1 - (uint8[pedido+2] & 1)` e chama
**0x00592A50(pedido, oponente)**, retornando 1.

0x00592A50 adiciona uma palavra a partir de **pedido+6**, usando os bits
13–15 de pedido+4 como contador de entradas. No primeiro elemento, isso
grava o seletor de oponente em pedido+6. Essa parte do pedido é diferente
dos bits 5–12 que transportam a chave de ocorrência no caso de Parasite.

O callback principal **0x00584E60**:

1. Verifica `uint8[pedido+4] & 4`; se estiver definido, evita as chamadas
   de dano do trecho examinado. Não atribuímos a esse bit o nome “negação”.
2. Converte `uint16[pedido]` para ID usando 0x004022E0.
3. Para 342, chama `0x005784E0(uint8[pedido+6] & 1, 200)`.
4. Para 345, chama o mesmo helper com **800**.
5. As instruções desses dois caminhos terminam com **AX=0**. O pseudocódigo
   preserva artefatos nos bits altos do retorno; não tratá-los como contrato.

O helper 0x005784E0 é o mesmo usado por Parasite para 1000: emite comando
0x43, cujo consumidor encaminha o valor como delta negativo ao atualizador de
LP. A [análise de Parasite](parasite-paracide.md) já seguiu esse caminho até a
escrita saturada em zero; ele não foi inferido somente do texto das cartas.

## Implicação para a arquitetura

Há pelo menos três camadas comprovadas:

- Registro por ID com slots de callbacks, alguns nulos.
- Callback compartilhado contendo seleção por ID e constantes de efeito.
- Helpers/comandos comuns que aplicam mudanças de estado e apresentação.

Portanto, “tabela de callbacks” e “condições por ID no código” coexistem.
Os valores 200/800 são imediatos do executável nesses caminhos, não campos
de CARD_Prop. Isso não exclui outros efeitos implementados por dados ou por
outros mecanismos; a conclusão é limitada às cartas examinadas.

## Evidências e reprodução

- [Callback principal](../evidence/simple-lp-card-callbacks.txt).
- [Instruções de seleção por ID](../evidence/simple-lp-card-targets.txt).
- [Preparação do destinatário e caminho Sparks](../evidence/simple-lp-card-selected-branches.txt).
- [Gravação da palavra de destinatário e caminho Ookazi](../evidence/simple-lp-card-target-write.txt).
- [Slot de callback +20](../evidence/simple-lp-target-callback-dispatch.txt).
- [Identidades, registros e descrições locais](../evidence/simple-lp-cards-validation.json).

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_simple_lp_cards.py
```

Esperado: sete linhas `PASS`. Revalida os hashes e o caminho comum já conhecido,
confere nomes/IDs, os dois registros e os imediatos 200/800 nos offsets
correspondentes. Não executa o alvo. A validação dinâmica de destinatário,
negação e ordem dos eventos permanece futura, como nos
[experimentos de Parasite](parasite-dynamic-checks.md).
