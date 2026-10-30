# Man-Eater Bug — classificação FLIP e produtor de pedido

Análise estática em 2026-10-29, mesmo alvo fingerprintado no
[estudo de seleção](man-eater-bug-targeting.md). Endereços VA, base
0x00400000. Ghidra 12.2 em leitura; nenhuma execução do jogo.

## Resultado e alcance

Foi identificado um produtor **condicional** que reconhece a carta como
FLIP, agenda mudança de face e cria um pedido de efeito. A classificação,
o produtor e a escrita do bit de face estão ligados ao código. Ainda falta
demonstrar todas as entradas por interface/batalha e o timing de cada uma.

Isso também refina a análise de Parasite: o produtor genérico em 0x00597E90,
antes citado como origem da ativação, não demonstra sozinho o disparo FLIP.
O caminho específico abaixo admite tanto Man-Eater Bug quanto Parasite.

## Classificação no executável

**0x00567620(índice, modo)** converte índice→ID e usa comparações e tabelas
de salto para reconhecer um conjunto de cartas. **ID 500**, Man-Eater Bug,
e **ID 762**, Parasite Paracide, chegam ao retorno verdadeiro em
**0x00567716**. As descrições locais de ambas começam com FLIP.
O parâmetro de modo altera um caso de outro ID; não o tratamos como ignorado
universalmente. Neste produtor ele recebe zero.

| ID | Tabela de bytes | Tabela de ponteiros | Destino |
|---|---|---|---|
| 500 | 0x005677B0, índice `ID-0x1E9` | 0x005677A8 | 0x00567716, verdadeiro |
| 501 | mesma | mesma | 0x00567691, falso |
| 762 | 0x00567830, índice `ID-0x280` | 0x00567824 | 0x00567716, verdadeiro |
| 761 | mesma | mesma | 0x00567691, falso |

Os IDs vizinhos são controles da leitura das tabelas. Retorno falso nessa
classificação não significa ausência de outros efeitos para essas cartas.
O vínculo a essa categoria, nos casos examinados, está codificado no EXE.

## Mudança de face e pedido

**0x00574F70(lado, posição, permitir_pedido, origem)** consulta a palavra da
posição e sua classe de origem. A assinatura é descritiva e limitada ao
caminho examinado; alguns callers têm protótipos incompletos no banco.

Para posição ocupada, a rotina monta um código com
`((posição & 0x1F) | lado<<15 | 0x1620)<<16 | índice` e a chave de ocorrência.
Se a posição é de monstro (<5), a classificação 0x00567620 retorna verdadeiro
e **0x0057EE80** admite o pedido, registra essa possibilidade localmente.
0x0057EE80 reconstrói um pedido temporário e chama a validação genérica
**0x0057EAC0**; esta chega à disponibilidade/alvos por **0x0059DD90**.
A classificação FLIP, portanto, não garante ativação sem as outras condições.

A rotina emite **comando 0x80**, com lado no bit 15 do opcode e posição no
primeiro argumento de dados. Se o bit de face anterior (`registro+6 & 2`)
está zero, `permitir_pedido != 0` e a possibilidade local foi aceita, chama
**0x005BA490(código, chave, contexto)**. Esse helper valida por 0x0057EE80
novamente e, em caso positivo, chama **0x005BA310** para enfileirar o pedido.
Se o bit anterior estava definido, o ramo examinado emite evento 8 em vez
de entrar nessa chamada de criação. Não atribuímos evento 8 ao disparo FLIP.

O consumidor de 0x80 é **0x00407E60**, via normalização 86 decimal e stub
**0x005B9668**. No estado 2, quando o byte alto do último argumento é zero,
ele aplica **XOR 2 em uint16[registro+6]**. Se o resultado tem máscara 0x0002 definida,
grava também o contador 0x00A55D60 em registro+4 e incrementa esse contador.
Isso comprova a mudança concreta de face nesse ramo, além da apresentação.
Registro vazio e outros estados/flags têm caminhos próprios.

