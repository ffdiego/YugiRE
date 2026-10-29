# Tribute to The Doomed — custo, seleção e resolução

Análise estática em 2026-10-29, executável SHA256
`db8fe7b7ea4b12f18a9bb1777b5f5b8eab254011db2a52571a0264725505b6de`.
Endereços são VA, image base 0x00400000. Ghidra 12.2 em leitura;
nenhuma execução do jogo ou alteração persistente do banco.

## Resultado

Tribute separa a verificação de disponibilidade, a preparação do descarte,
a seleção do monstro e a resolução. O descarte é solicitado pelo callback
em **registro+16**; a condição **0x00583510**, encontrada dentro da resolução,
trata de **Big Shield Gardna**. Portanto, ela não era o caminho de cobrança
do custo que ainda procurávamos.

O slot +16 é uma etapa de preparação que, nestas cartas, agenda custo;
não atribuímos semântica universal de custo a todos os registros da tabela.

| Carta | ID | Registro | Callback +16 | Quantidade |
|---|---:|---:|---|---:|
| Tribute to The Doomed | 0x3FF | 187 | 0x0057D7C0 | 1 |
| Magic Jammer | 0x405 | 193 | 0x0057D7C0 | 1 |
| Final Destiny | 0x42B | 224 | 0x0057D7C0 | 5 |
| Darkness Approaches | 0x430 | 229 | 0x0057D7C0 | 2 |

Identidades, descrições e registros conferidos nos recursos locais e no
executável. As quantidades são imediatos selecionados por ID dentro do
callback compartilhado, não números extraídos do texto durante o jogo.
As resoluções das outras três cartas não foram analisadas nesta etapa.

## Disponibilidade e exclusões da mão

O slot **+12 de Tribute** aponta para **0x00580D20**. Ele chama
`0x0057F130(lado, 1, índice_excluído, 0, 0)` e normaliza o retorno para 0/1.
Se o terceiro argumento recebido pelo callback for zero, o índice excluído
é zero; caso contrário, é `uint16[pedido]`. O significado de todos os contextos
que fornecem esse argumento não foi reconstruído.

0x0057F130 percorre a mão:

```text
contador: byte[0x00A55D68 + (lado & 1)*0xD44]
entradas: uint32[0x00A56434 + (lado & 1)*0xD44 + posição*4]
índice:   entrada & 0xFFF
```

Ele exige uma quantidade suficiente de entradas elegíveis. Ao encontrar o
índice excluído, ignora **uma ocorrência** e zera a exclusão: outra cópia da
mesma carta pode continuar elegível. Também rejeita índice vazio e associação
de Parasite identificada por **0x00570BE0**. Há filtros opcionais por propriedades
e classe que Tribute passa como zero.

A consulta **0x005699C0(0x49C)** também pode excluir entradas de classe
0x16. O ID é **Anti-Spell Fragrance**. Registramos a condição concreta;
não inferimos a regra de descarte somente pela descrição dessa carta nem
generalizamos a restrição para outras situações.

O dispatcher **0x0059DD90** chama primeiro o slot +12. Se ele retornar zero,
rejeita; se retornar um, continua procurando ao menos um candidato pelo slot
+8. Assim, no caminho examinado, disponibilidade para pagar e existência de
alvo são condições separadas. A [seleção de monstro](man-eater-bug-targeting.md)
já documentou o slot +8 de Tribute.

## Solicitação e execução do descarte

Na máquina **0x005BAFD0**, o estado 3 chama **0x0059DE30**, que despacha
registro+16. Os estados 4/5 tratam depois da preparação de alvo por +20.
O callback **0x0057D7C0** escolhe a quantidade por ID e chama:

```text
0x005BCE00(lado, quantidade, 0, 0)
    → 0x005BCC70(lado, tipo=2, quantidade, flags)
```

As flags incluem **0x10** e, se a consulta de ID 0x49C for positiva, **4**.
0x005BCC70 grava o descritor em **0x00A57810/0x00A57814** e chama
0x005BCCC0 para ativá-lo. No descritor, lado fica no bit 2, tipo nos bits
3–8 e quantidade no uint16 em +2. O callback retorna 1 após agendar;
esse retorno sozinho não significa que a carta já saiu da mão.

O processador em **0x005BC9B0** usa a tabela **0x005BCC34**. Tipo 2 seleciona
**0x005BCA42 → 0x005A91F0**. Este inicializa o contador de escolhas em
**0x00A55D3C** e encaminha a seleção por ramos dependentes das flags de modo.
No ramo com seleção automática, chama **0x00575A10** e decrementa esse contador.
No caminho de interface, **0x005A9060 → 0x005A8E10** filtra a mão, confirma a
escolha e usa o mesmo helper porque a flag 0x10 está presente. O decremento
fica em 0x005A9060. Não foi necessário dar nomes humano/IA a todos os bits.

O controlador **0x005BC620** consulta esse processador antes de
**0x005AB820** e **0x005BBE40**. Enquanto o descritor está ativo, o processador
retorna 1 e impede esse controlador de continuar até a preparação dos efeitos.
A prioridade de seleções e operações pendentes explica como o callback pode
agendar trabalho e retornar imediatamente. Sincronização entre jogadores e
todos os estados de interface não foram reimplementados.

O caminho de movimentação é:

```text
0x00575A10(lado, posição_na_mão)
    → 0x005759A0 → 0x00575AF0
    → comando 0x8E → 0x00408EE0
```

No caso comum, o comando codifica região de origem **11**, lado e chave da
palavra da mão; o destino é **14**. O consumidor resolve a chave por
0x005687D0, confere o local encontrado, copia a palavra e chama
**0x00570790 → 0x005708D0** para retirar/inserir. Para a mão, a retirada chama
**0x0056CFC0**, que zera o índice, e **0x0056D190**, que compacta as entradas
e reduz o contador. A inserção em 14 usa o helper de cemitério já seguido
na [análise de destruição](monster-destruction.md).

Se a consulta de **Banisher (0x453)** for positiva, 0x00575AF0 usa
**0x00575A80**, que emite o mesmo 0x8E com região de destino **15**.
Assim, descarte e destruição de campo têm produtores diferentes, embora
reutilizem o mecanismo de remoção/inserção e as listas de destino.
Há também um ramo de Graverobber em 0x00575A10 e eventos específicos em
0x00575AF0; eles não foram fechados neste estudo de custo.

## A condição extra na resolução: Big Shield Gardna

O callback compartilhado de destruição chama **0x00583510** para IDs
0x3FF e 0x4BB. O helper verifica **ID 0x4B1**, identificado como Big Shield
Gardna, posição <5, flag `registro+6 & 2` ausente e consulta de
0x0056B440 igual a zero. Se entrar no ramo, chama helpers de estado e
apresentação, emite **0xB1**, aplica **`pedido[4] |= 0x0E`** e retorna zero.
Isso interrompe a resolução antes de chamar o helper de destruição.

A descrição local de Gardna relata a negação de uma magia que a alveje
enquanto está virada para baixo. O ramo é compatível com essa descrição;
não atribuímos os nomes de todas as flags nem reconstruímos cada helper de
mudança de orientação. O custo já foi agendado na preparação anterior.
Não há reembolso nesse trecho examinado; isso não demonstra a política de
reembolso de todas as formas de negação do jogo.

## Reprodução e próximos testes

```bash
PYTHONDONTWRITEBYTECODE=1 python3 YugiRE/scripts/validate_tribute_cost.py
```

Esperado: nove linhas `PASS`. Valida os originais e banco, quatro registros,
dispatch de quantidades 1/2/5, seleção tipo 2, despacho de 0x8E, trechos de
instruções e oito exemplos do modelo de elegibilidade da mão. O modelo é
transcrito; não executa nem prova equivalência com funções nativas.

Evidências: [callbacks](../evidence/tribute-cost-callbacks.txt),
[pré-condição](../evidence/tribute-cost-precondition.txt),
[descritor e ordem de callbacks](../evidence/tribute-discard-request.txt),
[ativação do descritor](../evidence/tribute-discard-scheduler.txt),
[interface e mensagens](../evidence/tribute-discard-processing.txt),
[seleção tipo 2](../evidence/tribute-discard-selection.txt),
[aplicação](../evidence/tribute-discard-application.txt),
[movimentação](../evidence/tribute-discard-movement.txt),
[prioridade do controlador](../evidence/tribute-discard-priority.txt),
[produtor e compactação](../evidence/tribute-discard-command.txt),
[consumidor](../evidence/tribute-discard-consumer.txt),
[instruções](../evidence/tribute-cost-selected-instructions.txt),
[validação](../evidence/tribute-cost-validation.json).

Execução futura: confirmar seleção/descarte de uma carta comum e depois a
resolução do alvo; comparar contadores, entradas e descritor. Para testar o
helper compartilhado, qualquer uma das quatro cartas disponíveis pode servir,
respeitadas suas próprias condições de ativação. Gardna, Banisher e negação
ficam como cenários adicionais, sem exigir montá-los para a primeira captura.
Um dump genérico de início de duelo pode validar o layout da mão, mas não
resolve sozinho a ordem do pagamento; não foi solicitado nesta etapa.