As operações são assíncronas. O produtor emite 0x80 e pode agendar o pedido
na mesma chamada; isso não significa que o callback já executou.
O [controlador documentado na etapa de custo](tribute-discard-cost.md)
processa operações pendentes antes de prosseguir na preparação/resolução.

## Chegada ao efeito

O registro 91 de Man-Eater Bug tem +12/+16 nulos, filtro em +8, seleção
em +20 e resolução em +4. Depois da preparação e das possíveis respostas,
**0x005BB720**, estado 6, chama **0x0059DEB0**, que despacha +4 para
**0x00585C80**. A partir daí, a
[revalidação de alvo](man-eater-bug-targeting.md) e a
[destruição até o estado](monster-destruction.md) já foram seguidas.

No estado 4 dessa máquina, cartas reconhecidas por 0x00567620 também passam
por uma consulta de ID **0x5FA**, que pode emitir comando 0xB1. O ID e os
caminhos de supressão foram seguidos na [continuação de Royal Command](royal-command.md).
Outras respostas/negações continuam fora do fechamento atual. Não concluímos
que uma carta marcada como FLIP sempre tenha seu efeito aplicado.

## O que foi validado

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_flip_activation.py
```

Esperado: dez linhas `PASS`. A cadeia revalida preservação dos originais/banco
e etapas anteriores. Nesta etapa confere os quatro destinos das tabelas,
instruções de classificação/emissão/escrita, despacho de 0x80 e descrição
local de Man-Eater Bug. **São verificações estáticas; nenhum teste nativo
do efeito foi executado.** Um erro de interpretação ainda pode sobreviver
às verificações de bytes e precisa de inspeção independente ou execução.

Evidências: [classificador e produtor](../evidence/man-eater-flip-trigger.txt),
[validação do pedido](../evidence/man-eater-flip-producer.txt),
[consumidor e resolução](../evidence/man-eater-flip-face-write.txt),
[instruções selecionadas](../evidence/man-eater-flip-selected-instructions.txt),
[escrita do bit](../evidence/man-eater-flip-write-instructions.txt),
[relatório](../evidence/flip-activation-validation.json).
As consultas exploratórias de elegibilidade/eventos ficam em
`docs/evidence/man-eater-flip-{events,eligibility,request-filter,classification}.txt`;
não bastam isoladamente para atribuir todos os eventos a FLIP.

## Confirmação dinâmica planejada

Primeiro, validar o layout em qualquer início de duelo: LP em
`0x00A55D64 + lado*0xD44`, contador da mão em `0x00A55D68 + lado*0xD44`
e entradas da mão em `0x00A56434 + lado*0xD44`. Os endereços são VAs do
alvo analisado e exigem conferir a base carregada no processo real.
Esse teste não depende de uma carta específica e confirma somente o layout.

Depois, quando uma situação de FLIP estiver disponível, observar uma parada
em 0x00574F70 ou 0x005BA490 e registrar argumentos, bit de face e pedido;
verificar se esse foi de fato o produtor usado pela ação do jogador.
Parar no callback 0x00585C80 permite comparar o alvo e sua chave antes da
destruição. Capturas antes/depois ou breakpoints são necessários para a
sequência; um dump isolado não demonstra causalidade.

O Ghidra Debugger oferece inspeção de execução e snapshots por meio de um
backend; a instalação 12.2 inclui agente x64dbg. Nenhuma sessão dinâmica foi
configurada ou testada aqui. A orientação atual é adiar essa etapa para o
Windows e manter a análise estática por enquanto. Documentação oficial:
[estado](https://ghidra.re/ghidra_docs/GhidraClass/Debugger/A4-MachineState.html)
e [snapshots](https://ghidra.re/ghidra_docs/GhidraClass/Debugger/A5-Navigation.html).
O snapshot guarda o estado capturado, não uma gravação automática integral
de toda a execução. Definir Windows/Wine e o backend é uma etapa separada.
